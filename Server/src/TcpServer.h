#pragma once

#include <chrono>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

#include <winsock2.h>

#include "FrameReader.h"
#include "RankingStore.h"

namespace rank
{
    /**
     * TcpServer - 스레드 하나에서 select로 여러 연결을 처리하는 결과 수신 서버
     *
     * 메시지가 다 모이면 검증 -> 저장 -> 클라이언트에게 ACK 발송 (검증에 걸리면 저장 없이 Rejected ACK)
     * 잘못된 메시지, 저장 실패, 오래 조용한 연결, 한도를 넘는 연결은 끊음
     * TcpServer 안에는 계속 도는 루프가 없음 - Poll을 한 번 부르면 할 일을 기다렸다가(최대 timeoutMs) 처리하고 돌아옴
     * 계속 돌리려면 부르는 쪽이 Poll을 반복 호출
     *   (실제 서버 - main.cpp의 main 함수에서 Ctrl+C로 끌 때까지 반복 호출)
     *   (테스트 - Tests.cpp의 RunServerUntil에서 클라이언트 작업이 끝날 때까지 반복 호출)
     */
    class TcpServer
    {
    public:
        using Logger = std::function<void(const std::string&)>;

        TcpServer(RankingStore& store, Logger logger);
        ~TcpServer();

        TcpServer(const TcpServer&) = delete;
        TcpServer& operator=(const TcpServer&) = delete;

        // port가 0이면 OS가 빈 포트를 고름 (테스트용, 실제 포트는 Port())
        bool Start(uint16_t port);
        void Stop();

        // Poll - 할 일(새 연결, 받을 데이터, 마저 보낼 ACK)이 생길 때까지 최대 timeoutMs 기다렸다가 처리하고 함수 종료
        // 아무 일 없이 timeoutMs가 지나도 함수 종료
        void Poll(int timeoutMs);

        uint16_t Port() const { return port; }
        size_t ConnectionCount() const { return connections.size(); }

        // 이 시간 동안 아무것도 안 오면 연결을 끊음
        std::chrono::milliseconds idleTimeout{10000};

    private:
        // Connection - 접속한 클라이언트 하나의 상태
        struct Connection
        {
            SOCKET socket = INVALID_SOCKET;                      // 이 클라이언트와 데이터를 주고받는 소켓 (AcceptAll에서 받음)
            FrameReader reader{MaxPayloadSize};                  // 받은 바이트를 모아 다 모인 메시지를 하나씩 꺼냄
            std::vector<uint8_t> outgoing;                       // 아직 다 못 보낸 ACK 바이트 (Send가 보낸 만큼 지움)
            std::chrono::steady_clock::time_point lastActivity;  // 마지막으로 데이터를 받은 시각 (idleTimeout 판정 기준)
            bool closing = false;                                // true면 이번 Poll 끝(5단계 CloseMarked)에 소켓을 닫고 목록에서 뺌
        };

        void AcceptAll();
        void Receive(Connection& connection);
        void Send(Connection& connection);
        void Handle(Connection& connection, const std::vector<uint8_t>& payload);
        void CloseMarked();

        RankingStore& store;
        Logger logger;
        SOCKET listener = INVALID_SOCKET;  // 리스너 - 새 연결을 받는 소켓 (데이터는 연결마다 만든 Connection::socket으로 주고받음)
        uint16_t port = 0;
        std::vector<Connection> connections;
    };
}
