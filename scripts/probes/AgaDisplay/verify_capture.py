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
a = p.parse_args()
b = a.bitmap.read_bytes()
assert b[:2] == b'BM'
offset = struct.unpack_from('<I', b, 10)[0]
width, signed_height, planes, bits, compression = struct.unpack_from('<iiHHI', b, 18)
assert (width, abs(signed_height), planes, bits, compression) == (1816, 313, 1, 32, 0)
assert offset >= 54 and offset + width * abs(signed_height) * 4 == len(b)
colours = []
samples = 0
for pen in range(256):
    expected = 0xFF000000 | pen << 16 | (pen * 37 & 255) << 8 | (pen * 73 & 255)
    colours.append(expected)
    # Inset two guest pixels horizontally and vertically; avoid the screen's
    # first-row mouse pointer and boundaries when checking stable tile interiors.
    x0 = 516 + (pen % 16) * 80 + 8
    y0 = 44 + 16 + (pen // 16) * 15 + 2
    for y in range(y0, y0 + 11):
        row = y if signed_height < 0 else abs(signed_height) - 1 - y
        for x in range(x0, x0 + 64):
            actual = struct.unpack_from('<I', b, offset + (row * width + x) * 4)[0]
            assert actual == expected, f'Pen {pen}, pixel ({x},{y}): {actual:08X} != {expected:08X}'
            samples += 1
assert len(set(colours)) == 256
print(json.dumps({'bitmap': str(a.bitmap), 'sha256': hashlib.sha256(b).hexdigest(),
                  'width': width, 'height': abs(signed_height), 'uniqueColours': 256,
                  'exactInteriorPixels': samples}, indent=2))
