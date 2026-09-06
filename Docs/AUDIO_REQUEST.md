# 오디오 리소스 요청 목록

[POLISH.md](POLISH.md) 1번(오디오 패스)에서 생성형 AI로 제작할 사운드 목록이다.

## 현재 채택 — 기타 반주 전투 BGM (2026-09-06)

- 사용자 선택에 따라 GuitarRhythmOnly/guitar_loop.wav를 bgm_combat.wav에 그대로 적용. 메인 선율 없는 드럼·베이스·기타 반주, 48kHz/스테레오/16bit/76.8초. 끝 페이드 없는 루프 원본 사용, 게인 변경 없음.
- 전투 WAV만 해시 변경됨을 확인. 메타 GUID/총성/다른 곡 유지. 이전 전투곡 BeforeGuitarRhythmInstall에 백업. 현재 런타임 선택은 기타 반주 버전.

## 최신 후보 — 기타 주선율 교체 (2026-09-06)

- Artifacts/Audio/GuitarLeadAlternative/guitar_preview.wav. 비트/기타 반주는 GuitarDeveloped와 마스터 전 동일, 주선율만 낮고 짧은 문답형으로 수정. 게임 적용 전 청취 후보.

## 최신 비교 후보 — 기타 선율 발전 (2026-09-06)

- Artifacts/Audio/GuitarDeveloped/guitar_preview.wav: 4종 저음 리프 + 별도 주선율/응답/후반 변주. 이전 기타 후보와 동일 배경·RMS로 비교 가능. 현재 게임곡은 ArenaSmooth 유지.

## 비교 후보 — 일렉기타 / 남성 가창 레이어 (2026-09-06)

- 게임곡은 ArenaSmooth 유지. Artifacts/Audio/GuitarChoirCandidates의 guitar_preview.wav / choir_preview.wav 비교. 같은 배경과 평균 음량을 사용한다.
- 기타는 CC0 실제 기타 샘플 편집, 합창은 MIT 실제 남성 1인 가창을 겹친 합창풍 편곡. 출처와 배포 고지 조건은 Tools/Audio/GUITAR_CHOIR_CANDIDATES.md. 채택 미정.

## 최신 — 타격 잡음 후보 제거 (2026-09-06)

- 사용자 지적의 탁탁 소리는 직접 청음으로 특정하지 못했으므로 코드상 후보인 금속 틱·고스트 스네어·스네어 필·전환 노이즈·거친 공기 질감·반복 짧은 현악을 제거. 하이햇 음량을 줄이고 고정 고주파 사인 성분 제거. 기본 킥/백비트 유지.
- 짧은 금관 문답을 긴 저음 금관 프레이즈와 쉼표로 교체. 현재 미리듣기 ArenaSmooth, 백업 BeforeArenaSmooth. RMS -14.77 dBFS, 피크 -2 dBFS, 루프 끝점 차이 0. 주관적 오락실 느낌 해소는 청취 확인 필요.

## 이전 — 어두운 투기장 (2026-09-06)

- Stage/Background.png의 무너진 석벽·암회색 바닥을 직접 확인하고 어두운 투기장 방향으로 편곡. 저음 금관의 새 반음 모티프, 긴 저음 현악 질감, 거친 공기·짧은 금속 타격을 배치. 합성 합창 제거.
- 엇박 킥·실제 스네어·세분 하이햇, 짧은 저음 펄스로 현대적 리듬 유지. 조용한 중간부 없음. 150 BPM/76.8초, RMS -14.76 dBFS, 샘플 피크 -2 dBFS. 미리듣기 RuinedArena, 이전 버전 BeforeRuinedArena. 주관적 청취 채택 대기.

## 이전 — 금관·합창풍 편곡 (2026-09-06)

- 현악 주선율을 제거하고 실제 CC0 호른/트롬본의 짧고 넓은 문답형 주제로 재설계. 현악은 저음 짧은 반주만 담당. 기존 킥·스네어·하이햇 유지.
- 낮은 무가사 합창풍 배경은 직접 모음 합성한 음색이며 실제 사람 합창 녹음이 아님. 금관 소스 추가 후 검증된 샘플 총 114개. 현재 미리듣기 Artifacts/Audio/BrassChoir, 백업 BeforeBrassChoir.

## 이전 — 드럼 비트 및 선율 강화 (2026-09-06)

- 팀파니 백비트를 실제 CC0 스네어 2테이크로 교체. 매 박 하이브리드 킥(녹음 큰북+짧은 합성 저음), 2·4박 스네어, 고스트 노트와 8분 하이햇 추가.
- 현악 주제 강박 강조, 호른 더블링 확대, 배경 패드 감소로 주선율을 전면에 배치. 현재 미리듣기 Artifacts/Audio/BattleDrums, 백업 BeforeBattleDrums. 전투곡만 변경.

## 이전 — 공격적인 전투 테마 (2026-09-06)

- C단조의 강한 반복음·상행 응답으로 새 멜로디 작곡. 조용한 도입/하프 중간부를 없애고 전 구간 큰북·팀파니·8분음표 현악 반주 유지. 짧은 주제 음은 spiccato 샘플로 발음 선명도 보강.
- 150 BPM/76.8초. RMS -14.30 dBFS(직전 -15.36), 샘플 피크 -2 dBFS. 실제 게임 게인 유지. 현재 미리듣기 Artifacts/Audio/BattleAssault, 이전 곡 BeforeBattleAssault.

## 이전 — 선율·화성·리듬 완전 재작곡 (2026-09-06)

- 기존 A/B 선율을 폐기하고 D단조 12마디 현악 주제와 독립 하프 중간부로 교체. 셋잇단 질주 리듬, 새로운 베이스·화성, 절정의 호른 대선율을 추가. 실제 악기 샘플은 유지.
- 도입 0초/주제 6.4초/중간부 25.6초/절정 38.4초/확장 57.6초/마무리 64초. 150 BPM, 76.8초. 현재 미리듣기 Artifacts/Audio/MelodyRebuilt, 백업 BeforeMelodyRebuilt.

## 이전 — 실제 악기 샘플 기반 전투곡 (2026-09-06)

- 오락실 같은 합성 음색을 줄이기 위해 VSCO 2 CE의 CC0 실제 악기 샘플로 현악·호른·하프·큰북·팀파니 교체. 출처/라이선스/재생성은 Tools/Audio/ORCHESTRA_SOURCES.md.
- 8마디 A/B 선율과 150 BPM/76.8초 구조 유지. 현악 짧은 주법의 복수 테이크, 미세한 타이밍·강약 변화, 음표별 완만한 다이내믹과 넓은 홀 반사를 적용. 조용한 림/전환 질감은 합성 유지.
- 전투 BGM WAV만 갱신. 총성 및 다른 곡은 유지. 미리듣기는 Artifacts/Audio/OrchestralPolish, 이전 합성곡은 BeforeOrchestralPolish. 실연 오케스트라나 전문 레가토 엔진과 동일한 결과를 보장하는 것은 아니며 최종 음색은 사용자 청취로 확인.

## 이전 — G36 후보 / 판타지 선율 재작곡 (2026-09-06)

- 총성: CS279의 CC0 G36-E Fire 공개 OGG 미리듣기를 편집. 실사격 여부는 미확인이고 압축 소스다. 한 발의 잔향·채널 변형 3개, 0.18초/-3 dBFS. 출처와 재현 방법은 Tools/Audio/FIREARM_SOURCES.md.
- 전투곡: 판타지 합성 악기와 타악을 유지하며 독립적인 8마디 A/B 선율·화성으로 교체. 호른 주제 → 현악 응답, 긴 음과 쉼표로 프레이즈 구분. 150 BPM/76.8초, RMS -15.21 dBFS, 샘플 피크 -2 dBFS.
- 참고 링크: [모르반](https://www.youtube.com/watch?v=6gLdZLQ-UQY), [죽음의 신 크로우 크루아흐](https://www.youtube.com/watch?v=G6wlUYIuJxI). 제목 확인만 수행; 직접 청음·채보·샘플링하지 않은 독립 작곡이다.
- 현재 미리듣기: Artifacts/Audio/FantasyMelodyRework. 직전 M4와 전투곡 백업: BeforeFantasyMelodyRework. 기존 WAV 4개만 교체하고 GUID 및 나머지 런타임 오디오는 유지.

## 이전 — M4 계열 총성 / 중세 판타지 전투곡 (2026-09-06)

- 로열 총성: 같은 CC0 라이브러리의 `AR-15/D_32P.wav`로 교체. 원본 목록은 AR-15/M4, .223/5.56x45, 근거리 스테레오 사격으로 설명한다. 두 발과 첫 발의 채널 배합 변형으로 3종 구성. AK 녹음은 현재 출력에 섞지 않는다. 0.18초·-3 dBFS 피크·기존 게임 게인 유지.
- 전투곡: 신스 리드·전자 킥·하이햇·사이드체인·박자 딜레이를 제거하고, 활 현악·저음 현악 오스티나토·호른·류트풍 발현·가죽 북·홀 잔향으로 재편곡. 악기 음색은 합성 근사이며 실제 오케스트라 녹음이 아니다. 150 BPM/76.8초 및 테마·숨 고르기·복귀 구조 유지. 최종 RMS -14.95 dBFS, PCM 피크 -2 dBFS; 큰북 피크는 스테레오 연동 마스터 압축으로 정리했다.
- 전투곡·로열 WAV 4개만 교체. 준비/보스/결과곡의 테마 통일은 아직 하지 않았으므로 해당 전환 청취는 별도 확인한다.
- 현재 출력/미리듣기는 `Artifacts/Audio/FantasyRework`, 직전 AK·전자 테마 백업은 `BeforeFantasyRework`. 생성기는 기존 `rework_combat_theme.py`·`edit_recorded_rifle.py`를 갱신했다. 출처 상세는 Tools/Audio/FIREARM_SOURCES.md.

## 최신 재시도 — 실제 녹음 라이플 / 전개가 있는 전투 테마 (2026-09-06)

- 로열 총성은 합성 방식에서 **CC0 실제 AK-47 녹음 편집**으로 교체. The Free Firearm Sound Library의 서로 다른 단발 3개를 0.18초로 편집, 모노·48kHz 변환, 저역 정리·짧은 잔향·완만한 압축 적용. 정확한 AK-12 복제는 아니다. 출처·제작자·라이선스·원본 해시는 `Tools/Audio/FIREARM_SOURCES.md`에 기록했다.
- 전투 BGM만 다시 작곡: 4마디 문답형 테마, 화성 진행, 25.6초 숨 고르기 → 32초 빌드 → 38.4초 테마 복귀. 76.8초 전체 루프, 기존 150 BPM 유지. RMS -14 dBFS, PCM 피크 약 -2.34 dBFS. 준비·보스·결과 BGM은 변경하지 않았다.
- 생성/편집 도구: `rework_combat_theme.py`, `edit_recorded_rifle.py`. 런타임 파일 수 56개 유지, 로열 ID 매핑·게인·성급별 공속·쿨다운은 동일.
- 현재 미리듣기와 측정 결과는 `Artifacts/Audio/ThemeRework`, 이전 버전은 `BeforeThemeRework`, 외부 원본은 `FirearmSource`에 보관. 모두 Git 제외. 아래 내용은 이전 시도 이력이다.

## 최신 — 현대적 다크 그루브 / 로열 라이플 (2026-09-06)

- BGM 4곡 재작곡: 싱코페이션 미드베이스, 짧은 단조 FM 훅, 킥·클랩·오프비트 햇, 8마디 필과 프레이즈별 음색 변화. 150 BPM·기존 길이/전환 그리드 유지. RMS 계획 -17 / 전투 -14 / 보스 -13.5 / 결과 -23 dBFS, PCM 피크 상한 -2 dBFS. LUFS·true peak 인증 아님.
- 로열 스트레이트 플러시에 전용 FireRoyalRifle 큐와 0.155초 단발 3변형 추가. 기존 다중 총성과 분리. 0.04초 게이트로 같은 공격의 중복 타격음을 막되 ★3 초당 13.5발을 허용한다. 전투 규칙·공속은 변경하지 않는다.
- [사용자 참고 영상](https://www.youtube.com/watch?v=LVp5yZ6T_Xs)은 AK-12 사격 영상임을 제목·화면으로 확인. 음성을 직접 청음하거나 추출하지 않았으며, 정확한 음색 복제라고 주장하지 않는다. 파열음·압력 몸통·금속 작동음을 독립 코드 합성했다.
- 현재 32큐 / SFX52파일 + BGM4. 생성기 `Tools/Audio/modern_combat_audio.py`는 V2 생성기 이후 실행한다. 다른 카드·보상·무기 효과음은 유지한다.
- 미리듣기·검증: `Artifacts/Audio/ModernCombat`, 이전 BGM: `Artifacts/Audio/BeforeModernCombat`. 전부 Git 제외. 단발→성급별 연사 및 음악+연사 오프라인 미리듣기를 제공한다. 실제 휴대폰 청취와 최종 채택은 별도 확인한다.

아래는 이전 제작 이력이며, 현재 수치·파일 수는 이 최신 항목을 따른다.

## 2차 청취 수정 — 2026-09-06

- 사용자 피드백: 카드가 실제 덱을 만지는 질감이 아니고, 음악이 올드하며, 머지/라운드 차임이 저렴하게 들림.
- 카드 4종(선택/딜/플립/교체)을 종이 마찰·불규칙 섬유·카드 휨·착지 레이어로 다시 합성, 각 3변형. 실사 녹음은 아님.
- CardView 버튼에 겹치던 공용 클릭음을 제외. 카드 피치 랜덤화 제거, 변형 클립만 교대.
- BGM 4곡 재작곡: 레트로 톱니파 리드와 16분 아르페지오 제거. 150 BPM 하프타임 드럼, 싱코페이션 서브베이스, 절제된 현 질감/공간음. 기존 길이·박자 그리드 유지.
- 머지/족보/종료/보상 스팅어는 상승 벨 대신 짧은 마찰-클로저-따뜻한 저역 잔향으로 교체. 웨이브 클리어 0.32초, 머지 0.36/0.58초. 자주 나는 큐의 게인을 낮춤.
- 현재 31큐 / SFX 파일49 + BGM4. V2 교체·추가33파일, 이전 총성 등은 유지.
- 생성기 Tools/Audio/refine_table_audio.py. 이전 버전 Artifacts/Audio/BeforePolishV2, 새 파일/수치검증/미리듣기 Artifacts/Audio/PolishV2.
- V2 음색의 최종 채택 여부는 사용자 청취로 결정. 아래 1차 기록의 차임/사이드체인 설명은 구버전 이력.

## 진행 상태 — 코드 합성 오디오 패스 (2026-09-06)

- **31개 SFX 큐 + BGM 4곡 제작·연결.** 발사 5패턴은 각 3가지 변형으로 총 SFX 파일 41개.
- **BGM:** 계획/전투/보스는 150 BPM·76.8초, 결과는 38.4초. 같은 조성·박자 계열의 별도 편곡. 0.8초 크로스페이드, 중요 스팅어 때 음악 덕킹.
- **재생:** 전투 8 + 피드백 8 음원 풀, DSP 시간 쿨다운, 그룹별 합산 게인 제한. 스팅어는 고정 음정, Foley/총성만 약간의 피치 변형.
- **트리거:** 공격 패턴 5종·특수 착탄·보스 등장/처치·3단 족보·판매/유지 보너스·상점·조커·종료 결과 연결.
- **소스:** 외부 음원·샘플 없이 Python/NumPy로 직접 합성. 총성은 실사 녹음이 아닌 게임용 합성 대안이며, 아래의 과거 실총 라이브러리 방향과 비교해 청취 채택을 결정한다.
- **형식:** 48kHz/24bit. SFX 모노 PCM 인메모리, BGM 스테레오 Vorbis 스트리밍. BGM RMS는 계획 -23/전투 -17/보스 -16/결과 -24 dBFS로 효과음 공간 확보. **-14 LUFS 달성을 측정한 것은 아니다.**
- **검증:** WAV 45개 형식/피크/루프 경계 검증, EditMode 229/229 통과. 폰 스피커 청취와 최종 음색 승인, 스트리밍 루프 청감 확인은 남아 있다.
- 재생 코드: Scripts/UI/AudioManager.cs. 생성·재현 방법: Tools/Audio/README.md.
- 원본 비교본: Artifacts/Audio/BeforePolish/. 새 파일·효과음 모음·수치 보고서: Artifacts/Audio/Polish/.

### 과거 피드백과 현재 처리

1차 SFX 볼륨 과다 → 기존 인스펙터 sfxVolume=0.65, musicVolume=0.4는 유지하고 큐별 게인·동시 재생 예산을 추가했다.
1·2차 총성 “장난감”, BGM “밋밋함/올드함” → 외부 생성 위젯 대신 직접 합성한 다크신스와 폭발 노이즈·저역·기계음 레이어로 새 후보를 만들었다. **음색 개선의 최종 판정은 청취로 한다.**

아래 표·프롬프트는 원래 요청 스펙과 제작 피드백 기록이다. 현재 구현 상태는 위 내용을 따른다. Udio의 공식 다운로드 중단을 확인했으므로, 아래 예전 서비스 추천을 현재 상업 이용/다운로드 가능 여부의 근거로 사용하지 않는다.

> 게임의 톤·연출 맥락은 [DESIGN.md](DESIGN.md), 아트 톤은 [ART_REQUEST.md](ART_REQUEST.md)가 출처다.
> 이 문서는 **에셋 스펙만** 다룬다. 코드 배선(AudioManager·믹서·트리거)은 에셋이 들어온 뒤 별도 작업으로 한다 — ART_REQUEST와 같은 순서.

---

## 1. 공통 스펙

| 항목 | 값 | 비고 |
|---|---|---|
| 포맷(제출) | WAV, 48kHz / 24-bit (44.1kHz / 16-bit도 허용) | Unity에서 SFX는 압축 인메모리, BGM은 Vorbis 스트리밍으로 임포트 예정 |
| 채널 | SFX = 모노 / BGM·UI 앰비언스 = 스테레오 | 2D 게임이라 positional 팬 없음. 모노로 충분 |
| 라우드니스 | SFX 피크 ≈ −1 dBFS로 정규화 / BGM ≈ −14 LUFS(integrated) | 무음에서 출발하므로 헤드룸 확보 |
| 무음 트림 | 앞뒤 공백 제거, 필요한 어택만 남김 | |
| 루프 | BGM은 **이음매 없는 루프**(등파워 루프 포인트, 클릭 없음) | 파일 자체가 루프 구간 |
| 파일명 | `sfx_<분류>_<이름>.wav` / `bgm_<이름>.wav` | 아래 표의 ID를 그대로 파일명으로 |
| 라이선스 | **상업적 사용·재배포 가능한 생성 서비스만** (Suno/Udio 유료 티어, ElevenLabs SFX, Stable Audio 등) | 무료 티어 결과물은 대부분 상업 이용 불가 |

**톤 기준:** 판타지 아케이드 모바일. 카드·Chip 계열은 카지노/포커 뉘앙스(가벼운 청량함), 전투 계열은 JRPG 배틀(정제된 판타지 타격감). 과하게 리얼하지 않게, 게임적으로 다듬어서. 배경은 횃불 켜진 투기장 피트(ART_REQUEST §2 참고).

**표의 "성격" 칸이 곧 생성 프롬프트 씨앗이다.** 길이는 목표치이며 ±30%는 괜찮다.

---

## 2. SFX 목록

### 2.1 UI · 카드

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_ui_button` | 버튼 탭(교체·확정·전투 시작·판매 등 공용) | 0.08s | 짧고 마른 클릭, 나무/칩 두드리는 느낌 | – |
| `sfx_ui_card_deal` | 손패 딜(한 장 단위, 스태거로 여러 번 재생) | 0.12s | 카드가 테이블을 스치는 소리, 건조 | – |
| `sfx_ui_card_flip` | 카드 공개·뒤집기 | 0.15s | 빳빳한 종이 플립, 가벼운 "탁" | – |
| `sfx_ui_card_select` | 손패·트레이 카드 선택 토글 | 0.06s | 아주 짧은 픽 사운드, 피치 살짝 높음 | – |
| `sfx_ui_exchange` | 카드 교체 실행(선택 자리 드로우 교체) | 0.4s | 카드 여러 장 섞이며 새로 깔리는 소리 | – |
| `sfx_ui_chip_gain` | Chip 획득(판매 등 일반) | 0.3s | 동전/포커칩 딸깍 쌓이는 소리 | – |
| `sfx_ui_holdbonus` | 유지 보너스 Chip 획득(교체 아낀 보상) | 0.6s | `chip_gain`보다 화려한 상승 차임, "잘했다" 느낌 | – |
| `sfx_ui_shop_open` | 카드 상점 진열 열림(4장 등장) | 0.7s | 커튼/막 걷히는 느낌 + 카드 4장 촤르륵 | – |

### 2.2 소환 · 머지

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_unit_summon` | 족보 확정 → 대기 유닛 등장 | 0.5s | 마법진에서 유닛이 솟는 짧은 소환음, 반짝 | – |
| `sfx_unit_place` | 유닛을 칸에 배치 | 0.15s | 묵직한 "쿵", 돌바닥에 내려놓는 무게감 | – |
| `sfx_merge_star2` | ★1 → ★2 머지 | 0.5s | 상승 2음 차임, 중간 음역 | – |
| `sfx_merge_star3` | ★2 → ★3 머지 | 0.8s | `star2`보다 한 옥타브 위, 더 길고 화려한 반짝임 | – |
| `sfx_joker_promote` | 조커로 성급 강제 +1 | 1.0s | 머지보다 큰 "마법적" 연출, 상승 아르페지오 + 광채 | – |

### 2.3 전투 — 발사 (공격 패턴 5종 구분)

> **⚠ 2차 피드백 (2026-09-06): AI 생성으로는 실총 느낌이 안 나온다.** 텍스트→효과음 모델(ElevenLabs SFX 등)은 총성을 거의 항상 얇은 "pew/click"으로 만든다 — 학습 데이터 한계라 프롬프트를 아무리 다듬어도 잘 안 된다. **방향 전환: 실사 녹음 라이브러리에서 가져온다.**
>
> - **소스:** [freesound.org](https://freesound.org)(라이선스 필터 CC0), Sonniss GDC 무료 팩, Pixabay SFX 등 royalty-free 실사 총성.
> - **검색어:** `gunshot close mic dry` / `rifle single shot dry` / `9mm pistol shot` / `machine gun burst dry`. **"dry"(잔향 없음)·"close"(근접 마이크)·짧은 것**을 고른다 — 야외 에코가 길면 연사 시 뭉개진다.
> - **가공:** 앞의 무음 트림, 0.1~0.4초로 자르기, 피크 −1 dBFS 정규화, 모노 변환. 저역이 부족하면 짧은 sub 레이어(40~80Hz) 한 겹.
> - AI를 굳이 쓴다면 프롬프트를 "사건"이 아니라 "녹음"으로: `dry close-miked foley recording of a single rifle gunshot, sharp transient crack, tight low-end thump, no reverb, mono`. 그래도 기대치는 낮게.

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_fire_rapid` | 연사(連射) 유닛 발사 | 0.1s | 소형 자동화기 단발. 날카로운 크랙 + 단단한 저역 스냅, 아주 짧은 금속 잔향. 반복돼도 안 거슬리되 **가벼우면 안 됨** | – |
| `sfx_fire_heavy` | 강타(強打) 유닛 발사 | 0.35s | 대구경 라이플/샷건 단발. 폭발적인 보디 + 가슴 치는 저역 + 긴 테일, 강한 반동감 | – |
| `sfx_fire_multi` | 다중(多重) 유닛 발사 | 0.25s | 실총 계열 연발이 부채꼴로 겹쳐 터지는 소리. 개별 크랙이 뭉쳐 "드드득" | – |
| `sfx_fire_splash` | 범위(範圍) 유닛 발사 | 0.3s | 유탄 발사기/박격포 발사. "텅" 하는 무거운 발출 + 공기압, 화약 냄새 나는 저역 (착탄음은 §2.4 별도) | – |
| `sfx_fire_pierce` | 관통(貫通) 유닛 발사 | 0.3s | 레일건/관통탄. 고압 전자기 방전 "촤악" + 초음속 스냅, 묵직한 금속성. 얇은 SF 레이저 아님 | – |

### 2.4 전투 — 착탄 · 피격 · 처치

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_impact_hit` | 일반 피격(공용 타격) | 0.12s | 짧은 타격 "퍽", 살점/갑옷 중간 | – |
| `sfx_impact_splash` | 스플래시 폭발 착탄 | 0.4s | 압축된 폭발, 저역 + 흙먼지 | – |
| `sfx_impact_pierce` | 관통 빔이 여러 적을 꿰뚫음 | 0.3s | 연속 관통 "치칙칙", 금속 스침 | – |
| `sfx_enemy_death` | 잔챙이·일반 적 처치 | 0.25s | 짧은 소멸음, 팝 + 흩어짐 | – |
| `sfx_enemy_death_big` | 큰 적·보스 처치 | 0.8s | 무겁게 무너지는 붕괴음 + 저역 임팩트 | – |

### 2.5 보스

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_boss_appear` | 보스 등장(배너와 동기화) | 1.5s | 위협적인 등장 스팅어, 브라스 히트 + 저역 럼블 | – |

### 2.6 족보 확정 스팅어 (티어 3단)

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_hand_low` | 하이카드 ~ 트리플 확정 | 0.4s | 담백한 "툭", 확인음 수준 | – |
| `sfx_hand_mid` | 스트레이트 · 플러시 · 풀하우스 확정 | 1.0s | 상승감 있는 짧은 플러리시 | – |
| `sfx_hand_high` | 포카드 · 스트레이트플러시 이상 확정 | 2.0s | 잭팟, 화려한 팡파르 — [POLISH.md](POLISH.md) 2번 "상위 족보 축포"(골드 플래시)와 동기화 | – |

### 2.7 상태

| ID | 트리거 | 길이 | 성격 | 루프 |
|---|---|---|---|---|
| `sfx_state_life_loss` | 라이프 손실(웨이브 실패) | 0.7s | 경보성 하강음 — POLISH 2번 붉은 화면 플래시와 동기화 | – |
| `sfx_state_wave_clear` | 웨이브 클리어 | 1.2s | 짧고 밝은 승리 징글 | – |
| `sfx_state_game_over` | 게임 오버 | 2.5s | 하강하는 패배 모티프, 여운 | – |
| `sfx_state_stage_clear` | 50웨이브 완주 | 3.5s | 큰 팡파르, 클리어 보상감 | – |

---

## 3. BGM 목록

> **⚠ 2차 피드백 (2026-09-06): "올드하다".** 2차 결과물도 촌스러웠다 — AI 음악 생성기(Suno/Udio)에 형용사만 주면 "제네릭 RPG 배틀 테마 / 싸구려 MIDI 오케스트라 / 2000년대 JRPG 신스" 쪽으로 흐른다. **해결: 장르 태그 + 레퍼런스 아티스트/트랙명을 직접 박고, 낡은 것들을 명시적으로 배제한다.**
>
> - **장르 태그(하나 골라 밀기):** modern midtempo bass / phonk battle / aggressive drum & bass / hard techno / hybrid trailer (2020s). 프리 텍스트 서술 말고 이 태그를 프롬프트 맨 앞에.
> - **레퍼런스(예):** "in the style of a modern rhythm-game boss theme", "Muzzy / Pegboard Nerds style DnB", "Carpenter Brut style dark synthwave" 처럼 실제 아티스트·씬을 지목.
> - **배제 명시:** `no chiptune, no 8-bit, no cheesy MIDI orchestra, no early-2000s JRPG synth, no generic epic trailer`.
> - **프로덕션 키워드:** `modern production, heavy sidechain, saturated bass, crisp transients, wide stereo, loud master`.
> - **BPM:** 140~160. **길이 60~90초 이음매 없는 루프**, 인트로·아웃트로 없이.
> - 도구: 같은 프롬프트라도 Udio가 Suno보다 "요즘 프로덕션"에 가깝게 나오는 편. 여러 후보를 뽑아 게임에 임시로 넣고 고른다.

| ID | 사용 구간 | 길이(루프) | 성격 |
|---|---|---|---|
| `bgm_plan` | 드로우·교체·배치·상점 페이즈 | 60–90s | 차분하지만 긴장감 있는 포커 테이블 무드. 저강도. `bgm_combat`과 같은 조성·BPM 계열이라 크로스페이드가 자연스럽게 |
| `bgm_combat` | 전투 진행 중 | 60–90s | **강렬한 비트 중심의 배틀 트랙.** 묵직한 드럼이 미는 그루브 + 공격적 베이스 + 130~150 BPM. (위 피드백 박스 참고) |
| `bgm_boss` | 보스 웨이브(10·20·30·40·50) | 60–90s | `bgm_combat`의 위협 강화 버전, 브라스·타악·저역 프레셔 더 세게 *(선택)* |
| `bgm_result` | 결과 화면(클리어·게임오버 공용 베드) | 30–45s | 잔잔한 마무리 루프 *(선택)* |

> POLISH.md §1은 "Act별 트랙 또는 강도 레이어 1개"를 열어 뒀다. **최소 구성은 `bgm_plan` + `bgm_combat` 2곡 크로스페이드**이고, 여유가 되면 `bgm_boss`를 추가한다.

---

## 4. 우선순위

체감 대비 개수를 줄이려면 이 순서로 받는다.

**1순위 — 최소 플레이 세트 (13종):** `sfx_ui_button` · `sfx_ui_card_deal` · `sfx_ui_chip_gain` · `sfx_unit_place` · `sfx_merge_star2` · `sfx_merge_star3` · `sfx_fire_rapid` · `sfx_impact_hit` · `sfx_enemy_death` · `sfx_hand_high` · `sfx_state_life_loss` · `sfx_state_wave_clear` · `bgm_combat`

**2순위 — 구분감:** 발사 나머지 4종(`heavy`/`multi`/`splash`/`pierce`) · `sfx_impact_splash` · `sfx_impact_pierce` · `sfx_boss_appear` · `sfx_enemy_death_big` · `sfx_hand_low` · `sfx_hand_mid` · `sfx_unit_summon` · `bgm_plan`

**3순위 — 마감:** `sfx_ui_card_flip` · `sfx_ui_card_select` · `sfx_ui_exchange` · `sfx_ui_holdbonus` · `sfx_ui_shop_open` · `sfx_joker_promote` · `sfx_state_game_over` · `sfx_state_stage_clear` · `bgm_boss` · `bgm_result`

---

## 5. 생성 AI 사용 메모

- **SFX:** ElevenLabs Sound Effects, Stable Audio 등 텍스트→효과음. "성격" 칸을 프롬프트로 쓰고, 길이·"게임 UI/전투 사운드 이펙트, 드라이, 리버브 최소" 같은 제약을 덧붙인다.
- **BGM:** Suno / Udio. 루프용이라 인트로·아웃트로 없이 "seamless loop, no fade" 요청. 조성(예: A minor)·BPM을 `bgm_plan`/`bgm_combat` 동일하게 고정해야 크로스페이드가 붙는다.
- 여러 후보를 뽑아 프로젝트에 임시로 넣고 실제 재생 맥락에서 고르는 편이 프롬프트를 다듬는 것보다 빠르다(ART_REQUEST와 같은 결론).

V2 검증 기록: 오디오 EditMode 9/9 통과. 새 WAV 33개 유한 샘플·피크·BGM 루프 경계 검증 완료. 청감 승인과 모바일 청취는 별도.

### V2 비트 보강 (2026-09-06)
분위기는 유지하되 잘 들리지 않는다는 피드백 반영. 계획 단계에 약한 드럼 그루브 추가, 스네어/하이햇 선명도와 전투 드럼 비중 증가. PCM RMS 실측 계획 -20, 전투 -15.5, 보스 -15.10 dBFS로 이전 대비 약 +3/+2.5/+1.9 dB. 피크 -3 dBFS 이하, 기존 루프 길이/경계 유지. 이전 음악은 Artifacts/Audio/BeforeBeatLift에 보관. 효과음과 결과 음악은 동일.

### 드라이빙 그루브 수정 (2026-09-06)
어둡되 신나는 비트 요청: 기존 하프타임을 4박 킥·2/4 백비트·엇박 오픈햇·싱코페이션 베이스로 재편. 베이스 중저역 배음과 보스 퍼커션 보강. 계획/전투/보스 RMS -18.5/-14/-13 dBFS, 씬 musicVolume 0.55. 피크/유한 샘플/루프 경계 검증. 이전 음악 BeforeDrivingGroove에 보관, 음악 단독 미리듣기 PolishV2/BGM_Driving_Preview.wav.

2026-09-06: 준비/일반 전투/보스/결과 BGM 참조 모두 선택한 기타 반주 bgm_combat로 통일. 이전 bgm_plan/boss/result WAV는 현재 Game 씬에서 미사용 보관. AudioPolishSetup도 동일 연결 적용.
