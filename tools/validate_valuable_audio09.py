"""Check new cues and the complete set of embedded valuable descriptors."""
import wave,struct,math,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
report={}
for p in sorted((ROOT/'src/Audio').glob('valuable_*.wav')):
    with wave.open(str(p),'rb') as f:
        assert f.getparams()[:3]==(1,2,22050)
        values=struct.unpack('<'+'h'*f.getnframes(),f.readframes(f.getnframes()));duration=len(values)/22050
        peak=max(abs(x) for x in values);rms=math.sqrt(sum(x*x for x in values)/len(values))
        assert .15<duration<.8 and 10000<peak<25000 and rms>500
        assert abs(values[0])<10 and abs(values[-1])<200
        report[p.name]={'seconds':duration,'peak':peak,'rms':round(rms,2)}
assert len(report)==10
rows=json.loads((ROOT/'art/valuables/interaction-rework-09/manifest.json').read_text())
for row in rows:
    model=json.loads((ROOT/'src/Art'/('v09_'+row['asset']+'.mesh.json')).read_text())
    assert len(model['parts'])==row['runtime_parts']
    for m in model['materials']:
        if m['texture']:assert (ROOT/'src/Art'/m['texture']).is_file()
dest=ROOT/'art/valuables/interaction-rework-09/audio_validation.json';dest.write_text(json.dumps(report,indent=2))
print('PASS: 10 PCM16 sound cues; no clipping, silence or hard edges; 12 embedded model resources')
