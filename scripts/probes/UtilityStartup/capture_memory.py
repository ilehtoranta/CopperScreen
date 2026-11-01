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
