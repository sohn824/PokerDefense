#include "RankingStore.h"

#include <cstdio>
#include <fstream>
#include <sstream>

#ifdef _WIN32
#include <io.h>
#else
#include <unistd.h>
#endif

namespace rank
{
    namespace
    {
        // ParseHex - ToHex의 반대. 32자리 소문자 16진수만 받음
        bool ParseHex(const std::string& text, RunId& id)
        {
            if (text.size() != id.size() * 2)
            {
                return false;
            }

            for (size_t i = 0; i < id.size(); i++)
            {
                int value = 0;
                for (size_t j = 0; j < 2; j++)
                {
                    char c = text[i * 2 + j];
                    int digit = c >= '0' && c <= '9' ? c - '0'
                        : c >= 'a' && c <= 'f' ? c - 'a' + 10
                        : -1;
                    if (digit < 0)
                    {
                        return false;
                    }

                    value = value * 16 + digit;
                }

                id[i] = static_cast<uint8_t>(value);
            }

            return true;
        }
    }

    RankingStore::RankingStore(std::string path)
        : path(std::move(path))
    {
    }

    // Load - 파일을 한 줄씩 읽어 메모리 목록과 runId 색인을 다시 만듦
    size_t RankingStore::Load()
    {
        records.clear();
        indexById.clear();
        if (path.empty())
        {
            return 0;
        }

        std::ifstream file(path);
        std::string line;
        while (std::getline(file, line))
        {
            RunRecord record;
            // 서버가 쓰는 도중 꺼지면 마지막 줄이 잘려 있을 수 있음 - 그 줄은 ACK를 보내기 전이므로 버려도 됨
            if (FromLine(line, record) == false)
            {
                continue;
            }

            std::string key = ToHex(record.runId);
            if (indexById.count(key) > 0)
            {
                continue;
            }

            indexById[key] = records.size();
            records.push_back(record);
        }

        return records.size();
    }

    // Submit - 중복이면 기존 순위를, 새 기록이면 파일에 먼저 쓴 뒤 메모리에 넣고 순위를 돌려줌
    bool RankingStore::Submit(const RunRecord& record, Outcome& outcome)
    {
        std::string key = ToHex(record.runId);
        auto found = indexById.find(key);
        if (found != indexById.end())
        {
            // 이미 저장한 기록 - ACK가 유실돼 다시 온 경우이므로 처음과 같은 답을 돌려줌
            outcome.status = AckStatus::Duplicate;
            outcome.rank = RankOf(records[found->second]);
            outcome.total = Count();
            return true;
        }

        if (Append(record) == false)
        {
            return false;
        }

        indexById[key] = records.size();
        records.push_back(record);
        outcome.status = AckStatus::Accepted;
        outcome.rank = RankOf(record);
        outcome.total = Count();
        return true;
    }

    // RankOf - 이 기록의 순위 (1부터)
    uint32_t RankingStore::RankOf(const RunRecord& record) const
    {
        // 정렬하지 않고 나보다 나은 기록 수만 셈 - 기록이 수천 개 수준이면 충분하고 동점은 같은 순위가 됨
        uint32_t better = 0;
        for (const RunRecord& other : records)
        {
            if (IsBetter(other, record))
            {
                better++;
            }
        }

        return better + 1;
    }

    // Append - 파일 끝에 한 줄 덧붙이고 디스크까지 내림. 하나라도 실패하면 false
    bool RankingStore::Append(const RunRecord& record)
    {
        if (path.empty())
        {
            return true;
        }

        FILE* file = std::fopen(path.c_str(), "ab");
        if (file == nullptr)
        {
            return false;
        }

        std::string line = ToLine(record) + "\n";
        bool ok = std::fwrite(line.data(), 1, line.size(), file) == line.size() && std::fflush(file) == 0;

        // fflush는 OS 버퍼까지만 넘김 - 디스크에 실제로 쓰일 때까지 기다려야 전원이 나가도 남음
#ifdef _WIN32
        ok = ok && _commit(_fileno(file)) == 0;
#else
        ok = ok && fsync(fileno(file)) == 0;
#endif
        ok = std::fclose(file) == 0 && ok;
        return ok;
    }

    bool IsBetter(const RunRecord& a, const RunRecord& b)
    {
        if (a.wave != b.wave)
        {
            return a.wave > b.wave;
        }

        if (a.life != b.life)
        {
            return a.life > b.life;
        }

        return a.elapsedMs < b.elapsedMs;
    }

    // ToLine - "runId ruleset wave total cleared life summons bestHand elapsedMs" 공백 구분 한 줄
    std::string ToLine(const RunRecord& record)
    {
        std::ostringstream line;
        line << ToHex(record.runId) << ' ' << record.ruleset << ' ' << record.wave << ' ' << record.totalWaves
            << ' ' << (record.cleared ? 1 : 0) << ' ' << record.life << ' ' << record.summons
            << ' ' << static_cast<unsigned>(record.bestHand) << ' ' << record.elapsedMs;
        return line.str();
    }

    // FromLine - ToLine의 반대. 필드 수·범위·값 검사 중 하나라도 어긋나면 false
    bool FromLine(const std::string& line, RunRecord& record)
    {
        std::istringstream in(line);
        std::string id;
        RunRecord parsed;
        unsigned wave = 0;
        unsigned totalWaves = 0;
        unsigned cleared = 0;
        unsigned life = 0;
        unsigned summons = 0;
        unsigned bestHand = 0;
        unsigned long long elapsed = 0;

        if (!(in >> id >> parsed.ruleset >> wave >> totalWaves >> cleared >> life >> summons >> bestHand >> elapsed))
        {
            return false;
        }

        std::string rest;
        if ((in >> rest) || ParseHex(id, parsed.runId) == false
            || wave > 0xFFFF || totalWaves > 0xFFFF || cleared > 1 || life > 0xFFFF
            || summons > 0xFFFF || bestHand > 0xFF || elapsed > 0xFFFFFFFFull)
        {
            return false;
        }

        parsed.wave = static_cast<uint16_t>(wave);
        parsed.totalWaves = static_cast<uint16_t>(totalWaves);
        parsed.cleared = cleared == 1;
        parsed.life = static_cast<uint16_t>(life);
        parsed.summons = static_cast<uint16_t>(summons);
        parsed.bestHand = static_cast<uint8_t>(bestHand);
        parsed.elapsedMs = static_cast<uint32_t>(elapsed);

        // 파일을 손으로 고쳤거나 다른 버전이 쓴 줄도 여기서 거름
        if (Validate(parsed) != RejectReason::None)
        {
            return false;
        }

        record = parsed;
        return true;
    }
}
