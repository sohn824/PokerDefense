#include "FrameReader.h"

namespace rank
{
    FrameReader::FrameReader(uint32_t maxPayload)
        : maxPayload(maxPayload)
    {
    }

    // Append - recv로 받은 바이트를 뒤에 붙임
    void FrameReader::Append(const uint8_t* data, size_t size)
    {
        if (failed)
        {
            return;
        }

        // 이미 꺼낸 앞부분은 새로 받을 때 한 번에 지워 버퍼가 계속 커지지 않게 함
        if (offset > 0)
        {
            buffer.erase(buffer.begin(), buffer.begin() + static_cast<std::ptrdiff_t>(offset));
            offset = 0;
        }

        buffer.insert(buffer.end(), data, data + size);
    }

    // Next - 프레임 하나가 다 모였으면 꺼내고, 아니면 NeedMore
    FrameReader::Result FrameReader::Next(std::vector<uint8_t>& payload)
    {
        if (failed)
        {
            return Result::Error;
        }

        // 길이 4바이트가 다 오기 전에는 판단할 수 없음
        if (Buffered() < 4)
        {
            return Result::NeedMore;
        }

        const uint8_t* head = buffer.data() + offset;
        uint32_t length = (static_cast<uint32_t>(head[0]) << 24)
            | (static_cast<uint32_t>(head[1]) << 16)
            | (static_cast<uint32_t>(head[2]) << 8)
            | static_cast<uint32_t>(head[3]);

        // 본문이 다 오기를 기다리기 전에 길이부터 검사 - 거대한 길이로 메모리를 잡아두는 입력을 막음
        if (length == 0 || length > maxPayload)
        {
            failed = true;
            return Result::Error;
        }

        if (Buffered() < 4 + static_cast<size_t>(length))
        {
            return Result::NeedMore;
        }

        payload.assign(head + 4, head + 4 + length);
        offset += 4 + length;
        return Result::Frame;
    }
}
