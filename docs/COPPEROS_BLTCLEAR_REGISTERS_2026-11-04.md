# BltClear register allocation — 2026-11-04

The `.64` A1200 bundle keeps BltClear in C# and reduces dispatch/argument work
through CopperSharp68k. Proven word multiplication and constant Int32 shifts
use allocated registers instead of fixed D0/D1 staging. The main method saves
12 rather than 16 bytes; the C# unit shrinks 1,138→1,120 bytes. Ownership,
CPU/DMA selection, hardware access order, direct waits and completion semantics
retain their behavior.

## Results

Three identical 572-row installed-ROM runs use the unchanged v7 protocol:
public BltClear plus public WaitBlit, three warmups, 21 samples, display DMA off,
IPL 7. Results are emulated guest cycles, not host throughput/FPS. Original
EC020 comparison remains unavailable. CPU-control/crossover limits pass.

| 68000 synchronous zero clear | .63 | .64 | Kickstart 40.63 |
|---|---:|---:|---:|
| 16 bytes | 496 | 478 | 544 |
| 512 bytes | 2,310 | 2,286 | 1,636 |
| 768 bytes | 2,762 | 2,702 | 2,196 |
| 832 bytes | 2,820 | 2,784 | 2,296 |
| 1,024 bytes | 3,210 | 3,176 | 2,676 |
| 4,096 bytes | 9,450 | 9,424 | 8,916 |

Of 566 clear rows, 565 improve and one matches; none regress. Maximum savings
are 82 cycles on 68000 and 72 on EC020. Getter controls match. Kickstart is still
faster for medium DMA clears. Baselines above belong to this paired run.

The ordered 832-byte probe reduces pre-DMA work 884→846 cycles and 91→83
instructions. Kickstart uses 306 cycles/28 instructions. Public dispatch/argument
cost falls 368→342 cycles; save/restore work outside DMA falls 140→124.
Completion remains 158 cycles versus Kickstart's 94. Observed/control CPU,
RAM and cycles match. Busy intervals are sampled at instruction boundaries;
these are not exact bus timestamps. No scheduling occurs in the IPL-7 probe.

The standard 524,288-byte ROM uses **522,256 bytes**, 18 fewer than `.63`.
Padding is 2,032 bytes, footer headroom 104 and checksum FFFFFFFF. System
bodies remain in ROM.

## Validation

1,432 selected compiler cases pass, including 27 added execution/IR cases for
packed counts, live signed/unsigned product operands, shifts and register
constraints. Arithmetic execution covers 68000/68020/68040, optimized and
unoptimized forms, bounds, checked and wide operations. 69 focused graphics
cases, the 239-case frozen graphics suite, ordered-cost probe and three scoped
source identities pass. Nineteen other optional cases remain unavailable;
historical expanded-image/ColorMap exclusions remain.

Both production solutions build: 12 producer warnings, zero consumer warnings,
including the enabled bundle. All 11 app-native desktop/DMA cases pass.
Embedded-resource readback verifies the .64 ROM/manifest and unchanged HDF.
Paired frozen A1200 replays match pixels, CPU, full Chip RAM, audio, registers,
milestones and field 1000 scripted input. Readiness remains
12.0792278880542 emulated seconds; no boot-time improvement is claimed.
Concurrent engine changes are excluded from the frozen replay runtime.

ROM SHA-256: `153AF4DA33CA8AE266108091A4E40FAF8DB83538374148933B61EEB6A540BFAC`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Desktop pixels: `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

Producer details are in `CopperStart/Docs/NativeBltClearRegisters20261104.md`
and `CopperStart/Docs/Measurements/NativeBltClearRegisters20261104.json`.
Exact sources/runtimes, benchmarks, traces and tests are local under
`CopperStart/artifacts/graphics-bltclear-registers-20261104/rom-qualified`.
Consumer records use the same task directory name. Only the generated graphics
emitter file changes from frozen `.63`; the `.49` reset and exec-preemption.10
HDF remain. Other producer development is excluded. Media/build outputs remain
uncommitted; this is not a package release or installed MSIX qualification.
