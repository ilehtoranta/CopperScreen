# CopperOS BltClear dispatch qualification — 2026-11-01

Development bundle `diskboot-2026-11-01-a1200-desktop-single-bank.56` shortens the
compiled C# clear from `.55`. CPU spans through 16 bytes stay in the dispatch
frame, avoiding a second register-save frame and counted loop. Reordered private
pointer arguments shorten the native adapter without changing the public ABI.
The idle fence retains both required Agnus busy-status reads; DMA enable and
Chip RAM classification occur only on the large-DMA or busy-CPU paths.

The 512-byte DMA threshold, public ownership, wait flags, fill patterns, packed
counts, OCS/ECS setup, Fast RAM and disabled-DMA behavior are retained. No engine,
CPU timing, scheduler or host graphics implementation changes are included.

| Completed clear, flags 0 | Previous `.55` | New `.56` | Kickstart 40.63 |
| --- | ---: | ---: | ---: |
| 68000, 2 bytes | 594 | 554 | 508 |
| 68000, 16 bytes | 696 | 516 | 508 |
| 68000, 64 bytes | 1,056 | 1,056 | 510 |
| 68000, 512 bytes | 2,246 | 2,218 | 1,516 |
| 68000, 4 KiB | 9,584 | 9,556 | 8,796 |
| EC020, 2 bytes | 618 | 554 | unavailable |
| EC020, 16 bytes | 694 | 526 | unavailable |
| EC020, 4 KiB | 9,656 | 9,622 | unavailable |

These are median guest cycles including public BltClear and WaitBlit. The
`compiled-raster-v4-small-clear` grid contains 300 rows, three warmups and 21
samples, with completed output, memory guards and registers checked. It expands
the prior grid with every even size through 32 bytes and small packed spans.
The 16-byte clear improves 26% on 68000 and 24% on EC020. Medium 68000 CPU clears
remain substantially slower than Kickstart. This is diagnostic guest timing,
not host-throughput acceptance or a claim of faster desktop boot.

Instruction profiles show 68000 short-clear stack work falling from 148 to 84
cycles. The public adapter saves another four cycles. The two idle busy checks
still cost 86 cycles in the sampled trace; they retain hardware ordering.
Compiler and DMA setup implementations are unchanged. Detailed methodology,
profile findings and full measurements are in the producer's
`Docs/NativeBltClearDispatch20261101.md` and
`Docs/Measurements/NativeBltClearDispatch20261101.json`.

The standard ROM is exactly **524,288 bytes**, with **521,902 used**, 2,386 padding
bytes, 108 bytes free before the footer and checksum FFFFFFFF. The 770-byte clear
and helpers execute in the existing ROM gap before the graphics initializer.
The only frozen emitter change is the compiler-generated C# kernel source.
There is no executable RAM copy or extension bank.

Paired normal/scalar A1200 desktop replays match CPU state, full Chip RAM, pixels,
audio, display registers, milestones and scripted Tab input. Desktop readiness
remains 12.0792278880542 emulated seconds. Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

Validation passes 228 frozen graphics cases, three working-source image checks,
11 app native desktop/DMA checks and 69 launcher/update checks. Nineteen optional
graphics cases and eight optional host cases are unavailable; prior historical
expanded-image and unrelated ColorMap32 exclusions are unchanged. Both production
solutions build (12 existing producer warnings, no consumer warnings). Two final
300-row comparisons agree exactly. The built application's ROM, HDF and manifest
resources have been read back and verified. Two benchmark rows rise by 2–8 cycles;
these are retained in the producer report, rather than claiming universal gains.

ROM SHA-256: `40D4DA0846422BB4167D17566887F2AA64B4EB121CFFE975267A9F05B123AABD`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Baseline `.55`: `DE62F11BE95EBB9E37DD3B8E579E6A8502D143380191B3DAEC53B9C6288580C4`.
Kickstart: `8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.

The manifest and project hash gate select the staged `.56` ROM. Qualification
uses the frozen `.55` graphics composition and `.49` reset input; concurrent
producer development is excluded. Exact source/runtime/ROM receipts, profiles,
tests and measurements remain in the producer's
`artifacts/graphics-bltclear-dispatch-20261101/`; paired desktop captures are
retained under the same artifact path in this consumer. ROMs, media and build
artifacts remain local and uncommitted. No package is published.
