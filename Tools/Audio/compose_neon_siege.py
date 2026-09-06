"""Original procedural darksynth. Requires Python + NumPy; no sampled audio.

Run from any directory. Writes 48 kHz / 24-bit stereo WAVs in Artifacts/Audio.
The 32-bar arrangement is exactly 51.2 seconds at 150 BPM, in E minor.
All delay tails wrap around the loop. Seeded synthesis is reproducible.
"""
from pathlib import Path
import json
import wave
import sys
import numpy as np

SR = 48000
BPM = 150
BEAT = 60 / BPM
GAME_PACK = '--game-pack' in sys.argv
BARS = 48 if GAME_PACK else 32
N = round(BARS * 4 * BEAT * SR)
RNG = np.random.default_rng(150)
OUT = Path(__file__).resolve().parents[2] / 'Artifacts' / 'Audio'
if GAME_PACK:
    OUT = OUT / 'PolishSource'
OUT.mkdir(parents=True, exist_ok=True)
tracks = {k: np.zeros((N, 2), np.float32) for k in ['drums', 'bass', 'arp', 'pad', 'lead']}

def hz(m):
    return 440 * 2 ** ((m - 69) / 12)

def add(track, sound, beat, gain=1, pan=0):
    sound = np.asarray(sound, dtype=np.float32)
    if sound.ndim == 1:
        sound = sound[:, None] * np.array([np.sqrt((1-pan)/2), np.sqrt((1+pan)/2)])
    start = round(beat * BEAT * SR) % N
    end = start + len(sound)
    if end <= N:
        tracks[track][start:end] += sound * gain
    else:
        tracks[track][start:] += sound[:N-start] * gain
        tracks[track][:end-N] += sound[N-start:] * gain

def synth(note, duration, brightness=9, detune=0.002, decay=4):
    t = np.arange(round(duration*SR)) / SR
    f = hz(note)
    out = np.zeros((len(t), 2))
    for ch, d in enumerate([-detune, detune]):
        for h in range(1, brightness+1):
            if f*h*(1+d) < SR*.44:
                out[:, ch] += np.sin(2*np.pi*f*h*(1+d)*t + .08*h) / h * np.exp(-t*decay*h*.16)
    env = np.minimum(t/.004, 1) * np.minimum((duration-t)/.018, 1) * np.exp(-t*decay)
    return np.tanh(out*1.6) * env[:,None]

def kick():
    t = np.arange(round(.32*SR))/SR
    phase = 2*np.pi*(46*t + 112*.021*(1-np.exp(-t/.021)))
    body = np.sin(phase)*np.exp(-t*16)
    click = RNG.normal(0, 1, len(t))*np.exp(-t*650)*.15
    return np.tanh((body+click)*1.8)*np.minimum(t/.0008,1)

def snare():
    t = np.arange(round(.22*SR))/SR
    noise = RNG.normal(0,1,len(t))
    noise = noise - np.convolve(noise,np.ones(17)/17,'same')
    body = .5*np.sin(2*np.pi*185*t)*np.exp(-t*28)
    gate = np.minimum(t/.001,1)*np.clip((.205-t)/.02,0,1)
    return np.tanh((noise*.38*np.exp(-t*14)+body)*1.9)*gate

def hat(opened=False):
    dur = .17 if opened else .052
    t=np.arange(round(dur*SR))/SR
    noise=RNG.normal(0,1,len(t))
    noise=np.diff(noise,prepend=0)
    metal=sum(np.sin(2*np.pi*f*t) for f in [4211,5387,6971])*.09
    return (noise*.17+metal)*np.exp(-t*(25 if opened else 85))*np.minimum(t/.0007,1)*np.clip((dur-t)/.006,0,1)

# Four related eight-bar phrases: establish, open up, strip back, peak.
roots=[28,28,24,24,31,31,26,26]
arp_steps=[0,7,12,15,12,7,19,12,0,7,15,12,19,15,7,12]
melodies=[[76,79,83,81,79,76,74,71], [76,79,84,83,79,76,74,72],
          [79,83,86,83,81,79,76,74], [78,81,86,84,81,78,76,74]]
for bar in range(BARS):
    root=roots[bar%8]
    section=(bar//8)%4
    sparse=section==2 and bar%8<4
    for b in range(4):
        add('drums',kick(),bar*4+b,.91)
        if b in [1,3]:
            add('drums',snare(),bar*4+b,.50)
            add('drums',snare(),bar*4+b+.065,.075,-.4)
    for s in range(8):
        add('drums',hat(s%2==1 and not sparse),bar*4+s*.5,.28 if s%2 else .20,(-1 if s%2 else 1)*.28)
    if section in [1,3]:
        for s in [3,7,11,15]:
            add('drums',hat(),bar*4+s*.25,.105,.55 if s%8==3 else -.55)
    if bar%8==7:
        for i in range(4):
            add('drums',snare(),bar*4+3+i*.25,.13+.065*i,(-.35+i*.23))
    for s in range(16):
        note=root+(12 if s in [6,14] else 0)
        bass=synth(note,.145,12,0,5)
        t=np.arange(len(bass))/SR
        sub=np.sin(2*np.pi*hz(note)*t)*np.minimum(t/.003,1)*np.exp(-t*12)*np.minimum((.145-t)/.012,1)
        bass=np.tanh(bass*2.3)*.55+sub[:,None]*.48
        add('bass',bass,bar*4+s*.25,.42 if s%4 else .5)
        if not sparse or s%2==0:
            offset=arp_steps[s]
            # C and G use a major third; E uses minor; D uses suspended colour.
            if offset==15 and root in [24,31]: offset=16
            if offset==15 and root==26: offset=17
            a=synth(root+24+offset,.235,7,.0035,11)
            pan=.45*np.sin(s*1.7+bar)
            a*=np.array([np.sqrt(1-pan),np.sqrt(1+pan)])
            add('arp',a,bar*4+s*.25,.15 if section==0 else .19)
    third=3 if root==28 else (5 if root==26 else 4)
    for interval in [0,third,7,14]:
        p=synth(root+24+interval,1.85,4,.004,0.35)
        t=np.arange(len(p))/SR
        p*=np.minimum(t/.25,1)[:,None]
        add('pad',p,bar*4,.048 if sparse else .033)
    if section in [1,3] and bar%2==0:
        notes=melodies[(bar%8)//2]
        for i,note in enumerate(notes):
            lead=synth(note,.31 if i<7 else .63,8,.002,3.5)
            add('lead',lead,bar*4+i*.5,.15 if section==1 else .19)
    if bar%8==0:
        t=np.arange(round(.9*SR))/SR
        crash=RNG.normal(0,1,len(t))
        crash=np.diff(crash,prepend=0)*np.exp(-t*6)*np.minimum(t/.002,1)
        add('drums',crash,bar*4,.065,.4)

# Tempo-synced stereo echoes, circular so the musical tail crosses the seam.
for key in ['arp','lead']:
    dry=tracks[key].copy()
    for repeat in range(1,5):
        echo=np.roll(dry,round(.75*BEAT*SR*repeat),axis=0)
        if repeat%2: echo=echo[:,::-1]
        tracks[key]+=echo*(.32**repeat)
pad=tracks['pad'].copy()
for delay,gain in [(.071,.22),(.113,.16),(.181,.12),(.269,.08)]:
    tracks['pad']+=np.roll(pad[:,::-1],round(delay*SR),axis=0)*gain

phase=(np.arange(N)%round(BEAT*SR))/SR
duck=.27+.73*(1-np.exp(-phase/.073))
for key in ['bass','arp','pad','lead']:
    tracks[key]*=duck[:,None]
mix=sum(tracks.values()).astype(np.float64)
# DC removal and soft bus saturation; keep headroom below digital full scale.
mix-=mix.mean(axis=0)
mix=np.tanh(mix*1.45)
mix*=10**(-1.2/20)/np.max(np.abs(mix))
# Tiny seam correction (<1 ms); no long fade or silence in the loop.
for ch in range(2):
    delta=mix[-1,ch]-mix[0,ch]
    mix[-48:,ch]-=delta*np.linspace(0,1,48)

def write24(path,a):
    dither=(RNG.random(a.shape)-RNG.random(a.shape))/8388608
    pcm=np.round(np.clip(a+dither,-1,1)*8388607).astype(np.int32).ravel()
    packed=np.column_stack([pcm&255,(pcm>>8)&255,(pcm>>16)&255]).astype(np.uint8)
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2); w.setsampwidth(3); w.setframerate(SR); w.writeframes(packed.tobytes())

write24(OUT/'Neon_Siege_150BPM_Loop.wav',mix)
preview=mix.copy()
fade=round(SR*.025)
preview[:fade]*=np.linspace(0,1,fade)[:,None]
fade=round(SR*1.5)
preview[-fade:]*=np.linspace(1,0,fade)[:,None]
write24(OUT/'Neon_Siege_150BPM_Preview.wav',preview)
report={'title':'Neon Siege','bpm':BPM,'bars':BARS,'key':'E minor','duration_seconds':N/SR,
        'sample_rate':SR,'bit_depth':24,'channels':2,'peak_dbfs':float(20*np.log10(np.max(np.abs(mix)))),
        'rms_dbfs':float(20*np.log10(np.sqrt(np.mean(mix**2)))),
        'stereo_correlation':float(np.corrcoef(mix.T)[0,1]),
        'loop_endpoint_difference':float(np.max(np.abs(mix[-1]-mix[0]))),
        'finite_samples':bool(np.isfinite(mix).all()),'external_samples':False,
        'notes':'Original code-composed synthesis. Loop file has no fade-out; preview fades out. No LUFS claim.'}
(OUT/'Neon_Siege_info.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))

if GAME_PACK:
    # Related arrangements, not merely the same master turned down.
    pack = OUT.parent / 'Polish'
    pack.mkdir(exist_ok=True)
    def master(a, rms_db):
        a=a.astype(np.float64)
        a-=a.mean(axis=0)
        a=np.tanh(a*1.1)
        a*=10**(rms_db/20)/np.sqrt(np.mean(a*a))
        a*=min(1,10**(-2/20)/np.max(np.abs(a)))
        a[-96:]-=np.linspace(0,1,96)[:,None]*(a[-1]-a[0])
        return a
    arrangements = {
        'bgm_combat': (tracks['drums']*.82+tracks['bass']*.87+tracks['arp']*.9+tracks['pad']+tracks['lead']*.65,-17),
        'bgm_plan': (tracks['pad']*2.3+tracks['arp']*.48+tracks['bass']*.16,-23),
        'bgm_boss': (tracks['drums']+tracks['bass']*1.12+tracks['arp']+tracks['pad']*.7+tracks['lead']*1.1,-16),
        'bgm_result': ((tracks['pad']*2.2+tracks['arp']*.22)[:N//2],-24),
    }
    for name,(a,level) in arrangements.items():
        write24(pack/(name+'.wav'),master(a,level))
