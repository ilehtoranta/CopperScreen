"""Install the complete pinned MuchMore 4.6 package on a verified local HDF.

Requires amitools/lhafile. Keep privately supplied system files and media local.
Image preparation and complete file readback are not a runtime verdict.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-hdf', type=Path, required=True)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--shell-root', type=Path, help='Native Shell directory with matching shell-manifest.json')
    parser.add_argument('--native-env', action='store_true',
                        help='Explicit native-ROM diagnostic: retain Workbench Assign ENV setup')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Preserve earlier images')
    base = json.loads(Path(str(args.base_hdf) + '.json').read_text(encoding='utf-8-sig'))
    image = args.base_hdf.read_bytes()
    if sha(image) != base['sha256']:
        raise ValueError('Base image receipt mismatch')
    archive_sha = sha(args.archive.read_bytes())
    if archive_sha != '12062ff0e0f1d0cecbd36c94f8205c29e9111ec11d9d429f8b481b66e96f52b7':
        raise ValueError('Unexpected MuchMore 4.6 archive')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as output:
        output.write(image)
    device = BlkDevFactory().open(str(args.output), read_only=False)
    volume = ADFSVolume(device)
    volume.open()
    files = {f['path'].casefold(): dict(f) for f in base['files']}
    changed = []
    try:
        for entry in files.values():
            content = bytes(volume.read_file(FSString(entry['path'])))
            if len(content) != entry['bytes'] or sha(content) != entry['sha256']:
                raise ValueError('Base file mismatch: ' + entry['path'])

        def add(path, content, origin):
            parts = PurePosixPath(path).parts
            if ':' in path or path.startswith('/') or any(p in ('.', '..') for p in parts):
                raise ValueError('Unsafe guest path')
            for count in range(1, len(parts)):
                directory = '/'.join(parts[:count])
                if volume.get_path_name(FSString(directory)) is None:
                    volume.create_dir(FSString(directory))
            previous = files.get(path.casefold())
            if previous:
                volume.delete(FSString(previous['path']))
            volume.write_file(content, FSString(path))
            if bytes(volume.read_file(FSString(path))) != content:
                raise ValueError('Guest byte readback mismatch: ' + path)
            files[path.casefold()] = {'path': path, 'bytes': len(content),
                                      'sha256': sha(content), 'origin': origin}
            changed.append(path)

        archive = lhafile.Lhafile(str(args.archive))
        for entry in archive.infolist():
            path = entry.filename.replace('\\', '/')
            if not path.endswith('/'):
                add(path, archive.read(entry.filename), 'complete original MuchMore 4.6 archive: ' + path)
        shell = base['shell']
        if args.shell_root:
            shell = json.loads((args.shell_root / 'shell-manifest.json').read_text(encoding='utf-8-sig'))
            payload = (args.shell_root / 'CopperShell').read_bytes()
            if (shell['sha256'].lower() != sha(payload)
                    or shell['dosClient']['serviceVersion'] != 2
                    or not shell['codeSizeOptimizations']):
                raise ValueError('Native Shell receipt mismatch')
            add('L/CopperShell', payload, 'native Shell with matching compiler-input receipt')
        fixture = ('CopperScreen MuchMore 4.6 plain-text control.\n'
                   'Expected reader: numbered lines, 120 lines total.\n\n'
                   + ''.join(f'{i:03d}  CopperStart and native Kickstart text-reader control.\n' for i in range(1, 121)))
        add('fixtures/muchmore/control.txt', fixture.encode('ascii'), 'authored deterministic plain-text fixture')
        startup = b'C:CopperGui\nC:CopperFfp\nSYS:muchmore46/MuchMore SYS:fixtures/muchmore/control.txt\n'
        if args.native_env:
            if volume.get_path_name(FSString('ENV')) is None:
                volume.create_dir(FSString('ENV'))
            startup = b'C:Assign ENV: SYS:ENV\n' + startup
        add('S/Startup-Sequence', startup, 'controlled direct MuchMore invocation')
        for entry in files.values():
            content = bytes(volume.read_file(FSString(entry['path'])))
            if len(content) != entry['bytes'] or sha(content) != entry['sha256']:
                raise ValueError('Final file mismatch: ' + entry['path'])
    finally:
        volume.close()
        device.close()
    receipt = {'kind': 'controlled-muchmore-installation', 'filesystem': base['filesystem'],
               'baseSha256': base['sha256'], 'archiveSha256': archive_sha,
               'sha256': sha(args.output.read_bytes()), 'startup': startup.decode(),
               'provider': base['provider'], 'guiLoader': base.get('guiLoader'),
               'shell': shell, 'mathFfp': base['mathFfp'],
               'changedPaths': changed, 'files': sorted(files.values(), key=lambda f: f['path']),
               'scope': 'Private complete-package preparation and file readback; not runtime validation. Native ENV setup is an explicit diagnostic difference.'}
    Path(str(args.output) + '.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'sha256': receipt['sha256'], 'files': len(files)}))


if __name__ == '__main__':
    main()
