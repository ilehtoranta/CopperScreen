"""Verify actual guest DOS Split/rejoin output bytes independently of the GUI."""
import argparse
import hashlib
import json
from pathlib import Path
from capture_memory import banks, exec_resource_nodes
from mui_join_fixture import INPUT, EXPECTED, PATHS, STRIDE

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
memory = banks(a.capture)

def read(address, length):
    for base, data, _ in memory:
        if base <= address and address+length <= base+len(data):
            return data[address-base:address-base+length]
    return bytes(length)

def u32(address): return int.from_bytes(read(address,4),'big')

def snapshot(before, version):
    magic = bytes.fromhex(('4a57423' if before else '4a57413')+str(version))
    name = (('MUI_JoinBefore' if before else 'MUI_JoinAfter')+('2' if version==2 else '')).encode().ljust(16,b'\0')
    nodes = []
    for node in exec_resource_nodes(memory):
        if read(node+16,4)==magic and read(node+8,1)==b'\x08' and read(u32(node+10),16)==name:
            nodes.append(node)
    if len(nodes)!=1 or u32(nodes[0]+20)!=1: return None
    node = nodes[0]
    result = []
    for i in range(1 if before else len(EXPECTED)):
        length = u32(node+24+i*4)
        if length!=len(EXPECTED[i]) or not u32(node+48+i*4): return None
        result.append(read(node+128+i*STRIDE,length))
    return result

available = [(v, data) for v in (1,2) if (data := snapshot(True,v)) is not None]
version, before = available[0] if len(available)==1 else (None,None)
after = snapshot(False,version) if version is not None else None
paths = PATHS if version==2 else ['RAM:muifix']+['RAM:muifix.ja'+c for c in 'abcd']+['RAM:muifix.out']
preflight = before == [INPUT]
result = dict(protocol=f'muijoin-native-split-rejoin-bytes-v{version or "unknown"}',verifierProtocol='exec-registered-resource-bytes-v1',capture=str(a.capture),
              preflight=preflight,afterAvailable=after is not None,
              verdict='UNAVAILABLE_OR_FAILED_FILE_EVIDENCE')
if preflight and after is not None:
    matches = [actual==expected for actual,expected in zip(after,EXPECTED)]
    result.update(files=[dict(path=path,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),matchesExpected=match)
                         for path,data,match in zip(paths,after,matches)],
                  sourceUnchanged=after[0]==INPUT,
                  splitConcatenationMatches=b''.join(after[1:5])==INPUT,
                  joinedMatches=after[5]==INPUT,
                  verdict='PASS_ACTUAL_SPLIT_AND_REJOIN_BYTES' if all(matches) else 'FAIL_ACTUAL_SPLIT_OR_REJOIN_BYTES')
a.output.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result))
