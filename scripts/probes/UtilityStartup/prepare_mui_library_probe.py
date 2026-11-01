"""Install a native MUI library lifecycle regression in a private test HDF.

The command publishes copied results in a reset-lived Exec resource. It uses
only native Exec/library calls; no guest code or ROM is patched by the host.
"""
import argparse
import json
from pathlib import Path
import shutil

from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory
from prepare_frame_image_probe import Assembler
from prepare_mui_calc import put
from refresh_utility_gui import inventory, sha


def command():
    a = Assembler()
    def call(lvo):
        a.words(0x4eae, lvo)
    def store(offset):
        a.words(0x2b40, offset)  # MOVE.L D0,offset(A5)
    def count(offset):
        a.words(0x7000, 0x302c, 32)  # lib_OpenCnt(A4)
        store(offset)
    def open_library(version):
        a.words(0x2c78, 4)
        a.address(1, 'library')
        a.immediate(0, version)
        call(-552)
    def close_library(offset):
        a.words(0x224c, 0x2c78, 4)  # A1=library, A6=Exec
        call(-414)
        count(offset)

    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.immediate(0, 128)
    a.immediate(1, 0x10001)
    call(-198)
    a.words(0x4a80)
    a.branch(0x6700, 'done')
    a.words(0x2a40, 0x1b7c, 8, 8)  # A5=result; NT_RESOURCE
    a.address(0, 'name')
    a.words(0x43ed, 96, 0x2b49, 10, 0x7010)
    call(-624)  # Copy stable resource name into its own allocation.
    a.words(0x224d)
    call(-486)
    open_library(4)
    store(20)
    a.words(0x4a80)
    a.branch(0x6700, 'done')
    a.words(0x2840, 0x7000, 0x302c, 20)
    store(24)  # Library version
    count(28)  # First open
    open_library(5)
    store(32)  # Must be NULL; Exec owns minimum-version rejection.
    count(36)
    open_library(4)
    store(40)
    count(44)
    a.words(0x200c, 0x2c4c)  # Deliberately put base (not version) in D0.
    call(-6)
    store(48)
    count(52)
    close_library(56)
    close_library(60)
    close_library(64)
    a.immediate(0, 0x4d554931)  # MUI1: completed all calls
    store(16)
    a.label('done')
    a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    for label, value in [('name', b'MuiLibraryProbe\0'), ('library', b'muimaster.library\0')]:
        a.label(label)
        a.code += value
        if len(a.code) & 1:
            a.code.append(0)
    return a.finish()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base', type=Path, required=True)
    p.add_argument('--base-sha256', required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--native-reference', action='store_true')
    mode = p.add_mutually_exclusive_group()
    mode.add_argument('--builtins', action='store_true')
    mode.add_argument('--classic-menu', action='store_true')
    mode.add_argument('--native-window', action='store_true')
    mode.add_argument('--native-window-text', action='store_true')
    mode.add_argument('--native-window-pointer', action='store_true')
    mode.add_argument('--native-window-resize', action='store_true')
    mode.add_argument('--native-window-update', action='store_true')
    mode.add_argument('--native-window-indexed-background', action='store_true')
    mode.add_argument('--native-window-list-rows', action='store_true')
    mode.add_argument('--native-window-list-array', action='store_true')
    p.add_argument('--probe-only', action='store_true')
    p.add_argument('--preload-mui', action='store_true', help='Versioned guest allocation-order experiment before CopperGui')
    args = p.parse_args()
    if sha(args.base.read_bytes()) != args.base_sha256.lower():
        raise ValueError('Unexpected base image')
    if args.output.exists():
        raise FileExistsError('Preserve earlier test outputs')
    startup = (b'C:Assign ENV: RAM:\nC:Assign T: RAM:\n' if args.native_reference else b'')
    probe_name = 'MuiLibraryProbe'
    probe_command = command
    if args.builtins:
        from mui_builtin_probe import command as probe_command
        probe_name = 'MuiBuiltinProbe'
    if args.classic_menu:
        from mui_classic_menu_probe import command as probe_command
        probe_name = 'MuiClassicMenuProbe'
    if args.native_window:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True)
        probe_name = 'MuiWindowProbe'
    if args.native_window_text:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True)
        probe_name = 'MuiWindowTextProbe'
    if args.native_window_pointer:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True)
        probe_name = 'MuiWindowPointerProbe'
    if args.native_window_resize:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True, tall_text=True)
        probe_name = 'MuiWindowResizeProbe'
    if args.native_window_update:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True, update_text=True)
        probe_name = 'MuiWindowUpdateProbe'
    if args.native_window_indexed_background:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True, update_text=True, indexed_background=True)
        probe_name = 'MuiWindowIndexedBackgroundProbe'
    if args.native_window_list_rows:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True, update_text=True, indexed_background=True, list_rows=True)
        probe_name = 'MuiWindowListRowsProbe'
    if args.native_window_list_array:
        from mui_classic_menu_probe import command as window_command
        probe_command = lambda: window_command(open_windows=True, text_roots=True, pointer_hook=True, update_text=True, indexed_background=True, list_rows=True, array_insert=True)
        probe_name = 'MuiWindowListArrayProbe'
    tail = '' if args.probe_only else 'SYS:MUICalc/muicalc\n'
    files = {'C/' + probe_name: probe_command(), 'S/Startup-Sequence':
             startup + (('C:MuiPreloadProbe\n' if args.preload_mui else '') + 'C:CopperGui\nC:' + probe_name + '\n' + tail).encode('ascii')}
    if args.preload_mui:
        from mui_preload_probe import command as preload_command
        files['C/MuiPreloadProbe'] = preload_command()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(args.base, args.output)
    device = BlkDevFactory().open(str(args.output))
    volume = ADFSVolume(device)
    try:
        volume.open()
        before = inventory(volume)
        for name, data in files.items():
            put(volume, name, data)
        after = inventory(volume)
        expected = {n.casefold(): h for n, h in before.items()}
        expected.update({n.casefold(): sha(b) for n, b in files.items()})
        if expected != {n.casefold(): h for n, h in after.items()}:
            raise ValueError('Unexpected image changes')
    finally:
        volume.close()
        device.close()
    receipt = {'baseSha256': args.base_sha256, 'sha256': sha(args.output.read_bytes()),
               'allocationOrder': 'mui-before-gui-v1' if args.preload_mui else 'gui-before-mui-v1',
               'commandSha256': sha(files['C/' + probe_name]), 'allGuestFiles': after}
    Path(str(args.output) + '.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({k: v for k, v in receipt.items() if k != 'allGuestFiles'}))


if __name__ == '__main__':
    main()
