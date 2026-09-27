import argparse
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import warnings
from unittest import mock
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import native_regression as n


class NativeRegressionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.case = {"id": "demo", "title": "Demo", "scope": "Test fixture", "frames": 1,
                     "captureInterval": 1000, "milestones": [{"frame": 1, "description": "Test field"}]}

    def captures(self, directory):
        directory.mkdir(parents=True)
        n.write_json(directory / "frame-000001.json", {"frame": 1, "width": 908, "height": 313,
                                                       "unsupported": None, "videoUnsupported": None})
        for extension, size in (("chipram", 524288), ("slowram", 524288), ("bmp", 54 + 908 * 313 * 4), ("pcm", 4)):
            (directory / ("frame-000001." + extension)).write_bytes(bytes(size))
        summary = {key: "0" for key in n.SUMMARY_KEYS}
        summary.update(engine="lightweight-a500", frames="1", completed="1", pcm="real", unsupported="none")
        return {"files": n.inventory(directory, self.case), "summary": summary}

    def test_every_artifact_kind_and_summary_difference_are_detected(self):
        original = self.captures(self.root / "original")
        for ext in n.ARTIFACTS:
            with self.subTest(ext=ext):
                current = copy.deepcopy(original)
                name = "frame-000001." + ext
                current["files"][name]["sha256"] = "F" * 64
                self.assertEqual(n.compare(original, current)[0]["file"], name)
        current = copy.deepcopy(original)
        current["summary"]["output"] = "0xDEADBEEF"
        self.assertEqual(n.compare(original, current)[0]["summary"], "output")

    def test_missing_and_extra_captures_cannot_pass(self):
        original = self.captures(self.root / "captures")
        current = copy.deepcopy(original)
        del current["files"]["frame-000001.pcm"]
        self.assertEqual(len(n.compare(original, current)), 1)
        (self.root / "captures/frame-000001.pcm").unlink()
        with self.assertRaisesRegex(n.InvalidInput, "missing"):
            n.inventory(self.root / "captures", self.case)
        (self.root / "captures/frame-000001.pcm").write_bytes(bytes(4))
        (self.root / "captures/frame-000002.pcm").write_bytes(bytes(4))
        with self.assertRaisesRegex(n.InvalidInput, "unexpected"):
            n.inventory(self.root / "captures", self.case)

    def test_unsupported_snapshot_and_truncated_memory_are_rejected(self):
        directory = self.root / "captures"
        self.captures(directory)
        state = n.read_json(directory / "frame-000001.json")
        state["unsupported"] = "Unsupported mode"
        n.write_json(directory / "frame-000001.json", state)
        with self.assertRaisesRegex(n.InvalidInput, "unsupported"):
            n.inventory(directory, self.case)
        state["unsupported"] = None
        n.write_json(directory / "frame-000001.json", state)
        (directory / "frame-000001.slowram").write_bytes(bytes(12))
        with self.assertRaisesRegex(n.InvalidInput, "Incomplete memory"):
            n.inventory(directory, self.case)

    def test_requested_final_field_and_interval_are_both_required(self):
        case = {"frames": 18120, "captureInterval": 1000}
        self.assertEqual(n.expected_frames(case), [1, *range(1000, 18001, 1000), 18120])

    def test_runner_exit_without_a_complete_summary_is_not_success(self):
        for text in ("", "engine=lightweight-a500 completed=1", "engine=lightweight-a500\nengine=lightweight-a500"):
            with self.subTest(text=text), self.assertRaises(n.InvalidInput):
                n.parse_summary(text)

    def test_missing_media_and_wrong_identity_have_distinct_outcomes(self):
        record = {"path": "missing.adf", "bytes": 3, "sha256": n.digest(b"abc")}
        with self.assertRaises(n.UnavailableMedia):
            n.resolve_media(record, self.root)
        (self.root / "missing.adf").write_bytes(b"abd")
        with self.assertRaisesRegex(n.InvalidInput, "Identity mismatch"):
            n.resolve_media(record, self.root)

    def test_zip_checks_archive_and_exact_nested_entry(self):
        path = self.root / "disk set.zip"
        entry = "nested/Disk 2.adf"
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(entry, b"test media")
        record = {"path": path.name, "entry": entry, "archiveSha256": n.file_hash(path),
                  "sha256": n.digest(b"test media"), "bytes": 10}
        self.assertEqual(n.resolve_media(record, self.root), str(path) + "#/" + entry)
        record["sha256"] = "F" * 64
        with self.assertRaises(n.InvalidInput):
            n.resolve_media(record, self.root)
        record["sha256"] = n.digest(b"test media")
        record["entry"] = "Disk 2.adf"
        with self.assertRaisesRegex(n.InvalidInput, "missing or ambiguous"):
            n.resolve_media(record, self.root)

    def test_duplicate_zip_entries_are_rejected(self):
        path = self.root / "ambiguous.zip"
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("disk.adf", b"one")
                archive.writestr("disk.adf", b"two")
        record = {"path": path.name, "entry": "disk.adf", "archiveSha256": n.file_hash(path),
                  "sha256": n.digest(b"one"), "bytes": 3}
        with self.assertRaisesRegex(n.InvalidInput, "ambiguous"):
            n.resolve_media(record, self.root)

    def test_paths_cannot_escape_declared_media_root(self):
        with self.assertRaisesRegex(n.InvalidInput, "escapes"):
            n.under(self.root, "../outside.adf")

    def test_exit_codes_do_not_promote_skips_or_unreviewed_captures(self):
        for status, expected in (("PASS", 0), ("UNAVAILABLE", 2), ("REVIEW_REQUIRED", 2),
                                 ("INVALID", 1), ("FAILED", 1), ("TIMEOUT", 1),
                                 ("PARITY_FAILURE", 1), ("REGRESSION", 1)):
            with self.subTest(status=status):
                self.assertEqual(n.exit_code({"cases": [{"status": status}]}), expected)
        self.assertEqual(n.exit_code({"cases": [{"status": "PASS"}, {"status": "UNAVAILABLE"}]}), 2)
        self.assertEqual(n.exit_code({"cases": []}), 2)

    def test_partial_suite_cannot_report_success_or_supply_a_baseline(self):
        report = {"cases": [{"id": "one", "status": "PASS"}], "selectedCases": ["one", "two"], "complete": False}
        self.assertEqual(n.exit_code(report), 2)
        report["complete"] = True
        self.assertEqual(n.exit_code(report), 2)
        n.write_json(self.root / "report.json", report)
        args = argparse.Namespace(run=self.root)
        with self.assertRaisesRegex(n.InvalidInput, "incomplete suite"):
            n.create_baseline(args)
        with self.assertRaisesRegex(n.InvalidInput, "Source suite is incomplete"):
            n.compare_run(args)

    def test_running_suite_persists_partial_results_before_marking_complete(self):
        runner = self.root / "source-runner"
        runner.mkdir()
        (runner / "CopperMod.Amiga.Lightweight.Runner.dll").write_bytes(b"not executed")
        manifest = {"schemaVersion": 1, "profile": "pal-ocs-a500-68000-512k-chip-512k-slow-ks13",
                    "romSha256": "A" * 64, "cases": [self.case, dict(self.case, id="second")]}
        n.write_json(self.root / "manifest.json", manifest)
        args = argparse.Namespace(manifest=self.root / "manifest.json", cases=None, baseline=None,
                                  output=self.root / "results", build=False, runner=runner, reference_engine=None,
                                  rom=self.root / "absent.rom", dotnet="dotnet")
        snapshots = []
        with mock.patch.object(n.subprocess, "check_output", return_value="fixture"), \
             mock.patch.object(n, "save_report", side_effect=lambda r,o: snapshots.append(copy.deepcopy(r))):
            self.assertEqual(n.run_suite(args), 2)
        self.assertFalse(snapshots[0]["complete"])
        self.assertTrue(any(not r["complete"] and len(r["cases"]) == 1 for r in snapshots))
        self.assertTrue(snapshots[-1]["complete"])
        self.assertEqual(len(snapshots[-1]["cases"]), 2)

    def test_timeout_terminates_a_real_process_and_retains_output(self):
        log = self.root / "timeout.log"
        result = n.run_process([sys.executable, "-u", "-c", "import time; print('started'); time.sleep(30)"], log, .3)
        self.assertEqual(result["status"], "TIMEOUT")
        self.assertIn("started", log.read_text())

    def test_nonzero_process_exit_is_a_failure(self):
        result = n.run_process([sys.executable, "-c", "raise SystemExit(9)"], self.root / "exit.log", 5)
        self.assertEqual(result["status"], "FAILED")
        self.assertEqual(result["exitCode"], 9)

    def test_contract_survives_path_and_line_ending_changes(self):
        manifest = {"profile": "test", "media": {"disk": {"path": "disk.adf", "sha256": n.digest(b"abc"), "bytes": 3}}}
        (self.root / "disk.adf").write_bytes(b"abc")
        script = self.root / "input.json"
        script.write_bytes(b'[\n {"frame":0,"diskPath":"../old/disk.adf"}\n]\n')
        case = dict(self.case, script="input.json", scriptSha256Lf=n.file_hash(script), bootMedia="disk", mounts={"../old/disk.adf": "disk"})
        with mock.patch.object(n, "ROOT", self.root):
            original = n.prepare_case(case, manifest, self.root, "A" * 64)
            script.write_bytes(script.read_bytes().replace(b"\n", b"\r\n"))
            second = n.prepare_case(case, manifest, self.root, "A" * 64)
            self.assertEqual(original["contractSha256"], second["contractSha256"])
            case["mounts"] = {}
            with self.assertRaisesRegex(n.InvalidInput, "Unbound"):
                n.prepare_case(case, manifest, self.root, "A" * 64)

    def test_baseline_requires_review_and_rechecks_artifacts(self):
        manifest = {"schemaVersion": 1, "profile": "pal-ocs-a500-68000-512k-chip-512k-slow-ks13", "cases": [self.case]}
        n.write_json(self.root / "manifest.json", manifest)
        original = self.captures(self.root / "demo/normal")
        scalar = self.captures(self.root / "demo/scalar")
        log = " ".join(f"{key}={value}" for key, value in original["summary"].items())
        for mode in ("normal", "scalar"):
            (self.root / "demo" / (mode + ".log")).write_text(log)
        report = {"binaries": {}, "runtimeInfo": "fixture", "cases": [dict(self.case, status="REVIEW_REQUIRED",
                  parityChanges=[], normal=original, scalar=scalar, contractSha256="A" * 64, contract={})]}
        n.write_json(self.root / "report.json", report)
        n.write_json(self.root / "review.json", {"demo": {"frames": [1], "evidence": "fixture", "assessment": "Reviewed synthetic fixture"}})
        args = argparse.Namespace(run=self.root, review=self.root / "review.json", output=self.root / "baseline.json")
        n.create_baseline(args)
        self.assertEqual(n.read_json(args.output)["kind"], "reviewed-native-regression-baseline")
        with self.assertRaises(FileExistsError):
            n.create_baseline(args)
        args.output = self.root / "changed.json"
        (self.root / "demo/normal/frame-000001.pcm").write_bytes(b"\x01\x00\x00\x00")
        with self.assertRaisesRegex(n.InvalidInput, "evidence changed"):
            n.create_baseline(args)

    def test_report_escapes_titles_and_does_not_claim_performance(self):
        report = {"createdUtc": "fixture", "cases": [dict(self.case, title="<script>bad()</script>", status="UNAVAILABLE")]}
        n.render_report(report, self.root)
        text = (self.root / "report.html").read_text()
        self.assertNotIn("<script>bad()", text)
        self.assertIn("&lt;script&gt;", text)
        self.assertIn("not a performance measurement", text)

    def test_visual_comparison_embeds_its_verified_images_for_file_origin_canvas(self):
        for mode, data in (("normal", b"current fixture"), ("reference", b"reference fixture")):
            directory = self.root / "demo" / mode
            directory.mkdir(parents=True)
            (directory / "frame-000001.bmp").write_bytes(data)
        report = {"createdUtc": "fixture", "cases": [dict(self.case, status="REGRESSION",
                  baselineChanges=[{"file": "frame-000001.bmp"}])]}
        n.render_report(report, self.root)
        document = (self.root / "report.html").read_text()
        self.assertEqual(document.count('src="data:image/bmp;base64,'), 2)
        self.assertIn('aria-label="Changed pixels"', document)

    def test_checked_in_manifest_pins_existing_scripts_and_declares_all_media(self):
        manifest = n.load_manifest(n.DEFAULT_MANIFEST)
        for case in manifest["cases"]:
            script = n.under(n.ROOT, case["script"])
            n.require_hash(n.digest(script.read_bytes().replace(b"\r\n", b"\n")), case["scriptSha256Lf"], case["id"])
            for media_id in {case["bootMedia"], *case.get("mounts", {}).values()}:
                record = manifest["media"][media_id]
                self.assertRegex(record["sha256"], r"^[A-F0-9]{64}$")
                self.assertGreater(record["bytes"], 0)
                if "entry" in record:
                    self.assertRegex(record["archiveSha256"], r"^[A-F0-9]{64}$")

    def test_compare_command_detects_an_altered_expectation_and_evidence_mutation(self):
        source = self.root / "run"
        original = self.captures(source / "demo/normal")
        scalar = self.captures(source / "demo/scalar")
        for mode in ("normal", "scalar"):
            (source / "demo" / (mode + ".log")).write_text(" ".join(f"{k}={v}" for k,v in original["summary"].items()))
        manifest = {"schemaVersion": 1, "profile": "pal-ocs-a500-68000-512k-chip-512k-slow-ks13", "cases": [self.case]}
        n.write_json(source / "manifest.json", manifest)
        contract = {"frames": 1}
        item = dict(self.case, status="REVIEW_REQUIRED", contract=contract, contractSha256=n.object_hash(contract),
                    normal=dict(original, status="CAPTURED", exitCode=0), scalar=dict(scalar, status="CAPTURED", exitCode=0))
        n.write_json(source / "report.json", {"createdUtc": "fixture", "cases": [item], "binaries": {}})
        baseline = {"schemaVersion": 1, "kind": "reviewed-native-regression-baseline", "cases": {
                    "demo": dict(copy.deepcopy(original), contractSha256=n.object_hash(contract), milestones=self.case["milestones"])}}
        baseline_path = self.root / "baseline.json"
        n.write_json(baseline_path, baseline)
        args = argparse.Namespace(run=source, baseline=baseline_path, output=self.root / "matching", reference_run=None)
        self.assertEqual(n.compare_run(args), 0)
        baseline["cases"]["demo"]["files"]["frame-000001.pcm"]["sha256"] = "F" * 64
        n.write_json(baseline_path, baseline)
        args.output = self.root / "mismatch"
        self.assertEqual(n.compare_run(args), 1)
        comparison = n.read_json(args.output / "report.json")["cases"][0]
        self.assertEqual(comparison["status"], "REGRESSION")
        self.assertEqual(comparison["baselineChanges"][0]["file"], "frame-000001.pcm")
        (source / "demo/normal/frame-000001.pcm").write_bytes(b"\x01\x00\x00\x00")
        args.output = self.root / "tampered"
        self.assertEqual(n.compare_run(args), 1)
        self.assertEqual(n.read_json(args.output / "report.json")["cases"][0]["status"], "INVALID")

    def test_memory_difference_reports_first_offset_with_expected_bytes(self):
        left, right = self.root / "left", self.root / "right"
        expected, actual = self.captures(left), self.captures(right)
        data = bytearray((right / "frame-000001.chipram").read_bytes())
        data[123] = 0x42
        (right / "frame-000001.chipram").write_bytes(data)
        actual["files"] = n.inventory(right, self.case)
        differences = n.compare(expected, actual)
        n.add_difference_details(differences, left, right)
        self.assertEqual(differences[0]["firstByteOffset"], 123)
        self.assertEqual(differences[0]["changedBytes"], 1)
        self.assertTrue(differences[0]["actualBytes"].startswith("42"))


if __name__ == "__main__":
    unittest.main()
