#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

#include <winsock2.h>
#include <windows.h>

#include "RankingStore.h"
#include "TcpServer.h"

namespace
{
    std::atomic<bool> stopRequested{false};

    // Ctrl+C를 받으면 루프가 다음 Poll에서 빠져나오게 표시만 함 (핸들러는 다른 스레드에서 불림)
    BOOL WINAPI OnConsoleSignal(DWORD)
    {
        stopRequested = true;
        return TRUE;
    }

    // 로그 한 줄 출력 - 바로 보이도록 매번 flush
    void Log(const std::string& message)
    {
        std::printf("%s\n", message.c_str());
        std::fflush(stdout);
    }
}

// 사용법: RankServer [--port 7777] [--data runs.txt]
int main(int argc, char** argv)
{
    SetConsoleOutputCP(CP_UTF8);

    uint16_t port = 7777;
    std::string dataPath = "runs.txt";
    for (int i = 1; i + 1 < argc; i += 2)
    {
        if (std::strcmp(argv[i], "--port") == 0)
        {
            port = static_cast<uint16_t>(std::atoi(argv[i + 1]));
        }
        else if (std::strcmp(argv[i], "--data") == 0)
        {
            dataPath = argv[i + 1];
        }
    }

    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
    {
        Log("WSAStartup 실패");
        return 1;
    }

    rank::RankingStore store(dataPath);
    size_t loaded = store.Load();

    rank::TcpServer server(store, Log);
    if (server.Start(port) == false)
    {
        Log("포트 " + std::to_string(port) + " 열기 실패");
        WSACleanup();
        return 1;
    }

    Log("기록 " + std::to_string(loaded) + "개 불러옴 (" + dataPath + ")");
    Log("포트 " + std::to_string(server.Port()) + "에서 대기 중 - Ctrl+C로 종료");
    SetConsoleCtrlHandler(OnConsoleSignal, TRUE);

    // 0.2초마다 한 번씩은 돌아와서 종료 요청을 확인
    while (stopRequested == false)
    {
        server.Poll(200);
    }

    server.Stop();
    WSACleanup();
    Log("종료");
    return 0;
}
