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
   5장      1회 교체     족보 판정    배치/머지   웨이브     클리어 판정
                                                          │
                                                          └→ 실패 시 [GameOver]
```

각 페이즈의 책임과 종료 조건:

| 페이즈 | 하는 일 | 종료 조건 |
|---|---|---|
| `Draw` | 52장 덱을 셔플해 5장 드로우 | 즉시 |
| `Exchange` | 플레이어가 0~5장 선택 → 교체 (**라운드당 1회**) | 플레이어 확정 입력 |
| `Evaluate` | 최종 5장 → 족보 판정 → 유닛 1기 결정 | 즉시 |
| `Place` | 소환된 유닛을 그리드 슬롯에 배치. 동일 유닛끼리 머지 | 플레이어 확정 입력 |
| `Combat` | 웨이브 스폰, 유닛 자동 전투, 제한시간 카운트 | 적 전멸 or 시간 초과 |
| `Result` | 클리어/실패 판정, 보상 정산 | 즉시 → 다음 `Draw` |

**설계 결정: 페이즈는 순차적이고 배타적이다.** 레퍼런스 게임은 전투 중에도 소환이 가능하지만, 요청하신 플로우("라운드를 클리어하면 다시 카드 뽑기 플로우로")를 그대로 따른다. 나중에 동시 진행으로 바꾸려면 `Place`를 `Combat`과 병렬 상태로 승격시키면 되고, 그 외 시스템은 영향받지 않는다.

---

## 2. 어셈블리 / 레이어 구조

3개 어셈블리로 나눈다. 목적은 **포커 로직을 Unity 없이 테스트 가능하게 만드는 것** 하나뿐이다.

```
PokerDefense.Poker      ← 순수 C#. UnityEngine 참조 없음. EditMode 테스트 대상
        ↑
PokerDefense.Game       ← 런타임 전체 (플로우/보드/유닛/적/데이터 SO)
        ↑
PokerDefense.UI         ← 카드 UI, HUD, 팝업
```

- `Poker`는 `Game`을 모른다. `Game`은 `UI`를 모른다 (UI가 `Game`을 구독).
- `UI` 분리는 카드 연출과 전투 로직이 서로를 오염시키지 않게 하기 위함. 규모가 더 커지지 않으면 이 3개에서 늘리지 않는다.

### 폴더 구조

```
Assets/_Project/
├── Art/            Sprites/, UI/, VFX/   (생성형 AI 산출물 배치처)
├── Audio/
├── Data/           ScriptableObject 에셋 인스턴스 (Units/, Enemies/, Waves/)
├── Prefabs/        Units/, Enemies/, Projectiles/, UI/
├── Scenes/         Boot.unity, Game.unity
└── Scripts/
    ├── Poker/          (asmdef: PokerDefense.Poker)
    ├── Game/           (asmdef: PokerDefense.Game)
    │   ├── Flow/       GameFlowController, RoundContext
    │   ├── Board/      GridBoard, GridSlot, PlacementController, MergeResolver
    │   ├── Units/      UnitInstance, UnitTargeting, UnitAttack, Projectile
    │   ├── Enemies/    EnemyInstance, EnemyPath, WaveRunner, TargetRegistry
    │   └── Data/       *Definition.cs (SO 클래스 정의)
    ├── UI/             (asmdef: PokerDefense.UI)
    └── Tests/EditMode/ (asmdef: PokerDefense.Tests.EditMode)
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

`PokerDefense.Tests.EditMode`에서 13개 카테고리 각각의 대표 핸드 + 경계 케이스를 검증한다.
- 마운틴이 `Straight`가 아니라 `Mountain`으로 잡히는가
- 백스트레이트가 `Straight`로 잡히지 않는가 (A를 1로 해석)
- K-A-2-3-4 는 스트레이트가 **아닌가** (랩어라운드 불허)
- 로열 스트레이트 플러시가 `Flush`/`Mountain`/`StraightFlush` 어디로도 새지 않는가

**이 테스트가 전부 통과하는 것이 포커 코어의 완료 조건이다.**

---

## 4. 데이터 (ScriptableObject)

수치는 전부 여기로 뺀다. 코드에는 하드코딩된 밸런스 값을 두지 않는다.

| SO | 필드 |
|---|---|
| `UnitDefinition` | id, 표시명, 스프라이트, 공격력, 공격속도, 사거리, 타겟팅 방식, 투사체 프리팹, 스타 배수(★1/★2/★3) |
| `HandUnitTable` | `HandCategory → UnitDefinition` 매핑 1개 (에셋 1개, 전역) |
| `EnemyDefinition` | id, 스프라이트, 최대HP, 이동속도, 처치 보상, 타입(일반/다수/보스) |
| `WaveDefinition` | 스폰 엔트리 리스트(적, 수량, 간격), 제한시간, 웨이브 번호 |
| `StageDefinition` | `WaveDefinition` 순서 리스트, 시작 라이프/골드 |

---

## 5. 전투 (`Combat` 페이즈)

### 5.1 보드

- 중앙 **5열 × 3행 = 15슬롯** 그리드. 슬롯 하나에 유닛 하나.
- 적은 그리드를 감싸는 **고정 웨이포인트 트랙**을 순회한다. 유닛은 사거리 안에 들어온 적을 자동 공격한다.
- `GridBoard`가 슬롯 ↔ 월드 좌표 변환과 점유 상태를 소유한다. 유닛/적은 서로를 직접 찾지 않고 `TargetRegistry`(활성 적 목록)를 통해 조회한다 — 매 프레임 `FindObjectsOfType` 호출을 막기 위함.

### 5.2 머지

**설계 결정: 머지는 티어 상승이 아니라 "성급(★) 상승"이다.**
동일 `UnitDefinition` + 동일 성급 유닛 2기 → 같은 유닛의 ★+1 (최대 ★3). 스탯은 `UnitDefinition`의 성급 배수를 적용한다.

대안으로 "동일 티어 2기 → 상위 티어 1기"가 있지만, 그러면 *하이카드 2기 = 원페어 1기*가 되어 족보를 맞춘 가치가 희석된다. 족보 축(유닛 종류)과 머지 축(성급)을 직교시키는 편이 낫다.

### 5.3 클리어 판정

- 제한시간 내 웨이브 전멸 → 클리어, 골드 보상, 다음 라운드.
- 시간 초과 → 잔여 적 수만큼 라이프 감소. 라이프 0 → `GameOver`.
- 잔여 적은 다음 웨이브로 넘기지 않고 제거한다 (누적으로 인한 데스 스파이럴 방지).

### 5.4 시뮬레이션 방식

PvE 전용이므로 결정론적 고정 틱을 도입하지 않는다. 일반적인 `Update` + `deltaTime` 으로 충분하다. (PvP를 나중에 붙일 경우 이 결정을 재검토해야 하며, 영향 범위는 `Units/`·`Enemies/`에 한정된다.)

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

2. **그리드가 가득 찼을 때.** 15슬롯이 다 차면 소환된 유닛을 어떻게 할지 (자동 판매 / 배치 거부 / 강제 머지 선택).

3. **골드의 용도.** 위 1번의 추가 소환 외에 유닛 판매·글로벌 강화 등이 필요한지.

4. **스테이지 길이.** 몇 웨이브까지 가면 클리어인지, 아니면 무한 진행 + 최고 기록인지.

이 중 **1번은 M4 이전에 결정되어야** 전투 밸런싱을 시작할 수 있다. 2~4번은 M5 이후로 미뤄도 무방하다.

---

## 8. 마일스톤

각 단계는 **검증 조건을 통과해야** 다음으로 넘어간다.

| # | 내용 | 검증 조건 |
|---|---|---|
| M0 | 폴더/asmdef 뼈대, URP 2D 렌더러 설정 | 빈 씬이 에러 없이 실행됨 |
| M1 | 포커 코어 (Card/Deck/HandEvaluator) | §3.4의 EditMode 테스트 전부 통과 |
| M2 | 카드 UI + 드로우/교체 플로우 | 5장 드로우 → N장 선택 교체 1회 → 족보명 화면 표시 |
| M3 | 그리드 + 배치 + 머지 | 소환된 유닛 배치 → 동일 유닛 2기 머지 시 ★2 및 스탯 상승 확인 |
| M4 | 웨이브 + 전투 + 클리어 판정 | 제한시간 내 전멸 시 클리어, 초과 시 라이프 감소 |
| M5 | 라운드 루프 결합 + HUD | 라운드 3회 연속 진행이 끊김 없이 동작 |
| M6 | 아트 교체 + 밸런싱 데이터 채우기 | 플레이스홀더 스프라이트가 전부 대체됨 |

M0~M5는 **플레이스홀더 도형/색상 스프라이트**로 진행한다. 생성형 AI 이미지는 M6에서 한꺼번에 교체하며, 필요 목록은 [ART_REQUEST.md](ART_REQUEST.md)에 정리해 두었다.
