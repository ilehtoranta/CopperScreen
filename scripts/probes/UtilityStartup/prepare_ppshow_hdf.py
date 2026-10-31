"""Extend a qualified local system test image with the complete PPShow archive.

Requires amitools and lhafile. Images contain privately supplied system files;
keep them local. Preparation and byte readback are not an execution verdict.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import struct
import zipfile

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def control_ilbm():
    def chunk(kind, data):
        return kind + struct.pack('>I', len(data)) + data + (b'\0' if len(data) & 1 else b'')
    header = struct.pack('>HHhhBBBBHBBhh', 320, 200, 0, 0, 3, 0, 0, 0, 0, 10, 10, 320, 200)
    colors = bytes([0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255,
                    255, 255, 0, 255, 0, 255, 0, 255, 255, 255, 255, 255])
    body = bytearray()
    for y in range(200):
        for plane in range(3):
            for column in range(40):
                value = 0
                for bit in range(8):
                    x = column * 8 + bit
                    color = 7 if x < 8 or x >= 312 or y < 8 or y >= 192 else x // 40
                    value |= ((color >> plane) & 1) << (7 - bit)
                body.append(value)
    payload = b'ILBM' + chunk(b'BMHD', header) + chunk(b'CMAP', colors) + chunk(b'BODY', body)
    return b'FORM' + struct.pack('>I', len(payload)) + payload


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-hdf', type=Path, required=True)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--workbench', type=Path,
                        help='Privately supplied Workbench 2.0+ ADF/ZIP providing Libs/iffparse.library')
    parser.add_argument('--native-env', action='store_true',
                        help='Native-ROM diagnostic: create ENV and run the retained Workbench Assign command')
    parser.add_argument('--gui-loader', type=Path,
                        help='Canonical CopperGui loader with a matching .json receipt for this base provider')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Preserve earlier images')
    base = json.loads(Path(str(args.base_hdf) + '.json').read_text(encoding='utf-8-sig'))
    data = args.base_hdf.read_bytes()
    if sha(data) != base['sha256']:
        raise ValueError('Base image receipt mismatch')
    archive_hash = sha(args.archive.read_bytes())
    if archive_hash != '66383bf59b4842f140f86455a20c535c4ebf2d9996138218b06b7a7cc205bdf3':
        raise ValueError('Unexpected PPShow archive')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as output:
        output.write(data)
    device = BlkDevFactory().open(str(args.output), read_only=False)
    volume = ADFSVolume(device)
    volume.open()
    files = {entry['path'].casefold(): dict(entry) for entry in base['files']}
    directories = set()
    for entry in files.values():
        parent = PurePosixPath(entry['path']).parent
        while str(parent) != '.':
            directories.add(str(parent).casefold())
            parent = parent.parent
    changed = []
    try:
        for entry in files.values():
            current = bytes(volume.read_file(FSString(entry['path'])))
            if len(current) != entry['bytes'] or sha(current) != entry['sha256']:
                raise ValueError('Base guest file mismatch: ' + entry['path'])

        def add(path, content, origin):
            parts = PurePosixPath(path).parts
            if ':' in path or path.startswith('/') or any(p in ('.', '..') for p in parts):
                raise ValueError('Unsafe guest path')
            for count in range(1, len(parts)):
                directory = '/'.join(parts[:count])
                if directory.casefold() not in directories:
                    volume.create_dir(FSString(directory))
                    directories.add(directory.casefold())
            previous = files.get(path.casefold())
            if previous:
                volume.delete(FSString(previous['path']))
            volume.write_file(content, FSString(path))
            if bytes(volume.read_file(FSString(path))) != content:
                raise ValueError('Guest byte readback mismatch: ' + path)
            files[path.casefold()] = {'path': path, 'bytes': len(content), 'sha256': sha(content), 'origin': origin}
            changed.append(path)

        archive = lhafile.Lhafile(str(args.archive))
        for entry in archive.infolist():
            path = entry.filename.replace('\\', '/')
            if path.endswith('/'):
                continue
            content = archive.read(entry.filename)
            add(path, content, 'complete original PPShow archive: ' + entry.filename)
            if path.startswith('PPShow/libs/'):
                add('Libs/' + path.rsplit('/', 1)[1], content, 'bundled PPShow prerequisite: ' + entry.filename)
        add('fixtures/control.ilbm', control_ilbm(), 'original deterministic 320x200 three-plane color bars with white border')
        gui_loader = None
        if args.gui_loader:
            gui_loader = json.loads(Path(str(args.gui_loader) + '.json').read_text(encoding='utf-8-sig'))
            loader = args.gui_loader.read_bytes()
            if (gui_loader['kind'] != 'canonical-CopperGui-native-loader'
                    or gui_loader['sha256'].lower() != sha(loader)
                    or gui_loader['bytes'] != len(loader)
                    or gui_loader['classicEntry'] != base['provider']['classicEntry']
                    or gui_loader['providerSha256'].lower() != base['provider']['sha256'].lower()):
                raise ValueError('Canonical GUI loader/provider receipt mismatch')
            add('C/CopperGui', loader, 'canonical native GUI loader receipt; missing-provider creation enabled')
        workbench = None
        if args.workbench:
            source = args.workbench.read_bytes()
            if zipfile.is_zipfile(args.workbench):
                with zipfile.ZipFile(args.workbench) as bundle:
                    names = [n for n in bundle.namelist() if n.lower().endswith('.adf')]
                    if len(names) != 1:
                        raise ValueError('Workbench ZIP must contain exactly one ADF')
                    source = bundle.read(names[0])
            wb_device = BlkDevFactory().open('workbench.adf', read_only=True, fobj=io.BytesIO(source))
            wb_volume = ADFSVolume(wb_device)
            wb_volume.open()
            try:
                library = bytes(wb_volume.read_file(FSString('Libs/iffparse.library')))
                add('Libs/iffparse.library', library, 'private Workbench OS 2.0+ dependency')
                workbench = {'sourceSha256': sha(args.workbench.read_bytes()),
                             'adfSha256': sha(source), 'iffparseSha256': sha(library)}
            finally:
                wb_volume.close()
                wb_device.close()
        startup = b'C:CopperGui\nC:CopperFfp\nSYS:PPShow/PPShow SYS:fixtures/control.ilbm\n'
        if args.native_env:
            if 'env' not in directories:
                volume.create_dir(FSString('ENV'))
                directories.add('env')
            startup = b'C:Assign ENV: SYS:ENV\n' + startup
        add('S/Startup-Sequence', startup, 'controlled direct PPShow invocation')
        for entry in files.values():
            content = bytes(volume.read_file(FSString(entry['path'])))
            if len(content) != entry['bytes'] or sha(content) != entry['sha256']:
                raise ValueError('Final guest file mismatch: ' + entry['path'])
    finally:
        volume.close()
        device.close()
    receipt = {'kind': 'controlled-ppshow-installation', 'filesystem': base['filesystem'],
               'baseSha256': base['sha256'], 'archiveSha256': archive_hash,
               'sha256': sha(args.output.read_bytes()), 'startup': startup.decode(),
               'provider': base['provider'], 'shell': base['shell'], 'mathFfp': base['mathFfp'],
               'workbenchDependency': workbench,
               'guiLoader': gui_loader,
               'changedPaths': changed, 'files': sorted(files.values(), key=lambda f: f['path']),
               'scope': 'Private preparation and complete byte readback only. PPShow requires OS 2.0+; native 1.3 is not a positive reference.'}
    Path(str(args.output) + '.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({'sha256': receipt['sha256'], 'files': len(files)}))


if __name__ == '__main__':
    main()
