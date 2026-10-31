"""Inspect copied frameiclass observations from bounded A500 RAM captures.

With --reference, compare guest object geometry and every captured raster pixel.
Pointer values are process-local and are reported, but are not equality oracles.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def observe(directory, frame):
    prefix = directory / f'frame-{frame:06}'
    chip = prefix.with_suffix('.chipram').read_bytes()
    fast = prefix.with_suffix('.fastram').read_bytes()
    slow = prefix.with_suffix('.slowram').read_bytes()
    state = json.loads(prefix.with_suffix('.json').read_text(encoding='utf-8-sig'))
    if state.get('unsupported') is not None:
        raise ValueError('Unsupported emulation feature: ' + str(state['unsupported']))

    def raw(address, count):
        for start, data in [(0, chip), (0x200000, fast), (0xc00000, slow)]:
            if start <= address and address + count <= start + len(data):
                return data[address - start:address - start + count]
        raise ValueError(f'Unmapped observation address {address:08X}+{count}')

    def read(address):
        return int.from_bytes(raw(address, 4), 'big')

    def name(address):
        # ROM-owned resources may precede the probe. Its copied name is in RAM.
        try:
            return raw(address, 16).split(b'\0')[0]
        except ValueError:
            return b''

    node = read(read(4) + 336)
    seen = set()
    while node and read(node) and node not in seen:
        seen.add(node)
        if name(read(node + 10)) == b'FrameImageProbe':
            break
        node = read(node)
    else:
        raise ValueError('FrameImageProbe resource was not published')
    if read(node + 20) != 0x46524d31 or read(node + 24) != 16:
        raise ValueError('Guest probe did not finish all 16 cases')
    version = read(node + 28)
    if version not in (1, 2):
        raise ValueError('Unknown observation layout')
    header, stride = (64, 96) if version == 1 else (384, 1152)
    cases = []
    for index in range(16):
        address = node + header + index * stride
        case = dict(index=index, frameType=read(address), flags=read(address + 4),
                    objectAddress=read(address + 8), created=read(address + 8) != 0,
                    imagePrefix=raw(address + 12, 20).hex(), frameBoxResult=read(address + 32),
                    contentsBox=list(struct.unpack('>4h', raw(address + 36, 8))),
                    frameBox=list(struct.unpack('>4h', raw(address + 44, 8))))
        if version == 2:
            bits = raw(address + 96, 1024)
            pixels = bytes(sum(((bits[plane * 512 + y * 16 + x // 8] >> (7 - x % 8)) & 1) << plane
                               for plane in range(2)) for y in range(32) for x in range(128))
            case['pixelSha256'] = hashlib.sha256(pixels).hexdigest()
            case['pixelCounts'] = {str(value): pixels.count(value) for value in range(4)}
        cases.append(case)
    return dict(run=str(directory), frame=frame, version=version, resourceAddress=node,
                cases=cases, chipSha256=hashlib.sha256(chip).hexdigest(),
                fastSha256=hashlib.sha256(fast).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('--frame', type=int, default=4000)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    observation = observe(args.run, args.frame)
    failures = []
    if args.reference:
        reference = observe(args.reference, args.frame)
        if reference['version'] != observation['version']:
            raise ValueError('Reference and candidate layouts differ')
        for expected, actual in zip(reference['cases'], observation['cases']):
            for key, value in expected.items():
                if key == 'objectAddress':
                    continue
                if actual[key] != value:
                    failures.append(dict(index=actual['index'], field=key, expected=value, actual=actual[key]))
        observation['comparison'] = dict(reference=str(args.reference), failures=failures,
                                         passed=len(failures) == 0)
    if args.output:
        if args.output.exists():
            raise FileExistsError('Preserve prior observation reports')
        args.output.write_text(json.dumps(observation, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(completed=16, created=sum(case['created'] for case in observation['cases']),
                          comparison=observation.get('comparison'))))
    if failures:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
