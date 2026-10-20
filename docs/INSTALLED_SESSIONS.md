# Installed CopperStart and CopperOS sessions

P1-07 uses `copperscreen-installed-soak-v1` for provisional observations on the
owner's current PC. Release budgets remain **OPEN**. This protocol does not
replace the historical engine throughput protocols or freeze minimum hardware.
Lightweight owns the machine and every native device; Legacy stays unavailable.

## Workloads and duration

Use the exact signed diagnostic MSIX candidate with a clean package family and
the qualified standalone CopperStart ROM and CopperOS storage seed. Never patch a previous signed package
or discover alternate producer assets. Both are still development proof assets,
not admitted shipping content. Input hashes are checked before installation and
reported alongside the actual WindowsApps identity and loaded assembly hashes.

Run the following **sequentially**, through the normal native desktop window,
runtime, presentation path and miniaudio output:

1. CopperStart: bounded PAL OCS A500, 68000, 512 KiB Chip + 512 KiB slow, one
   read-only DF0, no RTC/Fast/HDF/shared folders. Mount the retained Lotus II ADF
   (`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`).
   Submit the retained `corpus-lotus2.json` sequence at observed frame milestones
   and reach at least 14,000 runtime frames before measurement. These asynchronous
   installed inputs do not claim exact scalar-replay alignment. Observe the
   resulting game/race/attract state for a full hour; this is not a full-game or
   title-specific high-score-save qualification.
2. CopperOS: the same bounded native ROM profile with a new owned workspace
   cloned from the qualified system seed. Reach the native Shell and execute
   `storage` to create, mutate and verify guest files/settings. Checkpoint, then
   observe the running Shell for a full hour. Verify the files again afterward,
   checkpoint and export a versioned backup. This is a Shell/storage session,
   not a continuously busy desktop application or OS audio demonstration.

Each measured hour starts **after** its usable warmup milestone. Stopwatch/QPC
time must reach at least 3,600 seconds, with nominal five-second samples covering the
whole interval, increasing frame counters, no paused/faulted state and no sample
gap over 30 seconds. The gap bound validates observation continuity; it is not a
release responsiveness budget. UTC and a separate external process observer are
retained. A shortened or interrupted session never becomes hour acceptance.
If the host wall clock changes, retain the adjacent UTC/QPC samples and frame
progress, disclose the adjustment and use QPC for duration. Do not subtract UTC
timestamps to qualify the hour or rewrite samples to hide the change.

After each hour, close normally and activate a new installed process with the
same owned profile. Repeat boot/input or native storage readback and compare
media, profile and settings hashes. CopperStart's protected original and ADF
export must match. CopperOS must reopen the complete saved native storage image,
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
The app remains non-elevated. The controller requests system/display wake only
for its lifetime, then releases that request; it does not change the power plan.

## Observations and interpretation

Record frame/presentation counters, faults/pauses, audio submission failures,
process private bytes and working set, managed/committed managed memory,
collections, handles/threads, CPU time and actual app-log bytes. Keep raw JSONL,
start/middle/end window renders, native console transcripts and crash logs.
Memory endpoints/ranges are descriptive; do not infer a release budget or the
absence of every leak from one hour. Samples and captures have diagnostic cost.

Only diagnostic builds include the additional audio counters. Guest PCM is
observed before floppy-drive sound mixing. The actual miniaudio callback records
requested/consumed/missing sample values, callbacks with starvation, nonzero
output and callback exceptions. Record the real backend and its initialization;
an absent device or null backend is unavailable audio coverage. Separate warmup
counters from the measured-hour deltas. Supplied silence is not a queue underrun.
The CLI's quiet guest PCM is not an active-audio pass. Callback delivery does not
establish audible speaker quality, OS/driver glitch counts or physical device
removal/recovery. No recordings of system or unrelated application audio are made.

With a real output sink, host execution restores its bounded audio reserve and
waits for either a released consumer slot or a host command. Signed `1.1.31.0`
uses eight slots; signed `1.1.43.0` uses ten, as qualified in the
[warmup/refill continuation](AUDIO_WARMUP_REFILL_2026-10-19.md).
There is no second video deadline gating an underfilled audio queue. The session
procedure preserves the candidate's guest clock, PCM sample sequence, queue
capacity, device volume and native device work. Presentation remains independently leased and bounded;
the audio-free fallback retains its wall-clock pacing. The reserve can add up
to roughly its slot count in fields of host latency; its size is an implementation setting,
not an owner-approved release latency budget.

New diagnostic packages also report `host-frame-start-gaps-v1`: cumulative
interval buckets between successive host starts of complete guest fields, with
upper bounds 1, 5, 10, 15, 20, 25, 30, 40, 80 and 160 milliseconds plus an
overflow bucket. Subtract the before/after counters for each measured interval.
These are host execution start intervals, not physical display/vsync measurements.
Pause/resume gaps and warmup must stay outside the compared measured interval.
The counters are excluded from normal production builds. Older package traces
lack this observation; do not manufacture a historical distribution.

Normal background applications are allowed. Avoid this task's own builds/tests
during the measured hours. This foreground-app protocol makes no formal host
throughput or uncontaminated performance-baseline claim. Do not cap memory,
force collections, suppress audio, trim pixels or skip guest hardware to improve
the observations. Preserve failures and unexpected closes with their original
identity, timings, reports and logs; trace and disclose unresolved causes.

The live `progress.json` dashboard is auxiliary. A sharing conflict while
replacing it is counted and cannot terminate the native session; the persistent
JSONL trace is authoritative. Readers should share read/write/delete access.
The locked-file control reproduces the old fatal writer and verifies recovery
after the reader releases its handle. A session failure writes its details before
unattended cleanup, preserving the last complete private checkpoint and external
exports rather than waiting for a discard-confirmation dialog.

## Reproduction and controls

Build production Release and run the relevant host/desktop tests first. The
diagnostic-only queue control can be run using `-p:CopperScreenInstalledTests=true`
and the filter `FullyQualifiedName~InstalledAudioTelemetryTests|FullyQualifiedName~MiniaudioSampleQueueTests`.
Use isolated artifacts for that test build. Its linked proof types may produce
duplicate-type warnings; normal production builds exclude the observers entirely.

```powershell
./scripts/test-msix-soak.ps1 -PackagePath '<signed diagnostic MSIX>' -MediaPath '<qualified raw Lotus II ADF>' -InputScriptPath '<frozen corpus-lotus2.json>' -OutputDirectory '<new evidence directory>'
```

`-Smoke -DurationSeconds 10` exercises the full preparation/reopen sequence with
short measurement intervals and always leaves `Acceptance=false`. It can take
several minutes per native boot. Its result is not an installed-hour result.
`-Cases copperos` or `-Cases copperstart` can narrow smoke preparation; full
acceptance always requires both sequential cases.
The [trace controls](../scripts/tests/test-soak-tools.ps1) use synthetic data to
reject short, missing, non-finite, reversed, paused, faulted, stalled and gapped
series. CI runs those controls, not the two-hour installed procedure.

The controller refuses a pre-existing identity, uses package-family LocalState,
preserves this run's private diagnostics outside package storage and removes only
its own registration at completion. Originals, ADF exports and workspace backups
remain in the local evidence directory. Never commit media, packages, generated
workspaces or signing material. WACK and actual install/update persistence remain
separate, exact-package records; a new diagnostic observer changes the MSIX hash.

The [signed 1.1.43.0 continuation](INSTALLED_SESSIONS_2026-10-20.md) completes both
full hours and fresh-process save/readback with zero cumulative missing audio
samples. The [capture audit correction](COPPEROS_CAPTURE_VALIDATION_2026-10-20.md)
supersedes its mistaken blank-midpoint interpretation: the original captures
are byte-identical and readable. Historical records stay unchanged. Diagnostic
captures retain PNG hashes, presented-frame identifiers and proof-text pixel
counts; missing fixed CopperOS proof text fails the capture and retains its
evidence. A preview-only impression must be checked against those files and
pixel evidence before opening a display defect. Release budgets and physical
display/audio recovery remain OPEN.
