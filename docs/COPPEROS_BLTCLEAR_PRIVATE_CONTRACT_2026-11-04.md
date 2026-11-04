# CopperOS BltClear private contract qualification — 2026-11-04

Development bundle `diskboot-2026-11-04-a1200-desktop-single-bank.61` is staged
and selected by the manifest and project hash gate. It uses the unchanged
CopperOS HDF and one standard 524,288-byte ROM.

CopperSharp68k now exposes explicit private callee preservation before register
allocation. The Amiga wrapper honors explicit resolvers before its normal
fallback, rejects ambiguity and does not invent a return-register clobber for
void calls. Public library calls retain their ABI; argument/base setup still
counts as a clobber.

BltClear stays in C#. Its exact private OwnBlitter, DisownBlitter and BlitterWait
bindings preserve all integer registers. Explicit compiler call-site metadata
is checked and linked to direct BSR instructions. Hardware waits use the shared
C# fence without library-vector dispatch. The DMA paths share a completion exit
and retain the synchronous tail helper so frame restoration overlaps DMA.
Private release omits repeated task/owner admission; public admission,
reservation, queue/waiter handoff and preemption behavior remain.

Full dispatch/DMA inlining was rejected because it increased small CPU clears
by 36–40 cycles through extra register saves. The retained code keeps that CPU
path isolated while simplifying DMA completion. No benchmark threshold was relaxed.

## Guest-cycle results

Three identical 572-row comparisons cover the installed candidate, `.60`, the
pre-regression `.56` image and local Kickstart 3.1 40.63. Conditions are PAL 8375,
OCS Denise, 2 MiB Chip RAM, display DMA disabled and IPL 7. Times below include
the following public WaitBlit. These are guest diagnostics, not host-throughput
acceptance measurements. The original EC020 fixture remains unavailable.

| 68000 synchronous zero clear | .60 | .61 | Kickstart |
|---|---:|---:|---:|
| 16 bytes | 506 | 506 | 544 |
| 512 bytes | 2,314 | 2,314 | 1,636 |
| 768 bytes | 3,002 | 2,932 | 2,196 |
| 832 bytes | 3,082 | 2,984 | 2,296 |
| 1,024 bytes | 3,474 | 3,374 | 2,676 |
| 4,098 bytes | 9,958 | 9,848 | 8,936 |

EC020 synchronous 832 bytes improves 3,152 to 3,052 cycles. Some asynchronous
cases regress by up to 16 cycles; a few small CPU cases differ by 2–4 cycles.
Across 283 clear rows per CPU, 30 68000 and 25 EC020 rows are slower than `.60`.
This is a bounded synchronous improvement, not a universal speedup.

The separate ordered 832-byte probe shows pre-DMA overhead 1,002 to 990 cycles
(Kickstart 306), and completion overhead 274 to 202 (Kickstart 94). DMA duration
is essentially unchanged. Dispatch, argument transport, frame saves and ownership
still dominate the gap. No scheduling occurs in this uncontended IPL-7 probe.
Observed/unobserved runs match cycles, CPU and full Chip RAM.

Full producer report and rows are retained in
`CopperStart/Docs/NativeBltClearPrivateContract20261104.md` and
`CopperStart/Docs/Measurements/NativeBltClearPrivateContract20261104.json`.

## Validation and size

The C# clear/helpers/adapter shrink from 1,124 to **1,060 bytes**. The complete
ROM uses **522,196 bytes**, 60 fewer, leaving 2,092 padding bytes and 104 bytes
before the footer. Checksum is FFFFFFFF. All executable bodies remain in ROM.

1,116 compiler tests, 67 focused graphics checks, the 237-case frozen graphics
suite, the ordered-cost test and three scoped working-source identities pass.
Nineteen optional graphics cases remain unavailable after running the cost test
separately. Historical expanded-image/ColorMap exclusions remain. An initial
unscoped identity run failed four historical expanded-image goldens; those
expectations were not changed. Queued-arrival boundary probes now verify all
integer registers across private calls, including handoff. OCS/ECS, Fast RAM,
pending/disabled DMA, split transfers and public-WaitBlit traps pass.

Both production solutions build (three existing producer warnings, zero consumer
warnings), including an explicitly enabled CopperStart build. All 11 app-native
desktop/DMA tests pass. Final embedded-resource readback verifies the `.61`
manifest, ROM and unchanged HDF. The separate desktop host suite has 90 passes,
eight optional skips and four failures in unrelated concurrent launcher/storage
work: first-run wording, selected-ROM retention, ROM acceptance and nullable
SparseStorage being normalized to false. These paths/assertions were not changed
by this task and remain failed evidence. A wrong-assembly host invocation ran
zero tests and is not counted as coverage.

Paired A1200 profile/control replays match pixels, CPU, full Chip RAM, audio,
registers, milestones and scripted input. Readiness remains
12.0792278880542 emulated seconds. No boot-speed improvement is claimed.

ROM SHA-256: `40354240A611BF324E240B83407113683DFA6BD698F547FA97E41C3BC47AD6DE`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Desktop pixels: `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

Four bounded emitter files change from the frozen `.60` composition; the `.49`
reset input and exec-preemption.10 HDF remain. Exact source/runtime receipts,
profiles, benchmarks and tests are local in the producer under
`artifacts/graphics-bltclear-flat-20261104/rom-qualified`. Consumer replay and
integration artifacts use the same task directory name. Binaries/media remain
uncommitted; this does not publish a package or qualify an installed MSIX update.
