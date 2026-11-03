# CopperOS BltClear ownership qualification — 2026-11-04

Development bundle `diskboot-2026-11-04-a1200-desktop-single-bank.60` is selected by the manifest and project hash gate.

All internal native graphics WaitBlit vector calls now use the C#
`WaitBlitterHardware` implementation directly, including bitmap, raster, text,
scaling and Layers paths. The public WaitBlit entry uses the same compiled
18-byte body, byte-for-byte identical to its previous native implementation.
It preserves integer registers and the initial DMACONR word read followed by
BBUSY byte polling. BltClear keeps its existing direct C# helper and inline
pre-submission fence. No runtime library-vector dispatch is used for these waits.

CopperSharp68k folds a discarded word read followed by constant-address bit
polling into absolute operands when its scratch registers are dead. The fold
preserves access widths/order and condition codes, rejects interior entries,
address fixups and effect overrides, and is not tied to an Amiga address.
This removes scratch-register setup without adding a save/restore adapter.

BltClear's three compiled ownership call sites now bind directly to the same
private OwnBlitter/DisownBlitter entries already used by the raster paths.
Compiler metadata identifies the final offsets and call kinds after optimization;
the linker verifies those explicit sites and replaces JSR(A6) with BSR.W.
It never opcode-scans the instruction stream. Only public tag admission and
vector dispatch are bypassed. The private reservation, owner, queued-work,
waiter and release logic is unchanged. CPU/DMA selection and completion policy
are unchanged. BltClear and its fence remain C#; no emulator code changed.

## Measurements

Median guest cycles for synchronous public BltClear plus public WaitBlit:

| CPU / bytes | Previous .59 | New .60 | Kickstart 40.63 |
| --- | ---: | ---: | ---: |
| M68000 / 512 | 2,314 | 2,318 | 1,636 |
| M68000 / 768 | 3,128 | 3,002 | 2,196 |
| M68000 / 770 | 3,068 | 2,934 | 2,212 |
| M68000 / 832 | 3,194 | 3,082 | 2,296 |
| M68000 / 1,024 | 3,592 | 3,452 | 2,676 |
| M68000 / 4,098 | 10,080 | 9,958 | 8,936 |
| M68EC020 / 770 | 3,134 | 3,030 | unavailable |
| M68EC020 / 832 | 3,264 | 3,154 | unavailable |
| M68EC020 / 4,098 | 10,150 | 10,018 | unavailable |

Two unchanged 572-row `compiled-raster-v7-crossover-phase-envelope` runs agree
exactly. Three warmups and 21 samples cover both CPUs, zero/patterned fills,
sync/async flags, packed dimensions and dense crossover sizes. Controls include
installed .59, pre-regression .56 and candidate DMA-disabled CPU execution.
Every result checks output, guards and preserved public registers. No row exceeds
either prior ROM by more than the existing eight-cycle phase envelope; maximum
increases over .59 are four cycles on 68000 and two on EC020. The 512-byte
synchronous path is still CPU-only and has not been optimized in this change.
Kickstart retains lower fixed overhead; its EC020 fixture is unavailable.

The separate instruction profile identifies the saving. For 68000 asynchronous
512-byte clears, ownership/public dispatch outside the generated kernel falls
from 526 to 396 cycles; DMA helper work falls from 544 to 536. Total call time
falls from 1,434 to 1,296 cycles (9.6%), excluding the later completion wait.
For synchronous 832-byte clears, ownership/dispatch falls from 514 to 396,
DMA helper work from 524 to 510, and call time from 3,110 to 2,998. Earlier
submission shifts some cycles into the final busy wait, so instruction-profile
components are not independent median savings. Remaining costs include C#
dispatch/frame setup and the retained ownership protocol, in addition to DMA.

These are PAL 8375 / OCS Denise guest-cycle diagnostics, 2 MiB Chip RAM,
display DMA disabled and IPL 7. They are not host-throughput acceptance results.
Full rows and producer evidence: `CopperStart/Docs/NativeBltClearOwnership20261104.md` and `CopperStart/Docs/Measurements/NativeBltClearOwnership20261104.json`.

## Behavior, ROM and provenance

The standard ROM is **524,288 bytes**, with **522,256 used**, **2,032 padding**,
108 bytes before the footer and checksum FFFFFFFF. The clear, helpers and entry
adapter total **1,124 bytes**, 12 fewer than .59. The contiguous graphics block
stays 26,400 bytes. All guest bodies execute from ROM.

Compiler checks pass 1,065 cases, including access order/width, all CCR
combinations, busy loops, dead-register conditions and explicit call metadata.
The frozen graphics suite passes 237 cases; 19 optional cases remain unavailable.
Historical expanded-image and unrelated ColorMap32 exclusions are unchanged.
New 68000/EC020 tests trap the public WaitBlit vector while raster, text and
bitmap operations complete with correct pixels and guards. Both fail against
the frozen .59 emitter as expected. BltClear also passes queued-arrival injection
at every shared ownership instruction boundary on both CPUs. Existing public
WaitBlit register-preservation, OCS/ECS, pending DMA and split-clear cases pass.

Paired A1200 desktop profile/control replays match CPU state, full Chip RAM,
pixels, audio, display registers, milestones and scripted Tab input. Readiness
remains 12.0792278880542 emulated seconds; this change makes no boot-speed claim.
The pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

ROM SHA-256: `9E871250A72E568FA844751140D6B391A71893C405913E96E749CA1D87027F9F`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Seventeen bounded emitter files change from the frozen .59 composition, retaining
its .49 reset input and exec-preemption.10 HDF. Concurrent producer development
is excluded. Exact sources, compiler/runtime receipts, profiles, tests, paired
benchmarks and size proof are local in the producer under
`artifacts/graphics-bltclear-owner-20261103/rom-qualified`; the date reflects the
start of the task. Consumer replay artifacts use the same task directory name.
ROMs, media and artifacts remain uncommitted; no package was published.


Integration builds pass for both production solutions (12 existing producer
warnings, zero consumer warnings). The default consumer build disables CopperStart;
the enabled build explicitly uses `-p:CopperScreenEnableCopperStart=true`.
Three working-source image identity checks and all 11 app-native desktop/DMA
checks pass. Resource readback verifies the .60 ROM, unchanged HDF and manifest.
The separate host suite has 91 passes, eight unavailable optional cases and
three failures in concurrently modified launcher code/tests: first-run ROM
wording, retaining a selected ROM when changing machines, and accepting a
previously rejected ROM selection. These paths do not execute the graphics
kernel; their assertions and launcher behavior were not changed by this task.
They remain failed evidence, not graphics qualification passes.
