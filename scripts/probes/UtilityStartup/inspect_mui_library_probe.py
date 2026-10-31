"""Check MuiLibraryProbe's copied Exec resource in an extended Fast RAM capture."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def inspect(path, base):
    data = path.read_bytes()
    matches = []
    start = 0
    while True:
        name = data.find(b'MuiLibraryProbe\0', start)
        if name < 0:
            break
        start = name + 1
        record = name - 96
        if (record < 0 or data[record + 8] != 8
                or struct.unpack_from('>I', data, record + 10)[0] != base + name):
            continue
        values = list(struct.unpack_from('>13I', data, record + 16))
        library = values[1]
        expected = [0x4d554931, library, 4, 1, 0, 1, library, 2, library, 3, 2, 1, 0]
        if not library or values != expected:
            raise ValueError(f'{path}: incomplete or failed native lifecycle: {values}')
        matches.append({'resourceAddress': base + record, 'libraryBase': library,
                        'version': values[2], 'rejectedVersion': 5, 'rejectedResult': values[4],
                        'openCounts': [values[i] for i in (3, 5, 7, 9, 10, 11, 12)],
                        'directOpenD0': library, 'directOpenResult': values[8]})
    if len(matches) != 1:
        raise ValueError(f'{path}: expected one completed MuiLibraryProbe resource')
    return {'capture': str(path), 'captureSha256': hashlib.sha256(data).hexdigest(),
            'fastRamBase': base, 'result': 'PASS', **matches[0]}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('capture', type=Path, nargs='+')
    p.add_argument('--fast-base', type=lambda s: int(s, 0), default=0x200000)
    args = p.parse_args()
    print(json.dumps([inspect(path, args.fast_base) for path in args.capture], indent=2))


if __name__ == '__main__':
    main()
