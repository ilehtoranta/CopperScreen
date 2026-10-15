"""Generate an original boot block that records ROM handoff defaults.

Replay the ADF with replay.py on the same A500 profile for each ROM. At $920,
54455354 means the probe finished; $938 holds CHIP mh_Upper, $93C CIA B DDRB,
and $93E ADKCONR. The probe walks MemList to find CHIP, since other memory can
precede it. It reads guest state through normal 68000 instructions.
"""

import argparse
from pathlib import Path
import struct


def make_disk():
    words = [0x21CE, 0x0904,                       # save SysBase
             0x13F9, 0x00BF, 0xD300, 0, 0x093C,  # MOVE.B CIA B DDRB,$93C
             0x33F9, 0x00DF, 0xF010, 0, 0x093E,  # MOVE.W ADKCONR,$93E
             0x206E, 0x0142]                     # A0=Exec MemList head
    loop = len(words)
    words += [0x3028, 14, 0x0240, 2]              # test MEMF_CHIP
    found_branch = len(words)
    words += [0x6600, 0x2050]                     # BNE found; A0=successor
    loop_branch = len(words)
    words += [0x6000]
    found = len(words)
    words += [0x21E8, 24, 0x0938,                 # copy mh_Upper
              0x21FC, 0x5445, 0x5354, 0x0920, 0x60FE]
    words[found_branch] |= (found * 2 - (found_branch * 2 + 2)) & 0xFF
    words[loop_branch] |= (loop * 2 - (loop_branch * 2 + 2)) & 0xFF
    disk = bytearray(901120)
    struct.pack_into('>I', disk, 0, 0x444F5300)
    struct.pack_into('>' + 'H' * len(words), disk, 12, *words)
    checksum = 0
    for offset in range(0, 1024, 4):
        value = checksum + struct.unpack_from('>I', disk, offset)[0]
        checksum = (value & 0xFFFFFFFF) + (value >> 32)
    struct.pack_into('>I', disk, 4, (~checksum) & 0xFFFFFFFF)
    return disk


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as output:
        output.write(make_disk())
