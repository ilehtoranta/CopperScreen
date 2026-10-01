"""Check every RGB24 tile interior in the original native AGA probe's 32-bit BMP.

This reader is independent of the emulator and uses the probe's PAL window and
tile coordinates. It validates all 256 colour values, not an emulator-generated
expected fingerprint. Use the separate FFS proof reader for native disk I/O.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('bitmap', type=Path)
p.add_argument('--mode', choices=['rgb24', 'ham6', 'ham8', 'programmed-dual'], default='rgb24')
a = p.parse_args()
b = a.bitmap.read_bytes()
assert b[:2] == b'BM'
offset = struct.unpack_from('<I', b, 10)[0]
width, signed_height, planes, bits, compression = struct.unpack_from('<iiHHI', b, 18)
assert (width, abs(signed_height), planes, bits, compression) == (1816, 313, 1, 32, 0)
assert offset >= 54 and offset + width * abs(signed_height) * 4 == len(b)
colours = []
samples = 0
def palette(index):
    return 0xFF000000 | index << 16 | (index * 37 & 255) << 8 | (index * 73 & 255)

held = palette(0)
for pen in range(512 if a.mode == 'programmed-dual' else 256):
    tile = pen & 255
    if a.mode == 'programmed-dual':
        pf1, pf2 = tile // 16, tile % 16
        select2 = pf2 != 0 and (pen >= 256 or pf1 == 0)
        expected = palette((pf2 + 32 if select2 else pf1) ^ 128)
        top, tile_height = (136 if pen >= 256 else 16), 7
    elif a.mode in ('ham6', 'ham8'):
        if tile % 16 == 0: held = palette(0)
        control = tile & 3 if a.mode == 'ham8' else tile >> 4 & 3
        index = tile >> 2 if a.mode == 'ham8' else tile & 15
        if control == 0: held = palette(index)
        else:
            shift = {1: 0, 2: 16, 3: 8}[control]
            mask = 0xFC if a.mode == 'ham8' else 0xFF
            component = tile & 0xFC if a.mode == 'ham8' else (tile & 15)*17
            held = (held & ~(mask << shift)) | component << shift
        expected = held
        top, tile_height = 16, 15
    else:
        expected = palette(tile)
        top, tile_height = 16, 15
    colours.append(expected)
    # Inset two guest pixels horizontally and vertically; avoid the screen's
    # first-row mouse pointer and boundaries when checking stable tile interiors.
    x0 = 516 + (tile % 16) * 80 + 8
    y0 = 44 + top + (tile // 16) * tile_height + 2
    for y in range(y0, y0 + tile_height - 4):
        row = y if signed_height < 0 else abs(signed_height) - 1 - y
        for x in range(x0, x0 + 64):
            actual = struct.unpack_from('<I', b, offset + (row * width + x) * 4)[0]
            assert actual == expected, f'Pen {pen}, pixel ({x},{y}): {actual:08X} != {expected:08X}'
            samples += 1
if a.mode == 'rgb24': assert len(set(colours)) == 256
print(json.dumps({'bitmap': str(a.bitmap), 'sha256': hashlib.sha256(b).hexdigest(),
                  'width': width, 'height': abs(signed_height), 'uniqueColours': len(set(colours)),
                  'exactInteriorPixels': samples}, indent=2))
