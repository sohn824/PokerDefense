"""Offline listening montage, not a recording of Unity runtime output."""
from pathlib import Path
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
FOLDER=ROOT/'Assets/_Project/Audio'
OUT=ROOT/'Artifacts/Audio/PolishV2'
SR=48000

def read(name):
    with wave.open(str(FOLDER/(name+'.wav'))) as w:
        raw=np.frombuffer(w.readframes(w.getnframes()),np.uint8).reshape(-1,3).astype(np.int32)
        ints=raw[:,0]|raw[:,1]<<8|raw[:,2]<<16
        a=((ints^0x800000)-0x800000).reshape(-1,w.getnchannels())/8388608
        return a if w.getnchannels()==2 else np.repeat(a,2,axis=1)

n=round(25.6*SR); mix=np.zeros((n,2))
t=np.arange(n)/SR
combat=np.clip((t-6.4)/.8,0,1)
boss=np.clip((t-19.2)/.8,0,1)
mix+=read('bgm_plan')[:n]*(1-combat)[:,None]*.55
mix+=read('bgm_combat')[:n]*(combat-boss)[:,None]*.55
mix+=read('bgm_boss')[:n]*boss[:,None]*.55
music_only=mix.copy()
def cue(name,at,gain):
    a=read('sfx_'+name)*.65*gain; pos=round(at*SR)
    count=min(len(a),n-pos); mix[pos:pos+count]+=a[:count]
cue('ui_card_deal',.5,.18)
cue('ui_card_select',1.5,.25); cue('ui_card_select_v2',1.9,.25)
cue('ui_exchange',2.3,.22); cue('ui_card_flip',2.95,.22)
cue('hand_mid',3.4,.20); cue('merge_star2',4.4,.24)
for i in range(20): cue('fire_rapid',8+i*.22,.22)
for i in range(4): cue('fire_heavy',13+i*.65,.22)
cue('fire_multi',16,.22); cue('fire_splash',17,.22); cue('fire_pierce',18,.22)
cue('state_wave_clear',18.5,.16)
cue('boss_appear',19.2,.33); cue('merge_star3',23.2,.28)
mix[:480]*=np.linspace(0,1,480)[:,None]
mix[-SR:]*=np.linspace(1,0,SR)[:,None]
assert np.max(np.abs(mix))<1
with wave.open(str(OUT/'Audio_Polish_Preview.wav'),'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(np.round(mix*32767).astype('<i2').tobytes())
print('25.6-second offline preview written; no post-mix loudness boost.')
music_only[:480]*=np.linspace(0,1,480)[:,None]
music_only[-SR:]*=np.linspace(1,0,SR)[:,None]
with wave.open(str(OUT/'BGM_Driving_Preview.wav'),'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(np.round(music_only*32767).astype('<i2').tobytes())
