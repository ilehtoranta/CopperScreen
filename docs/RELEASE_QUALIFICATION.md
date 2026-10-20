# Release compatibility, desktop and performance qualification

P1-07 is **in progress**. The first development matrix below is finite and
repeatable; it does not freeze the P0-02 shipping scope or minimum hardware.
The owner authorized provisional observations on this PC on 2026-10-17 and
explicitly left release budgets open. Lightweight owns every guest boot.
Legacy, host OS shims and WHDLoad are outside this qualification.
The [2026-10-17 evidence](RELEASE_QUALIFICATION_2026-10-17.md) records bounded
native/desktop results, the launcher correction and INVALID loaded-host timings.
The [signed MSIX continuation](RELEASE_MSIX_VALIDATION_2026-10-17.md) qualifies
that launcher fix with real install/update, six native CopperOS storage/media
activations and 18 installed layout observations. It preserves profiles/settings
and workspace hashes through restart/update and restores an external backup
after uninstall/reinstall. The subsequent
[fresh P1-07 WACK run](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) reports overall
PASS on that exact signed `1.1.26.0` update: all 13 required checks pass, with the
unchanged optional blocked-executable reference finding retained.
The [2026-10-18 installed sessions](INSTALLED_SESSIONS_2026-10-18.md) add two
sequential full-hour CopperStart/CopperOS observations and successful native
storage/media/profile/settings reopen checks on signed diagnostic `1.1.30.0`.
Both runs complete without a guest fault; real WASAPI queue starvation and
uneven pacing remain findings. Release budgets and P1-07 completion remain open.
The [installed-session protocol](INSTALLED_SESSIONS.md) records exact scope,
monotonic duration, callback/memory traces and the diagnostic dashboard correction.
The [audio refill/pacing correction](INSTALLED_AUDIO_PACING_2026-10-18.md) removes
the competing host video deadline and wakes on audio-slot consumption. A fresh
signed `1.1.31.0` run completes both full hours and reopens with zero software
queue starvation, callback exceptions or submission failures. The recorded
presentation skip/buffer-drop counters also stay at zero.
Host frame-start variation remains recorded; physical A/V quality and release
budgets remain open. The earlier package findings are unchanged.
The [signed audio/pacing update continuation](AUDIO_MSIX_VALIDATION_2026-10-18.md)
upgrades the retained pre-fix `1.1.30.0` payload to that exact, unchanged signed
`1.1.31.0` candidate. All six native installed phases and 18 launcher observations
pass, preserving workspace and host profile/settings hashes through restart/update
and restoring the external backup after clean reinstall. This is separate
installed persistence evidence. The
[fresh exact-package WACK run](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md) reports
overall PASS on signed `1.1.31.0`, with all 13 required checks passing and the
same 24 optional blocked-executable messages retained. Final production/Store
identity, physical qualification and release budgets remain open.

## Development matrix v1

Every result must identify exact source/binaries, ROM/media/script hashes,
configuration, test or interaction steps and retained outputs. A result applies
only to that combination and interval. Keep boot, interaction, video, guest PCM,
host audio delivery, saving and reopening as separate columns in the evidence.
Use PASS, FAIL, UNAVAILABLE, NOT RUN or NOT APPLICABLE with a reason; an optional
skip is never PASS. Native Kickstart/OS/game files remain user supplied.

| Case | Fixed development workload | Scope and limits |
| --- | --- | --- |
| KS13-Lemmings | PAL OCS A500, 68000, 512 KiB Chip + 512 KiB slow, native Kickstart 1.3; `lemmings-cpu-bus-timing-v1.json`; 18,120 fields | Existing gameplay/keyboard host-adapter regressions retain CPU/output identities and active guest PCM. No full-game, title-specific save or physical audio-device claim. |
| CopperStart-Lotus2 | Same bounded A500; qualified standalone `diskboot-2026-10-16-dev.8`; original boot/input control plus supplied Lotus II, 14,000 fields | Standard app session versus scalar guest control, nine image/state checkpoints and per-field PCM parity. Imported original stays read-only. No full-game completion or general DOS/Workbench compatibility claim. |
| CopperOS-storage | Same qualified ROM and `a500-copperos-cli-boot-proof-dev.1.31e326e7505f` system pair | Native Shell create/write/read/rename/delete, settings, process restart, changed-package-directory simulation, read-only/full volume and denied checkpoint/export/reopen. Use separate signed MSIX evidence for actual package updates. The CLI proof is not an audio demonstration. |

The exact inputs and their original qualification are documented in
[CopperStart support](COPPERSTART_APP.md), [CopperOS storage](COPPEROS_STORAGE.md)
and [native replay guidance](NATIVE_VALIDATION.md). Existing native Kickstart 3.1,
ECS/AGA, other CPU/RAM profiles and further titles retain their own evidence;
this bounded matrix does not qualify them automatically or remove their open
coverage. Minimal disk boot remains a separate experimental path. Its original
synthetic guest is also used to isolate desktop input controls from ROM/media
availability; those controls are not a commercial-game compatibility replay.
Scalar parity is a regression control, not independent hardware validation.

## Automated desktop controls

Run `CopperScreen.Desktop.Tests` in its own process. All desktop classes share
one collection-scoped `LaunchUiFixture`: disposing a per-class fixture around a
per-assembly Avalonia application can invalidate another class's dispatcher and
asynchronous operations. Headless desktop initialization must remain separate
from host diagnostic dispatcher tests.

`DesktopQualificationTests` exercises held joystick input after lost focus or
the Release input command without receiving key-up; fresh press/release afterward;
Alt+Enter fullscreen/windowed with the same runtime; keyboard pause/resume;
keyboard-only launcher navigation with actual accessible names; and launcher
scrolling at logical client sizes/scales 800×600/100%, 800×600/125%,
1280×720/150% and 960×640/200%. It checks the rendered saved-profile control
inside the actual client bounds after layout settles. The text palette is checked
against a 4.5:1 contrast ratio for five declared foreground/background pairs.

These are headless layout and input controls, not physical monitor, DPI transition,
screen reader or full accessibility certification. The existing desktop suite
separately exercises qualified CopperOS launch/pause/restart/stop/resume and
confirmed previous-checkpoint recovery with the supplied native pair.

The signed `copperscreen-installed-msix-v4` continuation adds real native-window
layout checks at 800×600, 1280×720 and 960×640 on this monitor's actual 100% scale,
repeated in every storage phase. The empty-workspace CopperOS view keeps Start
and Save disabled; its measured viewport and rendered Save control stay inside
the client after scrolling. Native save and profile persistence qualification
remain separate from those layout observations. See the
[installed protocol](MSIX_PACKAGING.md#p1-07-installed-launcher-layout-continuation)
and [exact-package evidence](RELEASE_MSIX_VALIDATION_2026-10-17.json).

## Current-PC engine observations

`scripts/measure-release-baseline.ps1` implements the separately versioned
`copperscreen-release-engine-baseline-v1` protocol. It preserves historical
scripts, input hashes and complete identities. The immutable October 12 record
supplies expectations before timing; a new run never learns its own golden.
It is a descriptive Release **engine runner** measurement, not an installed app
session, a paired optimization comparison or P0-02 budget acceptance.

The Windows protocol rechecks single-group homogeneous CPU-set topology and
native counters, pins one declared logical CPU with its physical SMT siblings
protected, verifies Normal priority/affinity during each process, records power
plan, pins .NET 10.0.12, and hashes runner/dependencies/runtime/inputs before and
after. Other topology/runtime/workload contracts need their own validated version.
Full pixels and real stereo PCM remain enabled and consumed. The fixed order is
lores, hires, native; native, hires, lores; hires, native, lores:

- Lores and hires: 600 warmup + 7,200 measured fields each.
- Native Lemmings: 14,520 warmup + 3,600 measured fields, with the reviewed input
  sequence and complete current identity. This is the October 12 interval;
  earlier protocols retain their own different intervals and fingerprints.

All nine runs must match the complete retained identities, zero measured
steady-state allocations and no unsupported feature. Native-counter telemetry
includes ten-second preflight/cooldowns and approximately one-second sampling.
The unchanged [host-load policy v2](engine/PERFORMANCE.md#measurement-integrity-retained-from-policy-v2)
rejects active competing tests/builds/benchmarks and sustained selected-CPU,
SMT-sibling or aggregate interference. Missing/stale/gapped counters, changed
placement, timeouts and identity failures stop the run. Both decimal separators
in locale-formatted FPS are parsed explicitly without changing guest execution.

The default stops on host-load rejection. `-LoadedHostDiagnostics` explicitly
continues only after load-policy violations, retains every sample/violation and
marks the entire series **INVALID / RERUN**. Its rates are diagnostic observations
under the recorded load. No contaminated subset becomes a valid baseline or
release acceptance; missing telemetry still stops. Normal background apps are
allowed, and no other job is paused or terminated. Do not run this task's own
builds/tests during timing. Prior valid results remain unchanged.

Example, after freezing a Release runner directory and resolving the portable
script's second-disk path:

```powershell
./scripts/measure-release-baseline.ps1 -RunnerDirectory '<frozen Release runner>' -RomPath '<native Kickstart 1.3>' -DiskPath '<Lemmings disk 1 ZIP>' -InputScriptPath '<hash-identical portable script>' -LogicalProcessor 2 -OutputDirectory '<new evidence directory>'
```

CPU 2 is a recorded choice for the audited Ryzen host; recheck topology rather
than assuming that number on another PC. Use `-ValidateOnly` for preflight and
rejection controls without timing. The
[qualification tooling controls](../scripts/tests/test-qualification-tools.ps1)
test changed identities, allocation/output bypass, locale parsing, changed/missing
frozen files, unavailable/stale/gapped telemetry, all sustained-load dimensions,
active competing jobs and threshold reset. CI runs these synthetic controls;
it does not run a performance acceptance benchmark.

## Remaining physical and installed qualification

Before marking P1-07 complete, freeze the release profile/matrix, exact installed
release candidate, named minimum/reference PCs and numeric startup, responsiveness,
real-time pacing, audio and memory/log growth budgets under P0-02. Do not substitute
historical engine FPS objectives or this provisional PC for those decisions.

Record at least these independent results on each declared configuration:

| Check | Required observation |
| --- | --- |
| 60-minute session for each declared boot path | Exact package hash/identity and inputs; monotonic duration; crashes/faults; presentation pacing, actual audio underruns, memory and log growth; saved data and reopen afterward. |
| Keyboard and controller | Every declared layout/device; remap every action; disconnect/reconnect while held; no stuck input; saved mapping retained. Synthetic mapping controls do not prove physical HID reconnect. |
| Focus and capture | Alt+Tab while holding keys/buttons; lose focus during capture; F10 and Release input; capture again without jumps/stuck buttons. |
| Display | Fullscreen/windowed, each declared size/scale, moving between physical monitors with different DPI, and readable/reachable controls after each change. |
| Sleep/resume | Resume an active and a paused session; verify responsive controls, bounded catch-up, audio recovery and storage reopen without losing saved work. |
| Audio device change | Remove/change the active device while playing; verify delivery recovers and guest execution/data remain intact. Queue submission failures are distinct from callback underruns. |
| Accessibility | Keyboard-only installed setup, visible focus, actual accessible names with the selected assistive tool, contrast and reading at each declared size/scale. |

The diagnostic installed-session observer now records actual output-callback
queue starvation separately from guest PCM and submission failures. The earlier
[current-PC hour record](INSTALLED_SESSIONS_2026-10-18.md) retains missing sample
values of 1.00% for CopperStart and 12.80% for the quiet CopperOS Shell, with no
callback exceptions or submission failures. The
[corrected installed run](INSTALLED_AUDIO_PACING_2026-10-18.md) now records zero
missing samples in both full hours after repairing the underfilled reserve;
this does not freeze an audio/pacing budget or qualify physical output.
Physical speaker quality, OS/driver glitch counts and device removal/recovery
remain UNAVAILABLE, not zero.
The [2026-10-18 recovery continuation](INSTALLED_RECOVERY_2026-10-18.md) is
**PARTIAL**: real active/paused CopperStart S3 transitions and resumed audible
output were observed on signed diagnostic `1.1.32.0`, with paused frames preserved.
Post-resume queue misses remain recorded. Windows rejected the codec-disable
method; a subsequent elevated endpoint probe was cancelled before execution.
CopperOS S3, actual device recovery and final save/reopen checks remain open.
The [owner-authorized endpoint retry](INSTALLED_RECOVERY_RETRY_2026-10-18.md)
also ended before probe execution at Windows consent; both audio devices remain
enabled and no new installed-app recovery result is claimed.
The later [owner-launched endpoint probe](RECOVERY_ENDPOINT_PROBE_2026-10-18.md)
verifies actual disable/restore and resolves the host-method availability issue.
Subsequent installed runs reproduced a durable post-S3 callback/frame stall.
The [native reopen continuation](INSTALLED_RECOVERY_REOPEN_2026-10-18.md) retains
the failed `1.1.33.0` run, verified endpoint restoration and the new signed
`1.1.34.0` correction. It reopens the original backend after restart failure;
production build and ten exact-package controls pass. Installed recovery,
checkpoint/export and fresh-process readback for both boot paths remain pending
the new helper run. Earlier dated results are unchanged, and this does not
extend earlier update/WACK or full-hour verdicts to the new package hash.
The later [installed diagnostic continuation](INSTALLED_RECOVERY_DIAGNOSTICS_2026-10-18.md)
retains `1.1.34.0` failing active S3 despite repeated reopen attempts. Signed
`1.1.35.0` adds bounded native logging and a full native-close control, with no
new production recovery algorithm. Both boot paths pass its software-only native
restart/reopen, export and fresh-process profile/settings/storage readback. One
CopperStart reopen window retains 3514 missing sample values; physical sound is
unobserved. The prepared physical controller explicitly requests the display on
after timer wake, without assigning an unrecorded panel state to older failures.
The [attended physical continuation](INSTALLED_RECOVERY_PHYSICAL_2026-10-18.md)
subsequently passes active/paused S3, actual HDMI endpoint interruption and
checkpoint/export/fresh-process persistence for both boot paths on `1.1.35.0`.
The owner confirms CopperStart audible output before sleep and after final HDMI
restoration. Post-resume display-on preparation is recorded; display-off behavior
and earlier failure causality remain unproven. Native recovery errors and
CopperStart missing samples remain findings. Audio continuity, release budgets
and exact-candidate update/WACK qualification remain open.
The later [display/audio continuation](AUDIO_DISPLAY_RECOVERY_2026-10-18.md)
records actual Windows display-state transitions and guest progress without an
audio callback clock on signed `1.1.36.0` and `1.1.37.0`. Both boot paths recover
healthy callbacks, checkpoint/export and pass fresh-process saved workspace,
profile and settings readback. The owner confirms audible CopperStart output
before and after display-off. Version 37 fills the existing reserve before native
playback starts; early full-reopen counts are zero for both paths. CopperStart's
paired normal/Games controls support the scheduling correction in this short
loaded-host observation. Warmup and other-window missing values, plus a late
748-value CopperOS refill underrun, remain recorded and unresolved. Neither the
short zero-miss restoration windows nor physical audibility qualify continuous
glitch-free sound. The [version-4 display procedure](INSTALLED_DISPLAY_RECOVERY.md)
does not exercise S3/PnP, upgrade, WACK or a full hour for these new package hashes.
P1-07 and release budgets remain OPEN; earlier dated records are unchanged.
The separate [EventPipe investigation](AUDIO_REFILL_TRACE_2026-10-18.md) retains
profiler effects and GC-only observations. Its smoke, trace and normal-exit
results are diagnostic evidence, with no new audio-continuity or performance
acceptance and no further production policy change.
The [low-intrusion warmup/publication continuation](AUDIO_WARMUP_REFILL_2026-10-19.md)
localizes early slow first-use execution and later UI publication stalls. The
desktop disables Quick JIT and keeps rendered-field audio production independent
of UI dispatch and presentation-lock waits, including running command publication. A retained WASAPI catch-up burst exhausted the eight-block reserve despite short owner work; replay proves ten blocks cover that demand, adding roughly 40 ms of queued PAL audio. Signed diagnostic `1.1.43.0` passes
both installed native paths, both cold processes, the 120-second native reopen
follow-ups, native save/export and fresh-process workspace/profile/settings
readback with zero cumulative missing values. The original 37 CopperOS episode
has no retrospective owner trace, so its precise cause remains unassigned.
The loaded-host capture is INVALID / RERUN for formal performance acceptance.
Physical sound and display/S3/PnP need separate
qualification for this exact hash. P1-07 and release budgets remain OPEN;
earlier dated failures and verdicts are unchanged.
The [signed 1.1.43.0 upgrade continuation](AUDIO_MSIX_VALIDATION_2026-10-19.md)
passes an actual same-family `1.1.42.0` to unchanged `1.1.43.0` update. Six native
activations preserve CopperOS workspace, profile and settings through restart
and update, then qualify denied-save recovery and clean-reinstall restoration.
All 18 launcher observations pass; 15 hash-checked host-state copies remain
inspectable after uninstall. This adds no new audio-hour, physical recovery,
performance-budget or WACK verdict. The earlier audio and dated records remain
unchanged. Release budgets remain OPEN.
The [fresh exact-hash 1.1.43.0 WACK continuation](AUDIO_MSIX_WACK_VALIDATION_2026-10-19.md)
reports overall PASS and all 13 required checks PASS. Ten optional checks pass;
the optional blocked-executable scan retains the same 24 messages as signed
1.1.31.0. No product correction or package rebuild was required. This closes
local WACK for the unchanged diagnostic hash; final production/Store-identity
WACK, dependency review, physical recovery and release budgets remain
OPEN. Lightweight remains the engine and Legacy is unavailable.

The [signed 1.1.43.0 full-hour continuation](INSTALLED_SESSIONS_2026-10-20.md)
completes 3,600 measured seconds for both CopperStart and CopperOS, followed by
successful native save/export and fresh-process workspace/profile/settings
readback. All four processes exit normally; warmup, measured hours and reopens
have zero missing audio samples, starved callbacks, callback errors, submission
failures or suppressed PCM. Memory ranges are descriptive and budgets remain
OPEN. The [capture audit correction](COPPEROS_CAPTURE_VALIDATION_2026-10-20.md)
establishes that the original CopperOS start, midpoint and end PNGs are
byte-identical and contain the Shell text. The earlier blank-image claim was
an incorrect visual review; its dated report and raw files remain unchanged.
LWA-HOST-005 is closed as a false positive. Diagnostic captures now retain
hash/pixel evidence and reject missing fixed-proof text; separate installed
layered probes distinguish guest, session, presenter and captured pixels.
The actual presentation-buffer drops of 19 / 3 remain descriptive observations.
Physical display/pacing and recovery gates remain open. Lightweight stays
active; Legacy remains unavailable.

The [recovery protocol](INSTALLED_RECOVERY.md) keeps these interruptions separate
from hour/performance acceptance and retains the original FAILED attempt.
Source/native correctness,
[signed P1-07 installed persistence](RELEASE_MSIX_VALIDATION_2026-10-17.md) and
[exact-package WACK](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) remain separate
evidence; neither establishes physical device behavior. The new full hours cover
only their declared CopperStart game/attract and CopperOS Shell/storage workloads,
not native Kickstart hours or the complete final release matrix.

For automated checks, build the production solution, then run host, desktop and
disk projects separately. Run `CopperMod.Amiga.Lightweight.Tests` with its own
`--artifacts-path artifacts/diagnostic-tests`. Supply only the documented optional
native input environment variables and retain every TRX skip/failure. Native
storage tests write dated receipts next to the supplied proof; use a private
copy when a run needs isolated outputs. Never commit ROMs, game/OS images,
packages, captures containing third-party content or generated workspaces.
