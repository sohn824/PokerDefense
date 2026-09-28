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
     * 연결마다 받은 바이트를 FrameReader에 쌓고, 프레임이 완성되면 검증 → 저장 → ACK
     * 논블로킹 소켓이라 느린 연결이 다른 연결을 막지 않음. 못 다 보낸 데이터는 다음 Poll에서 이어 보냄
     * 잘못된 프레임, 오래 조용한 연결, 한도를 넘는 연결은 끊음
     * Poll을 한 번씩 부르는 구조라 테스트에서 한 단계씩 돌릴 수 있음
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

        // 최대 timeoutMs 동안 기다렸다가 온 이벤트를 처리하고 돌아옴
        void Poll(int timeoutMs);

        uint16_t Port() const { return port; }
        size_t ConnectionCount() const { return connections.size(); }

        // 이 시간 동안 아무것도 안 오면 연결을 끊음
        std::chrono::milliseconds idleTimeout{10000};

    private:
        struct Connection
        {
            SOCKET socket = INVALID_SOCKET;
            FrameReader reader{MaxPayloadSize};
            std::vector<uint8_t> outgoing;
            std::chrono::steady_clock::time_point lastActivity;
            bool closing = false;  // 끊기로 한 연결 - Poll이 끝날 때 정리
        };

        void AcceptAll();
        void Receive(Connection& connection);
        void Send(Connection& connection);
        void Handle(Connection& connection, const std::vector<uint8_t>& payload);
        void CloseMarked();

        RankingStore& store;
        Logger logger;
        SOCKET listener = INVALID_SOCKET;
        uint16_t port = 0;
        std::vector<Connection> connections;
    };
}
