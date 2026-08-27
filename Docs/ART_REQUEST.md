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

유닛 13종 모두 이 헤더를 그대로 쓰고 **`CHARACTER`와 `POSE` 두 블록을 갈아 끼운다** — 무기 수와 사격 자세가 유닛마다 다르다. **스카우트 시트로 검증된 프롬프트다.**

**성별은 헤더를 두 벌로 나누지 않는다. 갈아 끼우는 곳은 2줄뿐이다.** 두 벌로 나누면 나머지 줄이 갈라져 한쪽만 고치는 사고가 난다.

| # | 위치 | 남성 | 여성 |
|---|---|---|---|
| 1 | 첫 줄 | `bishounen character design` | `bishoujo character design` |
| 2 | 후면 칸 설명 | `we see his back and the back of his head` | `we see her back and the back of her head` |

`POSE` 블록의 `he`/`she`는 어차피 유닛별로 갈아 끼우므로 스위치가 아니다. 배분은 §4-2단계 표에, 배분 규칙은 §3에 있다.

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

POSE - <유닛별로 갈아 끼우는 블록. 아래는 무기 1정 기준 골격이다>
every cell shows the FIRING pose aimed in that cell's direction:
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
| **하의 색을 반드시 적을 것** | 안 적으면 군단 기본값인 **흰 바지**가 나온다. 배터리 첫 시트가 그래서 **스카우트와 하체 색 블록이 똑같아졌고**, 캐릭터에 개성이 없다는 지적을 받았다. `===` 블록으로 승격하고 **`does NOT wear white trousers`** 로 명시적 부정까지 넣어야 먹혔다. 레퍼런스 시트에 흰 바지가 여럿이라 기본값의 인력이 세다 |
| **무기가 머리에 가리지 않게 할 것** | 배터리 첫 시트는 위쪽 3문 중 **가운데가 머리에 완전히 가려** 정면에서 `2+2`로 보였다. 개수는 맞았으므로 개수를 더 강조해도 소용없다 — **가림 자체를 금지**해야 한다(`sits ENTIRELY ABOVE HIS HEAD`) |
| `compact braced stance, NOT spread wide` | 다리를 넓게 벌리면 칸 안에서 캐릭터가 작아진다. **한 줄로 두면 무시된다** — 건슬링어 남성 시트가 다리를 벌리고 나왔고, 같은 내용을 `LEGS` **독립 문단**으로 빼자 여성 시트는 발이 모여 나왔다. 개수·경계처럼 꼭 지켜야 할 것은 문단으로 뺄 것 |

---

## 3. 생성 시 지켜야 할 규칙

- **구조 숫자를 실루엣으로 읽을 수 있어야 한다.** 2 / 2+2 / 3 / 3+2 / 4 (§10.2). M11의 최대 리스크다.
- **개수를 세는 방식만으로는 이 해상도에서 구조가 안 읽힌다.** 포신 하나가 화면에서 3~5px이라 2문이든 4문이든 뭉툭한 덩어리로 보인다. **무기의 배치로 윤곽을 가르는 축**을 함께 쓴다 — 좁은 세로 / 가로로 넓음 / 위로 솟음 / 부채꼴 / 사각 블록.
- **숫자를 직접 세어 확인한다.** 프롬프트에 총 개수를 숫자로 못 박아도 틀린다.
- **성급이 올라도 구조 숫자를 바꾸지 않는다** (§10.4). 페어는 영원히 2, 트리플은 3, 쿼드는 4다.
- **성급 오라 오버레이를 만들지 않는다.** 슬롯의 `★` 라벨이 성급을 담당한다 — §10.4의 *구조는 실루엣, 성급은 별* 두 축이 유지된다.
- **카드 문양은 유닛마다 고정 심볼 하나다** (§10.4). 한 유닛에 여러 무늬를 섞으면 **무늬가 능력과 연결된다는 오해**(§7에서 폐기한 카드 특성)를 아트가 되살린다.
  **배정 — 스카우트 `♠` / 건슬링어 `♥` / 트윈레인저 `♦` / 마크스맨 `♣` / 쿼드캐논 `♠` / 배터리 `♦`. 롱바렐만 예외로 네 무늬를 섞고, 레일거너는 하나로 통일한다** — DESIGN §10.4의 유일한 예외이고 그 둘을 가르는 축이다. 무늬는 4개뿐이라 겹치는데, **겹치는 둘은 구조 숫자로 갈리므로 문제가 되지 않는다**(스카우트 `1` 대 쿼드캐논 `4`). 구조 숫자가 없는 유닛은 성별·외형이 가장 먼 쪽과 묶는다. 남은 6종도 셀 구조가 없으니 자유롭게 고르되 **플러시 보머는 컨셉이 "같은 무늬 반복"이라 먼저 잡는다.** 붉은 무늬(`♥` `♦`)는 군복에 묻히므로 흰 외곽선을 지정한다.
- **성별도 무늬와 같은 장식 축이다. 역할·족보 세기와 상관시키지 않는다.** 상관시키면 §0이 정한 두 관계(족보 → 유닛, 유닛 → 역할) 밖에 셋째 관계가 생기고, 124px에서 성별을 읽는 단서(머리 모양·스커트)가 **구조 숫자를 읽어야 할 실루엣 대역과 정면으로 경쟁한다.** 배분은 §4-2단계 표에 있고 지켜야 할 조건은 둘이다.
  1. **패턴 5종이 각각 한 성별로 몰리지 않는다** — 현재 Rapid 1:2 / Heavy 2:1 / Multi 1:1 / Splash 1:2 / Pierce 1:1.
  2. **고빈도 4종(하이카드·원페어·투페어·트리플)을 2:2로 맞춘다.** 이쪽이 핵심이다. 이 넷이 손패의 92%(§3.3)이고 지원 소환도 이 넷에만 나오므로(§9.3) **보드를 실제로 채우는 것이 이 넷이다. 희귀 유닛을 아무리 반대 성별로 채워도 게임은 이 넷의 성별로 보인다.**

  **마운틴 킹만 이름이 성별을 고정한다.** 여성으로 가려면 이름을 바꿔야 하는데, 마운틴이 A-K-Q-J-10이라 *마운틴 퀸*도 수열 안에 있어 구조는 성립한다 — 바꾼다면 DESIGN §10.2를 먼저 고친다. 마크스맨을 남성에 둔 것도 `-man`이 붙은 유일한 이름이기 때문이다.

  **배분을 뒤집는 비용은 0이다.** 코드·데이터가 성별을 전혀 모른다. 확정 스펙이 아니라 작업 순서를 정하는 기준이므로, 시트가 한쪽 성별로 훨씬 잘 나오면 그 자리에서 바꾸고 다른 칸으로 균형을 되돌린다.
- **13종이 하나의 카드 군단으로 보여야 한다** (§10.2). 붉은 군복 + 흰·금 장식이 공통 언어다. 이전 유닛 시트를 스타일 레퍼런스로 첨부한다.
- **적은 틴트를 전제하지 않는다.** HP 표시가 밝기 곱셈에서 체력 바로 바뀌었으므로 적 스프라이트는 제 색을 그대로 쓴다.
- **소버린만 실루엣을 약간 크게 잡는다** (§10.4).

---

## 4. 단계별 요청 목록 (총 91장)

유닛 52(13종 × 4방향) + 적 28(7종 × 4방향) + 카드 7 + 배경·트랙 4.
유닛은 **13시트**, 적은 **7시트**만 생성하면 된다 — 한 시트가 4장을 낳는다.

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

### 2단계 — 나머지 유닛 7시트 (28장)

이전 시트를 스타일 레퍼런스로 물린다. **구조 숫자가 있는 여섯이 전부 끝났고**(`1` 스카우트 / `2` 건슬링어 / `2+2` 트윈레인저 / `3` 마크스맨 / `3+2` 배터리 / `4` 쿼드캐논) **구조가 없는 롱바렐·보머도 끝났다.**

> **스트레이트는 랜서(창)에서 롱바렐(장총)로 다시 기획했다.** 창 시트는 네 방향·좌표·수치까지 다 들어갔지만 **찌르기 궤적 이펙트가 총기와 훨씬 잘 맞아서** 갈아엎었다. 판정(직선 관통)과 수치는 그대로고 무기와 실루엣만 바뀐다. 자세한 근거는 [HISTORY.md](HISTORY.md).

**13종 전부 끝났다.** 개수 대신 컨셉으로 가른 계단(에이스)·레일(레일거너)·산(마운틴 킹)·원형 회전(템페스트)·왕좌(소버린)까지 모두 완료다. **색 블록은 계속 갈라야 한다** (§8).

| 우선 | 에셋 | 이름 | 구조 | 역할 | 성별 |
|---|---|---|---|---|---|
| — | `Unit_Scout` | 하이카드 스카우트 | 카드 1장, 권총 1정 | Rapid · **완료** | 남 |
| — | `Unit_Gunslinger` | 원페어 건슬링어 | **2** 쌍권총 | Rapid · **완료** | 여 |
| — | `Unit_TwinRanger` | 투페어 트윈레인저 | **2+2** 양팔 2연장 | Multi · **완료** | 여 |
| — | `Unit_Marksman` | 트리플 마크스맨 | **3** 3총구 | Heavy · **완료** | 남 |
| — | `Unit_Battery` | 풀하우스 배터리 | **3+2** 주포3 + 보조포2 | Multi · **완료** (Splash에서 변경, [HISTORY](HISTORY.md) 참고) | 남 |
| — | `Unit_QuadCannon` | 포카드 쿼드캐논 | **4** 4연장 중포 | Heavy · **완료** | 여 |
| — | `Unit_LongBarrel` | 스트레이트 롱바렐 | **무늬 섞인** 카드 5장이 박힌 아주 긴 총열 1정 | Pierce · **완료** | 남 |
| — | `Unit_Ace` | 백스트레이트 에이스 | A→2→3→4→5 계단 | Rapid · **완료** | 여 |
| — | `Unit_King` | 마운틴 킹 | ▲ 산 왕관, 꼭대기에 A | Heavy · **완료** | 남 |
| — | `Unit_Bomber` | 플러시 보머 | 같은 무늬 반복 | Splash · **완료** | 여 |
| — | `Unit_Tempest` | 백스트레이트 플러시 템페스트 | 같은 무늬 A-5 원형 회전 | Multi · **완료** | 남 |
| — | `Unit_RailGunner` | 스트레이트 플러시 레일거너 | 같은 무늬 5장 일렬 = 레일 | Pierce · **완료** | 여 |
| — | `Unit_Sovereign` | 로열 스트레이트 플러시 소버린 | 10-J-Q-K-A 왕좌, 왕관 | Multi · **완료** (Splash에서 변경, [HISTORY](HISTORY.md) 참고) | 여 |

**남 6 / 여 7.** 배분 규칙과 근거는 §3에 있다. 빈도 구간별로는 고빈도 4종 2:2, 중간 4종(백스트레이트·스트레이트·마운틴·플러시) 2:2, 희귀 5종 남 2 / 여 3이다.

> **개수 축만으로는 안 갈린다 — 두 번 확인했다.** 건슬링어 정면 종횡비가 0.485로 스카우트 0.460과 사실상 같았고, 트윈레인저 측면도 0.572로 건슬링어 0.599와 같았다. **두 번 다 실제로 가른 것은 색 블록이었다** — 검은 머리+흰 바지 / 금발 트윈테일+검은 타이츠 / 적갈색 단발+흰 부츠+흰 건틀릿. 남은 10종도 **구조 숫자와 색·머리·복장을 함께** 갈라야 한다.
>
> **덩어리 개수와 덩어리 사이 간격이 진짜 축이다.** 트윈레인저(`2+2`)를 쿼드캐논(`4`)과 가르는 것은 총열 수가 아니라 **두 덩어리 사이의 빈 공간**이었다. `3+2` 배터리도 같은 방식으로 잡는다.

### 3단계 — 적 28장 (7종 × 4방향)

**타입은 5종이지만 에셋은 7개이고 개별로 만든다.** 73px에서 서로 구분되는 것이 조건이다. **컨셉은 몬스터/크리처** — 유닛 13종이 "붉은 군복 + 흰·금 장식"의 카드 군단이라면, 적은 "어두운 그림자빛 몸통 + 종류별 글로우 악센트"로 하나의 무리로 묶는다. 글로우 색은 기존 `placeholderColor`를 그대로 물려받는다(그런트=붉은빛 / 러너=초록 / 스웜=주황 / 브루트=보라 / 미니보스=마젠타 / 보스=금빛 / 파이널보스=진홍) — 코드는 더 이상 틴트를 안 쓰지만(§10.5) 컨셉 연속성은 유지한다.

| 에셋 | 타입 | HP | 특성 |
|---|---|---|---|
| `Enemy_Grunt` | Normal | 60 | 기준 · **완료** |
| `Enemy_Runner` | Runner | 82 | 빠르다 (속도 4.2) · **완료** |
| `Enemy_Swarm` | Swarm | 50 | 작고 많다 · **완료** |
| `Enemy_Brute` | Tank | 690 | 크고 느리다 (속도 1.3) · **완료** |
| `Enemy_MiniBoss` | Boss | 550 | W5 전용 · displayName `Wraith`(2026-08-24, 기존 `Mini Boss`에서 갈아 넣음 — 화면에는 안 뜨는 값이지만 유닛처럼 적도 타입명 대신 고유 이름을 갖도록) · **완료** |
| `Enemy_Boss` | Boss | 1300 | W10·W15·W20·W30·W40 · displayName `Behemoth`(2026-08-24, 기존 `Boss`에서 갈아 넣음 — 레이스와 같은 이유로 화면에는 안 뜨지만 고유 이름 부여) · **완료** |
| `Enemy_FinalBoss` | Boss | 4200 | W50 최종 · displayName `Abaddon`(2026-08-27, 기존 `Final Boss`에서 갈아 넣음 — 레이스·베히모스와 같은 이유) · **완료** |

> **적 스케일은 해결했다 (2026-08-23).** `EnemyDefinition`에 `visualScale` 필드를 추가하고(기존 7개 에셋에 값을 명시적으로 써 넣었다 — 안 그러면 새 필드가 0으로 읽히는 함정이 있다) `CombatScreen.CreateView`가 `BodyScale × visualScale`로 그린다. 그런트 1.0 기준, 스웜 0.75(작고 많다), 브루트 1.35(크고 느리다), 미니보스 1.5 → 보스 1.8 → 파이널보스 2.3으로 보스 3종이 단계적으로 커진다.
>
> **4방향으로 확정했다 (2026-08-23).** 그런트 파일럿을 1방향으로 먼저 뽑아 봤는데 "정면으로 카메라를 향해 걸어오는" 포즈라 방향성이 뚜렷했고, 위/아래 이동 시 어색할 위험이 커서 유닛과 동일하게 4방향 턴어라운드로 가기로 했다. 다만 적은 조준이 없으므로 **방향의 의미가 유닛과 다르다** — 유닛은 조준 방향, 적은 **트랙 이동 방향**이다. 트랙은 4구간(상단→우향 / 우측→하향 / 하단→좌향 / 좌측→상향)뿐이라 방향 판정이 유닛(조준 대상 위치 기반)보다 오히려 결정적이다. `GridBoard.TrackDirection(progress)`가 진행도로 구간을 판정해 `AimDirection`을 돌려주고, `CombatScreen`이 매 프레임 `EnemyDefinition.ArtFor(direction)`로 스프라이트를 고른다. 유닛과 같은 2×2 그리드 시트 방식(한 이미지 안에서 동일 개체 보장)을 그대로 쓴다 — 자르기·트림·임포트 절차도 §7과 동일하다.

**그런트 파일럿 — 완료.** `Art/Enemies/Grunt/`에 있다.

| 검증 | 결과 |
|---|---|
| 2×2 분리 | 세로 간격 166px(560~726), 가로 간격 18px(625~642) — 둘 다 여유 있게 분리, 정중앙이 아닌 실제 간격 중점(x=643, y=633)에서 잘랐다 |
| 알파 | 완전 투명 배경(69.8%), 알파 0~255 전 구간 사용 |
| 방향 판별 | 후면(TOP-RIGHT)은 눈이 안 보이고 정면(TOP-LEFT)은 두 눈 다 보임 — 정상. 측면 좌우가 실제로 뒤바뀌지 않았는지 **눈(글로우) 위치를 픽셀로 측정**해 확인함 — BOTTOM-LEFT 눈 x=38%(왼쪽에 치우침, 왼쪽을 봄) / BOTTOM-RIGHT 눈 x=70%(오른쪽을 봄) — 라벨과 일치, 마크스맨 때와 달리 뒤바뀌지 않았다 |
| 높이 편차 | 정면·후면(608·604px) 대비 측면(551·557px)이 약 9% 낮다 — 자르기 실수가 아니라 **측면만 웅크린 보행 자세라 포즈 차이로 판단**, 정규화하지 않고 공통 캔버스(422×608)에 바닥 정렬만 맞췄다. 기존 유닛 최대 편차(배터리 4.3%)보다는 크므로 인게임에서 다시 볼 것 |
| 73px 판별 | 4방향 전부 축소해 확인 — 실루엣·글로우 색 모두 뚜렷하게 구분됨 |
| 실플레이 | W1(그런트 전용)에서 실제로 배치해 확인 — 트랙 우측 구간에서 정면, 하단 구간에서 좌측면으로 정확히 전환됨. 체력 바 표시 정상 |
| 임포트 | `Sprite/Single`, `npotScale=None`, `PPU=608`(공통 캔버스 높이), `Bilinear`, 밉맵 켬, 피벗 `BottomCenter`, `Alpha Is Transparency` 켬 — 메타에서 `alignment: 7` 확인 |

EditMode 219/219, 콘솔 0건.

**러너 — 완료.** 그런트 기준선에서 "더 마르고 낮은 자세, 전력 질주"로 명시적으로 갈랐다. `Art/Enemies/Runner/`. 간격 중점(x=656/y=640)에서 분리, 완전 투명 확인. **눈 위치 %만으로는 방향 판정이 애매했다**(좌 46% / 우 57% — 그런트 때(38%/70%)보다 중심에 가까움, 전력 질주 포즈라 뻗은 팔다리가 bbox를 넓혀 눈 위치 비율이 희석됐다) — **뒷다리(트레일링 레그)가 뻗은 방향과 앞다리·팔이 뻗은 방향을 직접 보고 판정**해 라벨과 일치함을 확인했다(왼쪽 셀=뒷다리 오른쪽으로 트레일링→왼쪽으로 전진, 오른쪽 셀=반대). **눈 위치 비율 하나만 믿으면 안 되는 사례**로 남겨둔다 — 역동적인 포즈일수록 사지 자세를 직접 봐야 한다. 공통 캔버스 560×595, `PPU=595`로 임포트. 실플레이 스크린샷은 생략(방향 선택 로직 자체는 그런트에서 이미 검증됨, 남은 리스크는 에셋 연결뿐이라 리플렉션 확인으로 대체).

**스웜 — 완료, 1차 시도는 반려.** 1차 시도는 "더 둥글고 통통하게"처럼 비교급 형용사로만 그런트와 갈랐는데, 받은 결과가 **그런트를 네 발로 엎드리게 한 것과 사실상 같았다**(같은 팔다리 비율, 같은 귀, 같은 불꽃 머리, 같은 붉은 잉걸불 글로우 — 포즈만 바뀜). 사용자가 직접 보고 "그런트랑 너무 비슷하다"고 지적했다. **원인은 비교급 형용사가 힘이 약하다는, 이 프로젝트에 이미 여러 번 기록된 함정과 같았다** — "더 크다/작다"류 상대 지시보다 **구조 자체를 다른 것으로 못박아야** 먹힌다. 2차 프롬프트에서 두 가지를 구조적으로 바꿨다.
- **몸 형태를 완전히 다른 종으로 지정.** "그런트와 같은 직립 임프 실루엣이 아니다"라고 명시적으로 금지하고, 네 발 짐승(자칼/하이에나 새끼)형으로 — 뾰족 귀 대신 눕은 둥근 귀, 불꽃 머리 대신 등줄기 짧은 털, 꼬리를 추가했다.
- **글로우 색을 그런트와 직접 대조.** "무리의 나머지가 쓰는 붉은 잉걸불 색이 아니라 더 노란 호박색/황금색"이라고 명시.

결과는 완전히 다른 실루엣(작은 네 발 짐승, 황금빛 글로우)으로 나왔고 55px 축소본에서도 그런트·러너(둘 다 직립 이족)와 즉시 구분됐다. `Art/Enemies/Swarm/`. 간격 중점(x=634/y=701)에서 분리, 완전 투명 확인. **방향 판정은 이번에도 눈 위치 비율이 안 맞았다**(글로우가 등줄기·다리에 고르게 퍼져 있어 좌 51%/우 50%로 무의미) — **주둥이(얼굴)와 꼬리가 어느 쪽에 있는지 직접 봐서 판정**했고 라벨과 일치했다. **정면·후면(495·505px) 대비 측면(303·309px)이 40%나 낮다** — 네발짐승이 정면에서는 꼬리가 위로 말려 올라가 키가 커지고 측면에서는 낮게 엎드린 실루엣이라 나온 자연스러운 차이로 판단, 정규화하지 않고 공통 캔버스(514×505)에 바닥 정렬만 했다 — 그런트·러너보다 훨씬 큰 편차라 인게임에서 측면 전환 시 확 작아 보이면 다시 볼 것. 실플레이 확인은 DevMode로 W11까지 점프해 시도했으나 자동 진행 중 보스 특전 화면과 겹쳐 스크린샷을 못 건졌다 — 리플렉션 확인(`HasArt=True`, 4방향 전부 연결)으로 대체했다.

**브루트 — 완료, 1차 시도로 통과.** 스웜의 교훈(비교급 형용사 대신 구조를 못박을 것)을 처음부터 프롬프트에 적용해서인지 반려 없이 통과했다. "이 무리 대부분은 날렵한 임프 계열이다 → 이 녀석은 그게 아니라 어깨가 골반보다 훨씬 넓은 육중한 오우거형"이라고 구조를 명시하고, 불꽃 머리·뾰족귀를 금지하고 짧은 뭉툭귀·상체에 파묻힌 작은 머리로 못박았다. 결과는 고릴라형 실루엣에 보라-바이올렛 글로우로, 다른 3종과 즉시 구분된다. `Art/Enemies/Brute/`. 세로 간격 39px(619~658)·가로 간격 23px(620~643)에서 분리, 간격 중점(x=639, y=632)에서 잘랐다. 완전 투명 배경(44.1%), 알파 0~255 전 구간. **방향 판정은 이번엔 눈 위치 비율 없이 머리(얼굴) 위치를 직접 봐서 바로 판정됐다** — 이족 직립 포즈라 그런트 때처럼 명확했다(측면에서 머리가 진행 방향 쪽에 붙어 있어 좌/우 라벨과 바로 일치 확인). 정면·후면(607·599px) 대비 측면(560·574px)이 약 7.7% 낮다 — 그런트(9%)와 비슷한 수준, 포즈 차이로 판단해 정규화하지 않고 공통 캔버스(582×607)에 바닥 정렬만 했다. `visualScale=1.35`라 판별 기준을 73px→99px로 올려 축소본을 확인했고, 4방향 모두 실루엣·글로우 색이 뚜렷하게 구분됐다. 실플레이 스크린샷은 생략(방향 선택 로직은 그런트에서 이미 검증됨, 남은 리스크는 에셋 연결뿐이라 리플렉션 확인으로 대체) — `HasArt=True`, `VisualScale=1.35`, 4방향 전부 연결 확인.

**레이스(구 미니보스) — 완료, 1차 시도로 통과.** 잡류 4종(직립 임프·스프린트 임프·네발짐승·오우거형)은 전부 다리로 서 있어서, 첫 보스급인 이 녀석은 **다리를 아예 없애고** 그림자 촉수 여러 가닥으로 늘어진 채 공중에 떠서 이동하는 것으로 구조를 못박았다. 눈도 잡류의 "양쪽 두 눈"이 아니라 후드 중앙의 커다란 눈 하나로 바꾸고 뿔 한 쌍을 얹었다. 결과는 다리 없는 부유형 실루엣에 마젠타-핑크 글로우로, 잡류 4종과 확연히 갈린다. `Art/Enemies/Wraith/`. 가로 간격은 90px(585~675)로 뚜렷했지만 **세로는 처음으로 완전한 제로 갭이 없었다** — 촉수가 워낙 길어 상하 셀 경계(y≈655)를 살짝 스치는 가닥이 있었다(전체 폭 1254px 중 71px만 걸침). 밀도 최소점(y=655)에서 잘랐는데, **자르고 나니 Down·Up·Right 세 방향에서 인접 셀의 촉수 끝이 본체와 분리된 작은 조각으로 붙어 나왔다**(422~533px 크기의 고립된 덩어리). 연결 성분 분석(BFS)으로 가장 큰 덩어리 하나만 남기고 나머지를 전부 지운 뒤 재트림·재조립했다 — **셀 경계가 완전히 비지 않는 유기적 형태(긴 촉수·머리카락 등)를 자를 때는 육안 확인만으로는 부족하고, 연결 성분 검사로 고립된 조각이 없는지 확인해야 한다**는 게 이번에 새로 얻은 교훈이다. 정면·후면(642·639px) 대비 측면(557·585px)이 약 13.2% 낮다 — 부유 자세상 측면에서 촉수가 옆으로 더 퍼지는 포즈 차이로 판단해 정규화하지 않았다. 공통 캔버스 493×642, `PPU=642`. 방향 판정은 머리(후드·눈) 위치를 직접 봐서 바로 확인됐다(라벨과 일치, 브루트와 동일하게 비율 측정 불필요). `visualScale=1.5`라 판별 기준을 110px로 올려 확인했고, 다리 없는 실루엣이 4방향 모두 즉시 구분됐다. `displayName`도 이번에 `Mini Boss`에서 `Wraith`로 바꿔 넣었다(화면에는 안 뜨는 값). 실플레이 스크린샷은 생략, 리플렉션으로 `HasArt=True`, `VisualScale=1.5`, 4방향 전부 연결 확인.

**베히모스(보스) — 완료.** 잡류 4종은 이족·사족으로 걷고 레이스는 다리 없이 떠 있으므로, 두 번째 보스는 **다리 여덟 개로 낮게 깔려 기어오는 전갈형 갑각류**로 구조를 못박아 앞선 5종과 갈랐다. `placeholderColor`(황금)를 물려받아 글로우는 주황-호박색 용암 균열로 지정 — 스웜의 황금 글로우와 겹칠 위험이 있었지만 스웜은 작은 네발짐승이라 실루엣만으로 즉시 갈린다. 결과는 등딱지 위로 말려 올라간 꼬리와 벌린 집게가 있는 폭이 넓은 실루엣으로, 지금까지 나온 어떤 적보다 가로로 넓다. `Art/Enemies/Behemoth/`. 세로 간격 22px(616~637)·가로 간격 61px(599~659)로 **둘 다 완전한 제로 갭**이라 레이스에서 겪은 촉수 문제는 없었지만, 꼬리·집게가 뻗은 형태라 레이스의 교훈대로 4방향 전부 연결 성분 검사를 돌려 확인했다 — **네 방향 모두 덩어리가 정확히 1개**, 고립된 조각 없음. 완전 투명 배경(시트 기준 불투명 36.8%), 알파 0~255 전 구간. 방향 판정은 눈(집게 사이 세 점 글로우)과 집게가 향한 쪽을 직접 봐서 바로 확인됐다 — 좌/우는 머리가 각각 왼쪽·오른쪽 끝에 붙어 있고, Up은 눈이 전혀 안 보이며 꼬리가 관측자 쪽으로 말려 있어 Down과 명확히 갈린다(라벨과 일치). 정면·후면(600·603px) 대비 측면(579·581px)이 약 3.7%만 낮다 — 지금까지 적 6종 중 가장 작은 편차로, 낮게 깔린 자세라 방향에 따른 키 변화가 거의 없다. 공통 캔버스 579×603, `PPU=603`. `visualScale=1.8`이라 판별 기준을 130px로 올려 축소본을 확인했고, 4방향 모두 실루엣이 뚜렷하게 구분됐다. 실플레이 스크린샷은 생략, 리플렉션으로 `HasArt=True`, `VisualScale=1.8`, 4방향 전부 연결 확인. EditMode 219/219, 콘솔 0건.

**아바돈(파이널보스) — 완료. 적 7종 전부 끝. 시트 생성·분할·공통 캔버스 정렬은 앞 세션에서 마쳤고 이번에 임포트부터 이어받았다.** 앞선 6종은 이족(그런트·러너)·사족(스웜)·오우거(브루트)·다리 없는 부유형(레이스)·전갈형(베히모스)이라 전부 지면에 붙어 있으므로, 최종 보스는 **크고 넓은 박쥐형 날개 한 쌍 + 뿔 머리**로 구조를 못박아 갈랐다 — 지금까지 유일한 유익종이다. `placeholderColor`(진홍)를 물려받아 글로우는 진홍-빨강 용암 균열로 지정, 몸통은 어두운 자흑색. 결과는 날개를 펼친 실루엣에 진홍 글로우로, 베히모스의 주황-호박과도 색으로 즉시 갈린다. `Art/Enemies/Abaddon/`. 4장 모두 공통 캔버스 458×642, `PPU=642`(레이스와 동일 높이). **분할 검증(이어받은 결과 확인):** 연결 성분 검사 4방향 전부 **본체 덩어리 정확히 1개**(133k~148k px), 나머지는 알파 17~26의 1~4px 먼지뿐(방향당 6~15px) — 레이스 때 같은 고립된 사지 조각은 없다. 배경은 알파 임계 16 기준 완전 투명. 방향 판정은 얼굴·가슴 코어 글로우 위치를 직접 봐서 확인 — Down은 정면 얼굴 + 두 눈 + 가슴 코어, Up은 등줄기 능선만 보이고 얼굴·코어 없음, 좌/우는 머리가 각각 왼쪽(bright centroid x≈38~43%)·오른쪽(x≈56~70%)에 붙어 라벨과 일치(뒤바뀜 없음). 정면·후면(634·642px) 대비 측면(둘 다 610px)이 약 5.0% 낮다 — 그런트(9%)·브루트(7.7%)보다 작은 편차로 포즈 차이로 보고 정규화하지 않았다. `visualScale=2.3`이라 판별 기준을 168px로 올려 4방향 축소본과 기존 6종을 나란히 확인 — 날개 실루엣으로 즉시 구분됐다. `displayName`도 `Final Boss` → `Abaddon`으로 갈아 넣었다(레이스·베히모스와 동일, 화면에는 안 뜨는 값). 임포트 설정은 메타에서 `alignment: 7`·`nPOTScale: 0`·`spritePixelsToUnits: 642` 확인, 스프라이트 `bounds.size.y == 1.000`. 실플레이 스크린샷은 생략, 리플렉션으로 `displayName=Abaddon`, `HasArt=True`, `VisualScale=2.3`, `ArtFor()` 4방향 전부 연결 확인. EditMode 219/219, 콘솔 0건.

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

### 이펙트 — 여섯 장 모두 **완료**

`Art/VFX/`에 있다. **공용 2장이 기본이고 유닛이 따로 지정하면 그것을 쓴다** — 추상 형태라 13종이 나눠 쓸 수 있어서, 총으로 안 읽히는 무기에만 전용 장을 붙인다.

| 파일 | 쓰임 | 수명 | 상태 |
|---|---|---|---|
| `MuzzleEffect.png` | 총구 화염. 조준 방향으로 밀어 띄운다 | 0.06초 | **완료** |
| `HitEffect.png` | 타격 스파크. 맞은 지점에 띄운다 | 0.12초 | **완료** |
| `BeamMuzzleEffect.png` | 총구 섬광. 롱바렐 전용 (차가운 은백 8방) | 0.06초 | **완료** |
| `PierceHitEffect.png` | 관통 자국. 롱바렐 전용 | 0.12초 | **완료** |
| `PierceTrailEffect.png` | **빔 궤적.** 총구에서 겨눈 적까지 한 줄로 뻗는다 | 0.18초 | **완료** |
| `SplashEffect.png` | **폭발.** 착탄 지점에 `splashRadius` 지름으로 그린다 | 0.20초 | **완료** (3차 시도, [상세](#splash-폭발-전용-프롬프트--splasheffectpng)) |

- **네 장은 방사 대칭이고 `PierceTrailEffect`만 예외다.** 방사 대칭을 요구한 이유는 뷰가 이펙트를 위치만 옮겨 찍기 때문인데, 궤적은 **반대로 방향이 있어야 하고 코드가 회전시킨다.** 대신 지켜야 할 것이 다르다.
  - **상하 대칭일 것.** 어느 각도로도 회전하므로 위아래가 다르면 왼쪽을 향할 때 뒤집혀 보인다. 받은 장은 뒤집어 겹친 차이 6.6%다.
  - **길이 방향으로 균일할 것.** 유닛과 적의 거리에 따라 **1.4~2.8배**로 늘어난다. 세로 단면이 자리마다 다르면 늘릴 때 뭉개진다. 받은 장은 열별 편차 1.4%다.
  - **좌우 끝이 꽉 찰 것.** 끝에서 알파가 죽으면 잘린 티가 난다. 받은 장은 가운데 대비 96.3% / 97.7%다.
  - **끝을 뾰족하게 만들지 말 것.** 길이가 늘 바뀌므로 끝 모양이 거리마다 달라 보인다. 날카로움은 끝이 아니라 **단면**(가는 흰 코어)으로 낸다. 양 끝은 유닛과 `PierceHitEffect` 폭발이 덮는다.
  > **판정 모양과 연출 모양은 다르다.** 처음에는 §10.1의 Pierce 판정 그대로 **꿰뚫은 트랙 구간**을 따라 그렸다. 판정과는 일치하지만 화면에서는 *적들 사이에 선이 하나 생긴 것*으로 보일 뿐 유닛이 찌른 것으로 안 읽혔다. **연출은 연출 기준으로 정한다.**
- **총구 이펙트와 타격 이펙트는 얹히는 바탕이 다르다.** 총구는 유닛과 어두운 배경 위에 뜨지만 **타격은 적 위에 뜬다.** `PierceHitEffect` 1차가 무기 색(흰 강철 + 붉은 줄)을 따라 진홍으로 나왔다가 붉은 적(`rgb(216,81,76)`)에 묻혔다 — 공용 타격 대비 3.1배 약했다. 금·호박빛으로 뒤집어 닫았다. **타격 쪽 색은 무기가 아니라 적을 보고 고른다.**

- **캐릭터 프롬프트를 쓰지 않는다.** 이펙트에 두꺼운 검은 아웃라인을 넣으면 스티커처럼 보인다.
- **방사 대칭으로 뽑는다.** 그래야 방향별로 4장을 만들 필요가 없다.
- **밝은 색만 쓰고 어두운 픽셀을 넣지 않는다.** 가산 합성처럼 보이게 하려면 필요하다 (받은 두 장은 어두운 픽셀 0~0.2%였다).
- **총구 위치는 유닛마다 방향별 목록으로 넣는다 — `muzzlesSide`(오른쪽 기준) / `muzzlesFacing` / `muzzlesBack`.** 그 방향에서 보이는 총구를 **전부** 적는다. `x`·`y`는 슬롯 로컬이다. **좌우만은 대칭으로 묶는다** — 실측 차이가 1.7~3.6px이라 나눌 값어치가 없다. 방향을 가른 이유는 DESIGN §10.4에 있다.
  > **묶을 때 한쪽 실측값을 그대로 쓰지 말고 양쪽의 중점을 쓸 것.** 보머는 **좌향 발사기가 우향보다 27px 짧게 그려져** 좌우 차가 화면 5.5px이었다. 우향 값을 그대로 넣으면 좌향만 4.9px 어긋난다. 중점(0.351)이면 양쪽 2.8px로 기존 6종 대역에 들어온다. **좌우를 따로 재야 알 수 있다** — 한쪽만 재고 뒤집으면 이 차이가 보이지 않는다.
- **발사 방식을 함께 정한다.** `muzzlesFireTogether`(한 발에 전부) / 끄면 하나씩, 그때 `muzzleRandomOrder`로 순서대로냐 무작위냐를 고른다. 무기 수로 유추하지 말 것 — §10.2의 공격 방식이 정한다(DESIGN §10.4의 표). 지금은 **트윈레인저만 동시, 배터리만 무작위**다.
  > **정면·후면도 반드시 따로 잰다.** 한때 주 총구의 x를 뒤집어 쓰는 근사로 넘어갔는데, 마크스맨에서 후면이 21.6px 어긋나 뒤통수에 화염이 떴다. 찾는 김에 재보니 **이미 넣은 스카우트도 후면이 23px 틀려 있었다** — 세 시트를 넘기는 동안 아무도 못 봤다.
  > 처음에 가슴 높이(+0.05)로 눈대중해서 **총구보다 30px 아래**에 떴다. 반드시 잴 것 — 재는 법은 §7에 있다.
- **넣은 유닛의 총구 수와 발사 방식**

  | 유닛 | 측면 | 정면 | 후면 | 발사 |
  |---|---|---|---|---|
  | 하이카드 스카우트 | 1 | 1 | 1 | 순서대로 |
  | 원페어 건슬링어 | 2 | 2 | 2 | 순서대로 |
  | 투페어 트윈레인저 | 2 | 2 | 2 | **동시** |
  | 트리플 마크스맨 | 1 | 1 | 1 | 순서대로 |
  | 포카드 쿼드캐논 | 1 | 1 | 1 | 순서대로 |
  | 풀하우스 배터리 | **5** | **5** | **5** | **무작위** |
  | 스트레이트 롱바렐 | 1 | 1 | 1 | 순서대로 |
  | 플러시 보머 | 1 | 1 | 1 | 순서대로 |

  > **모델이 세 번 넓어졌다.** ① 방향마다 무기 자리가 달라 방향을 갈랐고(마크스맨 후면 21.6px / 스카우트 후면 23px), ② 무기가 둘인 유닛 때문에 좌표를 둘로 늘렸고, ③ **배터리는 정면·후면에서 x가 둘 다 0이라 좌우 반전으로는 두 점이 한 자리로 붕괴해**(51px) 결국 목록이 됐다. 남은 7종에서 또 늘리지 않으려면 **처음부터 그 방향에 보이는 총구를 전부 세어 적을 것.**
- **날아가는 총알은 만들지 않는다.** §5.5가 즉시 히트로 못 박았다. 비행 시간이 생기면 규칙과 어긋난다.

### 롱바렐 전용 3장 프롬프트

**갈릴 축을 먼저 정한다.** 공용 2장과 안 갈리면 만들 이유가 없는데, 둘 다 *흰 코어 + 방사*라는 골격은 같아야 한다(같은 게임이다). 그래서 **골격은 두고 두 축만 뒤집는다.**

| 축 | 공용 2장 | 롱바렐 2장 | 왜 |
|---|---|---|---|
| 색 | 주황·노랑 **불** | 은백·금, 끝만 진홍 **강철** | 화약 폭발이 아니라 차가운 금속광이다. 무기가 흰 강철 + 붉은 줄 + 금 장식이라 거기서 그대로 온다 |
| 형태 | 폭발 구름 + 잔가지 다발 | **가늘고 긴 바늘 몇 개** | 41~104px에서 0.06~0.12초 보인다. 잔가지는 그 크기에서 뭉개져 덩어리가 되므로 **큰 형태만** 남긴다 |
| 고리 | `HitEffect`에 금색 원 | **원 금지** | 원은 범위(Splash)로 읽힌다. Pierce는 직선 관통이라 정반대다 |

**수명·크기는 코드가 정해져 있다.** 총구 섬광은 `0.40 → 0.48` 월드(화면 **41 → 70px**) 0.06초, 관통 자국은 `0.33 → 0.715` 월드(화면 **48 → 104px**) 0.12초다. 캔버스 1254²는 축소 여유일 뿐 디테일을 넣으라는 뜻이 아니다 — 유닛 판별 크기와 같은 이야기다.

#### 공통 헤더 (두 장에 그대로 쓴다)

```
A single game VFX sprite on a FULLY TRANSPARENT background.
One centred radial burst. Japanese anime / JRPG game effect look,
crisp and graphic, NOT photographic, NOT a 3D render.

=== RADIAL SYMMETRY IS MANDATORY ===
The shape must look the SAME after being rotated 90 degrees.
It has NO up, NO down, NO left, NO right.
NO direction of travel, NO motion trail, NO comet tail, NO arrow,
NO single dominant spike that is longer than the others.
The bright core sits EXACTLY at the centre of the square canvas.
============================

=== GLOWING ONLY - NO DARK PIXELS ===
Every pixel is either fully transparent or glowing light.
NO black, NO dark grey, NO brown, NO shadow, NO dark outline.
NO thick black cartoon outline - it must not look like a sticker.
============================

Square canvas 1254 x 1254. The shape fills most of the canvas.
Read at THUMBNAIL SIZE: big simple shapes, high contrast.
NO fine texture, NO tiny specks, NO scattered debris.

NO background art, NO ground, NO frame, NO panel,
NO character, NO hand, NO weapon, NO spear, NO sword,
NO text, NO numbers, NO letters, NO watermark, NO signature.
```

#### `BeamMuzzleEffect.png` — 총구 섬광

```
SHAPE - a cold STEEL GLINT flashing at the point of a spear.
A very small, very bright white-hot pinpoint at the centre, and
EIGHT long, straight, razor-thin needle rays radiating from it
in eight evenly spaced directions, like a lens star-flare on
polished steel. Between them, sixteen much shorter thin rays.
The rays are STRAIGHT and SHARP and taper to fine points.
Keep it CLEAN, SPARSE and OPEN - lots of transparent space between rays.

COLOUR - white-hot core, silver-white rays, thin pale gold edging
along the eight long rays, and a faint crimson tint ONLY at the
outermost tips.

This is COLD polished-steel light, NOT fire.
NO orange, NO flame, NO fire, NO smoke, NO embers,
NO explosion cloud, NO billowing shape.
```

#### `PierceHitEffect.png` — 관통 자국

```
SHAPE - a PUNCTURE. Something narrow and sharp has been driven
clean through, and the wound splits outward from one point.
An intensely bright small white puncture point at the centre.
FOUR long straight needle-thin spikes forming an even cross,
plus FOUR shorter spikes between them, all driven outward from
that one point like split cracks. Every spike is narrow and
tapers to a sharp point.
The overall silhouette is a SPIKY STAR, NOT a round ball.

COLOUR - white puncture core, crimson red spikes,
pale gold highlights along the four long spikes.

NO circular ring, NO round halo, NO ripple ring, NO expanding
circle, NO round cloud, NO smoke puff, NO ball of light.
A ring reads as an area blast - this attack is a straight-line pierce.
NO orange, NO flame, NO fire.
```

#### 받으면 잴 것

눈으로 보지 말고 잰다. 앞선 두 장이 통과한 값이 기준이다.

| 항목 | 기준 | 받은 2장 |
|---|---|---|
| 알파 | 투명 배경일 것. **어두운 픽셀 0.2% 이하** (공용 2장이 0.01·1.87%) | 0.02% / 0.02% |
| 방사 대칭 | 90·180도 돌려 겹쳐 **차이가 작을 것.** 이게 깨지면 4방향 회전 코드가 필요해진다 (공용 2장은 34/37%, 66/70%) | 13/8% / 15/5% |
| 중심 | **밝기 중심이 캔버스 중심에서 몇 px인지.** 피벗이 Center이고 총구에 얹으므로 어긋나면 이펙트가 통째로 밀린다 (공용 2장은 y로 25.4·13.9px) | x2 y2 / x2 y5 |
| 적 위 판별 | 48px로 줄여 적 몸통 `rgb(216,81,76)` 위에 얹고 **차이 40 넘는 픽셀의 비율**을 공용 타격(11.5%)과 비교 | 관통 **11.6%** |
| 판별 | **50px·100px로 줄여** 형태가 읽히는지, 그리고 공용 2장과 나란히 놓아 갈리는지 | 통과 |

> **평균 대비로 재면 안 된다.** 처음에 *적 위 평균 대비 ≥ 13.7(공용과 동급)* 을 목표로 잡았는데, 평균은 **채움 비율에 눌리는 값**이다. 공용 타격은 44.5%를 채워 평균이 올라간 것이고 관통은 16.9%인데, **그 성김이 원을 없애려고 일부러 요구한 것**이다. 일부러 만든 성질에 벌점을 주는 기준이었다. 2차는 평균 10.8로 목표에 못 미쳤지만 **또렷한 픽셀 비율 11.6% 대 11.5%로 같고 95퍼센타일 대비는 86.5 대 60.1로 오히려 강하다.** 성긴 이펙트는 평균이 아니라 **또렷한 픽셀의 양**으로 잰다.

> **원이 들어오면 반려한다.** `PierceHitEffect`에서 원은 지시를 어긴 것이 아니라 **역할을 뒤집는 것**이다 — 그대로 넣으면 Pierce가 화면에서 Splash로 보인다. 공용 타격의 금색 원은 48px에서도 또렷해서, 그대로 뒀으면 확실히 범위로 읽혔다.

### Splash 폭발 전용 프롬프트 — `SplashEffect.png`

**왜 필요한가.** 지금까지 Splash 패턴(보머·소버린)은 맞은 적마다 공용 `HitEffect`(고정 크기 스파크)만 떴다 — 실제로 몇 발이 얼마나 넓은 범위를 때렸는지가 화면에 없었다. `CombatContext`에 착탄 지점 + 그 순간의 `splashRadius`를 함께 남기는 `SplashEvent`를 추가했고(맞은 적 수와 무관하게 착탄 1회에 1건), 뷰가 이 반경 그대로 폭발 스프라이트를 그린다.

**Bomber 전용이 아니라 Splash 패턴 공용 기본값이다.** §10.1이 "13종이 각자 다른 시스템을 쓰지 않는다"고 못 박았고 Splash는 패턴 코드가 공유되므로, 착탄 폭발도 패턴 단위로 넣었다. `UnitDefinition.splashEffect`로 유닛별 override는 가능하지만(공용 이펙트 두 장과 같은 방식) 지금은 두 유닛 다 비워 이 한 장을 같이 쓴다.

> **풀하우스 배터리는 원래 Splash 셋째였는데 Multi로 옮겨갔다.** 초당 2.5회씩 폭발이 반복되는 것이 "연사"가 아니라 "난사"로 보인다는 지적 때문이다 — 자세한 경위는 [HISTORY.md](HISTORY.md)에 있다. **로열 스트레이트 플러시 소버린도 같은 이유로 이후 Splash → Multi로 다시 옮겨갔다**(자동소총 실루엣에 맞춰 초고속 연사로 재설계, [HISTORY.md](HISTORY.md) 참고) — 지금 Splash는 플러시 보머 하나뿐이다. 아래 표의 소버린 반경·화면 크기는 **Splash였던 당시의 기록**이라 지금은 쓰이지 않는다.

| 유닛 | splashRadius | 화면 지름(× 145.5px) |
|---|---|---|
| 플러시 보머 | 1.0 | 291px |
| 로열 스트레이트 플러시 소버린 (구 Splash 당시) | 2.2 | 640px |

**한 장으로 291~640px을 전부 커버해야 한다.** 유닛마다 새로 그리지 않는다 — 코드가 스프라이트 전체 폭을 `splashRadius × 2`에 맞춰 늘이고 줄일 뿐이다.

**캔버스 전체가 실제 피해 범위에 대응한다 — 스파크 두 장과 다른 제약이다.** `MuzzleEffect`·`HitEffect`는 크기가 코드 상수(`MuzzleScale`·`HitScale`)라 캔버스에 여백이 많아도 상관없었다. 이 이펙트는 **스프라이트 가로 폭 전체를 실제 반경의 지름으로 스케일**하므로, 그림 주위에 빈 여백이 크면 **화면에서 폭발이 실제로 때리는 범위보다 작아 보인다.** 스카우트 파일럿 검증에서 기존 `HitEffect`를 임시로 물려 확인한 결과, 밝은 내용물이 캔버스 폭의 약 55%만 채우고 있어 그대로 썼으면 손실이 났을 것이다 — **최소 85%까지 채울 것.**

> **위 두 문단의 291·640px은 최초 설계값 기준이고, 지금 실제 화면 크기는 그 75%다.** v3를 넣어 보니 소버린(반경 2.2)이 보드 폭의 96%를 덮어 "너무 크다"는 피드백을 받았다. 처음엔 `CombatScreen`에 코스메틱 배율을 넣어 화면 크기만 줄였는데, 곧바로 **`splashRadius` 데이터 자체를 75%로 줄이고 공격력·공격속도로 유효 피해량을 보정하는 쪽으로 바꿨다** — 화면 크기(보머 218px / 소버린 480px, 보드 폭의 72%)는 그대로고 이제는 진짜 반경이다. 밸런스 계산 과정은 [HISTORY.md](HISTORY.md)에 있다. **캔버스를 85% 채워야 하는 기준은 그대로 유효하다** — 축소된 반경 안에서도 여백이 있으면 손실이 나기 때문이다.

#### 1차 시도 — 반려

수치는 대부분 통과했지만(알파 정상, 채움 비율 97%, 중심 오프셋 9px, 방사 대칭 22%) **화풍이 게임과 안 맞았다.** `NOT photographic, NOT a 3D render`, `NO smoke, NO soot, NO ash cloud`를 넣었는데도 사실적인 실사 톤 폭발과 뭉게구름 형태가 그대로 나왔다 — 기존 `MuzzleEffect`·`HitEffect`(크리스프한 벡터형 광선)와 나란히 놓으면 이질감이 바로 보였다. **수치 통과가 스타일 통과를 보장하지 않는다** — 이 이펙트에서 처음 나온 함정이다.

**v2로 두 가지를 바꿨다.**
- `NOT photographic` 한 줄로는 막히지 않아 **"FLAT CEL-SHADED VECTOR ILLUSTRATION"** 문단으로 승격하고, 원인이 됐을 부드러운 그라데이션·연기 질감을 **"하드엣지 평면 채색", "2~3단 플랫 컬러 밴드"**로 구체적으로 반대 지정했다. §2의 교훈과 같다 — 한 줄 부정문은 무시되고 문단으로 뺴야 먹힌다.
- **레퍼런스 이미지 첨부를 권장으로 추가했다.** 캐릭터 시트는 이미 "이전 유닛 시트를 레퍼런스로 물린다"를 쓰는데 이펙트는 텍스트만으로 뽑아 왔다 — 이번이 첫 이탈이라 다음부터는 `MuzzleEffect.png`·`HitEffect.png`를 같이 첨부한다.

#### 2차 시도 — 화풍은 통과, 실루엣이 새로 반려됨

화풍 문제는 완전히 해결됐다 — 하드엣지 평면 채색, 어두운 픽셀 0.000%(지금까지 중 가장 깨끗함), 채움 비율 97~98%. 그런데 **화풍을 고치는 과정에서 다른 문제가 새로 생겼다.**

- **"폭발"이 아니라 "빛줄기 별"로 읽혔다.** `a ring of solid flat-orange jagged flame shapes`를 요구했는데, 받은 건 중심에서 바로 뾰족한 광선이 뻗어 나가는 구성이라 **고리·덩어리감이 없었다.**
- **상하좌우 네 축이 대각선보다 뚜렷이 길었다.** 프롬프트가 명시적으로 금지한 지점(`NO up, NO down, NO left, NO right`, `NO single dominant spike longer than the others`)인데 그대로 나왔다 — 640px로 키우면 십자형이 확연했다. 방사 대칭 수치도 26.8%/24.5%로 1차(21.9%/21.3%)보다 오히려 나빠졌다 — **십자 구조는 90도 회전엔 강해도(자기 자신과 겹침) "축이 있다는 인상" 자체는 못 막는다는 것이 이번에 드러났다.** 숫자와 인상이 어긋난 첫 사례다.
- **기존 `MuzzleEffect`(가늘고 긴 8방향 스파크)와 실루엣이 너무 닮았다.** 폭발과 총구 화염이 구분이 안 되면 Splash 패턴 자체가 무의미해진다.
- **정작 이 게임 안에 "고리" 선례가 있었다.** `HitEffect`가 이미 중심 스파크 바깥에 두꺼운 고리를 두르고 있다 — v2 SHAPE 문단이 고리를 요구하긴 했지만 강도가 약해 무시됐다.

**v3는 세 가지를 더 박았다** — ① 고리에 실제 부피(반경의 1/3 두께)를 명시하고 `HitEffect`를 직접 지목해 참조하게 함, ② "네 방향이 길면 안 된다"를 별도 `===` 블록으로 승격(문단 하나로는 v2에서 무시당했다), ③ **이 게임의 다른 이펙트(`MuzzleEffect`)와 실루엣이 겹치면 안 된다**는 것을 이유와 함께 명시.

```
A single game VFX sprite on a FULLY TRANSPARENT background.
One centred radial burst. Japanese anime / JRPG game effect look,
FLAT CEL-SHADED VECTOR ILLUSTRATION - like a mobile game skill icon,
NOT photographic, NOT a 3D render, NOT a realistic fire simulation.

=== THIS MUST LOOK DRAWN, NOT RENDERED ===
Every ray, spike and flame tongue is a HARD-EDGED FLAT SHAPE with a
crisp vector outline, filled with 2-3 flat colour bands (NOT a smooth
photographic gradient, NOT volumetric lighting, NOT soft airbrushed
glow). Reference look: a mobile gacha game skill-cast icon, or a
retro arcade explosion sprite - graphic and readable, not simulated.
If it looks like a photo or a realistic CGI fire render, it is wrong.
============================

=== NO DOMINANT AXIS - THIS IS A BLAST, NOT A COMPASS STAR ===
Do NOT build this as four long spikes at 12/3/6/9 o'clock with
shorter ones in between - that reads as a compass star or a sparkle,
not an explosion, and this game already has a sparkle-style effect
(a thin needle-ray star flare) used for muzzle flashes. This sprite
must look clearly different from that one: NOT thin needle rays from
a point, but a THICK, DENSE, IRREGULAR mass of flame with real bulk.
Vary the angle and length of every spike randomly so no 4-fold or
8-fold repeating pattern is visible - no two adjacent spikes should
be the same length, and no spike pointing exactly up, down, left or
right may be longer than its neighbours.
============================

=== RADIAL SYMMETRY IS MANDATORY (COVERAGE, NOT REPETITION) ===
The overall coverage must look roughly the SAME after being rotated
90 degrees (no side of the canvas emptier than another), but the
individual spike shapes must NOT repeat in an obvious 4-fold pattern
(see rule above - the two rules work together, not against each
other). NO direction of travel, NO motion trail, NO comet tail,
NO arrow. The bright core sits EXACTLY at the centre of the canvas.
============================

=== GLOWING ONLY - NO DARK PIXELS, NO SMOKE ===
Every pixel is either fully transparent or glowing light.
NO black, NO dark grey, NO brown, NO shadow, NO dark outline.
NO thick black cartoon outline - it must not look like a sticker.
NO smoke, NO soot, NO ash, NO billowing cloud puffs, NO mushroom
cloud shape - these all read as dark/grey/muddy pixels and are
strictly forbidden even when tinted orange. The whole shape is made
of flat, saturated, glowing colour - nothing textured or hazy.
============================

=== FILL THE CANVAS - THIS IS NOT A SMALL SPARK ===
The game engine scales this sprite's FULL CANVAS WIDTH to match the
actual explosion radius that damages enemies on screen.
The glowing shape must reach AT LEAST 85% of the way to every edge.
Large empty margin around a small tight burst will make the explosion
look smaller than the area it actually damages - this is a functional
bug, not just a look. Fill the frame.
============================

Square canvas 1254 x 1254. Read at THUMBNAIL SIZE: big simple shapes,
high contrast. NO fine texture, NO tiny specks, NO scattered debris,
NO grainy noise - this sprite is shown as small as 290px and as large
as 640px, so anything busier than a few clean flat shapes turns to
mud when shrunk or goes soft and blurry when enlarged.

NO background art, NO ground, NO frame, NO panel,
NO character, NO hand, NO weapon, NO card,
NO text, NO numbers, NO letters, NO watermark, NO signature.

SHAPE - a heavy GRENADE BLAST with real VOLUME, drawn as FLAT VECTOR
SHAPES. A blinding white-hot star core at the exact centre, surrounded
immediately by a THICK, CHUNKY RING of solid flat-orange flame blocks
(overlapping rounded flame-tongue shapes, each with clear area and
mass, NOT thin lines) - this ring must be the widest, densest part of
the image, roughly a third of the canvas radius thick. This is the
single most important shape note: match the weight and density of the
ring already used in this game's existing HitEffect sprite, just
bigger and bolder. Only past this ring do jagged spikes and flame
tongues break outward toward the edges, irregular in angle, length
and width as described above. Leave a few small transparent gaps
between spikes so it doesn't read as a solid disc, but the ring
itself must never look thin or wiry.

COLOUR - white-hot centre, then 2-3 FLAT colour bands stepping out
through bright orange to deep red at the outer tips (hard edges
between bands, not a smooth gradient), with a few small flat gold
diamond sparks scattered near the tips. Same warm fire palette as
the existing MuzzleEffect and HitEffect (NOT the cold steel palette
used for the Vanguard-only effects) - this is the shared default for
every Splash-pattern unit, not a per-unit effect.

NO thin ring drawn as an outline only - it must feel like a solid,
heavy blast, not a decorative halo or a UI radius indicator.
NO single small burst centred in a mostly empty canvas.
NO four-armed compass-star silhouette - that is the OTHER effect in
this game (the muzzle flash), not this one. If someone could mistake
this for a bigger muzzle flash, it has failed the brief.
NO photographic fire, NO realistic smoke, NO 3D volumetric render.
```

> **생성할 때 `MuzzleEffect.png`·`HitEffect.png` 두 장을 스타일 레퍼런스로 함께 첨부할 것.** 특히 `HitEffect`의 고리 두께를 목표로 삼고, `MuzzleEffect`의 가는 광선과는 겹치지 않게 갈라야 한다.

#### 받으면 잴 것 — v3에서 **완료**

| 항목 | 기준 | 근거 | 1차 | 2차 | 3차 |
|---|---|---|---|---|---|
| 알파 | 어두운 픽셀 0.2% 이하 | 기존 5장과 동일 기준 | 통과 (0.8%) | 통과 (0.000%) | **통과 (0.000%)** |
| 방사 대칭 | 90·180도 돌려 겹쳐 차이가 작을 것 | 위치만 옮겨 찍으므로 회전 코드가 없다 | 21.9%/21.3% | 26.8%/24.5% | **21.4%/20.2% — 여섯 장 중 최고** |
| 중심 | 밝기 중심이 캔버스 중심에서 몇 px인지 | 피벗이 Center이고 착탄 지점에 그대로 얹는다 | x4/y9px | x3/y16px | **x3/y13px (통과)** |
| 채움 비율 | 가로 bbox가 캔버스 폭의 85% 이상 | 캔버스 전체 폭이 실제 반경으로 스케일된다 | 97.3%/97.7% | 97.3%/98.5% | **98.8%/99.0%** |
| 화풍 | 크리스프한 셀셰이딩 벡터일 것 | 수치가 다 통과해도 화풍이 다르면 반려한다 | 탈락 — 실사풍 | 통과 | **통과** |
| 실루엣 | 고리·덩어리감이 있을 것. `MuzzleEffect`와 구분될 것 | 고리 없이 광선만 있으면 "폭발"이 아니라 "반짝임"으로 읽힌다 | (해당 없음) | 탈락 — 고리 없음, 십자형 축 | **통과** — 뚜렷한 화염 덩어리, `MuzzleEffect`와 확연히 다름 |

> **v3에도 경미한 대각선 편향이 남아 있다** (모서리 방향 스파이크가 상하좌우보다 최대 837px 대 573px로 길다). `NO up/down/left/right`만 금지하고 대각선은 안 막아서 생긴 틈이다. **291~465px 실사용 크기에서는 거의 안 보이고**, 640px(소버린)에서만 자세히 보면 티가 난다 — 십자형이었던 2차와 달리 고리가 있어 "폭발인데 모서리가 좀 더 뻗었다" 수준으로만 읽혀 **통과로 판단**했다. 반경이 훨씬 큰 유닛이 늘어나면 다시 볼 것.
>
> **`SplashEffect.png`가 `HitEffect.png`와 같은 임포트 설정으로 `Art/VFX/`에 있고, `Game.unity`의 `CombatScreen.splashSprite`에 연결됐다** (PPU 1254 / alignment Center / npotScale None / alphaIsTransparency 켬). `sprite.bounds.size`가 정확히 `(1, 1)`이라 `fullScale = splashRadius × 2`가 그대로 성립한다 — `splashRadius` 자체가 75%로 조정돼 있어(위 참고) 화면 크기는 코스메틱 배율 없이도 의도한 크기로 나온다. EditMode 217/217, 콘솔 0건. 착탄 지점에 실제로 그려지는 것을 캡처로 확인했다.

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
| **쌍무기 총구 (교대/동시)** | `UnitDefinition`·`CombatContext`·`UnitSlotView`·`BoardScreen` | **완료.** 교대는 발사 홀짝을 `CombatContext.ShotCountOf`가 세고, 동시는 `muzzlesFireTogether`로 켠다. 둘째 렌더러는 씬이 아니라 **처음 쓸 때 코드가 복제**한다. 무기가 하나인 유닛은 두 번째 좌표가 비어 단발로 떨어진다 |
| **방향별 총구 목록 + 반동 동조** | `UnitDefinition`·`UnitSlotView` | **완료.** 측면/정면/후면 **목록**을 따로 받아 총구 1~5개를 그대로 담는다. 발사 방식은 동시/순서대로/무작위 셋. 화염이 반동 배율을 따라가고, 렌더러는 **개수 가변 풀**이다 |
| **유닛별 이펙트** | `UnitDefinition`·`CombatContext`·`UnitSlotView`·`CombatScreen` | **완료.** `attackEffect`·`hitEffect`를 받고 **비우면 공용 2장으로 떨어진다.** 타격 쪽은 `HitEvent`가 때린 유닛을 함께 실어 보내야 뷰가 고를 수 있다 |
| **빔 궤적** | `UnitDefinition`·`CombatContext`·`CombatScreen` | **완료.** `PierceEvent`가 **유닛이 선 칸·겨눈 적·조준 방향**을 넘기고, 뷰가 그 방향의 총구에서 적까지 한 줄로 회전·신축해 그린다(적을 0.3만큼 지나친다). `pierceTrailEffect`가 비면 안 그린다 — **공용 대역이 없는 유일한 이펙트다**(방향이 있어 아무 유닛에나 못 쓴다) |
| **Splash 폭발** | `UnitDefinition`(`splashEffect`)·`CombatContext`(`SplashEvent`)·`CombatScreen`(폭발 풀) | **완료.** 착탄 지점 1회당 `SplashEvent` 1건(맞은 적 수와 무관), 뷰가 스프라이트 **전체 폭을 `splashRadius × 2`로 스케일**해 그린다 — 실제 피해 범위와 화면상 크기가 항상 같다. `splashSprite`가 비어 있으면 아무것도 안 그린다(피해 판정은 무관하게 정상 동작). **지금 Splash는 플러시 보머 하나뿐**이고, 유닛별 `splashEffect`로 덮어쓸 수 있다. **풀하우스 배터리·로열 스트레이트 플러시 소버린은 둘 다 Splash → Multi로 옮겨 이 이펙트를 안 쓴다**([HISTORY](HISTORY.md)) |
| **이름 라벨 제거** | `UnitSlotView` | 13종이 전부 들어오고 판별을 확인한 뒤. 먼저 지우면 색 사각형을 색으로만 구분하게 된다 |
| **카드 텍스트 → 이미지** | `CardView`, `CardText` | 4단계와 함께 |
| **적 스케일** | `EnemyDefinition`, `CombatScreen.CreateView` | **완료.** `visualScale`로 보스류를 더 크게 그린다 |
| **적 4방향 아트 + 이동 방향 연동** | `EnemyDefinition`(`ArtFor`), `GridBoard`(`TrackDirection`), `CombatScreen` | **완료(코드만).** 조준이 아니라 트랙 이동 방향(4구간)에 맞춰 고른다. 아트가 없으면 기존 플레이스홀더 색 사각형으로 떨어진다 |

---

## 7. 받은 이미지를 넣는 절차

스카우트에서 확립한 순서다.

1. 시트를 `Art/Units/<유닛>/`에 넣는다
2. **자르기 전에 분리 행·열을 찾는다.** 세로·가로 중앙선의 통과 픽셀을 세고, 걸리면 **위 칸이 끝나는 행과 아래 칸이 시작하는 행을 찾아 그 사이에서 자른다.** 건슬링어는 간격이 0행이라 정중앙(627)이 아니라 623에서 갈랐다 — 균등 분할이었으면 아래 칸 머리끝 4행이 위 칸에 섞여 **바닥 정렬이 발끝이 아니라 그 얼룩 기준으로 잡혔다**
3. 각 칸을 알파 기준으로 트림한다. **네 장의 높이 편차를 확인한다** — 건슬링어는 614·611·616·616(0.8%)이었다. 크게 벌어지면 방향이 바뀔 때 캐릭터가 커졌다 작아진다
   > **편차가 크면 먼저 원인을 가른다 — 몸이 진짜 작은 것과 머리·포즈가 다른 것은 다르다.** 부츠처럼 방향에 무관한 부위의 높이를 재서 bbox 높이로 나눠 본다. 비율이 네 방향 같으면 몸 전체가 균일하게 축소된 것이고, 그때는 **네 장을 같은 몸높이로 정규화해도 안전하다.** 롱바렐은 편차 2.5%에 부츠 비율이 넷 다 30.0~30.1%로 같아 정규화했다. 비율이 갈리면 정규화하면 안 된다 — 배터리의 4.3%는 *포대가 정면에서만 머리 위로 올라가서* 생긴 것이라 그대로 두는 게 맞았다
4. **아래 두 칸이 실제로 어느 쪽을 보는지 확인한다.** 프롬프트에 `BOTTOM-LEFT = LEFT`라고 적어도 **뒤바뀌어 나온다**(마크스맨이 그랬다). 못 잡으면 게임에서 오른쪽 적을 쏠 때 왼쪽을 겨눈 그림이 나오는데, 화면으로는 "좀 이상한데" 정도로만 보여 찾기 어렵다.
   판정은 **무기가 있는 위쪽 띠의 x 중심과 다리의 x 중심을 비교**하면 된다 — 무기가 오른쪽에 있으면 오른쪽을 보는 것이다. 뒤바뀌었으면 **다시 뽑지 말고 자를 때 엇갈려 배정한다**
5. 네 장을 **공통 캔버스에 가로 중앙·아래 정렬**로 다시 채운다 — 크기와 접지선이 같아야 방향이 바뀔 때 캐릭터가 위아래로 튀지 않는다
   > **가로 정렬은 "몸 튐"과 "무기 돌출"을 맞바꾸는 자리다. 한쪽만 보면 반드시 다른 쪽에서 터진다.**
   > - **bbox 중심**에 맞추면 무기가 칸 안에 들지만, 무기가 한쪽으로만 길 때 bbox 중심이 몸에서 멀어지고 **좌향·우향에서 부호가 뒤집혀 몸이 가로로 튄다.** 롱바렐은 화면 56.8px였다.
   > - **발 중심**에 맞추면 몸은 안 움직이지만 **무기가 칸 밖으로 나간다.** 롱바렐은 총구가 슬롯 배경을 34px 넘었다.
   > - **답은 절충이다.** 무기 끝이 슬롯 배경(±0.46)에 닿는 지점까지만 몸을 밀고 나머지는 그대로 둔다. 롱바렐은 몸 튐 35.3px가 됐는데 **기존 6종이 이미 14~38px을 감수하고 있다**(건슬링어 37.9 / 마크스맨 34.9 / 트윈레인저 27.5 / 배터리 20.4 / 스카우트 17.5 / 쿼드캐논 14.0). 방향이 바뀌는 순간은 스프라이트가 통째로 갈리는 순간이라 "돌아섰다"로 읽힌다.
   > - **폭 자체는 문제가 아니었다.** 롱바렐 측면 폭은 마크스맨(0.705)과 사실상 같다. 갈린 것은 정렬뿐이다.
6. 임포트: `Sprite / Single`, **`npotScale = None`**, `PPU = 스프라이트 높이`, `Bilinear`, 밉맵 켬, 피벗 `BottomCenter`, `Alpha Is Transparency` 켬
   > **피벗은 `TextureImporterSettings.spriteAlignment`로만 걸린다.** `TextureImporter`에는 그 속성이 없고, MCP `manage_asset`으로 넘기면 성공을 반환하면서 조용히 무시된다. 메타를 되읽어 `alignment: 7`을 확인할 것 — Center로 남으면 유닛이 슬롯에서 위로 뜬다
7. `UnitDefinition`의 아트 4칸에 물린다
8. **총구 위치를 세 번 잰다 — 측면 / 정면 / 후면.** 픽셀 좌표를 슬롯 로컬로 바꿔 각각 `muzzleOffset` / `muzzleOffsetFacing` / `muzzleOffsetBack`에 넣는다. **무기가 둘이면 측면의 아래쪽 총도 재서 `muzzleOffsetSecond`에 넣는다**

   ```
   world_x = (tip_x - 폭/2) / PPU * 0.85
   world_y = -0.46 + (높이 - tip_y) / PPU * 0.85
   ```

   - **측면**은 가장 바깥으로 뻗은 픽셀이 총구 끝이라 자동으로 잡힌다.
   - **정면·후면**은 총구가 실루엣 안쪽에 있어 바깥 끝으로는 못 잡는다. 20px 격자를 얹어 확대해 읽되, **눈으로 읽은 값을 그대로 믿지 말 것** — 롱바렐 정면 보어를 격자로 읽었다가 **x가 21px 틀렸고**, §7-10의 왕복 검산에서야 드러났다. **총구 주위만 좁게 잘라 특징 색으로 자동 검출**하는 편이 정확하다(롱바렐은 금색 링, 스카우트·마크스맨은 총열 끝).
   - **무기가 몸에 완전히 가려 안 보이는 방향이 있다.** 롱바렐 후면이 그렇다 — 총을 정면으로 겨눠 뒤에서는 몸에 가린다. 그때는 **정면 보어를 좌우 반전한 자리**를 쓴다. 실제로 그 점에 투영되고, 화염은 몸 뒤(order 2)에 들어가 안 보이며, 궤적만 몸 위로 뻗어 나간다.
   - 무기가 하나면 **부호를 그대로**, 둘이면 좌우 대칭이므로 양수로 넣는다.

   > **재는 식이 맞는지는 스카우트 측면으로 확인할 수 있다.** 같은 식을 돌리면 기록된 값(우향 +0.206/+0.261, 좌향 −0.231/+0.257)이 그대로 나와야 한다.
   >
   > **끝이 뾰족하게 수렴하는 무기는 기하학적 끝과 눈에 보이는 끝이 다르다.** 124px로 줄면 마지막 몇 px이 배경과 섞여 사라진다 — 폐기한 창 시트에서 그 차이가 **화면 9px**이었다. 알파 임계를 올려도 안 잡힌다(0.8px밖에 안 줄어든다). **얇아서 없어지는 것이지 흐려서 없어지는 것이 아니다.** 그때도 기하학적 끝을 그대로 썼다 — 화염 스프라이트가 41px이라 보이는 끝을 덮는다. 총구가 뭉툭한 유닛은 두 끝의 차이가 0.2px이라 이 문제가 없다. 마운틴 킹의 ▲나 소버린의 왕관처럼 뾰족한 형태가 오면 다시 만난다.

9. **124px로 줄여 기존 유닛과 나란히 놓고 판별을 확인한다.** 종횡비만 보지 말 것 (§4-2단계)
10. **화염 위치를 그림으로 검산한다.** 코드와 같은 식(방향별 좌표 + 반동 배율)으로 계산한 점을 **반동이 최대인 순간의 아트 위에** 찍어 총구에 붙는지 본다. 눈으로만 보면 0.06초라 놓친다
11. 슬롯에 올려 방향 전환과 총구 화염을 확인한다. **화염은 0.06초라 `Time.timeScale`을 낮춰야 보인다**

   > **족보가 뜰 때까지 기다리지 말 것.** Unity 메뉴 `DevMode > 족보 소환`에서 해당 족보를 눌러 바로 부른다. 손패의 92%가 하이카드·원페어라(DESIGN §3.3) 상위 족보는 기다려서는 사실상 못 본다.

> **함정:** 스프라이트로 전환하기 **전에** 텍스처 높이를 읽으면 안 된다. NPOT 스케일이 407×749를 512×512로 눌러 놓은 값이 나와 PPU가 어긋난다. `npotScale = None`을 먼저 걸고 다시 읽는다.

## 8. 생성 시 주의

- **투명 배경 PNG로 뽑을 것.** 채팅 화면에서는 흰 배경과 투명이 똑같이 보이므로 알파를 파일로 확인한다.
  > **투명 영역에 색이 남아 있는 것은 정상이다.** 알파가 0이어도 RGB가 남아 있어 뷰어에 따라 어두운 그라데이션처럼 보이는데, `Alpha Is Transparency`가 처리한다. 판단은 **알파 값으로만** 한다.
  > **알파가 255에 안 닿는 것도 결함이 아니다.** 1024×1536으로 나온 시트(스카우트·쿼드캐논)는 최대 알파가 **254**다 — 0.4% 차이라 보이지 않고 이미 게임에서 정상 동작한다. 1254² 시트는 255에 닿는다. 생성 규격 차이일 뿐이다.
- **동일 헤더를 고정**하고 이전 유닛 시트를 레퍼런스로 첨부해 13종 간 톤을 맞춘다.
- **가로로 긴 비율보다 세로 2:3**이 낫다. 2×2 격자에 전신 캐릭터가 들어가야 한다.
- 유닛은 124px, 적은 73px로 축소해 **판별되는지 확인한 뒤** 다음 시트로 넘어간다.
