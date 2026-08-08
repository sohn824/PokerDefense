# 작업 히스토리

## 이 문서의 관리 규칙

- **최신 항목이 위로.** 각 항목은 `날짜 — 제목` / 무엇을 했는지 / 검증 결과 / 남은 것 순으로 적는다.
- **레거시 정리:** 이후 작업으로 완전히 대체되어 현재 코드·설계와 무관해진 항목은 **삭제한다.** 삭제할 때 그 결정이 왜 뒤집혔는지가 지금도 의미 있으면 최신 항목의 "배경"에 한 줄로 흡수시키고, 아니면 흔적을 남기지 않는다.
- **현재 상태 요약**(바로 아래)은 항상 최신이어야 한다. 이력을 읽지 않아도 이 섹션만으로 프로젝트 상태를 파악할 수 있어야 한다.
- 설계 내용 자체는 여기에 쓰지 않는다. [DESIGN.md](DESIGN.md)가 유일한 설계 출처다.

---

## 현재 상태 요약

- **진행 단계:** M0~M4 완료. M5 착수 가능
- **코드:** `Scripts/Poker/`(포커 코어), `Scripts/Game/Flow/`(라운드 + 전투 + 스테이지), `Scripts/Game/Board/`, `Scripts/Game/Units/`, `Scripts/Game/Enemies/`, `Scripts/Game/Data/`, `Scripts/UI/`, `Scripts/Tests/Editor/`
- **데이터:** 유닛 13종 + `HandUnitTable`, 적 3종 + 웨이브 3개 + `Stage_1`. **수치는 전부 플레이스홀더이며 M6에서 밸런싱한다**
- **씬:** `Boot.unity`(빌드 0, 카메라만), `Game.unity`(빌드 1). **보드·적은 월드 스페이스, 카드 UI와 HUD만 uGUI**
- **렌더링:** URP 2D. **Game 뷰는 Portrait 1080x1920**, 카메라 직교 크기 6.6, 보드 중심 월드 y=3.4
- **어셈블리:** asmdef 없음. 게임 코드는 `Assembly-CSharp`, 테스트는 `Assembly-CSharp-Editor`
- **테스트:** EditMode 115/115 통과 (2026-08-08 확인)
- **지켜야 할 관례:** 레이어 역방향 `using` 금지, `Poker`에서 `UnityEngine.Random` 금지(재현성), ScriptableObject·MonoBehaviour 금지 — DESIGN.md §2 참조
- **UI 표기는 한글이다.** 폰트는 Maplestory(Light 기본 / Bold 굵기 연결), TMP **Dynamic 아틀라스**
- **폰트에 없는 글자 주의:** `♦`(U+2666), `—`(U+2014), `–`(U+2013)이 없다. 각각 `◆`, `-`로 대체했다. 새 기호를 쓰기 전에 `HasCharacter(c, false, true)`로 확인할 것
- **다음 작업:** M5 — 라운드 루프 결합 + HUD. 지금은 `New Round`·`Start Combat` 버튼으로 수동 진입한다
- **대기 중인 결정:** DESIGN §7 열린 이슈 1번(저등급 편중 완화)은 **보류 중이며 M5 전에 정해야 한다**. 2번(그리드 가득 참)은 M3에서 "유닛 포기"로 임시 처리해 둔 상태
- **대기 중인 결정:** DESIGN.md §7 열린 이슈 1번(저등급 편중 완화 수단) — M4 이전 확정 필요

---

## 이력

### 2026-08-08 — 한글 폰트 적용, UI 한글화

- Maplestory 폰트(Light / Bold) TTF를 `_Project/Art/Fonts/`에 넣고 TMP 폰트 에셋을 만들었다.
- **아틀라스는 Dynamic이다.** 한글 음절이 11,172자라 정적 아틀라스로는 감당이 안 된다. 쓰이는 글자만 그때그때 굽는다.
  - `CreateFontAsset` 후 아틀라스 텍스처와 머티리얼을 **서브에셋으로 넣어야** 리로드 때 사라지지 않는다.
  - Light의 굵기 표(`m_FontWeightTable`) 700 항목에 Bold를 연결해 `FontStyles.Bold`가 가짜 굵기가 아니라 진짜 Bold로 나오게 했다.
- TMP 기본 폰트를 교체하고, 씬에 이미 있던 텍스트 66개(uGUI 21 + 월드 45)를 전부 갈아끼웠다. 기본값만 바꾸면 기존 컴포넌트는 LiberationSans를 계속 물고 있다.
- **폰트 커버리지를 먼저 확인하고 표기를 정했다.** Dynamic 폰트는 문자 테이블이 비어 있어 `HasCharacter(c)`만으로는 판정이 안 되고 `HasCharacter(c, false, true)`로 원본 TTF를 조회해야 한다.
  - 있음: 한글 전체, 영숫자, `♠♥♣★☆◆◇`
  - **없음: `♦`(U+2666), `♢`, `—`(U+2014), `–`(U+2013)**
  - 다이아는 `◆`(U+25C6)로, em dash는 `-`로 대체했다. 실제로 두 번 두부가 났고 스크린샷으로 잡았다.
- 폰트 때문에 영문으로 뒀던 세 곳을 되돌렸다: 족보명(`HandCategoryNames`), 카드 무늬(S/H/D/C → `♠♥◆♣`), 성급(`*` → `★`).
- 나머지 UI 문구도 한글로 통일했다. 절반만 한글이면 더 어색하다.
- **`CardText`를 새로 뺐다.** `Card.ToString()`("4h")은 로그·테스트용이라 화면에 그대로 쓰면 카드 앞면(`4♥`)과 표기가 어긋난다. 키카드 표시가 실제로 그랬다.
- **유닛·적 이름은 영문 그대로다**(Scout, Guard, Grunt...). SO 에셋의 데이터라 코드가 아니라 이름 정하기의 문제다.
- **검증:** EditMode **115/115 통과**. Game 씬 실플레이로 족보명·키카드·성급·카드 무늬·전투 HUD 전부 확인, **두부 0개, 콘솔 에러·경고 0건**.

### 2026-08-08 — M4: 웨이브 + 전투 + 클리어 판정

설계(아래 항목)대로 a~f 6단계를 진행했다.

- **a. 보드를 월드 스페이스로 옮겼다.** `GridBoard`에 `CellSize`(1)와 `SlotToLocalPosition`을 넣고, `UnitSlotView`·`BoardScreen`을 `SpriteRenderer` + `BoxCollider2D` 기반으로 재작성했다.
  - **검증 조건이었던 "기존 87개 테스트 무수정 통과"를 충족했다.** 표현만 바꿨고 규칙 코드는 한 줄도 안 건드렸다는 증거다.
  - 클릭은 `Pointer.current` → `ScreenToWorldPoint` → `Physics2D.OverlapPoint`로 잡는다. 카드 UI 위 클릭은 `EventSystem.IsPointerOverGameObject()`로 걸러낸다.
  - 누를 수 없는 칸은 `hitbox.enabled = false`로 Physics2D 검사에서 아예 뺀다. uGUI 시절 `Button.interactable`이 하던 역할이다.
  - 플레이스홀더 스프라이트 `Art/Sprites/Square.png`(32px, PPU 32 → 1칸 = 1 월드 유닛)를 만들어 슬롯·적이 공유한다.
- **b~e. 전투를 순수 C#으로 만들었다.** `CombatContext.Tick(float deltaTime)`, `EnemyInstance`, `StageContext`, SO 3종.
  - 트랙은 `GridBoard`가 계산하는 닫힌 사각 루프다(반경 3.4 x 2.4, 길이 23.2). 적 위치는 진행도 하나이고 바퀴 수를 포함해 누적된다.
  - 유닛 공격 쿨다운은 `CombatContext`가 슬롯별로 들고 있다. **`GridBoard`는 전투 상태를 모른다.**
  - 사거리에 적이 없으면 쿨다운을 0에 붙여둔다. 적이 들어오는 즉시 쏘게 하기 위함이다.
  - `UnresolvedEnemies`는 **아직 안 나온 적까지** 포함한다. 제한시간이 스폰보다 먼저 끝나면 안 나온 적도 라이프를 깎아야 하기 때문이다.
- **f. 씬에 붙였다.** `CombatController`가 `Update`에서 `Time.deltaTime`을 넘기고, `CombatScreen`이 적 스프라이트를 목록에 맞춰 만들고 지운다. HP는 게이지 없이 색 밝기로 보여준다.
- **폰트 함정이 또 나왔다.** `UnitInstance.ToString()`의 `★`(U+2605)가 LiberationSans에 없어 두부가 났다. `ToString()`은 로그용으로 두고 화면 표기는 `BoardScreen.Describe()`가 `*`로 만든다.
- **`PlayerSettings.runInBackground`를 켰다.** 에디터가 포커스를 잃으면 플레이어 루프가 거의 멈춰 MCP로 실시간 동작을 관찰할 수 없었다. 모바일 타깃에서는 이 설정이 무시되므로 부작용이 없다.
- **만들지 않은 것:** 타겟팅 방식 enum(1종 고정), 투사체(즉시 히트), 골드·처치 보상, `TargetRegistry` 클래스, `RoundPhase.Combat`/`Result`(전이가 없으면 죽은 값이라 M5에서 넣는다).
- **테스트 28개 추가** (`CombatTests` 21, `GridBoardTests` 좌표 7). 트랙 형상, 스폰 수량·간격, 사거리 안팎, 성급에 따른 처치 속도 차이, 클리어·시간초과, 라이프 감소·하한 0, 웨이브 진행.
- **검증:** EditMode **115/115 통과**. Game 씬 실플레이 —
  - 유닛 6기 배치 → 웨이브 1(5마리, 25초 제한) **12.81초에 전멸, 클리어. 라이프 20 유지**, 웨이브 2로 넘어감
  - 보드를 비우고 웨이브 2(12마리, 30초) → **시간 초과, 라이프 20 → 8** (잔여 12만큼), 적 스프라이트 정리됨, 웨이브 3으로 넘어감
  - **콘솔 에러·경고 0건**
- **남은 것:** 전투 연출 없음(피격·사망 이펙트, 투사체). 라운드↔전투 자동 연결은 M5.

### 2026-08-08 — M4 설계

코드는 아직 없다. 결정만 기록한다.

- **보드를 월드 스페이스로 옮기기로 했다** (DESIGN §5.1). M3 때 "M4에서 좌표 변환만 붙이면 된다"고 적었는데 부정확했다. 적이 월드를 이동하고 유닛이 사거리로 조준하는 순간, 보드가 캔버스에 남아 있으면 사거리를 픽셀로 다뤄야 하고 URP 2D 렌더러를 세팅해둔 의미가 없어진다.
  - 옮기는 것은 표현뿐이다. `GridBoard`·`PlacementController`·`UnitInstance` 327줄은 그대로 살고 `UnitSlotView`·`BoardScreen` 207줄만 재작성한다.
- **전투도 순수 C#으로 둔다.** `CombatContext.Tick(float deltaTime)`이 핵심이다. `deltaTime`을 인자로 받으면 고정 틱을 강제하지 않아 §5.6과 충돌하지 않으면서, 테스트에서 전투 한 판을 통째로 시뮬레이션할 수 있다. **M4 검증 조건이 그대로 EditMode 테스트가 된다.**
- **트랙은 닫힌 루프다.** §5.4가 "적이 새어나갈 때"가 아니라 "제한시간 종료 시 잔여 적 수만큼" 라이프를 깎으므로, 적은 죽을 때까지 계속 돈다. 웨이포인트는 씬 오브젝트로 두지 않고 `GridBoard`가 그리드 크기에서 계산한다. 적 위치는 진행도(0~1) 하나로 표현하고 월드 좌표는 그릴 때만 변환한다.
- **M4에서 만들지 않는 것:** 타겟팅 방식 enum(1종 고정), 투사체(즉시 히트), 골드·처치 보상 필드, `TargetRegistry` 클래스(`CombatContext`가 직접 보유). 전부 "쓰일 때 만든다" 원칙이다.
- **§7 열린 이슈 1번(저등급 편중)은 보류하기로 했다.** 어느 안을 골라도 전투 루프·데이터 구조는 바뀌지 않아 M4가 막히지 않는다. 다만 M4를 마쳐도 난이도가 말이 되는지는 알 수 없다 — M5 전에는 정해야 한다.
- M4 세부 단계 6개와 각각의 검증 조건을 DESIGN §8에 적었다. a단계는 **기존 87개 테스트가 무수정으로 통과**하는 것이 검증 조건이다.

### 2026-08-08 — M3: 그리드 + 배치 + 머지

- **`GridBoard`(순수 C#)가 15슬롯 점유와 배치·머지 규칙을 소유한다.** `TryPlace`는 빈 칸이면 배치, 같은 유닛·같은 성급이면 머지, 그 외에는 **보드를 건드리지 않고** 거부한다.
- **`UnitInstance`는 불변이다.** 머지는 성급을 올리는 대신 승급된 새 인스턴스를 만들고 보드가 슬롯을 교체한다. 중간 상태가 생기지 않는다.
- **구현 중 발견한 설계 구멍: 그대로 뒀으면 ★3에 영원히 도달할 수 없었다.** 머지가 "소환 유닛을 놓을 때"만 일어나면, 소환 유닛은 항상 ★1이라 ★2에 얹을 수 없고 보드 위 ★2 두 기를 합칠 방법이 없다.
  - 처음 쓴 테스트 `board.TryPlace(0, board[1])`는 통과했지만 **UI에 그런 경로가 없었다.** 테스트가 실제 사용을 반영하지 못한 사례다. 게다가 그 호출은 1번 슬롯을 비우지도 않아 유닛이 복제됐다.
  - `TryMergeSlots(from, to)`를 추가했다. 재료 슬롯이 비워진다. UI는 유닛을 탭해 고르고 상대를 탭해 합치는 2탭 방식이다. DESIGN §5.2에 두 경로를 명시했다.
- **DESIGN §2와 달라진 것 2가지** (문서를 실제에 맞게 고쳤다):
  - `GridSlot` 클래스를 만들지 않았다. 슬롯은 `UnitInstance[]`면 충분하다.
  - `MergeResolver`를 만들지 않았다. 머지는 배치의 일부라 `GridBoard`가 함께 처리한다. 규칙 하나를 위해 클래스를 나누지 않았다.
- **보드는 uGUI 그리드다.** DESIGN §5.1의 슬롯↔월드 좌표 변환은 유닛이 월드의 적을 조준해야 하는 M4에서 붙인다.
- **성급 배수는 사거리에 곱하지 않는다.** 사거리까지 늘면 배치 위치를 고르는 의미가 줄어든다.
- 유닛 13종 + `HandUnitTable` 에셋을 생성했다. 공격력은 등장 확률(DESIGN §3.3)에 대충 반비례시킨 시작값이고 **전부 플레이스홀더**다.
- **Game 뷰가 16:9 가로라 보드가 잘렸다.** 타깃은 9:16 세로(DESIGN §0)다. Portrait 1080x1920을 추가해 선택했다.
- **§7 열린 이슈 2번(그리드 가득 참)을 임시 처리했다.** 놓을 자리가 없으면 "Discard Unit" 버튼을 내고 유닛을 버린다. 보상이 없어 아깝기만 한 처리이므로 정식 결정 전까지의 자리끼움이다.
- **테스트 26개 추가** (`GridBoardTests`) — 배치/머지/거부, 성급·종류 불일치, ★3 상한, 거부 시 보드 무변경, 슬롯 간 머지로 재료 칸 비워짐, `CanPlaceAt`·`CanMergeSlots`·`HasMergePartner`가 실제 시도 결과와 일치, 머지가 원본 인스턴스를 바꾸지 않음.
- **검증:** EditMode **87/87 통과**. Game 씬 플레이로 확인 — Guard ★1(ATK 14) 두 기 머지 → **★2 ATK 28**(정확히 배수 2배), 점유 슬롯은 늘지 않음. Scout ★1 넷을 2탭 머지로 이어 붙여 **★3 ATK 40** 도달, ★3에는 더 못 얹음. 다른 유닛이 있는 칸은 클릭 자체가 막힘. 보드 15/15에서 `IsStuck` → Discard 버튼 노출 → 포기 동작까지 확인. **콘솔 에러·경고 0건.**
- **남은 것:** 배치·머지 연출 없음(즉시 반영). 유닛 이동(빈 칸으로 옮기기)은 넣지 않았다 — 요구에 없었다.

### 2026-08-07 — M2: 카드 UI + 드로우/교체 플로우

- **`RoundContext`(순수 C#)가 페이즈 순서를 강제한다.** Draw → Exchange(자리당 1회, 여러 번 호출) → FinishExchange → Evaluate. 잘못된 순서로 부르면 예외를 던진다. MonoBehaviour가 아니라서 EditMode 테스트가 그대로 붙는다 — 이게 UI를 통하지 않고 규칙을 검증할 수 있는 이유다.
- **교체는 자리 단위로 잠근다.** `IsLocked(index)`. 한 라운드에 교체를 여러 번 누를 수 있지만 같은 자리는 한 번뿐이라 최대 5장까지 바뀐다. 나눠 바꾸게 한 이유는 바뀐 결과를 보고 다음 선택을 하게 만들기 위함이다(DESIGN §1). 잠긴 자리가 섞인 요청은 **손패를 한 장도 건드리지 않고** 통째로 거부한다 — 검사를 전부 끝낸 뒤에 교체한다.
- 교체가 여러 번이 되면서 "교체 끝"이 별도 동작이 되어야 했다. `FinishExchange()`와 UI의 `Confirm Hand` 버튼이 그것이다. 버튼이 2개(교체/확정)에서 3개(Exchange/Confirm Hand/New Round)로 늘었다.
- **`RoundController`(MonoBehaviour)는 이벤트만 노출한다.** `HandChanged`/`PhaseChanged`/`Evaluated`. UI가 구독하고 입력은 메서드로 되돌려준다. Game이 UI를 모르는 상태를 유지했다(DESIGN §2).
- Evaluate는 종료 조건이 "즉시"라(DESIGN §1) `ConfirmHand` 안에서 교체 종료 직후 바로 판정한다. 플레이어 입력이 필요한 페이즈는 Exchange 하나뿐이다.
- 잠긴 카드는 `Button.interactable = false`로 표시한다. uGUI의 disabled 틴트가 그대로 "못 바꾸는 자리"로 읽혀서 별도 잠금 아이콘을 두지 않았다.
- **UI는 uGUI + TextMeshPro.** TMP Essential Resources가 임포트되어 있지 않아 `TMP_PackageResourceImporter.ImportResources(true, false, false)`로 먼저 넣었다(메뉴 항목은 대화창을 띄워 자동화가 안 된다).
- **`EventSystem`에 `InputSystemUIInputModule`을 붙였다.** `activeInputHandler: 1`(Input System 전용)이라 기본 `StandaloneInputModule`을 쓰면 런타임에 터진다.
- 카드 5칸은 **씬에 고정**했다. 손패 크기가 상수 5라 프리팹/런타임 생성이 필요 없다.
- **무늬는 기호(♠) 대신 문자(S/H/D/C).** TMP 기본 폰트 LiberationSans SDF에 카드 심볼 글리프가 없어 기호를 쓰면 두부가 나온다. 같은 이유로 족보명도 영문(`HandCategoryNames`)이다.
- 선택 하이라이트를 처음엔 글자 위에 얹었더니 랭크·무늬가 뭉개졌다. `SetAsFirstSibling()`으로 배경 바로 위·글자 아래로 내렸다.
- **테스트 25개 추가** (`RoundContextTests`) — 페이즈 순서 강제, 자리 잠금, 여러 번 교체, **나눠서 교체해도 한 번에 교체한 것과 손패가 같음**(같은 시드 기준), 잠긴 자리 섞였을 때 손패 무변경, 교체 후에도 손패 무중복, 인덱스 범위·중복 검증, 시드 재현성, `HandEvaluator` 결과 일치.
- **검증:** EditMode **61/61 통과**. Game 씬 플레이로 전체 플로우 실행 — 5장 드로우 → 1장 교체 → 결과 보고 1장 더 → 2장 한꺼번에 → 마지막 1장 → 확정 → 족보명 표시("One Pair") → New Round로 잠금 해제까지 확인. 잠긴 카드 클릭 차단, 전부 잠겼을 때 Exchange 비활성·안내 문구 전환도 확인. **콘솔 에러·경고 0건.**
- **남은 것:** 카드 뒤집기·교체 연출 없음(즉시 교체). M3에서 `HandUnitTable`과 소환을 붙일 때 `RoundPhase.Place`가 실제 동작을 갖는다.

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
