"""Edit the CC0 G36-labelled effect into royal rifle one-shots; never sample YouTube.
Download/extraction instructions and attribution: FIREARM_SOURCES.md.
Only replaces the three existing royal rifle variants, preserving their GUIDs.
"""
from pathlib import Path
import hashlib
import json
import shutil
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'Artifacts/Audio/FirearmSource/g36-decoded.wav'
ASSETS = ROOT/'Assets/_Project/Audio'
OUT = ROOT/'Artifacts/Audio/FantasyMelodyRework'
BACK = ROOT/'Artifacts/Audio/BeforeFantasyMelodyRework'
SR = 48000
OUT.mkdir(parents=True, exist_ok=True)
BACK.mkdir(parents=True, exist_ok=True)


def read(path):
    with wave.open(str(path)) as w:
        assert w.getsampwidth() == 3
        rate = w.getframerate()
        b = np.frombuffer(w.readframes(w.getnframes()), np.uint8).reshape(-1, 3).astype(np.int32)
        p = b[:, 0] | b[:, 1] << 8 | b[:, 2] << 16
        return ((p ^ 8388608)-8388608).reshape(-1, w.getnchannels())/8388608, rate


def write(path, a, width=3):
    pcm = np.round(a.ravel()*(8388607 if width == 3 else 32767)).astype(np.int32)
    payload = np.column_stack([pcm & 255, (pcm >> 8) & 255, (pcm >> 16) & 255]).astype(np.uint8).tobytes() if width == 3 else pcm.astype('<i2').tobytes()
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1 if a.ndim == 1 else a.shape[1])
        w.setsampwidth(width)
        w.setframerate(SR)
        w.writeframes(payload)


source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
assert source_hash == '491cc7b01c15b2228fc88038f8527b175e5105b49ecf6ab3109420383249a98a', 'Unexpected decoded source'
raw, rate = read(SOURCE)
assert rate == 44100
mono = raw.mean(axis=1)
# Band-limit before resampling; preserve the source transient.
freq = np.fft.rfftfreq(len(mono), 1/rate)
curve = (1-np.exp(-(freq/110)**4))*np.exp(-(freq/12500)**8)
filtered = np.fft.irfft(np.fft.rfft(mono)*curve, len(mono))
alternate = np.fft.irfft(np.fft.rfft(raw[:, 0]*.8+raw[:, 1]*.2)*curve, len(mono))
report = dict(source_url='https://freesound.org/people/CS279/sounds/196400/',
    source='G36-E Fire / CS279 / public compressed OGG preview', license='CC0-1.0',
    provenance_note='G36-labelled designed effect; live-fire origin not independently verified. Three edits of one shot.',
    source_sha256=source_hash, variants=[])
guns = []
for i in range(3):
    lo, hi = 0, round(.10*rate)
    local = np.max(np.abs(raw[lo:hi]), axis=1)
    onset = lo+int(np.flatnonzero(local > np.max(local)*.12)[0])
    start = max(0, onset-round(.0015*rate))
    # One source shot, three subtle tail/channel variants; no pitch randomization.
    take = alternate if i == 2 else filtered
    segment = take[start:start+round(.18*rate)].copy()
    a = np.interp(np.arange(round(.18*SR))/SR, np.arange(len(segment))/rate, segment)
    t = np.arange(len(a))/SR
    a -= a.mean()
    # Keep first 40 ms intact, then shorten the outdoor tail for rapid fire.
    a *= np.exp(-np.maximum(t-.025, 0)/[.045, .052, .040][i])
    a *= np.minimum(t/.00015, 1)*np.clip((len(a)/SR-t)/.025, 0, 1)
    # Moderate transient compression makes the recorded body audible under music.
    a = np.tanh(a/np.max(np.abs(a))*1.7)
    a *= 10**(-3/20)/np.max(np.abs(a))
    assert np.isfinite(a).all() and np.max(np.abs(a)) < 1
    name = 'sfx_fire_royal_rifle'+('' if i == 0 else '_v'+str(i+1))
    for suffix in ['.wav', '.wav.meta']:
        old = ASSETS/(name+suffix)
        if old.exists() and not (BACK/old.name).exists():
            shutil.copy2(old, BACK/old.name)
    write(OUT/(name+'.wav'), a)
    shutil.copy2(OUT/(name+'.wav'), ASSETS/(name+'.wav'))
    guns.append(a)
    report['variants'].append(dict(file=name+'.wav', start_seconds=start/rate,
        seconds=len(a)/SR, peak_dbfs=float(20*np.log10(np.max(np.abs(a)))),
        rms_dbfs=float(20*np.log10(np.sqrt(np.mean(a*a)))),
        sha256=hashlib.sha256((ASSETS/(name+'.wav')).read_bytes()).hexdigest()))

demo = np.zeros(12*SR)
for i, gun in enumerate(guns):
    start = round((.25+i*.6)*SR)
    demo[start:start+len(gun)] += gun*.65*.24
for start, rate in [(2.5, 6), (5.5, 9.6), (8.5, 13.5)]:
    for j in range(round(rate*2)):
        pos = round((start+j/rate)*SR)
        gun = guns[j % 3]
        demo[pos:pos+len(gun)] += gun*.65*.24
assert np.max(np.abs(demo)) < 1
write(OUT/'Recorded_Rifle_Preview.wav', demo, 2)
music, rate = read(ASSETS/'bgm_combat.wav')
assert rate == SR
# Full theme at game gain with three short bursts. This is an offline audition,
# not a simulation of the shared voice budget or a live Unity recording.
combined = music*.55
for start, rate in [(8, 6), (18, 9.6), (42, 13.5)]:
    for j in range(round(rate*2)):
        pos = round((start+j/rate)*SR)
        gun = guns[j % 3]
        combined[pos:pos+len(gun)] += gun[:, None]*.65*.24
combined[-SR:] *= np.linspace(1, 0, SR)[:, None]
assert np.max(np.abs(combined)) < 1
write(OUT/'Combat_and_Recorded_Rifle.wav', combined, 2)
(OUT/'rifle-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
