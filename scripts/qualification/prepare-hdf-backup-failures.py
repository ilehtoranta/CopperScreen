"""Create bounded invalid copies of a qualified app-exported HDF backup."""
from pathlib import Path
import argparse, copy, hashlib, json, zipfile
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
if any(args.output.iterdir()): raise ValueError('Use a fresh output directory.')
source = args.source.read_bytes()
with zipfile.ZipFile(args.source) as archive:
    original = archive.read('disks/unit-0.hdf')
    assert hashlib.sha256(original).hexdigest().upper() == '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368'
    for case in ['corrupt-image', 'corrupt-profile', 'truncated-image']:
        with zipfile.ZipFile(args.output/(case+'.copperhdf'), 'x') as output:
            for entry in archive.infolist():
                payload = bytearray(archive.read(entry))
                if case == 'corrupt-image' and entry.filename == 'disks/unit-0.hdf': payload[131072+1760*512+100] ^= 1
                if case == 'corrupt-profile' and entry.filename == 'profile.json': payload[0] ^= 1
                if case == 'truncated-image' and entry.filename == 'disks/unit-0.hdf': payload = payload[:len(payload)//2]
                output.writestr(copy.copy(entry), payload)  # Recompute ZIP CRC, retain manifest hashes/lengths.
(args.output/'truncated-archive.copperhdf').write_bytes(source[:-32])
(args.output/'existing-backup.copperhdf').write_bytes(source)
record = {'sourcePath':str(args.source.resolve()),'sourceSha256':hashlib.sha256(source).hexdigest().upper(),
          'inputs':{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in args.output.iterdir()}}
(args.output/'preparation.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
print(json.dumps(record,indent=2))
