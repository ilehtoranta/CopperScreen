"""Prepare the complete pinned DPaint V demo on its own OFS floppy.
System HDFs and privately supplied libraries stay local. Readback is not a replay.
"""
import argparse
import hashlib
import json
import io
import zipfile
from pathlib import Path, PurePosixPath

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.ADFBlockDevice import ADFBlockDevice
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def put(volume, path, data, replace=False):
    parts = PurePosixPath(path).parts
    if ':' in path or path.startswith('/') or any(x in ('.', '..') for x in parts):
        raise ValueError('Unsafe guest path')
    for count in range(1, len(parts)):
        directory = '/'.join(parts[:count])
        if volume.get_path_name(FSString(directory)) is None:
            volume.create_dir(FSString(directory))
    if replace and volume.get_path_name(FSString(path)) is not None:
        volume.delete(FSString(path))
    volume.write_file(data, FSString(path))
    if bytes(volume.read_file(FSString(path))) != data:
        raise ValueError('Guest byte readback mismatch: ' + path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--base-hdf', type=Path, required=True)
    parser.add_argument('--output-adf', type=Path, required=True)
    parser.add_argument('--output-hdf', type=Path, required=True)
    parser.add_argument('--native-env', action='store_true')
    parser.add_argument('--native-ffp', type=Path, help='CopperStart disk-loaded FFP module test output directory')
    parser.add_argument('--gui-provider', type=Path, help='Native GUI payload and matching canonical CopperGui loader receipts')
    parser.add_argument('--workbench-31', type=Path, help='Private pinned Workbench3.1 ZIP providing standard OS libraries')
    args = parser.parse_args()
    for output in (args.output_adf, args.output_hdf):
        if output.exists():
            raise FileExistsError('Preserve earlier media: ' + str(output))
        output.parent.mkdir(parents=True, exist_ok=True)
    archive_bytes = args.archive.read_bytes()
    if sha(archive_bytes) != '9e8a31c13c9cb69046376968ef05a43c2e3cc298e14fb7698cf04f97b7cb4091':
        raise ValueError('Unexpected DPaint V demo archive')
    archive = lhafile.Lhafile(str(args.archive))
    payloads = {item.filename.replace('\\', '/'): archive.read(item.filename)
                for item in archive.infolist() if not item.filename.endswith('/')}
    if len(payloads) != 29 or sha(payloads['DPaint_Demo']) != '5402cc3dd2fe6d0ec84be9cc6eb389ec4dc207709b48e7be66366065847f635e':
        raise ValueError('Pinned complete demo mismatch')
    adf_device = ADFBlockDevice(str(args.output_adf))
    adf_device.create()
    adf_volume = ADFSVolume(adf_device)
    adf_volume.create(FSString('DPaintV_demo'))
    try:
        for path, data in payloads.items():
            put(adf_volume, path, data)
    finally:
        adf_volume.close()
        adf_device.close()
    # Independently reopen the installed floppy and compare to archive bytes.
    adf_device = ADFBlockDevice(str(args.output_adf), read_only=True)
    adf_device.open()
    adf_volume = ADFSVolume(adf_device)
    adf_volume.open()
    try:
        for path, data in payloads.items():
            if bytes(adf_volume.read_file(FSString(path))) != data:
                raise ValueError('Reopened floppy mismatch: ' + path)
    finally:
        adf_volume.close()
        adf_device.close()
    base = json.loads(Path(str(args.base_hdf) + '.json').read_text(encoding='utf-8-sig'))
    system_bytes = args.base_hdf.read_bytes()
    if sha(system_bytes) != base['sha256']:
        raise ValueError('System image receipt mismatch')
    args.output_hdf.write_bytes(system_bytes)
    device = BlkDevFactory().open(str(args.output_hdf), read_only=False)
    volume = ADFSVolume(device)
    volume.open()
    files = {item['path'].casefold(): dict(item) for item in base['files']}
    libraries = {}
    workbench = None
    if args.workbench_31:
        source = args.workbench_31.read_bytes()
        if sha(source) != 'd93611887acf91f68f5608a5b7812f03eb16f743f40451b67c93067b04c967ed':
            raise ValueError('Unexpected Workbench3.1 archive')
        with zipfile.ZipFile(args.workbench_31) as bundle:
            members = [n for n in bundle.namelist() if n.lower().endswith('.adf')]
            if len(members) != 1:
                raise ValueError('Expected one Workbench ADF')
            adf = bundle.read(members[0])
        if sha(adf) != 'a8f167bad2897e8c7f2cfe73de7a9f8dad10c7a5b1f78ca17f818ab17278b985':
            raise ValueError('Unexpected Workbench ADF')
        wb_device = BlkDevFactory().open('workbench.adf', read_only=True, fobj=io.BytesIO(adf))
        wb_volume = ADFSVolume(wb_device)
        wb_volume.open()
        try:
            paths = ['Libs/' + name + '.library' for name in
                     ('asl', 'diskfont', 'locale', 'mathtrans', 'iffparse', 'rexxsyslib', 'datatypes', 'commodities')]
            paths.append('C/Assign')
            for path in paths:
                libraries[path] = bytes(wb_volume.read_file(FSString(path)))
        finally:
            wb_volume.close()
            wb_device.close()
        workbench = {'sourceSha256': sha(source), 'adfSha256': sha(adf),
                     'libraries': [{'path': path, 'bytes': len(data), 'sha256': sha(data)}
                                   for path, data in sorted(libraries.items())]}
    startup = b'C:CopperGui\nC:Assign FONTS: DF0:Fonts\nCD DF0:\nDF0:DPaint_Demo\n'
    ffp = None
    if args.native_ffp:
        ffp = json.loads((args.native_ffp / 'mathffp.json').read_text(encoding='utf-8-sig'))
        for name, key in [('CopperFfp', 'installerSha256'), ('MathFfpNative', 'sha256')]:
            data = (args.native_ffp / name).read_bytes()
            if sha(data) != ffp[key].lower():
                raise ValueError('Native FFP receipt mismatch: ' + name)
            libraries['C/' + name] = data
        startup = b'C:CopperFfp\n' + startup
    gui = None
    if args.gui_provider:
        gui = json.loads((args.gui_provider / 'intuition.json').read_text(encoding='utf-8-sig'))
        loader = json.loads((args.gui_provider / 'CopperGui.json').read_text(encoding='utf-8-sig'))
        if (loader['providerSha256'].lower() != gui['sha256'].lower() or
                loader['classicEntry'] != gui['classicEntry']):
            raise ValueError('GUI loader and provider do not match')
        for source, target, expected in [('CopperIntuition.hunk', 'C/Intuition', gui['sha256']),
                                         ('CopperGui', 'C/CopperGui', loader['sha256'])]:
            data = (args.gui_provider / source).read_bytes()
            if sha(data) != expected.lower():
                raise ValueError('GUI receipt mismatch: ' + source)
            libraries[target] = data
        gui = {'provider': gui, 'loader': loader}
    if args.native_env:
        if volume.get_path_name(FSString('ENV')) is None:
            volume.create_dir(FSString('ENV'))
        startup = b'C:Assign ENV: RAM:\nC:Assign T: RAM:\n' + startup
    try:
        for item in files.values():
            content = bytes(volume.read_file(FSString(item['path'])))
            if len(content) != item['bytes'] or sha(content) != item['sha256']:
                raise ValueError('System input mismatch: ' + item['path'])
        for path, data in libraries.items():
            put(volume, path, data, replace=True)
            files[path.casefold()] = {'path': path, 'bytes': len(data), 'sha256': sha(data),
                                      'origin': ('CopperStart native GUI prerequisite' if args.gui_provider and path in
                                                 ('C/Intuition', 'C/CopperGui') else
                                                 'CopperStart native FFP prerequisite' if path in
                                                 ('C/CopperFfp', 'C/MathFfpNative') else
                                                 'private pinned Workbench3.1 OS prerequisite')}
        put(volume, 'S/Startup-Sequence', startup, replace=True)
        files['s/startup-sequence'] = {'path': 'S/Startup-Sequence', 'bytes': len(startup),
                                     'sha256': sha(startup), 'origin': 'controlled floppy-only DPaint invocation'}
        for item in files.values():
            if sha(bytes(volume.read_file(FSString(item['path'])))) != item['sha256']:
                raise ValueError('Final system readback mismatch: ' + item['path'])
    finally:
        volume.close()
        device.close()
    receipt = dict(base)
    receipt.update(kind='controlled-dpaint-floppy-boot', baseSha256=base['sha256'],
                   sha256=sha(args.output_hdf.read_bytes()), startup=startup.decode(), workbench31=workbench,
                   nativeFfp=ffp,
                   guiProvider=gui,
                   files=sorted(files.values(), key=lambda item: item['path']),
                   demoFloppy={'volume': 'DPaintV_demo', 'sha256': sha(args.output_adf.read_bytes()),
                               'bytes': args.output_adf.stat().st_size, 'filesystem': 'DOS0 OFS',
                               'archiveSha256': sha(archive_bytes),
                               'files': [{'path': path, 'bytes': len(data), 'sha256': sha(data)}
                                         for path, data in sorted(payloads.items())]},
                   scope='Complete original demo retained on its own floppy, including root Libs and required fonts. System HDF supplies boot prerequisites only. Preparation is not runtime qualification.')
    Path(str(args.output_hdf) + '.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    Path(str(args.output_adf) + '.json').write_text(json.dumps(receipt['demoFloppy'], indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'hdfSha256': receipt['sha256'], 'adfSha256': receipt['demoFloppy']['sha256'],
                      'demoFiles': len(payloads)}))


if __name__ == '__main__':
    main()
