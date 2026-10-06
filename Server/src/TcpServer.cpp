#include "TcpServer.h"

#include <ws2tcpip.h>

#include <algorithm>

namespace rank
{
    namespace
    {
        // select 한 번에 넣을 수 있는 소켓 수(FD_SETSIZE, Windows 기본 64)에서
        // 리스너(새 연결을 받는 소켓, listener) 1개를 뺀 만큼만 동시 연결을 받음 (63개)
        constexpr size_t MaxConnections = FD_SETSIZE - 1;

        // 소켓을 닫고, 핸들 변수를 INVALID_SOCKET(소켓 없음)으로 바꿈
        void CloseSocket(SOCKET& socket)
        {
            if (socket != INVALID_SOCKET)
            {
                closesocket(socket);
                socket = INVALID_SOCKET;
            }
        }

        // SetNonBlocking - 넘겨받은 소켓(리스너 또는 연결 소켓)을 논블로킹 모드로 바꿈, 실패하면 false
        //   블로킹(기본) - recv/send/accept가 할 일이 생길 때까지 그 자리에서 멈춰 기다림
        //   논블로킹 - 할 일이 없으면 기다리지 않고 바로 돌아옴 (실패로 돌아오고 오류 코드는 WSAEWOULDBLOCK)
        // 스레드 하나로 모든 연결을 처리하므로, 한 소켓에서 멈추면 다른 연결도 전부 멈춤 -> 논블로킹이 필요함
        // (ioctlsocket에 FIONBIO = 1을 넘기면 논블로킹, 0이면 블로킹)
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

    // Start - 리스닝 소켓(새 연결을 받는 전용 소켓)을 열고 포트에 묶음
    // 주소는 INADDR_ANY = 이 PC의 모든 네트워크 주소에서 받음 (다른 PC에서도 접속 가능)
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

    // Stop - 모든 연결과 리스너를 닫음 (아직 못 보낸 ACK가 있어도 그대로 닫음)
    // 기록은 이미 저장돼 있으므로, ACK를 못 받은 클라이언트가 다음에 다시 보내면 Duplicate로 답해 줌
    void TcpServer::Stop()
    {
        for (Connection& connection : connections)
        {
            CloseSocket(connection.socket);
        }

        connections.clear();
        CloseSocket(listener);
    }

    // Poll 진행 순서
    //   1. 지켜볼 소켓 등록 (리스너, 모든 연결)
    //   2. select로 할 일이 생길 때까지 대기 (최대 timeoutMs)
    //   3. 새 연결 수락
    //   4. 연결마다 수신·송신, 오래 조용한 연결은 closing 설정
    //   5. closing이 설정된 연결 닫기
    void TcpServer::Poll(int timeoutMs)
    {
        if (listener == INVALID_SOCKET)
        {
            return;
        }

        // 1. 이번에 select가 지켜볼 소켓 등록 - 새 연결이 왔는지 볼 리스너와, 받을 데이터가 왔는지 볼 모든 클라이언트 연결
        //    (보낼 게 남은 연결은 쓰기 가능해졌는지도 함께 봄)
        fd_set readable;
        fd_set writable;
        FD_ZERO(&readable);
        FD_ZERO(&writable);
        FD_SET(listener, &readable);
        for (const Connection& connection : connections)
        {
            FD_SET(connection.socket, &readable);
            // write 대기는 보낼 게 남은 연결만 등록함
            // (다 등록하면 소켓은 거의 늘 쓰기 가능이라 select가 바로 돌아와, Poll이 쉬지 않고 돌며 CPU를 낭비함)
            if (connection.outgoing.empty() == false)
            {
                FD_SET(connection.socket, &writable);
            }
        }

        timeval timeout{};
        timeout.tv_sec = timeoutMs / 1000;
        timeout.tv_usec = (timeoutMs % 1000) * 1000;

        // 2. 어느 소켓이든 준비될 때까지(최대 timeoutMs) 대기
        // (Windows의 select는 첫 인자를 쓰지 않음)
        int ready = select(0, &readable, &writable, nullptr, &timeout);
        if (ready == SOCKET_ERROR)
        {
            logger("select 실패: " + std::to_string(WSAGetLastError()));
            return;
        }

        // 3. 새 연결은 4단계 반복 전에 먼저 받아 둠
        //    (반복 도중에 받으면 push_back으로 vector가 커지며 원소가 다른 메모리로 옮겨질 수 있음
        //     -> 반복 중인 connection이 옛 위치를 가리키게 됨)
        if (FD_ISSET(listener, &readable))
        {
            AcceptAll();
        }

        // 4. 클라이언트 연결마다 받을 데이터가 왔으면 받고, 보낼 ACK가 남았으면 보냄
        //    idleTimeout 동안 아무것도 안 온 연결은 closing 설정 (실제로 닫는 건 5단계)
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

        // 5. closing이 설정된 연결을 한꺼번에 닫고 목록에서 뺌
        //    (4단계 순회 도중에 끊긴 연결을 지우면 순회가 망가지므로, 표시만 해 두고 여기서 지움)
        CloseMarked();
    }

    // AcceptAll - 접속을 기다리는 새 연결을 한 번에 모두 받아 connections에 추가
    // 동시 연결 수가 한도(63개)를 넘었거나 논블로킹으로 바꾸지 못한 연결은 받자마자 닫음
    void TcpServer::AcceptAll()
    {
        // 기다리는 연결이 없을 때까지 accept를 반복
        // (리스너가 논블로킹이라 더 받을 게 없으면 멈추지 않고 WSAEWOULDBLOCK 실패로 돌아와 빠져나감)
        for (;;)
        {
            SOCKET accepted = accept(listener, nullptr, nullptr);
            if (accepted == INVALID_SOCKET)
            {
                return;
            }

            if (connections.size() >= MaxConnections)
            {
                logger("동시 연결 수 초과로 연결 거절");
                closesocket(accepted);
                continue;
            }

            // 논블로킹으로 바꾸지 못한 소켓도 받지 않음
            // (블로킹 소켓이 하나라도 섞이면 그 소켓의 send에서 멈출 때, 스레드 하나로 도는 서버 전체가 함께 멈춤)
            if (SetNonBlocking(accepted) == false)
            {
                logger("논블로킹 설정 실패로 연결 거절");
                closesocket(accepted);
                continue;
            }

            Connection connection;
            connection.socket = accepted;
            connection.lastActivity = std::chrono::steady_clock::now();
            connections.push_back(std::move(connection));
        }
    }

    // Receive - 클라이언트가 보낸 데이터를 읽어 FrameReader에 쌓고, 다 모인 메시지를 하나씩 꺼내 Handle()로 처리
    // (Poll에서 이 연결에 받을 데이터가 왔을 때만 부름)
    void TcpServer::Receive(Connection& connection)
    {
        // 한 번에 최대 1024바이트까지 읽음
        // (더 남아 있으면 다음 Poll에서 select가 다시 알려 주므로 그때 이어서 읽음)
        uint8_t buffer[1024];
        int received = recv(connection.socket, reinterpret_cast<char*>(buffer), sizeof(buffer), 0);
        if (received == 0)
        {
            // recv가 0이면 상대가 연결을 정상적으로 닫은 것이므로
            // 서버 쪽에서도 이 클라이언트 연결을 정리하도록 closing 설정
            // (그 전에 받은 데이터는 이전 Receive에서 이미 처리했음)
            connection.closing = true;
            return;
        }

        if (received == SOCKET_ERROR)
        {
            // WSAEWOULDBLOCK이면 지금은 읽을 게 없는 것이라 다음 Poll에서 다시 시도
            // 그 밖의 오류는 연결이 깨진 것이라 closing 설정
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                connection.closing = true;
            }

            return;
        }

        // 받은 게 있으므로 오래 조용한 연결 판정 기준 시각을 지금으로 갱신
        connection.lastActivity = std::chrono::steady_clock::now();
        connection.reader.Append(buffer, static_cast<size_t>(received));

        // 다 모인 메시지를 더 꺼낼 게 없을 때까지 하나씩 꺼내 처리
        // (한 번 읽은 데이터 안에 메시지 여러 개가 붙어 올 수 있음)
        std::vector<uint8_t> payload;
        for (;;)
        {
            FrameReader::Result result = connection.reader.Next(payload);
            if (result == FrameReader::Result::NeedMore)
            {
                // 아직 덜 온 메시지는 FrameReader에 남겨 두고, 다음 Receive에서 이어 붙임
                return;
            }

            if (result == FrameReader::Result::Error)
            {
                logger("잘못된 프레임 길이로 연결 종료");
                connection.closing = true;
                return;
            }

            Handle(connection, payload);
            // Handle이 closing을 설정했으면 FrameReader에 남은 메시지는 처리하지 않음
            if (connection.closing)
            {
                return;
            }
        }
    }

    // Handle - 다 모인 메시지(제출) 하나를 처리 (메시지 해석 -> 값 검사 -> 저장 -> ACK 보내기)
    // ACK는 반드시 저장이 끝난 뒤에 보냄
    // (ACK를 먼저 보내면 클라이언트는 결과 파일을 지웠는데 서버는 저장 전에 꺼져, 기록이 양쪽 모두에서 사라질 수 있음)
    void TcpServer::Handle(Connection& connection, const std::vector<uint8_t>& payload)
    {
        RunRecord record;
        if (DecodeSubmit(payload.data(), payload.size(), record) == false)
        {
            // 해석할 수 없는 메시지는 ACK 없이 closing 설정 (ACK에 넣을 runId부터 믿을 수 없음)
            logger("해석할 수 없는 메시지로 연결 종료");
            connection.closing = true;
            return;
        }

        SubmitAck ack;
        ack.runId = record.runId;
        ack.reason = Validate(record);

        if (ack.reason != RejectReason::None)
        {
            // 값 검사에 걸린 기록은 저장하지 않고 Rejected ACK만 보냄
            ack.status = AckStatus::Rejected;
            ack.total = store.Count();
            logger("거부 " + ToHex(record.runId) + " reason=" + std::to_string(static_cast<int>(ack.reason)));
        }
        else
        {
            RankingStore::Outcome outcome;
            if (store.Submit(record, outcome) == false)
            {
                // 저장에 실패하면 ACK 없이 closing 설정
                // (ACK를 보내면 클라이언트가 결과 파일을 지워 버림, ACK를 못 받은 클라이언트는 파일을 남겨 두고 다음에 다시 보냄)
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

        // ACK를 보낼 버퍼(outgoing)에 넣고, 다음 Poll까지 기다리지 않고 바로 한 번 보내 봄
        std::vector<uint8_t> frame = MakeFrame(EncodeAck(ack));
        connection.outgoing.insert(connection.outgoing.end(), frame.begin(), frame.end());
        Send(connection);
    }

    // Send - 보낼 버퍼(outgoing)에 쌓인 ACK를 지금 보낼 수 있는 만큼 보냄
    // (Handle에서 ACK를 넣은 직후, 그리고 Poll에서 이 연결이 쓰기 가능해졌을 때 부름)
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
            // WSAEWOULDBLOCK이면 OS 송신 버퍼가 가득 차 지금은 못 보내는 것이므로
            // 쓰기 가능해지면 다음 Poll에서 다시 보냄
            // 그 밖의 오류는 연결이 깨진 것이라 closing 설정
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                connection.closing = true;
            }

            return;
        }

        // send는 OS 송신 버퍼에 들어간 만큼만 보내고 그 바이트 수를 돌려주므로, 보낸 만큼만 outgoing에서 지움
        // (남은 나머지는 소켓이 다시 쓰기 가능해지면 다음 Poll에서 이어서 보냄)
        connection.outgoing.erase(connection.outgoing.begin(), connection.outgoing.begin() + sent);
    }

    // CloseMarked - closing이 설정된 연결의 소켓을 닫고 목록에서 뺌
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
