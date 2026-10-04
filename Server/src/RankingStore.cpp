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
        // ParseHex - ToHex의 반대, 저장 파일의 32자리 16진수를 runId 16바이트로 되돌림
		// 예 : "000102030405060708090a0b0c0d0e0f" -> 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
        // 소문자만 받음 - 파일은 항상 ToHex(소문자)로 쓰므로 대문자가 있으면 잘못된 파일로 보고 거름
        bool ParseHex(const std::string& text, RunId& id)
        {
            if (text.size() != id.size() * 2)
            {
                return false;
            }

            // 바이트 i마다 글자 두 개(text[i*2], text[i*2+1])를 읽어 한 바이트로 합침
            for (size_t i = 0; i < id.size(); i++)
            {
                int value = 0;
                for (size_t j = 0; j < 2; j++)
                {
                    // 글자 -> 숫자: '0'~'9' = 0~9, 'a'~'f' = 10~15, 그 밖(대문자 포함)은 실패
                    char c = text[i * 2 + j];
                    int digit = c >= '0' && c <= '9' ? c - '0'
                        : c >= 'a' && c <= 'f' ? c - 'a' + 10
                        : -1;
                    if (digit < 0)
                    {
                        return false;
                    }

                    // 첫 글자가 위 자리 - 예: "2d" -> 0*16+2 = 2 -> 2*16+13 = 45 (0x2D)
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
            if (FromLine(line, record) == false)
            {
                continue;
            }

            // 같은 runId 줄이 또 나오면 첫 줄만 쓰고 나머지는 건너뜀 (두 줄은 같은 기록이라 내용도 같음)
            // 참고 : 같은 줄이 두 번 생기는 경우
            //   RankingStore::Append 안에서 줄을 OS까지 넘긴 뒤(fwrite·fflush),
            //   디스크에 확실히 기록(_commit)하거나 파일을 닫다가(fclose) 실패
            //   -> Submit이 false라 메모리에 안 넣고 ACK 없이 끊음 (줄은 이미 OS에 넘어가 파일에 남아 있을 수 있음)
            //   -> 클라이언트가 다시 보냄 -> 메모리에 없으니 같은 줄을 한 번 더 붙임
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

    bool RankingStore::Submit(const RunRecord& record, Outcome& outcome)
    {
        std::string key = ToHex(record.runId);
        auto found = indexById.find(key);
        if (found != indexById.end())
        {
            // 이미 저장한 기록 - ACK가 유실돼 다시 온 경우라 다시 저장하지 않음
            // 순위는 지금 기록 전체로 다시 계산 (그 사이 기록이 늘었으면 처음과 다를 수 있음)
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

    uint32_t RankingStore::RankOf(const RunRecord& record) const
    {
        // 정렬하지 않고 인자로 들어온 record보다 나은 기록 수만 셈
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

    bool RankingStore::Append(const RunRecord& record)
    {
        // path가 비면(테스트용) 파일 없이 메모리(records)에만 보관하는 모드 -> 쓰지 않고 성공으로 처리
        if (path.empty())
        {
            return true;
        }

        // fopen 두 번째 인자 "a+b" = 파일 열기 모드
        //   a = 쓰기는 항상 파일 끝에 덧붙임 (파일이 없으면 새로 만듦)
        //   + = 읽기도 허용 - 아래에서 마지막 바이트를 확인하려고
        //   b = 바이너리 - 텍스트 모드면 \n이 \r\n으로 바뀌고 끝에서 -1 이동(fseek)도 보장되지 않음
        FILE* file = std::fopen(path.c_str(), "a+b");
        if (file == nullptr)
        {
            return false;
        }

        // 파일 끝이 줄바꿈이 아니면(쓰다 만 줄이 남아 있으면) 줄바꿈을 먼저 넣어 그 줄과 분리
        // 그대로 이어 쓰면 새 기록이 잘린 줄과 한 줄로 합쳐져, 다음 Load 때 ACK까지 보낸 기록이 같이 버려짐
        // (서버가 쓰는 도중 꺼졌거나, 디스크가 가득 차서 줄의 일부만 써진 경우)
        // 빈 파일이면 fseek가 실패하므로 줄바꿈 없이 그대로 씀
        std::string line = ToLine(record) + "\n";
        if (std::fseek(file, -1, SEEK_END) == 0 && std::fgetc(file) != '\n')
        {
            line.insert(line.begin(), '\n');
        }

        // 읽은 뒤 쓰기로 넘어가려면 위치 지정이 한 번 필요함
        std::fseek(file, 0, SEEK_END);
        // 쓴 바이트 수가 모자라면 실패 (디스크가 가득 차는 등) - 이때 줄의 일부만 파일에 남을 수 있음
        bool ok = std::fwrite(line.data(), 1, line.size(), file) == line.size() && std::fflush(file) == 0;

        // fflush는 OS까지만 넘김 - OS는 받은 데이터를 RAM의 파일 캐시(OS가 파일 내용을 임시로 두는 영역)에 들고 있다가
        // 나중에 디스크에 쓰므로, 그 사이 전원이 나가면 사라짐
        // ACK를 보낸 기록이 사라지면 안 되므로 디스크에 실제로 쓰일 때까지 기다림 (서버 프로그램만 죽는 건 fflush로 충분)
        // (_commit = Windows, fsync = 그 외 OS)
#ifdef _WIN32
        ok = ok && _commit(_fileno(file)) == 0;
#else //_WIN32
        ok = ok && fsync(fileno(file)) == 0;
#endif //_WIN32
        // fclose는 앞에서 실패했어도 꼭 불러야 해서 && 앞에 둠 (ok && fclose 순서면 ok가 false일 때 안 닫힘)
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

    // ToLine - 기록 하나를 파일에 쓸 텍스트 한 줄로 만듦 (필드 사이는 공백 한 칸)
    //   순서: runId ruleset wave totalWaves cleared life summons bestHand elapsedMs  (각 이름 자리에 그 값이 들어감)
    //   예:   000102030405060708090a0b0c0d0e0f hand-loop-v1 37 50 0 0 37 7 812345
    //   끝에 줄바꿈은 안 붙임 (파일에 쓸 때 Append가 붙이기 때문)
    std::string ToLine(const RunRecord& record)
    {
        // runId는 ToHex로 (이진 바이트를 그대로 쓰면 줄바꿈·공백 바이트가 섞여 줄·필드가 깨짐)
        // bestHand는 uint8_t(= unsigned char)라 그대로 쓰면 숫자가 아니라 글자로 찍힘 -> unsigned int로 바꿔 숫자로 씀
        std::ostringstream line;
        line << ToHex(record.runId) << ' ' << record.ruleset << ' ' << record.wave << ' ' << record.totalWaves
            << ' ' << (record.cleared ? 1 : 0) << ' ' << record.life << ' ' << record.summons
            << ' ' << static_cast<unsigned int>(record.bestHand) << ' ' << record.elapsedMs;
        return line.str();
    }

    // FromLine - 파일에서 읽은 텍스트 한 줄(ToLine이 만든 형식)을 다시 기록 하나로 되돌림
    //   필드 수·범위·값 검사 중 하나라도 어긋나면 false (이때 record는 그대로)
    bool FromLine(const std::string& line, RunRecord& record)
    {
        std::istringstream in(line);
        std::string id;
        RunRecord parsed;  // 읽은 값은 임시로 여기(parsed)에 채우며 검사하고, 모두 통과했을 때만 record로 복사함

        // 숫자는 RunRecord 타입보다 큰 타입으로 받고 아래에서 범위를 직접 검사
        // (uint8_t로 바로 읽으면 숫자가 아니라 글자 하나를 읽음, 음수는 부호 없는 타입이라 큰 수가 되어 범위 검사에서 걸림)
        unsigned int wave = 0;
        unsigned int totalWaves = 0;
        unsigned int cleared = 0;
        unsigned int life = 0;
        unsigned int summons = 0;
        unsigned int bestHand = 0;
        unsigned long long elapsed = 0;

        // 필드 9개가 다 있어야 함 - 쓰다 만(잘린) 줄이 있으면 여기서 걸림
        if (!(in >> id >> parsed.ruleset >> wave >> totalWaves >> cleared >> life >> summons >> bestHand >> elapsed))
        {
            return false;
        }

        // 9개 뒤에 더 읽히는 게 있으면 실패 (두 줄이 한 줄로 합쳐졌거나 다른 형식의 줄)
        // 그다음 runId 형식(ParseHex), 각 값이 들어갈 타입 범위(uint16_t·uint8_t·uint32_t 최대값), cleared는 0·1만
        // (아래에서 작은 타입으로 줄이기 전에 확인해야 함 - 예: wave 65586을 uint16_t로 줄이면 50이 되어 Validate를 통과해 버림)
        std::string rest;
        if ((in >> rest) || ParseHex(id, parsed.runId) == false
            || wave > 0xFFFF || totalWaves > 0xFFFF || cleared > 1 || life > 0xFFFF
            || summons > 0xFFFF || bestHand > 0xFF || elapsed > 0xFFFFFFFFull)
        {
            return false;
        }

        // 위에서 범위를 확인했으니 실제 타입으로 줄여도 값이 안 잘림
        parsed.wave = static_cast<uint16_t>(wave);
        parsed.totalWaves = static_cast<uint16_t>(totalWaves);
        parsed.cleared = cleared == 1;
        parsed.life = static_cast<uint16_t>(life);
        parsed.summons = static_cast<uint16_t>(summons);
        parsed.bestHand = static_cast<uint8_t>(bestHand);
        parsed.elapsedMs = static_cast<uint32_t>(elapsed);

        // 형식은 맞아도 게임 규칙상 나올 수 없는 값이면 거름 (제출 받을 때와 같은 Validate)
        // 파일을 손으로 고쳤거나 다른 규칙 버전이 쓴 줄이면 여기서 걸림
        if (Validate(parsed) != RejectReason::None)
        {
            return false;
        }

        // 다 통과했을 때만 넘겨줌 - 중간에 실패하면 record는 원래 값 그대로
        record = parsed;
        return true;
    }
}
