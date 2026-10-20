# Installed CopperStart and CopperOS hour sessions — 2026-10-18

Both sequential installed sessions completed a full monotonic hour, followed by
successful saves/exports and fresh-process reopening. The observed runs had no
guest fault, unexpected close or audio callback exception. **Audio queue starvation
was observed in both cases; this is not audio-quality or performance acceptance.**
P1-07 remains in progress and release hardware/budgets remain **OPEN**, as instructed
by the owner. Lightweight remains the only engine; Legacy and host DOS shims were
not restored.

The [machine-readable record](INSTALLED_SESSIONS_2026-10-18.json) hashes the signed
package, source/input snapshots, controller, raw traces, native console transcripts,
window renders, exports, private diagnostics, tests and retained failed preflights.
The reproducible procedure is [installed sessions v1](INSTALLED_SESSIONS.md).
`durationAcceptance=true` and the controller's `Acceptance=true` establish this
procedure's duration/execution/persistence checks, not P0-02 budget acceptance,
physical audio quality or completion of P1-07.

## Exact configuration

- Current provisional PC: AMD Ryzen 5 5600X, 6 cores / 12 logical processors,
  approximately 32 GiB RAM, Windows 11 Home x64 build 26200, Balanced power plan.
  Normal background applications were allowed. This task ran no builds/tests
  during the measured hours; this is not a controlled throughput baseline.
- Signed diagnostic MSIX `1.1.30.0`, unique test identity
  `CopperScreen.Test.d8aca67d4a834a6fa2636877e6692998`.
  SHA-256: `48ebeefebca8e44c9b9c9bf6fa487ae553ef09d9635d56192159275327f055c1`.
  The existing trusted local test certificate was used; no trust change or
  certificate export was needed. The signed payload has 266 verified files;
  174 runtime source inputs and the actual controller were frozen locally.
- Qualified standalone CopperStart `diskboot-2026-10-16-dev.8`, bounded PAL OCS
  A500 / 68000, 512 KiB Chip + 512 KiB slow RAM. No Legacy, RTC, Fast RAM, HDF
  or shared-directory substitution. Copper68k stays the pinned NuGet dependency.
- CopperStart uses the retained read-only Lotus II ADF and opening-race input
  sequence, then observes the resulting game/score/attract cycle. Warmup reaches
  14,000 frames before measurement. Inputs are asynchronous installed inputs,
  not a claim of scalar checkpoint alignment or full-game completion.
- CopperOS uses the qualified original native Shell/storage proof pair
  `a500-copperos-cli-boot-proof-dev.1.31e326e7505f` and a new owned workspace.
  Native `storage` creates/mutates/verifies files and settings before measurement.
  The hour observes the running Shell, with midpoint checkpoint and post-hour
  readback. This quiet CLI workload is not an active OS audio demonstration.

The apps ran non-elevated from WindowsApps with the real desktop presentation
and miniaudio paths. All four installed processes exited with code 0. The
controller released its scoped wake request and removed only its own package
registration; private test diagnostics and external exports remain locally.

## Duration, stability and memory

| Observation | CopperStart | CopperOS |
| --- | ---: | ---: |
| Measured Stopwatch/QPC seconds | 3,600.0067 | 3,600.0537 |
| Retained nominal five-second samples | 716 | 711 |
| Largest sample gap, seconds | 5.2914 | 5.9434 |
| Sampled native frame increase | 177,564 | 156,150 |
| Sampled frame increase / monotonic second | 49.39 | 43.43 |
| Private memory first → last, MiB | 173.6 → 176.1 | 188.0 → 184.7 |
| Private memory min–max, MiB | 171.7–176.8 | 177.8–190.0 |
| Working set first → last / max, MiB | 209.6 → 166.5 / 214.8 | 222.2 → 185.7 / 225.7 |
| Managed memory first → last / max, MiB | 27.0 → 32.5 / 38.5 | 29.9 → 31.1 / 39.4 |
| App log growth, bytes | 1,994,912 | 1,933,300 |
| Handles first → last | 930 → 897 | 921 → 886 |
| Presentation skips / buffer drops | 15 / 0 | 38 / 0 |
| Guest fault / observed paused state | None / none | None / none |

Sample coverage begins within milliseconds of each measured interval and ends
within the final five seconds. Every sampled frame counter increases; no gap
exceeds the protocol's 30-second continuity bound. Warmup, final native readback
and reopening are additional time. Memory/log observations include diagnostic
cost and do not prove absence of all leaks or compliance with an unfrozen budget.
The varying frame rates are descriptive observations, not validated engine FPS
or real-time pacing acceptance; earlier INVALID / RERUN throughput evidence stays
unchanged.

During CopperStart, the host UTC clock jumped forward about three hours between
adjacent samples. QPC advanced 5.0255 seconds and the guest advanced 248 frames
across that boundary. The raw UTC values and adjustment are preserved. The full
3,600 seconds use QPC; the apparent four-hour UTC span is not session duration.
The harness did not set the system clock. No such discontinuity appeared in the
CopperOS trace. The result is dated October 18 in Europe/Helsinki.

## Audio findings

Both sessions initialized the real `ma_backend_wasapi` output. These deltas
exclude warmup and post-hour native storage/reopen work.

| Measured output observation | CopperStart | CopperOS |
| --- | ---: | ---: |
| Requested sample values | 345,460,736 | 345,403,392 |
| Missing sample values filled by silence | 3,453,870 | 44,204,654 |
| Missing / requested sample values | 1.00% | 12.80% |
| Callbacks with starvation | 4,680 | 37,654 |
| Callback exceptions / submit failures | 0 / 0 | 0 / 0 |
| Guest fields with nonzero PCM | 172,532 | 0 |

Supplied zero PCM is counted separately from unavailable queued samples.
CopperStart exercised active guest PCM; CopperOS's quiet Shell did not. Starvation
and uneven pacing are real findings requiring investigation and requalification
before declaring the advertised experience ready. The counters do not establish
an OS/driver glitch count, physical speaker quality or device-change recovery.
Those remain unavailable coverage. No unrelated application audio was recorded.

## Saves, protected media and exports

- CopperStart's protected original, both independent ADF exports and the disk
  after a new installed process match
  `82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
  Saved profile, last-machine and recent-session/settings hashes match across
  reopening. This does not qualify a title-specific native high-score/game save.
- CopperOS's post-hour native command and fresh-process command both report
  `COPPEROS STORAGE REOPEN VERIFIED`. The complete saved image matches
  `e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
  Profile and settings hashes match across reopening. Both external `.copperos`
  exports independently contain the complete 901,120-byte image with that hash,
  format 1 metadata and the admitted seed/pair. Their containers have separate
  hashes; matching native image content is the integrity comparison.
- Test-owned registration and package-private run directories are absent after
  cleanup. Exported media, private checkpoint copies and diagnostics are retained
  under the local ignored evidence root. No ROM, game media, generated workspace,
  package, dump or signing material is added to source control.

This procedure checks reopening, not an MSIX update. The earlier
[signed install/update record](RELEASE_MSIX_VALIDATION_2026-10-17.md) remains
separate evidence for update persistence. Its
[WACK verdict](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) covers exact `1.1.26.0`,
not this new diagnostic package hash. No new WACK verdict is claimed here.

## Unexpected-close investigation and correction

The initial `1.1.27.0` short preflight triggered a combined visibility/pause/fault
guard after about 55 seconds. Its report did not distinguish the condition. The
owner denied closing or pausing it; its app log and matching Windows events show
no CLR/native crash. **That first trigger remains unresolved.** Successful later
runs do not establish its cause, and the failed record remains unchanged.

The traced `1.1.28.0` successor completed both CopperStart preflights, then exposed
a different, concrete defect during CopperOS preparation: a read handle prevented
atomic replacement of the auxiliary `progress.json`. The diagnostic observer
caught that exception, attempted cleanup and waited on the normal dirty-disk
discard dialog. Retained process-stack/error evidence identifies the dashboard
writer and observer-initiated close; it is not a guest hardware fault.

The correction makes only the auxiliary dashboard best effort, counts failed
writes and preserves authoritative JSONL samples. Failure details are written
before unattended cleanup, which discards only that test's uncommitted state.
Production atomic profile/workspace saving is unchanged. The same locked-file
control fails against retained `1.1.29.0` and passes against the corrected writer.
A signed native preflight held the dashboard handle for 15 seconds: the guest
advanced 1,057 frames, counted three rejected dashboard writes, then completed
storage/reopen. The full CopperOS hour tolerated one dashboard write failure
without closing; CopperStart counted none. Both full sessions closed only when
the observer requested normal completion. Failed attempts and the negative test
control are retained as failures, not relabeled passes.

## Supporting checks and remaining release work

Before the measured hours: production Release build passed with zero warnings
and errors; normal production diagnostic-type isolation passed; host tests passed
265 with 14 unavailable (12 optional native inputs, two diagnostic-only controls);
desktop tests passed 22 with supplied native assets; diagnostic queue/dashboard
controls passed seven; soak tooling controls passed 13; MSIX tooling controls
passed 64. The expected old-writer negative-control failure is retained separately.
No engine semantics or golden outputs were changed by the installed observers.

Next P1-07 work is to investigate audio queue starvation and pacing with reproducible
host/audio diagnostics, then rerun the affected installed workloads. Minimum and
reference hardware, numeric release budgets, native Kickstart hour sessions,
physical HID/focus/capture/sleep/mixed-DPI/audio-device/accessibility checks and
the final shipping package remain open. Neither these two hours nor previous
WACK results close those gates.
