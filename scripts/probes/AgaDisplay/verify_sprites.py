"""Independent pixel and native read-clear proof checks for original AGA sprite fixtures.

Sprite shapes/banks come from specified source values, not emulator fingerprints.
The bitmap phase retains the engine's +1 lores raster contract; no physical
Alice/Lisa sub-CCK phase accuracy is claimed. Guest collision words are sampled
by ordinary CPU reads of CLXDAT, retained in Chip RAM after native DOS boot.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('bitmap', type=Path)
p.add_argument('--mode', choices=['sprites16', 'sprites32', 'sprites32page', 'sprites64'], required=True)
p.add_argument('--chipram', type=Path, required=True)
a = p.parse_args()
b = a.bitmap.read_bytes()
assert b[:2] == b'BM'
offset = struct.unpack_from('<I', b, 10)[0]
width, signed_height, planes, bits, compression = struct.unpack_from('<iiHHI', b, 18)
assert (width, abs(signed_height), planes, bits, compression) == (1816, 313, 1, 32, 0)
assert offset >= 54 and offset + width * abs(signed_height) * 4 == len(b)
def palette(index):
    return 0xFF000000 | index << 16 | (index*37 % 256) << 8 | (index*73 % 256)

count = {'sprites16': 16, 'sprites32': 32, 'sprites32page': 32, 'sprites64': 64}[a.mode]
samples = 0
colours = set()
for row, y0 in enumerate((64, 80, 112, 128, 160, 176)):
    step = (4, 2, 1)[row // 2]
    channels = range(4) if row % 2 == 0 else range(4, 8)
    # Test the entire row interval, including transparent pattern bits, fine
    # positions, edges and the gaps between sprites. The last pair is attached.
    for y in range(y0, y0+8):
        storage_y = y if signed_height < 0 else abs(signed_height)-1-y
        for x in range(552, 1664):
            values = {}
            for channel in channels:
                left = (140+70*(channel % 4 if channel != 7 else 2))*4+channel % 4
                sample = (x-left)//step
                if x < left or sample >= count: value = 0
                else:
                    pattern_sample = sample % 16 if a.mode == 'sprites32page' else sample
                    value = (pattern_sample+channel+pattern_sample//16) % 4
                values[channel] = value
            index = 64  # Raw planes 7/8 are C0, playfield-only XOR is 80.
            for even in (0, 2) if row % 2 == 0 else (4, 6):
                first, second = values[even], values[even+1]
                if even == 6:
                    pixel = first+4*second
                    if pixel: index = 176+pixel; break
                elif first or second:
                    index = (32 if first else 176)+(even//2)*4+(first or second)
                    break
            expected = palette(index)
            actual = struct.unpack_from('<I', b, offset+(storage_y*width+x)*4)[0]
            assert actual == expected, f'{a.mode} row {row}, pixel ({x},{y}): {actual:08X} != {expected:08X}'
            colours.add(expected); samples += 1

ram = a.chipram.read_bytes()
assert len(ram) == 2*1024*1024
marker = b'AGASPRCOLv1\0'
location = ram.find(marker)
assert location >= 0 and ram.find(marker, location+1) == -1, 'Missing/ambiguous native collision proof'
collisions = [v & 0x7FFF for v in struct.unpack_from('>6H', ram, location+len(marker))]
assert collisions == [0x67, 0x199, 0, 0, 0x60, 0x180], f'Native CLXDAT proof: {collisions}'
print(json.dumps({'bitmap': str(a.bitmap), 'sha256': hashlib.sha256(b).hexdigest(),
                  'chipRamSha256': hashlib.sha256(ram).hexdigest(), 'mode': a.mode,
                  'exactPixels': samples, 'uniqueColours': len(colours),
                  'nativeCollisionProof': [f'{v:04X}' for v in collisions]}, indent=2))
