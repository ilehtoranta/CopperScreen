"""Prepare owned restore inputs: retain Saved, redirect the native probe to After.

No executable instructions, lengths, allocation or FFS metadata are changed.
Original qualified app-exported backups are never replaced.
"""
from pathlib import Path
import argparse, hashlib, json, struct, zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
if any(args.output.iterdir()):
    raise ValueError('Use a fresh output directory.')
record = {}
for layout, expected in [('default', '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368'),
                         ('priority', '10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617')]:
    source = args.source / (layout + '.copperhdf')
    with zipfile.ZipFile(source) as archive:
        original = archive.read('disks/unit-0.hdf')
        assert hashlib.sha256(original).hexdigest().upper() == expected
        image = bytearray(original)
        touched = []
        for part in range(2):
            start = (4 + part * 55) * 32768
            def word(block, offset): return struct.unpack_from('>I', image, start + block * 512 + offset)[0]
            def find(directory, name):
                for bucket in range(72):
                    block = word(directory, 24 + bucket * 4)
                    while block:
                        at = start + block * 512
                        if image[at+433:at+433+image[at+432]].decode('ascii') == name: return block
                        block = word(block, 496)
                raise ValueError('Missing fixture file: ' + name)
            header = find(find(1760, 'C'), 'RdbProbe')
            blocks = [word(header, 308-i*4) for i in range(word(header, 8))]
            program = bytearray(b''.join(image[start+b*512:start+(b+1)*512] for b in blocks))
            for unit in range(2):
                old, new = f'DH{unit}:Data/Saved\0'.encode(), f'DH{unit}:Data/After\0'.encode()
                assert program.count(old) == 1 and len(old) == len(new)
                program = program.replace(old, new)
            for index, block in enumerate(blocks):
                image[start+block*512:start+(block+1)*512] = program[index*512:(index+1)*512]
                touched.append(block)
        assert sum(a != b for a, b in zip(original, image)) == 16
        digest = hashlib.sha256(image).hexdigest().upper()
        manifest = json.loads(archive.read('manifest.json'))
        assert manifest['Images'] == [{'Unit': 0, 'Bytes': len(image), 'Sha256': expected}]
        manifest['Images'][0]['Sha256'] = digest
        destination = args.output / source.name
        with zipfile.ZipFile(destination, 'x', compression=zipfile.ZIP_STORED) as target:
            for entry in archive.infolist():
                payload = image if entry.filename == 'disks/unit-0.hdf' else json.dumps(manifest).encode() if entry.filename == 'manifest.json' else archive.read(entry)
                target.writestr(entry, payload)
        record[layout] = {'sourceBackupSha256': hashlib.sha256(source.read_bytes()).hexdigest().upper(),
                          'sourceHdfSha256': expected, 'preparedHdfSha256': digest,
                          'preparedBackupSha256': hashlib.sha256(destination.read_bytes()).hexdigest().upper(),
                          'changedImageBytes': 16, 'nativeInstructionsChanged': False,
                          'existingSavedFilesRetained': True}
(args.output / 'preparation.json').write_text(json.dumps(record, indent=2)+'\n', encoding='utf-8')
print(json.dumps(record, indent=2))
