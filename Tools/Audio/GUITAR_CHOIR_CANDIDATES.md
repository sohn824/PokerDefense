# Guitar / male-voice choir auditions (2026-09-06)

Run `python Tools/Audio/audition_guitar_choir.py` after obtaining the sources below.
It renders two 76.8-second, 150 BPM candidates under ignored
`Artifacts/Audio/GuitarChoirCandidates`, with identical drums/bass/background,
no brass, and -17 dBFS RMS matching. `*_loop.wav` are loopable; `*_preview.wav`
have a one-second ending fade. These are audition levels, not installed game
assets. The script checks that every runtime audio/metadata hash stays unchanged.
RMS matching is not perceptual LUFS matching or a listening-quality certification.

## Guitar

- FreePats FSBS Electric Guitar Clean #1, bridge small, version 2026-08-07.
- https://freepats.zenvoid.org/ElectricGuitar/clean-electric-guitar.html
- CC0 1.0. Actual sampled electric guitar, edited here with a short damping
  envelope, saturation and cabinet-like filtering, low fifths and stereo layering.
  The palm-mute character is an edit, not a separately recorded palm-mute sample.
- Download: https://github.com/freepats/electric-guitar-FSBS-clean/releases/download/2026-08-07/EGuitarFSBS-clean-bridge-small-SFZ%2BWAV-20260807.7z
- Extract with full 7-Zip to `Artifacts/Audio/GuitarSource`, preserving paths.
  The archive uses Delta+BZip2; Windows tar, py7zr and reduced 7zr did not support it.
- 13 source WAVs, pitch mapping follows the supplied SFZ (C2 = MIDI 36).

## Male voices

- vocobox Human Voice Dataset: https://github.com/vocobox/human-voice-dataset
- Revision `77248fc69fd93c40a69d49c0cade4144c5d7a9f4`, MIT, Copyright (c) 2014 vocobox.
- Full notice: `Artifacts/Audio/VoiceSource/LICENSE` (local source download; not in Git). Include this notice when
  distributing the derived choir candidate; a copy accompanies the audition WAVs.
- Singer metadata labels the source male. It is one singer, layered and gently
  detuned to evoke an ensemble, NOT a recording of a choir and not synthesized vowels.
- `fetch_voice_candidates.py` downloads 18 baseline vowel notes plus license and
  metadata, verifies Git blob hashes, and writes a SHA-256 manifest.
  First obtain the pinned recursive GitHub tree as `Artifacts/Audio/voice-tree.json`:
  https://api.github.com/repos/vocobox/human-voice-dataset/git/trees/77248fc69fd93c40a69d49c0cade4144c5d7a9f4?recursive=1
- Source vowels are short; overlapping middle segments extend the sustains.
  Ensemble layering and resampling can have a processed character.

Output reports contain measured peaks, RMS, duration, seam difference and source
hashes. Source libraries, utilities and auditions remain local under Artifacts.
Current runtime combat music remains ArenaSmooth; neither candidate is selected.

## Developed guitar candidate

Run `python Tools/Audio/audition_guitar_choir.py --guitar-developed`.
Output: `Artifacts/Audio/GuitarDeveloped/guitar_preview.wav` and `guitar_loop.wav`.
Four low riff patterns, an eight-bar sustained lead phrase, a separate lower
response from 25.6s, theme return at 38.4s and octave reinforcement later.
The lead uses longer damping and less saturation than the rhythm guitar.
Backing verified sample-identical to the original candidates; RMS remains -17 dBFS.
Peak -2.42 dBFS; finite, unclipped, loop endpoint delta zero. All runtime assets
and metadata remain unchanged. This is a review candidate, not yet selected.

## Alternative lead only

`python Tools/Audio/audition_guitar_choir.py --lead-alternative` creates
`Artifacts/Audio/GuitarLeadAlternative/guitar_preview.wav`. A lower-register,
shorter, syncopated call/answer replaces the developed lead; its octave doubling
is removed. Shared backing and rhythm guitar arrays verified sample-identical
before mastering against GuitarDeveloped. Mix processing can change their final
balance slightly. RMS -17 dBFS, peak -2.25 dBFS, seam delta zero. Runtime unchanged.

## Rhythm only

`python Tools/Audio/audition_guitar_choir.py --no-lead` exports
`Artifacts/Audio/GuitarRhythmOnly/guitar_preview.wav` and `guitar_loop.wav`.
No main lead or octave doubling. Backing/rhythm arrays match GuitarLeadAlternative
exactly before mastering. 76.8s, RMS -17 dBFS, peak -1.94 dBFS, seam delta zero.
Runtime audio and metadata unchanged.

## Selected runtime (2026-09-06)

User selected GuitarRhythmOnly. Its guitar_loop.wav is now installed unchanged as bgm_combat.wav. Other candidates remain local auditions. Rebuild then copy this loop as the final override, preserving metadata.
