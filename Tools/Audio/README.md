# Poker Defense audio build

Local-only experiments: `analyze_rock_reference.py`, `audition_melody_sketches.py`
and `compose_iron_oath.py` are ignored and are not included in a fresh clone.
Their sections below document past auditions. Keep `compose_arena_breaks.py`
and `compose_arena_drive.py`: the selected BGM generator imports the latter's
instrument helpers. Backups, previews, downloaded samples and PythonDeps are
already excluded by `/Artifacts/`.

## Arena Breaks audition (2026-09-11)

Run `python Tools/Audio/compose_arena_breaks.py` (NumPy; imports the instrument
helpers in `compose_arena_drive.py` without rendering that score). This creates
an original 124 BPM / 77.42-second minor funk/breakbeat candidate, with clean
minor-seventh/ninth guitar chops, octave bass replies, ghost snares and a separate
break/build/lift. No lead melody or reference recording samples are included.
Outputs under `Artifacts/Audio/ArenaBreaks`: loop, full listening preview, and
30.97-second highlight. Sources remain the documented CC0 guitar/VSCO libraries.
Reference: user-supplied alexguz MP3, locally measured at 124.84 seconds with
strong tempo candidates around 123–127 BPM; this is signal analysis, not direct
listening or exact transcription. Output PCM RMS -15.90 dBFS / sample peak -1.50
dBFS; no clipping, matching loop endpoints. Previous candidates and runtime audio
remain unchanged. Do not interpret these measurements as listening approval.


## Arena Drive audition (2026-09-11)

`python Tools/Audio/compose_arena_drive.py` renders an original 100 BPM,
76.8-second riff-led rock candidate under `Artifacts/Audio/ArenaDrive`.
Files: `Arena_Drive_loop.wav`, `Arena_Drive_full_preview.wav`, and a 28.8-second
`Arena_Drive_highlight.wav`. No separate lead melody; power-chord riffs, bass,
kit-like drums, breakdown and final lift. Requires NumPy and existing CC0 guitar
and VSCO source banks documented below. Runtime assets are hash-checked unchanged.

`python Tools/Audio/analyze_rock_reference.py <local-mp3>` measures the supplied
reference locally with NumPy and SoundFile (optional packages in ignored
`Artifacts/Audio/PythonDeps`). It does not upload or copy reference audio into
Assets. The supplied magpiemusic recording measures 126.09 seconds, with strong
24–40s and 104–120s passages; tempo detection is ambiguous around 100/133 BPM.
100 BPM is this candidate's arranging choice, not a confirmed reference tempo.
The reference was analyzed as signal data, not directly auditioned. No portion
of that recording is included in the composition. Output PCM measures -14.50 dBFS
RMS / -1.58 dBFS sample peak; this is not LUFS or true-peak certification.


## Melody-only auditions (2026-09-10)

Run `python Tools/Audio/audition_melody_sketches.py` with NumPy and the existing
VSCO source bank. Outputs: `Artifacts/Audio/MelodySketches/{A_Resolute,B_Defiance,C_Horizon}.wav`.
Three original single-voice sketches, 17.74 seconds each, 112 BPM, identical CC0 harp
timbre and -20 dBFS RMS. No drums, chords, bass, guitar backing or added reverb.
A uses sparse falling gestures; B repeated notes and offbeat leaps; C longer rising
arches. This is a melody selection exercise, not the final instrument arrangement.
Runtime assets remain unchanged. Source/PCM measurements and note events accompany
the files. No claim of reference-song transcription or direct audio analysis.

## New audition: Iron Oath (2026-09-10)

`python Tools/Audio/compose_iron_oath.py` creates an original 128 BPM, D-minor,
90-second battle theme under `Artifacts/Audio/IronOath`. Outputs are a seamless
loop, a full listening copy with an ending fade, and a 30-second climax excerpt.
This is an audition only: it does not replace runtime audio. Requires NumPy and
existing FreePats guitar / VSCO source folders described in
[GUITAR_CHOIR_CANDIDATES.md](GUITAR_CHOIR_CANDIDATES.md) and
[ORCHESTRA_SOURCES.md](ORCHESTRA_SOURCES.md), both CC0 for the samples used here.
No voice samples are used. Reference supplied by user: Farland Saga 2, Storm
PART2 (폭풍 PART2). Reference audio could not be accessed; the arrangement follows
the user's solemn, resolute battle mood brief, without transcribing that track.
Measurements and source hashes accompany the WAVs; RMS/sample peak are not LUFS
or true-peak measurements. Current selected game music remains unchanged.

## Selected game BGM: Arena Breaks (2026-09-11)

The user selected Arena Breaks. Runtime `Assets/_Project/Audio/bgm_combat.wav`
is byte-identical to `Artifacts/Audio/ArenaBreaks/Arena_Breaks_loop.wav`:
124 BPM, 77.419 seconds, 48kHz stereo PCM16, no ending fade. Preparation, combat,
boss and result scene slots all reference this same asset. Existing GUID, importer
settings and volume are preserved. Rebuild with `compose_arena_breaks.py`, then
copy the loop as the FINAL override after any historical builders. Do not install
the faded preview. Previous runtime WAV/meta are backed up under ignored
`Artifacts/Audio/BeforeArenaBreaksInstall/<previous-sha256>/`.

## Git ownership

- Commit `Assets/_Project/Audio/*.wav` and their `.meta` files: these are the
  current runtime assets (52 SFX variants and 4 BGM tracks). `_v2` and `_v3`
  suffixes are actively used sound variations, not obsolete versions.
- Commit the Python sources in this folder, the Unity audio setup/runtime/tests,
  and relevant scene/document changes. The base builders remain dependencies
  of a complete rebuild; do not delete them as obsolete iterations.
- `Artifacts/` is local-only and ignored by Git. It contains previous audio,
  rendered previews, validation reports and UI review captures. Existing files
  remain available locally; a fresh clone does not include these artifacts.
- Python cache files and `.claude/settings.local.json` are also local-only.
- Do not ignore WAV files globally or remove Unity `.meta` files. A fresh clone
  uses the committed runtime audio directly; Python is only needed to regenerate it.


## Current combat version: sampled orchestra

Combat music now uses real CC0 VSCO 2 CE strings, horn, harp, bass drum and
timpani instead of harmonic-series instrument approximations. New C-minor accented theme, rising answer and continuous battle percussion replace
the previous score; the 150 BPM/76.8s loop remains; performance variation and a longer
hall reflection field soften the mechanical quality. Source license and rebuild:
[ORCHESTRA_SOURCES.md](ORCHESTRA_SOURCES.md). Run `fetch_orchestra.py` once before
`rework_combat_theme.py`. Current music preview is under `Artifacts/Audio/ArenaSmooth`.
The G36 rifle is unchanged; its preview remains under FantasyMelodyRework.

## Previous instrument synthesis / current G36 rifle

Latest overrides (run after modern_combat_audio.py):

1. `python Tools/Audio/rework_combat_theme.py` — combat music only.
2. `python Tools/Audio/edit_recorded_rifle.py` — three royal rifle variants only.
   This requires the CC0 recording described in [FIREARM_SOURCES.md](FIREARM_SOURCES.md).

Combat music uses synthesized bowed strings, low string ostinato, horn melody,
lute-like plucks and membrane war drums with diffuse hall reflections. Electronic
kick pitch dives, dance hats, tempo delay and sidechain pumping are removed.
These are synthesized instrument approximations, not orchestra recordings.
It has separate eight-bar A/B melodies, horn-to-string handover, phrase rests,
and six sections: theme
(0s), answer (12.8s), breathing space (25.6s), build (32s), theme return (38.4s),
turnaround (64s). 150 BPM / 76.8s / E minor, target -14 dBFS RMS with a -2 dBFS
sample peak ceiling (see current validation report for measured values). Plan/boss/result remain the preceding arrangements; only the beat
grid and tonal family are shared, not identical stems across the new combat track.

The royal rifle now uses CS279's CC0 G36-E Fire public OGG preview with three
tail/channel edits, trimmed to 0.18s, filtered and moderately compressed.
It is a G36-labelled effect; actual live-fire provenance is unverified and the
source is compressed. This replaces the M4 edits. Source provenance and hashes are in
FIREARM_SOURCES.md; raw downloads remain in ignored Artifacts.

Current auditions: `Artifacts/Audio/FantasyMelodyRework/Recorded_Rifle_Preview.wav`,
`Combat_Theme_Full_Preview.wav`, and `Combat_and_Recorded_Rifle.wav`.
These use game gains but are offline mixes, not Unity recordings. A fresh clone
plays the committed output files directly without Python or source downloads.

## Previous version: modern dark groove + synthesized royal rifle

Run `python Tools/Audio/modern_combat_audio.py` **after** the V2 refinement step.
It installs four new score arrangements and three dedicated royal rifle variants.
Current music: syncopated saturated mid-bass, short minor-key FM hook, tight
four-on-the-floor drums, 8-bar fills and phrase variation. 150 BPM and the existing
76.8/38.4-second sample grid are preserved. Measured RMS: -17/-14/-13.5/-23 dBFS
(plan/combat/boss/result), PCM peak ceiling -2 dBFS; musicVolume stays 0.55.
This is not a LUFS or true-peak certification.

Royal rifle uses 0.155-second nonperiodic crack/body/bolt layers, synthesized from
scratch. The reference link is https://www.youtube.com/watch?v=LVp5yZ6T_Xs
(AK-12 firing video); browser title/frame were checked, but its audio was not
directly auditioned or sampled. No claim of an exact acoustic reproduction.
The royal unit's id selects this cue; other Multi units keep their existing cue.
Its 0.04-second cooldown rejects duplicate hit events while allowing 13.5 shots/s.

Current previews: `Artifacts/Audio/ModernCombat/Modern_BGM_Preview.wav`,
`Royal_Rifle_Preview.wav`, `BGM_and_Royal_Rifle.wav`. Rifle audition: singles,
then 6, 9.6 and 13.5 shots/s at in-game gain. The combined preview is an offline
mix, not a Unity recording or full crowd/voice-budget simulation. Validation and
backups are in `ModernCombat` and `BeforeModernCombat`, both ignored by Git.

## Previous version: tactile / restrained V2

Latest music revision: **driving dark club groove**, replacing the earlier halftime
beat. Four-on-the-floor kick, backbeats on 2/4, open offbeat hats, syncopated bass
with stronger audible harmonics, and extra boss percussion. Plan/combat/boss RMS
is -18.5/-14/-13 dBFS; scene musicVolume is 0.55. The shared 150 BPM / 76.8-second
grid remains. Earlier BGM is in `Artifacts/Audio/BeforeDrivingGroove`.
`BGM_Driving_Preview.wav` is a music-only montage at the current in-game gain.
The historical halftime/beat-lift description below documents earlier iterations.

The initial bright-chime/synthwave pass below was rejected in listening feedback.
Current audio uses paper-fibre grains, card flex/landing transients, shorter closure
accents, and a 150 BPM halftime percussion/sub-bass score without a retro lead or
continuous arpeggio. This remains synthesized foley, not recorded cards.

After the two base build steps below, run **`python Tools/Audio/refine_table_audio.py`**
before applying the Unity setup menu. It replaces 25 existing files and adds eight
paper variants: the full pack now contains **49 SFX files / 31 cues + four BGM files**.
The same script preserves previous versions under `Artifacts/Audio/BeforePolishV2`.
For V2-only edits, rerun this script directly; do not rerun the old builders afterward.

Then run `python Tools/Audio/render_audio_preview.py` for the updated montage.
Current files, numeric validation and isolated card/reward audition live under
`Artifacts/Audio/PolishV2`. Paper variants keep pitch fixed. Card buttons no longer
receive the generic click. Frequent merge/wave/bonus cues use reduced gains.

V2 SFX peak target: -3 dBFS. Beat-lift revision: add a restrained drum bed to plan,
increase backbeat/hat definition and foreground the combat drums. Music RMS targets:
plan -20, combat -15.5, boss -14.5, result -24 dBFS. The -3 dBFS peak ceiling takes
precedence (boss measures about -15.1 dBFS RMS). Prior BGM files are preserved under
`Artifacts/Audio/BeforeBeatLift`. Previous-version details below remain for reproducibility.

Original procedural synthesis, Python 3 + NumPy. No recordings, external samples,
artist tracks, model downloads, or paid generation services are used in this pass.
The firearm sounds are designed game effects, **not real firearm recordings**.

## Rebuild

1. `python Tools/Audio/compose_neon_siege.py --game-pack`
2. `python Tools/Audio/build_game_audio.py`
3. `python Tools/Audio/refine_table_audio.py`
4. `python Tools/Audio/modern_combat_audio.py`
5. `python Tools/Audio/rework_combat_theme.py`
6. Obtain the source per FIREARM_SOURCES.md, then `python Tools/Audio/edit_recorded_rifle.py`
7. Let Unity import, then run **Tools > Poker Defense > Apply Audio Polish** with
   the Game scene open in Edit Mode. This updates audio import settings and the
   manager's cue references, then saves that scene. Preserve unrelated unsaved
   changes before running it manually.

The installer retains existing asset GUIDs. The first pre-pass WAVs and their
import metadata are preserved in `Artifacts/Audio/BeforePolish`; subsequent builds
do not replace that backup. Compare before restoring; those files predate this pass.

## Contents

- 31 SFX cues; the five weapon cues each have three variants (41 files).
- 150 BPM / E-minor-related plan, combat, boss arrangements: 48 bars / 76.8 s.
- Result bed: 24 bars / 38.4 s. All masters are stereo 48 kHz / 24-bit PCM WAV.
- SFX: mono 48 kHz / 24-bit, peak -1.5 dBFS; musical cues have fixed pitch at runtime.
- BGM RMS targets: plan -23, combat -17, boss -16, result -24 dBFS.
  These are **RMS, not LUFS**; the original -14 LUFS brief has not been certified.
  Lower levels leave space for game feedback. Inspector gains remain editable.
- SFX stay PCM / DecompressOnLoad to preserve transients; BGM uses Vorbis quality
  0.85 / Streaming. Compressed playback and transitions still need device listening.

## Runtime

Two music sources crossfade over 0.8 s with matching playback sample positions.
Related arrangements share a bar grid. Rapid reversals reuse a playing track.
The UI layer reads final combat/flow state in LateUpdate, including immediate
round transitions and the result screen. Boss music follows actual Boss entries
in wave data, including the mini-boss wave, rather than a hardcoded modulo.

Eight combat and eight feedback voices are reused; no per-shot GameObject creation.
Cue cooldowns use DSP time. Combat cannot consume the feedback pool. Aggregate
source gain budgets are 0.38 for combat and 0.32 for feedback; saturated pools drop
events rather than steal a ringing cue. Important stingers duck music to 55%.

## Verification and listening

`Artifacts/Audio/Polish/validation.json` records PCM duration, channels, peak/RMS,
loop endpoint delta, and SHA-256 for each installed file. This is signal validation,
not a listening review or a true-peak/loudness-standard certification.

`Artifacts/Audio/Polish/SFX_audition.wav` plays the effects in order; the adjacent
JSON lists each cue's timestamp. Listen on headphones and the target phone at
normal volume, particularly dense combat, bass audibility, rewarding versus
intrusive stingers, and the 76.8-second music seam.

EditMode tests cover all weapon mappings, DSP cooldown isolation, protected
feedback classification, every clip/variant, and imported music lengths/settings.
Gameplay rules and balance data are not modified by this audio pass.

Card deal now uses one 0.38-second hand-fanning gesture per new hand, with
three variants and a 0.45-second overlap guard. The refinement script uses
an isolated random seed for this gesture to preserve the remaining audio.

Current arena revision: sustained low brass, low bowed background and bass pulses. Removed metal ticks, air/transition noise, ghost snares, fills and repeated short strings. Softer hats; kick and snare backbeat retained.

2026-09-06: 준비/일반 전투/보스/결과 BGM 참조 모두 선택한 기타 반주 bgm_combat로 통일. 이전 bgm_plan/boss/result WAV는 현재 Game 씬에서 미사용 보관. AudioPolishSetup도 동일 연결 적용.

Current pistol override: run `python Tools/Audio/edit_recorded_pistol.py` after base audio builders. Dedicated Scout/Gunslinger cue; see FIREARM_SOURCES.md. Current pack: 33 cues, 55 SFX WAVs + 4 BGM WAVs.

## Current commit scope (2026-09-06)

Commit bgm_combat plus all 55 active SFX WAVs and their metadata (56 runtime
WAVs total). The unreferenced plan/boss/result WAVs and their metadata are now
ignored local history. Tests validate the selected combat loop, not retired files.
The unselected choir downloader and local VOICE_LICENSE copy are ignored;
voice source/render directories retain the MIT notice under Artifacts. Current
game audio uses no voice samples. Keep guitar source provenance, current generators,
Unity code/scene, and .meta files for active assets in Git.
