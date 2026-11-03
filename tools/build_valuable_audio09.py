"""Original material Foley and mechanical cues; canonical mono PCM16."""
import math,random,wave,struct
from pathlib import Path
OUT=Path(__file__).resolve().parents[1]/'src/Audio';RATE=22050
for name,duration in [('metal',.38),('plastic',.20),('glass',.65),('bone',.29),('paper',.22),('roll',.38),('motor',.72),('flip',.34),('drip',.28),('tube',.35)]:
    rng=random.Random('valuable09_'+name);values=[];low=0
    for i in range(round(duration*RATE)):
        t=i/RATE;n=rng.uniform(-1,1);low=.88*low+.12*n
        if name in ['glass','tube']:v=sum(math.sin(2*math.pi*f*t)*math.exp(-t*d) for f,d in [(1327,12),(2179,16),(3263,22)])*.16+n*.13*math.exp(-t*65)
        elif name=='metal':v=.2*math.sin(2*math.pi*213*t)*math.exp(-t*17)+.13*math.sin(2*math.pi*689*t)*math.exp(-t*25)+low*.5*math.exp(-t*25)
        elif name=='motor':v=(.14*math.sin(2*math.pi*91*t)+.045*math.sin(2*math.pi*273*t)+low*.16)*math.sin(math.pi*t/duration)**2
        elif name=='drip':v=math.sin(2*math.pi*(660*t+100*t*t))*math.exp(-t*21)*.27
        elif name=='roll':v=(low*.65+math.sin(2*math.pi*67*t)*.10)*math.sin(math.pi*t/duration)**2
        elif name=='paper':v=(n-low)*.32*math.sin(math.pi*t/duration)**2
        else:v=(low*.60+.22*math.sin(2*math.pi*(330 if name=='bone' else 180)*t))*math.exp(-t*27)
        values.append(v*max(0,min(1,t/.003,(duration-t)/.015)))
    peak=max(abs(v) for v in values);samples=[round(v/peak*.62*32767) for v in values]
    with wave.open(str(OUT/('valuable_'+name+'.wav')),'wb') as f:f.setparams((1,2,RATE,len(samples),'NONE','not compressed'));f.writeframes(struct.pack('<'+'h'*len(samples),*samples))
print('PASS: 10 original valuable cues generated')
