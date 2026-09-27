"""Create a disposable OFS partition fixture that allocates filesystem buffers in Fast RAM.

Input is a supplied, bootable Workbench 1.3 OFS partition image (no RDB offset).
Only the disposable copy's Startup-Sequence changes. Guest DOS performs allocation
and proof-file I/O; no licensed media or ROM data is distributed here.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
original = args.source.read_bytes()
assert original[:4] == b'DOS\0' and len(original) % 512 == 0
data = bytearray(original)
count = len(data) // 512

def get(offset):
    return struct.unpack_from('>I', data, offset)[0]

def put(offset, value):
    struct.pack_into('>I', data, offset, value & 0xFFFFFFFF)

def checksum(block):
    put(block * 512 + 20, 0)
    put(block * 512 + 20, -sum(struct.unpack_from('>128I', data, block * 512)))

headers = [i for i in range(2, count) if get(i * 512) == 2 and
           get(i * 512 + 508) == 0xFFFFFFFD and
           data[i * 512 + 433:i * 512 + 433 + data[i * 512 + 432]].lower() == b'startup-sequence']
assert len(headers) == 1
header = headers[0]
block = get(header * 512 + 308)
assert 0 < block < count and get(block * 512) == 8
# 1,200 OFS buffers exceed the available 512 KiB slow bank. Native Exec/DOS
# must use the newly registered CPU-only bank to satisfy this request.
payload = (b'FailAt 21\nAddBuffers SYS: 1200\nAvail\n'
           b'Echo >SYS:fast-proof.txt "Copper Fast RAM native OFS persistence"\n'
           b'Type SYS:fast-proof.txt\nEcho "FAST RAM NATIVE TEST FINISHED"\n')
assert len(payload) <= 488
data[block * 512 + 24:(block + 1) * 512] = payload + bytes(488 - len(payload))
put(block * 512 + 12, len(payload)); put(block * 512 + 16, 0); checksum(block)
for offset in range(24, 308, 4):
    put(header * 512 + offset, 0)
put(header * 512 + 8, 1); put(header * 512 + 16, block)
put(header * 512 + 324, len(payload)); put(header * 512 + 504, 0); checksum(header)
with args.output.open('xb') as output:
    output.write(data)
print(json.dumps({'source': str(args.source), 'sourceSha256': hashlib.sha256(original).hexdigest(),
                  'output': str(args.output), 'outputSha256': hashlib.sha256(data).hexdigest(),
                  'startup': payload.decode()}, indent=2))
