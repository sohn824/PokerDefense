# 프로파일링 사례 — AudioManager.LateUpdate GC 할당

> 2026-09-21, Unity Profiler(Hierarchy 뷰)로 실측. Play Mode에서 손패 확정→배치→전투까지 실제 플레이를 수행하며 기록했다. 아래 수치는 화면 캡처로 확보한 실측값이며, 추정치가 아니다.

## 발견 경위

`CombatContext.cs`(사거리·타겟·쿨다운 처리)를 먼저 의심했으나 이미 필드 재사용(`shotTargets`/`enemies`/`cooldowns`/`aims`를 `.Clear()`로 재사용, LINQ·`new List<>` 없음)으로 잘 최적화돼 있어 핫스팟이 아니었다.

Profiler Hierarchy에서 `PlayerLoop → UpdateScene → PreLateUpdate.ScriptRunBehaviourLateUpdate → LateBehaviourUpdate`를 드릴다운한 결과, `AudioManager.LateUpdate() [Invoke]`가 프레임당 **GC Alloc 2.6 KB, GC.Alloc 호출 36회, Time 0.98ms**로 Scripts 레이어 전체 할당(3.3KB)의 대부분을 차지하고 있었다. 연속된 두 프레임(56097, 56098)에서 동일한 수치를 확인해 우연이 아님을 검증했다.

## 원인

`AudioManager.LateUpdate()`는 보이스 16개 루프 안에서 `AudioPreferences.EffectsGain`을, 음악 크로스페이드 블렌딩에서 `AudioPreferences.MusicGain`을 두 번 더 읽는다. `AudioPreferences`의 각 프로퍼티(`Master`/`Music`/`Effects`/`Muted`/`MusicGain`/`EffectsGain`)는 접근할 때마다 `PlayerPrefs.GetFloat/GetInt`를 문자열 키 조립(`Prefix + key`)과 함께 새로 호출한다 — 캐싱이 전혀 없어 프레임당 18회 이상 반복 할당이 발생했다.

## 조치

[AudioManager.cs](../Assets/_Project/Scripts/UI/AudioManager.cs)의 `LateUpdate()`에서 `EffectsGain`/`MusicGain`을 루프 진입 전 지역 변수로 한 번만 읽어 재사용하도록 수정. 동작은 동일하고 읽기 횟수만 줄었다(behavior-preserving).

## 결과 (동일 방법으로 재측정)

| 지표 | 수정 전 | 수정 후 | 변화 |
|---|---|---|---|
| GC Alloc | 2.6 KB | 288 B | -89% |
| GC.Alloc 호출 수 | 36 | 4 | -89% |
| Time (Self) | 0.98 ms | 0.16 ms | -84% |

검증: `Assets > Refresh` 후 컴파일 오류 없음 확인, EditMode 225/225 통과(`AudioManager.LateUpdate`는 MonoBehaviour 콜백이라 직접 단위테스트 대상은 아니며, 이번 변경은 동작을 바꾸지 않는 리팩터라 테스트 수는 그대로다).

## 제한

이 측정은 Editor Play Mode, 데스크톱 환경 1회 세션 기준이다. 모바일 실기기에서의 절대 수치는 별도 확인이 필요하다(POLISH.md PERF-01 참조). 다만 "프레임당 반복 PlayerPrefs 호출을 캐싱으로 없앤다"는 조치 자체는 플랫폼에 무관하게 유효하다.
