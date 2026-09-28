#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace rank
{
    // 정수를 빅엔디언으로 씀 - CPU와 상관없이 같은 바이트
    inline void WriteU8(std::vector<uint8_t>& out, uint8_t value)
    {
        out.push_back(value);
    }

    inline void WriteU16(std::vector<uint8_t>& out, uint16_t value)
    {
        out.push_back(static_cast<uint8_t>(value >> 8));
        out.push_back(static_cast<uint8_t>(value));
    }

    inline void WriteU32(std::vector<uint8_t>& out, uint32_t value)
    {
        out.push_back(static_cast<uint8_t>(value >> 24));
        out.push_back(static_cast<uint8_t>(value >> 16));
        out.push_back(static_cast<uint8_t>(value >> 8));
        out.push_back(static_cast<uint8_t>(value));
    }

    // ByteReader - 버퍼를 앞에서부터 읽음
    // 모자라면 실패 상태가 되고 이후 읽기는 0 - 호출 쪽은 마지막에 Ok()만 확인
    class ByteReader
    {
    public:
        ByteReader(const uint8_t* data, size_t size)
            : data(data), size(size)
        {
        }

        uint8_t U8()
        {
            if (Require(1) == false)
            {
                return 0;
            }

            return data[position++];
        }

        uint16_t U16()
        {
            if (Require(2) == false)
            {
                return 0;
            }

            uint16_t value = static_cast<uint16_t>((data[position] << 8) | data[position + 1]);
            position += 2;
            return value;
        }

        uint32_t U32()
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

        bool Bytes(uint8_t* destination, size_t count)
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
