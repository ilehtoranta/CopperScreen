"""Extract a private relocatable GadTools dependency from the pinned local ROM.

Requires amitools and its bundled Remus split data. Keep the output private.
This prepares a guest dependency; it does not establish API compatibility.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct

from amitools.rom.romsplitter import RomSplitter
from amitools.binfmt.hunk.BinFmtHunk import BinFmtHunk


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rom', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    sidecar = Path(str(args.output) + '.json')
    if args.output.exists() or sidecar.exists():
        raise FileExistsError('Preserve previous modules and receipts')
    source_sha = sha(args.rom.read_bytes())
    if source_sha != '8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee':
        raise ValueError('This diagnostic recipe requires the pinned privately supplied A500 Kickstart 3.1 ROM')
    splitter = RomSplitter()
    if splitter.find_rom(str(args.rom)) is None:
        raise ValueError('ROM not found in installed split database')
    entries = splitter.query_entries('gadtools.library_40.4')
    if len(entries) != 1:
        raise ValueError('Expected one GadTools 40.4 module')
    entry = entries[0]
    address = splitter.remus_rom.base_addr + entry.offset
    for offset in entry.extra.relocs:
        target = struct.unpack_from('>I', splitter.rom_data, entry.offset + offset)[0]
        if not address <= target <= address + entry.size:
            raise ValueError('Module relocation targets another ROM component')
    image = splitter.extract_bin_img(entry, fixes=True, patches=False)
    output = io.BytesIO()
    BinFmtHunk().save_image_fobj(output, image)
    data = output.getvalue()
    if sha(data) != '93a1b6ad5737665f43606cd13269df8f03921f1b3a96a15aa1e623e34b67f590':
        raise ValueError('Output differs from the pinned relocation recipe; inspect the tool/database version')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as file:
        file.write(data)
    receipt = {'kind': 'private-kickstart-library-module', 'name': 'gadtools.library',
               'moduleName': entry.name, 'bytes': len(data), 'sha256': sha(data),
               'sourceRomSha256': source_sha, 'romMatch': splitter.remus_rom.name,
               'moduleOffset': entry.offset, 'moduleBytes': entry.size,
               'relocations': entry.extra.relocs, 'fixes': entry.extra.fixes,
               'patchesApplied': False,
               'scope': 'Private relocated guest library dependency; not runtime qualification',
               'tool': 'amitools RomSplitter/BinFmtHunk with bundled Remus splitdata',
               'toolSource': 'https://github.com/cnvogelg/amitools/blob/main/amitools/rom/romsplitter.py'}
    sidecar.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'sha256': receipt['sha256'], 'bytes': len(data)}))


if __name__ == '__main__':
    main()
