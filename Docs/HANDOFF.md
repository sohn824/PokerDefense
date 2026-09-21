# 프로젝트 인수인계 — 2026-09-13


## 먼저 읽을 것

[../CLAUDE.md](../CLAUDE.md)의 작업 관례 → 이 문서 → [DESIGN.md](DESIGN.md) §1·9·14~18 → [POLISH.md](POLISH.md)의 다음 작업 순서. 과거 결정과 함정은 [HISTORY.md](HISTORY.md)를 필요한 만큼 읽는다. 설계의 기준은 DESIGN이며, 이 문서는 진입점·코드 지도·검증 상태를 제공한다. LEGACY_LOOP_PLAN.md는 폐기된 기획 기록이다.

## 현재 방향

**재화 운영보다 직접 손패를 맞추는 재미가 중심이다.** 5장 드로우 → 일반 교체 → 선택 교체(선택 사항) → 손패 확정/유닛 1기 소환 → 배치·합치기·교체 또는 소환 포기 → 전투 → 결과 비트 → 다음 손패. 50웨이브를 진행하며 시간초과해도 라이프가 남으면 다음으로 간다.

- 일반 교체는 자리당 한 번이며 나눠 실행할 수 있다.
- 선택 교체는 **매 손패 1회, 무료, 이월 없음**. 일반 교체한 자리 하나에서 남은 덱의 서로 다른 후보 3장 중 한 장을 고른다. 공개 전 취소 가능, 공개 시 사용 완료, 공개 후 재추첨/확정/일반 교체 불가. 다음 손패에서 초기화한다.
- Chip·유지 보너스·판매 수입·상점·보유 카드·랜덤 소환은 **삭제**했다. 5라운드마다 상점을 열거나 보조 횟수를 충전하지 않는다.
- 15칸 보드의 동일 유닛·동일 성급 합치기(최대 ★3), 이동/자리 교환, 보스 보상 승급권은 유지한다. 기존 유닛 교체·퇴장·소환 포기는 확인 창을 거치며 환급은 없다.
- 손패 아래 패널은 카드를 1~4장 선택했을 때만 뜨며 그 교체의 확률 분포를 보여준다(DESIGN §19, `HandOdds`). 선택이 없거나 5장 이상이면 패널을 숨긴다 — 선택 전 족보 추천은 하지 않는다. 어느 자리가 최선인지도 추천하지 않는다.
- 타이틀·음량 옵션·일시정지·도움말·재도전·타이틀 복귀는 구현됐다. **런 저장/이어하기는 없다.** 옵션 저장과 구분한다.
- 어두운 투기장 비주얼과 현재 채택한 Arena Breaks BGM을 유지한다. 과거 오디오 후보를 다시 적용하지 않는다. 상세는 AUDIO_REQUEST.md·ART_REQUEST.md를 참조한다.

## 코드 지도

경로는 `Assets/_Project/` 기준이다. 규칙 변경 시 관련 Context → Controller → UI → Editor 씬 설치 → 테스트·문서 순으로 영향을 확인한다.

| 책임 | 읽을 파일 |
|---|---|
| 카드/덱/족보와 목표·확률 탐색 | `Scripts/Poker/Deck.cs`, `HandGoals.cs`, `HandOdds.cs`, `HandEvaluator.cs` |
| 손패 단계·자리 잠금·후보·손패당 사용 여부 | `Scripts/Game/Flow/RoundContext.cs`, `RoundController.cs` |
| 라운드/전투 전이와 다음 손패 | `Scripts/Game/Flow/GameFlowController.cs` |
| 라이프·웨이브·승급권 | `Scripts/Game/Flow/StageContext.cs`, `StageController.cs`, `Scripts/Game/Data/StageDefinition.cs`, `Data/Stage_1.asset` |
| 보드와 소환 대기·합치기·교체/퇴장/포기 | `Scripts/Game/Board/GridBoard.cs`, `PlacementController.cs` |
| 손패 행동·후보·확률 문구 | `Scripts/UI/RoundScreen.cs`, `AssistScreen.cs`, `HandOddsText.cs`, `ActionBarController.cs` |
| 파괴적 보드 조작 확인 | `Scripts/UI/BoardDecisionScreen.cs`, `BoardScreen.cs` |
| 첫 손패/배치/합치기 안내와 메뉴 | `Scripts/UI/OnboardingGuide.cs`, `MenuScreen.cs` |
| 결과·위협·기록 | `Scripts/UI/RoundBreakScreen.cs`, `RoundRecap.cs`, `ThreatPreview.cs`, `Scripts/Game/Flow/RunJournal.cs` |
| 씬 설치와 개발 비교 | `Editor/GameLoopSetup.cs`, `BalanceSimRunner.cs`, `WaveSkipWindow.cs` |
| 회귀 테스트 | `Scripts/Tests/Editor/HandLoopTests.cs`, `AssistTests.cs`, `RoundContextTests.cs`, `StageTests.cs`, `HandOddsTests.cs` |

`TryReplace`는 예상한 기존 유닛 인스턴스가 그대로 있을 때만 교체하고, 합칠 수 있는 조합은 교체로 우회하지 않는다. 확인 창은 취소 시 상태를 바꾸지 않으며 메뉴 중첩 이후에도 정지 상태를 복원한다.

## 검증 기록과 한계

아래는 직전 구현 작업에서 기록한 결과다. 이번 문서 갱신에서 테스트나 빌드를 재실행하지 않았다.

- EditMode **207/207 통과**(2026-09-15). 손패별 사용/초기화, 후보 중복·재공개 방지, 목표의 실제 덱/자리 조건, 가득 찬 보드 교체와 기존 인스턴스/합치기 가드, `HandOdds`의 경우의 수·확률·조합 상한 6개 포함. 삭제 기능 테스트를 제거했으므로 과거 241개와 개수로 회귀를 판단하지 않는다.
- Editor Play: 후보 선택 후 다음 손패 초기화, W5→W6 상점 없음, 15칸 보드 교체 확인/취소/메뉴 중첩, 승급권 성장과 전투 시작 확인. 1080×1920 및 360×800 목표 문구 확인, Missing Script 0.
- Windows 빌드 `Builds/LoopReview/PokerDefense.exe` 성공. 빌드 보고서 오류 0/경고 3. 실행 파일에서 정상 50웨이브를 완주한 검증은 아니다.
- TMP `Maplestory Light SDF.asset`의 `Importer(NativeFormatImporter) generated inconsistent result` 문제가 남아 있다. 빌드 성공과 콘솔 전체 오류 없음은 다른 주장이다.
- **현행 밸런스 미검증:** 공급 축소 후 웨이브 수치를 의도적으로 유지했다. 사람 정상 런·기기 QA와 새 기준 측정이 필요하다. 개발 웨이브 스킵은 난도 검증이 아니다.
- BalanceSimRunner는 단순 후보 선택 정책이며 가득 찬 보드의 전략적 교체까지 모델링하지 않는다. 실행 결과를 사람 승률로 간주하지 않는다.

## 이어받을 때 주의할 것

1. 먼저 `git status --short`와 diff를 확인한다. 코드·씬·에셋의 미커밋 변경이 있으며 다른 작업을 되돌리지 않는다. 커밋/푸시는 사용자 요청 시에만 한다.
2. Unity 6000.4.0f1로 열고 Boot부터 흐름을 확인한다. 빌드 씬 순서는 Boot → Title → Game. 테스트는 Unity Test Runner의 EditMode 전체를 실행한다. asmdef는 추가하지 않는다.
3. `Poker → Game → UI` 의존 방향을 유지한다. Poker는 순수 C#과 System.Random을 사용한다. 규칙은 Context, Unity 이벤트는 Controller, 화면 참조는 직렬화된 UI 필드로 관리한다. 기존 Allman 중괄호·한글 주석 관례를 따른다.
4. 씬 설치가 필요할 때 `GameLoopSetup.UpdateGuideAndAssist()`는 관련 생성 UI를 재구성한다. 호출 전 diff를 확인한다. 전체 `Apply()`는 타이틀까지 다시 만들므로 사용자 씬 편집을 덮을 수 있다. 문서 작업 때문에 실행하지 않는다.
5. `Joker`는 승급권 내부 명칭이다. `sellButton/sellLabel` 등 일부 직렬화 이름은 남아 있지만 현재 퇴장/포기 기능이다. SFX enum의 옛 슬롯은 에셋 번호 호환용이므로 재정렬하지 않는다. RoundContext의 `PlaceHeldCard`/상점 관련 일부 주석도 오래된 설명이며 구현 API가 아니다.
6. `.meta`와 원본 에셋을 함께 관리한다. 빌드/테스트가 만든 폰트·렌더 설정·Input Actions preload 변경은 의도한 변경인지 확인하고, 기존 수정까지 일괄 되돌리지 않는다.
7. `Artifacts/HandLoop/`의 Python 파일은 일회성 마이그레이션 도구이며 재실행하지 않는다. 게임 실행 의존성이 아니다. `Tools/Audio/`는 별도 오디오 제작 도구다. Artifacts·Builds는 배포 소스가 아니다.
8. RunJournal은 `Application.persistentDataPath/RunJournals`의 로컬 분석 로그다. `ruleset=hand-loop-v1`만 현행 비교에 사용한다. 전체 런 재현이나 저장/이어하기를 보장하지 않는다.

## 다음 작업의 출발점

POLISH의 P0부터 진행한다. 우선 정상 플레이에서 선택 교체 이해도·목표 활용·초반 합치기 속도·실패 웨이브·준비 시간을 관찰한다. 문제를 확인한 뒤 안내/배치 UX 또는 웨이브 수치를 조정한다. Chip 복원, 추가 보상/보관 슬롯, 새로운 성장 시스템을 선행하지 않는다.

## 멀티스레드 구현과 다음 기술 확장 (2026-09-20)

[멀티스레드 구조](THREADING_PLAN.md), [측정·검증 기록](THREADING_RESULTS.md)을 먼저 확인한다. HandOdds 비동기·제한 병렬화를 구현했다. RoundController가 단일 HandOddsWorker를 소유하며 불변 HandOddsRequest만 계산에 넘긴다. 선택 변경/손패 변경/후보 공개/비활성화에서 취소하고 최신 요청만 게시한다. Unity API와 UI 이벤트는 메인 스레드에서만 호출한다.

확률 열거는 HandEvaluator.CategoryOf(종류만 판정)를, 실제 손패 판정은 기존 Evaluate(승리 카드 포함)를 사용한다. 두 판정기의 동등성 테스트를 함께 유지한다. 기본 표시 상한은 **4장**(2026-09-21, k=4 재측정 후 3에서 올림 — THREADING_RESULTS §"상한 4장 확장"). 1~2장 워커 순차, 3~4장 최대 병렬도 2를 적용한다. 병렬 임계값 3은 MaxSlots=4와 독립적이다. 새 컴포넌트 설치나 씬 재생성은 필요 없다.

최신 EditMode 검증은 **225/225**다(2026-09-21 상한 변경은 기존 테스트의 파라미터일 뿐 개수를 바꾸지 않았다). 기존 207개, 멀티스레드/판정 테스트 12개, 표시 검증 테스트 6개를 포함한다. Player·모바일 성능은 Editor 계산 시간으로 대신 판단하지 않는다. TCP 결과 제출/랭킹은 다음 단계이며 아직 연결하지 않았다. Stand-Alone 게임은 그대로다. 포트폴리오 문서(덱·경력기술서·QnA)의 "3자리 상한" 서술은 아직 갱신하지 않았다.


### 교체 확률 UI (2026-09-20)

- 일반/선택 교체 확률은 고정 두 줄 요약과 `자세히 >` 진입을 사용한다. 높은 족보는 `HandRarity` 희귀도 기준이고 전투 성능 보장은 아니다.
- `HandOddsScreen`은 상위 족보 순으로 가능한 결과·확률 막대·경우의 수를 스크롤로 보여주며 현재 족보를 강조한다. 양수 0.1% 미만은 `<0.1%`로 표시한다.
- 선택 교체의 분포는 남은 덱에서 한 장을 뽑는 기준이며, 후보 3장 중 선택하는 성공률과 구분한다.
- 창을 열 때 게임을 정지하고 입력을 차단하며 닫으면 이전 입력 상태를 복원한다. 메뉴는 상세 창 위에 열 수 있다.
- 씬 재설치: `Tools/Poker Defense/Update Hand Odds UI` (Game 씬만 갱신). 위젯과 참조는 Editor에서 생성/직렬화한다.
