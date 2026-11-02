"""Check the original MUImem helper overwritten by the manual-stack failure."""
import argparse
import hashlib
import json
from pathlib import Path
from capture_memory import banks

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--original', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
original = a.original.read_bytes()
original_sha = hashlib.sha256(original).hexdigest()
if original_sha != 'e8d0322f300f865cc7f8f6c473f1255dd9b5f4e3b7b36bdd0cd36f3a914fba02':
    raise ValueError('Not the pinned original MUImem executable')
anchor = bytes.fromhex('2940fdac202cfdac6600000e')
source_anchor = original.find(anchor)
helper = 0x54C4
expected = original[helper:helper+40]
matches = []
memory = banks(a.capture)
for base, data, _ in memory:
    start = 0
    while (offset := data.find(anchor, start)) >= 0:
        start = offset + 2
        address = base + offset + helper - source_anchor
        for bank_base, bank, _ in memory:
            if bank_base <= address and address + len(expected) <= bank_base + len(bank):
                actual = bank[address-bank_base:address-bank_base+len(expected)]
                matches.append(dict(address=hex(address), bytes=actual.hex(), matchesOriginal=actual == expected))
result = dict(protocol='muimem-original-code-guard-v1', capture=str(a.capture),
              originalSha256=original_sha, helperFileOffset=hex(helper), expectedBytes=expected.hex(),
              observations=matches, verdict='PASS_ORIGINAL_HELPER_INTACT' if len(matches) == 1 and matches[0]['matchesOriginal']
              else 'HELPER_OVERWRITTEN' if len(matches) == 1 else 'UNAVAILABLE_CODE_ANCHOR')
a.output.write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(result))
