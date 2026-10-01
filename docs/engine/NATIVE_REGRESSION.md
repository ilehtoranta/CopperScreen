# Repeatable native compatibility checks

The native regression suite runs six pinned PAL OCS A500 replays, checks normal
and scalar CPU execution, and compares both with a separately reviewed baseline.
It produces a browsable report with milestone images, logs, hashes and the first
differing captured file. It does not certify physical hardware behavior, complete
games, or host throughput.

The separate [ECS-required Final Fight replay](ECS_REQUIRED_MEDIA_2026-10-01.md)
uses 2 MiB Chip RAM and 8373 Denise. Its frozen input and normal/scalar evidence
do not alter this suite's OCS profile, manifest or reviewed fingerprints.

## Run the suite

Requirements: the .NET 10 SDK, Python 3.10 or newer, and the exact locally supplied
Kickstart/media identities in the [manifest](../../CopperScreen.Lightweight.Tests/Workloads/native-regression-v1.json).
There are no Python package dependencies. ROMs and game media are not included or
downloaded. The manifest's media paths are relative to `--media-root`; preserve
its `OK/` and `Team17/` subdirectories. ZIP entries are read directly, including
scripted disk swaps; extracting disk two into the repository is unnecessary.

From the repository root, one command builds the production solution, freezes its
runner and dependencies, verifies inputs and executes every case:

```powershell
python scripts/native_regression.py run --build --rom C:/Data/ROM/Kickstart_13.rom --media-root C:/Data/TestImages --baseline CopperScreen.Lightweight.Tests/Workloads/native-regression-baseline-v1.json --output artifacts/native-regression-new
```

Use the path to your Python executable if `python` is not on PATH. On systems
where it is named `python3`, substitute that command. Open `report.html` in the
output directory. Use a fresh output directory for each run; previous evidence
and baselines are never overwritten by these commands.

`list` prints the available replay IDs and scope. Add `--cases lotus3,lemmings`
to run a subset. `--timeout 900` is the default per-mode wall-clock limit;
increase it for a slower host. A timeout is a failed check, not a pass. Captures
require approximately 0.7 GiB for the complete normal/scalar suite, plus logs and
the small frozen runner. Timing and allocation totals in the runner logs include
capture work and must not be used as performance acceptance results.

| Replay | Fields per mode | Reviewed scope |
| --- | ---: | --- |
| Lotus III | 18,000 | Disk-two prompt, title and one-player/split-screen driving demos |
| Desert Dream | 72,000 | Two-disk main sequence through continuing closing credits |
| Tower Assault | 20,000 | Second-disk options menu and opening level |
| Alien Breed SE '92 | 16,000 | WORDSYNC boot, matching second disk, menu and credits |
| Super Cars II | 19,000 | Options, race one and bounded acceleration/steering sequence |
| Lemmings | 18,120 | Two-disk boot and level-one gameplay with the validated delayed input |

Each replay starts from cold boot in the fixed PAL OCS/68000 profile, 512 KiB
Chip RAM plus 512 KiB slow RAM, one read-only DF0, no hardfile and full 908×313
output. Input events retain their original frame numbers. Only declared media
paths are replaced in the effective script; the report records that script and
its hash. `--reference-engine` selects an explicit frozen engine DLL for baseline
research, while keeping the capture runner and its other dependencies fixed.

## Reading a result

| Status | Meaning |
| --- | --- |
| `PASS` | Complete captures and final identities match the reviewed baseline; normal/scalar outputs also match. |
| `REGRESSION` | The same declared workload differs from the baseline. Inspect the earliest captured difference and logs; a justified hardware correction may intentionally change it. |
| `PARITY_FAILURE` | Normal and scalar execution differ. |
| `REVIEW_REQUIRED` | A complete matching normal/scalar replay has no reviewed baseline for this case. Successful execution alone does not establish a gameplay milestone. |
| `UNAVAILABLE` | Required ROM/media files are absent. This is unavailable coverage. |
| `INVALID` | Wrong identities, changed workload/evidence, incomplete captures or malformed output prevent a comparison. |
| `FAILED` / `TIMEOUT` | Execution failed or exceeded its wall-clock limit. Logs are retained. |

The Python process returns `0` only when every selected case passes, `1` for a
failure/invalid comparison, and `2` for incomplete coverage or review. PowerShell
callers should preserve `$LASTEXITCODE` when wrapping this command. Selecting a
subset only establishes that subset's result. Progress reports remain explicitly
incomplete until every selected case and the final binary-integrity check finish;
an interrupted suite cannot supply a successful result or a new baseline.

Captures contain CPU/device-state JSON, Chip RAM, slow RAM, a full field BMP and
signed 16-bit little-endian interleaved stereo PCM from the checkpoint field.
The suite samples field 1, every 1,000 fields and the final field: 850 files per
execution mode across the six cases. A reported first difference is the earliest
**captured** difference, not necessarily the first diverging instruction or pixel.
These observations are not save states and do not cover every internal latch.
Final CPU/hardware/output checksums supplement checkpoint comparisons; sampled
PCM and images do not constitute a full-stream hardware comparison.

The manifest pins archive hashes, exact ZIP entries, uncompressed hashes and
lengths. Script verification normalizes CRLF to LF before hashing so a Git
checkout's newline convention cannot change the workload. The report also keeps
the actual script-file hash. Replay contracts bind the parsed inputs, media
identities, ROM, profile, frame budget and capture interval independently of host
paths. Changed contracts require a separately reviewed baseline.

To recheck existing captures without another emulation run:

```powershell
python scripts/native_regression.py compare --run artifacts/native-regression-new --baseline CopperScreen.Lightweight.Tests/Workloads/native-regression-baseline-v1.json --output artifacts/native-regression-recheck
```

Add `--reference-run <original-baseline-run>` to either `run` or `compare` when
the old capture directory is available. Hash-verified reference images enable
side-by-side and changed-pixel views. `compare` also reports changed state fields
and the first differing memory/PCM byte when the matching reference data exists.
Without those local reference files, the committed hashes still detect changes.
Original captures, reports and historical expectations remain intact.

## Baseline provenance and deliberate updates

The [v1 baseline](../../CopperScreen.Lightweight.Tests/Workloads/native-regression-baseline-v1.json)
uses the accepted 2026-09-25 CPU bus-spacing engine
`7EE0B872B7E2F4A491FCBBC4D8BDC2BBFD577DF34A1EBB62CF4B0148018EE3E9`,
stable Copper68k 1.4.1 and the new host-only capture runner. Its binary identities,
runtime, replay contracts, exact hashes and per-title review notes are retained.
This creates new regression expectations without replacing historical records.

All declared milestone images were inspected. In addition, 54 Lotus III,
216 Desert Dream and 48 Alien Breed image/memory files match the independently
retained post-correction captures. Lemmings' final image and Chip RAM match its
accepted delayed-input gameplay evidence. Tower Assault's options/opening level
and Super Cars II's options/race progression were reviewed afresh on the accepted
engine; equality with their older pre-bus-correction captures is not claimed.
Alien Breed's field 4,000 is now labeled as a black transition, rather than using
the earlier engine's title-screen description.

The fresh production build matches all 850 reference capture files, and both
builds independently have identical normal/scalar captures. See the
[sprint validation record](NATIVE_REGRESSION_2026-09-26.md) for the complete check.
Existing hardware uncertainties and scoped performance exceptions remain in the
[issue register](ISSUES.md) and [performance guide](PERFORMANCE.md).

For a justified behavior change, first run into a fresh directory without
`--baseline`, then review the actual captures against independent evidence.
Create a review JSON keyed by replay ID, with `frames` listing every declared
milestone, `evidence` identifying the supporting record(s), and an `assessment`
describing what was observed and its limits. Optional `milestones` may correct
the captions for those same frames. For example:

```json
{
  "lotus3": {
    "frames": [11000, 15000, 17000],
    "evidence": ["docs/engine/LOTUS_III_CPU_BUS_TIMING_2026-09-25.md"],
    "assessment": "Reviewed the logo and both driving demos; player-controlled racing is not covered."
  }
}
```

The review must cover exactly the cases in that run. Then create a new version:

```powershell
python scripts/native_regression.py baseline --run artifacts/reviewed-run --review artifacts/review.json --output artifacts/new-baseline.json
```

The command rehashes the frozen binaries, captures and logs, rechecks scalar
parity, and refuses incomplete/divergent runs or an existing output file. It
cannot independently judge the truth of human review notes. A baseline change
still needs the hardware or compatibility evidence supporting that change;
matching scalar execution by itself is insufficient.

## Tooling tests

```powershell
python -m unittest discover -s scripts/tests -p test_native_regression.py -v
```

These tests need no ROM/media. They exercise altered expectations, evidence
mutation, every capture kind, missing/extra captures, timeouts, nonzero exit,
unsupported states, archive ambiguity, identity checks, path containment,
line-ending portability and report escaping. CI runs them on Windows and Linux.
The Windows ZIP-selector fix also has four discriminating engine-project runner
tests: all four failed before the fix and pass afterward. The diagnostic project
remains outside the production solution's shared outputs.
