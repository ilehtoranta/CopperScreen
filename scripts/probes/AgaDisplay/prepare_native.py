"""Add an original 68000 OpenScreenTagList probe to a disposable supplied FFS/RDB image.

No ROM, OS, or monitor-driver bytes are distributed. Native A1200 Kickstart allocates
the screen, builds its Copper list, draws 256 RGB24 tiles, and writes the proof file.
The original guest code uses supplied NDK library vectors and SA_* tags.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('source', type=Path)
p.add_argument('output', type=Path)
p.add_argument('--ndk', type=Path, required=True, help='Directory containing supplied FD library definitions')
a = p.parse_args()
mode, width, height = 0x21000, 320, 256

def vectors(name):
    result, bias = {}, 0
    for line in (a.ndk / name).read_text().splitlines():
        if line.startswith('##bias'): bias = int(line.split()[1])
        elif line and not line.startswith(('*', '#')):
            result[line.split('(')[0]] = -bias
            bias += 6
    return result

gfx, intuition, dos = [vectors(n + '_LIB.FD') for n in ('GRAPHICS', 'INTUITION', 'DOS')]
code, labels, fixes = bytearray(), {}, []
def words(*values):
    for v in values: code.extend(struct.pack('>H', v & 0xffff))
def long(v): code.extend(struct.pack('>I', v & 0xffffffff))
def label(n): labels[n] = len(code)
def relative(op, n):
    words(op); fixes.append((len(code), n)); words(0)
def lea(n, reg): relative(0x41fa + reg * 0x200, n)
def jsr(v): words(0x4eae, v)
def moveq(reg, value): words(0x7000 + reg * 0x200 + (value & 255))
def immediate(reg, v): words(0x203c + reg * 0x200); long(v)

words(0x48e7, 0x3f3e)                     # movem.l d2-d7/a2-a6,-(sp)
for name, register in [('intuition', 4), ('graphics', 5), ('dos', 2)]:
    words(0x2c78, 4)                       # ExecBase from address 4
    lea(name, 1); moveq(0, 39); jsr(-552)  # OpenLibrary(name,39)
    words(0x4a80); relative(0x6700, 'fail')
    words(0x2040 + register * 0x200)       # movea.l d0,areg
# A fresh ROM/DOS startup enables ECS compatibility. This V39 OS call is the
# documented bootblock equivalent of SetPatch's chipset activation; the ROM
# detects the fitted hardware and updates its display database itself.
words(0x2c4d); moveq(0, -1); jsr(gfx['SetChipRev'])
immediate(0, mode); jsr(gfx['ModeNotAvailable']); words(0x2e00)
words(0x2c4c, 0x207c); long(0)             # IntuitionBase, newScreen=NULL
lea('tags', 1); jsr(intuition['OpenScreenTagList'])
words(0x2640)                             # keep result across guest diagnostic I/O
lea('diagnostic', 0); words(0x2087, 0x2140, 4, 0x116D, 236, 13, 0x4A80)
relative(0x6700, 'diagnostic-write')
words(0x316B, 12, 8, 0x316B, 14, 10, 0x116B, 189, 12)
label('diagnostic-write')
words(0x2C4A); lea('diagnostic-name', 0); words(0x2208); immediate(2, 1006); jsr(dos['Open'])
words(0x2800, 0x4A80); relative(0x6700, 'fail')
words(0x2204); lea('diagnostic', 0); words(0x2408); immediate(3, 16); jsr(dos['Write'])
words(0x2204); jsr(dos['Close']); words(0x200B)
words(0x4a80); relative(0x6700, 'fail')
words(0x2640)                             # screen -> a3
for offset, value in [(12, width), (14, height)]:
    words(0x302b, offset, 0x0c40, value)    # native Screen geometry must match
    relative(0x6600, 'fail')
words(0x2c4d)                             # GfxBase
words(0x0c2b, 8, 189)                    # Screen.BitMap.Depth must be eight
relative(0x6600, 'fail')
for index in range(256):
    words(0x41eb, 44); immediate(0, index)
    for register, value in enumerate((index, index*37 & 255, index*73 & 255), 1):
        immediate(register, value * 0x01010101)
    jsr(gfx['SetRGB32'])
for pen in range(256):
    words(0x43eb, 84); immediate(0, pen); jsr(gfx['SetAPen'])
    words(0x43eb, 84)
    column, row = pen % 16, pen // 16
    for register, value in enumerate((column*20, 16+row*15, column*20+19, 30+row*15)):
        immediate(register, value)
    jsr(gfx['RectFill'])
jsr(gfx['WaitBlit'])                       # finish drawing before proof-file I/O
# Independently verifiable guest DOS file is written only after a real screen opens.
words(0x2c4a); lea('proofname', 0); words(0x2208); immediate(2, 1006); jsr(dos['Open'])
words(0x2800, 0x4a80); relative(0x6700, 'fail')
words(0x2204); lea('proof', 0); words(0x2408)
proof = b'Native AGA PAL 320x256 depth 8: all 256 RGB24 colours drawn\n'
immediate(3, len(proof)); jsr(dos['Write']); words(0x2204); jsr(dos['Close'])
words(0x2c4d); immediate(7, 6000)
label('wait'); jsr(gfx['WaitTOF']); words(0x5387); relative(0x6600, 'wait')
words(0x2c4c, 0x204b); jsr(intuition['CloseScreen']); moveq(0, 0)
relative(0x6000, 'exit')
label('fail'); moveq(0, 20)
label('exit'); words(0x4cdf, 0x7cfc, 0x4e75)
for name in ('intuition', 'graphics', 'dos'):
    label(name); code.extend((name + '.library\0').encode())
    if len(code) & 1: code.append(0)
label('proofname'); code.extend(b'SYS:aga-screen-proof.txt\0')
label('proof'); code.extend(proof)
if len(code) & 1: code.append(0)
label('diagnostic-name'); code.extend(b'SYS:aga-diagnostic.bin\0')
if len(code) & 1: code.append(0)
label('diagnostic'); code.extend(bytes(16))
label('tags')
for tag, value in [(0x80000023, width), (0x80000024, height), (0x80000025, 8),
                   (0x80000032, mode), (0x8000002d, 15), (0x80000038, 1), (0, 0)]:
    long(tag); long(value)
for offset, name in fixes: struct.pack_into('>h', code, offset, labels[name] - offset)
while len(code) % 4: code.append(0)
executable = struct.pack('>8I', 1011, 0, 1, 0, 0, len(code)//4, 1001, len(code)//4) + code + struct.pack('>I', 1010)

original = a.source.read_bytes()
assert len(original) == 917504 and original[32*512:32*512+4] == b'DOS\1'
data = bytearray(original[32*512:])
def get(off): return struct.unpack_from('>I', data, off)[0]
def put(off, v): struct.pack_into('>I', data, off, v & 0xffffffff)
def checksum(block, field=20):
    put(block*512+field, 0); put(block*512+field, -sum(struct.unpack_from('>128I', data, block*512)))
root = 880
bitmap = get(root*512+316)
assert 0 < bitmap < 1760
def allocate():
    for block in range(2, 1760):
        word = bitmap*512 + 4 + ((block-2)//32)*4
        mask = 1 << ((block-2)%32)
        if get(word) & mask:
            put(word, get(word) & ~mask); data[block*512:(block+1)*512] = bytes(512)
            return block
    raise ValueError('Fixture has no free blocks')
header = allocate()
blocks = [allocate() for _ in range((len(executable)+511)//512)]
name = b'AgaDisplayProbe'
put(header*512, 2); put(header*512+4, header); put(header*512+8, len(blocks)); put(header*512+16, blocks[0])
for i, block in enumerate(blocks):
    put(header*512+308-i*4, block)
    payload = executable[i*512:(i+1)*512]
    data[block*512:block*512+len(payload)] = payload
put(header*512+324, len(executable)); data[header*512+432] = len(name); data[header*512+433:header*512+433+len(name)] = name
put(header*512+500, root); put(header*512+508, -3)
h = len(name)
for c in name.upper(): h = (h*13+c) & 0x7ff
slot = root*512+24+(h%72)*4
put(header*512+496, get(slot)); put(slot, header)
checksum(header); checksum(root); checksum(bitmap, 0)
startup = next(i for i in range(2,1760) if get(i*512)==2 and get(i*512+508)==0xfffffffd and
               data[i*512+433:i*512+433+data[i*512+432]].lower()==b'startup-sequence')
block = get(startup*512+308)
payload = b'FailAt 21\nMakeDir RAM:ENV RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\nSYS:AgaDisplayProbe\nEcho "AGA PROBE EXITED"\n'
data[block*512:(block+1)*512] = payload + bytes(512-len(payload))
put(startup*512+324, len(payload)); checksum(startup)
image = original[:32*512] + data
with a.output.open('xb') as f: f.write(image)
print(json.dumps({'mode': 'aga-pal-256', 'displayId': f'{mode:08X}', 'width': width, 'height': height,
                  'proof': proof.decode(), 'hunkSha256': hashlib.sha256(executable).hexdigest(),
                  'sourceSha256': hashlib.sha256(original).hexdigest(), 'imageSha256': hashlib.sha256(image).hexdigest()}, indent=2))
