"""Combine synchronous glTF node-animation tracks into one playable clip, without resampling."""
import json,struct
from pathlib import Path
def merge_loop(path,name='Liquid_Timer_Flow'):
    path=Path(path);raw=path.read_bytes();magic,version,length=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and length==len(raw)
    chunks=[];offset=12
    while offset<len(raw):
        size,tag=struct.unpack_from('<II',raw,offset);offset+=8;chunks.append((tag,raw[offset:offset+size]));offset+=size
    assert chunks[0][0]==0x4e4f534a;g=json.loads(chunks[0][1]);animations=g.get('animations',[]);assert animations
    combined={'name':name,'samplers':[],'channels':[]}
    for animation in animations:
        base=len(combined['samplers']);combined['samplers'].extend(animation['samplers'])
        for source in animation['channels']:
            channel=dict(source);channel['sampler']+=base;combined['channels'].append(channel)
    g['animations']=[combined]
    # Scene-mode export starts at frame 1 / fps. Shift input keys to t=0, so the
    # six-second loop has no leading idle frame in importers that play from zero.
    assert chunks[1][0]==0x004e4942;binary=bytearray(chunks[1][1]);inputs={s['input'] for s in combined['samplers']};values={}
    for index in inputs:
        a=g['accessors'][index];assert a['componentType']==5126 and a['type']=='SCALAR'
        v=g['bufferViews'][a['bufferView']];offset=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',4)
        values[index]=(offset,stride,[struct.unpack_from('<f',binary,offset+i*stride)[0] for i in range(a['count'])])
    first=min(v[2][0] for v in values.values())
    for index,(offset,stride,times) in values.items():
        for i,t in enumerate(times):struct.pack_into('<f',binary,offset+i*stride,t-first)
        g['accessors'][index]['min']=[times[0]-first];g['accessors'][index]['max']=[times[-1]-first]
    chunks[1]=(chunks[1][0],bytes(binary))
    data=json.dumps(g,separators=(',',':'),ensure_ascii=False).encode('utf-8');data+=b' '*((-len(data))%4)
    chunks[0]=(chunks[0][0],data);body=b''.join(struct.pack('<II',len(data),tag)+data for tag,data in chunks)
    path.write_bytes(struct.pack('<4sII',b'glTF',2,len(body)+12)+body)
