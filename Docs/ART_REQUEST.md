# 아트 리소스 요청 목록

M11(아트 교체)에서 생성형 AI로 제작할 이미지 목록이다. **스펙은 확정이고 아래 수치는 제안이 아니라 기준이다.** 하이카드 스카우트로 전 공정(생성 → 분할 → 임포트 → 슬롯 적용 → 코드)을 한 바퀴 돌려 검증했다.

> **유닛 13종의 컨셉·이름·실루엣 규칙은 [DESIGN.md](DESIGN.md) §10.2~10.4가 유일한 출처다.** 여기에 중복해 적지 않는다.
> 적은 §10.5를 따른다.

**M11 검증 조건**(DESIGN §8)은 *유닛·적·카드 셋이 전부 스프라이트로 대체되고 슬롯의 이름 라벨이 사라짐*이다.

---

## 1. 확정 스펙

| 항목 | 값 | 근거 |
|---|---|---|
| 스타일 | **일본 아니메 / 미소년·미소녀 / JRPG·가챠 카드 아트 / 소프트 셀 셰이딩** | 고정 접두어로 재사용 |
| 얼굴 | **드러낸다. 헬멧·바이저·마스크 금지** | 얼굴을 덮으면 전대물(파워레인저)이 된다 |
| **생성 단위** | **유닛 1종당 4방향 턴어라운드 시트 1장** (2×2 격자) | 한 이미지 안에서는 같은 인물이 보장된다. 유닛끼리 화풍이 조금 달라도 "다양한 병종"으로 읽히지만, 같은 유닛의 방향이 다르면 다른 유닛이 된다 |
| **시점** | **4방향** — 아래(정면) / 위(후면) / 좌 / 우 | 적이 닫힌 루프를 360도로 도는데 유닛은 회전하지 않는다 (DESIGN §5.3). 좌우 플립은 쓰지 않는다 |
| 포즈 | **사격 자세 하나.** 대기 포즈를 따로 만들지 않는다 | 유닛은 전투 내내 쏜다 |
| 배경 | 투명 PNG. 그림자·바닥선·프레임 없음 | |
| 캔버스 | 유닛 시트 2:3 세로(예 1024×1536) / 적 256 / 카드 256×358 / 아이콘 128 | 잘라내면 유닛 1장이 약 400×750이 된다 |
| **PPU** | **스프라이트의 픽셀 높이** | 높이 = 1 월드유닛. 잘라낸 크기가 유닛마다 달라도 같은 칸에 맞는다 |
| 해상도 기준 | 1080×1920 (9:16) 모바일 세로 | |

### 판별 크기 — 캔버스보다 이쪽이 진짜 조건이다

**화면에 그려지는 크기는 캔버스보다 훨씬 작다.** 1 월드유닛 = 145.5px이다.

| 요소 | 월드 크기 | **화면 px** |
|---|---|---|
| 보드 슬롯 | 0.92 | **134** |
| 유닛 아트 | 0.85 (슬롯의 85%) | **124** |
| 적 | 0.50 | **73** |
| 트랙 전체 | 6.8 × 4.8 | 989 × 698 |
| 배경 | 화면 전체 | 1080 × 1920 |

**유닛은 124px, 적은 73px에서 서로 구분돼야 한다.** 캔버스를 크게 잡는 것은 축소 여유일 뿐 디테일을 넣으라는 뜻이 아니다.

### 모션 — 정적 스프라이트 + 코드 반동

**프레임 애니메이션을 만들지 않는다.** 방향당 1장, 유닛당 4장이 전부다.

- **표시 크기가 124px이다.** 이 크기에서 프레임 사이클은 거의 지각되지 않는다.
- **공격속도가 초당 0.4~6.75회로 갈린다.** 성급 배수(최대 2.25)까지 곱해져 최속 유닛은 한 발이 **148ms**다. 프레임 애니메이션을 그 속도에 맞추면 뭉갠다.
- **생성형 AI가 프레임 간 일관성을 못 맞춘다.** 2포즈 시트를 시도했을 때 같은 인물은 나왔지만 **다리와 몸통이 통째로 달라져** 교체 순간 캐릭터가 튀었다. 2프레임 교체는 *몸이 고정되고 무기 팔만 바뀌어야* 성립한다.

움직임은 `UnitSlotView`가 낸다. 수치는 코드에 있고 근거는 이렇다.

| 연출 | 값 | 왜 |
|---|---|---|
| 발사 반동 | 스케일 +6%, `Min(0.10초, 0.45/APS)` | 길이를 발사 간격에 물리되 상한을 둔다. 안 그러면 최속 유닛이 복귀를 못 하고 계속 떤다 |
| 대기 호흡 | 스케일 ±1.5%, 주기 약 2.2초 | 느린 유닛은 발사 사이가 2.5초라 반동만으로는 멈춰 보인다 |
| 방향 | 마지막으로 쏜 방향을 유지 | 전투 중이 아니면 정면 |

**반동을 스케일로 낸 이유**는 총구가 카메라를 향하기 때문이다. 화면상 "뒤로 밀림"이 정의되지 않고, 위아래로 흔들면 점프처럼 보인다.

적·카드·배경은 정적 1장이다.

---

## 2. 고정 프롬프트

유닛 13종 모두 이 헤더를 그대로 쓰고 `CHARACTER` 블록만 갈아 끼운다. **스카우트 시트로 검증된 프롬프트다.**

```
Japanese anime illustration, bishounen character design,
JRPG / gacha card art style, soft cel shading with crisp clean line art,
elegant slender proportions, refined delicate facial features,
large expressive detailed eyes, glossy hair with highlights.
NO helmet, NO visor, NO mask.

A 4-DIRECTIONAL CHARACTER TURNAROUND SPRITE SHEET of ONE single character.

=== CRITICAL LAYOUT RULE ===
Divide the canvas into a 2x2 GRID of FOUR EQUAL CELLS.
Place exactly ONE figure in the centre of each cell.
It is the SAME character drawn FOUR times - same face, same hairstyle,
same hair colour, same outfit, same weapon, same height, same build.
NOT four different characters.

TOP-LEFT     = facing DOWN toward the viewer (front view, face visible).
TOP-RIGHT    = facing UP away from the viewer (back view: we see his back
               and the back of his head, the face is NOT visible).
BOTTOM-LEFT  = facing LEFT (full side profile).
BOTTOM-RIGHT = facing RIGHT (full side profile).

NOTHING may cross a cell boundary - not a weapon, not hair, not a card.
Keep at least 15% empty margin inside every cell.
All four figures are exactly the same size and stand on the same baseline
within their cell.
NO grid lines, NO borders, NO separators drawn between cells.
============================

CHARACTER - <유닛별로 갈아 끼우는 블록>

POSE - every cell shows the FIRING pose aimed in that cell's direction:
the weapon is raised and aimed in the direction that figure faces,
compact braced stance, feet about shoulder width apart.
Front view: the barrel is foreshortened, pointing at the viewer.
Back view: the weapon is aimed away from the viewer.
Side views: the weapon is aimed left or right in full profile.

NO holster, NO spare weapon on the belt or hip.
The ONLY visible weapon is the one being fired.
Any playing card shows ONLY a plain suit symbol on an otherwise blank face.
NO rank, NO letter A, NO number, NO corner index.

Transparent background. NO ground, NO ground line, NO shadow, NO platform,
NO frame, NO background art.
NO muzzle flash, NO smoke, NO glow, NO energy effects.
NO text, NO numbers, NO letters anywhere in the image.
```

### 각 줄이 들어간 이유 — 전부 실패에서 나왔다

| 문구 | 왜 |
|---|---|
| `NO text, NO numbers` (대문자) | `no text`만 넣었을 때 가슴에 숫자 `2`가 박혔다. 랭크가 성능과 연결되는 것처럼 보이면 안 된다 (§10.4) |
| `NO helmet, NO visor` | 얼굴을 덮는 마스크 + 동일 슈트 + 정면 대칭이 겹치면 전대물이 된다 |
| `2x2 GRID of FOUR EQUAL CELLS` + `NOTHING may cross a cell boundary` | 5종 한 줄 시트에서 캐릭터 둘이 하나의 연결 덩어리가 되어 자동 분할이 깨졌다. 격자 규칙이 지켜지면 **균등 분할로 자를 수 있다** |
| `It is the SAME character drawn FOUR times, NOT four different characters` | 이걸 안 박으면 서로 다른 캐릭터 넷을 그린다. 다방향 시트의 최대 실패 요인이다 |
| `NO grid lines, NO borders, NO separators` | 격자를 요청하면 구분선을 실제로 그리는데, 그러면 네 칸이 선으로 이어져 알파 분리가 깨진다 |
| `NO holster, NO spare weapon` | 스카우트 허리에 예비 권총이 생겨 **뽑은 1정 + 홀스터 1정 = 2정**이 됐다. 원페어 건슬링어가 정확히 2정이라 구조 언어가 겹친다 |
| `ONLY a plain suit symbol, NO rank` | 카드가 에이스로 나왔다. 하이카드는 족보명 때문에 특히 A를 부른다 |
| `NO muzzle flash, NO glow` | 이펙트가 칸 경계를 넘어 캐릭터를 잇는다 |
| `compact braced stance, NOT spread wide` | 다리를 넓게 벌리면 칸 안에서 캐릭터가 작아진다 |

---

## 3. 생성 시 지켜야 할 규칙

- **구조 숫자를 실루엣으로 읽을 수 있어야 한다.** 2 / 2+2 / 3 / 3+2 / 4 (§10.2). M11의 최대 리스크다.
- **개수를 세는 방식만으로는 이 해상도에서 구조가 안 읽힌다.** 포신 하나가 화면에서 3~5px이라 2문이든 4문이든 뭉툭한 덩어리로 보인다. **무기의 배치로 윤곽을 가르는 축**을 함께 쓴다 — 좁은 세로 / 가로로 넓음 / 위로 솟음 / 부채꼴 / 사각 블록.
- **숫자를 직접 세어 확인한다.** 프롬프트에 총 개수를 숫자로 못 박아도 틀린다.
- **성급이 올라도 구조 숫자를 바꾸지 않는다** (§10.4). 페어는 영원히 2, 트리플은 3, 쿼드는 4다.
- **성급 오라 오버레이를 만들지 않는다.** 슬롯의 `★` 라벨이 성급을 담당한다 — §10.4의 *구조는 실루엣, 성급은 별* 두 축이 유지된다.
- **카드 문양은 유닛마다 고정 심볼 하나다** (§10.4). 한 유닛에 여러 무늬를 섞으면 **무늬가 능력과 연결된다는 오해**(§7에서 폐기한 카드 특성)를 아트가 되살린다.
- **13종이 하나의 카드 군단으로 보여야 한다** (§10.2). 붉은 군복 + 흰·금 장식이 공통 언어다. 이전 유닛 시트를 스타일 레퍼런스로 첨부한다.
- **적은 틴트를 전제하지 않는다.** HP 표시가 밝기 곱셈에서 체력 바로 바뀌었으므로 적 스프라이트는 제 색을 그대로 쓴다.
- **소버린만 실루엣을 약간 크게 잡는다** (§10.4).

---

## 4. 단계별 요청 목록 (총 70장)

유닛 52(13종 × 4방향) + 적 7 + 카드 7 + 배경·트랙 4.
유닛은 **13시트**만 생성하면 된다 — 한 시트가 4장을 낳는다.

### 1단계 — 파일럿 **완료** (하이카드 스카우트)

전 공정을 한 바퀴 돌려 방식을 확정했다. 결과는 `Art/Units/Scout/`에 있다.

| 검증 | 결과 |
|---|---|
| 2×2 분리 | **세로 중앙선 통과 픽셀 0개.** 네 칸이 독립 덩어리 |
| 알파 | 74% 투명, 깨끗함 |
| 동일 인물 | 얼굴·머리·복장·장비 4회 일치 |
| 카드 | 무늬만, 랭크 없음 |
| 방향 판별 (134px 실루엣 IoU) | 좌↔우 **10.3%**, 정면↔측면 23~24%, **정면↔후면 65%** |
| 실플레이 | 적이 트랙을 도는 동안 Up→Right→Down→Left 전부 전환 확인 |

**정면↔후면 65%는 문제가 아니다.** 같은 인물의 앞뒤라 실루엣이 닮는 게 정상이고, 화면에서는 색이 가른다 — 정면은 얼굴 + 흰 조끼, 후면은 검은 뒷머리 + 붉은 등판이다.

### 2단계 — 나머지 유닛 12시트 (48장)

스카우트 시트를 스타일 레퍼런스로 물린다. **구조가 있는 다섯을 먼저** 만든다.

| 우선 | 에셋 | 이름 | 구조 | 역할 |
|---|---|---|---|---|
| — | `Unit_Scout` | 하이카드 스카우트 | 카드 1장, 권총 1정 | Rapid · **완료** |
| 1 | `Unit_Guard` | 원페어 건슬링어 | **2** 쌍권총 | Rapid |
| 1 | `Unit_Ranger` | 투페어 트윈레인저 | **2+2** 양팔 2연장 | Multi |
| 1 | `Unit_Lancer` | 트리플 마크스맨 | **3** 3총구 | Heavy |
| 1 | `Unit_Warden` | 풀하우스 배터리 | **3+2** 주포3 + 보조포2 | Splash |
| 1 | `Unit_Champion` | 포카드 쿼드캐논 | **4** 4연장 중포 | Heavy |
| 2 | `Unit_Trickster` | 백스트레이트 에이스 | A→2→3→4→5 계단 | Rapid |
| 2 | `Unit_Vanguard` | 스트레이트 랜서 | 일직선 창, 카드 5장 일렬 | Pierce |
| 2 | `Unit_Highlander` | 마운틴 킹 | ▲ 산 왕관, 꼭대기에 A | Heavy |
| 2 | `Unit_Mystic` | 플러시 보머 | 같은 무늬 반복 | Splash |
| 2 | `Unit_Revenant` | 백스트레이트 플러시 템페스트 | 같은 무늬 A-5 원형 회전 | Multi |
| 2 | `Unit_Paladin` | 스트레이트 플러시 레일거너 | 같은 무늬 5장 일렬 = 레일 | Pierce |
| 2 | `Unit_Sovereign` | 로열 스트레이트 플러시 소버린 | 10-J-Q-K-A 왕좌, 왕관 | Splash |

> **건슬링어를 먼저 하는 것이 좋다.** 구조 숫자 **2**가 스카우트의 **1**과 바로 대비되어, 개수 축이 실제로 읽히는지 두 유닛으로 확인할 수 있다.

### 3단계 — 적 7장

**타입은 5종이지만 에셋은 7개이고 개별로 만든다.** 73px에서 서로 구분되는 것이 조건이다.

| 에셋 | 타입 | HP | 특성 |
|---|---|---|---|
| `Enemy_Grunt` | Normal | 60 | 기준 |
| `Enemy_Runner` | Runner | 82 | 빠르다 (속도 4.2) |
| `Enemy_Swarm` | Swarm | 50 | 작고 많다 |
| `Enemy_Brute` | Tank | 690 | 크고 느리다 (속도 1.3) |
| `Enemy_MiniBoss` | Boss | 550 | W5 전용 |
| `Enemy_Boss` | Boss | 1300 | W10·W15·W20 |
| `Enemy_FinalBoss` | Boss | 4200 | W20 최종 |

> **미결:** 코드가 적을 전부 `scale 0.5` 고정으로 그린다. 보스 3종을 크게 보이게 하려면 `EnemyDefinition`에 스케일 필드가 필요하다.
> **적도 4방향이 필요한가는 아직 정하지 않았다.** 적은 트랙을 돌며 네 방향으로 움직이므로 유닛만 방향을 갖추면 어색해질 수 있다.

### 4단계 — 카드 7장

**52장을 개별 생성하지 않는다.** 프레임 + 무늬 + 숫자 폰트를 런타임 합성한다.

- 카드 앞면 프레임 1장 / 뒷면 1장 / 선택·교체 하이라이트 프레임 1장
- 무늬 아이콘 4종 (♠ ♥ ♦ ♣) — **이미지로 가면 `♦` 두부 함정이 해소된다** (Maplestory에 U+2666이 없어 `◆`로 대체 중)

### 5단계 — 배경·트랙 4장

**교체가 아니라 신규다.** 지금은 트랙이 아예 안 그려지고 카메라가 단색 `(0.1, 0.1, 0.12)`으로만 지운다.

- 전체 배경 1장 (1080×1920)
- 트랙 텍스처 1장 — 989×698px 영역의 닫힌 사각 루프
- 빈 슬롯 타일 1장 / 슬롯 하이라이트 프레임 1장

> 여기서 **하이라이트가 아트를 밝게 뜨게 하는 것**과 **트랙이 하단 안내 문구를 침범하는 것**(§7-4)을 같이 정리한다.

---

### 이펙트 2장 — **완료**

`Art/VFX/`에 있다. **유닛별이 아니라 13종이 공용으로 쓴다** — 추상 형태라 나눠 쓸 수 있고, 필요하면 색조·크기만 코드에서 바꾼다.

| 파일 | 쓰임 | 수명 |
|---|---|---|
| `MuzzleEffect.png` | 총구 화염. 조준 방향으로 밀어 띄운다 | 0.06초 |
| `HitEffect.png` | 타격 스파크. 맞은 지점에 띄운다 | 0.12초 |

- **캐릭터 프롬프트를 쓰지 않는다.** 이펙트에 두꺼운 검은 아웃라인을 넣으면 스티커처럼 보인다.
- **방사 대칭으로 뽑는다.** 그래야 방향별로 4장을 만들 필요가 없다.
- **밝은 색만 쓰고 어두운 픽셀을 넣지 않는다.** 가산 합성처럼 보이게 하려면 필요하다 (받은 두 장은 어두운 픽셀 0~0.2%였다).
- **총구 위치는 `UnitDefinition.muzzleOffset`에 유닛마다 넣는다.** `x` = 좌우로 미는 거리, `y` = 총구 높이(슬롯 로컬)다. **좌우는 대칭으로 묶는다** — 실측에서 좌우 차이가 3.6px이라 나눌 값어치가 없고, 방향별로 두면 13종 × 4방향 = 52개 좌표가 된다. 후면 보정(`+0.04`)만 공용이다.
  **아트를 넣을 때 스프라이트에서 직접 재서 채운다.** 13종 모두 스카우트 실측값 `(0.24, 0.26)`이 들어가 있으니, 무기 위치가 다른 유닛(쿼드캐논의 가슴 블록, 배터리의 등 포신)은 그 값을 갈아 끼우면 된다.
  > 처음에 가슴 높이(+0.05)로 눈대중해서 **총구보다 30px 아래**에 떴다. 반드시 잴 것 — 재는 법은 §7에 있다.
- **날아가는 총알은 만들지 않는다.** §5.5가 즉시 히트로 못 박았다. 비행 시간이 생기면 규칙과 어긋난다.

---

## 5. M11 범위 밖

- **투사체 3종** — §5.5가 *공격은 즉시 히트, 투사체는 연출 단계*로 못 박았고 코드에 투사체가 없다.
- **버튼 프레임 / HUD 아이콘 / 결과 팝업** — 검증 조건에 걸리지 않는다.
- **피격·소환·머지 이펙트, 희귀 족보 등장 연출** — 연출 단계에서 §10.4의 3단계 스케일 규칙과 함께 본다.

## 6. 코드 변경이 동반되는 것

| 변경 | 위치 | 상태 |
|---|---|---|
| **HP 밝기 → 체력 바** | `CombatScreen` | **완료.** 적 뷰가 루트 + 몸통 + 바 2장 계층이 됐다 |
| **4방향 전환 + 반동** | `UnitDefinition`·`CombatContext`·`UnitSlotView`·`BoardScreen`·`Game.unity` | **완료.** 아트가 없는 유닛은 기존 색 사각형으로 떨어진다 |
| **총구 화염 + 타격 이펙트** | `CombatContext`(피격 지점 노출)·`UnitSlotView`(화염)·`CombatScreen`(타격 풀) | **완료.** 타격은 24개 고정 풀로 돌린다 — Splash 한 발이 여러 기를 때려 매번 만들면 초당 수백 개가 된다 |
| **이름 라벨 제거** | `UnitSlotView` | 13종이 전부 들어오고 판별을 확인한 뒤. 먼저 지우면 색 사각형을 색으로만 구분하게 된다 |
| **카드 텍스트 → 이미지** | `CardView`, `CardText` | 4단계와 함께 |
| **적 스케일** (선택) | `EnemyDefinition`, `CombatScreen.CreateView` | 3단계와 함께 |

---

## 7. 받은 이미지를 넣는 절차

스카우트에서 확립한 순서다.

1. 시트를 `Art/Units/<유닛>/`에 넣는다
2. **2×2 균등 분할** 후 각 칸을 알파 기준으로 트림한다
3. 네 장을 **공통 캔버스에 가로 중앙·아래 정렬**로 다시 채운다 — 크기와 접지선이 같아야 방향이 바뀔 때 캐릭터가 위아래로 튀지 않는다
4. 임포트: `Sprite / Single`, **`npotScale = None`**, `PPU = 스프라이트 높이`, `Bilinear`, 밉맵 켬, 피벗 `BottomCenter`, `Alpha Is Transparency` 켬
5. `UnitDefinition`의 아트 4칸에 물린다
6. **총구 위치를 잰다.** 좌·우 스프라이트에서 가장 바깥으로 뻗은 픽셀이 총구 끝이다. 픽셀 좌표를 슬롯 로컬로 바꿔 `muzzleOffset`에 넣는다

   ```
   world_x = (tip_x - 폭/2) / PPU * 0.85
   world_y = -0.46 + (높이 - tip_y) / PPU * 0.85
   ```

7. 슬롯에 올려 **124px 판별**과 방향 전환, 총구 화염 위치를 확인한다

> **함정:** 스프라이트로 전환하기 **전에** 텍스처 높이를 읽으면 안 된다. NPOT 스케일이 407×749를 512×512로 눌러 놓은 값이 나와 PPU가 어긋난다. `npotScale = None`을 먼저 걸고 다시 읽는다.

## 8. 생성 시 주의

- **투명 배경 PNG로 뽑을 것.** 채팅 화면에서는 흰 배경과 투명이 똑같이 보이므로 알파를 파일로 확인한다.
- **동일 헤더를 고정**하고 이전 유닛 시트를 레퍼런스로 첨부해 13종 간 톤을 맞춘다.
- **가로로 긴 비율보다 세로 2:3**이 낫다. 2×2 격자에 전신 캐릭터가 들어가야 한다.
- 유닛은 124px, 적은 73px로 축소해 **판별되는지 확인한 뒤** 다음 시트로 넘어간다.
