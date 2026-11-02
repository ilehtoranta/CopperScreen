"""Read completed extended runner captures using their recorded RAM mapping."""
import json
from pathlib import Path


def banks(path):
    path = Path(path)
    metadata = json.loads(path.with_suffix('.memory.json').read_text(encoding='utf-8'))
    fast = path.read_bytes()
    if len(fast) != metadata['fastRamBytes']:
        raise ValueError(f'{path}: incomplete Fast RAM dump')
    result = []
    if metadata['fastRamBase'] is not None:
        result.append((metadata['fastRamBase'], fast, path))
    for base, suffix in ((0, '.chipram'), (0xc00000, '.slowram')):
        bank_path = path.with_suffix(suffix)
        if bank_path.exists():
            result.append((base, bank_path.read_bytes(), bank_path))
    return result


def exec_resource_nodes(memory):
    """Walk actual public Exec resources; copied cookies elsewhere are not nodes.

    Check both links and the list sentinel before returning any evidence.
    Completed captures include physical RAM, not arbitrary ROM string storage.
    """
    def read(address, length):
        for base, data, _ in memory:
            if base <= address and address+length <= base+len(data):
                return data[address-base:address-base+length]
        raise ValueError(f'Unmapped Exec resource-list record: {address:x}')
    def u32(address): return int.from_bytes(read(address,4),'big')
    header=u32(4)+336  # SDK ExecBase.ResourceList
    if u32(header+4)!=0: raise ValueError('Invalid Exec ResourceList sentinel')
    current=u32(header); previous=header; result=[]; seen=set()
    while current!=header+4:
        if not current or current in seen or len(result)>=4096:
            raise ValueError('Invalid/cyclic Exec ResourceList')
        if u32(current+4)!=previous:
            raise ValueError('Broken Exec ResourceList backlink')
        read(current,14);seen.add(current);result.append(current)
        previous=current;current=u32(current)
    if u32(header+8)!=previous:
        raise ValueError('Broken Exec ResourceList tail')
    return result
