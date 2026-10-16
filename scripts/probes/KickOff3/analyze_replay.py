"""Summarize field-level diagnostics and compare native replay captures."""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ranges(frames):
    result = []
    for frame in frames:
        if result and frame == result[-1][1] + 1:
            result[-1][1] = frame
        else:
            result.append([frame, frame])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trace", type=Path, required=True)
    parser.add_argument("--normal", type=Path, required=True)
    parser.add_argument("--scalar", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    with args.trace.open(encoding="utf-8") as stream:
        rows = list(csv.reader(stream))
    rasters = {int(row[1]): int(row[2]) for row in rows if row and row[0] == "R"}
    fields = {int(row[1]): row for row in rows if row and row[0] == "F"}
    if len(rasters) != sum(row[0] == "R" for row in rows if row) or len(fields) != sum(row[0] == "F" for row in rows if row):
        raise ValueError("Duplicate field records; inspect the trace before drawing conclusions.")
    if sorted(rasters) != list(range(8801, 15001)) or sorted(fields) != list(range(6001, 15001)):
        raise ValueError("Incomplete or unexpected field coverage; do not report a successful complete replay.")
    blank_ranges = ranges(sorted(frame for frame, lit in rasters.items() if lit == 0))
    palette_changes = []
    for start, end in blank_ranges:
        changes = [row for row in rows if row and row[0] == "W" and start <= int(row[1]) <= end + 1 and 0x180 <= int(row[7], 16) <= 0x1BE]
        palette_changes.append({"blankFields": [start, end], "writes": changes})
    normal = {file.name: sha256(file) for file in args.normal.iterdir() if file.is_file()}
    scalar = {file.name: sha256(file) for file in args.scalar.iterdir() if file.is_file()}
    mismatches = sorted(name for name in normal.keys() | scalar.keys() if normal.get(name) != scalar.get(name))
    snapshots = [json.loads(file.read_text()) for file in sorted(args.normal.glob("frame-*.json"))]
    if [row["frame"] for row in snapshots] != [1] + list(range(100, 15001, 100)):
        raise ValueError("Expected completed 15,000-field native captures at the declared interval.")
    before_selection = (args.normal / "frame-009500.chipram").read_bytes()
    cursor_x, cursor_y = struct.unpack_from(">HH", before_selection, 0x95106)
    if before_selection[0x962B0] != 0 or not (84 <= cursor_x // 2 < 156 and 165 <= cursor_y < 181):
        raise ValueError("Input did not place the cursor inside the supplied guest's Practice hit rectangle.")
    menu_ids = {row["frame"]: (args.normal / f"frame-{row['frame']:06d}.chipram").read_bytes()[0x962B0]
                for row in snapshots if row["frame"] >= 10000}
    if any(menu_id != 0x15 for menu_id in menu_ids.values()):
        raise ValueError("Practice menu identity was not retained through the declared idle replay.")
    summary = {
        "schemaVersion": 1,
        "traceSha256": sha256(args.trace),
        "fieldCoverage": [min(fields), max(fields)],
        "rasterCoverage": [min(rasters), max(rasters)],
        "cursorBeforeSelection": [cursor_x, cursor_y],
        "practiceMenuId": "15",
        "practiceMenuSnapshots": len(menu_ids),
        "blackFieldRanges": blank_ranges,
        "lowRasterFieldRangesBelow128LitSamples": ranges(sorted(frame for frame, lit in rasters.items() if lit < 128)),
        "bitplaneFetchCountRange": [min(int(row[6]) for row in fields.values()), max(int(row[6]) for row in fields.values())],
        "nonzeroFetchCountRange": [min(int(row[7]) for row in fields.values()), max(int(row[7]) for row in fields.values())],
        "normalCaptureFiles": len(normal), "scalarCaptureFiles": len(scalar),
        "captureMismatches": mismatches,
        "unsupportedSnapshots": [row["frame"] for row in snapshots if row["unsupported"] or row["videoUnsupported"]],
        "finalState": snapshots[-1],
        "paletteChangesDuringBlankRanges": palette_changes,
        "captureHashes": normal,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in summary.items() if key not in {"captureHashes", "paletteChangesDuringBlankRanges", "finalState"}}, indent=2))
    if mismatches or summary["unsupportedSnapshots"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
