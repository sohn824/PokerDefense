#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace rank
{
    /**
     * FrameReader - TCP로 받은 이어진 바이트에서 메시지 경계를 찾아, 프레임([길이 4바이트][본문]) 하나씩 떼어 냄
     *               꺼낸 payload(메시지 본문)는 길이 4바이트를 뺀 부분
     *
     * TCP는 보낸 단위를 지키지 않음 - 메시지 하나가 recv 여러 번에 나뉘어 오거나,
     * 메시지 두 개가 recv 한 번에 붙어 올 수 있음
     * 그래서 받은 바이트를 쌓아 두고, 앞의 길이만큼 본문이 다 모였을 때만 하나씩 꺼냄
     *
     * 사용법: recv로 받을 때마다 Append -> Next를 반복 호출, Next의 반환값(Result)에 따라 다음과 같이 처리
     *   Frame    = 프레임 하나가 다 모여 메시지 하나를 꺼냄, 본문은 payload에 담김
     *              (뒤에 더 붙어 있을 수 있으니 다시 Next 호출)
     *              payload는 Frame일 때만 의미 있음 (다른 결과면 이전 값이 그대로 남음)
     *   NeedMore = 아직 덜 옴, 더 받아서 Append
     *   Error    = 길이가 0이거나 최대 크기(MaxPayloadSize, 256바이트)를 넘음, 연결을 끊어야 함 (한 번 나면 이후 계속 Error)
     *
     * 사용 예: TcpServer::Receive
     *            한 연결에 제출 메시지가 붙어 올 수 있어, NeedMore가 나올 때까지 메시지를 계속 꺼내 하나씩 처리
     *          클라이언트 RunFrameReader의 RunSubmitter.ReceiveFrameAsync
     *            제출 하나에 서버 답장(Ack)은 하나뿐이라, 첫 Frame을 꺼내면 받기를 멈추고 그 본문을 돌려줌
     */
    class FrameReader
    {
    public:
        enum class Result
        {
            NeedMore,  // 프레임이 아직 다 오지 않음
            Frame,     // 프레임 하나가 다 모여 payload로 꺼냄 (payload : 헤더를 제외한 메시지 본문)
            Error,     // 길이가 잘못됨 (0이거나 MaxPayloadSize 초과), 이후 계속 Error
        };

        explicit FrameReader(uint32_t maxPayload);

        void Append(const uint8_t* data, size_t size);
        Result Next(std::vector<uint8_t>& payload);

        // 아직 꺼내지 않은 바이트 수
        size_t Buffered() const { return buffer.size() - offset; }

    private:
        std::vector<uint8_t> buffer;
        size_t offset = 0;  // 이미 꺼낸 앞부분 - 다음 Append 때 한 번에 지움
        uint32_t maxPayload;
        bool failed = false;  // 잘못된 길이를 받으면 이후 계속 Error
    };
}
