"""Create a local OFS Personal Paint installation; never redistribute the image.

Requires amitools and lhafile. Optional Workbench files must be user supplied.
Without Workbench this is a prerequisite diagnostic, not a complete control.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import zipfile

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--base', type=Path, required=True, help='Pinned SysInfo OFS disk supplies standard boot code')
    parser.add_argument('--provider-root', type=Path, required=True)
    parser.add_argument('--shell-root', type=Path, required=True)
    parser.add_argument('--workbench', type=Path, help='User-supplied Workbench 1.3 ADF or single-ADF ZIP')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Preserve previous controlled images')
    archive = args.archive.read_bytes(); base = args.base.read_bytes()
    if sha(archive) != '2e20ea41001c4db0db5cd6a6505ed6c9708eea1aaccd6688e33ccd03334c51ff':
        raise ValueError('Unexpected Personal Paint archive')
    if sha(base) != '77e64513b230b2dceb8012fc747ac6b0fba2c0297b662dfac30ee1e11599830c':
        raise ValueError('Unexpected base boot disk')
    files = {}; origins = {}; directories = {'C', 'L', 'Libs', 'S'}
    def add(path, data, origin):
        path = str(PurePosixPath(path))
        if path.startswith('/') or '..' in PurePosixPath(path).parts:
            raise ValueError('Unsafe guest path')
        for previous in list(files):
            if previous.casefold() == path.casefold():
                del files[previous]; del origins[previous]
        files[path] = data; origins[path] = origin
        parent = PurePosixPath(path).parent
        while str(parent) != '.':
            directories.add(str(parent)); parent = parent.parent
    if args.workbench:
        if args.workbench.suffix.lower() == '.zip':
            with zipfile.ZipFile(args.workbench) as bundle:
                entries = [n for n in bundle.namelist() if n.lower().endswith('.adf')]
                if len(entries) != 1:
                    raise ValueError('Workbench ZIP must contain exactly one ADF')
                device = BlkDevFactory().open('workbench.adf', read_only=True, fobj=io.BytesIO(bundle.read(entries[0])))
        else:
            device = BlkDevFactory().open(str(args.workbench), read_only=True)
        volume = ADFSVolume(device); volume.open()
        try:
            def walk(node, prefix=''):
                for child in node.get_entries():
                    name = child.name.get_name().get_unicode()
                    path = prefix + name
                    if child.is_dir():
                        directories.add(path); walk(child, path + '/')
                    else:
                        add(path, bytes(volume.read_file(FSString(path))), 'user Workbench image')
            walk(volume.get_root_dir())
        finally:
            volume.close(); device.close()
    lha = lhafile.Lhafile(str(args.archive))
    for entry in lha.infolist():
        path = entry.filename.replace('\\', '/')
        if path.endswith('/'):
            directories.add(path.rstrip('/')); continue
        data = lha.read(entry.filename); add(path, data, 'original PPaint archive: ' + entry.filename)
        if path.startswith('PPaint/libs/'):
            add('Libs/' + path.rsplit('/', 1)[1], data, 'original PPaint archive: ' + entry.filename)
    provider = json.loads((args.provider_root / 'intuition.json').read_text(encoding='utf-8-sig'))
    shell = json.loads((args.shell_root / 'shell-manifest.json').read_text(encoding='utf-8-sig'))
    image = (args.provider_root / 'CopperIntuition.hunk').read_bytes()
    shell_image = (args.shell_root / 'CopperShell').read_bytes()
    if sha(image) != provider['sha256'].lower() or sha(shell_image) != shell['sha256'].lower():
        raise ValueError('Guest prerequisite manifest mismatch')
    add('C/Intuition', image, 'native GUI provider manifest')
    add('C/CopperGui', (args.provider_root / 'CopperGui').read_bytes(), 'native GUI installer')
    add('L/CopperShell', shell_image, 'native Shell manifest')
    startup = b'C:CopperGui\n'
    if args.workbench:
        startup += b'Assign PPaint: SYS:PPaint\nCD PPaint:\nPPaint\n'
    else:
        startup += b'SYS:PPaint/PPaint\n'
    add('S/Startup-Sequence', startup, 'controlled startup')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    device = BlkDevFactory().create(str(args.output), force=False, options={'size': 16 * 1024 * 1024})
    volume = ADFSVolume(device)
    try:
        volume.create(FSString('PPaintControl'), dos_type=0x444F5300, boot_code=base[12:1024])
        unique_directories = {p.casefold(): p for p in sorted(directories)}
        for directory in sorted(unique_directories.values(), key=lambda p: (p.count('/'), p)):
            volume.create_dir(FSString(directory))
        for path, data in sorted(files.items()):
            volume.write_file(data, FSString(path))
            if bytes(volume.read_file(FSString(path))) != data:
                raise ValueError('Guest file differs: ' + path)
        free = volume.get_free_blocks()
    finally:
        volume.close(); device.close()
    receipt = {'kind': 'controlled-personal-paint-ofs-installation', 'sha256': sha(args.output.read_bytes()),
               'archiveSha256': sha(archive), 'baseSha256': sha(base), 'provider': provider, 'shell': shell,
               'workbenchSha256': sha(args.workbench.read_bytes()) if args.workbench else None,
               'startup': startup.decode('ascii'), 'freeBlocks': free,
               'files': [{'path': path, 'bytes': len(data), 'sha256': sha(data), 'origin': origins[path]}
                         for path, data in sorted(files.items())],
               'scope': 'Local preparation and byte readback only; no execution verdict; missing Workbench means incomplete prerequisites'}
    Path(str(args.output) + '.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'sha256': receipt['sha256'], 'files': len(files), 'freeBlocks': free}))


if __name__ == '__main__':
    main()
