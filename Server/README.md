# Rank Server

게임이 끝나면 클라이언트가 보내는 결과를 받아 저장하고 순위를 돌려주는 C++ TCP 서버입니다.

## 구성

```
Server/
├── RankServer.sln           솔루션 (Visual Studio 2022)
├── RankServer.vcxproj       서버 실행 파일 (시작 프로젝트)
├── RankCore.vcxproj         서버와 테스트가 함께 쓰는 정적 라이브러리
├── RankTests.vcxproj        테스트 실행 파일
├── Common.props             세 프로젝트 공통 설정 (C++17, /utf-8, 경고 수준, ws2_32 링크, 출력 폴더)
├── src/                     서버 소스 (프로토콜, 프레임 조립, 저장소, 소켓 루프, main)
├── tests/                   테스트 소스
└── build.bat                명령줄 빌드 + 테스트
```

빌드 결과는 `bin/<구성>/`, 중간 파일은 `obj/`에 생기며 커밋하지 않습니다. 소스 파일을 추가하면 해당 `.vcxproj`와 `.vcxproj.filters`에도 넣어야 합니다.

## Visual Studio에서

1. `RankServer.sln`을 엽니다.
2. F5를 누르면 `RankServer`가 `--port 7777 --data runs.txt`로 실행됩니다. 기록 파일 `runs.txt`는 `Server/` 폴더에 생깁니다.
3. 테스트를 디버깅하려면 `RankTests`를 시작 프로젝트로 바꾸고 F5를 누릅니다.

## 명령줄에서

```
Server\build.bat          Release 빌드 후 테스트 실행
Server\build.bat Debug    Debug 빌드 후 테스트 실행
Server\bin\Release\RankServer.exe --port 7777 --data runs.txt
```

- `--port`: 기본값은 7777입니다. 게임의 `RunReporter` 인스펙터 값과 맞춰야 합니다.
- `--data`: 기록 파일입니다. 한 줄에 한 기록씩 저장하며, 서버를 다시 켜면 이 파일을 읽어 복구합니다.
- Ctrl+C로 종료합니다.
