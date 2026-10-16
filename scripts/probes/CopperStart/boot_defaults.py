"""Generate an original boot block that records ROM handoff defaults.

Replay the ADF with replay.py on the same A500 profile for each ROM. At $920,
54455354 means the probe finished; $938 holds CHIP mh_Upper, $93C CIA B DDRB,
and $93E ADKCONR. With --memory-limits, $940/$944 hold ExecBase
MaxLocMem/MaxExtMem and $948 the raw low word of its checksum sum.
With --dma-state, $94A holds DMACONR at boot-block entry.
With --idle-disk-dma, read/motor results are at $950/$95C, read byte count at
$954 and the DMA2 payload at $958. Both calls begin with idle DSKEN enabled.
The default image preserves the original probe bytes. The probe walks MemList
to find CHIP, since other memory can precede it. It reads guest state through
normal 68000 instructions.
"""

import argparse
from pathlib import Path
import struct


def make_disk(memory_limits=False, dma_state=False, idle_disk_dma=False):
    words = [0x21CE, 0x0904,                       # save SysBase
             0x13F9, 0x00BF, 0xD300, 0, 0x093C,  # MOVE.B CIA B DDRB,$93C
             0x33F9, 0x00DF, 0xF010, 0, 0x093E]  # MOVE.W ADKCONR,$93E
    if memory_limits:
        words += [0x21EE, 62, 0x0940,             # MaxLocMem
                  0x21EE, 78, 0x0944,             # MaxExtMem
                  0x7000, 0x7218, 0x41EE, 34,     # D0=0,D1=24,A0=SoftVer
                  0xD058, 0x51C9, 0xFFFC,         # ADD.W (A0)+,D0; DBRA
                  0x31C0, 0x0948]
    if dma_state:
        words += [0x33F9, 0x00DF, 0xF002, 0, 0x094A]  # MOVE.W DMACONR,$94A
    if idle_disk_dma:
        words += [
            0x2849,                              # preserve boot IORequest in A4
            0x203C, 0, 512, 0x223C, 1, 2,         # AllocMem(512, CHIP|CLEAR)
            0x4EAE, 0xFF3A, 0x2440, 0x224C,      # A2=buffer, restore A1
            0x234A, 40, 0x237C, 0, 512, 36,       # io_Data, io_Length
            0x237C, 0, 5632, 44, 0x337C, 2, 28,   # io_Offset, CMD_READ
            0x33FC, 0x4000, 0x00DF, 0xF024,       # idle DSKLEN
            0x33FC, 0x8210, 0x00DF, 0xF096,       # master + disk enables
            0x4EAE, 0xFE38, 0x21C0, 0x0950,       # DoIO result
            0x224C, 0x21E9, 32, 0x0954,           # io_Actual
            0x21D2, 0x0958,                       # first payload longword
            0x337C, 9, 28, 0x42A9, 36,            # TD_MOTOR, length=0
            0x33FC, 0x8210, 0x00DF, 0xF096,
            0x4EAE, 0xFE38, 0x21C0, 0x095C,
        ]
    words += [0x206E, 0x0142]                     # A0=Exec MemList head
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
    if idle_disk_dma:
        struct.pack_into('>I', disk, 5632, 0x444D4132)  # DMA2
    checksum = 0
    for offset in range(0, 1024, 4):
        value = checksum + struct.unpack_from('>I', disk, offset)[0]
        checksum = (value & 0xFFFFFFFF) + (value >> 32)
    struct.pack_into('>I', disk, 4, (~checksum) & 0xFFFFFFFF)
    return disk


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--memory-limits', action='store_true')
    parser.add_argument('--dma-state', action='store_true')
    parser.add_argument('--idle-disk-dma', action='store_true')
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as output:
        output.write(make_disk(args.memory_limits, args.dma_state, args.idle_disk_dma))
