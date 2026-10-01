"""Positive/negative controls for the original AGA guest proof readers.

These generated files test reader discrimination, not native ROM compatibility.
No ROM, OS, game media or emulator-generated golden output is needed.
"""
import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

CHECKER = Path(__file__).resolve().parents[1] / 'probes/AgaDisplay/verify_readback_scan.py'


def palette(index):
    return bytes((index*73%256,index*37%256,index,255))


def scan_raster(mode, doubled_planes=True, doubled_sprites=True, masked_position=True):
    width, height = 1816,313
    data = bytearray(width*height*4)
    length = {'scan16':16,'scan32':32,'scan32page':32,'scan64':64}[mode]
    for y in range(60,185):
        row = (y-44)//2 if doubled_planes else y-44
        start = (y*width+552)*4
        data[start:start+1112*4] = palette((row*17%256)^128)*1112
        step = (4,2,1)[0 if y < 96 else 1 if y < 144 else 2]
        patterns = [dict() for _ in range(8)]
        for sprite in range(8):
            for base in ((64,112,160) if sprite < 4 else (80,128,176)):
                top = base+(sprite%2 if sprite != 7 else 0)
                if not top <= y < top+8: continue
                source_row = (y-top)//2 if sprite%2 == 0 and doubled_sprites else y-top
                left = (132+40*(sprite%4))*4+sprite%4
                if not masked_position and sprite%2 == 0: left += 1024
                for bit in range(length):
                    index = bit%16 if mode == 'scan32page' else bit
                    pixel = (index+sprite+index//16+source_row)%4
                    for sub in range(step): patterns[sprite][left+bit*step+sub] = pixel
        # Paint low-priority pairs first; transparent pixels preserve earlier
        # data. Pair 6 is attached; the others select independent bank addresses.
        for even in (6,4,2,0):
            for x in sorted(patterns[even].keys() | patterns[even+1].keys()):
                if not 552 <= x < 1664: continue
                first,second = patterns[even].get(x,0),patterns[even+1].get(x,0)
                pixel = first+4*second if even == 6 else first or second
                if pixel:
                    index = 176+pixel if even == 6 else (32 if first else 176)+even//2*4+pixel
                    offset = (y*width+x)*4; data[offset:offset+4] = palette(index)
    header = b'BM'+struct.pack('<IHHI',54+len(data),0,0,54)
    header += struct.pack('<IiiHHIIIIII',40,width,-height,1,32,0,len(data),0,0,0,0)
    return bytearray(header+data)


def palette_ram():
    ram = bytearray(2097152); values = []
    for bank in range(8):
        for low in (False,True):
            for index in range(bank*32,(bank+1)*32):
                rgb = (index,index*37%256,index*73%256)
                n = [v%16 if low else v//16 for v in rgb]
                value = n[0]*256+n[1]*16+n[2]
                if index == 255: value = 0xDEF if low else 0x8ABC
                values.append(value)
    proof = b'AGAPALREAD1\0'+struct.pack('>512H',*values)
    ram[4096:4096+len(proof)] = proof
    return ram


class AgaReadbackScanControls(unittest.TestCase):
    def check(self, bitmap, ram, mode, valid):
        with tempfile.TemporaryDirectory() as folder:
            image, memory = Path(folder)/'test.bmp',Path(folder)/'test.chipram'
            image.write_bytes(bitmap); memory.write_bytes(ram)
            r = subprocess.run([sys.executable,str(CHECKER),str(image),'--chipram',str(memory),'--mode',mode],capture_output=True,text=True)
            if valid:
                self.assertEqual(0,r.returncode,r.stderr)
                proof = json.loads(r.stdout)
                self.assertEqual(512,proof['nativePaletteWords']) if mode == 'palette-readback' else self.assertEqual(139000,proof['exactPixels'])
            else: self.assertNotEqual(0,r.returncode)

    def test_all_scan_fetch_modes(self):
        for mode in ('scan16','scan32','scan32page','scan64'):
            with self.subTest(mode=mode): self.check(scan_raster(mode),bytes(2097152),mode,True)

    def test_bitplane_sprite_and_horizontal_scan_rules_are_independently_required(self):
        for feature in ('doubled_planes','doubled_sprites','masked_position'):
            with self.subTest(feature=feature): self.check(scan_raster('scan64',**{feature:False}),bytes(2097152),'scan64',False)

    def test_corrupt_scan_pixel_and_truncated_bitmap(self):
        data = scan_raster('scan64'); data[54+(64*1816+552)*4] ^= 1
        self.check(data,bytes(2097152),'scan64',False)
        self.check(scan_raster('scan64')[:-4],bytes(2097152),'scan64',False)

    def test_native_palette_proof(self):
        self.check(scan_raster('scan16'),palette_ram(),'palette-readback',True)

    def test_palette_bank_half_transparency_and_write_inhibition_corruption(self):
        for word in (0,32,64,479,511):
            ram = palette_ram(); ram[4096+12+word*2] ^= 1
            with self.subTest(word=word): self.check(scan_raster('scan16'),ram,'palette-readback',False)

    def test_missing_ambiguous_or_short_native_palette_proof(self):
        for kind in ('missing','duplicate','short'):
            ram = palette_ram()
            if kind == 'missing': ram[4096] = 0
            elif kind == 'duplicate': ram[8192:8192+12] = b'AGAPALREAD1\0'
            else: ram = ram[:-2]
            with self.subTest(kind=kind): self.check(scan_raster('scan16'),ram,'palette-readback',False)


if __name__ == '__main__': unittest.main()
