#!/usr/bin/env python3
"""Bounded native replay regression checks. Python 3.10+, standard library only.

These captures are correctness diagnostics, never throughput measurements or a
hardware oracle. Baselines require a separate, explicit milestone review.
"""
from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import html
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = ROOT / "CopperScreen.Lightweight.Tests/Workloads/native-regression-v1.json"
DEFAULT_RUNNER = ROOT / "CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0"
ARTIFACTS = ("json", "chipram", "slowram", "bmp", "pcm")
SUMMARY_KEYS = ("engine", "workload", "warmup", "frames", "completed", "cycle", "cpu",
                "hardware", "output", "pixels", "audioSamples", "pcm", "adf", "unsupported")
BOUNDARY = ("Bounded replay regression evidence. Matching scalar execution is a control, "
            "not independent hardware proof. Captured PCM covers checkpoint fields. "
            "Elapsed time includes diagnostics and is not a performance measurement.")


class InvalidInput(ValueError):
    pass


class UnavailableMedia(FileNotFoundError):
    pass


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def file_hash(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper() if hasattr(hashlib, "file_digest") else digest(stream.read())


def object_hash(value):
    return digest(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode())


def require_hash(actual, expected, subject):
    if not isinstance(expected, str) or not re.fullmatch(r"[A-Fa-f0-9]{64}", expected):
        raise InvalidInput(f"Missing or malformed SHA-256 for {subject}")
    if actual != expected.upper():
        raise InvalidInput(f"Identity mismatch: {subject}; expected {expected}, got {actual}")


def under(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise InvalidInput(f"Path escapes its declared root: {relative}")
    return path


def load_manifest(path):
    manifest = read_json(path)
    if manifest.get("schemaVersion") != 1 or manifest.get("profile") != "pal-ocs-a500-68000-512k-chip-512k-slow-ks13":
        raise InvalidInput("Unsupported manifest schema or machine profile")
    ids = []
    for case in manifest["cases"]:
        case_id = case["id"]
        if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", case_id) or case_id in ids:
            raise InvalidInput(f"Invalid or duplicate case id: {case_id}")
        ids.append(case_id)
        for key in ("frames", "captureInterval"):
            if type(case[key]) is not int or case[key] <= 0:
                raise InvalidInput(f"{case_id}: {key} must be a positive integer")
        if not case.get("milestones"):
            raise InvalidInput(f"{case_id}: declare reviewable milestones")
        for milestone in case["milestones"]:
            frame = milestone["frame"]
            if type(frame) is not int or frame not in expected_frames(case) or not milestone.get("description"):
                raise InvalidInput(f"{case_id}: milestone must name a captured field and describe its scope")
    if not ids:
        raise InvalidInput("The suite must contain at least one replay")
    return manifest


def expected_frames(case):
    return sorted({1, case["frames"], *range(case["captureInterval"], case["frames"] + 1, case["captureInterval"])})


def resolve_media(record, media_root):
    """Verify archive and uncompressed entry; leave the user's media untouched."""
    path = under(media_root, record["path"])
    try:
        actual = file_hash(path)
    except FileNotFoundError as error:
        raise UnavailableMedia(str(error)) from error
    selector = str(path)
    if "entry" in record:
        require_hash(actual, record["archiveSha256"], str(path))
        with zipfile.ZipFile(path) as archive:
            matches = [e for e in archive.infolist() if e.filename == record["entry"]]
            if len(matches) != 1:
                raise InvalidInput(f"ZIP entry missing or ambiguous: {path}#/{record['entry']}")
            data = archive.read(matches[0])
        actual = digest(data)
        selector += "#/" + record["entry"]
        size = len(data)
    else:
        size = path.stat().st_size
    require_hash(actual, record["sha256"], selector)
    if size != record["bytes"]:
        raise InvalidInput(f"Unexpected media length: {selector}")
    return selector


def prepare_case(case, manifest, media_root, rom_hash):
    script_path = under(ROOT, case["script"])
    require_hash(digest(script_path.read_bytes().replace(b"\r\n", b"\n")), case["scriptSha256Lf"], str(script_path))
    entries = read_json(script_path)
    if not isinstance(entries, list):
        raise InvalidInput("Input script must be an array")
    selectors = {}
    used_media = {case["bootMedia"]}
    previous = -1
    semantic = []
    effective = []
    used_mounts = set()
    for entry in entries:
        entry = dict(entry)
        frame = entry.get("frame")
        if type(frame) is not int or frame < previous or frame < 0 or frame >= case["frames"]:
            raise InvalidInput(f"{case['id']}: script frames must be ordered within the replay")
        previous = frame
        portable = dict(entry)
        keys = [key for key in ("diskPath", "adfPath") if key in entry]
        if len(keys) > 1:
            raise InvalidInput("A mount must use exactly one media path property")
        if keys:
            key = keys[0]
            source = entry[key]
            if source not in case.get("mounts", {}):
                raise InvalidInput(f"Unbound scripted media: {source}")
            media_id = case["mounts"][source]
            used_mounts.add(source)
            used_media.add(media_id)
            portable[key] = {"media": media_id, "sha256": manifest["media"][media_id]["sha256"]}
            entry[key] = media_id  # Replaced only after every identity has been verified.
        semantic.append(portable)
        effective.append(entry)
    if used_mounts != set(case.get("mounts", {})):
        raise InvalidInput(f"{case['id']}: unused media binding in manifest")
    for media_id in sorted(used_media):
        selectors[media_id] = resolve_media(manifest["media"][media_id], media_root)
    for entry in effective:
        for key in ("diskPath", "adfPath"):
            if key in entry:
                entry[key] = selectors[entry[key]]
    contract = {"profile": manifest["profile"], "romSha256": rom_hash,
                "bootMedia": manifest["media"][case["bootMedia"]]["sha256"],
                "script": semantic, "frames": case["frames"], "captureInterval": case["captureInterval"],
                "captureFormat": "native-boot-probe-extended-v1", "wideOutput": True, "drives": 1}
    return {"contractSha256": object_hash(contract), "contract": contract,
            "boot": selectors[case["bootMedia"]], "input": effective,
            "media": {key: manifest["media"][key] for key in sorted(used_media)}}


def parse_summary(log):
    lines = [line for line in log.splitlines() if line.startswith("engine=lightweight-a500 ")]
    if len(lines) != 1:
        raise InvalidInput("Runner did not emit exactly one final summary")
    fields = dict(re.findall(r"(\w+)=([^ ]+)", lines[0]))
    if any(key not in fields for key in SUMMARY_KEYS):
        raise InvalidInput("Incomplete runner summary")
    return {key: fields[key] for key in SUMMARY_KEYS}


def inventory(directory, case):
    files = {}
    frames = expected_frames(case)
    expected = {f"frame-{frame:06}.{ext}" for frame in frames for ext in ARTIFACTS}
    actual = {p.name for p in directory.glob("frame-*.*")}
    if actual != expected:
        raise InvalidInput(f"Capture set mismatch; missing={sorted(expected - actual)[:5]}, unexpected={sorted(actual - expected)[:5]}")
    for frame in frames:
        stem = f"frame-{frame:06}"
        state = read_json(directory / (stem + ".json"))
        if state.get("frame") != frame or state.get("unsupported") is not None or state.get("videoUnsupported") is not None:
            raise InvalidInput(f"Invalid or unsupported state at field {frame}")
        if (state.get("width"), state.get("height")) != (908, 313):
            raise InvalidInput(f"Unexpected framebuffer dimensions at field {frame}")
        for ext in ARTIFACTS:
            path = directory / (stem + "." + ext)
            size = path.stat().st_size
            if ext in ("chipram", "slowram") and size != 524288:
                raise InvalidInput(f"Incomplete memory capture: {path.name}")
            if ext == "bmp" and size != 54 + 908 * 313 * 4:
                raise InvalidInput(f"Incomplete image capture: {path.name}")
            if ext == "pcm" and (size == 0 or size % 4):
                raise InvalidInput(f"Incomplete stereo PCM capture: {path.name}")
            files[path.name] = {"sha256": file_hash(path), "bytes": size}
    return files


def compare(expected, actual):
    changes = []
    for name in sorted(set(expected["files"]) | set(actual["files"])):
        if expected["files"].get(name) != actual["files"].get(name):
            changes.append({"file": name, "expected": expected["files"].get(name), "actual": actual["files"].get(name)})
    for key in SUMMARY_KEYS:
        if expected["summary"].get(key) != actual["summary"].get(key):
            changes.append({"summary": key, "expected": expected["summary"].get(key), "actual": actual["summary"].get(key)})
    return changes


def add_difference_details(changes, reference, current):
    """Inspect the first changed file of each kind, only with verified reference bytes."""
    inspected = set()
    for change in changes:
        name = change.get("file")
        if not name:
            continue
        extension = Path(name).suffix
        left, right = reference / name, current / name
        if extension in inspected or not left.is_file() or not right.is_file():
            continue
        expected = change.get("expected") or {}
        if file_hash(left) != expected.get("sha256"):
            continue
        inspected.add(extension)
        if extension == ".json":
            before, after = read_json(left), read_json(right)
            change["stateFields"] = {key: {"expected": before.get(key), "actual": after.get(key)}
                                     for key in sorted(set(before) | set(after)) if before.get(key) != after.get(key)}
        elif extension in (".chipram", ".slowram", ".pcm"):
            before, after = left.read_bytes(), right.read_bytes()
            offsets = [i for i, (a, b) in enumerate(zip(before, after)) if a != b]
            if offsets:
                offset = offsets[0]
                change["firstByteOffset"] = offset
                change["changedBytes"] = len(offsets) + abs(len(before) - len(after))
                change["expectedBytes"] = before[offset:offset + 16].hex(" ")
                change["actualBytes"] = after[offset:offset + 16].hex(" ")


def run_process(command, log_path, timeout, cwd=ROOT):
    start = time.monotonic()
    with log_path.open("w", encoding="utf-8") as log:
        try:
            process = subprocess.run(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT,
                                     timeout=timeout, check=False)
        except subprocess.TimeoutExpired:
            return {"status": "TIMEOUT", "elapsedSeconds": round(time.monotonic() - start, 2), "command": command}
    return {"status": "FINISHED" if process.returncode == 0 else "FAILED",
            "exitCode": process.returncode, "elapsedSeconds": round(time.monotonic() - start, 2), "command": command}


def execute_mode(case, prepared, mode, case_dir, runner, dotnet, rom, timeout):
    directory = case_dir / mode
    directory.mkdir()
    command = [dotnet, str(runner / "CopperMod.Amiga.Lightweight.Runner.dll"), "--rom", str(rom),
               "--disk", prepared["boot"], "--input-script", str(case_dir / "input.json"),
               "--frames", str(case["frames"]), "--wide-output", "--boot-probe", str(directory),
               "--boot-probe-interval", str(case["captureInterval"]), "--boot-probe-extended"]
    if mode == "scalar":
        command.append("--scalar-cpu")
    result = run_process(command, case_dir / (mode + ".log"), timeout)
    if result["status"] != "FINISHED":
        return result
    try:
        result["summary"] = parse_summary((case_dir / (mode + ".log")).read_text(encoding="utf-8-sig"))
        summary = result["summary"]
        if summary["completed"] != str(case["frames"]) or summary["frames"] != str(case["frames"]) or summary["warmup"] != "0" or summary["unsupported"] != "none" or summary["pcm"] != "real":
            raise InvalidInput("Runner did not finish the declared native PCM workload")
        result["files"] = inventory(directory, case)
        result["status"] = "CAPTURED"
    except (ValueError, KeyError, OSError) as error:
        result.update(status="INVALID", error=str(error))
    return result


def exit_code(report):
    statuses = [case["status"] for case in report["cases"]]
    if any(s in ("FAILED", "TIMEOUT", "INVALID", "REGRESSION", "PARITY_FAILURE") for s in statuses):
        return 1
    if report.get("complete") is False or ("selectedCases" in report and
            set(report["selectedCases"]) != {case["id"] for case in report["cases"]}):
        return 2
    return 0 if statuses and all(s == "PASS" for s in statuses) else 2


def render_report(report, output):
    esc = lambda value: html.escape(str(value), quote=True)
    cards = []
    for case in report["cases"]:
        case_id = case["id"]
        status = case["status"]
        changes = case.get("baselineChanges", []) or case.get("parityChanges", [])
        details = esc(case.get("error", ""))
        if changes:
            first = changes[0]
            details += "<p>First difference: <code>" + esc(first.get("file", "summary: " + first.get("summary", ""))) + "</code></p>"
            details += "<details><summary>Comparison details</summary><pre>" + esc(json.dumps(changes[:20], indent=2)) + "</pre></details>"
        milestones = []
        for milestone in case.get("milestones", []):
            name = f"{case_id}/normal/frame-{milestone['frame']:06}.bmp"
            if (output / name).is_file():
                milestones.append(f'<figure><a href="{esc(name)}"><img loading="lazy" src="{esc(name)}" alt="{esc(milestone["description"])}"></a><figcaption>Field {milestone["frame"]:,}: {esc(milestone["description"])}</figcaption></figure>')
        # Same-build scalar comparison is always locally available. Baseline image
        # data may be supplied by --reference-run; hashes alone cannot display it.
        image_change = next((x for x in changes if x.get("file", "").endswith(".bmp")), None)
        if image_change:
            image_name = image_change["file"]
            reference = "reference" if case.get("baselineChanges") else "scalar"
            left = f"{case_id}/{reference}/{image_name}"
            right = f"{case_id}/normal/{image_name}"
            if (output / left).is_file() and (output / right).is_file():
                # Data URLs keep canvas reads available when report.html is
                # opened directly from disk (file origins otherwise taint it).
                before = "data:image/bmp;base64," + base64.b64encode((output / left).read_bytes()).decode("ascii")
                after = "data:image/bmp;base64," + base64.b64encode((output / right).read_bytes()).decode("ascii")
                details += f'<div class="comparison"><figure><img src="{before}" alt="Reference field"><figcaption>Reference</figcaption></figure><figure><img src="{after}" alt="Current field"><figcaption>Current</figcaption></figure><figure><canvas aria-label="Changed pixels"></canvas><figcaption class="difference-label">Loading pixel comparison…</figcaption></figure></div>'
        cards.append(f'<article class="{esc(status)}"><div class="heading"><h2>{esc(case["title"])}</h2><strong>{esc(status)}</strong></div><p>{esc(case.get("scope", ""))}</p>{details}<div class="gallery">{"".join(milestones)}</div><p><a href="{esc(case_id)}/normal.log">Normal log</a> · <a href="{esc(case_id)}/scalar.log">Scalar log</a></p></article>')
    counts = {status: sum(c["status"] == status for c in report["cases"]) for status in sorted({c["status"] for c in report["cases"]})}
    document = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>CopperScreen native regression</title>
<style>body{background:#101923;color:#e7edf4;font:16px/1.5 system-ui;margin:0 auto;max-width:1280px;padding:32px}h1,h2{line-height:1.2}h1{font-size:32px;margin-bottom:8px}h2{font-size:22px}a{color:#92d8ff}article{background:#1a2734;border-left:5px solid #e1b768;border-radius:8px;margin:24px 0;padding:20px}.PASS{border-color:#69d6aa}.REGRESSION,.PARITY_FAILURE,.FAILED,.INVALID,.TIMEOUT{border-color:#ff8e87}.heading{display:flex;align-items:center;justify-content:space-between;gap:20px}.heading strong{font-size:14px}figure{margin:0}img{width:100%;height:auto;background:#000;image-rendering:pixelated}figcaption{font-size:14px;padding:8px 0 16px;color:#c4d1df}.gallery,.comparison{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:16px}pre{overflow:auto;background:#101923;padding:16px;font-size:13px}.note{color:#bdcbd9;max-width:950px}code{overflow-wrap:anywhere}</style>
<h1>CopperScreen native regression</h1>'''
    completion = "Complete" if report.get("complete", True) else "Incomplete — running or interrupted"
    document += f'<p>{esc(completion)} · {esc(report["createdUtc"])} · {esc(" · ".join(f"{v} {k}" for k,v in counts.items()))}</p><p class="note">{esc(BOUNDARY)}</p><p class="note">Milestone captions describe the reviewed baseline. A changed or unreviewed run requires inspection before claiming the same progress.</p><a href="report.json">Machine-readable evidence</a>' + "".join(cards)
    document += '''<style>canvas{width:100%;height:auto;image-rendering:pixelated}</style><script>
for (const group of document.querySelectorAll('.comparison')) {
  const images = [...group.querySelectorAll('img')], canvas = group.querySelector('canvas');
  Promise.all(images.map(img => img.decode())).then(() => {
    const [a,b] = images;
    if (a.naturalWidth !== b.naturalWidth || a.naturalHeight !== b.naturalHeight) throw Error('Image dimensions differ');
    canvas.width = a.naturalWidth; canvas.height = a.naturalHeight;
    const ctx = canvas.getContext('2d', {willReadFrequently:true});
    ctx.drawImage(a,0,0); const left = ctx.getImageData(0,0,canvas.width,canvas.height);
    ctx.drawImage(b,0,0); const right = ctx.getImageData(0,0,canvas.width,canvas.height);
    let changed = 0;
    for (let i=0;i<left.data.length;i+=4) {
      const different = left.data[i]!==right.data[i] || left.data[i+1]!==right.data[i+1] || left.data[i+2]!==right.data[i+2];
      if (different) changed++;
      left.data[i]=different?255:0; left.data[i+1]=different?70:0; left.data[i+2]=different?120:0; left.data[i+3]=255;
    }
    ctx.putImageData(left,0,0); group.querySelector('.difference-label').textContent = changed.toLocaleString()+' changed RGB pixels (pink)';
  }).catch(error => { group.querySelector('.difference-label').textContent = 'Pixel comparison unavailable: '+error.message; });
}
</script></html>'''
    (output / "report.html").write_text(document, encoding="utf-8")


def save_report(report, output):
    report["exitCode"] = exit_code(report)
    write_json(output / "report.json", report)
    render_report(report, output)


def run_suite(args):
    manifest = load_manifest(args.manifest)
    selected = set(args.cases.split(",")) if args.cases else {c["id"] for c in manifest["cases"]}
    if not selected or selected - {c["id"] for c in manifest["cases"]}:
        raise InvalidInput("Unknown case selection; use list to see valid case ids")
    baseline = read_json(args.baseline) if args.baseline else None
    if baseline is not None and (baseline.get("schemaVersion") != 1 or baseline.get("kind") != "reviewed-native-regression-baseline"):
        raise InvalidInput("Not a reviewed native regression baseline")
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    if args.build:
        build = run_process([args.dotnet, "build", "CopperScreen.slnx", "-c", "Release"], output / "build.log", 600)
        if build["status"] != "FINISHED":
            raise InvalidInput(f"Production build {build['status']}; see {output / 'build.log'}")
    source_runner = Path(args.runner).resolve()
    runner = output / "runner"
    shutil.copytree(source_runner, runner)
    if args.reference_engine:
        shutil.copy2(args.reference_engine, runner / "CopperMod.Amiga.Lightweight.dll")
    if not (runner / "CopperMod.Amiga.Lightweight.Runner.dll").is_file():
        raise InvalidInput("Runner missing; build the production solution or pass --build")
    binaries = {p.relative_to(runner).as_posix(): file_hash(p) for p in sorted(runner.rglob("*")) if p.is_file()}
    write_json(output / "manifest.json", manifest)
    report = {"schemaVersion": 1, "createdUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scope": BOUNDARY, "manifestSha256": file_hash(args.manifest), "binaries": binaries,
              "runnerSource": str(source_runner), "referenceEngine": args.reference_engine,
              "baselineSha256": file_hash(args.baseline) if args.baseline else None,
              "runtimeInfo": subprocess.check_output([args.dotnet, "--info"], text=True),
              "harnessSha256": file_hash(__file__), "selectedCases": sorted(selected), "complete": False, "cases": []}
    save_report(report, output)
    rom = Path(args.rom).resolve()
    rom_error = None
    try:
        rom_hash = file_hash(rom)
        require_hash(rom_hash, manifest["romSha256"], str(rom))
        report["romSha256"] = rom_hash
    except FileNotFoundError as error:
        rom_error = UnavailableMedia(str(error))
    except (OSError, InvalidInput) as error:
        rom_error = error
    for case in manifest["cases"]:
        if case["id"] not in selected:
            continue
        item = {key: case[key] for key in ("id", "title", "scope", "milestones")}
        report["cases"].append(item)
        case_dir = output / case["id"]
        case_dir.mkdir()
        try:
            if rom_error is not None:
                raise rom_error
            prepared = prepare_case(case, manifest, Path(args.media_root).resolve(), rom_hash)
            item.update(contractSha256=prepared["contractSha256"], contract=prepared["contract"], media=prepared["media"])
            item["sourceScriptSha256"] = file_hash(under(ROOT, case["script"]))
            write_json(case_dir / "input.json", prepared["input"])
            item["effectiveInputSha256"] = file_hash(case_dir / "input.json")
            expected = baseline["cases"].get(case["id"]) if baseline else None
            if expected and expected["contractSha256"] != item["contractSha256"]:
                raise InvalidInput("Replay contract differs from baseline; use a separately reviewed baseline")
            if expected:
                item["milestones"] = expected["milestones"]
            for mode in ("normal", "scalar"):
                print(f"[{case['id']}] {mode}: {case['frames']:,} fields", flush=True)
                item[mode] = execute_mode(case, prepared, mode, case_dir, runner, args.dotnet, rom, args.timeout)
                if item[mode]["status"] != "CAPTURED":
                    item.update(status=item[mode]["status"], error=item[mode].get("error", f"{mode} replay {item[mode]['status']}; see its log"))
                    break
            else:
                item["parityChanges"] = compare(item["scalar"], item["normal"])
                item["baselineChanges"] = compare(expected, item["normal"]) if expected else []
                item["status"] = "PARITY_FAILURE" if item["parityChanges"] else "REGRESSION" if item["baselineChanges"] else "PASS" if expected else "REVIEW_REQUIRED"
                if args.reference_run and expected:
                    reference = Path(args.reference_run) / case["id"] / "normal"
                    for change in item["baselineChanges"]:
                        name = change.get("file", "")
                        if name.endswith(".bmp") and (reference / name).is_file() and expected["files"].get(name, {}).get("sha256") == file_hash(reference / name):
                            destination = case_dir / "reference"
                            destination.mkdir(exist_ok=True)
                            shutil.copy2(reference / name, destination / name)
                            break
            # Media is preloaded by each runner; detect changed files between
            # preflight and completion without copying copyrighted inputs.
            if prepare_case(case, manifest, Path(args.media_root).resolve(), file_hash(rom))["contractSha256"] != item["contractSha256"]:
                raise InvalidInput("Replay inputs changed during execution")
        except UnavailableMedia as error:
            item.update(status="UNAVAILABLE", error=str(error))
        except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
            item.update(status="INVALID", error=str(error))
        print(f"[{case['id']}] {item['status']}" + (": " + item["error"] if item.get("error") else ""), flush=True)
        save_report(report, output)
    # Detect accidental edits to the frozen execution inputs during the run.
    if any(not (runner / name).is_file() or file_hash(runner / name) != value for name, value in binaries.items()):
        for item in report["cases"]:
            item.update(status="INVALID", error="Frozen runner changed during execution")
    report["complete"] = True
    save_report(report, output)
    print(f"Report: {output / 'report.html'}", flush=True)
    return exit_code(report)


def create_baseline(args):
    run_path = Path(args.run).resolve()
    report = read_json(run_path / "report.json")
    if report.get("complete") is False or ("selectedCases" in report and
            set(report["selectedCases"]) != {case["id"] for case in report["cases"]}):
        raise InvalidInput("Cannot baseline an incomplete suite")
    manifest = load_manifest(run_path / "manifest.json")
    review = read_json(args.review)
    if set(review) != {case["id"] for case in report["cases"]}:
        raise InvalidInput("Review must cover exactly the recorded cases")
    baseline = {"schemaVersion": 1, "kind": "reviewed-native-regression-baseline", "scope": BOUNDARY,
                "sourceReportSha256": file_hash(run_path / "report.json"), "binaries": report["binaries"],
                "runtimeInfo": report["runtimeInfo"], "cases": {}}
    for name, expected in report["binaries"].items():
        require_hash(file_hash(under(run_path / "runner", name)), expected, "frozen runner " + name)
    cases = {c["id"]: c for c in manifest["cases"]}
    for item in report["cases"]:
        case_id = item["id"]
        if item["status"] not in ("REVIEW_REQUIRED", "PASS") or item.get("parityChanges"):
            raise InvalidInput(f"{case_id}: cannot baseline an incomplete or divergent run")
        note = review[case_id]
        milestones = cases[case_id]["milestones"]
        if not note.get("evidence") or not note.get("assessment") or note.get("frames") != [m["frame"] for m in milestones]:
            raise InvalidInput(f"{case_id}: review must name evidence, assessment and every milestone frame")
        if "milestones" in note:
            reviewed = note["milestones"]
            if [m["frame"] for m in reviewed] != note["frames"] or any(not m.get("description") for m in reviewed):
                raise InvalidInput(f"{case_id}: reviewed captions must cover the same milestone frames")
            milestones = reviewed
        # Re-read authoritative files; a green report is not proof they still match.
        observations = {}
        for mode in ("normal", "scalar"):
            observed = {"files": inventory(run_path / case_id / mode, cases[case_id]),
                        "summary": parse_summary((run_path / case_id / (mode + ".log")).read_text(encoding="utf-8-sig"))}
            if compare(item[mode], observed):
                raise InvalidInput(f"{case_id}: {mode} evidence changed after reporting")
            observations[mode] = observed
        if compare(observations["normal"], observations["scalar"]):
            raise InvalidInput(f"{case_id}: recorded scalar parity is not supported by the captures")
        baseline["cases"][case_id] = {"contractSha256": item["contractSha256"], "contract": item["contract"],
                                      "files": item["normal"]["files"], "summary": item["normal"]["summary"],
                                      "review": note, "milestones": milestones}
    destination = Path(args.output)
    with destination.open("x", encoding="utf-8") as stream:
        stream.write(json.dumps(baseline, indent=2, ensure_ascii=False) + "\n")
    print(f"Created reviewed baseline: {destination}")
    return 0


def compare_run(args):
    """Recheck retained evidence without rerunning emulation or rewriting its report."""
    source = Path(args.run).resolve()
    recorded = read_json(source / "report.json")
    if recorded.get("complete") is False or ("selectedCases" in recorded and
            set(recorded["selectedCases"]) != {case["id"] for case in recorded["cases"]}):
        raise InvalidInput("Source suite is incomplete; preserve it and use a new run directory")
    manifest = load_manifest(source / "manifest.json")
    baseline = read_json(args.baseline)
    if baseline.get("schemaVersion") != 1 or baseline.get("kind") != "reviewed-native-regression-baseline":
        raise InvalidInput("Not a reviewed native regression baseline")
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    report = dict(recorded, cases=[], sourceRun=str(source), sourceReportSha256=file_hash(source / "report.json"),
                  createdUtc=dt.datetime.now(dt.timezone.utc).isoformat(), baselineSha256=file_hash(args.baseline))
    definitions = {case["id"]: case for case in manifest["cases"]}
    for old in recorded["cases"]:
        item = dict(old)
        case_id = item["id"]
        case = definitions[case_id]
        target = output / case_id
        target.mkdir()
        report["cases"].append(item)
        try:
            # A timeout or nonzero runner exit remains a failure even if a final
            # checkpoint happened to be written before the process stopped.
            for mode in ("normal", "scalar"):
                execution = item.get(mode, {})
                if execution.get("status") != "CAPTURED" or execution.get("exitCode") != 0:
                    raise InvalidInput(f"No complete successful {mode} execution in the source run")
                current = {"files": inventory(source / case_id / mode, case),
                           "summary": parse_summary((source / case_id / (mode + ".log")).read_text(encoding="utf-8-sig"))}
                # Any post-run evidence mutation is reported, never silently
                # accepted just because it also occurs in a modified baseline.
                if compare(execution, current):
                    raise InvalidInput(f"{mode} capture evidence changed after execution")
                item[mode] = dict(execution, **current)
                shutil.copy2(source / case_id / (mode + ".log"), target / (mode + ".log"))
            if object_hash(item["contract"]) != item["contractSha256"]:
                raise InvalidInput("Source replay contract hash is inconsistent")
            expected = baseline["cases"].get(case_id)
            if expected and expected["contractSha256"] != item["contractSha256"]:
                raise InvalidInput("Replay contract differs from baseline")
            if expected:
                item["milestones"] = expected["milestones"]
            item["parityChanges"] = compare(item["scalar"], item["normal"])
            item["baselineChanges"] = compare(expected, item["normal"]) if expected else []
            item["status"] = "PARITY_FAILURE" if item["parityChanges"] else "REGRESSION" if item["baselineChanges"] else "PASS" if expected else "REVIEW_REQUIRED"
            changes = item["baselineChanges"] or item["parityChanges"]
            reference = Path(args.reference_run).resolve() / case_id / "normal" if item["baselineChanges"] and args.reference_run else source / case_id / "scalar" if not item["baselineChanges"] else None
            if reference:
                add_difference_details(changes, reference, source / case_id / "normal")
            image_names = {f"frame-{m['frame']:06}.bmp" for m in item["milestones"]}
            difference_image = next((c for c in changes if c.get("file", "").endswith(".bmp")), None)
            if difference_image:
                image_names.add(difference_image["file"])
                if reference and (reference / difference_image["file"]).is_file() and file_hash(reference / difference_image["file"]) == (difference_image.get("expected") or {}).get("sha256"):
                    destination = target / ("reference" if item["baselineChanges"] else "scalar")
                    destination.mkdir()
                    shutil.copy2(reference / difference_image["file"], destination / difference_image["file"])
            (target / "normal").mkdir()
            for name in image_names:
                shutil.copy2(source / case_id / "normal" / name, target / "normal" / name)
        except (OSError, ValueError, KeyError) as error:
            # Preserve unavailable coverage rather than relabeling it a replay failure.
            item.update(status="UNAVAILABLE" if old["status"] == "UNAVAILABLE" else "INVALID", error=str(error))
        print(f"[{case_id}] {item['status']}", flush=True)
    for name, expected_hash in recorded["binaries"].items():
        if not (source / "runner" / name).is_file() or file_hash(source / "runner" / name) != expected_hash:
            for item in report["cases"]:
                item.update(status="INVALID", error="Frozen runner evidence changed")
            break
    save_report(report, output)
    print(f"Report: {output / 'report.html'}")
    return exit_code(report)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    listing = sub.add_parser("list", help="List replay scope and required media without running")
    listing.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    run = sub.add_parser("run", help="Freeze a runner, verify inputs, replay and compare")
    run.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    run.add_argument("--rom", required=True)
    run.add_argument("--media-root", required=True)
    run.add_argument("--output", required=True, help="Fresh output directory; existing evidence is never overwritten")
    run.add_argument("--runner", default=str(DEFAULT_RUNNER))
    run.add_argument("--dotnet", default="dotnet")
    run.add_argument("--build", action="store_true")
    run.add_argument("--baseline", type=Path)
    run.add_argument("--reference-run", help="Prior run directory providing hash-verified baseline images for visual comparison")
    run.add_argument("--reference-engine", help="Use an explicitly selected frozen reference engine with the capture runner")
    run.add_argument("--cases", help="Comma-separated case ids; omitted means the complete manifest")
    run.add_argument("--timeout", type=int, default=900, help="Wall-clock limit per replay mode, seconds (default: 900)")
    baseline = sub.add_parser("baseline", help="Create a new baseline from complete captures and an explicit milestone review")
    baseline.add_argument("--run", required=True)
    baseline.add_argument("--review", required=True)
    baseline.add_argument("--output", required=True)
    comparison = sub.add_parser("compare", help="Recheck retained captures against a baseline; preserve original reports")
    comparison.add_argument("--run", required=True)
    comparison.add_argument("--baseline", required=True)
    comparison.add_argument("--output", required=True)
    comparison.add_argument("--reference-run")
    args = parser.parse_args(argv)
    try:
        if args.command == "list":
            manifest = load_manifest(args.manifest)
            for case in manifest["cases"]:
                print(f"{case['id']:20} {case['frames']:>7,} fields  {case['title']}\n  {case['scope']}")
            return 0
        if args.command == "baseline":
            return create_baseline(args)
        if args.command == "compare":
            return compare_run(args)
        if args.timeout <= 0:
            raise InvalidInput("--timeout must be positive")
        return run_suite(args)
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        print(f"Native regression: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
