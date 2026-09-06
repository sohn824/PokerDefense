"""V2: tactile card foley and restrained dark percussion score.

Python + NumPy; fully synthesized, not recordings. Replaces only listed cues.
Keeps the original 150 BPM / 48-bar grid for existing music transitions.
"""
from pathlib import Path
import hashlib
import json
import shutil
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ASSETS=ROOT/'Assets/_Project/Audio'
OUT=ROOT/'Artifacts/Audio/PolishV2'
BACK=ROOT/'Artifacts/Audio/BeforePolishV2'
SR=48000; BEAT=.4; N=3686400
rng=np.random.default_rng(202609062)
OUT.mkdir(parents=True,exist_ok=True); BACK.mkdir(parents=True,exist_ok=True)

def tvec(d): return np.arange(round(d*SR))/SR
def band(d,lo,hi):
    n=round(d*SR); f=np.fft.rfftfreq(n,1/SR)
    shape=(1-np.exp(-(f/max(lo,1))**4))*np.exp(-(f/hi)**4)
    a=np.fft.irfft(np.fft.rfft(rng.normal(size=n))*shape,n)
    return a/max(np.std(a),.001)
def env(a,attack=.001,release=.012):
    t=np.arange(len(a))/SR
    e=np.minimum(t/attack,1)*np.clip((len(a)/SR-t)/release,0,1)
    return a*e if a.ndim==1 else a*e[:,None]
def mixat(dst,a,seconds,gain=1):
    i=round(seconds*SR); count=min(len(a),len(dst)-i)
    if count>0: dst[i:i+count]+=a[:count]*gain

def paper(d=.16,weight=1):
    t=tvec(d)
    # Uneven fibres/grains, rather than a smooth synthesized whoosh or pitched beep.
    speed=(np.sin(np.pi*t/d)**1.2)
    grains=np.zeros(len(t))
    for _ in range(round(d*145)):
        center=rng.uniform(.008,d-.008); width=rng.uniform(.00035,.0028)
        grains+=rng.uniform(.12,.6)*np.exp(-((t-center)/width)**2)
    friction=band(d,900,9000)*(speed*.16+grains*.20)
    flex=band(d,90,1250)*np.exp(-((t-d*.67)/.008)**2)*.30*weight
    landing=band(d,180,3200)*np.exp(-np.maximum(t-d*.82,0)*190)*(t>=d*.82)*.35
    return env(friction+flex+landing,.002,.009)

def deck_exchange():
    a=np.zeros(round(.49*SR))
    for at,g in [(0,.63),(.047,.40),(.113,.65),(.208,.55),(.30,.75)]:
        mixat(a,paper(.15+rng.uniform(-.025,.025)),at,g)
    return env(a)

def dry_tap(d=.1,weight=1):
    t=tvec(d)
    # Short, non-harmonic resonant body: wood, felt, and a subdued ceramic edge.
    a=band(d,220,6000)*np.exp(-t*125)*.48
    for f,g,decay in [(210,.6,100),(437,.2,145),(1139,.09,220)]:
        a+=g*np.sin(2*np.pi*f*t)*np.exp(-t*decay)*weight
    return env(a,.0008,.015)

def seal(level=1,d=.5,resolve=True):
    t=tvec(d); a=np.zeros(len(t)); hit=min(.065,d*.2)
    # A brief inhale, solid closure and diffuse warm tail; no rising bell arpeggio.
    inhale=band(d,500,5300)*np.exp(-((t-hit*.65)/.024)**2)*.16
    a+=inhale
    mixat(a,dry_tap(min(.14,d-hit),1+level*.12),hit,.6)
    tail=t-hit; mask=tail>=0
    for f,g in [(164.81,.10),(246.94,.075),(329.63,.025)]:
        a+=mask*g*np.sin(2*np.pi*f*tail)*np.exp(-np.maximum(tail,0)*(12-level))
    a+=band(d,150,2900)*np.exp(-np.maximum(tail,0)*17)*mask*.055*level
    if not resolve:
        a+=np.sin(2*np.pi*77.78*t)*np.exp(-t*9)*.08
    return env(a,.002,.035)

cues={}
for name,d,w in [('ui_card_select',.105,.6),('ui_card_deal',.145,.8),('ui_card_flip',.185,1.1)]:
    for v in range(3): cues[name+('' if v==0 else '_v'+str(v+1))]=paper(d+rng.uniform(-.008,.008),w)
for v in range(3): cues['ui_exchange'+('' if v==0 else '_v'+str(v+1))]=deck_exchange()
cues['ui_button']=dry_tap(.07,.7)
for name,d,level in [('merge_star2',.36,1),('merge_star3',.58,2),('joker_promote',.72,3),
                     ('hand_low',.17,.3),('hand_mid',.33,.8),('hand_high',.65,2),
                     ('state_wave_clear',.32,.7),('state_stage_clear',1.25,3),
                     ('unit_summon',.24,.5),('unit_place',.14,.65),
                     ('ui_holdbonus',.24,.5),('boss_appear',.85,3)]:
    cues[name]=seal(level,d)
for name,d in [('state_life_loss',.40),('state_game_over',1.05)]: cues[name]=seal(.8,d,False)
chips=np.zeros(round(.23*SR))
for at,g in [(0,1),(.041,.65),(.097,.44)]: mixat(chips,dry_tap(.09,.8),at,g)
cues['ui_chip_gain']=chips
cues['ui_shop_open']=deck_exchange()*.7

def stereo(a,pan=0):
    return a[:,None]*np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)])
tracks={name:np.zeros((N,2),np.float32) for name in ['drums','bass','texture','room','extra']}
def put(track,a,beat,gain=1,pan=0):
    a=stereo(a,pan) if a.ndim==1 else a
    i=round(beat*BEAT*SR)%N; n=min(len(a),N-i)
    tracks[track][i:i+n]+=a[:n]*gain
    if n<len(a): tracks[track][:len(a)-n]+=a[n:]*gain
def kick():
    t=tvec(.29)
    phase=2*np.pi*(47*t+95*.016*(1-np.exp(-t/.016)))
    body=np.sin(phase)*np.exp(-t*18)+.19*np.sin(2*phase)*np.exp(-t*32)
    return env(np.tanh(body*1.7)+band(.29,650,5800)*np.exp(-t*220)*.16)
def rim():
    t=tvec(.17)
    a=band(.17,1000,8500)*np.exp(-t*36)*.40
    a+=np.sin(2*np.pi*185*t)*np.exp(-t*42)*.4
    a+=np.sin(2*np.pi*390*t)*np.exp(-t*88)*.12
    return env(a)
def hat(d=.065):
    t=tvec(d)
    return env(band(d,4700,11500)*np.exp(-t*(26 if d>.1 else 65)),.001,.008)
def bass(note,d,variation):
    t=tvec(d); f=440*2**((note-69)/12)
    phase=2*np.pi*f*t
    a=np.sin(phase)+.42*np.sin(phase*2+.5*np.sin(2*np.pi*1.2*t))
    a+=.24*np.sin(phase*3)*np.exp(-t*(5+variation*.3))
    a+=.10*np.sin(phase*5)*np.exp(-t*14)
    return env(np.tanh(a*2.1)*np.exp(-t*3),.004,.024)
def texture(note,d=1.3):
    t=tvec(d); f=440*2**((note-69)/12)
    # Damped, slightly inharmonic prepared-string attack, buried in filtered air.
    a=np.zeros(len(t))
    for h in [1,2,3,5]:
        a+=np.sin(2*np.pi*f*h*(1+.0008*h*h)*t)*np.exp(-t*(5+h*1.8))/h**1.7
    a+=band(d,1000,4700)*np.exp(-t*12)*.05
    return env(a,.008,.1)

# Driving 150 BPM dark club groove; syncopated bass, no retro saw lead.
for bar in range(48):
    root=[28,28,28,26,24,24,26,26][(bar//2)%8]
    variation=bar%8
    for b in [0,1,2,3]: put('drums',kick(),bar*4+b,.94)
    for b in [1,3]: put('drums',rim(),bar*4+b,.88)
    for s in range(16):
        offbeat=s%4==2
        gain=.15 if offbeat else (.035 if s%2 else .06)
        put('drums',hat(.155 if offbeat else .055),bar*4+s*.25+(.018 if s%2 else 0),gain,(-1 if s%2 else 1)*.3)
    if bar%8==7:
        for b,g in [(3.5,.17),(3.75,.24)]: put('extra',rim(),bar*4+b,g,-.2)
    for j,(b,d) in enumerate([(.5,.18),(.875,.075),(1.5,.18),(2.5,.18),(2.875,.075),(3.5,.18),(3.875,.07)]):
        note=root+(12 if j==6 and bar%2 else 0)
        put('bass',bass(note,d,variation),bar*4+b,.68 if d>.1 else .32)
    if bar%2==0: put('texture',texture(root+24),bar*4+.75,.13,(-1 if bar%4 else 1)*.5)
    if bar%4==3: put('texture',texture(root+31),bar*4+3,.075,-.4)
    for b in [.75,1.75,2.75,3.25]: put('extra',dry_tap(.10,1.6),bar*4+b,.24,-.25 if b<2 else .25)
    # Filtered stereo air / low resonant room. Independent modulation each phrase.
    d=2.8; t=tvec(d)
    room=band(d,180,1700)*np.sin(np.pi*t/d)**2*.025
    room+=np.sin(2*np.pi*440*2**((root+19-69)/12)*t)*np.sin(np.pi*t/d)**2*.035
    put('room',env(room,.2,.2),bar*4,.65,.55*np.sin(bar))

dry=tracks['texture'].copy()
for delay,g in [(.3,.22),(.6,.12),(.9,.055)]:
    tracks['texture']+=np.roll(dry[:,::-1],round(delay*SR),axis=0)*g
dry=tracks['room'].copy()
for delay,g in [(.083,.22),(.139,.17),(.211,.12)]: tracks['room']+=np.roll(dry[:,::-1],round(delay*SR),axis=0)*g
phase=(np.arange(N)%round(BEAT*SR))/SR
duck=.32+.68*(1-np.exp(-phase/.085))
tracks['bass']*=duck[:,None]

music={
 'bgm_plan':(tracks['drums']*.34+tracks['room']*2+tracks['texture']*.7+tracks['bass']*.40+tracks['extra']*.10,-18.5),
 'bgm_combat':(tracks['drums']*1.12+tracks['bass']*1.12+tracks['texture']*.65+tracks['room']*1.3+tracks['extra']*.35,-14),
 'bgm_boss':(tracks['drums']*1.20+tracks['bass']*1.20+tracks['extra']+tracks['texture']*.8+tracks['room'],-13),
 'bgm_result':((tracks['room']*2.4+tracks['texture']*.45)[:N//2],-24),
}
def save(name,a,target=None):
    a=a.astype(np.float64); a-=a.mean(axis=0)
    if target is None:
        a=env(a); a*=10**(-3/20)/max(np.max(np.abs(a)),1e-6)
    else:
        a=np.tanh(a*1.25)
        a*=10**(target/20)/np.sqrt(np.mean(a*a))
        a*=min(1,10**(-3/20)/np.max(np.abs(a)))
        a[-192:]-=np.linspace(0,1,192)[:,None]*(a[-1]-a[0])
    pcm=np.round(a.ravel()*8388607).astype(np.int32)
    packed=np.column_stack([pcm&255,(pcm>>8)&255,(pcm>>16)&255]).astype(np.uint8)
    path=OUT/(name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1 if a.ndim==1 else 2); w.setsampwidth(3); w.setframerate(SR); w.writeframes(packed.tobytes())
    for suffix in ['.wav','.wav.meta']:
        source=ASSETS/(name+suffix)
        if source.exists() and not (BACK/source.name).exists(): shutil.copy2(source,BACK/source.name)
    shutil.copy2(path,ASSETS/path.name)
    assert np.isfinite(a).all() and np.max(np.abs(a))<1
    if target is not None: assert np.max(np.abs(a[-1]-a[0]))<1e-7
    return {'file':path.name,'seconds':len(a)/SR,'channels':1 if a.ndim==1 else 2,
            'peak_dbfs':float(20*np.log10(np.max(np.abs(a)))),
            'rms_dbfs':float(20*np.log10(np.sqrt(np.mean(a*a)))),
            'endpoint_delta':float(np.max(np.abs(a[-1]-a[0]))),
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
# Replace the single-card tick with one continuous hand-fanning gesture.
# Separate RNG keeps every other cue and the music bit-identical.
sequence_rng = rng
rng = np.random.default_rng(202609063)
for v in range(3):
    gesture = np.zeros(round(.38*SR))
    mixat(gesture, paper(.29+rng.uniform(-.01,.01), .35), 0, .65)
    mixat(gesture, paper(.25, .45), .075, .45)
    t = tvec(.38)
    settle = band(.38, 150, 1900)*np.exp(-((t-.315)/.013)**2)*.10
    cues['ui_card_deal'+('' if v == 0 else '_v'+str(v+1))] = env(gesture+settle, .006, .035)
rng = sequence_rng

report=[save('sfx_'+name,a) for name,a in cues.items()]
report += [save(name,a,target) for name,(a,target) in music.items()]
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(f'Installed {len(report)} refined clips. Previous versions: {BACK}')

# Isolated audition: paper -> exchange -> merge -> end-of-round, with breathing room.
audition=[]; timeline=[]; cursor=0
for name in ['ui_card_select','ui_card_select_v2','ui_card_deal','ui_card_flip','ui_exchange',
             'merge_star2','merge_star3','state_wave_clear','hand_high','state_stage_clear']:
    with wave.open(str(OUT/('sfx_'+name+'.wav'))) as w:
        raw=np.frombuffer(w.readframes(w.getnframes()),np.uint8).reshape(-1,3).astype(np.int32)
        x=raw[:,0]|raw[:,1]<<8|raw[:,2]<<16
        x=((x^0x800000)-0x800000)/8388608
    timeline.append({'cue':name,'start_seconds':cursor/SR})
    audition.extend([x*.3,np.zeros(round(.6*SR))]); cursor+=len(x)+round(.6*SR)
with wave.open(str(OUT/'Cards_and_Rewards_Preview.wav'),'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(np.round(np.concatenate(audition)*32767).astype('<i2').tobytes())
(OUT/'Cards_and_Rewards_Preview.json').write_text(json.dumps(timeline,indent=2),encoding='utf-8')
