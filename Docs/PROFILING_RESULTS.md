# 프로파일링 사례 — AudioManager.LateUpdate의 GC 할당

> 2026-09-21, Unity Profiler의 Hierarchy 뷰로 측정했다. Play Mode에서 손패 확정 → 배치 → 전투까지 실제로 플레이하며 기록했고, 아래 수치는 모두 화면 캡처로 남긴 실측값이다.

## 어떻게 찾았나

처음에는 전투 처리(`CombatContext.cs` — 사거리·타겟·쿨다운)를 의심했다. 하지만 이미 리스트를 `.Clear()`로 재사용하고 LINQ나 `new List<>`도 쓰지 않아서 문제가 될 곳이 없었다.

그래서 Profiler Hierarchy에서 `PlayerLoop → UpdateScene → PreLateUpdate.ScriptRunBehaviourLateUpdate → LateBehaviourUpdate` 순으로 내려가 봤다. 그 결과 `AudioManager.LateUpdate()`가 프레임마다 **GC Alloc 2.6KB, GC.Alloc 호출 36회, 0.98ms**를 쓰고 있었다. 스크립트 전체 할당(3.3KB)의 대부분이다. 연속된 두 프레임(56097, 56098)에서 같은 수치가 나와 일시적인 튐이 아님을 확인했다.

## 원인

`AudioManager.LateUpdate()`는 보이스 16개를 도는 루프 안에서 `AudioPreferences.EffectsGain`을 읽고, 음악 크로스페이드에서 `AudioPreferences.MusicGain`을 두 번 더 읽는다.

문제는 `AudioPreferences`의 프로퍼티(`Master`, `Music`, `Effects`, `Muted`, `MusicGain`, `EffectsGain`)가 읽을 때마다 `PlayerPrefs.GetFloat/GetInt`를 새로 호출한다는 점이다. 호출할 때마다 키 문자열(`Prefix + key`)도 새로 만든다. 값을 저장해 두지 않으니 프레임마다 18번 넘게 같은 조회와 할당이 반복됐다.

## 조치

[AudioManager.cs](../Assets/_Project/Scripts/UI/AudioManager.cs)의 `LateUpdate()`에서 `EffectsGain`과 `MusicGain`을 루프에 들어가기 전에 지역 변수로 한 번만 읽어 두고, 루프 안에서는 그 값을 쓰도록 바꿨다. 동작은 그대로이고 읽는 횟수만 줄었다.

## 결과 (같은 방법으로 다시 측정)

| 지표 | 수정 전 | 수정 후 | 변화 |
|---|---|---|---|
| GC Alloc | 2.6 KB | 288 B | -89% |
| GC.Alloc 호출 수 | 36 | 4 | -89% |
| Time (Self) | 0.98 ms | 0.16 ms | -84% |

**검증:** `Assets > Refresh` 후 컴파일 오류가 없었고, EditMode 225/225가 통과했다. `LateUpdate`는 MonoBehaviour 콜백이라 단위 테스트로 직접 검사하지는 않는다. 동작을 바꾸지 않는 수정이므로 테스트 수도 그대로다.

## 한계

에디터 Play Mode, 데스크톱에서 한 세션을 측정한 결과다. 모바일 실기기에서의 수치는 따로 확인해야 한다(POLISH.md PERF-01). 다만 "프레임마다 반복하던 PlayerPrefs 조회를 한 번으로 줄인다"는 조치 자체는 플랫폼과 상관없이 효과가 있다.
