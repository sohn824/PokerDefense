#pragma once

#include <cstdint>
#include <string>
#include <unordered_map>
#include <vector>

#include "Protocol.h"

namespace rank
{
    /**
     * RankingStore - 기록 저장과 순위 계산
     *
     * 파일에 한 줄씩 덧붙여 저장하고, 다시 켜면 파일에서 복구
     * 파일에 쓴 뒤에 ACK를 보내므로 ACK를 받은 기록은 서버가 꺼져도 남음
     * 같은 runId는 한 번만 저장 (ACK 유실로 다시 와도 중복되지 않음)
     */
    class RankingStore
    {
    public:
        struct Outcome
        {
            AckStatus status = AckStatus::Accepted;
            uint32_t rank = 0;
            uint32_t total = 0;
        };

        // path가 비면 메모리에만 보관 (테스트용)
        explicit RankingStore(std::string path);

        // Load - 파일에서 기록을 읽고 개수를 돌려줌. 깨진 줄(쓰다 만 마지막 줄 등)은 건너뜀
        size_t Load();

        // Submit - 저장 후 순위를 돌려줌. 파일 쓰기에 실패하면 false (메모리에도 넣지 않음)
        bool Submit(const RunRecord& record, Outcome& outcome);

        uint32_t Count() const { return static_cast<uint32_t>(records.size()); }

    private:
        uint32_t RankOf(const RunRecord& record) const;
        bool Append(const RunRecord& record);

        std::string path;
        std::vector<RunRecord> records;
        std::unordered_map<std::string, size_t> indexById;  // runId(hex) → records 위치
    };

    // 순위 기준: 웨이브 높은 순 → 라이프 많은 순 → 빨리 끝낸 순
    bool IsBetter(const RunRecord& a, const RunRecord& b);

    // 기록 ↔ 파일 한 줄
    std::string ToLine(const RunRecord& record);
    bool FromLine(const std::string& line, RunRecord& record);
}
