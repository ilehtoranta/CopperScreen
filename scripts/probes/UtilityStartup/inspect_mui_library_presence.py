"""Read copied guest OpenLibrary observations from the native presence probe."""
import argparse, json
from pathlib import Path
from capture_memory import banks
from mui_library_presence_probe import LIBRARIES

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
memory = banks(a.capture)
def read(address, size):
    for base, data, _ in memory:
        if base <= address and address + size <= base + len(data):
            return data[address-base:address-base+size]
    return bytes(size)
def u32(address): return int.from_bytes(read(address, 4), 'big')
nodes = []
for base, data, _ in memory:
    start = 0
    while (i := data.find(bytes.fromhex('4c504231'), start)) >= 0:
        start = i + 4
        node = base + i - 16
        if read(node + 8, 1) == b'\x08' and read(u32(node + 10), 16) == b'MuiLibraryProbe\0':
            nodes.append(node)
complete = len(nodes) == 1 and u32(nodes[0] + 20) == 1
result = dict(protocol='mui-library-presence-v1', capture=str(a.capture),
              verdict='COMPLETE' if complete else 'UNAVAILABLE_PROBE_RESULT',
              libraries=[dict(name=name, opened=u32(nodes[0]+24+i*8) != 0,
                  version=u32(nodes[0]+28+i*8) >> 16, revision=u32(nodes[0]+28+i*8) & 65535)
                  for i, name in enumerate(LIBRARIES)] if complete else [])
a.output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
