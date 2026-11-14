# Remainder-first BltClear — 2026-11-04

The `.69` A1200 bundle clears the remainder first, then submits the largest DMA
transfer last. This allows asynchronous split clears to return with useful
blitter work still running. A one-word remainder uses a CPU store after the
initial fence. OCS smaller full-row groups precede maximum-sized transfers;
ECS combines the remaining full rows into one transfer.

BltClear remains C#. Separate split bookkeeping keeps the common save frame
small, and both paths share completion/release code. The initial fence reuses the
custom-register base while retaining both reads. Ownership, direct waits, Fast
RAM support and the synchronous flag retain their behavior. No emulator or
compiler change is included in this qualification.

| 68000 asynchronous zero clear | .68 return | .69 return | Kickstart return |
|---|---:|---:|---:|
| 4,098 bytes | 9,274 | 1,040 | 480 |
| 8,194 bytes | 17,618 | 1,042 | 480 |
| 131,074 bytes | 267,778 | 1,042 | 488 |
| 131,200 bytes | 267,880 | 1,394 | 784 |

These are guest cycles to return, not to complete the transfer. For 131,074 bytes,
total completion changes from 267,862 to 267,762 cycles. Across the new split
matrix, asynchronous completion changes by -132 to +82 cycles; synchronous split
completion is 26–148 cycles slower from bookkeeping. The main gain is CPU/DMA
overlap. Kickstart still has lower setup/return overhead.

The retained 572-row completion benchmark passes unchanged timing limits:
260 clear rows improve, 288 match and 18 are slower by up to 76 cycles. Both that
matrix and the new 40-row return/completion matrix repeat byte-identically.
Three warmups and 21 samples, display DMA off, IPL 7; no host FPS claim. Original
EC020 timing remains unavailable.

The frozen graphics suite passes 233 cases. Eight expanded instruction-boundary
queue tests and thirteen current-source identity/ownership/split cases also pass.
The ordered probe passes; 19 optional graphics cases remain unavailable. Paired
A1200 replays match CPU state, full Chip RAM and pixels. Ready remains
12.0792278880542 guest seconds; no boot-speed gain is claimed.

The standard 524,288-byte ROM uses 521,632 bytes, only 6 more than `.68`.
Checksum FFFFFFFF; the HDF is unchanged. Default/enabled Release builds pass and
embedded ROM/HDF/manifest hashes are verified. Detailed measurements and source
provenance are in `CopperStart/Docs/NativeBltClearRemainder20261104.md`, its JSON,
and producer/consumer `artifacts/graphics-bltclear-remainder-20261104`. Other
producer/emulator changes are excluded; ROMs and media remain local artifacts.

All 11 app native boot/drawing tests pass against the embedded `.69` ROM.

ROM SHA-256: `062276B650438A8E8937427CE432B6EF9C84CFBBEA0EDAF0B7CB134AC7658DB2`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
