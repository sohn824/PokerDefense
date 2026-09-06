# Combat score sample provenance

The latest combat score is an original composition rendered with real instrument
multisamples from **VS Chamber Orchestra 2 Community Edition**, Versilian Studios
/ Sam Gossner and contributors. It does not sample a finished song.

- Publisher: https://versilian-studios.com/vsco-community/
- Repository: https://github.com/sgossner/VSCO-2-CE
- License: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/
- Pinned revision: `440300901dfe9275fd84e0b7763af1f8443ae62e`.
- Retrieved 2026-09-06. Subset: 114 WAVs covering sustained horn, violin/cello
  sections, cello spiccato with alternating takes, harp, bass drum, timpani and two snare takes.
- `fetch_orchestra.py` selects the subset, verifies Git blob hashes against the
  pinned tree and writes a per-file SHA-256 manifest in local source storage.
- Kick low-body reinforcement, hi-hat and transition noise are procedurally synthesized.

Rebuild with Python and NumPy:

```text
python Tools/Audio/fetch_orchestra.py
python Tools/Audio/rework_combat_theme.py
```

The download step needs network access. Samples, tree, manifest, backups and
auditions live under ignored `Artifacts/Audio`. The committed runtime WAV is
self-contained: no sample library, Python, plugin or download is needed to play.

The renderer uses nearest-note multisamples (source C3 = MIDI 60), preserves
recorded stereo, short release fades, small timing/velocity variations and
distributed hall reflections. It is an offline sample arrangement, not a live
orchestra performance or a full legato sample engine. Sample peaks are measured;
LUFS/true-peak certification and subjective listening approval are not claimed.

Current output: `Artifacts/Audio/ArenaSmooth/Combat_Theme_Full_Preview.wav`.
Previous orchestral score: `Artifacts/Audio/BeforeArenaSmooth/bgm_combat.wav`.
Only `bgm_combat.wav` is replaced, retaining its Unity metadata.

Current arena revision: sustained low brass, low bowed background and bass pulses. Removed metal ticks, air/transition noise, ghost snares, fills and repeated short strings. Softer hats; kick and snare backbeat retained.
