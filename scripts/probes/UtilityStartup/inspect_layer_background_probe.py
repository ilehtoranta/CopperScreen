"""Validate the completed native window-close/background regression receipt."""
import argparse
import hashlib
import json
import struct
from capture_memory import banks


def inspect(path, suppress=False):
    matches = []
    for base, data, bank_path in banks(path):
        start = 0
        while True:
            name = data.find(b'LayerBackgroundProbe\0', start)
            if name < 0:
                break
            start = name + 1
            record = name - 400
            if record < 0 or data[record + 8] != 8:
                continue
            if struct.unpack_from('>I', data, record + 10)[0] != base + name:
                continue
            u = lambda offset: struct.unpack_from('>I', data, record + offset)[0]
            result = dict(record=hex(base + record), screen=hex(u(20)), window=hex(u(24)),
                          beforePixel=u(28), afterPixel=u(32), firstWindow=hex(u(36)),
                          completeMagic=hex(u(40)), phase=u(44), suppress=bool(u(48)))
            valid = (u(40) == 0x42474631 and u(44) == 4 and u(20) and u(24)
                     and u(28) == 1 and u(32) == int(suppress) and u(36) == 0
                     and u(48) == int(suppress))
            result['status'] = 'PASS' if valid else 'FAIL'
            result['bankSha256'] = hashlib.sha256(bank_path.read_bytes()).hexdigest()
            if u(64) == 0x4c4e4154:
                result['nativeProvider'] = dict(lastLvo=hex(u(64 + 64) >> 16),
                                                lastResult=u(64 + 68),
                                                snapshotLedgerCount=u(64 + 172))
            matches.append(result)
    if len(matches) != 1:
        raise ValueError(f'{path}: expected one completed probe record, got {matches}')
    return matches[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture')
    parser.add_argument('--suppress', action='store_true')
    args = parser.parse_args()
    result = inspect(args.capture, args.suppress)
    print(json.dumps(result, indent=2))
    if result['status'] != 'PASS':
        raise SystemExit(1)


if __name__ == '__main__':
    main()
