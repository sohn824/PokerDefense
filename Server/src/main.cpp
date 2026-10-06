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
    // 종료 요청 여부 - Ctrl+C를 누르면 true가 되고, main의 while 루프가 이 값을 보고 빠져나옴
    // (Ctrl+C를 처리하는 OnConsoleSignal은 OS가 main과 다른 스레드에서 실행함
    //  -> 두 스레드가 함께 쓰는 변수라 atomic으로 둠, 일반 bool이면 바뀐 값을 main이 못 볼 수 있음)
    std::atomic<bool> stopRequested{false};

    // Ctrl+C를 누르면 stopRequested만 true로 바꿈 (실제 종료는 main 루프가 처리)
    // TRUE를 돌려주면 OS가 프로그램을 바로 끄지 않아, main의 Stop·WSACleanup까지 실행됨
    BOOL WINAPI OnConsoleSignal(DWORD)
    {
        stopRequested = true;
        return TRUE;
    }

    // 로그 한 줄 출력 - printf가 출력을 모아 두지 않고 바로 콘솔에 보이도록 매번 fflush
    void Log(const std::string& message)
    {
        std::printf("%s\n", message.c_str());
        std::fflush(stdout);
    }
}

// 사용법: RankServer [--port 7777] [--data runs.txt]
// (생략하면 포트 7777, 기록 파일은 실행한 폴더의 runs.txt)
int main(int argc, char** argv)
{
    SetConsoleOutputCP(CP_UTF8);

    uint16_t port = 7777;
    std::string dataPath = "runs.txt";
    // 실행 인자를 "--이름 값" 두 개씩 짝으로 읽음 (argv[0]은 프로그램 이름이라 1부터)
    // --port, --data 외의 이름은 무시하고, 값이 빠진 마지막 이름도 무시
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

    // Winsock 사용 시작 - 소켓 함수를 쓰기 전에 한 번 필요 (끝에서 WSACleanup과 짝)
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

    // Poll이 최대 0.2초마다 돌아오므로, 그때마다 종료 요청(stopRequested)을 확인
    while (stopRequested == false)
    {
        server.Poll(200);
    }

    server.Stop();
    WSACleanup();
    Log("종료");
    return 0;
}
