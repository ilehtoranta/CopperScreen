"""Validate the native AvailMem probe against the captured public MemHeaders."""
import argparse, json
from pathlib import Path
from capture_memory import banks
from exec_availmem_probe import FLAGS

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path, help='Fast RAM capture')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args(); memory = banks(a.capture)
def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data):
            return data[address-base:address-base+length]
    return bytes(length)
def u32(address): return int.from_bytes(read(address, 4), 'big')
def u16(address): return int.from_bytes(read(address, 2), 'big')
nodes = []
for base, data, _ in memory:
    start = 0
    while (i := data.find(bytes.fromhex('4d454d31'), start)) >= 0:
        start = i + 4; node = base + i - 16
        if read(node+8, 1) == b'\x08' and read(u32(node+10), 14) == b'AvailMemProbe\0': nodes.append(node)
complete = len(nodes) == 1 and u32(nodes[0]+20) == 1
header = u32(u32(4)+322); regions = []; seen = set()
while header and u32(header):
    if header in seen or len(seen) >= 256: raise ValueError('Invalid Exec MemList')
    seen.add(header); lower, upper = u32(header+20), u32(header+24)
    if lower >= upper: raise ValueError('Invalid MemHeader extent')
    regions.append(dict(address=hex(header), attributes=u16(header+14), total=upper-lower+32))
    header = u32(header)
expected = {f: sum(r['total'] for r in regions if (r['attributes'] & (f & 0xFFFF)) == (f & 0xFFFF)) for f in FLAGS[3:6]}
values = {f: u32(nodes[0]+24+i*4) for i, f in enumerate(FLAGS)} if complete else {}
allocated = u32(nodes[0]+56) if complete else 0
after = u32(nodes[0]+60) if complete else None
passed = complete and allocated != 0 and len(regions) > 0 and all(values[f] == n for f, n in expected.items()) and after == values[0x80000] and values[0x20000] == values[0xA0000]
result = dict(protocol='exec-availmem-native-probe-v1', capture=str(a.capture), complete=complete,
              regions=regions, values={hex(k): v for k, v in values.items()}, expectedTotals={hex(k): v for k, v in expected.items()},
              transientAllocation=hex(allocated), totalWhileAllocated=after,
              verdict='PASS_TOTAL_FILTERING_ALLOCATION_INVARIANCE_AND_LARGEST_PRECEDENCE' if passed else 'FAIL_OR_UNAVAILABLE_AVAILMEM_PROBE')
a.output.write_text(json.dumps(result, indent=2) + '\n'); print(json.dumps(result))
