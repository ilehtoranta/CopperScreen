# Installed audio refill and host pacing correction — 2026-10-18

The host no longer gates an underfilled audio queue behind an independent video
deadline. Both corrected installed CopperStart and CopperOS sessions completed
3,600 monotonic seconds with **zero missing sample values, starved callbacks,
callback exceptions or submission failures**, followed by successful exports and
fresh-process reopening. Lightweight remains the only engine; guest timing,
PCM generation, buffer capacity and Legacy availability are unchanged.

P1-07 remains **in progress**. Release budgets and minimum/reference hardware
remain **OPEN**. Host scheduling variation is still observed; these results do
not establish physical audio quality, display/vsync pacing or final release
performance acceptance.

The [machine-readable evidence](INSTALLED_AUDIO_PACING_2026-10-18.json) binds the
exact package, source/controller snapshots, controls, raw traces, native console
messages, window renders, exports and cleanup. The procedure remains
[installed sessions v1](INSTALLED_SESSIONS.md). The earlier
[hour-session findings](INSTALLED_SESSIONS_2026-10-18.md) remain unchanged.

## Demonstrated defect and correction

The runtime allocated eight audio slots and declared a target of eight, but
normal refill produced only one field at an independent video deadline whenever
at least two buffers remained. Catch-up waited until the queue fell below two.
The deadline and polling waits could therefore hold an underfilled reserve after
scheduling delays. The previous hour snapshots never reached the declared
eight-slot target: CopperStart sampled one to seven queued buffers, CopperOS one
to six. Their recorded software starvation was 1.00% and 12.80% respectively.
Those are separate desktop intervals, not a controlled performance comparison.

The [runtime correction](../CopperScreen/CopperScreenRuntime.cs) removes that
second deadline and immediately replenishes the existing reserve, in bounded
batches. At capacity it waits for either an audio-consumer slot release or a host
command. The [miniaudio output](../CopperScreen/MiniaudioAudioOutput.cs) signals
only when a complete queue slot is released, including discarded old epochs;
partial reads retain their slot. Pause, stop and other commands keep a separate
wake. Device callbacks finish before their event is disposed. The audio-free
fallback retains its existing wall-clock pacing.

No samples are repeated, stretched, resampled or suppressed by the correction.
Full native CPU/device/pixel/audio work remains enabled and single-owner. There
is no timer-resolution, affinity, priority, power-plan or emulated-clock change.
The queue is still eight slots; it now reaches its configured target. That reserve
can add roughly eight fields of host latency, which remains subject to the
owner's unfrozen release latency budget.

The [discriminating control](../CopperScreen.Lightweight.Tests/RuntimeAudioPacingTests.cs)
uses an artificial slow video clock and a manually consumed audio sink. The old
runtime fails to fill its reserve within the control's generous timeout. The
corrected runtime fills it, refills repeated four-buffer drains, preserves the
complete ordered PCM sequence and processes pause/stop with the sink full. This
is a functional synthetic control, not a hardware or throughput measurement.
The old failure is retained. Queue controls separately cover partial reads,
released-slot notification, silence, full queues and discarded generations.

## Exact installed run

- Signed diagnostic MSIX `1.1.31.0`, identity
  `CopperScreen.Test.af6f00c7f3b04b94ab921e70b62d2229`.
  SHA-256: `640d6040998d7d5fca19207224a7cd5627f46045dc39f989e74dae24004ae01e`.
  All 266 signed payload files were independently verified; production builds
  exclude the diagnostic observers. The existing trusted local test certificate
  was used without a new trust change or credential export.
- Provisional current PC: Ryzen 5 5600X, six cores / twelve logical processors,
  approximately 32 GiB RAM, Windows 11 Home x64 build 26200, Balanced power plan.
  Normal background applications were allowed. This task ran no builds/tests
  during the measured hours. This is not a controlled engine-throughput baseline.
- Same qualified standalone CopperStart ROM `diskboot-2026-10-16-dev.8` and bounded
  PAL OCS A500 / 68000 / 512 KiB Chip + 512 KiB slow RAM. No RTC, Fast RAM, HDF,
  shared-directory substitution or host DOS shims. Copper68k remains pinned.
- CopperStart: protected retained Lotus II ADF, retained opening-race sequence,
  at least 14,000 warmup frames, then the game/score/credits/attract cycle. These
  asynchronous installed inputs do not claim scalar checkpoint alignment,
  full-game completion or a title-specific native game/high-score save.
- CopperOS: original qualified native Shell/storage pair
  `a500-copperos-cli-boot-proof-dev.1.31e326e7505f`, a new owned workspace and native
  create/mutate/verify before measurement. The measured Shell is quiet; this is
  not active CopperOS audio qualification or a continuously busy OS workload.

## Full-hour observations

| Observation | CopperStart | CopperOS |
| --- | ---: | ---: |
| Stopwatch/QPC seconds | 3,600.0117 | 3,600.0095 |
| Retained nominal five-second samples | 718 | 718 |
| Largest observation gap, seconds | 5.1094 | 5.0794 |
| Complete guest fields during the hour | 179,638 | 179,639 |
| Guest fields / monotonic second | 49.8993 | 49.8996 |
| Real WASAPI callbacks | 168,680 | 168,680 |
| Requested / consumed sample values | 345,456,640 / 345,456,640 | 345,456,640 / 345,456,640 |
| Missing sample values / starved callbacks | 0 / 0 | 0 / 0 |
| Callback exceptions / submission failures | 0 / 0 | 0 / 0 |
| Guest fields with nonzero PCM | 174,103 | 0 |
| Nonzero output callbacks | 163,656 | 0 |
| Presentation skips / buffer drops | 0 / 0 | 0 / 0 |
| Host frame-start gaps over 80 through 160 ms | 7 | 5 |
| Host frame-start gaps over 160 ms | 0 | 0 |
| Private memory first → last, MiB | 179.2 → 177.8 | 187.3 → 177.9 |
| Private memory min–max, MiB | 175.4–182.3 | 177.8–196.6 |
| Working set first → last, MiB | 215.3 → 215.9 | 224.7 → 216.9 |
| Managed memory first → last, MiB | 26.4 → 29.3 | 33.6 → 23.2 |
| Handles first → last | 894 → 882 | 899 → 882 |
| App log growth, bytes | 1,995,634 | 1,963,098 |

Both intervals had increasing sampled frame counters, no paused/faulted state,
no unexpected close and no detected UTC/QPC discontinuity. Warmup, final native
readback and fresh-process reopening are additional time. The controller's
`Acceptance=true` establishes this protocol's execution/duration/persistence
checks; it does not approve release budgets or the complete P1-07 matrix.

The new diagnostic-only `host-frame-start-gaps-v1` histogram measures host starts
of complete guest fields. Its full distribution is retained. It is not physical
display timing; the observed 80–160 ms intervals remain visible findings. The
before/after snapshots are asynchronous: CopperStart's histogram differs from
its completed-field delta by one boundary field; CopperOS's counts match.
Older traces have no such histogram, so no historical distribution is invented.
The previous loaded-host engine measurements remain INVALID / RERUN.

Supplied silent PCM is distinct from missing queue data. CopperStart exercised
active guest PCM and actual nonzero output callbacks. CopperOS exercised a quiet
native Shell and continuous real sink delivery. Neither establishes speaker
perception, OS/driver glitch counts or device-change recovery. Memory/log ranges
include observer cost and do not prove absence of every leak or an unfrozen
budget's acceptance. No unrelated application audio was recorded.

## Persistence and supporting checks

All four non-elevated installed processes exited with code 0. CopperStart's
protected original and both ADF exports retain
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
CopperOS's post-hour command and fresh-process command both report
`COPPEROS STORAGE REOPEN VERIFIED`; both saved images retain
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
Both external backups independently contain that complete 901,120-byte image
and the admitted format-1 seed/pair metadata. Profiles and settings match across
reopening in both cases. The owned registration/private run directories are
removed; exported media, checkpoint copies and diagnostics remain locally.

Before the installed hours, production Release built with zero warnings/errors.
Host tests passed **272**, with **nine unavailable**: seven optional native cases
and two diagnostic-only controls. The final native desktop run passed **22/22**;
the initial run without the required native input variables retains its
19 passes / three unavailable.
Diagnostic audio/pacing controls passed **9/9**. The pacing control's expected
single failure against the old runtime remains separate evidence. The native Lotus replay
preserves per-field PCM parity for all 14,000 fields and all nine retained
pixel/Chip-RAM/cycle/CPU checkpoints match the previously reviewed captures.
No engine semantics, package pins or golden outputs were changed.

This run checks process reopening, not an MSIX update. The earlier
[signed update evidence](RELEASE_MSIX_VALIDATION_2026-10-17.md) and
[exact-package WACK result](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) remain
immutable and do not certify this new package hash. No new WACK verdict or
shipping-content admission is claimed. No ROM, media, generated workspace,
package or signing material is added to source control.

The original untraced preflight guard trigger remains unresolved as recorded in
the earlier hour evidence; this audio correction does not establish its cause.
P1-07 still needs declared hardware/budgets, native Kickstart hour coverage,
physical HID/focus/capture/sleep/mixed-DPI/audio-device/accessibility checks and
final shipping-package qualification. Host jitter, physical A/V latency and
device recovery remain separate release work.
