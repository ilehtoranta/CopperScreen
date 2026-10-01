"""Independent original-guest AGA palette readback and scan-doubling checks.

Expectations derive from RGB tuples, DMA source rows and Commodore's parity
rules. Bitmap coordinates retain the engine's existing raster phase contract;
these diagnostic captures do not establish physical Alice/Lisa phase accuracy.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('bitmap', type=Path)
p.add_argument('--chipram', type=Path, required=True)
p.add_argument('--mode', choices=['palette-readback','scan16','scan32','scan32page','scan64'], required=True)
a = p.parse_args()
b, ram = a.bitmap.read_bytes(), a.chipram.read_bytes()
assert b[:2] == b'BM' and len(ram) == 2*1024*1024
offset = struct.unpack_from('<I', b, 10)[0]
width, height, planes, bits, compression = struct.unpack_from('<iiHHI', b, 18)
assert (width, abs(height), planes, bits, compression) == (1816,313,1,32,0)
assert offset >= 54 and offset+width*abs(height)*4 == len(b)
result = {'mode':a.mode, 'bitmapSha256':hashlib.sha256(b).hexdigest(), 'chipRamSha256':hashlib.sha256(ram).hexdigest()}
if a.mode == 'palette-readback':
    marker = b'AGAPALREAD1\0'
    start = ram.find(marker)
    assert start >= 0 and ram.find(marker,start+1) == -1, 'Missing/ambiguous native palette proof'
    assert start+len(marker)+1024 <= len(ram), 'Truncated palette proof'
    actual = struct.unpack_from('>512H',ram,start+len(marker))
    expected = []
    for bank in range(8):
        for low in (False,True):
            for register in range(32):
                index = bank*32+register
                components = (index, index*37%256, index*73%256)
                word = sum(((v & 15) if low else (v >> 4)) << shift for v,shift in zip(components,(8,4,0)))
                if index == 255: word = 0xDEF if low else 0x8ABC
                expected.append(word)
    assert list(actual) == expected, 'Native palette halves/bank/transparency/write inhibition differ'
    result['nativePaletteWords'] = len(actual)
else:
    count = {'scan16':16, 'scan32':32, 'scan32page':32, 'scan64':64}[a.mode]
    samples, colours = 0, set()
    for y in range(60,185):
        # BPL1MOD rewinds all eight planes; BPL2MOD advances one 40-byte OS row.
        # DIWSTRT's even parity therefore displays each source row twice.
        background = (((y-44)//2)*17%256) ^ 128
        step = 4 if y < 96 else 2 if y < 144 else 1
        for x in range(552,1664):
            values = []
            for channel in range(8):
                left = (132+40*(channel%4))*4+channel%4
                value = 0
                for base in ((64,112,160) if channel < 4 else (80,128,176)):
                    top = base+(channel%2 if channel != 7 else 0)
                    if top <= y < top+8 and left <= x < left+count*step:
                        source_row = (y-top)//2 if channel%2 == 0 else y-top
                        bit = (x-left)//step
                        if a.mode == 'scan32page': bit %= 16
                        value = (bit+channel+bit//16+source_row)%4
                values.append(value)
            index = background
            for even in (0,2,4,6):
                a_pixel, b_pixel = values[even:even+2]
                pixel = a_pixel+4*b_pixel if even == 6 else a_pixel or b_pixel
                if pixel:
                    index = 176+pixel if even == 6 else (32 if a_pixel else 176)+even//2*4+pixel
                    break
            expected = 0xFF000000 | index << 16 | (index*37%256) << 8 | (index*73%256)
            sy = y if height < 0 else abs(height)-1-y
            actual = struct.unpack_from('<I',b,offset+(sy*width+x)*4)[0]
            assert actual == expected, f'{a.mode} ({x},{y}): {actual:08X} != {expected:08X}'
            samples += 1; colours.add(expected)
    result.update(exactPixels=samples,uniqueColours=len(colours))
print(json.dumps(result,indent=2))
