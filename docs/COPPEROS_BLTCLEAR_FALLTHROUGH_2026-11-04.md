# BltClear acquisition fallthrough — 2026-11-04

The `.68` A1200 bundle keeps BltClear in C# and makes uncontended ownership fall
through: `ADDQ.W #1,$AA(A6)` followed by a not-taken `BNE.S` to the contended
handler. CopperSharp places the small rejoining handler before the DMA loop
chain, retaining its existing fallthrough edges. Zero-fill setup also falls
through. A compiler improvement replaces flag-dead logical shifts by 16 with
`CLR.W; SWAP`, reducing pattern extraction cost without increasing code size.

Atomic ownership, contention, FIFO grants, queued blits, close races, register
preservation, Fast RAM support and CPU/DMA selection retain their behavior.
Internal calls remain directly bound in ROM. No emulator or Copper68k package
change is included.

Two identical 572-row v7 comparisons use three warmups and 21 samples, display
DMA off, IPL 7, and public BltClear plus WaitBlit. Of 566 clears, 397 improve,
157 match and 12 are 2–12 cycles slower. Existing crossover limits and getter
controls pass unchanged. These are guest cycles, not host FPS; the original
EC020 fixture is unavailable.

| 68000 synchronous clear | .67 | .68 | Kickstart 40.63 |
|---|---:|---:|---:|
| 832 bytes, zero | 2,532 | 2,520 | 2,296 |
| 1,024 bytes, zero | 2,922 | 2,890 | 2,676 |
| 1,024 bytes, pattern | 2,962 | 2,922 | 2,696 |
| 4,096 bytes, zero | 9,168 | 9,154 | 8,916 |

The ordered 832-byte zero trace reduces pre-DMA work from 658 to 644 cycles and
67 to 65 instructions. Complete timings also depend on bus and polling phase;
the change does not make every case faster or reach Kickstart parity.

The compiled BltClear unit is 1,008 bytes, 12 fewer than before. The standard
524,288-byte ROM uses 521,626 bytes, with 2,662 padding bytes and 634 bytes before
the fixed footer. Checksum FFFFFFFF. The HDF is unchanged.

1,971 selected compiler tests and 228 frozen graphics cases pass. The ordered
probe and repeat benchmark pass separately; 19 optional graphics cases remain
unavailable. Nine current-source identity/ownership cases pass. Paired A1200
replays match CPU state, full Chip RAM and pixels. Ready remains
12.0792278880542 guest seconds; no boot-speed gain is claimed.

CopperStart Release builds with 13 existing warnings. CopperScreen default and
enabled Release builds pass without warnings. Embedded ROM/HDF/manifest hashes
are verified. Producer detail is `CopperStart/Docs/NativeBltClearFallthrough20261104.md`
and its measurement JSON. Exact input/runtime snapshots, repeated matrices,
profiles, tests and replays are in producer and consumer
`artifacts/graphics-bltclear-fallthrough-20261104`. Other producer/emulator work is
excluded from this qualification. No ROMs, media or build artifacts are committed.

All 11 app native boot/drawing tests pass against the embedded `.68` ROM.

ROM SHA-256: `B51D7283A233570ADED7965210472A3AE4B398AA326556E9BAC587D054DC9692`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
