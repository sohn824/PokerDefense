#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <vector>

namespace rank
{
    /**
     * 결과 제출 프로토콜 v1 - C# RunProtocol.cs와 바이트 단위로 동일
     *
     * 프레임: [본문 길이 u32][본문], 정수는 빅엔디언
     *
     * SubmitRun (클라이언트 → 서버)
     *   version u8 | type u8 | runId u8[16] | rulesetLength u8 | ruleset
     *   wave u16 | totalWaves u16 | cleared u8 | life u16 | summons u16 | bestHand u8 | elapsedMs u32
     * SubmitAck (서버 → 클라이언트)
     *   version u8 | type u8 | runId u8[16] | status u8 | reason u8 | rank u32 | total u32
     *
     * Ack = 검사하고 파일 저장까지 끝났다는 확인 (TCP의 도착 확인과 다름)
     */
    // 형식이 바뀌면 올림
    constexpr uint8_t ProtocolVersion = 1;

    // 본문 최대 크기, 넘으면 연결 끊음
    constexpr uint32_t MaxPayloadSize = 256;

    // 족보를 한 번도 확정하지 않은 판의 bestHand
    constexpr uint8_t NoHand = 0xFF;

    // 본문 두 번째 바이트 (버전 다음)
    enum class MessageType : uint8_t
    {
        SubmitRun = 1,
        SubmitAck = 2,
    };

    // 클라이언트 처리: Accepted·Duplicate → 기록 삭제, Rejected → 다시 보내지 않음
    enum class AckStatus : uint8_t
    {
        Accepted = 0,   // 새로 저장
        Duplicate = 1,  // 이미 저장된 runId (성공으로 취급)
        Rejected = 2,   // 규칙 위반
    };

    // Rejected 이유
    enum class RejectReason : uint8_t
    {
        None = 0,
        UnknownRuleset = 1,  // 모르는 규칙 버전 Key
        OutOfRange = 2,      // 나올 수 없는 값
    };

    // 판마다 클라이언트가 만드는 Guid, 중복 제출 판별용
    using RunId = std::array<uint8_t, 16>;

    // RunRecord - 한 판의 결과 (C# RunResult와 동일)
    struct RunRecord
    {
        RunId runId{};
        std::string ruleset;       // 규칙 버전 Key
        uint16_t wave = 0;         // 도달 웨이브 (실패한 웨이브 포함)
        uint16_t totalWaves = 0;
        bool cleared = false;      // 마지막 웨이브까지 버텼는지
        uint16_t life = 0;         // 게임 오버면 0
        uint16_t summons = 0;
        uint8_t bestHand = NoHand; // HandCategory 값
        uint32_t elapsedMs = 0;
    };

    // SubmitAck - 제출에 대한 서버 응답
    struct SubmitAck
    {
        RunId runId{};              // 어느 제출의 응답인지
        AckStatus status = AckStatus::Rejected;
        RejectReason reason = RejectReason::None;
        uint32_t rank = 0;          // 1부터, 저장됐을 때만
        uint32_t total = 0;         // 전체 기록 수
    };

    // 아래 Encode/Decode는 본문만 다룸 (길이 4바이트는 MakeFrame · FrameReader 담당)

    // 서버용: TcpServer::Handle에서 Decode → Validate → 저장 → EncodeAck → MakeFrame
    bool DecodeSubmit(const uint8_t* data, size_t size, RunRecord& record);
    std::vector<uint8_t> EncodeAck(const SubmitAck& ack);
    std::vector<uint8_t> MakeFrame(const std::vector<uint8_t>& payload);

    // 클라이언트용: 실제 클라이언트는 C#, 여기서는 테스트 전용
    std::vector<uint8_t> EncodeSubmit(const RunRecord& record);
    bool DecodeAck(const uint8_t* data, size_t size, SubmitAck& ack);

    // Validate - 규칙상 나올 수 없는 값이면 거부 (제출 받을 때 · 파일 다시 읽을 때)
    RejectReason Validate(const RunRecord& record);

    // ToHex - runId를 32자리 16진수로 (중복 키 · 파일 · 로그)
    std::string ToHex(const RunId& id);
}
