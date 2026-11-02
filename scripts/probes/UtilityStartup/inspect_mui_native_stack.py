"""Symbolize current PC and candidate stack code addresses in completed captures.

Stack words are candidates, not an unwound or validated call stack.
"""
import argparse
import json
from pathlib import Path
from capture_memory import banks

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--library', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
p.add_argument('--stack', choices=['active', 'user'], default='active')
a = p.parse_args()
j = json.loads(a.capture.read_text())
meta = json.loads(Path(str(a.library)+'.json').read_text())
symbols = json.loads(Path(str(a.library)+'.symbols.json').read_text())
memory = banks(a.capture.with_suffix('.fastram'))

def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data):
            return data[address-base:address-base+length]
    raise ValueError(f'Unmapped capture address {address:#x}')

def u32(address):
    return int.from_bytes(read(address, 4), 'big')

library = j['state']['Library']
name = u32(library+10)
if read(name, 18).split(b'\0')[0] != meta['Name'].encode():
    raise ValueError('Library identity differs from receipt')
if read(library-30, 2) != bytes.fromhex('4ef9'):
    raise ValueError('Unsupported native vector encoding')
# Init may use a compiler-owned name literal rather than the packager string.
# The emitted NewObjectA gate is the reliable relocation anchor.
image = u32(library-28)-meta['Exports']['copperos.mui.library.new-object-a']
code = image+meta['Layout']['CompiledCode']

def symbolize(address):
    offset = address-code
    matches = [s for s in symbols if s['Address'] <= offset < s['Address']+s['Size']]
    return dict(address=hex(address), offset=hex(offset), symbols=matches) if matches else None

pc = int(j['pc'], 16)
sp = int(j['userStackPointer'] if a.stack == 'user' else j['address'][7], 16)
result = dict(protocol='mui-native-symbol-candidates-v1', capture=str(a.capture),
              librarySha256=meta['Sha256'], stackSelection=a.stack, codeBase=hex(code), pc=symbolize(pc),
              candidateStackWords=[dict(stackAddress=hex(sp+i), **s)
                                   for i in range(0, 1024, 2) if (s := symbolize(u32(sp+i)))])
a.output.write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(result))
