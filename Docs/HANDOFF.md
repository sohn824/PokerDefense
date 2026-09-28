# 프로젝트 인수인계 — 2026-09-27

## 먼저 읽을 것

읽는 순서는 [../CLAUDE.md](../CLAUDE.md)의 작업 관례 → 이 문서 → [DESIGN.md](DESIGN.md) §1·9·14~18 → [POLISH.md](POLISH.md)의 다음 작업 순서다. 과거 결정과 자주 빠지는 함정은 [HISTORY.md](HISTORY.md)에서 필요한 만큼 찾아 읽는다.

설계의 기준은 DESIGN이다. 이 문서는 어디서 시작할지, 코드가 어디 있는지, 무엇을 검증했는지를 알려준다. LEGACY_LOOP_PLAN.md는 폐기한 기획의 기록이므로 따르지 않는다.

## 현재 방향

**재화를 굴리는 재미보다 손패를 직접 맞추는 재미가 중심이다.**

한 라운드의 흐름: 5장 드로우 → 일반 교체 → 선택 교체(원할 때만) → 손패 확정과 유닛 1기 소환 → 배치·합치기, 또는 기존 유닛 교체나 소환 포기 → 전투 → 결과 → 다음 손패. 이렇게 50웨이브를 진행하며, 시간 안에 적을 다 못 잡아도 라이프가 남아 있으면 다음 웨이브로 넘어간다.

- **일반 교체**는 자리마다 한 번씩이며, 여러 번에 나눠 바꿀 수 있다.
- **선택 교체**는 **손패마다 1회, 무료, 다음 손패로 이월되지 않는다.** 일반 교체한 자리 하나를 골라, 남은 덱에서 뽑은 서로 다른 후보 3장 중 한 장으로 바꾼다. 후보를 공개하기 전에는 취소할 수 있고, 공개하면 사용한 것으로 친다. 공개한 뒤에는 다시 뽑거나 확정 전으로 돌리거나 일반 교체를 할 수 없다. 다음 손패에서 초기화된다.
- Chip, 유지 보너스, 판매 수입, 상점, 보유 카드, 랜덤 소환은 **삭제했다.** 5라운드마다 상점을 열거나 보조 횟수를 채워주지 않는다.
- 15칸 보드에서 같은 유닛·같은 성급끼리 합치기(최대 ★3), 이동과 자리 교환, 보스 보상 승급권은 그대로 있다. 기존 유닛 교체, 퇴장, 소환 포기는 확인 창을 거치며 되돌려 받는 것은 없다.
- 손패 아래 확률 패널은 카드를 1~4장 골랐을 때만 나타나 그 교체의 확률 분포를 보여준다(DESIGN §19, `HandOdds`). 아무것도 고르지 않았거나 5장을 고르면 숨는다. 고르기 전에 족보를 추천하지 않고, 어느 자리를 바꾸는 게 좋은지도 추천하지 않는다.
- 타이틀, 음량 옵션, 일시정지, 도움말, 재도전, 타이틀 복귀는 구현했다. **런 저장·이어하기는 없다.** 옵션 저장과는 다른 이야기다.
- 어두운 투기장 비주얼과 지금 쓰는 Arena Breaks BGM을 유지한다. 예전 오디오 후보로 되돌리지 않는다. 자세한 내용은 AUDIO_REQUEST.md, ART_REQUEST.md에 있다.

## 코드 지도

경로는 `Assets/_Project/` 기준이다. 규칙을 바꿀 때는 Context → Controller → UI → Editor 씬 설치 → 테스트·문서 순으로 영향을 따라가며 확인한다.

| 책임 | 읽을 파일 |
|---|---|
| 카드·덱·족보, 목표·확률 탐색 | `Scripts/Poker/Deck.cs`, `HandGoals.cs`, `HandOdds.cs`, `HandEvaluator.cs` |
| 손패 단계, 자리 잠금, 후보, 손패당 사용 여부 | `Scripts/Game/Flow/RoundContext.cs`, `RoundController.cs` |
| 라운드·전투 전환과 다음 손패 | `Scripts/Game/Flow/GameFlowController.cs` |
| 라이프·웨이브·승급권 | `Scripts/Game/Flow/StageContext.cs`, `StageController.cs`, `Scripts/Game/Data/StageDefinition.cs`, `Data/Stage_1.asset` |
| 보드, 소환 대기, 합치기, 교체·퇴장·포기 | `Scripts/Game/Board/GridBoard.cs`, `PlacementController.cs` |
| 손패 행동, 후보, 확률 문구 | `Scripts/UI/RoundScreen.cs`, `AssistScreen.cs`, `HandOddsText.cs`, `ActionBarController.cs` |
| 되돌릴 수 없는 보드 조작의 확인 창 | `Scripts/UI/BoardDecisionScreen.cs`, `BoardScreen.cs` |
| 첫 손패·배치·합치기 안내와 메뉴 | `Scripts/UI/OnboardingGuide.cs`, `MenuScreen.cs` |
| 결과·위협 미리보기·기록 | `Scripts/UI/RoundBreakScreen.cs`, `RoundRecap.cs`, `ThreatPreview.cs`, `Scripts/Game/Flow/RunJournal.cs` |
| 씬 설치와 개발용 비교 도구 | `Editor/GameLoopSetup.cs`, `BalanceSimRunner.cs`, `WaveSkipWindow.cs` |
| 회귀 테스트 | `Scripts/Tests/Editor/HandLoopTests.cs`, `AssistTests.cs`, `RoundContextTests.cs`, `StageTests.cs`, `HandOddsTests.cs` |

`TryReplace`는 교체하려던 기존 유닛 인스턴스가 그대로 있을 때만 교체한다. 합칠 수 있는 조합이면 교체로 처리하지 않는다. 확인 창은 취소하면 상태를 바꾸지 않고, 그 위에 메뉴를 열었다 닫아도 정지 상태를 원래대로 돌려놓는다.

## 검증 기록과 한계

**최신:** EditMode **246/246 통과**(2026-09-27, Unity Test Runner 직접 실행). 기존 207개에 멀티스레드·판정 테스트 12개, 확률 표시 테스트 6개, 결과 제출 네트워크 테스트 21개를 더한 수다. C++ 서버 테스트 16개는 `Server/build.bat`(또는 솔루션의 RankTests)로 따로 돈다. 2026-09-21에 교체 상한을 바꿀 때는 기존 테스트의 입력값만 고쳤으므로 개수는 그대로다.

아래는 그 이전 구현 때 남긴 기록이며, 이후 다시 실행하지 않았다.

- EditMode **207/207 통과**(2026-09-15). 손패별 사용과 초기화, 후보 중복·재공개 방지, 목표의 실제 덱·자리 조건, 가득 찬 보드의 교체와 기존 인스턴스·합치기 확인, `HandOdds`의 경우의 수·확률·조합 상한 테스트 6개가 들어 있다. 삭제한 기능의 테스트를 지웠으므로, 예전 241개와 개수를 비교해 회귀를 판단하지 않는다.
- 에디터 Play: 후보를 고른 뒤 다음 손패에서 초기화되는지, W5→W6에 상점이 없는지, 15칸 보드의 교체 확인·취소·메뉴 중첩, 승급권 성장과 전투 시작을 확인했다. 1080×1920과 360×800에서 목표 문구를 확인했고, Missing Script는 0개다.
- Windows 빌드 `Builds/LoopReview/PokerDefense.exe` 성공(빌드 보고서 오류 0 / 경고 3). 실행 파일로 50웨이브를 끝까지 플레이해 본 것은 아니다.
- TMP 폰트 `Maplestory Light SDF.asset`의 `Importer(NativeFormatImporter) generated inconsistent result` 문제가 남아 있다. 빌드가 성공했다는 것과 콘솔에 오류가 하나도 없다는 것은 다른 이야기다.
- **현재 밸런스는 검증하지 않았다.** 유닛 공급을 줄인 뒤에도 웨이브 수치는 일부러 그대로 뒀다. 사람이 끝까지 플레이해 보고, 기기에서 확인하고, 새 기준으로 측정해야 한다. 개발용 웨이브 스킵은 난이도 검증이 아니다.
- BalanceSimRunner는 후보를 단순한 규칙으로 고르는 시뮬레이션이라, 가득 찬 보드에서 전략적으로 교체하는 것까지는 흉내 내지 못한다. 그 결과를 사람의 승률로 보지 않는다.

## 이어받을 때 주의할 것

1. **먼저 작업 트리를 본다.** `git status --short`와 diff를 확인한다. 코드·씬·에셋에 커밋하지 않은 변경이 있을 수 있으니 다른 작업을 되돌리지 않는다. 커밋과 푸시는 사용자가 요청할 때만 한다.
2. **실행과 테스트.** Unity 6000.4.0f1로 열고 Boot 씬부터 흐름을 확인한다. 빌드 씬 순서는 Boot → Title → Game이다. 테스트는 Unity Test Runner에서 EditMode 전체를 돌린다. asmdef는 추가하지 않는다.
3. **구조 규칙.** 의존 방향 `Poker → Game → UI`를 지킨다. Poker는 순수 C#과 `System.Random`만 쓴다. 규칙은 Context, Unity 이벤트는 Controller, 화면 참조는 직렬화된 UI 필드로 관리한다. 기존의 Allman 중괄호와 한글 주석 관례를 따른다.
4. **씬 설치 도구는 조심해서 쓴다.** `GameLoopSetup.UpdateGuideAndAssist()`는 관련 UI를 다시 만든다. 부르기 전에 diff를 확인한다. 전체 `Apply()`는 타이틀까지 다시 만들어서 사용자가 씬에서 고친 것을 덮어쓸 수 있다. 문서 작업 때문에 이걸 실행하지 않는다.
5. **이름이 옛 기능을 가리키는 곳이 있다.** `Joker`는 승급권의 내부 이름이다. `sellButton`/`sellLabel` 같은 직렬화 이름이 남아 있지만 지금은 퇴장·포기 기능이다. SFX enum의 옛 슬롯은 에셋 번호를 맞추려고 남긴 것이니 순서를 바꾸지 않는다. RoundContext의 `PlaceHeldCard`나 상점 관련 주석 일부도 옛 설명일 뿐 실제 API가 아니다.
6. **`.meta`와 원본 에셋은 함께 관리한다.** 빌드나 테스트가 폰트, 렌더 설정, Input Actions preload를 바꿔 놓을 수 있다. 의도한 변경인지 확인하되, 기존 수정까지 한꺼번에 되돌리지 않는다.
7. **실행에 필요 없는 폴더.** `Artifacts/HandLoop/`의 Python 파일은 한 번 쓰고 끝난 데이터 변환 도구이므로 다시 돌리지 않는다. `Tools/Audio/`는 오디오 제작 도구다. Artifacts와 Builds는 배포할 소스가 아니다.
8. **RunJournal은 분석용 로그다.** `Application.persistentDataPath/RunJournals`에 저장된다. 지금 규칙과 비교할 때는 `ruleset=hand-loop-v1`만 쓴다. 런 전체를 재현하거나 저장·이어하기를 해 주지는 않는다.

## 다음 작업은 어디서부터

POLISH의 P0부터 한다. 먼저 실제 플레이에서 선택 교체를 이해하는지, 목표 안내를 쓰는지, 초반 합치기가 얼마나 빠른지, 어느 웨이브에서 지는지, 준비 시간은 얼마나 걸리는지 관찰한다. 문제를 확인한 뒤에 안내·배치 UX나 웨이브 수치를 조정한다. Chip 복원, 추가 보상이나 보관 슬롯, 새 성장 시스템을 먼저 만들지 않는다.

## 멀티스레드 구현과 다음 기술 확장 (2026-09-20)

[멀티스레드 구조](THREADING_PLAN.md)와 [측정·검증 기록](THREADING_RESULTS.md)을 먼저 본다.

- **구조.** 교체 확률 계산(`HandOdds`)을 비동기로 돌리고, 큰 계산만 제한적으로 병렬화했다. RoundController가 `HandOddsWorker` 하나를 갖고, 계산에는 복사해서 만든 바뀌지 않는 `HandOddsRequest`만 넘긴다. 선택·손패가 바뀌거나 후보를 공개하거나 비활성화되면 취소하고, 가장 최근 요청의 결과만 화면에 올린다. Unity API와 UI 이벤트는 메인 스레드에서만 부른다.
- **판정 함수.** 확률 계산은 족보 종류만 판정하는 `HandEvaluator.CategoryOf`를, 실제 손패 판정은 승리 카드까지 만드는 기존 `Evaluate`를 쓴다. 두 함수의 결과가 같은지 확인하는 테스트를 계속 유지한다.
- **상한과 병렬 기준.** 교체 상한은 **4장**이다(2026-09-21, 4장까지 다시 측정한 뒤 3에서 올림 — THREADING_RESULTS "상한 4장 확장"). 1~2장은 워커에서 순차로, 3~4장은 최대 2스레드 병렬로 돈다. 병렬 기준 3은 `MaxSlots`=4와 따로 정한 값이다. 새 컴포넌트를 설치하거나 씬을 다시 만들 필요는 없다.
- **아직 안 한 것.** Player·모바일 성능은 에디터 계산 시간으로 대신 판단하지 않는다. 오프라인 게임 구조는 그대로다.

### 교체 확률 UI (2026-09-20)

- 일반·선택 교체 확률은 두 줄 요약으로 보여주고, `자세히 >`를 누르면 상세 창이 열린다. "높은 족보"는 `HandRarity`의 희귀도 기준이며, 전투에서 더 강하다는 뜻은 아니다.
- 상세 창(`HandOddsScreen`)은 높은 족보부터 가능한 결과, 확률 막대, 경우의 수를 스크롤로 보여주고, 지금 손패의 족보를 강조한다. 0보다 크지만 0.1% 미만인 확률은 `<0.1%`로 표시한다.
- 선택 교체의 분포는 남은 덱에서 한 장을 뽑았을 때 기준이다. 후보 3장 중에서 원하는 카드를 고를 확률과는 다르다.
- 상세 창을 열면 게임을 멈추고 입력을 막으며, 닫으면 이전 입력 상태로 돌려놓는다. 상세 창 위에 메뉴를 열 수 있다.
- 씬 재설치는 `Tools/Poker Defense/Update Hand Odds UI`로 한다(Game 씬만 갱신). 위젯과 참조는 에디터 코드가 만들고 직렬화한다.

## 게임 결과 제출과 랭킹 (2026-09-27)

[NETWORK.md](NETWORK.md)를 먼저 본다.

- **구조.** 게임이 끝나면 `RunReporter`가 결과를 outbox 파일(`persistentDataPath/RunOutbox`)에 먼저 저장하고, `Task.Run`에서 C++ 서버로 보낸다. ACK를 받아야 파일을 지운다. 결과는 `Update`에서 확인해 메인 스레드에서만 결과 화면에 알린다.
- **서버.** `Server/`의 C++ 프로젝트(select 단일 스레드, Winsock). `Server/RankServer.sln`을 Visual Studio로 열어 빌드·실행한다(F5 = RankServer, 인자 `--port 7777 --data runs.txt`). 명령줄은 `Server/build.bat`(빌드 + 테스트 16개). 솔루션과 프로젝트 파일은 직접 관리하는 파일이며, 소스를 추가하면 `.vcxproj`와 `.filters`에도 넣는다.
- **씬 연결.** `Tools/Poker Defense/Install Run Reporter`가 흐름 오브젝트에 `RunReporter`를 붙이고 `ResultScreen.reporter`를 연결한다. 여러 번 실행해도 된다.
- **주의.** 전송은 게임 결과를 바꾸지 않는다. 웨이브 스킵을 쓴 판은 보내지 않는다. 서버는 값의 범위만 검사하는 데모 랭킹이며 점수를 재현해 검증하지 않는다. 프로토콜을 바꾸면 C#(`RunProtocol.cs`)과 C++(`Protocol.cpp`)의 고정 바이트열 테스트를 함께 고친다.
