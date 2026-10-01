"""Synthetic positive/negative controls for the independent AGA pixel checker.

No native ROM/media: generate the specified probe layout from RGB tuples and
test rejection of lost HAM low bits, incorrect priority and corrupted captures.
"""
import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


CHECKER = Path(__file__).resolve().parents[1] / 'probes/AgaDisplay/verify_capture.py'
SPRITE_CHECKER = CHECKER.with_name('verify_sprites.py')


def raster(mode):
    width, height = 1816, 313
    payload = bytearray(width * height * 4)
    held = (0, 0, 0)
    for tile in range(512 if mode == 'programmed-dual' else 256):
        n = tile % 256
        row, column = divmod(n, 16)
        if mode == 'programmed-dual':
            use_second = column > 0 and (tile >= 256 or row == 0)
            index = (column + 32 if use_second else row) ^ 128
            rgb = (index, index*37 % 256, index*73 % 256)
            top, size = (136 if tile >= 256 else 16), 7
        elif mode.startswith('ham'):
            if column == 0: held = (0, 0, 0)
            command, index = (n % 4, n // 4) if mode == 'ham8' else (n // 16 % 4, n % 16)
            if command == 0: held = (index, index*37 % 256, index*73 % 256)
            else:
                component = {1: 2, 2: 0, 3: 1}[command]
                value = index*4 + held[component] % 4 if mode == 'ham8' else index*17
                held = tuple(value if c == component else held[c] for c in range(3))
            rgb = held
            top, size = 16, 15
        else:
            rgb = (n, n*37 % 256, n*73 % 256)
            top, size = 16, 15
        pixel = bytes((rgb[2], rgb[1], rgb[0], 255))
        for y in range(44+top+row*size+2, 44+top+(row+1)*size-2):
            x = 516+column*80+8
            offset = (y*width+x)*4
            payload[offset:offset+256] = pixel*64
    header = b'BM' + struct.pack('<IHHI', 54+len(payload), 0, 0, 54)
    header += struct.pack('<IiiHHIIIIII', 40, width, -height, 1, 32, 0, len(payload), 0, 0, 0, 0)
    return bytearray(header + payload)


class AgaProbeControls(unittest.TestCase):
    def check(self, data, mode, valid):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'synthetic.bmp'
            path.write_bytes(data)
            result = subprocess.run([sys.executable, str(CHECKER), str(path), '--mode', mode], capture_output=True, text=True)
            if valid:
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertGreater(json.loads(result.stdout)['exactInteriorPixels'], 0)
            else:
                self.assertNotEqual(0, result.returncode)

    def test_all_four_valid_layouts(self):
        for mode in ('rgb24', 'ham6', 'ham8', 'programmed-dual'):
            with self.subTest(mode=mode): self.check(raster(mode), mode, True)

    def test_one_corrupt_sample_is_rejected_in_every_mode(self):
        for mode in ('rgb24', 'ham6', 'ham8', 'programmed-dual'):
            data = raster(mode)
            data[54+(62*1816+524)*4] ^= 1
            with self.subTest(mode=mode): self.check(data, mode, False)

    def test_ham8_lost_low_bits_are_rejected(self):
        data = raster('ham8')
        # Direct COLOR01 has B=73; raw code 5 replaces the upper bits with 4,
        # retaining the low bit: B=5. A zero-filled low-bit implementation gives 4.
        data[54+(62*1816+924)*4] = 4
        self.check(data, 'ham8', False)

    def test_wrong_second_grid_priority_is_rejected(self):
        data = raster('programmed-dual')
        # Both fields are opaque: PF2=1 must win on the second grid.
        data[54+(189*1816+604)*4:54+(189*1816+604)*4+4] = bytes((201, 165, 129, 255))
        self.check(data, 'programmed-dual', False)

    def test_wrong_geometry_and_truncated_data_are_rejected(self):
        data = raster('rgb24')
        self.check(data[:-4], 'rgb24', False)
        struct.pack_into('<i', data, 18, 908)
        self.check(data, 'rgb24', False)


def sprite_raster(mode):
    width, height = 1816, 313
    def colour(n): return bytes((n*73 % 256, n*37 % 256, n, 255))
    payload = bytearray(colour(64)*(width*height))
    size = {'sprites16': 16, 'sprites32': 32, 'sprites32page': 32, 'sprites64': 64}[mode]
    for top, first, step in ((64,0,4), (80,4,4), (112,0,2), (128,4,2), (160,0,1), (176,4,1)):
        values = [[0]*width for _ in range(8)]
        for channel in range(first, first+4):
            left = (140+70*(channel % 4 if channel != 7 else 2))*4+channel % 4
            for i in range(size):
                source = i % 16 if mode == 'sprites32page' else i
                pixel = (source+channel+source//16) % 4
                values[channel][left+i*step:left+(i+1)*step] = [pixel]*step
        for x in range(width):
            selected = 64
            for pair in range(first, first+4, 2):
                even, odd = values[pair][x], values[pair+1][x]
                if pair == 6:
                    pen = even+4*odd
                    if pen: selected = 176+pen; break
                elif even:
                    selected = 32+pair*2+even; break
                elif odd:
                    selected = 176+pair*2+odd; break
            for y in range(top, top+8): payload[(y*width+x)*4:(y*width+x+1)*4] = colour(selected)
    header = b'BM'+struct.pack('<IHHI',54+len(payload),0,0,54)
    header += struct.pack('<IiiHHIIIIII',40,width,-height,1,32,0,len(payload),0,0,0,0)
    ram = bytearray(2*1024*1024)
    proof = b'AGASPRCOLv1\0'+struct.pack('>6H',0x8067,0x8199,0x8000,0x8000,0x8060,0x8180)
    ram[0x2000:0x2000+len(proof)] = proof
    return bytearray(header+payload), ram


class AgaSpriteProbeControls(unittest.TestCase):
    def check(self, data, ram, mode, valid):
        with tempfile.TemporaryDirectory() as folder:
            bitmap, memory = Path(folder)/'synthetic.bmp', Path(folder)/'synthetic.chipram'
            bitmap.write_bytes(data); memory.write_bytes(ram)
            result = subprocess.run([sys.executable, str(SPRITE_CHECKER), str(bitmap), '--mode', mode,
                                     '--chipram', str(memory)], capture_output=True, text=True)
            if valid:
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(53376, json.loads(result.stdout)['exactPixels'])
            else: self.assertNotEqual(0, result.returncode)

    def test_all_four_native_sprite_layouts(self):
        for mode in ('sprites16','sprites32','sprites32page','sprites64'):
            with self.subTest(mode=mode): self.check(*sprite_raster(mode), mode, True)

    def test_wrong_odd_and_attached_bank_are_rejected(self):
        for x, y in ((841,64), (1122,80)):
            data, ram = sprite_raster('sprites64')
            data[54+(y*1816+x)*4+2] = 46
            with self.subTest(pixel=(x,y)): self.check(data, ram, 'sprites64', False)

    def test_missing_wide_tail_or_fine_position_is_rejected(self):
        for mode, x, y in (('sprites32',640,64), ('sprites64',736,64), ('sprites16',841,160)):
            data, ram = sprite_raster(mode)
            data[54+(y*1816+x)*4] ^= 1
            with self.subTest(mode=mode): self.check(data, ram, mode, False)

    def test_missing_corrupt_and_duplicate_guest_collision_proofs_are_rejected(self):
        for failure in ('missing','corrupt','duplicate'):
            data, ram = sprite_raster('sprites64')
            if failure == 'missing': ram[0x2000] = 0
            elif failure == 'corrupt': ram[0x200D] ^= 1
            else: ram[0x3000:0x300C] = b'AGASPRCOLv1\0'
            with self.subTest(failure=failure): self.check(data, ram, 'sprites64', False)

    def test_truncated_capture_is_rejected(self):
        data, ram = sprite_raster('sprites16'); self.check(data[:-4], ram, 'sprites16', False)


if __name__ == '__main__': unittest.main()
