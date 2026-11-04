"""Original synthesized hollow porcelain impact and fracture, canonical PCM16 mono."""
import math,random,wave,struct,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];RATE=22050;report=[]
for name,duration in [('porcelain',.42),('porcelain_break',.78)]:
    rng=random.Random(name);events=[(0,1)]
    if name.endswith('break'):events += [(rng.uniform(.014,.42),rng.uniform(.12,.48)) for _ in range(22)]
    values=[];low=0
    for i in range(round(duration*RATE)):
        t=i/RATE;noise=rng.uniform(-1,1);low=.75*low+.25*noise;v=0
        for onset,gain in events:
            u=t-onset
            if u<0:continue
            modes=sum(a*math.sin(math.tau*f*u)*math.exp(-decay*u) for f,a,decay in [(510,.23,24),(1171,.19,31),(2437,.13,44),(3721,.07,66)])
            v+=gain*(modes+(noise-low)*.17*math.exp(-u*120)+low*.22*math.exp(-u*50))
        values.append(v*min(1,t/.002,(duration-t)/.02))
    peak=max(map(abs,values));samples=[round(v/peak*.64*32767) for v in values]
    path=ROOT/'src/Audio'/('valuable_'+name+'.wav')
    with wave.open(str(path),'wb') as f:f.setparams((1,2,RATE,len(samples),'NONE','not compressed'));f.writeframes(struct.pack('<'+'h'*len(samples),*samples))
    rms=math.sqrt(sum(s*s for s in samples)/len(samples))
    assert 300<rms<12000 and max(map(abs,samples))<30000 and abs(samples[0])<10 and abs(samples[-1])<100
    report.append({'cue':path.name,'duration_s':duration,'rate_hz':RATE,'channels':1,'bits':16,'peak':max(map(abs,samples)),'rms':rms})
(ROOT/'art/valuables/maneki-integrated-14/audio-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
