#include "TcpServer.h"

#include <ws2tcpip.h>

#include <algorithm>

namespace rank
{
    namespace
    {
        // select 한 번에 넣을 수 있는 소켓 수(FD_SETSIZE)에서 리스너 몫을 뺀 만큼만 동시 연결을 받음
        constexpr size_t MaxConnections = FD_SETSIZE - 1;

        // 소켓을 닫고 INVALID_SOCKET으로 표시 - 두 번 닫아도 안전
        void CloseSocket(SOCKET& socket)
        {
            if (socket != INVALID_SOCKET)
            {
                closesocket(socket);
                socket = INVALID_SOCKET;
            }
        }

        // 논블로킹으로 바꿈 - recv/send/accept가 기다리지 않고 바로 돌아옴 (할 게 없으면 WSAEWOULDBLOCK)
        bool SetNonBlocking(SOCKET socket)
        {
            u_long enabled = 1;
            return ioctlsocket(socket, FIONBIO, &enabled) == 0;
        }
    }

    TcpServer::TcpServer(RankingStore& store, Logger logger)
        : store(store), logger(std::move(logger))
    {
    }

    TcpServer::~TcpServer()
    {
        Stop();
    }

    // Start - 리스닝 소켓을 열고 포트에 묶음
    bool TcpServer::Start(uint16_t requestedPort)
    {
        listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (listener == INVALID_SOCKET)
        {
            return false;
        }

        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_addr.s_addr = htonl(INADDR_ANY);
        address.sin_port = htons(requestedPort);

        if (bind(listener, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0
            || listen(listener, SOMAXCONN) != 0
            || SetNonBlocking(listener) == false)
        {
            CloseSocket(listener);
            return false;
        }

        // 0을 요청했으면 OS가 고른 포트를 읽어 둠
        int length = sizeof(address);
        getsockname(listener, reinterpret_cast<sockaddr*>(&address), &length);
        port = ntohs(address.sin_port);
        return true;
    }

    // Stop - 모든 연결과 리스너를 닫음
    void TcpServer::Stop()
    {
        for (Connection& connection : connections)
        {
            CloseSocket(connection.socket);
        }

        connections.clear();
        CloseSocket(listener);
    }

    // Poll - 한 바퀴: 기다릴 소켓 등록 → select 대기 → 새 연결 수락 → 연결별 수신·송신·시간 초과 → 끊을 연결 정리
    void TcpServer::Poll(int timeoutMs)
    {
        if (listener == INVALID_SOCKET)
        {
            return;
        }

        // 1. 이번에 지켜볼 소켓 등록 - 리스너(새 연결)와 모든 연결(수신)
        fd_set readable;
        fd_set writable;
        FD_ZERO(&readable);
        FD_ZERO(&writable);
        FD_SET(listener, &readable);
        for (const Connection& connection : connections)
        {
            FD_SET(connection.socket, &readable);
            // 보낼 게 남은 연결만 쓰기 대기 - 항상 넣으면 select가 바로 돌아와 CPU를 태움
            if (connection.outgoing.empty() == false)
            {
                FD_SET(connection.socket, &writable);
            }
        }

        timeval timeout{};
        timeout.tv_sec = timeoutMs / 1000;
        timeout.tv_usec = (timeoutMs % 1000) * 1000;

        // 2. 어느 소켓이든 준비될 때까지(최대 timeoutMs) 대기. Windows의 select는 첫 인자를 쓰지 않음
        int ready = select(0, &readable, &writable, nullptr, &timeout);
        if (ready == SOCKET_ERROR)
        {
            logger("select 실패: " + std::to_string(WSAGetLastError()));
            return;
        }

        // 3. 새 연결을 먼저 받아 둠 - 아래 반복 도중에 vector가 늘어나 참조가 깨지지 않게
        if (FD_ISSET(listener, &readable))
        {
            AcceptAll();
        }

        // 4. 연결마다 받을 것 받고, 보낼 것 보내고, 너무 오래 조용하면 끊기로 표시
        auto now = std::chrono::steady_clock::now();
        for (Connection& connection : connections)
        {
            if (FD_ISSET(connection.socket, &readable))
            {
                Receive(connection);
            }

            if (connection.closing == false && FD_ISSET(connection.socket, &writable))
            {
                Send(connection);
            }

            if (now - connection.lastActivity > idleTimeout)
            {
                logger("유휴 시간 초과로 연결 종료");
                connection.closing = true;
            }
        }

        // 5. 끊기로 한 연결은 반복이 끝난 뒤 한 번에 정리 (반복 중에 지우면 순회가 깨짐)
        CloseMarked();
    }

    // AcceptAll - 대기 중인 새 연결을 모두 받음. 한도를 넘으면 받자마자 닫음
    void TcpServer::AcceptAll()
    {
        // 논블로킹 리스너라 대기 중인 연결이 없으면 WSAEWOULDBLOCK으로 빠져나옴
        for (;;)
        {
            SOCKET accepted = accept(listener, nullptr, nullptr);
            if (accepted == INVALID_SOCKET)
            {
                return;
            }

            if (connections.size() >= MaxConnections || SetNonBlocking(accepted) == false)
            {
                logger("동시 연결 수 초과로 연결 거절");
                closesocket(accepted);
                continue;
            }

            Connection connection;
            connection.socket = accepted;
            connection.lastActivity = std::chrono::steady_clock::now();
            connections.push_back(std::move(connection));
        }
    }

    // Receive - 받은 바이트를 연결의 FrameReader에 쌓고, 완성된 프레임을 모두 처리
    void TcpServer::Receive(Connection& connection)
    {
        uint8_t buffer[1024];
        int received = recv(connection.socket, reinterpret_cast<char*>(buffer), sizeof(buffer), 0);
        if (received == 0)
        {
            // 상대가 정상 종료 - 받은 것까지는 이미 처리했음
            connection.closing = true;
            return;
        }

        if (received == SOCKET_ERROR)
        {
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                connection.closing = true;
            }

            return;
        }

        connection.lastActivity = std::chrono::steady_clock::now();
        connection.reader.Append(buffer, static_cast<size_t>(received));

        // 한 번에 여러 프레임이 붙어 올 수 있으므로 더 꺼낼 게 없을 때까지 반복
        std::vector<uint8_t> payload;
        for (;;)
        {
            FrameReader::Result result = connection.reader.Next(payload);
            if (result == FrameReader::Result::NeedMore)
            {
                return;
            }

            if (result == FrameReader::Result::Error)
            {
                logger("잘못된 프레임 길이로 연결 종료");
                connection.closing = true;
                return;
            }

            Handle(connection, payload);
            if (connection.closing)
            {
                return;
            }
        }
    }

    // Handle - 제출 하나 처리: 해석 → 값 검사 → 저장 → ACK를 보낼 버퍼에 넣음
    // 순서가 중요함 - ACK를 먼저 보내고 저장하다 꺼지면, 클라이언트는 이미 지운 기록을 서버도 잃음
    void TcpServer::Handle(Connection& connection, const std::vector<uint8_t>& payload)
    {
        RunRecord record;
        if (DecodeSubmit(payload.data(), payload.size(), record) == false)
        {
            // runId도 믿을 수 없으니 ACK를 보내지 않고 끊음
            logger("해석할 수 없는 메시지로 연결 종료");
            connection.closing = true;
            return;
        }

        SubmitAck ack;
        ack.runId = record.runId;
        ack.reason = Validate(record);

        if (ack.reason != RejectReason::None)
        {
            ack.status = AckStatus::Rejected;
            ack.total = store.Count();
            logger("거부 " + ToHex(record.runId) + " reason=" + std::to_string(static_cast<int>(ack.reason)));
        }
        else
        {
            RankingStore::Outcome outcome;
            if (store.Submit(record, outcome) == false)
            {
                // 저장하지 못했는데 ACK를 보내면 클라이언트가 기록을 지워 버림 - 끊어서 다음에 다시 보내게 함
                logger("기록 저장 실패로 연결 종료");
                connection.closing = true;
                return;
            }

            ack.status = outcome.status;
            ack.rank = outcome.rank;
            ack.total = outcome.total;
            logger(std::string(outcome.status == AckStatus::Duplicate ? "중복 " : "저장 ") + ToHex(record.runId)
                + " wave=" + std::to_string(record.wave) + " rank=" + std::to_string(ack.rank)
                + "/" + std::to_string(ack.total));
        }

        // 바로 보내 보고, 못 다 보낸 나머지는 다음 Poll에서 이어 보냄
        std::vector<uint8_t> frame = MakeFrame(EncodeAck(ack));
        connection.outgoing.insert(connection.outgoing.end(), frame.begin(), frame.end());
        Send(connection);
    }

    // Send - 보낼 버퍼에서 보낼 수 있는 만큼 보냄
    void TcpServer::Send(Connection& connection)
    {
        if (connection.outgoing.empty())
        {
            return;
        }

        int sent = send(connection.socket, reinterpret_cast<const char*>(connection.outgoing.data()),
            static_cast<int>(connection.outgoing.size()), 0);
        if (sent == SOCKET_ERROR)
        {
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                connection.closing = true;
            }

            return;
        }

        // 일부만 나갔으면 나머지는 소켓이 다시 쓰기 가능해질 때 이어서 보냄
        connection.outgoing.erase(connection.outgoing.begin(), connection.outgoing.begin() + sent);
    }

    // CloseMarked - closing 표시된 연결의 소켓을 닫고 목록에서 뺌
    void TcpServer::CloseMarked()
    {
        for (Connection& connection : connections)
        {
            if (connection.closing)
            {
                CloseSocket(connection.socket);
            }
        }

        connections.erase(std::remove_if(connections.begin(), connections.end(),
            [](const Connection& connection) { return connection.socket == INVALID_SOCKET; }), connections.end());
    }
}
