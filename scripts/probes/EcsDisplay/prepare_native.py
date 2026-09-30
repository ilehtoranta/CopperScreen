"""Add an original 68000 OpenScreenTagList probe to a disposable supplied FFS/RDB image.

No ROM, OS, or monitor-driver bytes are distributed. Native Kickstart allocates
the screen, builds its Copper list, draws four panels, and writes the proof file.
Stock probes use NDK library vectors and SA_* tags. Separately named programmed
probes take chipset ownership after native screen allocation and proof I/O.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('source', type=Path)
p.add_argument('output', type=Path)
p.add_argument('--mode', choices=['super', 'super-lace', 'ntsc-super', 'productivity', 'programmed-ntsc-super', 'programmed-productivity'], default='super')
p.add_argument('--ndk', type=Path, required=True, help='Directory containing supplied FD library definitions')
p.add_argument('--diagnostics', action='store_true', help='Write native mode availability and screen return data')
a = p.parse_args()
mode, width, height = {
    'super': (0x29020, 1280, 256),
    'super-lace': (0x29024, 1280, 512),
    'ntsc-super': (0x19020, 1280, 200),
    'productivity': (0x39024, 640, 480),  # VGAPRODUCT_KEY; ModeID bits are not BPLCON0 bits
    'programmed-ntsc-super': (0x29020, 1280, 200),
    'programmed-productivity': (0x29020, 640, 480),
}[a.mode]
programmed = a.mode.startswith('programmed-')

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
    lea(name, 1); moveq(0, 36); jsr(-552)  # OpenLibrary(name,36)
    words(0x4a80); relative(0x6700, 'fail')
    words(0x2040 + register * 0x200)       # movea.l d0,areg
if a.diagnostics:
    words(0x2c4d); immediate(0, mode); jsr(gfx['ModeNotAvailable']); words(0x2e00)
words(0x2c4c, 0x207c); long(0)             # IntuitionBase, newScreen=NULL
lea('tags', 1); jsr(intuition['OpenScreenTagList'])
if a.diagnostics:
    words(0x2640)                         # preserve screen across guest DOS calls
    lea('diagnostic', 0); words(0x2087, 0x2140, 4, 0x4a80)
    relative(0x6700, 'diagnostic-write')
    words(0x316b, 12, 8, 0x316b, 14, 10)
    label('diagnostic-write')
    words(0x2c4a); lea('diagnostic-name', 0); words(0x2208); immediate(2, 1006); jsr(dos['Open'])
    words(0x2800, 0x4a80); relative(0x6700, 'fail')
    words(0x2204); lea('diagnostic', 0); words(0x2408); immediate(3, 12); jsr(dos['Write'])
    words(0x2204); jsr(dos['Close']); words(0x200b)
words(0x4a80); relative(0x6700, 'fail')
words(0x2640)                             # screen -> a3
for offset, value in [(12, width), (14, height)]:
    words(0x302b, offset, 0x0c40, value)    # native Screen geometry must match
    relative(0x6600, 'fail')
words(0x2c4d)                             # GfxBase
for index, rgb in enumerate([(0, 0, 0), (15, 15, 15), (15, 0, 0), (0, 0, 15)]):
    words(0x41eb, 44)                     # &screen->ViewPort
    moveq(0, index)
    for r, value in enumerate(rgb, 1): moveq(r, value)
    jsr(gfx['SetRGB4'])
for pen, bounds in enumerate([(0, 16, width//2-1, height//2-1),
                             (width//2, 16, width-1, height//2-1),
                             (0, height//2, width//2-1, height-1),
                             (width//2, height//2, width-1, height-1)]):
    words(0x43eb, 84); moveq(0, pen); jsr(gfx['SetAPen'])
    words(0x43eb, 84)
    for r, value in enumerate(bounds): immediate(r, value)
    jsr(gfx['RectFill'])
jsr(gfx['WaitBlit'])                       # finish all drawing before taking chipset ownership
# Independently verifiable guest DOS file is written only after a real screen opens.
words(0x2c4a); lea('proofname', 0); words(0x2208); immediate(2, 1006); jsr(dos['Open'])
words(0x2800, 0x4a80); relative(0x6700, 'fail')
words(0x2204); lea('proof', 0); words(0x2408)
proof = f'Native ECS {a.mode} {width}x{height} screen opened\n'.encode()
immediate(3, len(proof)); jsr(dos['Write']); words(0x2204); jsr(dos['Close'])
if programmed:
    # Non-default monitor launchers are not on the supplied Workbench/Install media.
    # This separate hardware probe takes ownership after native ROM allocation,
    # drawing and DOS proof I/O. It does not stand in for the stock VGA driver.
    words(0x2c78, 4); jsr(-132); jsr(-120)  # Forbid, Disable
    def custom(r, v):
        words(0x33fc, v); long(0xdff000 + r)
    custom(0x096, 0x7fff); custom(0x09a, 0x7fff)
    lea('copper', 0)
    for screen_offset, low_offset, high_offset in [(192, 6, 2), (196, 14, 10)]:
        words(0x202b, screen_offset, 0x3140, low_offset, 0x4840, 0x3140, high_offset)
    words(0x2008, 0x33c0); long(0xdff082)
    words(0x4840, 0x33c0); long(0xdff080)
    timing = [(0x1c0,113), (0x1c8,524), (0x1c2,8), (0x1de,0),
                 (0x1ca,3), (0x1e0,0), (0x1e2,57),
                 (0x1c4,105), (0x1c6,19), (0x1cc,510), (0x1ce,30),
                 (0x1dc,0x5b80), (0x100,0x2271), (0x08e,0x1e29),
                 (0x090,0xfec9), (0x1e4,0x0100), (0x092,0x12),
                 (0x094,0x58), (0x108,0), (0x10a,0)] if a.mode == 'programmed-productivity' else [
                 (0x1dc,0), (0x100,0x2241), (0x08e,0x2c81),
                 (0x090,0xf4c1), (0x1e4,0x2000), (0x092,0x38),
                 (0x094,0xd8), (0x108,0xfff8), (0x10a,0xfff8)]
    for r, v in timing + [(0x102,0), (0x106,0x10), (0x088,0), (0x096,0x8380)]: custom(r,v)
    words(0x60fe)                         # unprivileged task holds mode; STOP would trap
words(0x2c4d); immediate(7, 6000)
label('wait'); jsr(gfx['WaitTOF']); relative(0x51cf, 'wait')  # dbra d7
words(0x2c4c, 0x204b); jsr(intuition['CloseScreen']); moveq(0, 0)
relative(0x6000, 'exit')
label('fail'); moveq(0, 20)
label('exit'); words(0x4cdf, 0x7cfc, 0x4e75)
for name in ('intuition', 'graphics', 'dos'):
    label(name); code.extend((name + '.library\0').encode())
    if len(code) & 1: code.append(0)
label('proofname'); code.extend(b'SYS:ecs-screen-proof.txt\0')
label('proof'); code.extend(proof)
if len(code) & 1: code.append(0)
if a.diagnostics:
    label('diagnostic-name'); code.extend(b'SYS:ecs-mode-diagnostic.bin\0')
    if len(code) & 1: code.append(0)
    label('diagnostic'); code.extend(bytes(12))
label('tags')
for tag, value in [(0x80000023, width), (0x80000024, height), (0x80000025, 2),
                   (0x80000032, mode), (0x8000002d, 15), (0x80000038, 1), (0, 0)]:
    long(tag); long(value)
if programmed:
    label('copper')
    for r in [0x0e0,0x0e2,0x0e4,0x0e6]: words(r,0)
    colors = [(0,0,0),(3,3,3),(3,0,0),(0,0,3)]
    for i in range(16):
        left, right = colors[i&3], colors[i>>2]
        encoded = sum(((left[c]<<2)|right[c]) << (8-c*4) for c in range(3))
        words(0x180+i*2, encoded)
    words(0xffff,0xfffe)
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
name = b'EcsDisplayProbe'
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
payload = b'FailAt 21\nMakeDir RAM:ENV RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\nSYS:EcsDisplayProbe\nEcho "ECS PROBE EXITED"\n'
data[block*512:(block+1)*512] = payload + bytes(512-len(payload))
put(startup*512+324, len(payload)); checksum(startup)
image = original[:32*512] + data
with a.output.open('xb') as f: f.write(image)
print(json.dumps({'mode': a.mode, 'displayId': f'{mode:08X}', 'width': width, 'height': height,
                  'proof': proof.decode(), 'hunkSha256': hashlib.sha256(executable).hexdigest(),
                  'sourceSha256': hashlib.sha256(original).hexdigest(), 'imageSha256': hashlib.sha256(image).hexdigest()}, indent=2))
