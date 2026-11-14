# BltClear private ownership and frames — 2026-11-04

The `.62` A1200 bundle uses compiled C# private ownership and a lighter private
hardware-wait calling convention. The uncontended clear reserves the existing
lock atomically without publishing task-owner/nesting records. Contended grants,
queued work, sleeping waiters, library lifetime and public ownership retain their
existing behavior. Release cannot erase a grant acquired after its lock release.

CopperSharp68k provides `APTR.BitTestAndSet8` as a single guest BSET instruction.
The shared register-free hardware wait takes the caller's existing A0 binding,
removing A6 argument transport and the synchronous completion save frame. Calls
remain direct BSR.W bindings, with no WaitBlit library-vector dispatch.
Asynchronous final DMA remains asynchronous; synchronous completion still waits.

## Results

Identical installed-ROM comparisons contain 572 rows. The protocol measures
public BltClear plus public WaitBlit, warmup 3, 21 samples, display DMA off, IPL 7.
These are guest-cycle results, not host throughput or FPS measurements. Original
EC020 comparison remains unavailable. CPU-control and crossover limits pass.

| 68000 synchronous zero clear | .61 | .62 | Kickstart 40.63 |
|---|---:|---:|---:|
| 16 bytes | 506 | 506 | 544 |
| 512 bytes | 2,314 | 2,314 | 1,636 |
| 704 bytes | 2,812 | 2,662 | 1,976 |
| 768 bytes | 2,932 | 2,810 | 2,196 |
| 832 bytes | 2,984 | 2,862 | 2,296 |
| 1,024 bytes | 3,374 | 3,254 | 2,676 |
| 4,098 bytes | 9,848 | 9,758 | 8,936 |

Of 566 clear rows, 266 improve, 286 match and 14 regress by 2 cycles. Maximum
saving is 150 cycles. The ordered 832-byte profile reduces pre-DMA work 990→930
cycles and completion 202→158. Kickstart still needs only 306 and 94 cycles.
Setup, dispatch and register preservation remain substantial; no scheduling
occurs in this uncontended IPL-7 probe. Observed/unobserved execution matches.

The C# unit is 1,186 bytes. The standard 524,288-byte ROM uses **522,322 bytes**,
126 more than `.61`; padding is 1,966 bytes and footer headroom is 104. Checksum
is FFFFFFFF. All executable system bodies remain in ROM.

## Validation and retained inputs

1,073 compiler tests, 69 focused graphics tests, the 239-case frozen graphics
suite, the ordered-cost probe and three scoped working-source identity checks
pass. Nineteen optional graphics cases remain unavailable; historical expanded
composition/ColorMap exclusions remain. Queue injection at every private-clear
instruction boundary, contended task grants, OCS/ECS splits, Fast RAM, patterns,
guards, pending/disabled DMA and public-WaitBlit traps are covered.

Both production solutions build: CopperStart has 11 existing warnings in other
projects; CopperScreen has none, including the explicitly enabled bundle build.
All 11 app-native desktop/DMA tests pass. Embedded-resource readback verifies
the .62 ROM and manifest plus the unchanged HDF. Receipts are retained in this
task's consumer artifact directory. The unrelated host desktop failures recorded for
`.61` were not reclassified or rerun as graphics evidence.

Paired A1200 replays match pixels, CPU, full Chip RAM, audio, registers, milestones
and scripted input at field 1000. Readiness is unchanged at
12.0792278880542 emulated seconds; no boot-time improvement is claimed. Replay
runtime/source are frozen separately from concurrent engine work.

ROM SHA-256: `7B42E3F056F7A1E8EE23D12751FCC63B7D48359552477A4E7FD86416C31C0DF5`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Desktop pixels: `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

Producer details and machine-readable rows are in
`CopperStart/Docs/NativeBltClearLightweight20261104.md` and
`CopperStart/Docs/Measurements/NativeBltClearLightweight20261104.json`.
Exact frozen source/runtime receipts, profiles and tests are local under
`CopperStart/artifacts/graphics-bltclear-lightweight-20261104/rom-qualified`.
Consumer replay, build and integration records use the same task directory name.
Only three emitter files change from frozen `.61`; `.49` reset and
exec-preemption.10 HDF remain. Concurrent producer changes are excluded.
The app manifest, bundle and hash gate select `.62`. Media/build outputs remain
uncommitted; this is not a package publication or installed MSIX qualification.
