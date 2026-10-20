# Installed display recovery and audio refill — 2026-10-18

Status: **display recovery passed; audio gaps reduced, residual gaps OPEN**.
P1-07, release qualification and release budgets remain **OPEN**. This is a
bounded continuation on the provisional development PC, using Lightweight only.
Legacy and host OS shims remain unavailable. Nothing was published.

The [machine-readable record](AUDIO_DISPLAY_RECOVERY_2026-10-18.json) binds 744
local evidence files, including explicit frozen sources, signed packages, raw
installed reports, failing-before controls, TRX results and retained private
state. All 16 earlier dated recovery/audio/probe reports checked by the recorder
remain hash-identical. Their failures and exact-package verdicts are unchanged.
The procedure is [installed recovery v4](INSTALLED_DISPLAY_RECOVERY.md).

## Corrections and evidence boundary

The old runtime could wait indefinitely behind a full audio queue when HDMI
stopped delivering callbacks. An absent callback clock now switches host pacing
to complete guest fields at the ordinary video deadline. Guest PCM is still
rendered/drained; unavailable-time playback is omitted and counted separately.
There is no catch-up burst or presentation wait in this fallback. Commands,
pause state and native storage retain the same runtime owner. The app does not
turn the display on.

Windows output stays on WASAPI and can retry an initially unavailable endpoint.
The producing runtime thread requests best-effort MMCSS `Games` registration,
reverted on that thread at exit. Process priority and affinity are unchanged.
Diagnostic-only paired controls can temporarily remove/reapply registration.

Native playback previously started before the replacement reserve was ready;
both version-36 full native reopen controls lost 8192 sample values. Version 37 joins the old callback before
resetting its queue and primes the existing eight-buffer reserve before starting
native playback. Ordinary pause/reset still uses concurrent-safe epoch discard.
No buffer-capacity increase, sample repetition/stretching, guest timing change,
engine/disk source change or Copper68k replacement was used.

Diagnostic accounting keeps active underruns, deliberate pause silence,
unavailable-time suppressed PCM and discarded unread queue content separate.
Actual maximum callback gaps remain visible, including intentional interruptions.
Observers and control commands are excluded from ordinary production builds.

## Frozen candidates and checks

| Version | Signed MSIX SHA-256 | Explicit source / payload files |
| --- | --- | --- |
| 1.1.36.0 | `71a3b8766fcbeceb02ac60c75721e02dc8901c24e428eb01fdc3ae9d1e4457af` | 188 / 266 |
| 1.1.37.0 | `d93305ab8cd470266d72818fa1157721186f8a5fb7ee789b6df16e1d04ba3ef8` | 188 / 266 |

Local roots are `artifacts/audio36-882c1a71` and `artifacts/audio37-f9105546`.
Both use the existing authorized local test certificate, thumbprint
`510C8C13859A287EFFE367DB25E2BB05E9AA34AB`. Signatures and all 266 ZIP/layout
payload files were verified. The explicit `candidate-record.json` source hashes
are authoritative; packaging's parent-checkout Git metadata does not attribute
the frozen source. Independent work in the shared checkout is not silently
included in either candidate. Version 37 changes only the runtime and native
audio output relative to frozen 36. The pinned Copper68k DLL is unchanged.

Both exact production solution Release builds passed with zero warnings/errors.
Focused runtime/queue checks passed 9/9 for 36 and 11/11 for 37. Exact packaged
DLL controls passed 12/12 and 15/15 respectively, with no skips. The unavailable-
clock regression fails against signed 35; the prefill regression fails against
signed 36. Normal-build observer isolation passed. No chipset/CPU behavior was
changed, and engine suites were not substituted for installed host validation.

## Installed display and persistence results

Host: Windows 11 Home build 26200, Ryzen 5 5600X, 32 GiB, Balanced power plan;
24G42E NVIDIA HDMI output. Apps ran non-elevated. Each version used a fresh owned
diagnostic identity and frozen v4 controller. This mode requests no sleep or PnP
change and needs no administrator helper.

The controller observes `GUID_CONSOLE_DISPLAY_STATE`, requires state 0 throughout
the off interval, requests state 1 on restoration, then checks callbacks. These
are Windows display-state receipts, not panel instrumentation. Requested sampling
windows are eight seconds; exact snapshot durations are retained separately.

| Version / boot path | Guest fields while off, callback clock unavailable | Suppressed PCM values while off | Missing values in display-on follow-up | Save/export + fresh-process readback |
| --- | ---: | ---: | ---: | --- |
| 36 CopperStart | 412 | 794224 | 0 | PASS |
| 36 CopperOS | 420 | 811532 | 0 | PASS |
| 37 CopperStart | 407 | 782686 | 0 | PASS |
| 37 CopperOS | 401 | 771148 | 0 | PASS |

All restoration windows had started native output, fresh callbacks and zero
callback exceptions. All eight app processes exited normally with code 0. For
each version, both fresh processes matched their saved native disk/workspace and
all three profile/settings JSON hashes. Imported Lotus media remained unchanged
(`82dc1faf…b3a06`); each CopperOS exported archive retained `System.adf` plus
`export.json`, with the native saved image hash `e58ea8d7…f84bb0`.
The recorder checks full hashes. Both owned package registrations were removed
after copying private state into the evidence roots.

The owner confirms CopperStart game/title audio was audible before display-off
and after restoration on both candidates, including signed 37. Exact listening
times were not recorded; this does not establish uninterrupted/glitch-free sound.
No physical audibility claim is made for CopperOS's quiet Shell.

## Audio findings retained

All stopped-device restart windows had zero missing values. Baseline windows were
also zero except CopperOS 36, which retained 22854 missing values. For full
close/reopen, the total is measured from **before
the close command** through the follow-up, so early loss cannot move outside the
selected window:

| Candidate / path | Early reopen missing values | Total through follow-up |
| --- | ---: | ---: |
| 36 CopperStart | 0 | 8192 |
| 36 CopperOS | 0 | 8192 |
| 37 CopperStart | 0 | 0 |
| 37 CopperOS | 0 | 748 |

CopperOS 37 was started with a primed queue and no missing-count increase at the
follow-up's initial snapshot. The count stayed unchanged through
17:16:33.246475Z, more than seven seconds after reopening; it then increased by
748 values. One 80–160 ms producer field-start gap occurred in the window.
This supports the startup correction but leaves a later refill stall unresolved;
it does not identify host preemption, GC or command work as the proven cause.
The bounded timeline is retained with its source telemetry.

The same installed CopperStart 37 process ran normal/Games/normal/Games windows:

| Runtime scheduling | Guest fields | Callbacks | Missing sample values |
| --- | ---: | ---: | ---: |
| Normal 1 | 370 | 400 | 102370 |
| Games 1 | 421 | 394 | 0 |
| Normal 2 | 314 | 386 | 185918 |
| Games 2 | 404 | 390 | 0 |

Suppressed/discarded deltas were zero in all four windows. Aggregate CPU time
was 100% in each; the paired observations support reduced producer starvation
with MMCSS in this bounded loaded-host case. Version 36's four windows all had
zero missing values, so its comparison alone was inconclusive. Registration or
CPU load alone does not explain earlier gaps. Observer work can affect timing.

Warmup missing counts remain substantial: 36 CopperStart 558838 / CopperOS
270746; 37 CopperStart 489182 / CopperOS 313294. Version 37's first-session final
counts are 829926 / 321418, including the deliberate normal-scheduling controls
and missing values outside the selected follow-ups. The 748-value finding is
therefore not the entire run's remaining loss. Raw snapshots, totals, callback
gaps and separate omitted/discarded counters are retained; no whole-session
zero-underrun claim is made.

## Remaining work

LWA-HOST-004 remains OPEN in the [issue register](engine/ISSUES.md). Capture a
bounded producer scheduling/GC/command timeline around residual warmup/refill
loss before changing queue capacity or guest execution. Repeat full installed
hours after a demonstrated correction. The recorded competing host load means
these timing observations are **INVALID / RERUN for performance acceptance**;
they remain functional recovery and fault-investigation evidence. No other job
was paused or terminated, and historical performance protocols are unchanged.

Fresh S3, physical endpoint PnP, signed update persistence, WACK, workspace archive
import/restore and full-hour pacing were **not run for either new package hash**.
Earlier 31/35 results do not transfer. Final Store identity, named hardware,
physical desktop/HID/accessibility matrix and numeric release budgets remain
OPEN. Separate-process persistence here does not establish persistence across an
MSIX update.
