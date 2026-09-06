"""Current score + dedicated royal rifle. Original NumPy synthesis, no samples.

Run after refine_table_audio.py for a complete rebuild. Only seven runtime WAVs
are written; cards, feedback and other weapons remain unchanged. Backups and
auditions stay in ignored Artifacts. 150 BPM, 48 bars; result is 24 bars.
"""
from pathlib import Path
import hashlib
import json
import shutil
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Assets/_Project/Audio'
OUT = ROOT / 'Artifacts/Audio/ModernCombat'
BACK = ROOT / 'Artifacts/Audio/BeforeModernCombat'
SR = 48000
BEAT = .4
N = 3686400
rng = np.random.default_rng(202609069)
OUT.mkdir(parents=True, exist_ok=True)
BACK.mkdir(parents=True, exist_ok=True)


def time(d):
    return np.arange(round(d*SR))/SR


def noise(d, low, high):
    t = time(d)
    f = np.fft.rfftfreq(len(t), 1/SR)
    filt = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    a = np.fft.irfft(np.fft.rfft(rng.normal(size=len(t)))*filt, len(t))
    return a/max(np.std(a), 1e-6)


def envelope(a, attack=.001, release=.012):
    t = np.arange(len(a))/SR
    e = np.minimum(t/attack, 1)*np.clip((len(a)/SR-t)/release, 0, 1)
    return a*e if a.ndim == 1 else a*e[:, None]


def rifle():
    d = .155
    t = time(d)
    # Broadband muzzle crack, low pressure body and short bolt movement.
    # Nonperiodic body avoids a laser-like pitch sweep during fast repetitions.
    crack = noise(d, 1400, 10500)*np.exp(-t/ .006)*.65
    body = noise(d, 90, 1500)*np.exp(-t/.025)*1.05
    thump = np.sin(2*np.pi*112*t)*np.exp(-t/.012)*.23
    bolt = noise(d, 1700, 6800)*np.exp(-((t-.038)/.004)**2)*.10
    tail = noise(d, 250, 4200)*np.exp(-t/.036)*.18
    a = np.tanh((crack+body+thump)*1.35)+bolt+tail
    # Tiny diffuse reflections, not a long echo that smears the next shot.
    dry = a.copy()
    for delay, gain in [(.019, .07), (.031, .035)]:
        offset = round(delay*SR)
        a[offset:] += dry[:-offset]*gain
    return envelope(a, .00015, .025)


def kick():
    t = time(.27)
    phase = 2*np.pi*(49*t+110*.013*(1-np.exp(-t/.013)))
    a = np.sin(phase)*np.exp(-t/ .074)
    a += .18*np.sin(phase*2)*np.exp(-t/.026)
    a += noise(.27, 1800, 7300)*np.exp(-t/.003)*.075
    return envelope(np.tanh(a*1.45), .0005, .025)


def clap():
    t = time(.19)
    n = noise(.19, 800, 8300)
    e = np.zeros(len(t))
    for at, gain in [(0, .7), (.009, .65), (.019, .85)]:
        e += (t >= at)*np.exp(-np.maximum(t-at, 0)/.007)*gain
    e += (t >= .023)*np.exp(-np.maximum(t-.023, 0)/.038)*.33
    body = np.sin(2*np.pi*198*t)*np.exp(-t/.018)*.35
    return envelope(n*e*.48+body)


def hat(opened=False):
    d = .15 if opened else .045
    t = time(d)
    return envelope(noise(d, 5700, 13000)*np.exp(-t/(.041 if opened else .014)), .0007, .01)


def bass(note, d, bright):
    t = time(d)
    f = 440*2**((note-69)/12)
    p = 2*np.pi*f*t
    # A centered sub plus a moving, saturated mid-bass with per-note filter decay.
    cutoff = 2.2+bright*8*np.exp(-t/.055)
    a = np.sin(p)*.95
    for h in range(2, 13):
        a += np.sin(h*p+.22*np.sin(2*np.pi*(2.1+h*.13)*t))*np.exp(-h/cutoff)/h*.95
    return envelope(np.tanh(a*1.65)*np.exp(-t*.8), .003, .018)


def hook(note, d=.22):
    t = time(d)
    f = 440*2**((note-69)/12)
    a = np.zeros((len(t), 2))
    for channel, detune in enumerate([.998, 1.002]):
        p = 2*np.pi*f*detune*t
        a[:, channel] = np.sin(p+1.3*np.sin(p*2)*np.exp(-t/ .035))
        a[:, channel] += .21*np.sin(3*p)*np.exp(-t/.035)
    return envelope(np.tanh(a)*np.exp(-t[:, None]/.07), .002, .025)


tracks = {name: np.zeros((N, 2), np.float32) for name in ['kick', 'tops', 'bass', 'hook', 'air', 'fill']}


def put(track, a, beat, gain=1, pan=0):
    if a.ndim == 1:
        a = a[:, None]*np.sqrt(np.array([(1-pan)/2, (1+pan)/2]))
    i = round(beat*BEAT*SR) % N
    count = min(len(a), N-i)
    tracks[track][i:i+count] += a[:count]*gain
    if count < len(a):
        tracks[track][:len(a)-count] += a[count:]*gain


# Six 8-bar phrases: hook answers, filter movement, short turnarounds.
# No long beatless intro: the game can crossfade into any point of the loop.
for bar in range(48):
    phrase = bar//8
    root = [28, 28, 24, 26][(bar//2) % 4]
    turn = bar % 8 == 7
    for b in range(4):
        if not (turn and b == 3):
            put('kick', kick(), bar*4+b, .92)
    for b in [1, 3]:
        put('tops', clap(), bar*4+b, .42)
    for step in range(16):
        off = step % 4 == 2
        gain = .14 if off else (.036 if step % 2 else .055)
        put('tops', hat(off), bar*4+step/4+(.025 if step % 2 else 0), gain, .32 if step % 2 else -.32)
    pattern = [(0, .13, 0), (.75, .075, 0), (1.5, .16, 0), (2.25, .09, 12), (2.75, .085, 0), (3.5, .14, 0)]
    if bar % 2:
        pattern = [(.5, .16, 0), (1.25, .085, 0), (1.75, .07, 7), (2.5, .16, 0), (3.25, .08, 0), (3.75, .07, 12)]
    for beat, duration, interval in pattern:
        put('bass', bass(root+interval, duration, .45+.1*phrase), bar*4+beat, .57)
    # Short minor-key call/response; mostly E/B/G/D, no bright major lift.
    motif = [(0.5, 12), (1.75, 19), (2.5, 15), (3.25, 14)] if bar % 2 == 0 else [(0.75, 12), (2, 10), (3.5, 7)]
    for b, interval in motif:
        put('hook', hook(root+interval+12), bar*4+b, .11 if phrase in [0, 3] else .17)
    if turn:
        for b, gain in [(3.25, .12), (3.5, .17), (3.75, .22)]:
            put('fill', clap(), bar*4+b, gain, -.18 if b == 3.5 else .18)
        t = time(.65)
        rise = envelope(noise(.65, 1600, 7800)*(t/.65)**2, .08, .015)
        put('fill', rise, bar*4+2.375, .055)
    if bar % 8 == 0:
        t = time(.6)
        put('air', envelope(noise(.6, 3500, 11500)*np.exp(-t/.16)), bar*4, .075, .2)
    # Dark stereo chord bed adds space without a continuous lead/arpeggio wall.
    t = time(1.6)
    pad = np.zeros((len(t), 2))
    for interval in [12, 19, 22]:
        f = 440*2**((root+interval-69)/12)
        pad[:, 0] += np.sin(2*np.pi*f*.999*t)*.02
        pad[:, 1] += np.sin(2*np.pi*f*1.001*t+.15)*.02
    put('air', envelope(pad, .15, .25), bar*4)

dry = tracks['hook'].copy()
for delay, gain in [(.3, .22), (.6, .10)]:
    tracks['hook'] += np.roll(dry[:, ::-1], round(delay*SR), axis=0)*gain
phase = (np.arange(N) % round(BEAT*SR))/SR
duck = .26+.74*(1-np.exp(-phase/.055))
tracks['bass'] *= duck[:, None]
tracks['hook'] *= (.60+.40*duck)[:, None]
tracks['air'] *= (.75+.25*duck)[:, None]


def save(name, a, target=None):
    a = a.astype(np.float64)
    a -= a.mean(axis=0)
    if target is None:
        a = envelope(a, .00015, .018)
        a *= 10**(-2/20)/max(np.max(np.abs(a)), 1e-9)
    else:
        # Gentle saturation; linked stereo peak scaling preserves imaging.
        a = np.tanh(a*1.18)
        a *= 10**(target/20)/max(np.sqrt(np.mean(a*a)), 1e-9)
        a *= min(1, 10**(-2/20)/np.max(np.abs(a)))
        a[-192:] -= np.linspace(0, 1, 192)[:, None]*(a[-1]-a[0])
    assert np.isfinite(a).all() and np.max(np.abs(a)) < 1
    if target is not None:
        assert np.max(np.abs(a[-1]-a[0])) < 1e-7
    pcm = np.round(a.ravel()*8388607).astype(np.int32)
    packed = np.column_stack([pcm & 255, (pcm >> 8) & 255, (pcm >> 16) & 255]).astype(np.uint8)
    path = OUT/(name+'.wav')
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1 if a.ndim == 1 else 2)
        w.setsampwidth(3)
        w.setframerate(SR)
        w.writeframes(packed.tobytes())
    for suffix in ['.wav', '.wav.meta']:
        src = ASSETS/(name+suffix)
        if src.exists() and not (BACK/src.name).exists():
            shutil.copy2(src, BACK/src.name)
    shutil.copy2(path, ASSETS/path.name)
    report.append(dict(file=path.name, seconds=len(a)/SR,
        peak_dbfs=float(20*np.log10(np.max(np.abs(a)))), rms_dbfs=float(20*np.log10(np.sqrt(np.mean(a*a)))),
        endpoint_delta=float(np.max(np.abs(a[-1]-a[0]))), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    return a


report = []
guns = [save('sfx_fire_royal_rifle'+('' if i == 0 else '_v'+str(i+1)), rifle()) for i in range(3)]
mixes = {}
for name, weights, target in [
    ('plan', [.55, .60, .78, .65, 1, .35], -17),
    ('combat', [1, 1, 1, 1, 1, .85], -14),
    ('boss', [1.05, 1.12, 1.10, 1.16, 1, 1.2], -13.5),
    ('result', [.12, .08, .23, .45, 1, 0], -23),
]:
    mix = sum(tracks[key]*weight for key, weight in zip(tracks, weights))
    if name == 'result':
        mix = mix[:N//2]
    mixes[name] = save('bgm_'+name, mix, target)


def audition(name, a):
    a = a.copy()
    a[-2400:] *= np.linspace(1, 0, 2400)[:, None] if a.ndim == 2 else np.linspace(1, 0, 2400)
    assert np.max(np.abs(a)) < 1
    with wave.open(str(OUT/(name+'.wav')), 'wb') as w:
        w.setnchannels(1 if a.ndim == 1 else 2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(np.round(a*32767).astype('<i2').tobytes())


# Raw single shots, then actual 1/2/3-star cadence at in-game gain (0.65*0.24).
gun_demo = np.zeros(round(12*SR))
for i in range(3):
    at = round((.25+i*.55)*SR)
    gun_demo[at:at+len(guns[i])] += guns[i]*.65*.24
for start, rate in [(2.5, 6), (5.5, 9.6), (8.5, 13.5)]:
    for j in range(round(rate*2)):
        at = round((start+j/rate)*SR)
        gun_demo[at:at+len(guns[j % 3])] += guns[j % 3]*.65*.24
audition('Royal_Rifle_Preview', gun_demo)
length = round(38.4*SR)
t = np.arange(length)/SR
combat = np.clip((t-6.4)/.8, 0, 1)
boss = np.clip((t-25.6)/.8, 0, 1)
preview = .55*(mixes['plan'][:length]*(1-combat)[:, None]
    +mixes['combat'][:length]*(combat-boss)[:, None]+mixes['boss'][:length]*boss[:, None])
audition('Modern_BGM_Preview', preview)
combined = preview.copy()
for start, rate in [(13, 6), (20, 9.6), (29, 13.5)]:
    for j in range(round(rate*2)):
        at = round((start+j/rate)*SR)
        combined[at:at+len(guns[j % 3])] += guns[j % 3][:, None]*.65*.24
audition('BGM_and_Royal_Rifle', combined)
(OUT/'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
