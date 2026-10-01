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
p.add_argument('--mode', choices=['rgb24', 'ham6', 'ham8', 'programmed-dual',
                                'sprites16', 'sprites32', 'sprites32page', 'sprites64'], default='rgb24')
p.add_argument('--ndk', type=Path, required=True, help='Directory containing supplied FD library definitions')
a = p.parse_args()
mode, width, height = 0x21000, 320, 256
depth = 6 if a.mode == 'ham6' else 8
sprite_fetch = {'sprites16': 0, 'sprites32': 1, 'sprites32page': 2, 'sprites64': 3}.get(a.mode)
if a.mode in ('ham6', 'ham8'): mode |= 0x800

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
words(0x0c2b, depth, 189)                # validate the native bitmap depth
relative(0x6600, 'fail')
for index in range(16 if a.mode == 'ham6' else 64 if a.mode == 'ham8' else 256):
    words(0x41eb, 44); immediate(0, index)
    for register, value in enumerate((index, index*37 & 255, index*73 & 255), 1):
        immediate(register, value * 0x01010101)
    jsr(gfx['SetRGB32'])
tiles = []
for pen in range(256):
    column, row = pen % 16, pen // 16
    if sprite_fetch is not None:
        continue
    if a.mode == 'programmed-dual':
        raw = sum(((row >> bit & 1) << (2*bit)) | ((column >> bit & 1) << (2*bit+1)) for bit in range(4))
        for top in (16, 136): tiles.append((raw, (column*20, top+row*7, column*20+19, top+row*7+6)))
    else: tiles.append((pen & 63 if depth == 6 else pen, (column*20, 16+row*15, column*20+19, 30+row*15)))
if sprite_fetch is not None:
    tiles = [(0xC0, (0, 0, 319, 255))]
for pen, bounds in tiles:
    # V39 HAM8 display Copper lists rotate the bitmap planes: graphics pens
    # keep their two command bits at 7/6, while Lisa sees them at BP2/BP1.
    # Convert our specified raw Lisa code to the OS's logical drawing pen.
    drawing_pen = (pen >> 2) | ((pen & 3) << 6) if a.mode == 'ham8' else pen
    words(0x43eb, 84); immediate(0, drawing_pen); jsr(gfx['SetAPen'])
    words(0x43eb, 84)
    for register, value in enumerate(bounds):
        immediate(register, value)
    jsr(gfx['RectFill'])
jsr(gfx['WaitBlit'])                       # finish drawing before proof-file I/O
# Independently verifiable guest DOS file is written only after a real screen opens.
words(0x2c4a); lea('proofname', 0); words(0x2208); immediate(2, 1006); jsr(dos['Open'])
words(0x2800, 0x4a80); relative(0x6700, 'fail')
words(0x2204); lea('proof', 0); words(0x2408)
proof = b'Native AGA PAL 320x256 depth 8: all 256 RGB24 colours drawn\n'
if a.mode != 'rgb24':
    proof = f'Native AGA {a.mode} PAL 320x256 depth {depth}: encoded pixels drawn\n'.encode()
if sprite_fetch is not None:
    proof = f'Native AGA {a.mode} PAL 320x256 depth 8: sprite DMA data prepared\n'.encode()
immediate(3, len(proof)); jsr(dos['Write']); words(0x2204); jsr(dos['Close'])
if a.mode == 'programmed-dual':
    # The OS allocated/drew the eight-plane bitmap and wrote the proof. This
    # separately named probe then owns the chipset; it is not a stock DPF screen.
    words(0x2c78, 4); jsr(-132); jsr(-120)  # Forbid, Disable
    def custom(r, v):
        words(0x33fc, v); long(0xdff000+r)
    custom(0x096, 0x7fff); custom(0x09a, 0x7fff)
    lea('copper', 0)
    for plane in range(8):
        words(0x202b, 192+plane*4, 0x3140, plane*8+6, 0x4840, 0x3140, plane*8+2)
    words(0x2008, 0x33c0); long(0xdff082)
    words(0x4840, 0x33c0); long(0xdff080)
    for r, v in [(0x100,0x0610), (0x102,0), (0x104,0x24), (0x106,0x1440),
                 (0x10c,0x8011), (0x1fc,3), (0x08e,0x2c81), (0x090,0x2cc1),
                 (0x1e4,0x2100), (0x092,0x38), (0x094,0xd8), (0x108,0xfff8),
                 (0x10a,0xfff8), (0x088,0), (0x096,0x8380)]: custom(r,v)
    words(0x60fe)                         # task holds mode without a privileged STOP
if sprite_fetch is not None:
    # Native Exec allocates and aligns the DMA data in Chip RAM. The Copper
    # list itself is in this HUNK on the explicit no-Fast-RAM A1200 profile.
    words(0x2c78, 4); jsr(-132); jsr(-120)  # Forbid, Disable
    def custom(r, v):
        words(0x33fc, v); long(0xdff000+r)
    custom(0x096, 0x7fff); custom(0x09a, 0x7fff)
    byte_width = 2 if sprite_fetch == 0 else 8 if sprite_fetch == 3 else 4
    stream_bytes = byte_width * 56
    immediate(0, stream_bytes*8+7); immediate(1, 0x10002); jsr(-198)  # AllocMem(MEMF_CHIP|CLEAR)
    words(0x4a80); relative(0x6700, 'fail')
    words(0x0680); long(7); words(0x0280); long(0xfffffff8); words(0x2c00,0x2440)
    lea('sprite-0', 1); immediate(1, stream_bytes*4-1)
    label('sprite-copy'); words(0x34d9); relative(0x51c9, 'sprite-copy')
    lea('sprite-copper', 0)
    for plane in range(8):
        words(0x202b, 192+plane*4, 0x3140, plane*8+6, 0x4840, 0x3140, plane*8+2)
    for sprite in range(8):
        words(0x2006,0x0680); long(sprite*stream_bytes)
        words(0x3140, 64+sprite*8+6, 0x4840, 0x3140, 64+sprite*8+2)
    words(0x2008, 0x33c0); long(0xdff082)
    words(0x4840, 0x33c0); long(0xdff080)
    for r, v in [(0x100,0x0211), (0x102,0), (0x104,0x24), (0x106,0x0c40),
                 (0x10c,0x802b), (0x1fc,3 | sprite_fetch << 2), (0x08e,0x2c81), (0x090,0x2cc1),
                 (0x1e4,0x2100), (0x092,0x38), (0x094,0xd8), (0x108,0xfff8),
                 (0x10a,0xfff8), (0x098,0xf000), (0x10e,0xc3), (0x088,0), (0x096,0x83a0)]: custom(r,v)
    label('sprite-sample-field'); lea('sprite-collisions', 0)
    for row, y in enumerate((64,80,112,128,160,176)):
        for phase, line in [('clear', y+1), ('read', y+9)]:
            label(f'sprite-{row}-{phase}')
            words(0x3039); long(0xdff006); words(0x0240,0xff00,0x0c40,line << 8)
            relative(0x6600, f'sprite-{row}-{phase}')
            if phase == 'clear': words(0x3039); long(0xdff00e)
            else:
                words(0x3039); long(0xdff00e); words(0x30c0)  # native read-clear -> d0 -> proof RAM
    relative(0x6000, 'sprite-sample-field')
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
for tag, value in [(0x80000023, width), (0x80000024, height), (0x80000025, depth),
                   (0x80000032, mode), (0x8000002d, 15), (0x80000038, 1), (0, 0)]:
    long(tag); long(value)
if a.mode == 'programmed-dual':
    label('copper')
    for plane in range(8): words(0xe0+plane*4,0,0xe2+plane*4,0)
    # Restart pointers and priority each field, then exchange priorities at the
    # second grid. The guest chooses PF2 offset 32 and colour-address XOR 128.
    words(0x104,0x24,0xb401,0xfffe,0x104,0x64,0xffff,0xfffe)
if sprite_fetch is not None:
    label('sprite-copper')
    for plane in range(8): words(0xe0+plane*4,0,0xe2+plane*4,0)
    for sprite in range(8): words(0x120+sprite*4,0,0x122+sprite*4,0)
    words(0x106,0x0c40,0x10e,0xc3, 0x6001,0xfffe,0x106,0x0c80,0x10e,0xc1,
          0x9001,0xfffe,0x106,0x0cc0,0x10e,0xc2, 0xffff,0xfffe)
    # Each stream's size is a multiple of eight bytes. The guest copies this
    # original payload to the aligned MEMF_CHIP allocation above.
    for sprite in range(8):
        label(f'sprite-{sprite}')
        for y in ((64,112,160) if sprite < 4 else (80,128,176)):
            x = 140 + 70 * (sprite % 4 if sprite != 7 else 2)
            fine = sprite % 4
            pos = y << 8 | ((x-1) >> 1)
            ctl = (y+8) << 8 | ((x-1) & 1) | ((fine & 2) << 3) | ((fine & 1) << 3) | (0x80 if sprite == 7 else 0)
            byte_width = 2 if sprite_fetch == 0 else 8 if sprite_fetch == 3 else 4
            for control in (pos, ctl):
                words(control)
                code.extend(bytes(byte_width-2))
            for _ in range(8):
                for plane in range(2):
                    for word in range(byte_width // 2):
                        pattern = 0
                        for bit in range(16):
                            i = word*16+bit
                            pixel = (i + sprite + i//16) % 4
                            pattern |= ((pixel >> plane) & 1) << (15-bit)
                        words(pattern)
        code.extend(bytes(byte_width*2))
    if len(code) & 1: code.append(0)
    code.extend(b'AGASPRCOLv1\0')
    label('sprite-collisions'); code.extend(bytes(12))
for offset, name in fixes: struct.pack_into('>h', code, offset, labels[name] - offset)
while len(code) % 4: code.append(0)
executable = struct.pack('>8I', 1011, 0, 1, 0, 0, len(code)//4, 1001, len(code)//4) + code + struct.pack('>I', 1010)
assert len(executable) <= 72*512, 'The bounded FFS installer has no file-extension blocks'

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
print(json.dumps({'mode': 'aga-pal-256' if a.mode == 'rgb24' else a.mode, 'displayId': f'{mode:08X}', 'width': width, 'height': height,
                  'proof': proof.decode(), 'hunkSha256': hashlib.sha256(executable).hexdigest(),
                  'sourceSha256': hashlib.sha256(original).hexdigest(), 'imageSha256': hashlib.sha256(image).hexdigest()}, indent=2))
