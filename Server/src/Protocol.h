#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <vector>

namespace rank
{
    /**
     * 결과 제출 프로토콜 v1 (C# 클라이언트의 RunProtocol.cs와 같은 형식)
     *
     * 메시지 하나 = [길이 4바이트][본문]
     * TCP는 메시지 경계가 없어서, 받는 쪽이 본문 끝을 알 수 있게 길이를 먼저 보냄
     * 1바이트는 0~255(256가지)라 큰 수는 여러 바이트로 나눠 씀
     * 숫자는 큰 자리 바이트부터 씀 (빅엔디언, 예: 300 = 1×256 + 44 → 01 2C)
     *
     * 메시지 본문은 아래 값을 순서대로 이어 붙임 (크기는 구조체 타입과 같음)
     *   SubmitRun  클라 → 서버: version, type, runId, 규칙 이름 길이, 규칙 이름,
     *                           wave, totalWaves, cleared, life, summons, bestHand, elapsedMs
     *   SubmitAck  서버 → 클라: version, type, runId, status, reason, rank, total
     *
     * Ack = 서버가 검사하고 파일 저장까지 끝낸 뒤 보내는 답장 (TCP의 도착 확인과 다름)
     */

    // 프로토콜 형식이 바뀌면 이 값을 올림
    constexpr uint8_t ProtocolVersion = 1;

    // 본문 최대 크기, 넘으면 연결 끊음
    constexpr uint32_t MaxPayloadSize = 256;

    // 족보를 한 번도 확정하지 않은 판의 bestHand
    constexpr uint8_t NoHand = 0xFF;

    // 메시지 종류 - 본문 [버전][종류][…] 중 두 번째 바이트, 받는 쪽이 이 값으로 제출/답장을 구분
    enum class MessageType : uint8_t
    {
        SubmitRun = 1,
        SubmitAck = 2,
    };

    // 클라이언트 처리: Accepted·Duplicate -> 기록 삭제, Rejected -> 다시 보내지 않음
    enum class AckStatus : uint8_t
    {
        Accepted = 0,   // 새로 저장
        Duplicate = 1,  // 이미 저장된 runId (성공으로 취급)
        Rejected = 2,   // 규칙 위반
    };

    // Rejected된 이유
    enum class RejectReason : uint8_t
    {
        None = 0,
        UnknownRuleset = 1,  // 모르는 규칙 버전 Key
        OutOfRange = 2,      // 나올 수 없는 값
    };

    // 판마다 클라이언트가 만드는 Guid(전역 고유 식별자), 중복 제출 판별용
    using RunId = std::array<uint8_t, 16>;

    // RunRecord - 한 판의 결과 (C# RunResult와 동일)
    struct RunRecord
    {
        RunId runId{};
        std::string ruleset;       // 규칙 버전 Key
        uint16_t wave = 0;         // 마지막으로 치른 웨이브 번호 (1부터, 게임 오버를 낸 웨이브 포함)
        uint16_t totalWaves = 0;   // 전체 웨이브 수 (hand-loop-v1은 50)
        bool cleared = false;      // 모든 웨이브를 치렀는지 (마지막 웨이브에서 라이프가 0이 돼도 true)
        uint16_t life = 0;         // 남은 라이프 (게임 오버면 0)
        uint16_t summons = 0;      // 총 소환 수 (라운드당 최대 1, 소환 포기·퇴장한 유닛도 포함)
        uint8_t bestHand = NoHand; // 확정한 족보 중 가장 희귀한 것의 HandCategory 값, 확정한 적 없으면 NoHand
        uint32_t elapsedMs = 0;    // 게임 시간 기준 (ms). 일시정지는 빠지고, 배속 중에는 실제 시간보다 빨리 흐름
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

    // Encode = 구조체 -> 바이트, Decode = 바이트 -> 구조체
    // 둘 다 메시지 본문만 다룸
    // 앞의 길이 4바이트는 MakeFrame · FrameReader가 담당

    // [서버가 쓰는 함수]
    // 제출을 받아 답장하는 순서 (TcpServer::Handle)
    //   DecodeSubmit → Validate → 파일 저장 → EncodeAck → MakeFrame
    bool DecodeSubmit(const uint8_t* data, size_t size, RunRecord& record);  // 형식이 어긋나면 false
    std::vector<uint8_t> EncodeAck(const SubmitAck& ack);
    std::vector<uint8_t> MakeFrame(const std::vector<uint8_t>& payload);     // 본문 앞에 길이를 붙임

    // [테스트만 쓰는 함수]
    // 실제 클라이언트가 아니라 서버 테스트에서 클라이언트 역할을 할 때 사용
    std::vector<uint8_t> EncodeSubmit(const RunRecord& record);
    bool DecodeAck(const uint8_t* data, size_t size, SubmitAck& ack);

    // Validate - 게임 규칙상 나올 수 없는 값이면 거부 이유를 돌려줌
    // 제출을 받을 때, 서버 시작 시 저장 파일을 다시 읽을 때 사용
    RejectReason Validate(const RunRecord& record);

    // ToHex - runId 16바이트를 32자리 16진수 문자열로 변환 (중복 확인 키 · 저장 파일 · 로그에 사용)
    std::string ToHex(const RunId& id);
}
