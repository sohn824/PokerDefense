# 작업 히스토리

## 이 문서의 관리 규칙

- **최신 항목이 위로.** 각 항목은 `날짜 — 제목` / 무엇을 했는지 / 검증 결과 / 남은 것 순으로 적는다.
- **레거시 정리:** 이후 작업으로 완전히 대체되어 현재 코드·설계와 무관해진 항목은 **삭제한다.** 삭제할 때 그 결정이 왜 뒤집혔는지가 지금도 의미 있으면 최신 항목의 "배경"에 한 줄로 흡수시키고, 아니면 흔적을 남기지 않는다.
- **현재 상태 요약**(바로 아래)은 항상 최신이어야 한다. 이력을 읽지 않아도 이 섹션만으로 프로젝트 상태를 파악할 수 있어야 한다.
- 설계 내용 자체는 여기에 쓰지 않는다. [DESIGN.md](DESIGN.md)가 유일한 설계 출처다.

---

## 현재 상태 요약

- **진행 단계:** M0·M1 완료. M2 착수 가능
- **코드:** `Assets/_Project/Scripts/Poker/` (Card, Deck, HandCategory, HandResult, HandEvaluator), `Assets/_Project/Scripts/Tests/Editor/`
- **씬:** `_Project/Scenes/Boot.unity`(빌드 0), `Game.unity`(빌드 1). 둘 다 직교 카메라 하나뿐인 빈 씬
- **렌더링:** URP 2D (`Assets/Settings/PokerDefense_2DRenderer.asset`, PC·Mobile RP 에셋 양쪽에 연결)
- **어셈블리:** asmdef 없음. 게임 코드는 `Assembly-CSharp`, 테스트는 `Assembly-CSharp-Editor`
- **테스트:** EditMode 36/36 통과 (2026-08-07 확인)
- **지켜야 할 관례:** 레이어 역방향 `using` 금지, `Poker`에서 `UnityEngine.Random` 금지(재현성), ScriptableObject·MonoBehaviour 금지 — DESIGN.md §2 참조
- **다음 작업:** M2 — 카드 UI + 드로우/교체 플로우
- **대기 중인 결정:** DESIGN.md §7 열린 이슈 1번(저등급 편중 완화 수단) — M4 이전 확정 필요

---

## 이력

### 2026-08-07 — M0 마무리 (렌더러 / 씬 / 폴더)

M1을 먼저 하느라 미뤄둔 M0 잔여분을 처리했다. M2(카드 UI)는 이게 없으면 시작할 수 없다.

- **URP 2D 렌더러로 전환.** `PC_Renderer`·`Mobile_Renderer`가 둘 다 `UniversalRendererData`(3D)였다. URP 3D 템플릿 그대로였던 것. `PokerDefense_2DRenderer.asset`(`Renderer2DData`)을 만들어 `PC_RPAsset`·`Mobile_RPAsset` 양쪽의 `m_RendererDataList`를 교체했다.
  - URP 17에서는 `ResourceReloader`가 사라졌다(셰이더 리소스가 `GraphicsSettings`로 이동). 그래서 렌더러 에셋에 리소스를 채우는 단계가 필요 없었다.
- **씬 2개 생성.** `_Project/Scenes/Boot.unity`, `Game.unity`. 각각 직교 카메라(size 5, Solid Color) 하나가 전부다. `UniversalAdditionalCameraData`를 명시적으로 붙여 런타임 자동 추가를 피했다.
- **빌드 세팅:** Boot(0) → Game(1).
  - 함정: `manage_build`로 씬 목록을 바꿔도 `EditorBuildSettings.asset`은 디스크에 즉시 안 써진다. `File/Save Project`를 실행해야 반영된다.
- **고아 에셋 삭제.** `Assets/Settings/` 전체와 후보들의 GUID 참조를 전수 조사한 뒤 참조 0건인 것만 지웠다.
  - 삭제: `PC_Renderer.asset`, `Mobile_Renderer.asset`(2D 렌더러로 교체되며 고아가 됨), `Assets/Scenes/SampleScene.unity` 및 빈 `Assets/Scenes/` 폴더
  - **`SampleSceneProfile.asset`은 남겼다.** `PC_RPAsset`·`Mobile_RPAsset`의 `m_VolumeProfile`이 이걸 가리킨다. URP 템플릿이 만든 배선이라 이름이 프로젝트와 안 맞지만 실제로 쓰이고 있다. `DefaultVolumeProfile`로 갈아끼우는 건 포스트프로세싱 값이 바뀌는 일이라 지금 범위 밖.
  - `InputSystem_Actions.inputactions`도 유지 — `ProjectSettings/EditorBuildSettings.asset`이 참조 중.
- **폴더 뼈대 생성** (DESIGN §2 구조대로 18개). 각 리프에 `.gitkeep`을 뒀다. 빈 폴더는 git이 추적하지 않는데 Unity가 만든 폴더 `.meta`는 추적되므로, 클론하면 고아 `.meta`가 생긴다. Unity는 `.`으로 시작하는 파일을 무시하므로 `.gitkeep.meta`는 생기지 않는다(확인함).
- **검증:** 삭제 후 Boot·Game 각각 플레이 진입/종료 — **콘솔 에러·경고 0건**. 삭제한 3개 GUID의 잔여 참조 0건. EditMode 36/36 통과(1.01s). M0 검증 조건("빈 씬이 에러 없이 실행됨") 충족.

### 2026-08-07 — asmdef 전면 폐지

- **배경:** 어셈블리 분리 없이 에디터만으로 관리하고 싶다는 요청. 바로 전날 기록에 "불가능"이라고 적어둔 것이 있어 Test Framework 1.6.0 패키지 소스를 직접 확인했다.
- **그 "불가능" 판단은 틀렸다.** 근거로 든 두 명제 중 하나만 맞았다.
  - 맞음: asmdef는 predefined assembly를 참조할 수 없다.
  - **틀림:** "Test Runner는 nunit을 참조하는 asmdef가 있어야 테스트를 발견한다." 실제로는 `EditorLoadedTestAssemblyProvider.cs`가 asmdef를 전혀 보지 않고, **로드된 어셈블리 중 `nunit.framework`를 참조하는 것**을 스캔한 뒤 `AssemblyFlags.EditorOnly`면 EditMode로 분류한다.
  - 그리고 필요한 참조 방향은 `Assembly-CSharp-Editor` → `Assembly-CSharp`인데, 이건 predefined assembly끼리라 자동으로 성립한다. 막히는 방향(asmdef → predefined)과 반대여서 애초에 문제가 아니었다.
- **변경:**
  - `PokerDefense.Poker.asmdef`, `PokerDefense.Tests.EditMode.asmdef` 삭제 (각 `.meta` 포함)
  - `Scripts/Tests/EditMode/` → `Scripts/Tests/Editor/` 폴더명 변경. `FolderPathTestCompilationContextProvider.cs`가 경로에 `Editor` 폴더가 있는지로 판정하므로 이름이 정확히 `Editor`여야 한다.
  - **C# 코드는 한 줄도 바뀌지 않았다.** 네임스페이스는 어셈블리와 무관하므로 `PokerDefense.Poker` 그대로. 기존 테스트는 순수 NUnit만 써서 참조 손실도 없었다.
- **포기한 것:** `[UnityTest]`·`LogAssert` 등 `UnityEngine.TestTools` (해당 asmdef가 `autoReferenced: false`), PlayMode 테스트, 그리고 레이어 의존 방향의 컴파일러 강제. 필요해지면 테스트용 asmdef 하나만 되살리면 된다.
- **검증:** 리프레시 후 `Library/ScriptAssemblies`에서 `PokerDefense.*.dll`이 사라지고 `Assembly-CSharp.dll`·`Assembly-CSharp-Editor.dll`이 생성됨. 콘솔 컴파일 에러 0건. EditMode **36/36 통과** (1.10s).

### 2026-08-06 — Poker 어셈블리의 엔진 참조 허용

- **배경:** M1에서는 `PokerDefense.Poker`를 `noEngineReferences: true`로 두어 UnityEngine 참조를 컴파일 단계에서 차단했다. 셔플 재현성(`System.Random` 주입 강제)과 밸런싱 시뮬레이터의 `dotnet` 콘솔 이식성을 노린 것이었다. 구조가 이해하기 어렵다는 판단에 따라 이 제약을 해제한다.
- `noEngineReferences: false`로 변경. 이제 `Poker`에서 `Debug.Log` 등 Unity API를 쓸 수 있다.
- ~~**어셈블리 자체를 없애는 선택지는 검토 결과 불가능했다.**~~ **이 판단은 틀렸다.** 2026-08-07 항목 참조 — 다음 날 asmdef를 전부 제거했다.
- **컴파일러 대신 관례로 지켜야 하는 것 2가지** (DESIGN.md §2에 기록):
  - 셔플에 `UnityEngine.Random`을 쓰지 않는다 → 쓰면 `같은_시드는_같은_순서를_만든다` 테스트가 깨진다
  - `Poker`에 ScriptableObject·MonoBehaviour를 두지 않는다 → 밸런스 데이터는 `Game`의 `HandUnitTable` 담당
- 코드는 한 줄도 바뀌지 않았다. 권한만 열었다.
- **검증:** 하지 못했다. Unity MCP 연결이 끊겨 리프레시·테스트 실행이 불가능했다. → 2026-08-07 asmdef 폐지 작업에서 36/36 통과로 확인됨.

### 2026-08-06 — 프로젝트 정리

- URP 템플릿 튜토리얼 잔재 삭제: `Assets/Readme.asset`, `Assets/TutorialInfo/` (Readme.cs, ReadmeEditor.cs, URP.png, Layout.wlt). 사전에 GUID 참조를 전수 조사해 서로만 참조하고 외부 참조가 0인 고립된 세트임을 확인한 뒤 삭제했다.
- `manifest.json`에서 미사용 패키지 5종 제거: `ai.navigation`, `collab-proxy`, `multiplayer.center`, `timeline`, `visualscripting`. `packages-lock.json`에서 각각 1회(최상위 항목)만 등장해 다른 패키지의 의존이 없음을 확인했다.
- **함정:** 패키지 제거 후에도 `Library/ScriptAssemblies`에 해당 DLL 38개가 남아 `Unity.PlasticSCM.Editor.dll` 로드 실패 예외가 대량 발생했다. 고아 DLL을 수동 삭제 후 리프레시하여 해소. **에디터를 켠 채로 패키지를 제거하면 재발할 수 있다.**
- **유지하기로 한 것:** `Assets/Settings/` 렌더 파이프라인 에셋 일체(지우면 렌더링이 깨짐), `SampleScene`·`SampleSceneProfile`·`InputSystem_Actions`(각각 `EditorBuildSettings`·RP 에셋에서 참조 중이며, M2에서 Boot/Game 씬을 만들 때 함께 교체하는 편이 자연스러움), `Library`(1.8GB지만 `.gitignore` 대상이라 저장소에 영향 없음).
- **검증:** 콘솔 에러·경고 0건, EditMode 36/36 통과.

### 2026-08-06 — M1: 포커 코어 구현

- `PokerDefense.Poker` 어셈블리 생성.
- `Card`(readonly struct, `IEquatable`), `Deck`(시드 주입 Fisher-Yates, 라운드마다 새로 생성), `HandCategory`(13종), `HandResult`, `HandEvaluator` 구현.
- `PokerDefense.Tests.EditMode` 어셈블리와 테스트 36개 작성.
  - 13개 카테고리 대표 핸드 전수
  - 경계: 마운틴≠스트레이트, 백스트레이트≠스트레이트, K-A-2-3-4 랩어라운드 불허, 특수 스트레이트 플러시가 Flush/Straight로 새지 않음, 최저·최고 일반 스트레이트
  - `KeyCards` 정렬 규약(투페어는 높은 페어 먼저, 풀하우스는 트리플 먼저, 백스트레이트는 5-4-3-2-A)
  - `HandCategory` 값 개수를 13으로 고정하는 가드 테스트 — enum이 늘면 대표 핸드 테스트도 함께 늘리도록 강제
  - Deck: 중복 없는 52장, 동일 시드 재현성, 고갈 시 예외
- 테스트 표기용 헬퍼 `Hand.Of("As Ks Qs Js Ts")`는 테스트 어셈블리에만 둔다. 프로덕션 코드에 파서를 넣지 않았다.
- **검증:** Unity Test Runner EditMode 36/36 통과 (1.09s). 컴파일 에러·경고 없음.
- **남은 것:** M0 중 **URP 2D 렌더러 설정과 씬 생성은 아직 하지 않았다** (M1이 순수 C#이라 필요하지 않았음). M2 착수 전에 처리해야 한다. → 2026-08-07에 처리 완료. 족보→유닛 매핑 테이블(`HandUnitTable`)은 M3에서 만든다.

### 2026-08-06 — 기본 구조 설계

- 게임 범위를 확정: 싱글 PvE / 2D 스프라이트(URP 2D) / 족보 1개 = 유닛 1기 / 누적 배치 + 머지.
- `Docs/DESIGN.md` 작성 — 라운드 페이즈 상태 머신, 3개 어셈블리 분리, 포커 코어 API, SO 데이터 구조, 전투/머지 규칙, 마일스톤 M0~M6.
- `Docs/ART_REQUEST.md` 작성 — 유닛 13종, 적 4종, 카드·맵·UI·VFX 목록. 구현은 플레이스홀더로 진행하고 M6에서 일괄 교체하기로 함.
- 주요 설계 판단 2건:
  - 머지를 "티어 상승"이 아닌 "성급(★) 상승"으로 — 족보 축과 머지 축을 직교시켜 족보 가치 희석을 막음.
  - `HandCategory` enum 순서를 세기 순서로 쓰지 않음 — 판정과 보상을 분리해 밸런싱을 데이터로 뺌.
- **검증:** 문서만 작성. 코드 없음.
- **남은 것:** 열린 이슈 1번 결정, M0 프로젝트 뼈대 생성.
