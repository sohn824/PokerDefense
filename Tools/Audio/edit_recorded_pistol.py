"""CC0 Free Firearm Sound Library 1911 near recording -> dedicated pistol cue."""
from pathlib import Path
import wave, hashlib, json
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'Artifacts/Audio/FirearmSource/Prepared SFX Library/1911/A_42P.wav'
OUT=ROOT/'Artifacts/Audio/RecordedPistol'
OUT.mkdir(parents=True,exist_ok=True)
assert hashlib.sha256(SRC.read_bytes()).hexdigest()=='8e84438e771c157155a6a1ff47a6a7a7d81b6f39b185d41e426c57337a82254a'
with wave.open(str(SRC)) as w:
    assert w.getsampwidth()==3 and w.getframerate()==96000 and w.getnchannels()==2
    b=np.frombuffer(w.readframes(w.getnframes()),np.uint8).reshape(-1,3).astype(np.int32)
v=b[:,0]|b[:,1]<<8|b[:,2]<<16
x=((v^8388608)-8388608).reshape(-1,2)/8388608
def write(p,a):
    with wave.open(str(p),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(48000)
        w.writeframes(np.round(a*32767).astype('<i2').tobytes())
guns=[];report=[]
for i,onset in enumerate([.94145833,5.00154167,.94145833]):
    mono=x.mean(1) if i<2 else x[:,0]*.8+x[:,1]*.2
    f=np.fft.rfftfreq(len(mono),1/96000)
    mono=np.fft.irfft(np.fft.rfft(mono)*(1-np.exp(-(f/90)**4))*np.exp(-(f/14000)**8),len(mono))
    start=round((onset-.0015)*96000)
    a=mono[start:start+15360:2].copy()
    t=np.arange(len(a))/48000
    a-=a.mean()
    a*=np.exp(-np.maximum(t-.025,0)/.045)
    a=np.tanh(a/np.max(np.abs(a))*2)
    a*=np.minimum(t/.0004,1)*np.clip((.16-t)/.022,0,1)
    a*=10**(-3/20)/np.max(np.abs(a))
    assert np.isfinite(a).all() and len(a)==7680
    name='sfx_fire_pistol'+('' if i==0 else '_v'+str(i+1))+'.wav'
    write(OUT/name,a);write(ROOT/'Assets/_Project/Audio'/name,a)
    guns.append(a)
    report.append(dict(file=name,seconds=.16,peak_dbfs=-3,source_onset=onset,sha256=hashlib.sha256((OUT/name).read_bytes()).hexdigest()))
demo=np.zeros(10*48000)
for j,time in enumerate([.25,1,1.75]+[3+k/2.8 for k in range(7)]+[6+k/6.3 for k in range(18)]):
    pos=round(time*48000);a=guns[j%3]
    demo[pos:pos+len(a)]+=a*.65*.22
assert np.max(np.abs(demo))<1
write(OUT/'Pistol_Preview.wav',demo)
(OUT/'validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
