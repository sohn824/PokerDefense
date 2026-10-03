// 서버 테스트 - 외부 라이브러리 없이 실행 파일 하나로 돎 (ctest가 종료 코드로 판정)

#include <atomic>
#include <chrono>
#include <cstdio>
#include <fstream>
#include <functional>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include <winsock2.h>
#include <ws2tcpip.h>

#include "FrameReader.h"
#include "Protocol.h"
#include "RankingStore.h"
#include "TcpServer.h"

using namespace rank;

namespace
{
    struct TestCase
    {
        const char* name;
        void (*run)();
    };

    std::vector<TestCase>& Registry()
    {
        static std::vector<TestCase> tests;
        return tests;
    }

    struct Registrar
    {
        Registrar(const char* name, void (*run)()) { Registry().push_back({name, run}); }
    };

    struct Failure : std::runtime_error
    {
        using std::runtime_error::runtime_error;
    };
}

#define TEST(name) \
    static void name(); \
    static Registrar registrar_##name(#name, name); \
    static void name()

#define CHECK(expression) \
    do \
    { \
        if (!(expression)) \
        { \
            throw Failure(std::string(__FILE__) + ":" + std::to_string(__LINE__) + " " + #expression); \
        } \
    } while (false)

namespace
{
    // 서버와 클라이언트가 같은 바이트를 만드는지 확인하는 기준
    // Unity 쪽 RunProtocolTests에도 같은 기록과 같은 정답 바이트열이 들어 있고,
    // 양쪽이 각자의 인코딩 결과를 이 바이트열과 비교함 - 형식이 어긋나면 어긋난 쪽 테스트가 실패
    RunRecord SampleRecord()
    {
        RunRecord record;
        for (uint8_t i = 0; i < 16; i++)
        {
            record.runId[i] = i;
        }

        record.ruleset = "hand-loop-v1";
        record.wave = 37;
        record.totalWaves = 50;
        record.cleared = false;
        record.life = 0;
        record.summons = 37;
        record.bestHand = 7;
        record.elapsedMs = 812345;
        return record;
    }

    // [길이 45][버전 1][종류 1][runId 00~0f][ruleset 길이 12]["hand-loop-v1"]
    // [wave 37][total 50][cleared 0][life 0][summons 37][bestHand 7][elapsedMs 812345]
    const char* SampleFrameHex =
        "0000002d" "0101" "000102030405060708090a0b0c0d0e0f" "0c" "68616e642d6c6f6f702d7631"
        "0025" "0032" "00" "0000" "0025" "07" "000c6539";

    std::vector<uint8_t> FromHex(std::string hex)
    {
        std::string compact;
        for (char c : hex)
        {
            if (c != ' ')
            {
                compact.push_back(c);
            }
        }

        std::vector<uint8_t> bytes;
        for (size_t i = 0; i + 1 < compact.size(); i += 2)
        {
            bytes.push_back(static_cast<uint8_t>(std::stoi(compact.substr(i, 2), nullptr, 16)));
        }

        return bytes;
    }

    RunRecord Record(uint8_t id, uint16_t wave, uint16_t life, uint32_t elapsedMs)
    {
        RunRecord record = SampleRecord();
        record.runId.fill(0);
        record.runId[0] = id;
        record.wave = wave;
        record.summons = wave;
        record.life = life;
        record.cleared = life > 0;
        if (record.cleared)
        {
            record.wave = 50;
            record.summons = 50;
        }

        record.elapsedMs = elapsedMs;
        return record;
    }

    std::string TempPath(const char* name)
    {
        char buffer[MAX_PATH];
        GetTempPathA(MAX_PATH, buffer);
        std::string path = std::string(buffer) + name;
        std::remove(path.c_str());
        return path;
    }
}

// ---------- 프로토콜 ----------

TEST(제출_메시지는_고정_바이트열과_같다)
{
    CHECK(MakeFrame(EncodeSubmit(SampleRecord())) == FromHex(SampleFrameHex));
}

TEST(제출_메시지를_다시_읽으면_같은_값이다)
{
    std::vector<uint8_t> payload = EncodeSubmit(SampleRecord());
    RunRecord decoded;
    CHECK(DecodeSubmit(payload.data(), payload.size(), decoded));
    CHECK(decoded.runId == SampleRecord().runId);
    CHECK(decoded.ruleset == "hand-loop-v1");
    CHECK(decoded.wave == 37 && decoded.life == 0 && decoded.summons == 37);
    CHECK(decoded.bestHand == 7 && decoded.elapsedMs == 812345 && decoded.cleared == false);
}

TEST(모자라거나_남는_바이트가_있으면_읽지_않는다)
{
    std::vector<uint8_t> payload = EncodeSubmit(SampleRecord());
    RunRecord decoded;
    CHECK(DecodeSubmit(payload.data(), payload.size() - 1, decoded) == false);

    payload.push_back(0);
    CHECK(DecodeSubmit(payload.data(), payload.size(), decoded) == false);
}

TEST(다른_버전이나_종류는_읽지_않는다)
{
    std::vector<uint8_t> payload = EncodeSubmit(SampleRecord());
    RunRecord decoded;
    payload[0] = 2;
    CHECK(DecodeSubmit(payload.data(), payload.size(), decoded) == false);

    payload[0] = ProtocolVersion;
    payload[1] = static_cast<uint8_t>(MessageType::SubmitAck);
    CHECK(DecodeSubmit(payload.data(), payload.size(), decoded) == false);
}

TEST(ACK를_다시_읽으면_같은_값이다)
{
    SubmitAck ack;
    ack.runId = SampleRecord().runId;
    ack.status = AckStatus::Duplicate;
    ack.rank = 3;
    ack.total = 12;
    std::vector<uint8_t> payload = EncodeAck(ack);

    SubmitAck decoded;
    CHECK(DecodeAck(payload.data(), payload.size(), decoded));
    CHECK(decoded.runId == ack.runId && decoded.status == AckStatus::Duplicate);
    CHECK(decoded.rank == 3 && decoded.total == 12);
}

TEST(규칙에_맞지_않는_값은_거부한다)
{
    CHECK(Validate(SampleRecord()) == RejectReason::None);

    RunRecord record = SampleRecord();
    record.ruleset = "economy-v0";
    CHECK(Validate(record) == RejectReason::UnknownRuleset);

    record = SampleRecord();
    record.wave = 51;
    CHECK(Validate(record) == RejectReason::OutOfRange);

    // 클리어 못 했는데 라이프가 남아 있을 수는 없음
    record = SampleRecord();
    record.life = 5;
    CHECK(Validate(record) == RejectReason::OutOfRange);

    // 라운드당 소환은 최대 1기
    record = SampleRecord();
    record.summons = 38;
    CHECK(Validate(record) == RejectReason::OutOfRange);

    record = SampleRecord();
    record.bestHand = 13;
    CHECK(Validate(record) == RejectReason::OutOfRange);
}

// ---------- 프레임 조립 ----------

TEST(한_바이트씩_와도_프레임을_조립한다)
{
    std::vector<uint8_t> frame = FromHex(SampleFrameHex);
    FrameReader reader(MaxPayloadSize);
    std::vector<uint8_t> payload;

    for (size_t i = 0; i + 1 < frame.size(); i++)
    {
        reader.Append(&frame[i], 1);
        CHECK(reader.Next(payload) == FrameReader::Result::NeedMore);
    }

    reader.Append(&frame.back(), 1);
    CHECK(reader.Next(payload) == FrameReader::Result::Frame);
    CHECK(payload == EncodeSubmit(SampleRecord()));
    CHECK(reader.Buffered() == 0);
}

TEST(붙어서_온_두_프레임을_나눠_꺼낸다)
{
    std::vector<uint8_t> frame = FromHex(SampleFrameHex);
    std::vector<uint8_t> twice = frame;
    twice.insert(twice.end(), frame.begin(), frame.begin() + 10);

    FrameReader reader(MaxPayloadSize);
    reader.Append(twice.data(), twice.size());
    std::vector<uint8_t> payload;
    CHECK(reader.Next(payload) == FrameReader::Result::Frame);
    CHECK(reader.Next(payload) == FrameReader::Result::NeedMore);

    reader.Append(frame.data() + 10, frame.size() - 10);
    CHECK(reader.Next(payload) == FrameReader::Result::Frame);
    CHECK(payload == EncodeSubmit(SampleRecord()));
}

TEST(길이가_0이거나_너무_크면_오류다)
{
    uint8_t zero[] = {0, 0, 0, 0};
    FrameReader first(MaxPayloadSize);
    std::vector<uint8_t> payload;
    first.Append(zero, sizeof(zero));
    CHECK(first.Next(payload) == FrameReader::Result::Error);

    // 본문이 오기 전에 길이만 보고 바로 거부해야 함
    uint8_t huge[] = {0x7F, 0xFF, 0xFF, 0xFF};
    FrameReader second(MaxPayloadSize);
    second.Append(huge, sizeof(huge));
    CHECK(second.Next(payload) == FrameReader::Result::Error);
    CHECK(second.Next(payload) == FrameReader::Result::Error);
}

// ---------- 기록 저장 ----------

TEST(순위는_웨이브_라이프_시간_순이다)
{
    RankingStore store("");
    RankingStore::Outcome outcome;
    CHECK(store.Submit(Record(1, 30, 0, 1000), outcome) && outcome.rank == 1);
    CHECK(store.Submit(Record(2, 40, 0, 1000), outcome) && outcome.rank == 1);
    CHECK(store.Submit(Record(3, 20, 0, 1000), outcome) && outcome.rank == 3);
    CHECK(store.Submit(Record(4, 50, 5, 9000), outcome) && outcome.rank == 1);
    CHECK(store.Submit(Record(5, 50, 5, 8000), outcome) && outcome.rank == 1 && outcome.total == 5);
}

TEST(같은_runId는_한_번만_저장하고_같은_순위를_돌려준다)
{
    RankingStore store("");
    RankingStore::Outcome outcome;
    CHECK(store.Submit(Record(1, 30, 0, 1000), outcome) && outcome.status == AckStatus::Accepted);
    CHECK(store.Submit(Record(2, 40, 0, 1000), outcome));

    CHECK(store.Submit(Record(1, 30, 0, 1000), outcome));
    CHECK(outcome.status == AckStatus::Duplicate && outcome.rank == 2 && outcome.total == 2);
}

TEST(파일에서_다시_읽고_잘린_마지막_줄은_버린다)
{
    std::string path = TempPath("rank_store_test.txt");
    {
        RankingStore store(path);
        RankingStore::Outcome outcome;
        CHECK(store.Submit(Record(1, 30, 0, 1000), outcome));
        CHECK(store.Submit(Record(2, 40, 0, 1000), outcome));
    }

    // 쓰는 도중 꺼진 상황 - 줄 중간에서 끊긴 기록
    {
        std::ofstream file(path, std::ios::app | std::ios::binary);
        file << "03000000000000000000000000000000 hand-loop-v1 25";
    }

    RankingStore reloaded(path);
    CHECK(reloaded.Load() == 2);

    RankingStore::Outcome outcome;
    CHECK(reloaded.Submit(Record(1, 30, 0, 1000), outcome) && outcome.status == AckStatus::Duplicate);
    std::remove(path.c_str());
}

// ---------- 소켓 ----------

namespace
{
    // 테스트용 블로킹 클라이언트 - 서버는 메인 스레드에서 Poll로 돌리고 클라이언트는 별도 스레드에서 돎
    SOCKET Connect(uint16_t port)
    {
        SOCKET client = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_port = htons(port);
        inet_pton(AF_INET, "127.0.0.1", &address.sin_addr);
        if (connect(client, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0)
        {
            closesocket(client);
            return INVALID_SOCKET;
        }

        DWORD timeout = 3000;
        setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
        return client;
    }

    bool SendAll(SOCKET socket, const uint8_t* data, size_t size)
    {
        size_t sent = 0;
        while (sent < size)
        {
            int result = send(socket, reinterpret_cast<const char*>(data + sent), static_cast<int>(size - sent), 0);
            if (result <= 0)
            {
                return false;
            }

            sent += static_cast<size_t>(result);
        }

        return true;
    }

    // 프레임 하나를 끝까지 받음. 연결이 끊기면 false
    bool ReceiveFrame(SOCKET socket, std::vector<uint8_t>& payload)
    {
        FrameReader reader(MaxPayloadSize);
        uint8_t buffer[256];
        for (;;)
        {
            FrameReader::Result result = reader.Next(payload);
            if (result != FrameReader::Result::NeedMore)
            {
                return result == FrameReader::Result::Frame;
            }

            int received = recv(socket, reinterpret_cast<char*>(buffer), sizeof(buffer), 0);
            if (received <= 0)
            {
                return false;
            }

            reader.Append(buffer, static_cast<size_t>(received));
        }
    }

    // 클라이언트 작업이 끝날 때까지 서버를 한 단계씩 돌림
    void RunServerUntil(TcpServer& server, std::thread& client, const std::atomic<bool>& finished)
    {
        auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(5);
        while (finished == false && std::chrono::steady_clock::now() < deadline)
        {
            server.Poll(10);
        }

        client.join();
    }
}

TEST(나눠서_보낸_제출에_ACK로_순위를_돌려준다)
{
    RankingStore store("");
    TcpServer server(store, [](const std::string&) {});
    CHECK(server.Start(0));

    std::atomic<bool> finished{false};
    SubmitAck ack;
    bool received = false;
    std::thread client([&]
    {
        SOCKET socket = Connect(server.Port());
        std::vector<uint8_t> frame = MakeFrame(EncodeSubmit(SampleRecord()));

        // 앞 7바이트와 나머지를 따로 보내 서버가 조각을 이어 붙이는지 확인
        SendAll(socket, frame.data(), 7);
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
        SendAll(socket, frame.data() + 7, frame.size() - 7);

        std::vector<uint8_t> payload;
        received = ReceiveFrame(socket, payload) && DecodeAck(payload.data(), payload.size(), ack);
        closesocket(socket);
        finished = true;
    });

    RunServerUntil(server, client, finished);
    CHECK(received);
    CHECK(ack.runId == SampleRecord().runId);
    CHECK(ack.status == AckStatus::Accepted && ack.rank == 1 && ack.total == 1);
    CHECK(store.Count() == 1);
}

TEST(규칙에_맞지_않는_제출은_저장하지_않고_거부를_알린다)
{
    RankingStore store("");
    TcpServer server(store, [](const std::string&) {});
    CHECK(server.Start(0));

    RunRecord record = SampleRecord();
    record.life = 3;  // 클리어 못 했는데 라이프가 남음

    std::atomic<bool> finished{false};
    SubmitAck ack;
    bool received = false;
    std::thread client([&]
    {
        SOCKET socket = Connect(server.Port());
        std::vector<uint8_t> frame = MakeFrame(EncodeSubmit(record));
        SendAll(socket, frame.data(), frame.size());
        std::vector<uint8_t> payload;
        received = ReceiveFrame(socket, payload) && DecodeAck(payload.data(), payload.size(), ack);
        closesocket(socket);
        finished = true;
    });

    RunServerUntil(server, client, finished);
    CHECK(received);
    CHECK(ack.status == AckStatus::Rejected && ack.reason == RejectReason::OutOfRange);
    CHECK(store.Count() == 0);
}

TEST(잘못된_길이를_보내면_연결을_끊는다)
{
    RankingStore store("");
    TcpServer server(store, [](const std::string&) {});
    CHECK(server.Start(0));

    std::atomic<bool> finished{false};
    bool closedByServer = false;
    std::thread client([&]
    {
        SOCKET socket = Connect(server.Port());
        uint8_t huge[] = {0x7F, 0xFF, 0xFF, 0xFF};
        SendAll(socket, huge, sizeof(huge));
        std::vector<uint8_t> payload;
        closedByServer = ReceiveFrame(socket, payload) == false;
        closesocket(socket);
        finished = true;
    });

    RunServerUntil(server, client, finished);
    CHECK(closedByServer);
    CHECK(store.Count() == 0);
}

TEST(아무것도_보내지_않는_연결은_시간이_지나면_끊는다)
{
    RankingStore store("");
    TcpServer server(store, [](const std::string&) {});
    server.idleTimeout = std::chrono::milliseconds(100);
    CHECK(server.Start(0));

    std::atomic<bool> finished{false};
    bool closedByServer = false;
    std::thread client([&]
    {
        SOCKET socket = Connect(server.Port());
        std::vector<uint8_t> payload;
        closedByServer = ReceiveFrame(socket, payload) == false;
        closesocket(socket);
        finished = true;
    });

    RunServerUntil(server, client, finished);
    CHECK(closedByServer);
    CHECK(server.ConnectionCount() == 0);
}

int main()
{
    SetConsoleOutputCP(65001);
    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    int failed = 0;
    for (const TestCase& test : Registry())
    {
        try
        {
            test.run();
            std::printf("[PASS] %s\n", test.name);
        }
        catch (const std::exception& error)
        {
            failed++;
            std::printf("[FAIL] %s\n       %s\n", test.name, error.what());
        }
    }

    std::printf("\n%zu개 중 %d개 실패\n", Registry().size(), failed);
    WSACleanup();
    return failed == 0 ? 0 : 1;
}
