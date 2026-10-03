#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace rank
{
    // 쓰기 - 정수를 큰 자리 바이트부터 out 끝에 덧붙임 (빅엔디언, CPU와 상관없이 같은 바이트)
    // WriteByte는 1바이트라 순서가 없고, 순서가 의미 있는 건 WriteUInt16·WriteUInt32

    // 1바이트
    inline void WriteByte(std::vector<uint8_t>& out, uint8_t value)
    {
        out.push_back(value);
    }

    // 2바이트. value 타입이 uint16_t라 범위 검사가 필요 없음 (C#은 int를 받아 잘라서 씀)
    inline void WriteUInt16(std::vector<uint8_t>& out, uint16_t value)
    {
        out.push_back(static_cast<uint8_t>(value >> 8));
        out.push_back(static_cast<uint8_t>(value));
    }

    // 4바이트
    inline void WriteUInt32(std::vector<uint8_t>& out, uint32_t value)
    {
        out.push_back(static_cast<uint8_t>(value >> 24));
        out.push_back(static_cast<uint8_t>(value >> 16));
        out.push_back(static_cast<uint8_t>(value >> 8));
        out.push_back(static_cast<uint8_t>(value));
    }

    // ByteReader - 본문을 앞에서부터 차례로 읽음
    // 바이트가 모자라면 실패 상태(Ok() = false)가 되고, 그 읽기부터는 모두 0을 돌려줌
    // 그래서 읽는 쪽은 매번 확인하지 않고 마지막에 Ok()를 한 번만 확인
    class ByteReader
    {
    public:
        ByteReader(const uint8_t* data, size_t size)
            : data(data), size(size)
        {
        }

        // 1바이트
        uint8_t ReadByte()
        {
            if (Require(1) == false)
            {
                return 0;
            }

            return data[position++];
        }

        // 부호 없는 16비트 정수 = 2바이트
        uint16_t ReadUInt16()
        {
            if (Require(2) == false)
            {
                return 0;
            }

            uint16_t value = static_cast<uint16_t>((data[position] << 8) | data[position + 1]);
            position += 2;
            return value;
        }

        // 부호 없는 32비트 정수 = 4바이트
        uint32_t ReadUInt32()
        {
            if (Require(4) == false)
            {
                return 0;
            }

            uint32_t value = (static_cast<uint32_t>(data[position]) << 24)
                | (static_cast<uint32_t>(data[position + 1]) << 16)
                | (static_cast<uint32_t>(data[position + 2]) << 8)
                | static_cast<uint32_t>(data[position + 3]);
            position += 4;
            return value;
        }

        // count바이트를 그대로 destination에 복사 (runId·문자열용)
        bool ReadBytes(uint8_t* destination, size_t count)
        {
            if (Require(count) == false)
            {
                return false;
            }

            for (size_t i = 0; i < count; i++)
            {
                destination[i] = data[position + i];
            }

            position += count;
            return true;
        }

        bool Ok() const { return ok; }
        size_t Remaining() const { return size - position; }

    private:
        bool Require(size_t count)
        {
            if (ok == false || size - position < count)
            {
                ok = false;
                return false;
            }

            return true;
        }

        const uint8_t* data;
        size_t size;
        size_t position = 0;
        bool ok = true;
    };
}
