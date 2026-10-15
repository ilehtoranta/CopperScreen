"""Capture one replacement-ROM compatibility case on the existing native runner.

Build first: dotnet build CopperScreen.slnx -c Release
Example (paths are supplied locally, never downloaded):
  python scripts/probes/CopperStart/replay.py --rom path/to/CopperStart.rom \
    --disk path/to/game.zip --output artifacts/copperstart/game
Repeat with --scalar-cpu, or --firmware native --rom path/to/Kickstart.rom,
using a separate output directory. Inspect images and input responses before
recording boot, intro, menu or gameplay milestones. Completion is not a PASS.
This is diagnostic evidence, not a throughput/acceptance protocol.
"""

import argparse
import hashlib
import json
import re
import struct
import subprocess
import zipfile
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def media_identity(spec):
    filename, separator, selection = spec.partition("#/")
    path = Path(filename).resolve(strict=True)
    result = {"path": str(path), "sha256": digest(path)}
    if path.suffix.lower() == ".zip":
        with zipfile.ZipFile(path) as archive:
            entries = [e for e in archive.infolist() if not e.is_dir()
                       and Path(e.filename).suffix.lower() in
                       (".adf", ".adz", ".dms", ".ipf", ".scp")]
            if separator:
                entries = [e for e in entries if e.filename == selection]
            if len(entries) != 1:
                raise ValueError("Choose exactly one disk with archive.zip#/entry.adf")
            payload = archive.read(entries[0])
            result.update(entry=entries[0].filename, entryBytes=len(payload),
                          entrySha256=hashlib.sha256(payload).hexdigest())
    elif separator:
        raise ValueError("A #/ selector requires a ZIP archive")
    return result


def boot_receipt(path):
    """Private CopperStart receipt; takeover code may overwrite it."""
    ram = path.read_bytes()
    u32 = lambda address: struct.unpack_from(">I", ram, address)[0]
    control = u32(0x840)
    result = {"control": f"{control:08X}", "validControlRange": False}
    if not (0x2000 <= control <= len(ram) - 256 and control % 4 == 0):
        return result
    result.update(validControlRange=True, state=u32(control + 16),
                  attempts=u32(control + 20), lastBootReadError=u32(control + 24))
    request = u32(control + 4)
    if 0x2000 <= request <= len(ram) - 56 and request % 2 == 0:
        result["request"] = {
            "address": f"{request:08X}",
            "command": struct.unpack_from(">H", ram, request + 28)[0],
            "error": struct.unpack_from("b", ram, request + 31)[0],
            "actual": u32(request + 32), "length": u32(request + 36),
            "data": f"{u32(request + 40):08X}",
            "offset": u32(request + 44),
        }
    return result


def main():
    root = Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, required=True)
    parser.add_argument("--disk", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--firmware", choices=("copperstart", "native"), default="copperstart")
    parser.add_argument("--runner", type=Path, default=root / "CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll")
    parser.add_argument("--frames", type=int, default=3000)
    parser.add_argument("--interval", type=int, default=100)
    parser.add_argument("--slow-ram-kib", type=int, choices=(0, 512), default=0)
    parser.add_argument("--scalar-cpu", action="store_true")
    parser.add_argument("--input-script", type=Path)
    args = parser.parse_args()
    if args.frames <= 0 or args.interval <= 0:
        parser.error("frames and interval must be positive")
    rom = args.rom.resolve(strict=True)
    runner = args.runner.resolve(strict=True)
    media = media_identity(args.disk)
    output = args.output.resolve()
    # A fresh directory prevents stale captures being mistaken for this run.
    output.mkdir(parents=True, exist_ok=False)
    disk = media["path"] + ("#/" + media["entry"] if "entry" in media else "")
    command = ["dotnet", str(runner), "--rom", str(rom), "--disk", disk,
               "--frames", str(args.frames), "--slow-ram-kib", str(args.slow_ram_kib),
               "--boot-probe", str(output / "captures"),
               "--boot-probe-interval", str(args.interval), "--boot-probe-extended"]
    if args.scalar_cpu:
        command.append("--scalar-cpu")
    if args.input_script:
        command.extend(("--input-script", str(args.input_script.resolve(strict=True))))
    manifest = {
        "schemaVersion": 1, "firmware": args.firmware,
        "classification": "unreviewed", "command": command,
        "rom": {"path": str(rom), "sha256": digest(rom)}, "disk": media,
        "machine": {"cpu": "68000", "agnus": "8371", "denise": "8362",
                    "video": "PAL", "chipRamKiB": 512,
                    "slowRamKiB": args.slow_ram_kib, "fastRamKiB": 0, "drives": 1},
        "binaries": {p.name: digest(p) for p in (
            runner, runner.with_name("CopperMod.Amiga.Lightweight.dll"),
            runner.with_name("Copper68k.dll"), runner.with_name("CopperDisk.dll"))},
        "input": ({"path": str(args.input_script.resolve()),
                   "sha256": digest(args.input_script)} if args.input_script else None),
        "requestedFrames": args.frames, "captureInterval": args.interval,
        "receiptInterpretation": "Private bootstrap diagnostics; takeover code may overwrite them.",
    }
    manifest_path = output / "run.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    with (output / "runner.log").open("w", encoding="utf-8") as log:
        completed = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT)
    log = (output / "runner.log").read_text(encoding="utf-8")
    summaries = [line for line in log.splitlines() if line.startswith("engine=")]
    manifest["exitCode"] = completed.returncode
    manifest["summary"] = (dict(re.findall(r"(\w+)=([^\s]+)", summaries[-1]))
                           if summaries else None)
    snapshots = sorted((output / "captures").glob("frame-*.json"))
    expected = sorted({1, args.frames, *range(args.interval, args.frames + 1, args.interval)})
    states = [json.loads(p.read_text()) for p in snapshots]
    manifest["captureFrames"] = [s["frame"] for s in states]
    manifest["captureFiles"] = {
        p.name: digest(p) for p in sorted((output / "captures").glob("*")) if p.is_file()}
    expected_files = {f"frame-{frame:06d}.{extension}" for frame in expected
                      for extension in ("json", "bmp", "chipram", "slowram", "pcm")}
    manifest["completedReplay"] = (
        completed.returncode == 0 and len(summaries) == 1
        and manifest["summary"].get("completed") == str(args.frames)
        and manifest["summary"].get("unsupported") == "none"
        and manifest["captureFrames"] == expected
        and set(manifest["captureFiles"]) == expected_files
        and not any(s["unsupported"] or s["videoUnsupported"] for s in states))
    if args.firmware == "copperstart":
        manifest["bootReceipts"] = [
            {"frame": s["frame"], **boot_receipt(p.with_suffix(".chipram"))}
            for p, s in zip(snapshots, states)]
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"{manifest_path}: completedReplay={manifest['completedReplay']}; compatibility unreviewed")
    return 0 if manifest["completedReplay"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
