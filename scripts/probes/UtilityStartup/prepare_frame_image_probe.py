"""Prepare a public BOOPSI frameiclass geometry probe on a private boot HDF.

The guest publishes copied observations as a named Exec resource. No host calls
or ROM patches participate in the probe. Media and generated commands stay local.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


class Assembler:
    def __init__(self):
        self.code = bytearray()
        self.labels = {}
        self.patches = []

    def words(self, *words):
        self.code += struct.pack('>' + 'H' * len(words), *(v & 65535 for v in words))

    def label(self, label):
        self.labels[label] = len(self.code)

    def branch(self, opcode, label):
        self.words(opcode)
        self.patches.append((len(self.code), label))
        self.words(0)

    def address(self, register, label):
        self.branch(0x41fa + (register << 9), label)

    def immediate(self, register, value):
        self.words(0x203c + (register << 9), value >> 16, value)

    def finish(self):
        for offset, label in self.patches:
            struct.pack_into('>h', self.code, offset, self.labels[label] - offset)
        self.code += bytes(-len(self.code) % 4)
        count = len(self.code) // 4
        return struct.pack('>8I', 1011, 0, 1, 0, 0, count, 1001, count) + self.code + struct.pack('>I', 1010)


def command(draw=False, filled=False, null_draw_info=False, odd=False):
    a = Assembler()
    def call(lvo):
        a.words(0x4eae, lvo)
    def store(offset, value):
        a.words(0x2b7c, value >> 16, value, offset)

    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.address(1, 'intuition'); a.words(0x7000); call(-552)
    a.words(0x2840, 0x4a80); a.branch(0x6700, 'done')
    if draw:
        a.address(1, 'graphics'); a.words(0x7000); call(-552)
        a.words(0x2e00, 0x4a80); a.branch(0x6700, 'close')
    a.immediate(0, 65536 if draw else 2048); a.immediate(1, 0x10001); call(-198)
    a.words(0x2c00, 0x2a40, 0x4a80); a.branch(0x6700, 'close_graphics' if draw else 'close')
    a.words(0x1b7c, 8, 8, 0x43ed, 32, 0x2b49, 10)
    a.address(0, 'resource'); a.words(0x7010); call(-624)
    store(24, 16); store(28, 2 if draw else 1)
    a.words(0x224d); call(-486)  # AddResource, reset-lived copied observations.
    if draw:
        a.immediate(0, 1024); a.immediate(1, 0x10003); call(-198)
        a.words(0x2b40, 16, 0x4a80); a.branch(0x6700, 'close_graphics')
        a.words(0x2c47, 0x43ed, 64); call(-198)  # InitRastPort(A1).
        a.words(0x41ed, 176, 0x7002); a.immediate(1, 128); a.immediate(2, 32); call(-390)
        a.words(0x41ed, 176, 0x2b48, 68, 0x202d, 16, 0x2b40, 184)
        a.words(0x0680, 0, 512, 0x2b40, 188)
        store(224, 0x0002000c)
        a.words(0x41ed, 256, 0x2b48, 228)
        store(236, 0x00020016); store(240, 0x00160000)
        for offset, value in enumerate([0x00010000, 0x00010002, 0x00010003, 0x00010000, 0x00020001, 0x00000001]):
            store(256 + offset * 4, value)
    a.words(0x4bed, 384 if draw else 64)
    cases = []
    for frame_type in range(4):
        for recessed in range(2):
            for specify in range(2):
                index = len(cases)
                cases.append(dict(frameType=frame_type, recessed=recessed, specify=specify))
                store(0, frame_type); store(4, recessed | (specify << 1) | (4 if filled else 0))
                a.words(0x2c4c, 0x91c8); a.address(1, 'class'); a.address(2, f'tags{index}')
                call(-636)  # NewObjectA(NULL, "frameiclass", tags).
                a.words(0x2640, 0x2b40, 8, 0x4a80); a.branch(0x6700, f'next{index}')
                a.words(0x204b, 0x43ed, 12, 0x7014, 0x2c78, 4); call(-624)
                store(36, 0x000a0014); store(40, 0x0051000b if odd else 0x0050000a)
                store(44, 0x006400c8); store(48, 0x00780028)
                store(64, 0x207)
                a.words(0x41ed, 36, 0x2b48, 68, 0x41ed, 44, 0x2b48, 72)
                store(76, 0); store(80, specify)
                # DoMethodA: Hook A0, object A2, method message A1.
                a.words(0x206b, 0xfffc, 0x244b, 0x43ed, 64, 0x2c68, 8, 0x4e96, 0x2b40, 32)
                if draw:
                    a.words(0x2046, 0x43e8, 64, 0x7003, 0x2c47); call(-234)
                    store(64, 0x206 if specify else 0x202)
                    a.words(0x2046, 0x41e8, 64, 0x2b48, 68)
                    store(72, 0x00100008); store(76, specify)
                    if null_draw_info:
                        store(80, 0)
                    else:
                        a.words(0x2046, 0x41e8, 224, 0x2b48, 80)
                    store(84, 0x00500010)
                    a.words(0x206b, 0xfffc, 0x244b, 0x43ed, 64, 0x2c68, 8, 0x4e96)
                    a.words(0x2c47); call(-228)  # WaitBlit before reading the planar result.
                    a.words(0x2046, 0x2068, 16, 0x43ed, 96)
                    a.immediate(0, 1024); a.words(0x2c78, 4); call(-624)
                a.words(0x204b, 0x2c4c); call(-642)
                a.label(f'next{index}'); a.words(0x4bed, 1152 if draw else 96)
    a.words(0x2046, 0x217c, 0x4652, 0x4d31, 20)
    if draw:
        a.words(0x2268, 16); a.immediate(0, 1024); a.words(0x2c78, 4); call(-210)
        a.label('close_graphics'); a.words(0x2247, 0x2c78, 4); call(-414)
    a.label('close'); a.words(0x224c, 0x2c78, 4); call(-414)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    for label, text in [('intuition', 'intuition.library'), ('resource', 'FrameImageProbe'), ('class', 'frameiclass'), ('graphics', 'graphics.library')]:
        if len(a.code) & 1:
            a.code.append(0)
        a.label(label); a.code += text.encode('ascii') + b'\0'
    if len(a.code) & 1:
        a.code.append(0)
    for index, case in enumerate(cases):
        a.label(f'tags{index}')
        for tag, value in [(1, -6), (2, -3), (3, 100), (4, 20), (0x1b, case['frameType']),
                           (0x15, case['recessed']), (0x17, 0 if filled else 1)]:
            a.code += struct.pack('>II', 0x80020000 + tag, value & 0xffffffff)
        a.code += bytes(8)
    return a.finish(), cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-hdf', type=Path, required=True)
    parser.add_argument('--output-hdf', type=Path, required=True)
    parser.add_argument('--native', action='store_true')
    parser.add_argument('--draw', action='store_true', help='Copy raw 128x32 two-plane frame renders for native comparison')
    parser.add_argument('--filled', action='store_true', help='Use filled frames instead of IA_EdgesOnly')
    parser.add_argument('--null-draw-info', action='store_true')
    parser.add_argument('--odd', action='store_true', help='Use odd content dimensions to measure centering rounding')
    args = parser.parse_args()
    if args.output_hdf.exists() or Path(str(args.output_hdf) + '.json').exists():
        raise FileExistsError('Preserve prior probe fixtures')
    sha = lambda data: hashlib.sha256(data).hexdigest()
    base = json.loads(Path(str(args.base_hdf) + '.json').read_text(encoding='utf-8-sig'))
    data = args.base_hdf.read_bytes()
    if sha(data) != base['sha256']:
        raise ValueError('Base receipt mismatch')
    args.output_hdf.write_bytes(data)
    image, cases = command(args.draw, args.filled, args.null_draw_info, args.odd)
    startup = b'C:FrameImageProbe\n' if args.native else b'C:CopperFfp\nC:CopperGui\nC:FrameImageProbe\n'
    device = BlkDevFactory().open(str(args.output_hdf), read_only=False)
    volume = ADFSVolume(device); volume.open()
    files = {item['path'].casefold(): dict(item) for item in base['files']}
    try:
        for item in files.values():
            if sha(bytes(volume.read_file(FSString(item['path'])))) != item['sha256']:
                raise ValueError('Base file mismatch: ' + item['path'])
        for path, content in [('C/FrameImageProbe', image), ('S/Startup-Sequence', startup)]:
            if volume.get_path_name(FSString(path)) is not None:
                volume.delete(FSString(path))
            volume.write_file(content, FSString(path))
            files[path.casefold()] = dict(path=path, bytes=len(content), sha256=sha(content), origin='public BOOPSI frame geometry probe')
        for item in files.values():
            if sha(bytes(volume.read_file(FSString(item['path'])))) != item['sha256']:
                raise ValueError('Final file mismatch: ' + item['path'])
    finally:
        volume.close(); device.close()
    receipt = dict(base)
    receipt.update(kind='public-frameiclass-geometry-probe', baseSha256=base['sha256'],
                   sha256=sha(args.output_hdf.read_bytes()), startup=startup.decode(), files=list(files.values()),
                   commandSha256=sha(image), cases=cases, resource='FrameImageProbe',
                   layout=dict(headerBytes=384 if args.draw else 64, recordBytes=1152 if args.draw else 96,
                               version=2 if args.draw else 1, imageOffset=12, contentsOffset=36, frameOffset=44,
                               pixelOffset=96 if args.draw else None, pixelBytes=1024 if args.draw else 0,
                               filled=args.filled,
                               nullDrawInfo=args.null_draw_info,
                               oddContents=args.odd,
                               draw='state=specify; IM_DRAW100x20 or IM_DRAWFRAME80x16; offset16,8; 128x32 bitmap,2planes; controlled DrawInfo pens' if args.draw else None))
    Path(str(args.output_hdf) + '.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(sha256=receipt['sha256'], commandSha256=sha(image), cases=len(cases))))


if __name__ == '__main__':
    main()
