"""Small deterministic multisample renderer for the pinned CC0 VSCO subset.
Source octave convention: C3 = MIDI 60 (checked against recorded pitch).
No plugin, DAW or source samples are needed to play the resulting game WAV.
"""
from pathlib import Path
from functools import lru_cache
import hashlib
import json
import re
import wave
import numpy as np

SOURCE = Path(__file__).resolve().parents[2]/'Artifacts/Audio/OrchestraSource'
SR = 48000
manifest = json.loads((SOURCE/'manifest.json').read_text())
bank = {}
for entry in manifest:
    match = re.search(r'_([A-G])(#?)(\d)_', Path(entry['path']).name)
    pitch = (int(match[3])+2)*12 + {'C':0,'D':2,'E':4,'F':5,'G':7,'A':9,'B':11}[match[1]] + bool(match[2]) if match else 0
    bank.setdefault(entry['group'], []).append((pitch, entry))
counter = {}

@lru_cache(maxsize=128)
def load(path, digest):
    target = SOURCE/path
    assert hashlib.sha256(target.read_bytes()).hexdigest() == digest
    with wave.open(str(target)) as w:
        rate, channels, width = w.getframerate(), w.getnchannels(), w.getsampwidth()
        payload = w.readframes(w.getnframes())
    if width == 2:
        a = np.frombuffer(payload, '<i2').astype(float)/32768
    elif width == 3:
        b = np.frombuffer(payload, np.uint8).reshape(-1,3).astype(np.int32)
        p = b[:,0] | b[:,1]<<8 | b[:,2]<<16
        a = ((p ^ 8388608)-8388608)/8388608
    else:
        raise ValueError('Unsupported PCM width')
    a = a.reshape(-1,channels)
    if channels == 1:
        a = np.repeat(a,2,axis=1)
    # Remove recording lead-in without removing the audible attack.
    peak = np.max(np.abs(a),axis=1)
    onset = np.flatnonzero(peak > peak.max()*.012)[0]
    a = a[max(0,onset-round(rate*.008)):]
    a -= np.mean(a,axis=0)
    a *= .8/max(np.max(np.abs(a)),1e-8)
    return a, rate

def render(group, note, duration, strength=1):
    candidates = bank[group]
    if group == 'drum':
        label = {0:'BDrum',1:'Timpani',2:'Snare2-HitSN'}[note]
        candidates = [(p,e) for p,e in candidates if label in e['path']]
    root = min(candidates,key=lambda item:abs(item[0]-note))[0]
    choices = [e for p,e in candidates if p == root]
    key = (group,note if group == 'drum' else root)
    count = counter.get(key,0)
    counter[key] = count+1
    entry = choices[count % len(choices)]
    a, rate = load(entry['path'],entry['sha256'])
    shift = 1 if group == 'drum' else 2**((note-root)/12)
    length = round((duration+.14)*SR)
    positions = np.arange(length)*rate/SR*shift
    out = np.column_stack([np.interp(positions,np.arange(len(a)),a[:,ch],right=0) for ch in range(2)])
    t = np.arange(length)/SR
    sustained = group in ['horn','trombone','strings','cello']
    attack = .022 if sustained else .001
    gain = np.minimum(t/attack,1)*np.clip((duration+.14-t)/.14,0,1)
    if sustained:
        gain *= .80+.20*np.sin(np.pi*np.minimum(t/max(duration,.01),1))
    # Broad placement, preserving each stereo recording rather than artificial detune.
    pan = {'strings':-.25,'cello':.2,'horn':.12,'horn_short':.12,'trombone':-.1,'short':.15,'harp':-.30,'drum':0}[group]
    out *= np.sqrt(np.array([1-pan,1+pan]))
    return out*gain[:,None]*strength
