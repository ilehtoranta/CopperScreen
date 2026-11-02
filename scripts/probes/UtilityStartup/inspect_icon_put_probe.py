"""Independent registered-resource actual-byte oracle for the native icon API."""
import argparse,json,struct
from pathlib import Path
from capture_memory import banks,exec_resource_nodes
from icon_update_fixture import icon

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('capture',type=Path);p.add_argument('--output',type=Path,required=True)
p.add_argument('--provider-only',action='store_true',help='Check retained provider/vector/call ownership; separate original file oracle required')
a=p.parse_args();memory=banks(a.capture)
def read(address,length):
    for base,data,_ in memory:
        if base<=address and address+length<=base+len(data):return data[address-base:address-base+length]
    raise ValueError(f'Unmapped native record {address:x}')
def u32(address):return int.from_bytes(read(address,4),'big')
nodes=exec_resource_nodes(memory)
def find(name,cookie,width):
    found=[n for n in nodes if read(n+8,1)==b'\x08' and read(n+16,4)==cookie and
           read(u32(n+10),width)==name.encode().ljust(width,b'\0')]
    return found[0] if len(found)==1 else None
before=find('MUI_IconBefore',b'IUB1',16)
after=find('MUI_IconPutProbe',b'IPB1',20)
writer=find('CopperStart.IconWriter',b'IWP1',23)
expected=bytearray(icon(True))
struct.pack_into('>II',expected,58,31,37);struct.pack_into('>I',expected,74,16384)
preflight=bool(before and u32(before+20)==1 and [u32(before+24),u32(before+28)]==[130,130] and
               read(before+128,130)==icon(True) and read(before+640,130)==icon(False))
effect=bool(after and u32(after+48)==1 and u32(after+32)==1 and u32(after+36)==0 and
            [u32(after+40),u32(after+44)]==[130,130] and
            read(after+128,130)==icon(True) and read(after+640,130)==expected)
binding=None
if writer:
    library=u32(writer+20);entry=u32(writer+36)
    binding=dict(address=hex(writer),library=hex(library),segment=u32(writer+32),entry=hex(entry),
                 calls=u32(writer+40),lastResult=u32(writer+44),installed=u32(writer+28),
                 vectorMatches=read(library-84,2)==bytes.fromhex('4ef9') and u32(library-82)==entry)
owner=bool(binding and binding['segment'] and binding['installed']==1 and binding['calls']==1 and
           binding['lastResult']==1 and binding['vectorMatches'])
result=dict(protocol='classic-icon-native-put-file-bytes-v1',capture=str(a.capture),
            preflight=preflight,exactSourceAndTargetBytes=effect,writer=binding,
            result=u32(after+32) if after else None,ioError=u32(after+36) if after else None,
            lengths=[u32(after+40),u32(after+44)] if after else None,
            verdict='PASS_NATIVE_ICON_PUT_DISK_OBJECT_BYTES' if preflight and effect and owner else 'FAIL_OR_UNAVAILABLE_NATIVE_ICON_WRITE',
            scope='Standalone genuine guest public icon API and DOS byte snapshots, not an original utility or GUI replay.')
if a.provider_only:
    result.update(protocol='classic-icon-guest-writer-binding-v1',
                  verdict='PASS_RETAINED_ICON_WRITER_USED_ONCE' if owner else 'FAIL_OR_UNAVAILABLE_WRITER_OWNERSHIP',
                  scope='Retained provider/vector/call ownership only; requires separate original GUI/cleanup and actual-file byte evidence.')
a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
