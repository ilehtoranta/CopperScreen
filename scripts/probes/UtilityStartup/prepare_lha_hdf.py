"""Install the complete pinned LhA 2.15 package on a verified local HDF.

Requires amitools/lhafile. Keep privately supplied system files and media local.
Image preparation and complete file readback are not a runtime verdict.
"""
import argparse
import io
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
    parser.add_argument('--gadtools', type=Path, help='Qualified private module with adjacent .json receipt')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Preserve earlier images')
    base = json.loads(Path(str(args.base_hdf) + '.json').read_text(encoding='utf-8-sig'))
    image = args.base_hdf.read_bytes()
    if sha(image) != base['sha256']:
        raise ValueError('Base image receipt mismatch')
    archive_sha = sha(args.archive.read_bytes())
    if archive_sha != '76bae515264fcc3e1c69058ff03a4bcb096152a732cf19fdb03cceee18932497':
        raise ValueError('Unexpected LhA 2.15 archive')
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

        archive_payload = args.archive.read_bytes()[6112:]
        if len(archive_payload) != 167691 or sha(archive_payload) != 'b1e9ce416a84c4e2b53c70fd51d434df09eb11fe110014d86c0bea76147d1899':
            raise ValueError('Pinned SFX payload mismatch')
        archive = lhafile.Lhafile(io.BytesIO(archive_payload))
        for entry in archive.infolist():
            path = 'LhA/' + entry.filename.replace('\\', '/')
            if not path.endswith('/'):
                add(path, archive.read(entry.filename), 'complete original LhA 2.15 archive: ' + path)
        shell = base['shell']
        if args.shell_root:
            shell = json.loads((args.shell_root / 'shell-manifest.json').read_text(encoding='utf-8-sig'))
            shell_payload = (args.shell_root / 'CopperShell').read_bytes()
            if (shell['sha256'].lower() != sha(shell_payload)
                    or shell['dosClient']['serviceVersion'] != 2
                    or not shell['codeSizeOptimizations']):
                raise ValueError('Native Shell receipt mismatch')
            add('L/CopperShell', shell_payload, 'native Shell with matching compiler-input receipt')
        gadtools = None
        if args.gadtools:
            gadtools = json.loads(Path(str(args.gadtools) + '.json').read_text(encoding='utf-8-sig'))
            library = args.gadtools.read_bytes()
            if (gadtools['kind'] != 'private-kickstart-library-module'
                    or gadtools['name'] != 'gadtools.library'
                    or gadtools['sourceRomSha256'] != '8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee'
                    or gadtools['bytes'] != len(library)
                    or gadtools['sha256'].lower() != sha(library)):
                raise ValueError('Private GadTools dependency receipt mismatch')
            add('Libs/gadtools.library', library, 'private native GadTools module with matching relocation receipt')
        add('fixtures/lha/distribution.lha', archive_payload, 'original pinned self-extractor compressed payload at offset6112')
        fixture_readback = bytes(volume.read_file(FSString('fixtures/lha/distribution.lha')))
        if len(fixture_readback) != 167691 or sha(fixture_readback) != 'b1e9ce416a84c4e2b53c70fd51d434df09eb11fe110014d86c0bea76147d1899':
            raise ValueError('Installed archive does not match the pinned original payload')
        startup = b'C:CopperGui\nSYS:LhA/lha_68k t SYS:fixtures/lha/distribution.lha\n'
        if args.native_env:
            if volume.get_path_name(FSString('ENV')) is None:
                volume.create_dir(FSString('ENV'))
            startup = b'C:Assign ENV: SYS:ENV\n' + startup
        add('S/Startup-Sequence', startup, 'controlled direct LhA invocation')
        for entry in files.values():
            content = bytes(volume.read_file(FSString(entry['path'])))
            if len(content) != entry['bytes'] or sha(content) != entry['sha256']:
                raise ValueError('Final file mismatch: ' + entry['path'])
    finally:
        volume.close()
        device.close()
    receipt = {'kind': 'controlled-lha-installation', 'filesystem': base['filesystem'],
               'baseSha256': base['sha256'], 'archiveSha256': archive_sha, 'payloadSha256': sha(archive_payload), 'payloadOffset': 6112,
               'payloadBytes': len(archive_payload), 'payloadReadbackSha256': sha(fixture_readback),
               'sha256': sha(args.output.read_bytes()), 'startup': startup.decode(),
               'provider': base['provider'], 'guiLoader': base.get('guiLoader'),
               'shell': shell, 'mathFfp': base['mathFfp'], 'gadtoolsDependency': gadtools,
               'changedPaths': changed, 'files': sorted(files.values(), key=lambda f: f['path']),
               'scope': 'Private complete-package preparation and file readback; not runtime validation. Native ENV setup is an explicit diagnostic difference.'}
    Path(str(args.output) + '.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'sha256': receipt['sha256'], 'files': len(files)}))


if __name__ == '__main__':
    main()
