# BltClear setup and argument reduction — 2026-11-04

The `.63` A1200 bundle retains C# BltClear and reduces generated setup work.
CopperSharp68k eliminates dead Boolean materialization, constant-compare
transport and temporary copies used only to test a flag bit. Compile-time
liveness/CCR and entry-point proofs guard those rewrites; no guest checks are
added. C# shares one custom-register base through setup and single-transfer
paths without increasing the save frame. Ownership, queue/waiter handoff,
fences, hardware write order and CPU/DMA thresholds retain their behavior.

## Results

Three identical 572-row installed-ROM runs use the existing v7 protocol:
public BltClear plus public WaitBlit, three warmups, 21 samples, display DMA off,
IPL 7. These are guest-cycle results, not host throughput/FPS. Original EC020
comparison remains unavailable. CPU-control and crossover limits pass unchanged.

| 68000 synchronous zero clear | .62 | .63 | Kickstart 40.63 |
|---|---:|---:|---:|
| 16 bytes | 506 | 496 | 544 |
| 512 bytes | 2,314 | 2,310 | 1,636 |
| 704 bytes | 2,662 | 2,614 | 1,976 |
| 768 bytes | 2,812 | 2,762 | 2,196 |
| 832 bytes | 2,866 | 2,818 | 2,296 |
| 1,024 bytes | 3,254 | 3,210 | 2,676 |
| 4,098 bytes | 9,758 | 9,706 | 8,936 |

Of 566 clear rows, 555 improve and 11 match; none regress. Maximum savings
are 64 cycles on 68000 and 78 on EC020. The ordered 832-byte probe reduces
pre-DMA work 930→884 cycles and 100→91 instructions; completion remains 158.
Kickstart starts after 306 cycles/28 instructions and completes in 94 cycles
after final DMA. Dispatch, argument allocation and saves remain substantial.
Observed/unobserved profile executions match. No scheduling occurs in this
uncontended IPL-7 probe. No Kickstart parity is claimed.

The generated C# unit shrinks 1,186→1,138 bytes. The standard 524,288-byte ROM
uses **522,274 bytes**, 48 fewer than `.62`; padding is 2,014 bytes and footer
headroom is 104. Checksum is FFFFFFFF. System bodies remain in ROM.

## Validation

1,210 selected compiler tests pass, including 16 new rewrite cases exercised
on 68000/68020 with all CCR combinations, condition inversions, live values,
live flags and interior entries. 69 focused graphics tests, the 239-case frozen
graphics suite, ordered-cost probe and three scoped source identities pass.
Nineteen optional graphics cases remain unavailable; historical expanded-image
and ColorMap exclusions remain. All 11 app-native desktop/DMA tests pass.
Embedded-resource readback verifies the .63 ROM/manifest and unchanged HDF.
Both production solutions build: 13 existing producer warnings, no consumer
warnings, including the explicitly enabled bundle build.

Paired A1200 replays match desktop pixels, CPU, full Chip RAM, audio, registers,
milestones and scripted input at field 1000. Readiness remains
12.0792278880542 emulated seconds; no boot-time improvement is claimed.
The retained replay runtime is frozen separately from concurrent engine work.

ROM SHA-256: `60FF44EFB254F4E751989F12A45A6B0A6B4004AAADF103A40B44160754E6EB9A`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Desktop pixels: `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

Producer details and rows are in `CopperStart/Docs/NativeBltClearSetup20261104.md`
and `CopperStart/Docs/Measurements/NativeBltClearSetup20261104.json`. Exact
sources/runtimes, profiles, benchmarks and tests are local under
`CopperStart/artifacts/graphics-bltclear-setup-20261104/rom-qualified`.
Consumer records use the same task directory name. Only the generated kernel
emitter file changes from frozen `.62`; the `.49` reset and exec-preemption.10
HDF remain. Other producer changes are excluded. Media/build outputs remain
uncommitted; this is not a package release or installed MSIX qualification.
