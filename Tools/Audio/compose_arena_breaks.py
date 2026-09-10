"""Original minor funk/breakbeat audition. Reuses instruments, not Arena Drive's score.
124 BPM, 40 bars. CC0 FreePats guitar / VSCO snare; procedural bass and cymbals.
NumPy only; all renders are under ignored Artifacts, never Assets.
"""
from pathlib import Path
import json
import hashlib
import wave
import numpy as np
import compose_arena_drive as kit

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Audio/ArenaBreaks'
SR=48000
BPM=124
BEAT=60/BPM
N=round(40*4*BEAT*SR)

def chop(note,beats,side):
    t=np.arange(round((beats*BEAT+.028)*SR))/SR
    y=np.zeros(len(t))
    for interval,gain in [(0,.8),(7,.42),(10,.40),(14,.24)]:
        root,path=min(kit.sources,key=lambda v:abs(v[0]-note-interval))
        x,rate=kit.sample(path)
        pos=np.arange(len(t))*rate/SR*2**((note+interval-root+side*.02)/12)
        y+=np.interp(pos,np.arange(len(x)),x,right=0)*gain
    y*=np.exp(-t/.105)
    y=kit.filtered(np.tanh(y*1.7),190,5100)
    return y*np.minimum(t/.003,1)*np.clip((beats*BEAT+.028-t)/.028,0,1)

def electric_bass(note,beats,accent):
    t=np.arange(round((beats*BEAT+.04)*SR))/SR
    f=440*2**((note-69)/12)
    phase=2*np.pi*f*t
    y=np.sin(phase)+.46*np.sin(2*phase)+.25*np.sin(3*phase)+.12*np.sin(4*phase)
    pluck=.16*np.sin(6*phase)*np.exp(-t/.026)
    y=np.tanh((y+pluck)*1.4)*np.exp(-t/(.40 if accent else .23))
    return y*np.minimum(t/.003,1)*np.clip((beats*BEAT+.04-t)/.04,0,1)

# Onsets interlock with guitar chops; octave answers carry the hook, no lead track.
BASS=[
    [(0,0,.48),(.75,0,.23),(1.5,12,.26),(2.25,7,.3),(3,0,.25),(3.5,10,.22)],
    [(0,0,.3),(.5,12,.24),(1.25,10,.26),(2,0,.45),(2.75,3,.27),(3.5,7,.25)],
    [(0,0,.55),(1,0,.24),(1.75,7,.27),(2.5,12,.32),(3.25,10,.28)],
    [(0,0,.3),(.75,7,.3),(1.5,12,.3),(2.25,10,.26),(3,7,.25),(3.5,0,.32)],
]
CHOPS=[[.5,1.25,2,2.75,3.5],[.25,1,1.75,2.5,3.25],[.75,1.5,2.75,3.5],[.5,1.25,2.25,3.25,3.75]]

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    before=kit.hashes()
    kit.BEAT=BEAT
    kit.rng=np.random.default_rng(20260912)
    kit.counter.clear()
    rng=np.random.default_rng(20260912)
    stems={k:np.zeros((N,2),np.float32) for k in ['guitar','bass','drums','rhythm']}
    duck=np.ones(N)
    def put(stem,x,beat,gain,pan=0):
        if x.ndim==1:x=x[:,None]*np.sqrt(np.array([(1-pan)/2,(1+pan)/2]))
        p=round(beat*BEAT*SR)%N
        n=min(len(x),N-p)
        stems[stem][p:p+n]+=x[:n]*gain
        if n<len(x):stems[stem][:len(x)-n]+=x[n:]*gain
    for bar in range(40):
        intro=bar<4
        hook=12<=bar<20 or 28<=bar<36
        breakdown=20<=bar<24
        build=24<=bar<28
        energy=.80 if intro else .66 if breakdown else 1.05 if hook else .94
        root=40 if not hook else [40,40,43,45][(bar//2)%4]
        if breakdown:root=[40,38,40,38][bar%4]
        events=BASS[bar%4]
        if breakdown:events=[(0,0,.7),(1.75,12,.35),(3,7,.40)]
        for b,interval,d in events:
            put('bass',electric_bass(root-12+interval,d,b%1==0),bar*4+b,.21*energy)
        for side in [-1,1]:
            chops=CHOPS[bar%4] if not breakdown else [.75,2.75]
            for i,b in enumerate(chops):
                put('guitar',chop(root+12,.16 if i%2 else .23,side),bar*4+b+(side+1)*.006,
                    .16*energy*float(rng.uniform(.9,1.06)),side*.75)
        # Lower crunchy accents make this fit the arena; no singing guitar melody.
        if hook:
            for b,d in [(0,.4),(2.25,.3)]:
                for side in [-1,1]:
                    put('rhythm',kit.guitar(root,d,side,True),bar*4+b+(side+1)*.007,.085,side*.9)
        kicks=([0,.75,2.5,3.5] if bar%2==0 else [0,1.5,2.25,3.75])
        if breakdown:kicks=[0,2.5]
        for b in kicks:
            put('drums',kit.drum('kick'),bar*4+b,.70*energy)
            p=round((bar*4+b)*BEAT*SR);n=min(round(.12*SR),N-p)
            duck[p:p+n]=np.minimum(duck[p:p+n],1-.12*np.exp(-np.arange(n)/SR/.04))
        for b in [1,3]:put('drums',kit.drum('snare'),bar*4+b,.48*energy)
        # Quiet ghost notes and tiny alternating delays, rather than louder hats.
        for b in ([.75,2.625,3.75] if bar%2 else [1.75,2.75]):
            if not intro and not breakdown:put('drums',kit.drum('snare'),bar*4+b,.065,pan=-.12)
        for i in range(16):
            if intro and i%2:continue
            if breakdown and i%4 not in [0,2]:continue
            b=i*.25+(.025 if i%2 else 0)
            opened=hook and i in [6,14]
            put('drums',kit.drum('open' if opened else 'hat'),bar*4+b,
                (.038 if i%2==0 else .014)*energy,pan=.25)
        if bar in [4,12,28,36]:put('drums',kit.drum('crash'),bar*4,.11,pan=-.35)
        if bar%4==3 and not breakdown:
            for j in range(3):put('drums',kit.drum('tom'),bar*4+3.25+j*.25,.12+j*.025,pan=-.2+j*.2)
        if build:
            for b in np.arange(0,4,.5 if bar==27 else 1):
                put('drums',kit.drum('snare'),bar*4+float(b),.065+.016*(bar-24))
    for key in ['guitar','bass','rhythm']:stems[key]*=duck[:,None]
    dry=stems['drums'].copy()
    for d,g in [(.021,.085),(.043,.05),(.071,.025)]:
        stems['drums']+=np.roll(dry[:,::-1],round(d*SR),axis=0)*g
    mix=kit.filtered(sum(stems.values()).astype(float),30,16500)
    mix-=mix.mean(axis=0)
    mix=np.tanh(mix*1.25)/1.25
    mix*=min(10**(-14.5/20)/np.sqrt(np.mean(mix*mix)),10**(-1.5/20)/abs(mix).max())
    mix[-480:]-=np.linspace(0,1,480)[:,None]*(mix[-1]-mix[0])
    def write(name,x):
        assert np.isfinite(x).all() and abs(x).max()<1
        with wave.open(str(OUT/name),'wb') as w:
            w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
            w.writeframes(np.round(x*32767).astype('<i2').tobytes())
    write('Arena_Breaks_loop.wav',mix)
    preview=mix.copy();preview[:960]*=np.linspace(0,1,960)[:,None];preview[-SR:]*=np.linspace(1,0,SR)[:,None]
    write('Arena_Breaks_full_preview.wav',preview)
    excerpt=mix[round(4*4*BEAT*SR):round(20*4*BEAT*SR)].copy()
    excerpt[:960]*=np.linspace(0,1,960)[:,None];excerpt[-SR:]*=np.linspace(1,0,SR)[:,None]
    write('Arena_Breaks_highlight.wav',excerpt)
    assert before==kit.hashes()
    with wave.open(str(OUT/'Arena_Breaks_loop.wav')) as w:
        pcm=np.frombuffer(w.readframes(w.getnframes()),'<i2').reshape(-1,2).astype(float)/32768
    report=dict(title='Arena Breaks',bpm=BPM,seconds=N/SR,highlight_seconds=len(excerpt)/SR,
                rms_dbfs=float(20*np.log10(np.sqrt(np.mean(pcm*pcm)))),
                sample_peak_dbfs=float(20*np.log10(abs(pcm).max())),
                endpoint_delta=float(abs(pcm[-1]-pcm[0]).max()),clipped_samples=int(np.sum(abs(pcm)>=1)),
                stereo_correlation=float(np.corrcoef(pcm.T)[0,1]),runtime_unchanged=True,
                reference_recording_sampled=False,
                reference_method='Signal measurements, not direct listening or exact transcription.',
                sections={k:round(b*4*BEAT,3) for k,b in [('intro',0),('groove',4),('hook',12),('break',20),('build',24),('lift',28),('turnaround',36)]})
    (OUT/'validation.json').write_text(json.dumps(report,indent=2))
    (OUT/'score.json').write_text(json.dumps(dict(bass=BASS,guitar_onsets=CHOPS),indent=2))
    (OUT/'source_hashes.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for _,p in kit.sources},indent=2))
    source_report=ROOT/'Artifacts/Audio/ReferenceRock/analysis.json'
    if source_report.exists():(OUT/'reference_analysis.json').write_bytes(source_report.read_bytes())
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
