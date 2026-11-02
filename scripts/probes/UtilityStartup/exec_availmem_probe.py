"""Native Exec AvailMem TOTAL/LARGEST comparison on pinned private images."""
import json, shutil, sys
from pathlib import Path
from prepare_frame_image_probe import Assembler
from prepare_mui_calc import put
from refresh_utility_gui import inventory, sha
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory

FLAGS = [0, 2, 4, 0x80000, 0x80002, 0x80004, 0x20000, 0xA0000]

def command():
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.immediate(0, 160); a.immediate(1, 0x10001); call(-198)
    a.words(0x4a80); a.branch(0x6700, 'done')
    a.words(0x2a40, 0x1b7c, 8, 8, 0x43ed, 128, 0x2b49, 10)
    a.address(0, 'name'); a.immediate(0, 14); call(-624)
    a.words(0x224d); call(-486)
    a.words(0x2b7c, 0x4d45, 0x4d31, 16)
    for i, flags in enumerate(FLAGS):
        a.immediate(1, flags); call(-216); a.words(0x2b40, 24 + i * 4)
    a.immediate(0, 4096); a.immediate(1, 1); call(-198)
    a.words(0x2440, 0x2b40, 56)
    a.immediate(1, 0x80000); call(-216); a.words(0x2b40, 60)
    a.words(0x200a, 0x4a80); a.branch(0x6700, 'complete')
    a.words(0x224a); a.immediate(0, 4096); call(-210)
    a.label('complete'); a.words(0x2b7c, 0, 1, 20)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    a.label('name'); a.code += b'AvailMemProbe\0\0'
    return a.finish()

if __name__ == '__main__':
    root = Path(sys.argv[1]); root.mkdir(parents=True, exist_ok=False)
    guest = command()
    for kind, name, expected in [('copperstart', 'mui-calc-copperos-ffs-v50-backfill.hdf', '4473c347e20345e0f13171876b0147bbd63c4cb00cd79a5e19a21ab63b084695'), ('kickstart31', 'mui-calc-copperos-ofs-v48.hdf', 'e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799')]:
        base = Path('artifacts/utility-mui-2026-10-31') / name
        assert sha(base.read_bytes()) == expected
        out = root / (kind + '.hdf'); shutil.copyfile(base, out)
        dev = BlkDevFactory().open(str(out)); vol = ADFSVolume(dev); vol.open()
        writes = {'C/AvailMemProbe': guest, 'S/Startup-Sequence': b'C:CopperGui\nC:AvailMemProbe\n'}
        try:
            before = inventory(vol)
            for path, data in writes.items(): put(vol, path, data)
            after = inventory(vol)
            wanted = {k.casefold(): v for k, v in before.items()}
            wanted.update({k.casefold(): sha(v) for k, v in writes.items()})
            assert {k.casefold(): v for k, v in after.items()} == wanted
        finally: vol.close(); dev.close()
        Path(str(out) + '.json').write_text(json.dumps(dict(protocol='exec-availmem-probe-v1', flags=FLAGS, baseSha256=expected, sha256=sha(out.read_bytes()), installedFiles={k: sha(v) for k, v in writes.items()}, allGuestFiles=after), indent=2) + '\n')
        print(out)
