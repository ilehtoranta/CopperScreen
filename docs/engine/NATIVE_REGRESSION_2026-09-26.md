# Native regression suite validation — 2026-09-26

The six-title native corpus now has a repeatable runner, a reviewed baseline and
an HTML/JSON report. The current production engine matches the accepted
2026-09-25 engine at every checkpoint in this corpus. The Windows scripted
ZIP-entry bug is corrected without changing engine source or emulated timing.

The [usage guide](NATIVE_REGRESSION.md) documents one-command execution, exit
codes, baseline review and comparison of retained captures. The
[machine-readable evidence](NATIVE_REGRESSION_2026-09-26.json) records exact
binary/input/report identities, test counters and validation limits.

## Native evidence

Reference engine SHA-256 is
`7EE0B872B7E2F4A491FCBBC4D8BDC2BBFD577DF34A1EBB62CF4B0148018EE3E9`,
the accepted [Lotus CPU bus-spacing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md).
It runs with the new capture runner and the same current dependencies as the
candidate. The rebuilt engine is
`0176FA30E2F82641344D4AA58E39114737799F6BBD7FD5D6A88CF536FBC77097`.
Both consume stable Copper68k 1.4.1. Engine source and CPU package are unchanged
by this work; rebuild identities are recorded separately.

Profile: PAL OCS A500, 68000, native Kickstart 1.3, 512 KiB Chip RAM plus 512 KiB
slow RAM, read-only DF0, no hardfile, full 908×313 fields. All disk changes read
their pinned ZIP entries directly. No media was patched, downloaded or committed.

| Replay | Fields per mode | Captured files per mode | Result |
| --- | ---: | ---: | --- |
| Lotus III | 18,000 | 95 | Title and both driving demos; exact normal/scalar and reference agreement |
| Desert Dream | 72,000 | 365 | Both disks through closing credits; exact agreement |
| Tower Assault | 20,000 | 105 | Options menu and opening level; exact agreement |
| Alien Breed SE '92 | 16,000 | 85 | Second-disk menu and credits; exact agreement |
| Super Cars II | 19,000 | 100 | Race one after acceleration/steering; exact agreement |
| Lemmings | 18,120 | 100 | Delayed-input level-one gameplay; exact agreement |

Each complete suite executes **326,240 fields** across normal and scalar modes,
with **850 captured files per mode**. Files include CPU/device-state JSON, full
field images, Chip RAM, slow RAM and checkpoint-field PCM. Final runner
CPU/hardware/output identities also match. Capture data occupies approximately
0.7 GiB per complete suite.

All declared milestone images were visually reviewed. Independent retained
post-correction captures also match: Lotus **54**, Desert Dream **216**, Alien
Breed **48**, and Lemmings' final image/Chip RAM **2**. The Lemmings final CPU,
hardware and cycle identity agrees with the accepted delayed-input workload;
its whole-run output checksum differs from the performance workload because
this diagnostic includes the boot interval in its checksum.

Tower Assault and Super Cars II were reviewed on the accepted post-bus-spacing
engine. Their older pre-correction snapshots were not treated as unchanged
goldens. Alien Breed's field 4,000 caption was corrected to describe its black
transition; field 9,000 shows the menu and field 16,000 the credits. Historical
reports and fingerprints remain intact.

## Tool and integration checks

| Check | Evidence |
| --- | --- |
| Production Release solution | Build succeeded with zero warnings and errors |
| Diagnostic engine project, separate artifacts | 732 passed, zero skipped |
| Host tests | 92 passed; three optional native host tests skipped |
| Windows ZIP regression | Four relative/absolute and legacy/current-property cases fail before the fix; all 11 input-script cases pass after it |
| Python tooling | 21 tests passed, no external packages or copyrighted fixtures |
| Real image-mismatch control | Disposable expected image changed at field 11,000; comparator returns exit 1 and identifies that file |
| Missing-ROM control | `UNAVAILABLE`, exit 2, no emulation |
| Wrong-ROM control | `INVALID`, exit 1, no emulation |
| Report UX | Hosted layout, milestone gallery and side-by-side/difference view inspected; deliberate mismatch shows 127,890 changed RGB pixels |

The tooling tests cover image/state/RAM/PCM mismatches, final-summary changes,
missing or extra captures, unsupported states, truncated memory, duplicate ZIP
entries, archive/entry identity, path containment, newline portability, timeout
termination, nonzero exit, evidence mutation, incomplete suites and immutable
baseline creation. A changed comparison report does not replace its source run.
CI runs these tests on Windows and Linux without ROM/media; the Linux job has
not been run locally during this session.

The three skipped host tests remain unavailable host-adapter coverage. The
standalone native corpus provides its own evidence; it does not turn those
skips into passes. The disk library was not modified.

## Limits and local evidence

Raw reference/candidate/final captures, frozen runners, review notes, retained
capture comparisons and negative controls are under
`artifacts/native-regression-2026-09-26/`. The versioned baseline contains hashes
and review notes only. ROM/media and raw captures remain local ignored files.

The final full replay was launched before the last report-only refinements
(explicit incomplete-suite reporting and embedded comparison images). Those
refinements are covered by the final tooling tests and rechecking the retained
native data with the final comparator; they do not alter emulation or inputs.
The in-app browser's URL policy blocked direct `file://` preview. Hosted browser
rendering was verified; direct-file rendering remains unverified. Comparison
images are embedded to avoid file-origin canvas restrictions.

These are bounded compatibility and regression results, not full-game or
physical-chip certification. PCM covers sampled checkpoint fields; host audio
was not listened to and no complete audio-stream comparison is claimed. No
speed improvement or fresh throughput acceptance is asserted. Existing
performance protocols, thresholds, scoped exceptions and historical result
labels are unchanged.
