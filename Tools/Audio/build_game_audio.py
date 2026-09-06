"""Build original sample-free SFX, install the music pack, preserve first originals.
Run compose_neon_siege.py --game-pack first. Python + NumPy only.
"""
from pathlib import Path
import hashlib
import json
import shutil
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Audio/Polish'
ASSETS=ROOT/'Assets/_Project/Audio'
BACKUP=ROOT/'Artifacts/Audio/BeforePolish'
SR=48000
rng=np.random.default_rng(9062026)
OUT.mkdir(parents=True,exist_ok=True)
BACKUP.mkdir(parents=True,exist_ok=True)
for path in ASSETS.glob('*.wav*'):
    if not (BACKUP/path.name).exists(): shutil.copy2(path,BACKUP/path.name)

def time(d): return np.arange(round(d*SR))/SR
def noise(d,lo=80,hi=12000):
    n=round(d*SR)
    f=np.fft.rfftfreq(n,1/SR)
    shape=(1-np.exp(-(f/max(lo,1))**4))*np.exp(-(f/hi)**4)
    a=np.fft.irfft(np.fft.rfft(rng.normal(size=n))*shape,n)
    return a/max(np.std(a),.001)
def tone(d,f,decay):
    t=time(d)
    return np.sin(2*np.pi*f*t)*np.exp(-t*decay)
def envelope(a,attack=.0005,release=.012):
    t=np.arange(len(a))/SR
    return a*np.minimum(t/attack,1)*np.clip((len(a)/SR-t)/release,0,1)
def shot(kind,variant=0):
    d={'rapid':.115,'heavy':.34,'multi':.26,'splash':.32,'pierce':.29}[kind]
    t=time(d)
    low={'rapid':115,'heavy':67,'multi':96,'splash':58,'pierce':83}[kind]
    # No audible descending oscillator: a broadband crack with a static body.
    a=noise(d,150,13500)*np.exp(-t/ .013)*.65
    a+=noise(d,35,680)*np.exp(-t/(.034 if kind=='rapid' else .085))*.95
    a+=tone(d,low*(1+variant*.018),38 if kind=='rapid' else 17)*.5
    a+=tone(d,1830,100)*.09+tone(d,3170,155)*.06
    if kind=='multi':
        dry=a.copy()
        for at,g in [(.027,.65),(.054,.4)]:
            n=round(at*SR); a[n:]+=dry[:-n]*g
    if kind=='pierce': a+=noise(d,1600,8500)*np.exp(-t*19)*.35
    if kind=='splash': a+=noise(d,20,280)*np.exp(-t*13)*.8
    return envelope(np.tanh(a*1.45))
def chime(notes,d,step=.09):
    a=np.zeros(round(d*SR))
    for i,n in enumerate(notes):
        at=round(i*step*SR)
        if at>=len(a): break
        t=np.arange(len(a)-at)/SR; f=440*2**((n-69)/12)
        v=(np.sin(2*np.pi*f*t)+.22*np.sin(2*np.pi*f*2.003*t)+.075*np.sin(2*np.pi*f*3.98*t))*np.exp(-t*7)
        a[at:]+=envelope(v,.003,.035)*(.8**(i*.2))
    return envelope(a,.001,.04)
def foley(d,body=800):
    t=time(d)
    return envelope(noise(d,600,9000)*np.exp(-t*36)*.5+tone(d,body,85)*.25)
def impact(d=.13,low=105):
    t=time(d)
    return envelope(noise(d,70,3100)*np.exp(-t*36)*.6+tone(d,low,28)*.7)
def wash(d,low=200,high=6000):
    t=time(d)
    return envelope(noise(d,low,high)*np.sin(np.pi*t/d)**2,.002,.025)

cues={
 'ui_button':foley(.075,950), 'ui_card_deal':foley(.12,410),
 'ui_card_flip':wash(.15,1400,9500)+foley(.15,560)*.25,
 'ui_card_select':foley(.055,1450), 'ui_exchange':wash(.36,750,8000),
 'ui_chip_gain':chime([88,95,100],.33,.065),
 'ui_holdbonus':chime([76,83,88,95],.60),
 'ui_shop_open':chime([64,71,76,83],.7,.105)+wash(.7)*.14,
 'unit_summon':chime([52,64,71,76],.5,.065)+wash(.5)*.20,
 'unit_place':impact(.17,85), 'merge_star2':chime([76,83],.55,.115),
 'merge_star3':chime([88,95,100,107],.85,.085),
 'joker_promote':chime([64,71,76,83,88,95],1,.09),
 'impact_hit':impact(), 'impact_splash':impact(.42,55)+wash(.42,60,1700)*.30,
 'impact_pierce':foley(.24,2300)+wash(.24,1800,6500)*.4,
 'enemy_death':impact(.23,175)+wash(.23,1000,5500)*.18,
 'enemy_death_big':impact(.8,49)+wash(.8,50,1600)*.4,
 'boss_appear':chime([40,47,52],1.5,.12)+impact(1.5,48)*.7,
 'hand_low':chime([64,71],.38,.075), 'hand_mid':chime([64,67,71,76],.85,.08),
 'hand_high':chime([64,71,76,83,88,95],1.55,.09),
 'state_life_loss':chime([75,70,64,58],.7,.11),
 'state_wave_clear':chime([71,76,79,83],.85,.095),
 'state_game_over':chime([64,59,55,52,40],2.5,.23),
 'state_stage_clear':chime([64,68,71,76,83,88,95],3.2,.16),
}
for kind in ['rapid','heavy','multi','splash','pierce']:
    for v in range(3): cues['fire_'+kind+('' if v==0 else '_v'+str(v+1))]=shot(kind,v)

def write(path,a,channels=1):
    a=a.astype(np.float64)
    a-=a.mean(axis=0)
    a=envelope(a) if a.ndim==1 else a
    a*=10**(-1.5/20)/max(np.max(np.abs(a)),1e-6)
    pcm=np.round(a.ravel()*8388607).astype(np.int32)
    packed=np.column_stack([pcm&255,(pcm>>8)&255,(pcm>>16)&255]).astype(np.uint8)
    with wave.open(str(path),'wb') as w:
        w.setnchannels(channels); w.setsampwidth(3); w.setframerate(SR); w.writeframes(packed.tobytes())
    return a

audition=[]; timeline=[]; cursor=0
for name,a in cues.items():
    a=write(OUT/('sfx_'+name+'.wav'),a)
    timeline.append({'cue':name,'start_seconds':cursor/SR})
    audition.extend([a*.45,np.zeros(round(.35*SR))]); cursor+=len(a)+round(.35*SR)
write(OUT/'SFX_audition.wav',np.concatenate(audition))
(OUT/'SFX_audition.json').write_text(json.dumps(timeline,indent=2),encoding='utf-8')
report=[]
for p in sorted(OUT.glob('*.wav')):
    if not p.name.startswith(('bgm_', 'sfx_')): continue
    with wave.open(str(p)) as w:
        info={'file':p.name,'seconds':w.getnframes()/w.getframerate(),'channels':w.getnchannels(),'rate':w.getframerate()}
        raw=np.frombuffer(w.readframes(w.getnframes()),dtype=np.uint8).reshape(-1,3).astype(np.int32)
        ints=raw[:,0]|(raw[:,1]<<8)|(raw[:,2]<<16)
        ints=(ints^0x800000)-0x800000
        a=ints.reshape(-1,w.getnchannels())/8388608
        info.update(peak_dbfs=float(20*np.log10(np.max(np.abs(a)))),rms_dbfs=float(20*np.log10(np.sqrt(np.mean(a*a)))),
                    seam_delta=float(np.max(np.abs(a[-1]-a[0]))),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
        assert np.isfinite(a).all() and np.max(np.abs(a))<.999
        assert w.getframerate()==SR and w.getsampwidth()==3
        if p.name.startswith('bgm_'): assert info['seam_delta']<.00001
    shutil.copy2(p,ASSETS/p.name)
    report.append(info)
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(f'Installed {len(report)} clips; originals preserved in {BACKUP}')
