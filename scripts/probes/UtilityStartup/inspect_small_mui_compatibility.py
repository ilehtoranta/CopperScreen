"""Read-only compatibility evidence from a completed pinned-MUI capture."""
import argparse,collections,json
from pathlib import Path
from capture_memory import banks
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('capture',type=Path); p.add_argument('--output',type=Path,required=True)
a=p.parse_args(); j=json.loads(a.capture.read_text()); bs=banks(a.capture.with_suffix('.fastram'))
def read(address,length):
 for base,data,_ in bs:
  if base<=address and address+length<=base+len(data):return data[address-base:address-base+length]
 return bytes(length)
def u(address):return int.from_bytes(read(address,4),'big')
s=j['state']; registry=u(s['Root']); records={}; rec=u(registry+12)
for _ in range(1024):
 if not rec or rec in records.values():break
 records[u(rec+4)]=rec;rec=u(rec)
def attrs(obj):
 node=u(records[obj]+12);out={}
 for _ in range(4096):
  if not node:break
  out[u(node+4)]=u(node+8);node=u(node)
 return out
objects=[]
for obj in s['Objects']:
 data=dict(obj);r=records[obj['Object']];data['parentRecord']=u(r+28)
 if obj['Class']=='Window.mui':data['rootObject']=attrs(obj['Object']).get(0x8042cba5)
 objects.append(data)
sp=j.get('LastExceptionA7',0)
result=dict(capture=str(a.capture),fields=j['CompletedFrames'],cpu={k:v for k,v in j.items() if k.startswith(('FirstException','LastException'))},classCounts=dict(collections.Counter(o['Class'] for o in objects)),objects=objects,exceptionStack=[dict(address=sp+i,value=u(sp+i)) for i in range(0,128,4)] if sp else [])
a.output.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(dict(output=str(a.output),windows=[o for o in objects if o['Class']=='Window.mui'])))
