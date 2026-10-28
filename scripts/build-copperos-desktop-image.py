"""Compose the pinned native CopperOS desktop as an 8 MiB bootable FFS HDF.

Uses the original producer's FFS serializer and independent filesystem reader.
No command proof image or vendor OS files are used. Outputs stay local; this
does not publish a package or qualify other ROM/machine combinations.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--copperos-root', type=Path, required=True)
    parser.add_argument('--desktop-directory', type=Path, required=True)
    parser.add_argument('--output-directory', type=Path, required=True)
    args = parser.parse_args()
    contract_path = Path(__file__).resolve().parents[1] / 'CopperScreen/Firmware/CopperOsA1200.json'
    contract = json.loads(contract_path.read_text(encoding='utf-8-sig'))
    builder = args.copperos_root / 'tools/ImageBuilder/build_system_image.py'
    if digest(builder.read_bytes()) != 'c1cf4f4c7b604e87fca604913e647392329684c919043a38726933dcc7e7709a':
        raise ValueError('Use the pinned CopperOS FFS image producer')
    payloads = {}
    for item in contract['Files']:
        data = (args.desktop_directory / item['Path']).read_bytes()
        if len(data) != item['Bytes'] or digest(data) != item['Sha256'] or data[:4] != b'\0\0\x03\xf3':
            raise ValueError('Unqualified desktop input: ' + item['Path'])
        payloads[item['Path']] = data
    spec = importlib.util.spec_from_file_location('copperos_ffs_producer', builder)
    producer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(producer)
    disk = producer.Disk(16384)
    dirs = {name: disk.directory(disk.root, name) for name in ['C', 'L', 'S']}
    for name, data in payloads.items():
        disk.file(dirs[name[0]], name, data)
    disk.file(dirs['S'], 'S/Startup-Sequence', b'; CopperOS native desktop\nC:Desktop\n')
    disk.file(disk.root, 'ReadMe', b'CopperOS Desktop\nOpen Files or Editor from Home.\n')
    image = disk.finish()
    if len(image) != contract['Bytes'] or digest(image) != contract['Sha256']:
        raise ValueError('Desktop image does not reproduce the pinned identity')
    verification = producer.verify(image, disk.files)
    # Never overwrite an earlier image or receipt.
    args.output_directory.mkdir(parents=True, exist_ok=False)
    (args.output_directory / 'CopperOS.hdf').write_bytes(image)
    receipt = dict(seed=contract, files=disk.files, verification=verification,
                   producerSha256=digest(builder.read_bytes()), recipeSha256=digest(Path(__file__).read_bytes()),
                   runtimeQualification='See docs/COPPEROS_DESKTOP_A1200.md')
    (args.output_directory / 'desktop-image.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print('Created CopperOS desktop HDF: ' + digest(image))


if __name__ == '__main__':
    main()
