"""Make a local SysInfo control disk with native disk-loaded GUI prerequisites.

Requires amitools (the collection's local tools directory can be on PYTHONPATH).
The original disk is preserved. Never commit the resulting ADF or guest HUNKs.
CopperGui keeps native Kickstart's Intuition and installs the native CopperStart
provider only when the boot screen owner needs upgrading.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original", required=True, type=Path)
    parser.add_argument("--provider-root", required=True, type=Path)
    parser.add_argument("--installer", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    original = args.original.read_bytes()
    if sha(original) != "77e64513b230b2dceb8012fc747ac6b0fba2c0297b662dfac30ee1e11599830c":
        raise ValueError("Use the collected original SysInfo 4.4 ADF")
    manifest = json.loads((args.provider_root / "intuition.json").read_text(encoding="utf-8-sig"))
    provider = (args.provider_root / "CopperIntuition.hunk").read_bytes()
    installer = args.installer.read_bytes()
    if sha(provider).upper() != manifest["sha256"]:
        raise ValueError("Provider does not match its compiler manifest")
    if provider[:4] != b"\0\0\x03\xf3" or installer[:4] != b"\0\0\x03\xf3":
        raise ValueError("Native guest HUNK inputs are required")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if args.output.exists():
        raise FileExistsError("Retain earlier controlled installations")
    shutil.copyfile(args.original, args.output)
    device = BlkDevFactory().open(str(args.output))
    volume = ADFSVolume(device)
    try:
        volume.open()
        program = bytes(volume.read_file(FSString("SysInfo/SysInfo")))
        startup = bytes(volume.read_file(FSString("S/Startup-Sequence")))
        target = b"SYS:SysInfo/SysInfo"
        if startup.count(target) != 1:
            raise ValueError("Unexpected original startup sequence")
        updated = startup.replace(target, b"C:CopperGui\n" + target)
        volume.write_file(provider, FSString("C/Intuition"))
        volume.write_file(installer, FSString("C/CopperGui"))
        volume.delete(FSString("S/Startup-Sequence"))
        volume.write_file(updated, FSString("S/Startup-Sequence"))
        if bytes(volume.read_file(FSString("SysInfo/SysInfo"))) != program:
            raise ValueError("SysInfo program changed")
        free_blocks = volume.get_free_blocks()
    finally:
        volume.close()
        device.close()
    receipt = {
        "kind": "controlled-native-gui-prerequisite-installation",
        "originalSha256": sha(original), "sha256": sha(args.output.read_bytes()),
        "utilityProgramSha256": sha(program), "provider": manifest,
        "installerSha256": sha(installer), "freeBlocks": free_blocks,
        "originalStartup": startup.decode("latin-1"), "startup": updated.decode("latin-1"),
        "changes": ["C/Intuition", "C/CopperGui", "S/Startup-Sequence"],
        "lifetime": "Every attempted native GUI installation retains its module until reset, including partial failure",
    }
    Path(str(args.output) + ".json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: receipt[key] for key in ("sha256", "utilityProgramSha256", "freeBlocks")}))


if __name__ == "__main__":
    main()
