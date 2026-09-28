#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace rank
{
    /**
     * FrameReader - 받은 바이트를 [길이 u32][본문] 프레임 단위로 잘라냄
     *
     * TCP는 보낸 단위를 지키지 않음 - 한 번에 반쪽이 오거나 두 개가 붙어 올 수 있음
     * 받은 만큼 Append하고 NeedMore가 나올 때까지 Next를 반복
     * 길이가 0이거나 최대 크기를 넘으면 Error (이후 계속 Error, 연결을 끊어야 함)
     */
    class FrameReader
    {
    public:
        enum class Result
        {
            NeedMore,
            Frame,
            Error,
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
        bool failed = false;
    };
}
