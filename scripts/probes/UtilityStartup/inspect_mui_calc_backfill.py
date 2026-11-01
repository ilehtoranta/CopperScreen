"""Read-only audit of a closed Calc window's bitmap in diagnostic RAM captures."""
import argparse
import hashlib
import json
from pathlib import Path

from capture_memory import banks


class Memory:
    def __init__(self, path):
        self.regions = banks(path)

    def read(self, address, size):
        for base, data, _ in self.regions:
            if base <= address and address + size <= base + len(data):
                return data[address - base:address - base + size]
        raise ValueError(f"Unmapped capture read: {address:#x}/{size}")

    def u(self, address, size=4):
        return int.from_bytes(self.read(address, size), "big")

    def signed_word(self, address):
        return int.from_bytes(self.read(address, 2), "big", signed=True)


def inspect(arithmetic, settled):
    before = Memory(arithmetic)
    after = Memory(settled)
    state = json.loads(arithmetic.with_suffix('.json').read_text(encoding='utf-8'))['state']
    windows = {obj['NativeWindow'] for obj in state['Objects'] if obj['NativeWindow']}
    if len(windows) != 1:
        raise ValueError(f"Expected one live native Calc window, got {windows}")
    window = windows.pop()
    screen = before.u(window + 46)
    left, top, width, height = [before.signed_word(window + offset) for offset in (4, 6, 8, 10)]
    bitmap = before.u(screen + 88)  # Public Screen.RastPort.BitMap
    stride, rows, depth = before.u(bitmap, 2), before.u(bitmap + 2, 2), before.u(bitmap + 5, 1)
    if not (0 < depth <= 8 and 0 <= left < left + width <= stride * 8
            and 0 <= top < top + height <= rows):
        raise ValueError('Window is outside the captured bitmap')
    if after.read(bitmap, 40) != before.read(bitmap, 40):
        raise ValueError('Screen bitmap changed between observations')
    planes = [before.u(bitmap + 8 + 4 * index) for index in range(depth)]

    def count_nonzero(memory):
        data = [memory.read(plane, stride * rows) for plane in planes]
        return sum(any(plane[y * stride + x // 8] & (0x80 >> (x % 8)) for plane in data)
                   for y in range(top, top + height) for x in range(left, left + width))

    final_state = json.loads(settled.with_suffix('.json').read_text(encoding='utf-8'))['state']
    root = final_state['Root']
    registry = after.u(root)
    counts = dict(classes=after.u(registry + 8), objects=after.u(registry + 12),
                  dispatchDepth=after.u(root + 32), callbackDepth=after.u(root + 36))
    if after.u(registry) != 0x4d554934 or any(counts.values()):
        raise ValueError('MUI registry is invalid or not empty')
    before_count, after_count = count_nonzero(before), count_nonzero(after)
    return dict(schemaVersion=1, arithmeticCapture=str(arithmetic), settledCapture=str(settled),
                captureSha256={str(path): hashlib.sha256(path.read_bytes()).hexdigest()
                               for path in (arithmetic, settled)},
                nativeWindow=hex(window), screen=hex(screen), bitmap=hex(bitmap),
                rectangle=dict(left=left, top=top, width=width, height=height),
                bitmapStride=stride, bitmapRows=rows, bitmapDepth=depth,
                screenFirstWindow=hex(after.u(screen + 4)), muiRegistry=counts,
                nonzeroPixelsBefore=before_count, nonzeroPixelsAfter=after_count,
                backgroundClear='PASS' if before_count > 0 and after_count == 0 else 'FAIL',
                scope='Pen-zero restoration in the closed window rectangle; hardware sprites are separate.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--arithmetic', type=Path, required=True)
    parser.add_argument('--settled', type=Path, required=True)
    parser.add_argument('--expect-cleared', action='store_true')
    args = parser.parse_args()
    result = inspect(args.arithmetic, args.settled)
    print(json.dumps(result, indent=2))
    if args.expect_cleared and result['backgroundClear'] != 'PASS':
        raise SystemExit(1)


if __name__ == '__main__':
    main()
