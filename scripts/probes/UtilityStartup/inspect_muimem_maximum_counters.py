"""Compare MUImem's visible Maximum row with the guest's actual Exec MemList.

Only stable region extents are checked. Free/used/largest values can change
between the application's queries and a capture and need separate sampling.
"""
import argparse, json
from pathlib import Path
from capture_memory import banks

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
memory = banks(a.capture.with_suffix('.fastram'))
def mapped(address, length):
    return any(base <= address and address + length <= base + len(data) for base, data, _ in memory)
def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data):
            return data[address-base:address-base+length]
    raise ValueError('Unmapped guest record')
def u32(address): return int.from_bytes(read(address, 4), 'big')
def u16(address): return int.from_bytes(read(address, 2), 'big')
state = json.loads(a.capture.read_text())['state']
windows = [o['Object'] for o in state['Objects'] if o['Class'] == 'Window.mui' and o['NativeWindow']]
visible = [o for o in state['Objects'] if o['Visible'] and o['ContainingWindow'] in windows and o['Width'] > 0]
labels = [o for o in visible if o['Class'] == 'Text.mui' and o['Text'].endswith('Maximum')]
execbase = u32(4)
header = u32(execbase + 322)  # ExecBase.MemList.lh_Head, classic 68000 ABI.
regions = []
seen = set()
while header and mapped(header, 32) and u32(header):
    if header in seen or len(seen) >= 256: raise ValueError('Invalid Exec MemList')
    seen.add(header)
    lower, upper = u32(header+20), u32(header+24)
    if lower >= upper: raise ValueError('Invalid MemHeader extent')
    # Verified against the native Kickstart 3.1 AvailMem routine: MEMF_TOTAL
    # sums mh_Upper - mh_Lower + sizeof(struct MemHeader), including 32 bytes.
    regions.append(dict(address=hex(header), attributes=u16(header+14), lower=hex(lower), upper=hex(upper), bytes=upper-lower+32))
    header = u32(header)
expected = [sum(r['bytes'] for r in regions if r['attributes'] & 2),
            sum(r['bytes'] for r in regions if r['attributes'] & 4),
            sum(r['bytes'] for r in regions)]
values = sorted([o for o in visible if len(labels) == 1 and o['Class'] == 'Text.mui' and
                 o['Top'] == labels[0]['Top'] and o['Text'].isascii() and o['Text'].isdigit()], key=lambda o: o['Left'])
actual = [int(o['Text']) for o in values]
result = dict(protocol='muimem-exec-maximum-counters-v2', capture=str(a.capture),
              scope='MAXIMUM_ROW_REGION_EXTENTS_ONLY', regions=regions,
              columns=['Chip', 'Fast', 'Total'], expected=expected, actual=actual,
              verdict='PASS_MAXIMUM_COUNTERS' if len(regions) > 0 and len(labels) == 1 and actual == expected else 'FAIL_OR_UNAVAILABLE_MAXIMUM_COUNTERS')
a.output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
