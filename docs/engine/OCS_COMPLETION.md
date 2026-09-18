# PAL OCS completion work

Scope confirmed by the owner on 2026-09-18: complete PAL OCS chipset features,
including collision detection, Paula serial and writable standard ADFs. Retain
the Lightweight single-owner clock and allocation-light execution. NTSC, ECS/AGA,
new CPU models, expansion controllers and optional external host transports are
separate work. Existing physical timing uncertainties remain identified in
[ISSUES.md](ISSUES.md); functional support does not establish exhaustive silicon
conformance.

## Implementation and verification sequence

1. Denise collisions: CLXCON matching, sprite grouping, persistent CLXDAT and
   CPU read-clear strobes; lores/hires, single/dual/HAM, sprite priorities and
   attached sprites. Verify reads and effective writes around pixel boundaries.
2. Paula UART: SERPER timing, SERDAT transmit buffering/start/stop, SERDATR receive
   state and overrun, TBE/RBF interrupts, UARTBRK and externally controllable pins.
   Use deterministic pin-level cases and an exercised native serial path.
3. Writable standard ADF: memory-to-disk DMA, serial write timing, write protection,
   cancellation and completion, track changes and standard-sector decoding.
   Provide explicit export/save, host configuration and native format/write/read
   plus save/reopen checks using disposable copies of media.
4. Reconcile remaining documented OCS register behavior against the source and
   primary documentation. Preserve explicit unsupported/unverified boundaries;
   do not label raw latches or silent fallbacks as implemented hardware.
5. Run affected native replays and the full Release/test checks, then measure the
   complete candidate against the frozen accepted pre-work build. Update current
   scope and verification records only from observed results.

## Performance contract

The maximum regression is 1%, assessed with the previously confirmed one-sided
95% upper confidence bound on paired frame times. The earlier dual-playfield
Lemmings exception does not apply to this work. Keep the same complete CPU,
hardware, picture and PCM work; no opt-out collision mode or hardware bypass.
New behavior needs its own active-feature correctness/cost checks as well as
retention of existing workloads.

Baseline engine SHA256:
`5F6E27939729365C6AE470D4605E7FB3EB9C3981B093FB59A67D48D839595C1B`.
The frozen identical-runner bundle is local ignored evidence under
`artifacts/ocs-completion-2026-09-18/reference/`. Use the current
[measurement protocol](PERFORMANCE.md); record each trial without overwriting
earlier measurements. A valid result exceeding the limit requires explicit owner
acceptance; missing/noisy telemetry remains INVALID / RERUN.

## Implementation and observed evidence, 2026-09-18

**OCS scope incomplete; measured build performance accepted with scoped exceptions.** The current source contains:

| Area | Implemented | Observed verification and limits |
| --- | --- | --- |
| Denise collisions | CLXCON matches, sprite groups, sticky CLXDAT, CPU read-clear, raw single/dual/hires samples and bounded BPU-disable tail | 29 focused cases pass in the current 502-case suite. All 24 native snapshots across four probes match the pinned hardware-photo readouts; the exact transition phase remains unverified. |
| Paula UART | Transmit/receive shift and holding registers, status, period, break, overrun, interrupts, owner-thread pins | 12 deterministic cases pass. All three native UART probes retain their earlier complete fingerprints on the measured build; visible TBE phase differences prevent a cycle-exact hardware-match claim. |
| Standard ADF writes | Guest DMA/serializer, protection, cancellation, strict sector export, desktop Save ADF and dirty-media guards | Ten engine write cases, including allocation coverage, pass. Native Workbench format/verify, file write/read and separate-machine reopen passed on the measured optimized build; manual desktop checks remain outstanding. |
| MSBSYNC | Byte alignment in the ideal recovered-bit stream | Focused coverage passed; no general GCR/flux-media support implied. |
| Paddles / light pen | Ideal charge-time counters and beam latch with owner-thread APIs | Four focused cases passed. Physical RC/comparator and latch phases, plus desktop peripheral mapping, remain unverified or missing. |

Latest full diagnostic engine run: **502 passed, zero failed/skipped**,
`engine-hires-collision-work.log` in the local evidence root. The matching production
Release build passed with zero warnings/errors (`build-hires-collision-work.log`).
Current frozen engine SHA256 is
`754C8A3DE511F65DD82E52F4485F89E888E0A7E6921736A019CC4B44BA5EB7C0`.
The retry passed **74 disk tests**. After the final collision and throughput
optimizations, the full host suite passed **91 tests, zero failed/skipped**, with
both optional native cases enabled using the supplied inputs
(`host-final-optimization.log`).
Desktop save/close dialogs have not been manually exercised. See
[native evidence](../NATIVE_VALIDATION.md#ocs-completion-probes-2026-09-18).

The register audit additionally found **missing VHPOSW/VPOSW beam repositioning
beyond LOF and external synchronization/genlock** (LWA-VIDEO-009). Raw latches do
not implement these features. Canonical beam changes must preserve accepted bus
phases and correctly reschedule device work. Keyboard MCU startup/recovery and
general CIA serial/CNT modes remain separate board-I/O gaps. The issue register
also retains bounded disk, sprite, HAM and interrupt timing assumptions. These
facts prevent an unrestricted OCS-complete claim.

## Earlier performance engineering evidence

Four short, two-pair lores/hires series completed with identical CPU/hardware/output
fingerprints, zero measured allocations and no unsupported-mode reports. They used
the frozen identical runner but omitted formal host-load telemetry and are
**engineering diagnostics only**. The first collision implementation was roughly
5% slower in lores. Moving rare sprite work out of the hot renderer and folding
the UART deadline into existing interface scheduling reduced the latest completed
lores mean difference to roughly 1.2%, with noisy hires results. Neither establishes
the required confidence bound. Raw series are retained as `diagnostic-initial.json`,
`diagnostic-interface-deadline.json`, `diagnostic-cold-match.json` and
`diagnostic-sprite-boundary.json`.

Windows blocked the packed collision-flag candidate's first load after its reference
sample. That original `diagnostic-collision-flag.json` remains incomplete. The retry
and later optimizations below supersede its implementation status, without replacing
its evidence. The current full comparison completed in
`retention-hires-collision-work/`; the old native exception has not been reused.

## Execution restriction and successful retry

At the owner's retry request on 2026-09-18, DLL execution succeeded without an
application-control bypass. Current diagnostic tests passed **496/496**, disk tests
passed **74/74**, and host tests passed **89** with two optional native skips.
Those two native host cases then ran explicitly with the supplied ROM/media/script
and both passed. Logs: `engine-retry-1.log`, `disk-retry-1.log`, `host-retry-1.log`
and `host-native-retry-1.log`. The previous blocked runs below retain their labels.

Production engine SHA256 for this retry:
`1FBDE6E8FF93737082C6D721DD26853E481D5A1AD5BA6A6E0568410DAFD7C42E`.
The native format/write/export and fresh-machine reopen checks repeated their
earlier full fingerprints and visible completion messages on this identified build.
Logs/captures use `native-format-retry-1`, `native-reopen-retry-1`; assembly hashes
are in `native-retry-1-build-hashes.json`.

**The collision replay exposed a correctness discrepancy:** `sprcoll8=$8400`
and `sprcoll8d=$840A`, whereas pinned A500 OCS hardware photographs show `$8401`
and `$840B`. The border probes still return `$8020` and `$8000`. The earlier native
record did not repeat both sprcoll8 probes after the BPU-disable guard was added;
it therefore cannot establish retention of bit 0. Keep all initial/corrected/retry
captures. The mismatch required a discriminating bitplane/window/control-boundary
case, implemented below. Ordinary unit tests alone did not establish native retention.

The previously blocked short benchmark now runs (`diagnostic-collision-flag-retry-1.json`),
with matching work and zero measured allocations but variable timing. The controlled
comparison in `retention-retry-1/` was interrupted to investigate the collision
discrepancy; its partial samples are **INCOMPLETE / NOT ACCEPTED**, as recorded in
`INTERRUPTED.txt`. They do not establish a confidence-bound result.

### Collision transition correction after retry

Four new focused cases distinguish disabling bitplanes inside a still-open DIW
from disabling after its right edge, in single and dual playfield modes. Two failed
before the change (`collision-tail-before.log`). The bounded model now retains
collision comparison for the rest of that line's display window and retires it
before later lines. This restores all four native probe values together:
`sprcoll8=$8401`, `sprcoll8d=$840B`, `sprcollbrd1=$8020`, `sprcollbrd2=$8000`.
Native logs/captures have the suffix `-collision-tail`; engine SHA256 is
`325DF2A919D61B51459768FC939AD3EDF167A6B2DDE61DB4F43919DAD47300E8`.
The aggregate hardware observations support the transition model, but do not
establish its exact pixel phase. No title-specific condition or border bypass was added.

The full diagnostic suite reached **500 passing cases** (`engine-collision-tail.log`),
followed by **27 collision cases passing** after the final tail-state cleanup
(`collision-tail-final-tests.log`). Release compilation passed. The short comparison
`diagnostic-collision-tail.json` retains complete fingerprints and zero allocations
but shows about 3–4% slower frame times. It is engineering evidence, not formal
acceptance. This intermediate trial preceded the final optimization and scoped
owner acceptance below.

### Profile-guided optimization

Local `dotnet-trace` 10.0.745401 profiles place the largest cost in the device tick
(including the inlined renderer), bitplane stepping and hires rendering. Trace
timing is diagnostic, not an acceptance measurement. A renderer-reference layout
experiment showed no useful gain and was reverted; its trial remains recorded.

The retained changes publish the next physical CCK directly while bitplane fetching
is active, removing redundant minima whose candidates cannot precede that CCK.
Hires pending playfield comparisons now use the existing sprite work mask. A lone
eligible sprite whose two playfield collision bits are already latched needs no
further comparison until a CLXDAT read clears them; overlapping groups still run
their full comparisons. Two added cases verify read-clear and control-change
behavior. This does not skip emulated pixels, DMA phases or new collision events.

All **502 engine tests pass** (`engine-hires-collision-work.log`) and the production
Release build passes (`build-hires-collision-work.log`). Current frozen candidate
engine SHA256: `754C8A3DE511F65DD82E52F4485F89E888E0A7E6921736A019CC4B44BA5EB7C0`.
All six captured frames per collision probe (24 snapshots total) match hardware
expectations on this build; see `collision-final-optimization-readouts.json`,
`check-collision-readouts.py` and the `*-final-optimization` captures.

Short trials (`diagnostic-direct-bitplane-deadline.json` and
`diagnostic-hires-collision-work.json`) retain complete fingerprints and zero
allocations but show substantial host timing variation. Formal measurement completed
in `retention-hires-collision-work/`, using the unchanged v1 protocol and identical
frozen runner. All 36 samples passed workload and telemetry checks. Paired mean /
one-sided 95% upper frame-time changes were lores **+0.333% / +1.696%**, hires
**-1.155% / +0.198%**, native Lemmings **+1.296% / +3.582%**.
Only hires meets the owner's 1% bound. On 2026-09-18 the owner explicitly accepted
the lores and native exceptions for this measured build. Raw results remain
unchanged; this does not waive future regressions or complete the remaining OCS
scope. See [the complete result](PERFORMANCE.md#ocs-completion-work-2026-09-18--accepted-with-scoped-exceptions).

After formal measurement ended, the same engine repeated native format/write/export
and fresh-machine reopen. Every earlier CPU/hardware/output fingerprint matches;
final BMPs show the proof text and completion markers. Evidence uses
`native-format-final-optimization`, `native-reopen-final-optimization`,
`native-final-optimization-build-hashes.json` and `verify-final-optimization.ps1`.
The complete host suite then passed **91/91 with no skips**, and all three UART
replays (`tbeirq1`, `tsre1`, `txirq0`, suffix `-final-optimization`) retain their
earlier full fingerprints after excluding probe-only FPS/allocation totals.
This preserves UART execution coverage; it does not resolve physical phase gaps.

### Original blocked evidence

Windows Code Integrity event 3077 blocked local DLL loads with `0x800711C7`, citing
Enterprise signing requirements/policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`.
This is an operating-system application-control restriction, not a failing hardware
assertion. The selected diagnostic run could not execute its 97 cases, and the
latest production benchmark candidate could not start. The subsequent authorized
retry succeeded; these records describe the original failure, not a current block.

Blocked DLLs, relative to the repository:

- `artifacts/diagnostic-tests/bin/CopperMod.Amiga.Lightweight.Tests/release/CopperMod.Amiga.Lightweight.dll`
  — SHA256 `72C743383BD742BA53E0F4A1EE5BF43AD04ACA044998FAB111569E886378C9F9`.
- `artifacts/ocs-completion-2026-09-18/candidate-collision-flag/CopperMod.Amiga.Lightweight.dll`
  — SHA256 `CFFB0099B9EE3BBC1179D6A861AA3A5DFC059607324AD2A91C56A3CCA3BBC1F0`.

Local event/hash evidence: `windows-code-integrity-blocks.json` and
`blocked-build-hashes.json` under `artifacts/ocs-completion-2026-09-18/`.
Do not assign either hash to the earlier successful native write build, whose
assembly identity was not captured at execution time.

## Remaining beam and synchronization implementation

The [A500 Agnus register notes](https://www.manualslib.com/manual/1112209/Commodore-Amiga-A500.html?page=215)
define writable vertical/horizontal position bits. Updating only the clock's
readback would leave device ownership and output inconsistent. The source audit
identified these dependencies:

| Dependency | Required discriminating coverage |
| --- | --- |
| Clock line/field end and CIA TOD predictions | Forward/backward writes, late LOF changes, values beyond nominal PAL end, no backwards canonical time |
| Refresh, disk, audio and sprite slots currently derived from `cycle % 454` | New input slots follow the beam; already accepted output phases keep their original deadlines |
| Copper WAIT and bitplane control sequencer | Beam jumps across matches and DDF transitions, including pending register/data phases |
| Incremental Denise cursor and delayed display controls | Reposition without replaying or dropping an accepted shifter/control stage; bounded framebuffer writes |
| External sync versus beam readback | ERSY within one line versus across a line, external input edges, synchronization output and display effects |

Pinned [vAmigaTS VPOS probe notes](https://github.com/dirkwhoffmann/vAmigaTS/blob/0489f55d22ef7560d924a304e30998aea6864264/Agnus/Registers/VPOS/README.md)
report that `ersy1` sets/clears ERSY within one line with no effect; `ersy2` crosses
a line with ERSY set and shows hardware display corruption from missing HSYNC.
The `Freeze` comments in the assembly therefore do not justify freezing canonical
time or all beam counters. These probes and their OCS photographs are investigation
inputs, not yet successful engine replay evidence. Later ECS resynchronization
documentation is not sufficient evidence for OCS timing.

Remaining completion work after the retry:

1. Preserve the repaired collision transition and its focused/native coverage;
   obtain finer physical timing evidence if a new boundary discrepancy appears.
2. Finish the missing beam/synchronization behavior with discriminating tests;
   retain explicit physical timing and peripheral boundaries.
3. After further engine changes, freeze and identify the new candidate and repeat
   affected native checks. The measured optimized build has collision/UART,
   format/write/export/reopen and Lemmings retention evidence; desktop save/close
   checks are still outstanding. Keep earlier evidence intact.
4. Optimize using fresh engineering trials as needed. Run the complete controlled
   homogeneous-retention-v1 protocol for lores, hires and native Lemmings without
   concurrent builds/tests. Require every one-sided 95% upper bound to be at most
   1%, or request an explicit exception for a valid result that exceeds it.
5. Reconcile current scope and this record only after those results exist.
