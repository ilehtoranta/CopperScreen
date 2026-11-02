"""Check actual guest DOS before/after icon files, independently of GUI success."""
import argparse, hashlib, json, struct
from pathlib import Path
from capture_memory import banks, exec_resource_nodes
from icon_update_fixture import icon

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args(); memory = banks(a.capture)
def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data): return data[address-base:address-base+length]
    return bytes(length)
def u32(address): return int.from_bytes(read(address,4),'big')

def reader_state():
    nodes=[node for node in exec_resource_nodes(memory)
           if read(node+16,4)==bytes.fromhex('49554131') and
           read(node+8,1)==b'\x08' and
           read(u32(node+10),16)==b'MUI_IconAfter\0\0\0']
    if len(nodes)!=1: return None
    node=nodes[0]
    return dict(address=hex(node),complete=u32(node+20),
                taskSeen=u32(node+40),taskGone=u32(node+44),
                remainingDelayOnePolls=u32(node+48),timedOut=u32(node+52))
def snapshot(before):
    cookie = bytes.fromhex('49554231' if before else '49554131')
    name = ('MUI_IconBefore' if before else 'MUI_IconAfter').encode().ljust(16,b'\0')
    nodes=[]
    for node in exec_resource_nodes(memory):
        if read(node+16,4)==cookie and read(node+8,1)==b'\x08' and read(u32(node+10),16)==name:
            nodes.append(node)
    if len(nodes)!=1 or u32(nodes[0]+20)!=1: return None
    node=nodes[0]; lengths=[u32(node+24),u32(node+28)]
    if not before and (u32(node+40)!=1 or u32(node+44)!=1): return None
    if not all(0<n<=512 for n in lengths) or not u32(node+32) or not u32(node+36): return None
    return [read(node+offset,n) for offset,n in zip([128,640],lengths)]
def parse(data):
    if len(data)<98 or data[:4]!=bytes.fromhex('e3100001'): raise ValueError('Invalid icon header')
    long=lambda off: int.from_bytes(data[off:off+4],'big')
    if not long(22) or long(66): raise ValueError('Fixture requires one non-drawer normal image')
    width,height,depth=struct.unpack_from('>hhh',data,82)
    count=((width+15)//16)*2*height*depth
    if width!=16 or height!=16 or depth!=1 or len(data)<98+count: raise ValueError('Unexpected fixture image')
    return dict(type=data[48],x=long(58),y=long(62),stack=long(74),width=width,height=height,depth=depth,
                pixelsSha256=hashlib.sha256(data[98:98+count]).hexdigest())
before=snapshot(True); after=snapshot(False)
preflight=before is not None and before==[icon(True),icon(False)]
result=dict(protocol='icon-update-native-file-write-v2',verifierProtocol='exec-registered-resource-bytes-v1',capture=str(a.capture),preflight=preflight,
            beforeAvailable=before is not None,afterAvailable=after is not None,readerState=reader_state(),
            verdict='UNAVAILABLE_OR_FAILED_FILE_EVIDENCE')
if preflight and after is not None:
    try:
        source=parse(after[0]); target=parse(after[1]); expected=parse(icon(True))
        expected.update(x=31,y=37,stack=16384)
        passed=after[0]==icon(True) and target==expected
        result.update(sourceUnchanged=after[0]==icon(True),source=source,target=target,expectedTarget=expected,
                      targetChanged=after[1]!=icon(False),verdict='PASS_ACTUAL_ICON_IMAGE_UPDATE_AND_DEFAULT_METADATA_PRESERVATION' if passed else 'FAIL_ACTUAL_ICON_UPDATE')
    except ValueError as e: result['error']=str(e)
a.output.write_text(json.dumps(result,indent=2)+'\n'); print(json.dumps(result))
