"""Inspect guest Exec memory headers in an untimed runner snapshot; no ROM bytes included."""
import argparse
import json
from pathlib import Path


def inspect(stem):
    stem = Path(stem)
    state = json.loads(stem.with_suffix('.json').read_text())
    mapping = json.loads(stem.with_suffix('.memory.json').read_text())
    base = mapping['fastRamBase']
    assert base is not None, 'Guest did not configure Fast RAM'
    assert not state['overlay'], 'ROM overlay remains enabled'
    assert state['unsupported'] is None and state['videoUnsupported'] is None
    regions = [(0, stem.with_suffix('.chipram').read_bytes()),
               (0xC00000, stem.with_suffix('.slowram').read_bytes()),
               (base, stem.with_suffix('.fastram').read_bytes())]
    size = mapping['fastRamBytes']
    assert len(regions[-1][1]) == size

    def read(address, count):
        for start, data in regions:
            if start <= address and address + count <= start + len(data):
                return data[address - start:address - start + count]
        raise AssertionError(f'Unmapped guest pointer {address:08X}+{count}')

    def u32(address):
        return int.from_bytes(read(address, 4), 'big')

    # exec/execbase.i: MemList=322. exec/memory.i: MemHeader=32 bytes;
    # attributes=14, first=16, lower=20, upper=24, free=28.
    exec_base = u32(4)
    sentinel = exec_base + 322 + 4
    node = u32(exec_base + 322)
    headers = []
    visited = set()
    while node != sentinel:
        assert node and node not in visited and len(visited) < 16, 'Invalid memory list'
        visited.add(node)
        attributes = int.from_bytes(read(node + 14, 2), 'big')
        lower, upper, free = (u32(node + offset) for offset in (20, 24, 28))
        assert lower < upper and free <= upper - lower
        chunk = u32(node + 16)
        chunks, total = set(), 0
        while chunk:
            assert chunk not in chunks and len(chunks) < 4096, 'Invalid free list'
            chunks.add(chunk)
            following, length = u32(chunk), u32(chunk + 4)
            assert lower <= chunk < upper and length >= 8 and chunk + length <= upper
            total += length
            chunk = following
        assert total == free, 'Free-list bytes disagree with MemHeader'
        headers.append(dict(header=node, attributes=attributes, lower=lower,
                            upper=upper, free=free, chunks=len(chunks)))
        node = u32(node)
    fast = [h for h in headers if base <= h['lower'] < base + size and h['upper'] == base + size]
    assert len(fast) == 1, 'Guest did not register the complete Fast RAM bank'
    assert fast[0]['attributes'] & 7 == 5, 'Expected PUBLIC|FAST, without CHIP'
    assert fast[0]['free'] > 0
    return dict(frame=state['frame'], execBase=exec_base, base=base, bytes=size,
                headers=headers, fastMemoryRegistered=True, freeListsConsistent=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stem', help='Snapshot path without suffix, e.g. frame-001200')
    args = parser.parse_args()
    print(json.dumps(inspect(args.stem), indent=2))
