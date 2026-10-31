"""Validate native List methods, Cycle construction and balanced repeated allocation."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def inspect(path, base=0x200000):
    data = path.read_bytes()
    matches = []
    start = 0
    while True:
        name = data.find(b'MuiBuiltinProbe\0', start)
        if name < 0:
            break
        start = name + 1
        record = name - 224
        if (record < 0 or data[record + 8] != 8
                or struct.unpack_from('>I', data, record + 10)[0] != base + name):
            continue
        fields = list(struct.unpack_from('>28I', data, record + 16))
        if fields[0] not in (0x4d554232, 0x4d554233) or fields[1] == 0:
            raise ValueError(f'{path}: incomplete probe: {fields}')
        if fields[2] == 0 or fields[2] != fields[3]:
            raise ValueError(f'{path}: repeated cycle did not recover available memory: {fields[2:4]}')
        cycles = []
        for index in range(2):
            values = fields[4 + index * 8:12 + index * 8]
            cls, obj, inserted, status, entries, cleared, after_status, after_entries = values
            if not cls or not obj or not fields[20 + index] or status != 1 or entries != 1 or after_status != 1 or after_entries != 0:
                raise ValueError(f'{path}: List cycle {index + 1} failed: {values}')
            if fields[0] == 0x4d554233 and (not fields[22 + index] or fields[24 + index] != 0 or fields[26 + index] != 1):
                raise ValueError(f'{path}: Cycle.mui cycle {index + 1} failed: {fields[22:28]}')
            cycles.append({'class': cls, 'object': obj, 'insertReturn': inserted,
                           'entriesAfterInsert': entries, 'clearReturn': cleared,
                           'entriesAfterClear': after_entries, 'listview': fields[20 + index]})
            if fields[0] == 0x4d554233:
                cycles[-1].update(cycleObject=fields[22 + index], cycleActive=fields[24 + index])
        matches.append({'result': 'PASS', 'protocol': 'MUB3' if fields[0] == 0x4d554233 else 'MUB2',
                        'resourceAddress': record + base,
                        'libraryBase': fields[1], 'availableAfterCycles': fields[2:4],
                        'cycles': cycles})
    if len(matches) != 1:
        raise ValueError(f'{path}: expected one completed native built-in probe')
    return {'capture': str(path), 'sha256': hashlib.sha256(data).hexdigest(), **matches[0]}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('capture', type=Path, nargs='+')
    p.add_argument('--fast-base', type=lambda text: int(text, 0), default=0x200000)
    args = p.parse_args()
    print(json.dumps([inspect(path, args.fast_base) for path in args.capture], indent=2))


if __name__ == '__main__':
    main()
