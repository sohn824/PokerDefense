#include "Protocol.h"

#include "ByteIO.h"

namespace rank
{
    namespace
    {
        // hand-loop-v1 규칙의 값 범위 - 게임 데이터(Stage_1.asset, HandCategory)가 바뀌면 같이 고침
        constexpr const char* HandLoopRuleset = "hand-loop-v1";
        constexpr uint16_t HandLoopTotalWaves = 50;
        constexpr uint16_t HandLoopStartingLife = 20;
        constexpr uint8_t HandLoopLastHand = 12;  // RoyalStraightFlush

        // 버전·종류가 기대한 메시지인지 확인
        bool ReadHeader(ByteReader& reader, MessageType expected)
        {
            uint8_t version = reader.U8();
            uint8_t type = reader.U8();
            return reader.Ok() && version == ProtocolVersion && type == static_cast<uint8_t>(expected);
        }
    }

    // EncodeSubmit - 필드를 정해진 순서대로 씀
    std::vector<uint8_t> EncodeSubmit(const RunRecord& record)
    {
        std::vector<uint8_t> out;
        WriteU8(out, ProtocolVersion);
        WriteU8(out, static_cast<uint8_t>(MessageType::SubmitRun));
        out.insert(out.end(), record.runId.begin(), record.runId.end());
        WriteU8(out, static_cast<uint8_t>(record.ruleset.size()));
        out.insert(out.end(), record.ruleset.begin(), record.ruleset.end());
        WriteU16(out, record.wave);
        WriteU16(out, record.totalWaves);
        WriteU8(out, record.cleared ? 1 : 0);
        WriteU16(out, record.life);
        WriteU16(out, record.summons);
        WriteU8(out, record.bestHand);
        WriteU32(out, record.elapsedMs);
        return out;
    }

    // DecodeSubmit - 끝까지 읽고 마지막에 한 번만 검사. 실패하면 record는 그대로
    bool DecodeSubmit(const uint8_t* data, size_t size, RunRecord& record)
    {
        ByteReader reader(data, size);
        if (ReadHeader(reader, MessageType::SubmitRun) == false)
        {
            return false;
        }

        RunRecord decoded;
        reader.Bytes(decoded.runId.data(), decoded.runId.size());

        uint8_t rulesetLength = reader.U8();
        std::vector<uint8_t> ruleset(rulesetLength);
        if (rulesetLength > 0)
        {
            reader.Bytes(ruleset.data(), rulesetLength);
        }

        decoded.ruleset.assign(ruleset.begin(), ruleset.end());
        decoded.wave = reader.U16();
        decoded.totalWaves = reader.U16();
        uint8_t cleared = reader.U8();
        decoded.life = reader.U16();
        decoded.summons = reader.U16();
        decoded.bestHand = reader.U8();
        decoded.elapsedMs = reader.U32();

        // 바이트가 모자라거나 남으면 형식 오류 (다른 버전 메시지를 잘못 읽지 않게)
        if (reader.Ok() == false || reader.Remaining() != 0 || cleared > 1)
        {
            return false;
        }

        decoded.cleared = cleared == 1;
        record = decoded;
        return true;
    }

    // EncodeAck - 응답 본문을 만듦
    std::vector<uint8_t> EncodeAck(const SubmitAck& ack)
    {
        std::vector<uint8_t> out;
        WriteU8(out, ProtocolVersion);
        WriteU8(out, static_cast<uint8_t>(MessageType::SubmitAck));
        out.insert(out.end(), ack.runId.begin(), ack.runId.end());
        WriteU8(out, static_cast<uint8_t>(ack.status));
        WriteU8(out, static_cast<uint8_t>(ack.reason));
        WriteU32(out, ack.rank);
        WriteU32(out, ack.total);
        return out;
    }

    // DecodeAck - 응답 해석 (테스트 전용). 모르는 상태 값이면 실패
    bool DecodeAck(const uint8_t* data, size_t size, SubmitAck& ack)
    {
        ByteReader reader(data, size);
        if (ReadHeader(reader, MessageType::SubmitAck) == false)
        {
            return false;
        }

        SubmitAck decoded;
        reader.Bytes(decoded.runId.data(), decoded.runId.size());
        uint8_t status = reader.U8();
        uint8_t reason = reader.U8();
        decoded.rank = reader.U32();
        decoded.total = reader.U32();

        if (reader.Ok() == false || reader.Remaining() != 0
            || status > static_cast<uint8_t>(AckStatus::Rejected)
            || reason > static_cast<uint8_t>(RejectReason::OutOfRange))
        {
            return false;
        }

        decoded.status = static_cast<AckStatus>(status);
        decoded.reason = static_cast<RejectReason>(reason);
        ack = decoded;
        return true;
    }

    // MakeFrame - [길이 u32][본문]
    std::vector<uint8_t> MakeFrame(const std::vector<uint8_t>& payload)
    {
        std::vector<uint8_t> frame;
        frame.reserve(payload.size() + 4);
        WriteU32(frame, static_cast<uint32_t>(payload.size()));
        frame.insert(frame.end(), payload.begin(), payload.end());
        return frame;
    }

    // Validate - 규칙상 나올 수 없는 값이면 거부
    RejectReason Validate(const RunRecord& record)
    {
        if (record.ruleset != HandLoopRuleset)
        {
            return RejectReason::UnknownRuleset;
        }

        // 값 범위 - 소환은 라운드당 최대 1기라 웨이브 수를 넘을 수 없음
        bool inRange = record.totalWaves == HandLoopTotalWaves
            && record.wave >= 1 && record.wave <= record.totalWaves
            && record.life <= HandLoopStartingLife
            && record.summons <= record.wave
            && (record.bestHand <= HandLoopLastHand || record.bestHand == NoHand);

        // 값끼리 모순 - 클리어면 마지막 웨이브까지, 아니면 라이프 0으로 끝난 판
        bool consistent = record.cleared ? record.wave == record.totalWaves : record.life == 0;

        return inRange && consistent ? RejectReason::None : RejectReason::OutOfRange;
    }

    // ToHex - runId를 32자리 16진수 문자열로
    std::string ToHex(const RunId& id)
    {
        static const char* digits = "0123456789abcdef";
        std::string text;
        text.reserve(id.size() * 2);
        for (uint8_t value : id)
        {
            text.push_back(digits[value >> 4]);
            text.push_back(digits[value & 0x0F]);
        }

        return text;
    }
}
