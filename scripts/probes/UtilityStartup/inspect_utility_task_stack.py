"""Inspect the real window port owner's Task stack at an idle MUI capture."""
import argparse,json
from pathlib import Path
from capture_memory import banks

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('capture',type=Path)
p.add_argument('--minimum',type=int,required=True)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args();j=json.loads(a.capture.read_text());memory=banks(a.capture.with_suffix('.fastram'))
def read(address,length):
    for base,data,_ in memory:
        if base<=address and address+length<=base+len(data):return data[address-base:address-base+length]
    raise ValueError(f'Unmapped public record: {address:x}')
def u32(address):return int.from_bytes(read(address,4),'big')
def text(address):
    result=bytearray()
    for i in range(128):
        b=read(address+i,1)[0]
        if not b:break
        result.append(b)
    return result.decode('latin1')
state=j.get('state');rows=[]
if state and state['Depth']==0 and state['ActiveOperations']==0:
    for obj in state['Objects']:
        if obj['Class']!='Window.mui' or not obj['NativeWindow']:continue
        window=obj['NativeWindow']
        # SDK Window.UserPort=86; MsgPort.mp_SigTask=16; Task bounds=58/62.
        port=u32(window+86);task=u32(port+16)
        lower=u32(task+58);upper=u32(task+62)
        rows.append(dict(window=hex(window),port=hex(port),task=hex(task),
                         taskName=text(u32(task+10)),lower=hex(lower),upper=hex(upper),
                         bytes=upper-lower if upper>=lower else 0,
                         meetsMinimum=upper>=lower and upper-lower>=a.minimum))
result=dict(protocol='public-window-owner-task-stack-v1',capture=str(a.capture),
            requiredMinimumBytes=a.minimum,owners=rows,
            verdict='PASS_ACTUAL_TASK_STACK_MINIMUM' if rows and all(r['meetsMinimum'] for r in rows) else 'UNAVAILABLE_OR_INSUFFICIENT_TASK_STACK')
a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
