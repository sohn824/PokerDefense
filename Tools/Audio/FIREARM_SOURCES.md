# Royal rifle source

## Current: G36-E Fire (2026-09-06)

- Creator: CS279. Source: https://freesound.org/people/CS279/sounds/196400/
- License: CC0 1.0. The page labels this G36-E Fire; live-fire recording origin
  is not independently verified. This is a different assault-rifle-labelled
  effect, not an AR-15 platform recording.
- Public preview used: https://cdn.freesound.org/previews/196/196400_3293865-lq.ogg
- OGG SHA-256: `7784bdfaf1ae1d4c4b0eff28f7e3549f57110ee5d15f01df3d001c884154a712`.
- Original WAV requires a Freesound login; this edit uses the compressed public
  preview, not the original lossless recording. Exporting 24-bit does not restore
  missing source detail.
- Decode using soundfile 0.14.0 / NumPy: `a, rate = soundfile.read(path, always_2d=True)`;
  write `a / max(1, float(numpy.max(numpy.abs(a)))) * .95` with subtype `PCM_24`
  to `Artifacts/Audio/FirearmSource/g36-decoded.wav` at the source rate (44100 Hz).
  Normalize before PCM conversion because Vorbis decoded peaks exceed 1.
- Decoded WAV hash: `491cc7b01c15b2228fc88038f8527b175e5105b49ecf6ab3109420383249a98a`.
- Run `python Tools/Audio/edit_recorded_rifle.py` after decoding. It reads the
  decoded WAV with the standard library; soundfile is only needed for decoding.
- One source shot with three tail/channel edits, 110 Hz high-pass, 12.5 kHz
  low-pass, interpolation to 48 kHz, shortened 180 ms tail, gentle saturation,
  fades and -3 dBFS normalization. Existing filenames and GUIDs are preserved.
- Output: `Artifacts/Audio/FantasyMelodyRework`; previous M4 clips:
  `Artifacts/Audio/BeforeFantasyMelodyRework`. Both ignored by Git.

## Previous: AR-15/M4

The previous three `sfx_fire_royal_rifle*.wav` files were edits of real recordings,
not procedural sound synthesis. Other sound effects and all music retain their
previous provenance (procedurally composed).

- Library: **The Free Firearm Sound Library**.
- Creators: **Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney**.
- Source page: https://opengameart.org/content/the-free-firearm-sound-library
- License: **CC0 1.0**, as stated on the source page and in its quoted creator statement.
- License reference: https://creativecommons.org/publicdomain/zero/1.0/
- Download: https://opengameart.org/sites/default/files/Prepared%20SFX%20Library.7z
- Retrieved: 2026-09-06.
- Archive SHA-256: `cc1ab5a99a0a365105c7c5dd783f4b0b1fe90938114d3ceec53856bfe005f7d6`.
- Source member: `Prepared SFX Library/AR-15/D_32P.wav`.
- Source WAV SHA-256: `acee9d2106b68fe5956225a19d7aedf943d97793817f9bda9d486a2b74b0a812`.
- Included master sheet identifies this as near-distance stereo AR-15 / M4,
  .223 / 5.56x45 rifle fire. This follows the library's identification; the exact
  firearm configuration is not independently verified.

Edits: two separate shots near 0.70 and 5.64 seconds, plus an alternate
80% left / 20% right microphone-channel blend of the first shot as variant 3;
stereo-to-mono, 65 Hz rumble removal, 15.5 kHz low-pass before 96-to-48 kHz
decimation, tail shortening, moderate transient compression, fades, -3 dBFS
peak normalization, 24-bit PCM export. Each runtime variant is 0.18 seconds.
No YouTube audio is included. The previous AK-47 edits are local backups only.

## Reproduce

Keep the archive locally at `Artifacts/Audio/FirearmSource/prepared.7z`.
Extract only the named WAV and optionally `Prepared SFX Library/Prepared Master Sheet.csv`
into `Artifacts/Audio/FirearmSource`, preserving the archive's subdirectories.
Verify the source hash above, then run:

```text
python Tools/Audio/edit_recorded_rifle.py
```

The generator writes existing runtime asset filenames and preserves `.meta` files.
Its report records source/output hashes and precise cut positions. Raw downloads,
backups, reports and auditions are all under ignored `Artifacts/`. Commit only
the final runtime WAVs/metadata, editing script and this provenance document.
A fresh clone can play the committed WAVs without downloading the source library.

## Dedicated pistol (2026-09-06)

The Free Firearm Sound Library (CC0; creators listed above), member
`Prepared SFX Library/1911/A_42P.wav`: source master sheet labels 1911 .45 handgun,
near distance, front of shooter, stereo. SHA-256:
`8e84438e771c157155a6a1ff47a6a7a7d81b6f39b185d41e426c57337a82254a`.
Run `python Tools/Audio/edit_recorded_pistol.py` after extracting that member from
the existing prepared.7z archive. Two shots (0.94146s / 5.00154s), plus first-shot
80/20 channel mix. 90Hz rumble removal, 14kHz lowpass, 96-to-48kHz decimation,
160ms tail shaping, mild saturation and -3dBFS peaks. 16-bit mono PCM.
Dedicated FirePistol cue for high_card and one_pair; other Rapid weapons retained.
Current previews/reports: Artifacts/Audio/RecordedPistol. No YouTube samples.
