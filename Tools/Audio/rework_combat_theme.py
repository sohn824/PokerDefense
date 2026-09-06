"""Dark fantasy combat: bowed strings, horns, lute and membrane war drums.
48-bar thematic arrangement, 150 BPM / C minor. Real CC0 VSCO 2 CE multisamples plus quiet procedural texture.
Only overwrites bgm_combat.wav. Original score rendered in NumPy; no sampled music recordings.
"""
from pathlib import Path
import hashlib
import json
import shutil
import wave
import numpy as np
from orchestra_sampler import render

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Assets/_Project/Audio'
OUT = ROOT / 'Artifacts/Audio/ArenaSmooth'
BACK = ROOT / 'Artifacts/Audio/BeforeArenaSmooth'
SR = 48000
BEAT = .4
N = 3686400
rng = np.random.default_rng(202609071)
OUT.mkdir(parents=True, exist_ok=True)
BACK.mkdir(parents=True, exist_ok=True)


def clock(d):
    return np.arange(round(d*SR))/SR


def env(a, attack=.003, release=.02):
    t = np.arange(len(a))/SR
    gain = np.minimum(t/attack, 1)*np.clip((len(a)/SR-t)/release, 0, 1)
    return a*gain if a.ndim == 1 else a*gain[:, None]


def noise(d, low, high):
    n = round(d*SR)
    f = np.fft.rfftfreq(n, 1/SR)
    filt = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    a = np.fft.irfft(np.fft.rfft(rng.normal(size=n))*filt, n)
    return a/max(np.std(a), 1e-6)


def drum(kind):
    if kind == 'kick':
        a = render('drum', 0, .28, .9)
        t = np.arange(len(a))/SR
        a *= np.exp(-t/.16)[:,None]
        a += env(np.sin(2*np.pi*(57*t+1.7*(1-np.exp(-t/.015))))*np.exp(-t/.075),.001,.03)[:,None]*.28
        return a
    if kind == 'snare':
        return render('drum', 2, .18, .9)
    t = clock(.085)
    a = noise(.085, 5000, 13500)*np.exp(-t/.018)*.22
    return env(a, .0004, .015)


def voice(note, d, kind):
    group = {'lead':'horn', 'lute':'harp', 'bass':'short', 'pad':'strings'}[kind]
    if kind == 'pad' and note < 60:
        group = 'cello'
    return render(group, note, d, .82 if kind == 'lead' else .72)


def low_pulse(note, duration):
    t = clock(duration)
    f = 440*2**((note-69)/12)
    x = np.sin(2*np.pi*f*t)+.24*np.sin(2*np.pi*2*f*t)
    return env(np.tanh(x*1.25)*np.exp(-t/ .19),.004,.045)


stems = {s: np.zeros((N, 2), np.float32) for s in ['drums', 'bass', 'lead', 'pad', 'fx']}


def put(stem, a, beat, gain=1, pan=0):
    if a.ndim == 1:
        a = a[:, None]*np.sqrt(np.array([(1-pan)/2, (1+pan)/2]))
    # Small deterministic performance drift; section downbeats stay anchored.
    offset = rng.uniform(-.006, .006) if beat % 4 else 0
    gain *= rng.uniform(.93, 1.04)
    pos = round((beat*BEAT+offset)*SR) % N
    count = min(len(a), N-pos)
    stems[stem][pos:pos+count] += a[:count]*gain
    if count < len(a):
        stems[stem][:len(a)-count] += a[count:]*gain


# Ruined stone arena: C pedal, minor seconds, sparse low brass, broken dance beat.
# Harmonic rhythm spans two bars; avoid a major-third heroic cadence.
roots = [36,36,37,37,32,32,31,31]
sections = [('gates',0,8),('pressure',8,16),('stalking',16,24),
            ('clash',24,32),('siege',32,40),('loop_drive',40,48)]
for bar in range(48):
    root = roots[(bar//2)%8]
    energy = .92 if 16<=bar<24 else 1.08 if bar>=24 else 1.0
    kick_beats = [0,.75,2,2.5,3.75] if bar%2 else [0,1.5,2,3.5]
    for beat in kick_beats:
        put('drums',drum('kick'),bar*4+beat,.48*energy)
    for beat in [1,3]:
        put('drums',drum('snare'),bar*4+beat,.33*energy)
    for j in range(16):
        if j%2 or bar%4==3:
            put('drums',drum('hat'),bar*4+j*.25,.025 if j%4==1 else .014)
    for beat in [0,.75,1.5,2,2.75,3.5]:
        put('bass',low_pulse(root,.25),bar*4+beat,.11*energy)
        # Avoid repeated bowed attack clicks underneath the kick.
    # Bowed low texture and open fifths, no synthetic vowel choir or string lead.
    if bar%2==0:
        for interval in [0,7]:
            put('pad',render('cello',root+interval,3.2,.7),bar*4,.024)
    # Sparse low sustained phrases replace arcade-like short brass calls.
    # Four two-bar gestures with a full-bar rest between responses.
    if bar%2==0:
        note = [48,49,44,43][(bar//4)%4]
        put('lead',render('trombone',note,1.10,.72),bar*4+.25,.11)
        put('lead',render('horn',note+7,1.35,.72),bar*4+.35,.075)
    if bar%8 in [3,7]:
        note = 55 if bar%8==3 else 47
        put('lead',render('horn',note,.95,.72),bar*4+1.5,.09)

# Diffuse hall reflections; no tempo delay or dance-style sidechain pumping.
for stem in ['lead', 'pad', 'bass', 'drums']:
    dry = stems[stem].copy()
    for delay, gain in [(d, .046*np.exp(-d/.48)) for d in np.linspace(.031, 1.35, 32)]:
        stems[stem] += np.roll(dry[:, ::-1], round(delay*SR), axis=0)*gain*(.3 if stem == 'drums' else 1)
a = sum(stems.values()).astype(np.float64)
a -= a.mean(axis=0)
a *= 10**(-12.5/20)/np.sqrt(np.mean(a*a))
# Linked mastering compressor catches coincident drum/ensemble peaks without
# pumping the strings to the beat. Two loop passes settle the release at the seam.
block = 240
peaks = np.max(np.abs(a.reshape(-1, block, 2)), axis=(1, 2))
threshold = 10**(-7/20)
required = np.minimum(1, (threshold/np.maximum(peaks, 1e-9))**(2/3))
gains = np.ones(len(required))
gain = 1.0
release = np.exp(-block/SR/.08)
for _ in range(2):
    for i in range(len(required)):
        target = min(required[i], required[(i+1) % len(required)])
        gain = min(target, 1-(1-gain)*release)
        gains[i] = gain
a *= np.interp(np.arange(N)/block, np.arange(len(gains)+1), np.r_[gains, gains[0]])[:, None]
a *= 10**(-12.5/20)/np.sqrt(np.mean(a*a))
a *= min(1, 10**(-2/20)/np.max(np.abs(a)))
a[-192:] -= np.linspace(0, 1, 192)[:, None]*(a[-1]-a[0])
assert np.isfinite(a).all() and np.max(np.abs(a)) < 1
assert np.max(np.abs(a[-1]-a[0])) < 1e-7


def write(path, samples, width=3):
    pcm = np.round(samples.ravel()*(8388607 if width == 3 else 32767)).astype(np.int32)
    payload = np.column_stack([pcm & 255, (pcm >> 8) & 255, (pcm >> 16) & 255]).astype(np.uint8).tobytes() if width == 3 else pcm.astype('<i2').tobytes()
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(width)
        w.setframerate(SR)
        w.writeframes(payload)


for suffix in ['.wav', '.wav.meta']:
    old = ASSETS/('bgm_combat'+suffix)
    if old.exists() and not (BACK/old.name).exists():
        shutil.copy2(old, BACK/old.name)
write(OUT/'bgm_combat.wav', a)
shutil.copy2(OUT/'bgm_combat.wav', ASSETS/'bgm_combat.wav')
preview = a*.55
preview[-SR:] *= np.linspace(1, 0, SR)[:, None]
write(OUT/'Combat_Theme_Full_Preview.wav', preview, 2)
report = dict(score="Smooth dark arena: sustained low brass, clean kick-snare groove, no metallic ticks or noise FX", seconds=N/SR, peak_dbfs=float(20*np.log10(np.max(np.abs(a)))),
    rms_dbfs=float(20*np.log10(np.sqrt(np.mean(a*a)))),
    endpoint_delta=float(np.max(np.abs(a[-1]-a[0]))),
    sections=[dict(name=n, start_seconds=start*1.6, end_seconds=end*1.6) for n, start, end in sections],
    sha256=hashlib.sha256((ASSETS/'bgm_combat.wav').read_bytes()).hexdigest())
(OUT/'theme-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
