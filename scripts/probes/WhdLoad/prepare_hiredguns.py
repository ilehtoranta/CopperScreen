"""Prepare NEW disposable WHDLoad Hired Guns shared-folder fixtures.

Requires user-supplied Workbench 3.1 and the five Hired Guns DOS disk ZIPs/ADFs,
plus the official WHDLoad and HiredGuns.lha packages. No media is distributed.
Install the Python lhafile package before running. The existing checked DOS
extractor reads the disks; the file selections follow Install-HiredGuns 1.0.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys


def unpack_lha(source, destination):
    import lhafile

    archive = lhafile.Lhafile(str(source))
    for entry in archive.infolist():
        parts = entry.filename.replace("\\", "/").split("/")
        if any(p in ("", ".", "..") or ":" in p for p in parts):
            raise ValueError(f"Unsafe archive name: {entry.filename!r}")
        target = destination.joinpath(*parts)
        if entry.compress_type == b"-lhd-":
            target.mkdir(parents=True, exist_ok=True)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open("xb") as output:
                output.write(archive.read(entry.filename))


def merge_tree(source, destination):
    """Merge data and per-entry metadata when multiple disks share a drawer."""
    destination.mkdir(parents=True, exist_ok=True)
    for entry in source.iterdir():
        if entry.name == ".copperfs.json":
            continue
        target = destination / entry.name
        if entry.is_dir():
            merge_tree(entry, target)
        else:
            shutil.copy2(entry, target)
    metadata = source / ".copperfs.json"
    if metadata.exists():
        output = destination / metadata.name
        existing = json.loads(output.read_text()) if output.exists() else {"Version": 1, "Entries": []}
        entries = {e["Name"].lower(): e for e in existing["Entries"]}
        entries.update({e["Name"].lower(): e for e in json.loads(metadata.read_text())["Entries"]})
        output.write_text(json.dumps({"Version": 1, "Entries": list(entries.values())}, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workbench", type=Path, required=True)
    parser.add_argument("--disk", type=Path, action="append", required=True, help="Repeat for disks 1 through 5, in order")
    parser.add_argument("--whdload", type=Path, required=True)
    parser.add_argument("--installer", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if len(args.disk) != 5:
        parser.error("Supply exactly five --disk inputs in disk-number order.")
    sources = [args.workbench, *args.disk, args.whdload, args.installer]
    for source in sources:
        if not source.is_file():
            parser.error(f"Missing input: {source}")
    root = args.output.resolve()
    root.mkdir(parents=True, exist_ok=False)
    extractor = Path(__file__).resolve().parents[2] / "prepare-host-directory-native.py"

    def extract(source, target):
        subprocess.run([sys.executable, str(extractor), str(source.resolve()), str(target)], check=True)

    system = root / "system"
    extract(args.workbench, system)
    unpack_lha(args.whdload, root / "whdload-package")
    unpack_lha(args.installer, root / "game-installer")
    whdload = next(p for p in (root / "whdload-package").rglob("WHDLoad") if p.is_file())
    shutil.copy2(whdload, system / "C/WHDLoad")
    slave = next((root / "game-installer").rglob("HiredGuns.slave"))
    game = system / "Games/HiredGuns"
    shutil.copytree(slave.parent, game)
    data = game / "game"
    data.mkdir()
    for number, source in enumerate(args.disk, 1):
        disk = root / f"disk{number}"
        extract(source, disk)
        if not (disk / f"Disk{number}").is_file():
            raise ValueError(f"Input {number} is not the expected Hired Guns disk")
        for entry in disk.iterdir():
            name = entry.name.lower()
            if (name == ".copperfs.json" or re.fullmatch(r"disk.*", name)
                    or (number in (1, 3) and re.fullmatch(r"hard.*", name))
                    or (number == 1 and (name in ("c", "devs", "libs", "s") or name.endswith(".info")))
                    or (number == 5 and name.endswith(".info"))):
                continue
            target = data / entry.name
            if entry.is_dir():
                merge_tree(entry, target)
            else:
                shutil.copy2(entry, target)
    (system / "S/Startup-Sequence").write_text(
        'FailAt 21\nMakeDir RAM:ENV\nMakeDir RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\n'
        'Stack 16384\nEcho >SYS:launched.txt "launching Hired Guns"\nCD SYS:Games/HiredGuns\n'
        'WHDLoad Slave=HiredGuns.slave Data=game SavePath=SYS:Saves SaveDir=HiredGuns Preload NoNetwork NoReq SplashDelay=0 QuitKey=89 >SYS:whdload-output.txt\n'
        'If WARN\nEcho >SYS:whdload-failed.txt "WHDLoad failed"\nElse\n'
        'Echo >SYS:returned.txt "WHDLoad returned successfully"\nEndIf\nLoadWB\nEndCLI >NIL:\n',
        encoding="latin1", newline="\n")
    # Hired Guns writes SavedGames/TrackNN. WHDLoad's standard SavePath mode
    # creates its missing subdirectories when the deferred writes are flushed.
    (system / "Saves").mkdir()
    manifest = [{"path": str(p.resolve()), "bytes": p.stat().st_size,
                 "sha256": hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sources]
    (root / "inputs.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(system)


if __name__ == "__main__":
    main()
