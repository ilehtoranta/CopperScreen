"""Read original MUIJoin globals after Use; GUI cleanup alone is insufficient."""
import argparse
import hashlib
import json
from pathlib import Path
from capture_memory import banks

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--original', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
original = a.original.read_bytes()
sha = hashlib.sha256(original).hexdigest()
if sha != '783e4eb88aea56af5abf3415e9afbde3c20791838d56eb9c750f1d0d354c011d':
    raise ValueError('Not the pinned original MUIJoin executable')
anchor = bytes.fromhex('200f90bc00000020b0ac04b065002196594f48e721367e01')
offset = original.find(anchor)
if offset != 0x102c or original.find(anchor, offset+1) >= 0:
    raise ValueError('Original MUIJoin anchor changed')
memory = banks(a.capture)

def read(address, length):
    for base, data, _ in memory:
        if base <= address and address+length <= base+len(data):
            return data[address-base:address-base+length]
    raise ValueError(f'Unmapped preference record {address:x}')

def u32(address): return int.from_bytes(read(address, 4), 'big')

observations = []
for base, data, _ in memory:
    start = 0
    while (found := data.find(anchor, start)) >= 0:
        start = found+2
        program = base+found-offset
        # Original LEA absolute,A4 is relocated by the native DOS loader.
        if read(program+0x38, 2) != bytes.fromhex('49f9'):
            continue
        globals_address = u32(program+0x3a)
        values = dict(splitSuffix=read(globals_address+0x487, 5).hex(),
                      joinSuffix=read(globals_address+0x48c, 5).hex(),
                      length=u32(globals_address+0x496),
                      buffer=u32(globals_address+0x49a),
                      maxMemory=u32(globals_address+0x49e))
        observations.append(dict(programBase=hex(program), dataBase=hex(globals_address),
                                 values=values))
expected = dict(splitSuffix='2e6a000000', joinSuffix='2e6f757400',
                length=4096, buffer=4096, maxMemory=0)
passed = len(observations) == 1 and observations[0]['values'] == expected
result = dict(protocol='muijoin-original-preferences-defaults-v1',
              capture=str(a.capture), originalSha256=sha, expected=expected,
              observations=observations,
              verdict='PASS_PREFERENCES_PRESERVED' if passed else
              'FAIL_PREFERENCES_CHANGED' if len(observations) == 1 else
              'UNAVAILABLE_ORIGINAL_PROGRAM',
              scope='Read-only original executable globals; use an after-Use capture, plus independent GUI/cleanup evidence.')
a.output.write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(result))
