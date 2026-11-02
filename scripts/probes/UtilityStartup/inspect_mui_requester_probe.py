"""Verify guest MUI_RequestA return/disposal alongside native hardware UI proof."""
import argparse,json
from pathlib import Path
from capture_memory import banks,exec_resource_nodes

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('capture',type=Path);p.add_argument('--output',type=Path,required=True)
p.add_argument('--require-reference-window',action='store_true')
a=p.parse_args();memory=banks(a.capture)
def read(address,length):
    for base,data,_ in memory:
        if base<=address and address+length<=base+len(data):return data[address-base:address-base+length]
    raise ValueError(f'Unmapped probe record {address:x}')
def u32(address):return int.from_bytes(read(address,4),'big')
nodes=[n for n in exec_resource_nodes(memory) if read(n+8,1)==b'\x08' and
       read(n+16,4)==b'MUP1' and read(u32(n+10),20)==b'MUI_RequestProbe'.ljust(20,b'\0')]
rows=[dict(address=hex(n),library=hex(u32(n+20)),application=hex(u32(n+24)),
           stage=u32(n+28),requestResult=u32(n+32),complete=u32(n+36),
           referenceObject=hex(u32(n+44)),referenceNativeWindow=hex(u32(n+52)),referenceGetSucceeded=u32(n+56)) for n in nodes]
passed=len(rows)==1 and rows[0]['stage']==3 and rows[0]['complete']==1 and u32(nodes[0]+20)!=0 and u32(nodes[0]+24)!=0
if a.require_reference_window:passed=passed and u32(nodes[0]+44)!=0 and u32(nodes[0]+52)!=0 and u32(nodes[0]+56)!=0
result=dict(protocol='mui-requester-native-return-resource-v1',capture=str(a.capture),observations=rows,requireReferenceWindow=a.require_reference_window,
            verdict='PASS_REQUEST_CALL_RETURNED_AND_APPLICATION_DISPOSED' if passed else 'UNAVAILABLE_OR_FAILED_REQUEST_COMPLETION',
            scope='Requires separate native dialog-text/hardware OK/cleanup proof; a rejected request can also return zero.')
a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
