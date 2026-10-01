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


if __name__ == '__main__': unittest.main()
