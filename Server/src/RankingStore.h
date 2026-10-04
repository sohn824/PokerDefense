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
     * 파일에 한 줄씩 덧붙여 저장하고, 서버를 다시 켜면 파일에서 복구
     * Submit이 true면 디스크까지 저장된 상태 (서버는 이걸 확인한 뒤에 ACK를 보냄)
     * 같은 runId는 한 번만 저장 (ACK 유실로 다시 와도 중복 저장 안 됨)
     */
    class RankingStore
    {
    public:
        struct Outcome
        {
            AckStatus status = AckStatus::Accepted;  // Accepted 또는 Duplicate
            uint32_t rank = 0;                       // 1부터, 지금 기록 전체 기준
            uint32_t total = 0;                      // 전체 기록 수 (이번 기록 포함)
        };

        // path - 기록을 저장할 파일 경로
        // (서버는 실행 인자 --data 값을 넘김, 생략하면 runs.txt - 실행한 폴더 기준 상대 경로)
        // 빈 문자열이면 파일을 전혀 안 씀 -> 메모리에만 보관, 객체가 사라지면 기록도 사라짐
        // (파일 없이 순위·중복 로직만 확인하는 테스트용, 이때는 Submit이 true여도 디스크에 없음)
        explicit RankingStore(std::string path);

        // Load - 메모리의 기록을 비우고 파일에서 다시 읽음, 읽은 개수를 돌려줌 (파일이 없으면 0)
        // 깨진 줄(쓰다 만 마지막 줄 등), 규칙에 안 맞는 줄, 이미 읽은 runId 줄은 건너뜀
        size_t Load();

        // Submit - 새 기록이면 파일에 쓰고 Accepted, 이미 있는 runId면 쓰지 않고 Duplicate
        // 둘 다 지금 기준 순위를 돌려줌, 파일 쓰기에 실패하면 false (메모리에도 넣지 않음)
        bool Submit(const RunRecord& record, Outcome& outcome);

        uint32_t Count() const { return static_cast<uint32_t>(records.size()); }

    private:
        uint32_t RankOf(const RunRecord& record) const;  // 이 기록의 순위 (1부터, 동점은 같은 순위)

        // 파일 끝에 한 줄 덧붙이고 디스크까지 내림 (끝이 잘린 줄이면 줄바꿈부터 넣음)
        // 쓰기·디스크 기록·닫기 중 하나라도 실패하면 false, path가 비면 쓰지 않고 true
        bool Append(const RunRecord& record);

        std::string path;
        std::vector<RunRecord> records;
        // key: ToHex(runId) - value: records 위치
        // (runId는 std::array<uint8_t, 16>인데 std::array에는 표준 해시(std::hash)가 없어
        //  unordered_map 키로 바로 못 씀 -> ToHex로 문자열로 바꿔서 key로 사용)
        std::unordered_map<std::string, size_t> indexById;
    };

    // 순위 기준: 웨이브 높은 순 -> 라이프 많은 순 -> 빨리 끝낸 순
    bool IsBetter(const RunRecord& a, const RunRecord& b);

    // 기록 - 파일 한 줄 변환 (ToLine: 기록 -> 한 줄, FromLine: 한 줄 -> 기록)
    std::string ToLine(const RunRecord& record);
    bool FromLine(const std::string& line, RunRecord& record);
}
