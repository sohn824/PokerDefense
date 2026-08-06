# 작업 히스토리

## 이 문서의 관리 규칙

- **최신 항목이 위로.** 각 항목은 `날짜 — 제목` / 무엇을 했는지 / 검증 결과 / 남은 것 순으로 적는다.
- **레거시 정리:** 이후 작업으로 완전히 대체되어 현재 코드·설계와 무관해진 항목은 **삭제한다.** 삭제할 때 그 결정이 왜 뒤집혔는지가 지금도 의미 있으면 최신 항목의 "배경"에 한 줄로 흡수시키고, 아니면 흔적을 남기지 않는다.
- **현재 상태 요약**(바로 아래)은 항상 최신이어야 한다. 이력을 읽지 않아도 이 섹션만으로 프로젝트 상태를 파악할 수 있어야 한다.
- 설계 내용 자체는 여기에 쓰지 않는다. [DESIGN.md](DESIGN.md)가 유일한 설계 출처다.

---

## 현재 상태 요약

- **진행 단계:** M1 완료 (포커 코어 + 테스트) + 템플릿 잔재 정리
- **코드:** `Assets/_Project/Scripts/Poker/` (Card, Deck, HandCategory, HandResult, HandEvaluator), `Assets/_Project/Scripts/Tests/EditMode/`
- **테스트:** EditMode 36개 전부 통과
- **다음 작업:** M2 — 카드 UI + 드로우/교체 플로우
- **대기 중인 결정:** DESIGN.md §7 열린 이슈 1번(저등급 편중 완화 수단) — M4 이전 확정 필요

---

## 이력

### 2026-08-06 — 프로젝트 정리

- URP 템플릿 튜토리얼 잔재 삭제: `Assets/Readme.asset`, `Assets/TutorialInfo/` (Readme.cs, ReadmeEditor.cs, URP.png, Layout.wlt). 사전에 GUID 참조를 전수 조사해 서로만 참조하고 외부 참조가 0인 고립된 세트임을 확인한 뒤 삭제했다.
- `manifest.json`에서 미사용 패키지 5종 제거: `ai.navigation`, `collab-proxy`, `multiplayer.center`, `timeline`, `visualscripting`. `packages-lock.json`에서 각각 1회(최상위 항목)만 등장해 다른 패키지의 의존이 없음을 확인했다.
- **함정:** 패키지 제거 후에도 `Library/ScriptAssemblies`에 해당 DLL 38개가 남아 `Unity.PlasticSCM.Editor.dll` 로드 실패 예외가 대량 발생했다. 고아 DLL을 수동 삭제 후 리프레시하여 해소. **에디터를 켠 채로 패키지를 제거하면 재발할 수 있다.**
- **유지하기로 한 것:** `Assets/Settings/` 렌더 파이프라인 에셋 일체(지우면 렌더링이 깨짐), `SampleScene`·`SampleSceneProfile`·`InputSystem_Actions`(각각 `EditorBuildSettings`·RP 에셋에서 참조 중이며, M2에서 Boot/Game 씬을 만들 때 함께 교체하는 편이 자연스러움), `Library`(1.8GB지만 `.gitignore` 대상이라 저장소에 영향 없음).
- **검증:** 콘솔 에러·경고 0건, EditMode 36/36 통과.

### 2026-08-06 — M1: 포커 코어 구현

- `PokerDefense.Poker` 어셈블리 생성. `noEngineReferences: true`로 두어 UnityEngine 참조를 컴파일 단계에서 차단했다. 설계상의 "순수 C#" 약속이 문서가 아니라 빌드로 강제된다.
- `Card`(readonly struct, `IEquatable`), `Deck`(시드 주입 Fisher-Yates, 라운드마다 새로 생성), `HandCategory`(13종), `HandResult`, `HandEvaluator` 구현.
- `PokerDefense.Tests.EditMode` 어셈블리와 테스트 36개 작성.
  - 13개 카테고리 대표 핸드 전수
  - 경계: 마운틴≠스트레이트, 백스트레이트≠스트레이트, K-A-2-3-4 랩어라운드 불허, 특수 스트레이트 플러시가 Flush/Straight로 새지 않음, 최저·최고 일반 스트레이트
  - `KeyCards` 정렬 규약(투페어는 높은 페어 먼저, 풀하우스는 트리플 먼저, 백스트레이트는 5-4-3-2-A)
  - `HandCategory` 값 개수를 13으로 고정하는 가드 테스트 — enum이 늘면 대표 핸드 테스트도 함께 늘리도록 강제
  - Deck: 중복 없는 52장, 동일 시드 재현성, 고갈 시 예외
- 테스트 표기용 헬퍼 `Hand.Of("As Ks Qs Js Ts")`는 테스트 어셈블리에만 둔다. 프로덕션 코드에 파서를 넣지 않았다.
- **검증:** Unity Test Runner EditMode 36/36 통과 (1.09s). 컴파일 에러·경고 없음.
- **남은 것:** M0 중 **URP 2D 렌더러 설정과 씬 생성은 아직 하지 않았다** (M1이 순수 C#이라 필요하지 않았음). M2 착수 전에 처리해야 한다. 족보→유닛 매핑 테이블(`HandUnitTable`)은 M3에서 만든다.

### 2026-08-06 — 기본 구조 설계

- 게임 범위를 확정: 싱글 PvE / 2D 스프라이트(URP 2D) / 족보 1개 = 유닛 1기 / 누적 배치 + 머지.
- `Docs/DESIGN.md` 작성 — 라운드 페이즈 상태 머신, 3개 어셈블리 분리, 포커 코어 API, SO 데이터 구조, 전투/머지 규칙, 마일스톤 M0~M6.
- `Docs/ART_REQUEST.md` 작성 — 유닛 13종, 적 4종, 카드·맵·UI·VFX 목록. 구현은 플레이스홀더로 진행하고 M6에서 일괄 교체하기로 함.
- 주요 설계 판단 2건:
  - 머지를 "티어 상승"이 아닌 "성급(★) 상승"으로 — 족보 축과 머지 축을 직교시켜 족보 가치 희석을 막음.
  - `HandCategory` enum 순서를 세기 순서로 쓰지 않음 — 판정과 보상을 분리해 밸런싱을 데이터로 뺌.
- **검증:** 문서만 작성. 코드 없음.
- **남은 것:** 열린 이슈 1번 결정, M0 프로젝트 뼈대 생성.
