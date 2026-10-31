"""Prepare a local MUI Calc/CopperOS development-library test hardfile.

The original seven-file giftware package is retained. This is preparation,
not a claim that the development library is a compatible MUI replacement.
"""
import argparse
import json
from pathlib import Path, PurePosixPath
import shutil

import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory
from refresh_utility_gui import inventory, sha


def put(volume, path, data):
    parts = PurePosixPath(path).parts
    if path.startswith("/") or ":" in path or any(p in (".", "..") for p in parts):
        raise ValueError("Unsafe guest path")
    for i in range(1, len(parts)):
        directory = "/".join(parts[:i])
        if volume.get_path_name(FSString(directory)) is None:
            volume.create_dir(FSString(directory))
    if volume.get_path_name(FSString(path)) is not None:
        volume.delete(FSString(path))
    volume.write_file(data, FSString(path))
    if bytes(volume.read_file(FSString(path))) != data:
        raise ValueError(f"Guest readback mismatch: {path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--base-sha256", required=True)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--library-root", type=Path, required=True)
    parser.add_argument("--library-name", choices=("copperos-muimaster.library", "muimaster.library"),
                        default="copperos-muimaster.library")
    parser.add_argument("--native-reference", action="store_true")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    base = args.base.read_bytes()
    if sha(base) != args.base_sha256.lower():
        raise ValueError("Unexpected OS prerequisite hardfile")
    archive = args.archive.read_bytes()
    if sha(archive) != "ca41cecb89027fa2a3fd199df2ff200a98819646d2c32876490992b01c4d35fd":
        raise ValueError("Unexpected original MUI Calc 1.25 archive")
    manifest = json.loads((args.library_root / (args.library_name + ".json")).read_text())
    library = (args.library_root / args.library_name).read_bytes()
    if (manifest["Cpu"] != "68000" or manifest["Name"] != args.library_name
            or sha(library) != manifest["Sha256"].lower()):
        raise ValueError("CopperOS library differs from its 68000 build receipt")
    if args.output.exists():
        raise FileExistsError("Preserve earlier controlled images")
    package = lhafile.Lhafile(str(args.archive))
    files = {}
    for entry in package.infolist():
        name = entry.filename.replace("\\", "/")
        files["MUICalc/" + name] = package.read(entry.filename)
    if len(files) != 7:
        raise ValueError("The complete original seven-file package is required")
    # The build owns its Resident name/version. The default development build
    # gets a filename alias only; a classic-profile build needs no alias.
    files["Libs/" + args.library_name] = library
    files["Libs/muimaster.library"] = library
    prefix = b"C:Assign ENV: RAM:\nC:Assign T: RAM:\n" if args.native_reference else b""
    files["S/Startup-Sequence"] = prefix + b"C:CopperGui\nSYS:MUICalc/muicalc\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(args.base, args.output)
    device = BlkDevFactory().open(str(args.output))
    volume = ADFSVolume(device)
    try:
        volume.open()
        before = inventory(volume)
        for path, data in files.items():
            put(volume, path, data)
        after = inventory(volume)
        expected = {p.casefold(): h for p, h in before.items()}
        expected.update({p.casefold(): sha(data) for p, data in files.items()})
        if {p.casefold(): h for p, h in after.items()} != expected:
            raise ValueError("Unexpected changes to prerequisite files")
    finally:
        volume.close()
        device.close()
    receipt = {
        "kind": "controlled-mui-calc-copperos-development-library",
        "runtimeQualified": False,
        "archiveSha256": sha(archive), "baseSha256": sha(base),
        "sha256": sha(args.output.read_bytes()), "library": manifest,
        "libraryAlias": ("Filename alias only; Resident identity/version unchanged"
                         if args.library_name != "muimaster.library" else "No alias"),
        "nativeReference": args.native_reference,
        "startup": files["S/Startup-Sequence"].decode("ascii"),
        "installedFiles": [{"path": p, "bytes": len(b), "sha256": sha(b)} for p, b in files.items()],
        "allGuestFiles": after,
    }
    Path(str(args.output) + ".json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(json.dumps({"sha256": receipt["sha256"], "packageFiles": 7,
                      "librarySha256": sha(library), "runtimeQualified": False}))


if __name__ == "__main__":
    main()
