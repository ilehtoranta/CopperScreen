# Keyboard and CIA completion, 2026-09-20

This change implements the remaining ordinary digital keyboard/CIA functions in
Lightweight. It is not a certification of every 8520 silicon revision, keyboard
MCU instruction, matrix scan, electrical phase or A500 reset pulse duration.
Execution stays on the machine owner thread and the canonical integer clock.

## Evidence and reuse

The independent specifications are Commodore's Hardware Reference Manual,
[Appendix F, 8520](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_f.html)
and [Appendix G, keyboard](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_g.html),
plus the A500/A2000 Technical Reference Manual's
[A500 differences](https://www.manualslib.com/manual/917391/Commodore-Amiga-A500.html):
the A500 reset chord is hard-wired and does not send shutdown messages.
These specify protocol and register behavior, not measured propagation delays.

Legacy was inspected at CopperMod `ada86020ad7a9b298cd7f689c3731d8d779684d1`:
`CopperMod.Amiga/Input/Keyboard.cs` and `CustomChips/Cia/Controller.cs`.
Its keyboard injects complete bytes and acknowledges an SDR read. It does not
supply the missing clock/recovery behavior. Its held-key/raw-key concepts and CIA
arithmetic were useful references; its scheduler, callbacks and collections were
not imported. There is no new package or sibling-checkout reference. Copper68k's
existing full-reset API is used at an owner-thread instruction boundary.

## Implemented contract

- Keyboard data setup, low clock and high clock phases remain 142 CPU cycles
  each. Eight rising clocks latch SDR and serial ICR; reads are not acknowledgments.
  A host low/high pulse of at least eight CPU cycles acknowledges the byte.
- An unacknowledged byte times out after 1,014,412 CPU cycles (143 ms rounded to
  CCK). Slow logical-one synchronization pulses repeat until acknowledged;
  recovery sends F9 and retransmits the original byte before queued input.
  Losing the F9 acknowledgment retains the original byte.
- Native ROM cold resets start with slow synchronization, then FD, a snapshot
  of held keys, and FE. Input changes after that snapshot follow FE. ROM-free
  diagnostic machines start synchronized, preserving the existing fixture API.
  The held-key snapshot uses ascending raw-key order, not an MCU matrix scan.
- `SetKeyState(key, down)` accepts physical codes below 0x78. It suppresses repeat,
  toggles Caps Lock on press, and detects Ctrl/left-Amiga/right-Amiga. The desktop
  uses this API. `KeyboardCapsLockOn` exposes the emulated latch. The existing
  `SubmitKey`/`SubmitInput.KeyCode` APIs retain raw-byte transport semantics for
  replay scripts, including explicit queue-full failure. Physical queue overflow
  sends FA after the ten retained queued codes; the in-flight byte is separate.
- The A500 chord resets CPU and devices once on assertion, preserving canonical
  time, RAM, mounted media and the independently powered keyboard. Releasing a
  chord key rearms it. No A2000 reset-warning code or fabricated KCLK reset pulse
  is emitted. CPU execution is never dispatched recursively.
- CIA serial output shifts MSB first on falling CNT and completes on the eighth
  rising CNT (16 Timer-A underflows). SDR is a holding buffer; the latest pending
  write supplies the next byte. Returning to input cancels unfinished output and
  partial receive state. Stopped timers do not transmit. Idle CNT is high and SP
  retains the last shifted bit; open-collector input/output levels are combined.
- Timer A can count external rising CNT. Timer B can count E-clock, rising CNT,
  Timer-A underflows, or underflows gated by high CNT. Switching a running timer
  back to E-clock starts at the next tick rather than counting past time.
- Timer PB6/PB7 pulse/toggle outputs override DDR and PRB; pulses last one E-clock.
  CIA-B's actual port output drives the floppy control lines, including DF3 select
  and motor. Masked interrupts do not hide observable serial/port transitions.
- Falling FLAG edges latch ICR. PRB accesses produce a bounded PC strobe on the
  third following E-clock. The owner-thread parallel API supplies data and ACK
  pins and exposes port data/strobe; CIA-B exposes external SP/CNT input and
  resolved output pins. No host printer, MIDI or parallel transport is attached.

Ordinary timer advances remain arithmetic. Only active serial transmission visits
individual half-bit edges, with at most two buffered bytes per call. Keyboard
phases and enabled timer-port transitions share the existing interface deadline;
there is no new per-CCK keyboard/serial poll or release-path allocation.

## Verification

`LightweightCiaPinsTests`, `LightweightKeyboardProtocolTests`, the revised
`LightweightKeyboardHandshakeTests` and existing input/IRQ/TOD tests cover byte
boundaries, buffering, direction changes, recovery/retransmission, cold startup,
held input, overflow, Caps Lock, reset, external clocks, CNT gating, timer port
outputs, FLAG/PC, CIA-B IRQ delivery, clock partitioning and allocation behavior.
These are specification/model tests, not captures from physical CIA silicon.

Production Release build: zero warnings/errors. Final full isolated Release
engine run: **607 passed**, zero failed/skipped. CopperDisk: **74 passed**.
The initial two full runs each caught one small allocation in a different existing
disk test (8,168 bytes in active ADF write; 7,752 bytes in IPF density seeking).
The ADF test passed in isolation and both passed in the final full run. Those
failed logs are retained, not counted as passes or benchmark measurements.

The final host run passed **95 cases**, zero failed/skipped, on the corrected
strobe build (`host-strobe.log`). All three native cases were enabled: ordinary
Lemmings, Lemmings Return press/release, and ROM-only cold keyboard startup,
Caps Lock on/off and reboot after the A500 chord. Both Lemmings runs retain the
accepted full output; the ordinary run also retains its CPU fingerprint. The
native tests observe guest acknowledgments, continued execution and reset state;
they do not establish MCU or electrical timing.

Local ignored evidence: `artifacts/keyboard-cia-2026-09-20/`, plus the named TRX
files under each test project's ignored `TestResults/`. ROM and media are never
included in source control. Native ROM/media/script hashes and the existing
Lemmings checkpoints remain in [native validation](../NATIVE_VALIDATION.md).

## Performance gate

Reference: accepted engine at `d403d358e47e89a012998a98b451690b3a81df48`;
working HEAD `06e0bfed505aba8744e14b4c8bb5d71cc123eaa1` only adds the website gallery.
Reference engine SHA256:
`7778A0F26FCC2D2EE1F66C69172F48D7DFB6E61D2CD6623693A47FBDFC7D4C85`.
Candidate engine SHA256:
`D79290B6BD3ED08B5797DC89DC69F77A41E6D5B58935844DCEC85906BBE5890A`.
The candidate bundle reuses the exact frozen runner, Copper68k and CopperDisk.

[Homogeneous retention v2](../../scripts/run-lightweight-homogeneous-retention-v2.ps1)
changes only the accepted reference/native identity from the earlier protocols.
Both builds must match complete workload identities, including the post-ERSY
native identity. Six balanced pairs, full lores/hires/native workloads, Normal
priority, verified topology/affinity, host-load policy v2 and zero measured
allocation are retained. No builds/tests run alongside measurement. Every
workload requires the one-sided 95% upper frame-time bound at most +1%.
Earlier owner exceptions do not transfer. The completed valid series does not
meet the lores/hires gate; the owner accepted these specific exceptions on
2026-09-20, as recorded below.

The partial `retention-v2/` series was aborted after final review identified
overlapping delayed PC strobes. The pending-pulse fix and regression precede
the new frozen `candidate-strobe/` bundle. No partial timing result is accepted.

## Completed performance result

**Valid series; overall 1% gate NOT MET by measurement. Build accepted with
scoped lores/hires exceptions on 2026-09-20.**
Native passes. Lores exceeds 1% on the paired mean; hires has a mean below 1%
but an inconclusive upper bound above it. These are separate from functional
correctness, which passed. No measured samples were excluded or pooled with the
aborted earlier build.

| Workload | Reference mean FPS | Candidate mean FPS | Paired mean frame-time change | One-sided 95% upper bound | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 394.86 | 389.95 | +1.2690% | +3.2345% | Above limit; exception accepted |
| Hires | 356.66 | 353.41 | +0.9377% | +2.6422% | Inconclusive; exception accepted |
| Native Lemmings | 264.94 | 265.85 | -0.3450% | +0.5559% | Pass |

All 36 samples have matching complete-workload fingerprints, zero measured
steady-state allocation and no unsupported feature reports. Native also matches
the independently validated post-ERSY identity. Host telemetry is valid throughout.
Windows 10.0.26200, Ryzen 5 5600X, logical CPU 2 with SMT siblings 2/3, Normal
priority and host-load policy v2 were verified. The unchanged balanced order is
R1,C1,C2,R2,R3,C3,C4,R4,R5,C5,C6,R6 for each workload. There were no concurrent
builds/tests or competing benchmark runners.

Evidence: `artifacts/keyboard-cia-2026-09-20/retention-v2-strobe/`, including
`protocol.log`, `telemetry.jsonl`, all 36 workload logs, `samples.json` and
`summary.json`. The source hashes and engine patch are retained alongside the
frozen bundle. Protocol SHA256:
`6BC84348D27AEA672B2AE4A7B590B7C39278E5A7DB739D03145C00EB5DA90C58`.
The reference, candidate and dependencies remained frozen for the full series.

Raw FPS pairs (pair labels, not chronological execution order):

| Workload | Pair | Reference FPS | Candidate FPS |
| --- | ---: | ---: | ---: |
| lores | 1 | 390.43 | 385.33 |
| lores | 2 | 394.14 | 396.61 |
| lores | 3 | 398.22 | 387.66 |
| lores | 4 | 392.94 | 399.60 |
| lores | 5 | 400.14 | 381.14 |
| lores | 6 | 393.31 | 389.38 |
| hires | 1 | 356.69 | 358.74 |
| hires | 2 | 354.09 | 337.43 |
| hires | 3 | 354.20 | 355.14 |
| hires | 4 | 360.88 | 356.38 |
| hires | 5 | 355.85 | 357.28 |
| hires | 6 | 358.25 | 355.52 |
| lemmings | 1 | 259.63 | 265.61 |
| lemmings | 2 | 264.58 | 264.67 |
| lemmings | 3 | 265.97 | 268.46 |
| lemmings | 4 | 265.05 | 264.79 |
| lemmings | 5 | 265.40 | 264.45 |
| lemmings | 6 | 269.03 | 267.12 |

## Owner acceptance, 2026-09-20

After reviewing the complete results and the explicit choice between further
optimization and these exceptions, the owner replied: "I accept this."
This accepts the lores +3.2345% and hires +2.6422% upper-bound exceptions for
engine SHA256 `D79290B6BD3ED08B5797DC89DC69F77A41E6D5B58935844DCEC85906BBE5890A`
against the reference recorded above. Native Lemmings passed without an exception.

The measured values and raw classifications remain unchanged. Acceptance does
not establish a measured 1% pass, close the hardware limits below, or raise the
default 1% upper-bound budget for future changes.

## Remaining accuracy boundaries

The existing CIA timer load/underflow pipeline, zero-latch convention, ICR sampling,
CIA-to-Paula sub-CCK phases and TOD comparator/debounce uncertainties remain open.
The reset chord is a bounded host-boundary reset action, not an electrical model
of assertion width or CPU reset-vector bus cycles. MCU self-test failure/blink,
matrix scan/debounce and oscillator variation are not simulated. Native functional
success does not close these hardware-evidence gaps. See
[LWA-CIA-001..003 and LWA-INPUT-001..002](ISSUES.md).
