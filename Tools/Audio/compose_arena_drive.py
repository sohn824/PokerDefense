"""Arena Drive: original riff-led battle-rock audition, 100 BPM / 76.8 seconds.

Uses existing CC0 FreePats guitar and VSCO drum samples, plus synthesized bass
and percussion. No reference recording fragments or earlier melody arrays.
Never writes Assets. Python + NumPy; source setup in GUITAR_CHOIR_CANDIDATES.md
and ORCHESTRA_SOURCES.md.
"""
from pathlib import Path
from functools import lru_cache
import hashlib
import json
import re
import wave
import numpy as np
from orchestra_sampler import render, counter, SOURCE

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Audio/ArenaDrive'
SR=48000
BPM=100
BEAT=60/BPM
BARS=32
N=round(BARS*4*BEAT*SR)
rng=np.random.default_rng(20260911)

def filtered(x,low=0,high=6000):
    f=np.fft.rfftfreq(len(x),1/SR)
    g=np.exp(-(f/high)**4)
    if low:g*=1-np.exp(-(f/low)**4)
    if x.ndim==2:g=g[:,None]
    return np.fft.irfft(np.fft.rfft(x,axis=0)*g,n=len(x),axis=0)

def pitch(name):
    m=re.match(r'([A-G])(#?)(\d)',name)
    return (int(m[3])+1)*12+dict(C=0,D=2,E=4,F=5,G=7,A=9,B=11)[m[1]]+bool(m[2])

sources=[(pitch(p.stem),p) for p in (ROOT/'Artifacts/Audio/GuitarSource').rglob('*.wav')]

@lru_cache(maxsize=32)
def sample(path):
    with wave.open(str(path)) as w:
        rate,ch,width=w.getframerate(),w.getnchannels(),w.getsampwidth()
        raw=w.readframes(w.getnframes())
    if width==2:x=np.frombuffer(raw,'<i2').astype(float)/32768
    elif width==3:
        b=np.frombuffer(raw,np.uint8).reshape(-1,3).astype(np.int32)
        x=(((b[:,0]|b[:,1]<<8|b[:,2]<<16)^8388608)-8388608)/8388608
    else:raise ValueError(width)
    x=x.reshape(-1,ch).mean(axis=1)
    nz=np.flatnonzero(abs(x)>abs(x).max()*.022)
    x=x[max(0,nz[0]-round(rate*.005)):]
    x-=x.mean()
    return x/max(abs(x).max(),1e-8),rate

def guitar(note,beats,side,opened=False,pick=0):
    length=round((beats*BEAT+.045)*SR)
    t=np.arange(length)/SR
    raw=np.zeros(length)
    for interval,gain in [(0,1),(7,.53),(12,.24 if opened else .12)]:
        root,path=min(sources,key=lambda s:abs(s[0]-note-interval))
        x,rate=sample(path)
        pos=np.arange(length)*rate/SR*2**((note+interval-root+side*.027)/12)
        pos+=pick*rate*.0015
        raw+=np.interp(pos,np.arange(len(x)),x,right=0)*gain
    raw*=np.exp(-t/(1.0 if opened else .115))
    raw=filtered(raw,90,8000)
    # Saturate the combined power chord, then shape the cabinet; no lead layer.
    y=np.tanh(raw*(5.4 if opened else 7.2))
    y=filtered(y,80,4800 if side<0 else 4200)
    y*=np.minimum(t/.003,1)*np.clip((beats*BEAT+.045-t)/.045,0,1)
    return y

def bass(note,beats):
    t=np.arange(round((beats*BEAT+.04)*SR))/SR
    f=440*2**((note-69)/12)
    phase=2*np.pi*f*t
    y=np.sin(phase)+.30*np.sin(2*phase)+.13*np.sin(3*phase)
    y=np.tanh(y*1.6)*np.exp(-t/.6)
    return y*np.minimum(t/.006,1)*np.clip((beats*BEAT+.04-t)/.04,0,1)

def drum(kind):
    if kind=='kick':
        t=np.arange(round(.30*SR))/SR
        y=np.sin(2*np.pi*(53*t+1.1*(1-np.exp(-t/.014))))*np.exp(-t/.08)
        y+=filtered(rng.normal(0,1,len(t)),1400,3800)*np.exp(-t/.009)*.14
        return y*np.minimum(t/.001,1)
    if kind=='snare':
        x=render('drum',2,.20,.8).mean(axis=1)
        t=np.arange(len(x))/SR
        x=filtered(x,140,7300)*np.exp(-t/.14)
        body=np.sin(2*np.pi*185*t)*np.exp(-t/.043)
        return x*.9+body*.24*np.minimum(t/.002,1)
    if kind=='tom':
        t=np.arange(round(.27*SR))/SR
        return np.sin(2*np.pi*(105*t+1.8*(1-np.exp(-t/.03))))*np.exp(-t/.075)*np.minimum(t/.002,1)
    d={'hat':.075,'open':.24,'crash':1.05}[kind]
    t=np.arange(round(d*SR))/SR
    y=filtered(rng.normal(0,1,len(t)),4600 if kind!='crash' else 2200,11000)
    y*=np.exp(-t/({'hat':.018,'open':.07,'crash':.32}[kind]))
    return y*np.minimum(t/.002,1)*np.clip((d-t)/.035,0,1)

# beat, semitone offset from D2, length, open chord. Not scalar lead phrases.
RIFFS=[
    [(0,0,.30,0),(.5,0,.24,0),(.875,0,.18,0),(1.5,10,.38,1),(2,0,.3,0),(2.5,0,.22,0),(3,3,.42,1),(3.625,0,.24,0)],
    [(0,0,.3,0),(.5,0,.24,0),(1,0,.28,0),(1.75,0,.18,0),(2.5,5,.42,1),(3.25,3,.28,1),(3.75,0,.20,0)],
    [(0,0,.28,0),(.5,0,.22,0),(1,0,.65,1),(2,0,.24,0),(2.5,0,.24,0),(3,10,.45,1),(3.625,12,.23,1)],
    [(0,0,.55,1),(1,0,.23,0),(1.5,0,.23,0),(2,3,.45,1),(2.75,5,.3,1),(3.5,0,.32,1)],
]

def hashes():
    return {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'Assets/_Project/Audio').glob('*') if p.is_file()}

def main():
    assert sources,'Missing CC0 guitar source bank'
    counter.clear()
    before=hashes()
    OUT.mkdir(parents=True,exist_ok=True)
    stems={k:np.zeros((N,2),np.float32) for k in ['guitars','bass','drums']}
    duck=np.ones(N)
    def put(stem,x,beat,gain,pan=0):
        if x.ndim==1:x=x[:,None]*np.sqrt(np.array([(1-pan)/2,(1+pan)/2]))
        pos=round(beat*BEAT*SR)%N
        count=min(len(x),N-pos)
        stems[stem][pos:pos+count]+=x[:count]*gain
        if count<len(x):stems[stem][:len(x)-count]+=x[count:]*gain
    for bar in range(BARS):
        intro=bar<4
        breakdown=16<=bar<20
        build=20<=bar<24
        climax=bar>=24
        energy=.75 if intro else .64 if breakdown else 1.08 if climax else 1
        root=38
        events=RIFFS[bar%4]
        if breakdown:
            root=[34,36,38,33][bar-16]
            events=[(0,0,1.2,1),(2.5,0,.75,1)]
        elif build:
            root=[34,36,38,38][bar-20]
            events=[(b,0,.20,0) for b in np.arange(0,4,.5 if bar<23 else .25)]
        for i,(at,interval,duration,opened) in enumerate(events):
            for side in [-1,1]:
                offset=.009 if side==1 else 0
                jitter=float(rng.uniform(-.003,.003)) if at else 0
                put('guitars',guitar(root+interval,duration,side,opened,i%3),bar*4+at+offset+jitter,
                    .21*energy*float(rng.uniform(.93,1.05)),side*.88)
            put('bass',bass(root-12+(interval if interval<8 else interval-12),duration+.08),bar*4+at,.19*energy)
        kicks=[0,2.5] if breakdown else [0,.875,2,2.5,3.625] if bar%2==0 else [0,1.75,2.5,3.5]
        if intro:kicks=[0,2]
        for at in kicks:
            put('drums',drum('kick'),bar*4+at,.72*energy)
            pos=round((bar*4+at)*BEAT*SR)
            length=min(round(.15*SR),N-pos)
            duck[pos:pos+length]=np.minimum(duck[pos:pos+length],1-.14*np.exp(-np.arange(length)/SR/.055))
        for at in ([2] if breakdown else [1,3]):put('drums',drum('snare'),bar*4+at,.49*energy)
        for i in range(8):
            if intro and i%2:continue
            kind='open' if climax and i%2 else 'hat'
            put('drums',drum(kind),bar*4+i*.5,.050 if i%2 else .034,pan=.33)
        if bar in [4,12,20,24,28]:put('drums',drum('crash'),bar*4,.13,pan=-.3)
        if bar%4==3 and not breakdown:
            for j in range(3):
                put('drums',drum('tom' if j>0 else 'snare'),bar*4+3.25+j*.25,.20+j*.028,pan=-.25+j*.25)
        if build:
            for i in range(4 if bar<23 else 8):
                put('drums',drum('snare'),bar*4+i*(1 if bar<23 else .5),.10+.028*(bar-20))
        # Open fifths reinforce the final passage without becoming a separate melody.
        if climax and bar%2==0:
            for side in [-1,1]:
                put('guitars',guitar(50 if bar%4==0 else 48,1.65,side,True),bar*4,.060,pan=side*.6)
    stems['guitars']*=duck[:,None]
    stems['bass']*=duck[:,None]
    dry=stems['drums'].copy()
    for delay,gain in [(.023,.10),(.041,.07),(.067,.04)]:
        stems['drums']+=np.roll(dry[:,::-1],round(delay*SR),axis=0)*gain
    mix=sum(stems.values()).astype(float)
    mix=filtered(mix,30,17000)
    mix-=mix.mean(axis=0)
    mix=np.tanh(mix*1.3)/1.3
    gain=min(10**(-14.5/20)/np.sqrt(np.mean(mix*mix)),10**(-1.5/20)/abs(mix).max())
    mix*=gain
    mix[-480:]-=np.linspace(0,1,480)[:,None]*(mix[-1]-mix[0])
    def write(name,x):
        assert np.isfinite(x).all() and abs(x).max()<1
        with wave.open(str(OUT/name),'wb') as w:
            w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
            w.writeframes(np.round(x*32767).astype('<i2').tobytes())
    write('Arena_Drive_loop.wav',mix)
    preview=mix.copy()
    preview[:960]*=np.linspace(0,1,960)[:,None]
    preview[-SR:]*=np.linspace(1,0,SR)[:,None]
    write('Arena_Drive_full_preview.wav',preview)
    excerpt=mix[round(20*4*BEAT*SR):].copy()
    excerpt[:960]*=np.linspace(0,1,960)[:,None]
    excerpt[-SR:]*=np.linspace(1,0,SR)[:,None]
    write('Arena_Drive_highlight.wav',excerpt)
    assert before==hashes(),'Runtime audio changed unexpectedly'
    with wave.open(str(OUT/'Arena_Drive_loop.wav')) as w:
        pcm=np.frombuffer(w.readframes(w.getnframes()),'<i2').reshape(-1,2).astype(float)/32768
    report=dict(title='Arena Drive',bpm=BPM,seconds=N/SR,highlight_seconds=len(excerpt)/SR,
                rms_dbfs=float(20*np.log10(np.sqrt(np.mean(pcm*pcm)))),
                sample_peak_dbfs=float(20*np.log10(abs(pcm).max())),
                clipped_samples=int(np.sum(abs(pcm)>=1)),endpoint_delta=float(abs(pcm[-1]-pcm[0]).max()),
                stereo_correlation=float(np.corrcoef(pcm.T)[0,1]),
                sections={'intro':0,'main_riff':9.6,'variation':28.8,'breakdown':38.4,'build':48,'climax':57.6},
                source_recording_sampled=False,reference_method='Local signal analysis; not direct listening or exact transcription.',
                runtime_unchanged=True)
    (OUT/'validation.json').write_text(json.dumps(report,indent=2))
    (OUT/'source_hashes.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for _,p in sources},indent=2))
    (OUT/'score.json').write_text(json.dumps({'riff_events':RIFFS,'source_manifest_sha256':hashlib.sha256((SOURCE/'manifest.json').read_bytes()).hexdigest()},indent=2))
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
