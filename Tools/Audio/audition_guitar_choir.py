"""Two sample-based candidates on an identical backing; never writes Assets."""
from pathlib import Path
import hashlib, json, re, wave, argparse
from functools import lru_cache
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--guitar-developed',action='store_true')
parser.add_argument('--lead-alternative',action='store_true')
parser.add_argument('--no-lead',action='store_true')
args=parser.parse_args()
if args.lead_alternative or args.no_lead:
    args.guitar_developed=True
OUT=ROOT/('Artifacts/Audio/GuitarRhythmOnly' if args.no_lead else 'Artifacts/Audio/GuitarLeadAlternative' if args.lead_alternative else 'Artifacts/Audio/GuitarDeveloped' if args.guitar_developed else 'Artifacts/Audio/GuitarChoirCandidates')
OUT.mkdir(parents=True,exist_ok=True)
assets=ROOT/'Assets/_Project/Audio'
before={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in assets.glob('*') if p.is_file()}
# Reuse the backing generator without executing its asset export or brass phrases.
source=(ROOT/'Tools/Audio/rework_combat_theme.py').read_text(encoding='utf-8')
prefix=source[:source.index('# Diffuse hall reflections;')]
start=prefix.index('    # Sparse low sustained phrases')
prefix=prefix[:start]
ctx={'__file__':str(ROOT/'Tools/Audio/rework_combat_theme.py')}
exec(compile(prefix,'shared_backing','exec'),ctx)
SR,N=ctx['SR'],ctx['N']
base=sum(ctx['stems'][s] for s in ['drums','bass','pad','fx']).astype(float)
np.save(OUT/'shared_backing.npy',base)
guitar_dir=next((ROOT/'Artifacts/Audio/GuitarSource').glob('EGuitar*'))
voice_dir=ROOT/'Artifacts/Audio/VoiceSource'

def midi(name):
    m=re.match(r'([A-G])(#?)(\d)',name)
    return (int(m[3])+1)*12+{'C':0,'D':2,'E':4,'F':5,'G':7,'A':9,'B':11}[m[1]]+bool(m[2])

banks={'guitar':[(midi(p.stem),p) for p in guitar_dir.rglob('*.wav')],
       'choir':[(midi(p.stem),p) for p in voice_dir.glob('*.wav')]}

@lru_cache(maxsize=64)
def read(path):
    with wave.open(str(path)) as w:
        rate,ch,width=w.getframerate(),w.getnchannels(),w.getsampwidth()
        b=w.readframes(w.getnframes())
    if width==2:
        x=np.frombuffer(b,'<i2').astype(float)/32768
    elif width==3:
        b=np.frombuffer(b,np.uint8).reshape(-1,3).astype(np.int32)
        p=b[:,0]|b[:,1]<<8|b[:,2]<<16
        x=((p^8388608)-8388608)/8388608
    else:
        raise ValueError(width)
    x=x.reshape(-1,ch).mean(axis=1)
    peak=np.max(np.abs(x))
    idx=np.flatnonzero(np.abs(x)>peak*.045)
    x=x[max(0,idx[0]-round(rate*.006)):idx[-1]+1]
    x-=x.mean()
    return x/max(np.max(np.abs(x)),1e-9)*.7,rate

def sample(kind,note,d,detune=1,sustain=False):
    key,path=min(banks[kind],key=lambda item:abs(item[0]-note))
    x,rate=read(path)
    # Resampling alters pitch; voice recordings are layered, not cloned speech.
    pos=np.arange(round(d*SR))*rate/SR*2**((note-key)/12)*detune
    y=np.interp(pos,np.arange(len(x)),x,right=0)
    if kind=='choir' and pos[-1]>=len(x):
        # Overlap-add the middle of the vowel with smooth windows for sustains.
        segment=x[len(x)//4:len(x)*3//4]
        z=np.interp(np.arange(round(len(segment)*SR/rate/2**((note-key)/12))),
                    np.arange(len(segment))*SR/rate/2**((note-key)/12),segment)
        hop=max(1,len(z)//2)
        fill=np.zeros(len(y)+len(z)); weights=np.zeros_like(fill)
        window=np.hanning(len(z))
        for at in range(0,len(y),hop):
            fill[at:at+len(z)]+=z*window
            weights[at:at+len(z)]+=window
        fill=fill[:len(y)]/np.maximum(weights[:len(y)],.01)
        cross=np.clip((np.arange(len(y))/SR-.25)/.25,0,1)
        y=y*(1-cross)+fill*cross
    t=np.arange(len(y))/SR
    envelope=np.minimum(t/(.12 if kind=='choir' else .006),1)*np.clip((d-t)/(.22 if kind=='choir' else .04),0,1)
    if kind=='guitar':
        y*=np.exp(-t/(.70 if sustain else .12))
        y=np.tanh(y*(3.3 if sustain else 5))
        f=np.fft.rfftfreq(len(y),1/SR)
        y=np.fft.irfft(np.fft.rfft(y)*(1-np.exp(-(f/80)**4))*np.exp(-(f/4400)**4),len(y))
    return y*envelope

def add(dst,x,beat,gain):
    at=round(beat*.4*SR)%N
    n=min(len(x),N-at)
    dst[at:at+n]+=x[:n]*gain
    if n<len(x): dst[:len(x)-n]+=x[n:]*gain

def write(path,x):
    assert np.isfinite(x).all() and np.max(np.abs(x))<1
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
        w.writeframes(np.round(x*32767).astype('<i2').tobytes())

# Distinct riff gestures; tuples are onset, interval and duration in beats.
riffs=[[(0,0,.45),(.75,0,.4),(1.5,7,.4),(2,0,.6),(3,3,.4),(3.5,1,.4)],
       [(0,0,.7),(1,7,.4),(1.75,0,.4),(2.5,1,.4),(3.25,0,.5)],
       [(0,0,.4),(.5,0,.4),(1.5,12,.6),(2.5,7,.4),(3.5,0,.4)],
       [(0,0,.7),(1.25,3,.4),(2,1,.6),(3,0,.7)]]
phrases=[[(.5,7,.6),(1.5,12,1.1),(3,10,.65)],
         [(0,7,1.2),(2,3,.6),(3,5,.65)],
         [(0,7,.65),(1,8,.45),(1.75,12,1.3)],
         [(.5,10,.6),(1.5,7,.7),(2.75,5,.8)],
         [(0,12,1.1),(1.5,15,.6),(2.5,14,1.0)],
         [(0,12,.7),(1,10,.6),(2,7,1.3)],
         [(0,8,.6),(1,7,.6),(2,5,.6),(3,3,.65)],
         [(0,1,.65),(1.25,3,.55),(2,0,1.35)]]
report=[]
alternative=[[(.25,7,.45),(1,5,.55),(2.25,3,.9)],
             [(.5,0,.65),(2,1,.4),(2.75,0,.65)],
             [(0,3,.45),(.75,7,.65),(2.5,5,.6)],
             [(1,3,.7),(2.5,0,.85)],
             [(.25,7,.45),(1,8,.4),(1.75,7,.5),(3,3,.6)],
             [(0,5,.6),(1.25,3,.45),(2,1,.55)],
             [(.5,3,.6),(1.75,5,.4),(2.5,1,.6)],
             [(0,0,1.0),(2.5,7,.45),(3.25,0,.5)]]
for kind in (['guitar'] if args.guitar_developed else ['guitar','choir']):
    lead=np.zeros_like(base)
    rhythm=np.zeros_like(base)
    for bar in range(48):
        root=ctx['roots'][(bar//2)%8]
        if kind=='guitar':
            pattern=riffs[bar%4] if args.guitar_developed else [(b,i,.75) for b,i in [(0,0),(.75,0),(1.5,7),(2,0),(2.75,1),(3.5,0)]]
            for beat,interval,beats in pattern:
                d=beats*.4
                left=sample(kind,root+interval,d,.999)
                right=sample(kind,root+interval,d,1.001)
                power=sample(kind,root+interval+7,d)
                sound=np.column_stack([left+.35*power,right+.35*power])
                gain=.085 if args.guitar_developed else .12
                add(lead,sound,bar*4+beat,gain)
                add(rhythm,sound,bar*4+beat,gain)
            if args.guitar_developed and not args.no_lead:
                # Lead enters after four bars; B section has a longer, lower response.
                events=(alternative if args.lead_alternative else phrases)[(bar-4)%8]
                if bar<4: events=[]
                elif 16<=bar<24:
                    events=([(0,3,.65),(1.5,1,.55),(3,0,.7)] if bar%2==0 else [(.75,5,.65),(2,3,.8)]) if args.lead_alternative else ([ (.5,7,1.5),(2.5,3,1.0)] if bar%2==0 else [(1,5,1.0),(2.5,0,1.0)])
                elif 40<=bar<44:
                    events=events[:2]
                for beat,interval,beats in events:
                    note=root+12+interval
                    x=sample(kind,note,beats*.4,sustain=True)
                    add(lead,np.column_stack([x*.95,x]),bar*4+beat,.11)
                    if 28<=bar<40 and beats>=1 and not args.lead_alternative:
                        harmony=sample(kind,note-12,beats*.4,sustain=True)
                        add(lead,np.column_stack([harmony,harmony*.85]),bar*4+beat+.03,.035)
        elif bar%2==0:
            for interval in [12,19]:
                note=max(43,root+interval)
                voices=np.zeros((round(2.7*SR),2))
                for singer,det in enumerate([.995,.998,1.002,1.005]):
                    v=sample(kind,note,2.7,det)
                    delay=round((singer*.013)*SR)
                    v=np.r_[np.zeros(delay),v][:len(voices)]
                    voices[:,singer%2]+=v*.5
                add(lead,voices,bar*4,.13)
    if kind=='guitar':
        np.save(OUT/'rhythm_guitar.npy',rhythm)
    dry=lead.copy()
    for delay,gain in [(.047,.10),(.113,.07),(.239,.06),(.431,.045),(.719,.03)]:
        lead+=np.roll(dry[:,::-1],round(delay*SR),axis=0)*gain*(1 if kind=='choir' else .35)
    mix=base+lead
    mix-=mix.mean(axis=0)
    mix=np.tanh(mix*1.7)/1.7
    # Identical RMS target so louder does not automatically win the comparison.
    mix*=10**(-17/20)/np.sqrt(np.mean(mix*mix))
    assert np.max(np.abs(mix))<10**(-1/20)
    mix[-192:]-=np.linspace(0,1,192)[:,None]*(mix[-1]-mix[0])
    write(OUT/(kind+'_loop.wav'),mix)
    preview=mix.copy();preview[-SR:]*=np.linspace(1,0,SR)[:,None]
    write(OUT/(kind+'_preview.wav'),preview)
    report.append(dict(variant=kind,developed=args.guitar_developed,seconds=N/SR,rms_dbfs=float(20*np.log10(np.sqrt(np.mean(mix*mix)))),peak_dbfs=float(20*np.log10(np.max(np.abs(mix)))),endpoint_delta=float(np.max(np.abs(mix[-1]-mix[0])))))
assert before=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in assets.glob('*') if p.is_file()}
(OUT/'validation.json').write_text(json.dumps(report,indent=2))
(OUT/'source-hashes.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for kind in banks for _,p in banks[kind]},indent=2))
print(json.dumps(report,indent=2));print('All runtime audio files and metadata unchanged')
