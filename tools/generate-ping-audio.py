from pathlib import Path
import math, struct, wave, hashlib

out=Path(__file__).resolve().parent.parent/'src/Audio';out.mkdir(exist_ok=True)
cues={
 'ping_location':[(523,0,.30,.8),(784,.10,.28,.28)],
 'ping_valuable':[(660,0,.25,.7),(990,.10,.30,.5)],
 'ping_item':[(440,0,.25,.7),(554,.08,.24,.4)],
 'ping_upgrade':[(440,0,.22,.55),(660,.10,.24,.65),(880,.20,.32,.5)],
 'ping_enemy':[(220,0,.19,.75),(185,.22,.23,.65)],
 'ping_cart':[(330,0,.28,.7),(495,.12,.28,.6)],
 'ping_cosmetic':[(784,0,.24,.5),(988,.10,.27,.5),(1175,.20,.30,.35)],
 'ping_vehicle':[(294,0,.22,.7),(392,.09,.25,.6)],
}
rate=24000
for name,notes in cues.items():
 n=int((max(start+duration for f,start,duration,amp in notes)+.07)*rate)
 samples=[]
 for i in range(n):
  t=i/rate;v=0
  for f,start,duration,amp in notes:
   u=(t-start)/duration
   if 0<u<1:
    envelope=math.sin(math.pi*u)**2*math.exp(-1.4*u)
    phase=2*math.pi*f*(t-start)
    v+=amp*envelope*(math.sin(phase)+.12*math.sin(phase*2))
  samples.append(v)
 gain=.22/max(map(abs,samples))
 path=out/(name+'.wav')
 with wave.open(str(path),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate)
  w.writeframes(b''.join(struct.pack('<h',round(v*gain*32767)) for v in samples))
 print(name, f'{n/rate:.2f}s',hashlib.sha256(path.read_bytes()).hexdigest()[:12])
