"""Create a local PPMore reader disk with pinned guest library prerequisites.

Requires amitools and lhafile. Keep downloaded archives and generated media
local; this recipe does not authorize redistribution of a combined disk.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def checked(path, expected):
    data = path.read_bytes()
    if sha(data) != expected:
        raise ValueError(f"Input hash mismatch: {path}")
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--ppmore", type=Path, required=True)
    parser.add_argument("--ppshow", type=Path, required=True)
    parser.add_argument("--reqtools", type=Path, required=True)
    parser.add_argument("--provider-root", type=Path, required=True)
    parser.add_argument("--shell-root", type=Path,
                        help="Use a minimal disk with the native guest Shell and its disk-library loader")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    inputs = [
        (args.base, "77e64513b230b2dceb8012fc747ac6b0fba2c0297b662dfac30ee1e11599830c"),
        (args.ppmore, "6cd4c726f2013381e4844b6582116aa28d972610bc84fc51db44ecc5b0a63c52"),
        (args.ppshow, "66383bf59b4842f140f86455a20c535c4ebf2d9996138218b06b7a7cc205bdf3"),
        (args.reqtools, "50c0355d2a34e69df41ad6b8bbea11ef282e6160e5b12782bd2f1118a15eb6bc"),
    ]
    for path, expected in inputs:
        checked(path, expected)
    more = lhafile.Lhafile(str(args.ppmore))
    show = lhafile.Lhafile(str(args.ppshow))
    req = lhafile.Lhafile(str(args.reqtools))
    manifest = json.loads((args.provider_root / "intuition.json").read_text(encoding="utf-8-sig"))
    provider = checked(args.provider_root / "CopperIntuition.hunk", manifest["sha256"].lower())
    installer = (args.provider_root / "CopperGui").read_bytes()
    fixture = "CopperScreen PPMore A/B fixture\n\n" + "".join(
        f"Line {i:03}: Fixed ASCII reader test. CopperScreen native guest utility.\n"
        for i in range(1, 81))
    files = {
        "C/Intuition": provider,
        "C/CopperGui": installer,
        "C/PPMore": more.read("PPMore18a\\PPMore"),
        "Docs/PPMore.doc": more.read("PPMore18a\\PPMore.doc"),
        "Docs/ReqTools-DISTRIBUTION": req.read("ReqTools\\DISTRIBUTION"),
        "Libs/reqtools.library": req.read("ReqTools\\libs13\\reqtools.library"),
        "Libs/powerpacker.library": show.read("PPShow\\libs\\powerpacker.library"),
        "fixture.txt": fixture.encode("ascii"),
    }
    shell_manifest = None
    if args.shell_root:
        shell_manifest = json.loads((args.shell_root / "shell-manifest.json").read_text(encoding="utf-8-sig"))
        files["L/CopperShell"] = checked(args.shell_root / "CopperShell", shell_manifest["sha256"].lower())
    if args.output.exists():
        raise FileExistsError("Preserve earlier controlled disks")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if args.shell_root:
        device = BlkDevFactory().create(str(args.output))
    else:
        shutil.copyfile(args.base, args.output)
        device = BlkDevFactory().open(str(args.output))
    volume = ADFSVolume(device)
    try:
        if args.shell_root:
            volume.create(FSString("PPMoreControl"), dos_type=0x444F5300,
                          boot_code=args.base.read_bytes()[12:1024])
            for directory in ("C", "L", "Libs", "Docs", "S"):
                volume.create_dir(FSString(directory))
            startup = b"C:CopperGui\nC:PPMore SYS:fixture.txt\n"
        else:
            volume.open()
            volume.create_dir(FSString("Docs"))
            original_startup = bytes(volume.read_file(FSString("S/Startup-Sequence")))
            startup = original_startup.replace(b"SYS:SysInfo/SysInfo", b"C:CopperGui\nC:PPMore SYS:fixture.txt")
            if startup == original_startup:
                raise ValueError("Unexpected base startup")
        for name, data in files.items():
            volume.write_file(data, FSString(name))
            if bytes(volume.read_file(FSString(name))) != data:
                raise ValueError(f"Guest file differs: {name}")
        if not args.shell_root:
            volume.delete(FSString("S/Startup-Sequence"))
        volume.write_file(startup, FSString("S/Startup-Sequence"))
        free = volume.get_free_blocks()
    finally:
        volume.close()
        device.close()
    receipt = {
        "kind": "controlled-ppmore-reader-prerequisites",
        "inputs": [{"file": str(path), "sha256": digest} for path, digest in inputs],
        "sha256": sha(args.output.read_bytes()), "provider": manifest,
        "files": [{"path": name, "bytes": len(data), "sha256": sha(data)} for name, data in files.items()],
        "startup": startup.decode("latin-1"), "freeBlocks": free,
        "reqtoolsSource": "https://ftp.fau.de/aminet/misc/fish/fish-0794.lha",
        "reqtoolsMember": "ReqTools/libs13/reqtools.library",
        "scope": "Controlled local installation; no execution verdict",
    }
    if shell_manifest:
        receipt["kind"] = "controlled-ppmore-native-shell-reader"
        receipt["shell"] = shell_manifest
    Path(str(args.output) + ".json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"sha256": receipt["sha256"], "freeBlocks": free}))


if __name__ == "__main__":
    main()
