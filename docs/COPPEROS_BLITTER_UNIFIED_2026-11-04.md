# Unified C# blitter ownership — 2026-11-04

The `.67` A1200 bundle uses one C# ownership counter for all native graphics
clients, public Own/Disown, queued blits, sleeping tasks and shutdown. CopperSharp
emits direct `ADDQ.W/SUBQ.W offset(A6)` transitions. The uncontended paths no longer
load task state, publish Owner/Nest or inspect pending lists. Queue callbacks,
FIFO grants, close races and the separate WaitBlit contract retain their behavior.
Contended Own uses the reserved blitter signal, matching Kickstart.

BltClear remains C# with Fast RAM support and unchanged CPU/DMA thresholds.
No emulator or shared Copper68k package change is included. Public/vector and
private callers share the compiled ownership implementation; recorded internal
calls/tail calls bind directly in ROM.

Two identical 572-row v7 comparisons use three warmups and 21 samples, display
DMA off, IPL 7, and public BltClear plus WaitBlit. Of 566 clears, 271 improve,
273 match, and 22 CPU-path rows are 2–10 cycles slower. CPU helper bytes are
unchanged; bus/polling phase affects measurements. Getter controls and retained
crossover limits pass. These are guest cycles, not host FPS; the original EC020
fixture is unavailable.

| Bytes, synchronous zero | .66 | .67 | Kickstart 40.63 |
|---:|---:|---:|---:|
| 16 | 442 | 442 | 544 |
| 512 | 2,250 | 2,250 | 1,636 |
| 704 | 2,426 | 2,324 | 1,976 |
| 768 | 2,544 | 2,442 | 2,196 |
| 832 | 2,632 | 2,532 | 2,296 |
| 1,024 | 3,008 | 2,922 | 2,676 |
| 4,096 | 9,268 | 9,168 | 8,916 |
| 4,098 | 9,496 | 9,398 | 8,936 |

Public 68000 Own/Disown medians fall from 256/256 to 64/64 cycles; Kickstart is
62/64. The separate ordered 832-byte clear probe reduces pre-DMA work 724→658
cycles and post-DMA work 118→84. Public dispatch remains a cost; full BltClear
has not reached Kickstart parity, and synchronous 512-byte CPU clears are unchanged.

The C# BltClear unit is 1,020 bytes. The standard 524,288-byte ROM uses 521,638
bytes (600 fewer), with 2,650 padding bytes and 634 bytes before its fixed footer.
Checksum FFFFFFFF. The HDF is unchanged. The private blitter record ABI is CMBD.

1,394 selected compiler regressions and 225 frozen graphics cases pass. The
ordered probe and installed ownership comparison pass separately; 19 optional
graphics cases remain unavailable. Preemption/FIFO/close tests, Fast RAM and
OCS/ECS DMA guards pass. Paired A1200 replays match CPU state, full Chip RAM and
pixels. Ready remains 12.0792278880542 guest seconds; no boot-speed gain is claimed.

ROM SHA-256: `2370986078FF7AE7A731E851F025F3EB1F798718A1A553BCF5940B00F3791BA0`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.

Producer detail: `CopperStart/Docs/NativeBlitterUnified20261104.md` and its
measurement JSON. Exact evidence: producer and consumer
`artifacts/graphics-blitter-unified-20261104`; measured ROM and frozen producer
inputs are in the producer's `rom-qualified` directory. The production solution
uses generated source and the verified local bundle, not a sibling compiler at
runtime. ROM/media/build artifacts are not committed.

Production builds: CopperStart Release passes (13 warnings in existing dependency,
compiler and desktop/DOS sources); CopperScreen default and enabled Release builds
pass without warnings. Three current-source identity checks plus four contended
register cases pass. All 11 app native boot/drawing tests pass. Their isolated
fixture now runs the ROM AUTOINIT routine, with bounded Exec allocation/server
stubs, instead of assuming a zeroed ownership gate. The embedded ROM, unchanged
HDF and `.67` manifest hashes are verified.

Within the producer archive, `provenance.json` identifies the active `*-v67`
capsules; inherited `.66` capsules remain historical evidence.

A final 114-case compiler atomic/bit-update/byref pass also succeeds after the
atomic fixtures were moved into the dedicated internal-ABI test assembly.
