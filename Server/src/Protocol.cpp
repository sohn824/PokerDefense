#include "Protocol.h"

#include "ByteIO.h"

namespace rank
{
    namespace
    {
        // hand-loop-v1 규칙에서 나올 수 있는 값의 범위
        // 게임 데이터(Stage_1.asset, HandCategory)가 바뀌면 같이 고침
        constexpr const char* HandLoopRuleset = "hand-loop-v1";
        constexpr uint16_t HandLoopTotalWaves = 50;
        constexpr uint16_t HandLoopStartingLife = 20;
        constexpr uint8_t HandLoopLastHand = 12;  // 가장 높은 족보 (RoyalStraightFlush)

        // ReadHeader - 본문 앞 두 바이트(버전, 종류)가 기대한 메시지인지 확인
        bool ReadHeader(ByteReader& reader, MessageType expected)
        {
            uint8_t version = reader.U8();
            uint8_t type = reader.U8();
            return reader.Ok() && version == ProtocolVersion && type == static_cast<uint8_t>(expected);
        }
    }

    // EncodeSubmit - RunRecord -> 서버로 보낼 바이트 (길이 4바이트 제외)
    // 테스트 전용 함수 - 실제 클라이언트는 C#의 RunProtocol.EncodeSubmit을 사용
    // 필드는 Protocol.h 머리 설명의 순서대로 씀
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

    // DecodeSubmit - 클라이언트에서 받은 바이트 -> RunRecord (길이 4바이트 제외)
    // 끝까지 읽은 뒤 한 번만 검사하고, 실패하면 record는 건드리지 않음
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

        // 바이트가 모자라거나 남으면 형식 오류
        if (reader.Ok() == false || reader.Remaining() != 0 || cleared > 1)
        {
            return false;
        }

        decoded.cleared = cleared == 1;
        record = decoded;
        return true;
    }

    // EncodeAck - SubmitAck -> 클라이언트로 보낼 바이트 (길이 4바이트 제외)
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

    // DecodeAck - 서버에서 받은 바이트 -> SubmitAck (길이 4바이트 제외)
    // 테스트 전용 함수 - 실제 클라이언트는 C#의 RunProtocol.TryDecodeAck를 사용
    // 형식이 어긋나거나 모르는 상태·이유 값이면 실패
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

    // MakeFrame - 본문 앞에 길이 4바이트를 붙여 보낼 메시지로 만듦
    std::vector<uint8_t> MakeFrame(const std::vector<uint8_t>& payload)
    {
        std::vector<uint8_t> frame;
        frame.reserve(payload.size() + 4);
        WriteU32(frame, static_cast<uint32_t>(payload.size()));
        frame.insert(frame.end(), payload.begin(), payload.end());
        return frame;
    }

    // Validate - 게임 규칙상 나올 수 없는 값이면 거부 이유를 돌려줌
    RejectReason Validate(const RunRecord& record)
    {
        if (record.ruleset != HandLoopRuleset)
        {
            return RejectReason::UnknownRuleset;
        }

        // 1) 값마다 범위 확인 (소환은 라운드당 최대 1기라 웨이브 수를 넘을 수 없음)
        bool inRange = record.totalWaves == HandLoopTotalWaves
            && record.wave >= 1 && record.wave <= record.totalWaves
            && record.life <= HandLoopStartingLife
            && record.summons <= record.wave
            && (record.bestHand <= HandLoopLastHand || record.bestHand == NoHand);

        // 2) 값끼리 맞는지 확인: 클리어면 마지막 웨이브까지 갔어야 하고, 아니면 라이프 0으로 끝났어야 함
        bool consistent = record.cleared ? record.wave == record.totalWaves : record.life == 0;

        return inRange && consistent ? RejectReason::None : RejectReason::OutOfRange;
    }

    // ToHex - runId 16바이트를 32자리 16진수 문자열로 변환 (1바이트 = 16진수 2자리)
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
