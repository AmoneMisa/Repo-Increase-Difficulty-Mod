"""Original deterministic Foley/synth cues, mono PCM16 22050 Hz. No external recordings."""
import math, random, struct, wave
from pathlib import Path
OUT=Path(__file__).resolve().parents[1]/'src/Audio'; OUT.mkdir(parents=True,exist_ok=True)
RATE=22050
def make(name,seconds,kind,freq=400):
    rng=random.Random(name); samples=[]; low=0
    for i in range(int(seconds*RATE)):
        t=i/RATE; noise=rng.uniform(-1,1); low=low*.84+noise*.16
        hit=(noise-low)*math.exp(-t*85)
        if kind=='bow':
            value=.7*hit+.3*math.sin(2*math.pi*(150*t+23*(1-math.exp(-t*18))))*math.exp(-t*9)+.25*low*math.exp(-t*6)
        elif kind=='ice':
            value=.6*hit+.24*math.sin(2*math.pi*(700*t-230*t*t))*math.exp(-t*7)+.48*(noise-low)*math.exp(-t*10)
        elif kind=='sticky':
            envelope=(1-math.exp(-t*40))*math.exp(-t*11)
            value=(.18*math.sin(2*math.pi*(540*t-260*t*t))+.22*low)*envelope
        elif kind=='latch':
            value=.18*math.sin(2*math.pi*720*t)*(1-math.exp(-t*100))*math.exp(-t*34)
        elif kind=='frost':
            pulse=max(0,math.sin(t*83))**10
            value=.5*(noise-low)*(math.exp(-t*8)+pulse*.3)*math.exp(-t*3)+.13*math.sin(t*2*math.pi*1300)*math.exp(-t*6)
        elif kind=='poison':
            value=.6*low*math.exp(-t*5)+.15*math.sin(2*math.pi*(220*t+9*math.sin(t*14)))*math.exp(-t*7)
        elif kind=='spritz':
            value=.25*(noise-low)*math.sin(math.pi*t/seconds)**2+.12*math.sin(2*math.pi*1800*t)*math.exp(-t*75)
        else:
            envelope=math.sin(math.pi*min(1,t/seconds))**2
            value=(.26*math.sin(2*math.pi*(freq*t+70*t*t))+.08*math.sin(2*math.pi*freq*1.49*t)+.08*low)*envelope
        fade=min(1,t/.003,(seconds-t)/.02)
        samples.append(value*max(0,fade))
    peak=max(abs(x) for x in samples)
    level=.30 if kind in ('sticky','latch') else .72
    samples=[int(x/max(peak,.001)*level*32767) for x in samples]
    with wave.open(str(OUT/(name+'.wav')),'wb') as out:
        out.setparams((1,2,RATE,len(samples),'NONE','not compressed'))
        out.writeframes(struct.pack('<'+'h'*len(samples),*samples))
for name,seconds,kind in [('poison_shot',.45,'bow'),('ice_shot',.42,'ice'),('sticky_shot',.48,'sticky'),('ice_hit',.65,'frost'),('poison_hit',.55,'poison')]: make(name,seconds,kind)
make('sticky_hit',.18,'latch')
for i,name in enumerate(('ghost','silencer','link_a','link_b','repair','defence','protection','decoy')):
    make(name+'_activate',.28 if name!='decoy' else .4,'chirp',220+i*63)
make('power_down',.22,'chirp',140)
make('spritz_shot',.26,'spritz')
print('Created 16 original item cues')
