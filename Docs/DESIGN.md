# 포커 디펜스 — 기본 구조 설계

> 이 문서는 **구조 설계**만 다룬다. 수치 밸런싱은 ScriptableObject 데이터로 분리하며 이 문서에 고정하지 않는다.
> 작업 진행 기록은 [HISTORY.md](HISTORY.md), 필요한 이미지 목록은 [ART_REQUEST.md](ART_REQUEST.md) 참조.

---

## 0. 확정된 전제

| 항목 | 결정 |
|---|---|
| 게임 모드 | **싱글 PvE만.** 네트워크 코드 없음 |
| 렌더링 | **2D 스프라이트 (URP 2D Renderer)** |
| 소환 규칙 | **족보 1개 = 유닛 1기** |
| 배치 규칙 | **누적 배치 + 동일 유닛 머지** |
| 엔진 | Unity 6000.4.0f1 / URP 17.4 / Input System 1.19 |
| 타깃 | 모바일 세로 (레퍼런스와 동일한 9:16 기준) |

---

## 1. 라운드 루프

```
        ┌──────────────────────────────────────────────┐
        ↓                                              │
 [Draw] → [Exchange] → [Evaluate] → [Place] → [Combat] → [Result]
   5장     자리당 1회    족보 판정    배치/머지   웨이브     클리어 판정
                                                          │
                                                          └→ 실패 시 [GameOver]
```

각 페이즈의 책임과 종료 조건:

| 페이즈 | 하는 일 | 종료 조건 |
|---|---|---|
| `Draw` | 52장 덱을 셔플해 5장 드로우 | 즉시 |
| `Exchange` | 카드를 골라 교체. **여러 번 가능하되 한 자리는 한 번만** | 플레이어 확정 입력 |
| `Evaluate` | 최종 5장 → 족보 판정 → 유닛 1기 결정 | 즉시 |
| `Place` | 소환된 유닛을 그리드 슬롯에 배치. 동일 유닛끼리 머지 | 플레이어 확정 입력 |
| `Combat` | 웨이브 스폰, 유닛 자동 전투, 제한시간 카운트 | 적 전멸 or 시간 초과 |
| `Result` | 클리어/실패 판정, 보상 정산 | 즉시 → 다음 `Draw` |

**설계 결정: 교체는 자리 단위로 잠근다.** 라운드 안에서 몇 번이든 교체를 누를 수 있지만, 한 번 바꾼 자리는 그 라운드에 다시 못 바꾼다. 결과적으로 최대 교체 장수는 5장으로 "한 번에 몰아서 5장까지"와 같다. 나눠 바꾸게 한 이유는 **바뀐 결과를 보고 다음 선택을 하게** 만들기 위함이다. 한 장씩 열어보는 편이 기대감이 크다.

부작용 하나: 중간 정보를 보고 판단하므로 플레이어가 약간 더 강해진다. §7 열린 이슈 1번(저등급 편중)에는 유리한 방향이라 지금은 그대로 둔다.

**설계 결정: 페이즈는 순차적이고 배타적이다.** 레퍼런스 게임은 전투 중에도 소환이 가능하지만, 요청하신 플로우("라운드를 클리어하면 다시 카드 뽑기 플로우로")를 그대로 따른다. 나중에 동시 진행으로 바꾸려면 `Place`를 `Combat`과 병렬 상태로 승격시키면 되고, 그 외 시스템은 영향받지 않는다.

---

## 2. 어셈블리 / 레이어 구조

**asmdef를 쓰지 않는다.** 모든 코드가 Unity의 predefined assembly에 들어간다.

```
Assembly-CSharp           ← 게임 코드 전부 (Poker/Game/UI)
        ↑
Assembly-CSharp-Editor    ← Editor/ 폴더 밑. EditMode 테스트
```

`Assembly-CSharp-Editor`는 `Assembly-CSharp`을 자동으로 참조하므로 테스트가 게임 코드를 볼 수 있다. 에디터에서 폴더만 만들면 되고 설정할 것이 없다.

### 레이어는 폴더와 네임스페이스로만 표현한다

```
PokerDefense.Poker      ← 포커 규칙 전용
        ↑
PokerDefense.Game       ← 런타임 전체 (플로우/보드/유닛/적/데이터 SO)
        ↑
PokerDefense.UI         ← 카드 UI, HUD, 팝업
```

- `Poker`는 `Game`을 모른다. `Game`은 `UI`를 모른다 (UI가 `Game`을 구독).
- **이 방향을 컴파일러가 강제하지 않는다.** 한 어셈블리이므로 어느 쪽이든 서로를 참조할 수 있다. 지키는 것은 전적으로 관례다.

### 관례로 지킬 것

- **역방향 참조 금지.** `Poker` 네임스페이스 안에서 `PokerDefense.Game`/`PokerDefense.UI`를 `using` 하지 않는다. `Game`에서 `PokerDefense.UI`를 `using` 하지 않는다.
- **셔플에 `UnityEngine.Random`을 쓰지 않는다.** 전역 정적 상태라 인스턴스별 시드를 잡을 수 없고, 그러면 `Deck(int seed)`의 재현성 테스트가 성립하지 않는다. `System.Random` 주입을 유지한다.
- **`Poker`에 ScriptableObject·MonoBehaviour를 두지 않는다.** 족보 → 유닛 매핑처럼 게임 밸런스에 속하는 것은 `Game`의 `HandUnitTable`이 담당한다.

컴파일러가 막아주지 않으므로 이 세 줄이 유일한 방어선이다. 어겨도 빌드는 통과한다.

### 테스트가 asmdef 없이 동작하는 근거

Unity Test Framework 1.6.0 소스 기준:

- `EditorLoadedTestAssemblyProvider.cs` — 테스트 러너는 asmdef를 보지 않는다. **로드된 어셈블리 중 `nunit.framework`를 참조하는 것**을 전부 스캔하고, `AssemblyFlags.EditorOnly`면 EditMode로 분류한다. `Assembly-CSharp-Editor`가 여기에 해당한다.
- `FolderPathTestCompilationContextProvider.cs` — 경로에 `Editor` 폴더가 있으면 테스트 스크립트가 컴파일 가능한 위치로 인정한다.
- `nunit.framework.dll`(`com.unity.ext.nunit`)은 `isExplicitlyReferenced: 0`이라 predefined assembly가 자동 참조한다.

**대신 포기한 것:**

- `[UnityTest]`, `LogAssert` 등 `UnityEngine.TestTools`를 쓸 수 없다. `UnityEngine.TestRunner.asmdef`가 `autoReferenced: false`라 predefined assembly가 자동 참조하지 않는다. 순수 NUnit(`[Test]`, `[TestCase]`, `Assert`)만 가능하다.
- PlayMode 테스트 불가. 필요해지면 그때 테스트용 asmdef를 하나 만든다.
- 파일 하나 고쳐도 전체 재컴파일. 이 규모에서는 체감되지 않는다.

### 폴더 구조

```
Assets/_Project/
├── Art/            Sprites/, UI/, VFX/   (생성형 AI 산출물 배치처)
├── Audio/
├── Data/           ScriptableObject 에셋 인스턴스 (Units/, Enemies/, Waves/)
├── Prefabs/        Units/, Enemies/, Projectiles/, UI/
├── Scenes/         Boot.unity, Game.unity
└── Scripts/
    ├── Poker/          namespace PokerDefense.Poker
    ├── Game/           namespace PokerDefense.Game
    │   ├── Flow/       RoundPhase, RoundContext, RoundController,
    │   │               CombatContext, CombatController, StageContext
    │   ├── Board/      GridBoard, PlacementController
    │   ├── Units/      UnitInstance
    │   ├── Enemies/    EnemyInstance
    │   └── Data/       UnitDefinition, HandUnitTable, EnemyDefinition,
    │                   WaveDefinition, StageDefinition
    ├── UI/             namespace PokerDefense.UI
    └── Tests/Editor/   → Assembly-CSharp-Editor (폴더명이 Editor여야 함)
```

---

## 3. 포커 코어 (`PokerDefense.Poker`)

### 3.1 카드

```csharp
public enum Suit  { Spade, Heart, Diamond, Club }        // 4
public enum Rank  { Two = 2, ..., Ten = 10, Jack, Queen, King, Ace = 14 }  // 13

public readonly struct Card { public readonly Suit Suit; public readonly Rank Rank; }
```

- `Ace = 14` 로 두고, 백스트레이트(A-2-3-4-5) 판정 시에만 A를 1로 재해석한다. 조커 없음 → 52장.

### 3.2 덱

```csharp
public sealed class Deck
{
    public Deck(int seed);      // System.Random 시드 주입 → 테스트 재현 가능
    public Card Draw();
    public int Remaining { get; }
}
```

**설계 결정: 덱은 라운드마다 새로 셔플한다.** 라운드 간 덱을 이어가면 카드 카운팅이라는 전략 축이 생기지만, 후반 라운드에서 덱 고갈 처리·리셋 타이밍 등 규칙이 붙는다. 지금은 라운드 독립이 단순하다. (교체는 같은 덱에서 뽑으므로 **한 라운드 안에서는 중복 카드가 나오지 않는다.**)

### 3.3 족보 판정

```csharp
public enum HandCategory
{
    HighCard, OnePair, TwoPair, ThreeOfAKind,
    Straight, Flush, FullHouse, FourOfAKind, StraightFlush,
    // 특수 (기본 카테고리의 하위 분류를 별도 승격)
    BackStraight,        // A-2-3-4-5
    Mountain,            // A-K-Q-J-10
    BackStraightFlush,   // A-2-3-4-5 동일 무늬
    RoyalStraightFlush,  // A-K-Q-J-10 동일 무늬
}

public readonly struct HandResult
{
    public readonly HandCategory Category;
    public readonly IReadOnlyList<Card> KeyCards;   // 연출용 하이라이트 (페어 2장 등)
}

public static class HandEvaluator
{
    public static HandResult Evaluate(IReadOnlyList<Card> five);
}
```

**주의: `HandCategory`의 enum 순서를 "강함"으로 쓰지 않는다.** 백스트레이트는 정통 포커에서 가장 약한 스트레이트지만 이 게임에서는 특수 유닛을 준다. 즉 **판정 결과(Category)와 보상 세기(유닛)는 별개 축**이며, 연결은 코드가 아니라 데이터 테이블(`HandUnitTable`)이 담당한다. 밸런싱을 코드 수정 없이 바꾸기 위함이다.

**참고 — 5장 드로우 시 등장 확률** (밸런싱 시작점):

| 족보 | 확률 |
|---|---|
| 하이카드 | 50.1% |
| 원페어 | 42.3% |
| 투페어 | 4.75% |
| 트리플 | 2.11% |
| 스트레이트(마운틴·백스 제외) | 0.31% |
| 플러시 | 0.197% |
| 풀하우스 | 0.144% |
| 마운틴 / 백스트레이트 | 각 0.039% |
| 포카드 | 0.024% |
| 스트레이트 플러시(특수 제외) | 0.00123% |
| 로열 / 백스트레이트 플러시 | 각 0.000154% |

→ **90% 이상이 하이카드·원페어로 몰린다.** 교체 1회로 이 분포가 다소 완화되지만, 저등급 유닛 위주 전개가 기본값이 된다. 대응책은 §7의 열린 이슈 참조.

### 3.4 테스트 (M1의 성공 기준)

`Scripts/Tests/Editor/`에서 13개 카테고리 각각의 대표 핸드 + 경계 케이스를 검증한다.
- 마운틴이 `Straight`가 아니라 `Mountain`으로 잡히는가
- 백스트레이트가 `Straight`로 잡히지 않는가 (A를 1로 해석)
- K-A-2-3-4 는 스트레이트가 **아닌가** (랩어라운드 불허)
- 로열 스트레이트 플러시가 `Flush`/`Mountain`/`StraightFlush` 어디로도 새지 않는가

**이 테스트가 전부 통과하는 것이 포커 코어의 완료 조건이다.**

---

## 4. 데이터 (ScriptableObject)

수치는 전부 여기로 뺀다. 코드에는 하드코딩된 밸런스 값을 두지 않는다.

| SO | 필드 | 상태 |
|---|---|---|
| `UnitDefinition` | id, 표시명, 플레이스홀더 색, 공격력, 공격속도, 사거리, 성급 배수(★1/★2/★3) | M3 완료 |
| `HandUnitTable` | `HandCategory → UnitDefinition` 매핑 1개 (에셋 1개, 전역) | M3 완료 |
| `EnemyDefinition` | id, 표시명, 플레이스홀더 색, 최대HP, 이동속도, 타입(일반/다수/보스) | M4 |
| `WaveDefinition` | 스폰 엔트리 리스트(적, 수량, 간격), 제한시간, 웨이브 번호 | M4 |
| `StageDefinition` | `WaveDefinition` 순서 리스트, 시작 라이프 | M4 |

**아직 넣지 않은 필드가 있다.** 쓰이지 않는 필드를 미리 두면 추측 코드가 된다.

- **스프라이트 대신 플레이스홀더 색.** M0~M5는 도형·색으로 진행하고 M6에서 스프라이트로 교체한다.
- **타겟팅 방식·투사체 프리팹 없음.** M4는 타겟팅 1종(사거리 내 가장 앞선 적)과 즉시 히트로 시작한다. 두 번째 방식이 실제로 필요해질 때 필드를 만든다.
- **골드·처치 보상 없음.** §7 열린 이슈 1·3번이 미결이다. 정해지면 그때 추가한다.

---

## 5. 전투 (`Combat` 페이즈)

### 5.1 보드

- 중앙 **5열 × 3행 = 15슬롯** 그리드. 슬롯 하나에 유닛 하나.
- 적은 그리드를 감싸는 **고정 웨이포인트 트랙**을 순회한다. 유닛은 사거리 안에 들어온 적을 자동 공격한다.
- `GridBoard`가 점유 상태와 배치·머지 규칙을 소유한다. 슬롯은 `UnitInstance[15]`이고 인덱스로만 다룬다.
- 유닛/적은 서로를 직접 찾지 않고 활성 적 목록을 통해 조회한다 — 매 프레임 `FindObjectsOfType` 호출을 막기 위함. 이 목록은 `CombatContext`가 직접 들고 있으며 별도 클래스로 빼지 않는다.

**설계 결정: 보드는 월드 스페이스다 (M4).** M3까지는 uGUI 그리드였다. 적이 월드를 이동하고 유닛이 사거리로 조준하는 순간, 보드가 캔버스에 남아 있으면 사거리를 픽셀로 다뤄야 하고 URP 2D 렌더러·2D 물리·스프라이트 도구를 쓸 수 없다. 카드 UI만 uGUI로 남는다.

옮기는 대상은 **표현뿐이다.** `GridBoard`·`PlacementController`·`UnitInstance`(327줄)는 그대로 살고 `UnitSlotView`·`BoardScreen`(207줄)만 재작성한다. `GridBoard`에는 `SlotToWorld(int index)` 하나가 추가된다. 규칙을 순수 C#으로 빼둔 값어치가 여기서 나온다.

### 5.2 머지

**설계 결정: 머지는 티어 상승이 아니라 "성급(★) 상승"이다.**
동일 `UnitDefinition` + 동일 성급 유닛 2기 → 같은 유닛의 ★+1 (최대 ★3). 스탯은 `UnitDefinition`의 성급 배수를 적용한다.

대안으로 "동일 티어 2기 → 상위 티어 1기"가 있지만, 그러면 *하이카드 2기 = 원페어 1기*가 되어 족보를 맞춘 가치가 희석된다. 족보 축(유닛 종류)과 머지 축(성급)을 직교시키는 편이 낫다.

**머지 경로는 두 가지다. 둘 다 있어야 한다.**

1. **소환 유닛 → 보드**: 배치할 때 같은 유닛·같은 성급 칸을 고르면 합쳐진다.
2. **보드 → 보드**: 이미 놓인 두 유닛을 합친다 (`GridBoard.TryMergeSlots`). 재료 쪽 칸이 비워진다.

2번이 없으면 **★3에 도달할 수 없다.** 소환되는 유닛은 항상 ★1이라 ★2에 얹을 수 없고, 보드 위의 ★2 두 기를 합칠 방법이 사라지기 때문이다. UI에서는 유닛을 탭해 고르고 상대를 탭해 합친다.

성급은 공격력·공격속도에만 곱해지고 **사거리에는 곱해지지 않는다.** 성급으로 사거리까지 늘면 배치 위치를 고르는 의미가 줄어든다.

### 5.3 트랙과 적 이동

**트랙은 닫힌 루프다.** 적은 골에 도달해 빠져나가지 않고 죽을 때까지 계속 돈다. §5.4대로 라이프는 "적이 새어나갔을 때"가 아니라 "제한시간이 끝났을 때 잔여 적 수만큼" 깎이기 때문이다. 일반적인 타워 디펜스와 다른 지점이다.

- **웨이포인트를 씬 오브젝트로 두지 않는다.** 그리드를 감싸는 사각 루프라 `GridBoard`가 그리드 크기에서 계산하면 된다. 씬에서 점을 찍어 관리할 이유가 없다.
- **적 위치는 트랙 위 진행도(0~1) 하나로 표현한다.** 월드 좌표는 화면에 그릴 때만 변환한다. 상태가 순수 C#으로 유지되고, "가장 앞선 적"이 진행도 비교 한 번으로 끝난다.

### 5.4 클리어 판정

- 제한시간 내 웨이브 전멸 → 클리어, 다음 라운드.
- 시간 초과 → 잔여 적 수만큼 라이프 감소. 라이프 0 → `GameOver`.
- 잔여 적은 다음 웨이브로 넘기지 않고 제거한다 (누적으로 인한 데스 스파이럴 방지).

### 5.5 타겟팅과 공격

- **타겟은 사거리 안에서 가장 앞선 적** 하나다. M4는 이 한 종류만 만든다.
- **공격은 즉시 히트다.** 투사체는 비행 시간과 빗나감 처리가 따라붙는데 클리어 판정과 무관하다. 연출 단계에서 붙인다.
- 성급 배수는 공격력·공격속도에 곱해지고 사거리에는 곱해지지 않는다 (§5.2).

### 5.6 시뮬레이션 방식

PvE 전용이므로 결정론적 고정 틱을 도입하지 않는다. 일반적인 `Update` + `deltaTime` 으로 충분하다. (PvP를 나중에 붙일 경우 이 결정을 재검토해야 하며, 영향 범위는 `Units/`·`Enemies/`에 한정된다.)

**단, `deltaTime`은 인자로 받는다.**

```csharp
public sealed class CombatContext          // 순수 C#. MonoBehaviour가 아니다
{
    public void Tick(float deltaTime);     // Update()에서 Time.deltaTime을 넘긴다
    public CombatOutcome Outcome { get; }  // InProgress / Cleared / TimedOut
    public int RemainingEnemies { get; }
}
```

고정 틱을 강제하지 않으므로 위 결정과 충돌하지 않으면서, 테스트에서 `Tick(0.1f)`를 원하는 만큼 돌려 전투 한 판을 통째로 시뮬레이션할 수 있다. **M4의 검증 조건이 그대로 EditMode 테스트가 된다.** 포커 코어·라운드·보드와 같은 방식이다.

---

## 6. 씬 구성

- `Boot.unity` — SO 로드, 진입점. 곧바로 `Game` 로드.
- `Game.unity` — 보드, 그리드, UI 캔버스, `GameFlowController` 전부 포함.

씬은 2개면 충분하다. 타이틀/메타 진행은 지금 범위 밖.

---

## 7. 열린 이슈 (결정 필요, 구현 전 확인 요망)

1. **저등급 편중 완화 수단.**
   §3.3대로면 라운드당 유닛 1기 중 90%가 하이카드/원페어다. 머지 재료를 모으는 속도가 매우 느려진다.
   → 레퍼런스 게임처럼 **골드로 추가 소환**(랜덤 저등급 유닛)하는 버튼을 두는 것을 제안한다. 포커는 "고등급 확정 소환", 골드는 "머지 재료 수급"으로 역할이 갈린다.
   대안: 교체 횟수를 라운드 진행에 따라 늘리기 / 교체 시 특정 무늬·숫자를 고정하는 강화 요소.

   **M3 실측:** Scout(하이카드) 4기를 모으는 데 **13라운드**가 걸렸다. ★3 하나에 같은 유닛 4기가 필요하므로 이 속도로는 머지가 사실상 돌아가지 않는다. 위 예측이 그대로 확인됐다.

   **결정 보류 (2026-08-08).** 실제로 전투를 돌려본 뒤 체감을 보고 정하기로 했다. M4는 구조만 만들고 수치는 플레이스홀더로 둔다 — 어느 안을 고르든 전투 루프·데이터 구조는 바뀌지 않으므로 작업이 막히지 않는다. **다만 M4를 마쳐도 "난이도가 말이 되는가"는 확인할 수 없다.** M5(라운드 루프) 이전에는 결정되어야 한다.

2. **그리드가 가득 찼을 때.** 15슬롯이 다 차면 소환된 유닛을 어떻게 할지 (자동 판매 / 배치 거부 / 강제 머지 선택).
   → **M3의 임시 처리:** 놓을 자리가 하나도 없으면(`PlacementController.IsStuck`) "Discard Unit" 버튼을 내고 유닛을 그냥 버린다. 보상이 없어 아깝기만 한 처리이므로 정식 결정 전까지의 자리끼움이다.

3. **골드의 용도.** 위 1번의 추가 소환 외에 유닛 판매·글로벌 강화 등이 필요한지.

4. **스테이지 길이.** 몇 웨이브까지 가면 클리어인지, 아니면 무한 진행 + 최고 기록인지.

1번은 보류하되 **M5 이전에는 결정되어야** 한다 (위 항목 참조). 2~4번은 M5 이후로 미뤄도 무방하다.

---

## 8. 마일스톤

각 단계는 **검증 조건을 통과해야** 다음으로 넘어간다.

| # | 내용 | 검증 조건 |
|---|---|---|
| M0 | 폴더 뼈대, URP 2D 렌더러 설정 | 빈 씬이 에러 없이 실행됨 |
| M1 | 포커 코어 (Card/Deck/HandEvaluator) | §3.4의 EditMode 테스트 전부 통과 |
| M2 | 카드 UI + 드로우/교체 플로우 | 5장 드로우 → 자리당 1회 교체 → 족보명 화면 표시 |
| M3 | 그리드 + 배치 + 머지 | 소환된 유닛 배치 → 동일 유닛 2기 머지 시 ★2 및 스탯 상승 확인 |
| M4 | 웨이브 + 전투 + 클리어 판정 | 제한시간 내 전멸 시 클리어, 초과 시 라이프 감소 |
| M5 | 라운드 루프 결합 + HUD | 라운드 3회 연속 진행이 끊김 없이 동작 |
| M6 | 아트 교체 + 밸런싱 데이터 채우기 | 플레이스홀더 스프라이트가 전부 대체됨 |

M0~M5는 **플레이스홀더 도형/색상 스프라이트**로 진행한다. 생성형 AI 이미지는 M6에서 한꺼번에 교체하며, 필요 목록은 [ART_REQUEST.md](ART_REQUEST.md)에 정리해 두었다.

### M4 세부 단계

M4는 좌표계 이전이 앞에 붙어 덩치가 크다. 단계마다 검증하고 넘어간다.

| # | 작업 | 검증 조건 |
|---|---|---|
| a | 보드를 월드 스페이스로 이전 (§5.1) | 배치·머지가 이전과 동일하게 동작. **기존 87개 테스트가 무수정으로 통과** |
| b | `EnemyDefinition`·`WaveDefinition`·`StageDefinition` + 플레이스홀더 에셋 | 인스펙터에서 웨이브 1개를 구성할 수 있음 |
| c | `CombatContext` 스폰·트랙 이동 (§5.3) | 스폰 수량·간격이 데이터대로, 적이 트랙을 순회 |
| d | 타겟팅·공격·사망 (§5.5) | 사거리 밖 적은 피해를 받지 않음, HP 0에 제거 |
| e | 클리어/시간초과 판정 + 라이프 (§5.4) | **M4 검증 조건을 EditMode 테스트로 고정** |
| f | 씬 연결 (`Place` → `Combat` → `Result`) | 실제 플레이로 클리어·실패를 각각 확인 |

a단계에서 **기존 테스트가 한 줄도 고치지 않고 통과하는 것**이, 좌표계 이전이 규칙을 건드리지 않았다는 증거다. 고쳐야 한다면 표현과 규칙이 섞여 있었다는 뜻이므로 그 지점을 먼저 정리한다.

라운드 루프 전체 결합은 M5다. M4에서는 "Start Combat" 버튼으로 전투에 수동 진입하고 결과만 표시한다.
