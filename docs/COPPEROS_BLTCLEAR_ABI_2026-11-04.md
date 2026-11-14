# BltClear internal ABI and ownership — 2026-11-04

The `.65` A1200 bundle keeps BltClear in C# and retains Fast RAM CPU support.
CopperSharp68k's explicit internal register ABI removes the entry adapter,
keeps the graphics base in A6, and reduces main/DMA save frames from 12/24 to
8/16 bytes. A private pending word replaces repeated queue-head reads while
preserving FIFO, nesting, ownership and close behavior. Private blitter waits
remain direct hardware calls. CPU/DMA selection and completion semantics
retain their behavior.

Two identical 572-row v7 installed-ROM runs show all **566 clear cases improve**;
six getter controls match. Three warmups, 21 samples, display DMA off, IPL 7,
public BltClear plus public WaitBlit. Results are guest cycles, not host FPS.
Original EC020 comparison is unavailable. CPU-control/crossover limits pass.

| Bytes | .64 | .65 | Kickstart 40.63 |
|---:|---:|---:|---:|
| 16 | 478 | 442 | 544 |
| 512 | 2,286 | 2,250 | 1,636 |
| 704 | 2,580 | 2,444 | 1,976 |
| 768 | 2,702 | 2,562 | 2,196 |
| 832 | 2,788 | 2,624 | 2,296 |
| 1,024 | 3,176 | 3,028 | 2,676 |
| 4,096 | 9,424 | 9,290 | 8,916 |
| 4,098 | 9,674 | 9,514 | 8,936 |

Kickstart remains faster for medium DMA clears. In the separate ordered
832-byte probe, preparation before DMA falls 846→744 cycles and completion
falls 158→118. The public dispatch difference falls 342→294 cycles.
Observed/unobserved probes match cycles, registers and full Chip RAM.

The 524,288-byte ROM uses **522,244 bytes**, 12 fewer than `.64`, with 2,044
padding bytes and 74 bytes before the footer. Checksum: FFFFFFFF.
BltClear's C# unit shrinks 1,120→1,078 bytes; queue maintenance adds 30 bytes.

Validation: 1,567 compiler tests, 73 focused graphics tests, 239 frozen graphics
tests and the ordered profile pass. Nineteen optional graphics cases remain
unavailable. Paired A1200 profile/control desktop/Tab replays match CPU/state,
RAM and pixels. Desktop-ready remains 12.0792278880542 guest seconds; this is
not a measured boot-speed improvement. Eleven app-native tests also pass.
Production builds pass both with and without the CopperStart feature, with
no warnings. Embedded ROM, unchanged HDF and `.65` manifest hashes match.

Qualification: `diskboot-2026-11-04-a1200-desktop-single-bank.65`.
ROM SHA-256: `7BF6DC48CF349A105D4981E7AF9E74F655D9BFAFC28A4D58C4B2DEC35CD2707E`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Producer evidence: `CopperStart/Docs/NativeBltClearAbi20261104.md` and its
`Docs/Measurements/NativeBltClearAbi20261104.json`. Exact source/runtime and
raw results: `CopperStart/artifacts/graphics-bltclear-abi-20261104/rom-qualified`.
Consumer replay/build/embedded-resource evidence uses the same artifact folder
name in CopperScreen. Other producer development is excluded from this bundle.
