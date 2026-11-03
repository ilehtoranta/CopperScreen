# CopperOS medium BltClear qualification — 2026-11-01

Development bundle `diskboot-2026-11-01-a1200-desktop-single-bank.57` optimizes
the C# CPU clear with 32-byte blocks and skips remainder dispatch for complete
blocks. CopperSharp68k folds contiguous RAM stores plus their pointer advance
into post-increment stores, preserving memory order, register results and flags.
The guest implementation remains C#, compiled to code that executes from ROM.

Median completed public BltClear plus WaitBlit cycles, flags 0:

| CPU / bytes | Previous `.56` | New `.57` | Kickstart 40.63 |
| --- | ---: | ---: | ---: |
| 68000 / 64 | 1,056 | 788 | 510 |
| 68000 / 128 | 1,536 | 1,016 | 716 |
| 68000 / 256 | 2,496 | 1,472 | 956 |
| 68000 / 510 | 4,400 | 2,470 | 1,476 |
| EC020 / 64 | 976 | 792 | unavailable |
| EC020 / 256 | 2,134 | 1,442 | unavailable |
| EC020 / 510 | 3,668 | 2,386 | unavailable |

The 256-byte reduction is 41% on 68000 and 32% on EC020. Kickstart's measured
medium path uses DMA and still has lower fixed overhead. The 356-row
`compiled-raster-v5-medium-clear` record uses three warmups, 21 samples,
completed-output/register/guard checks and a DMA-disabled CPU control. Two final
runs agree exactly. This is diagnostic guest timing, not host-throughput
acceptance or a boot-speed claim.

The faster CPU loop shifts selection: asynchronous DMA remains eligible at
512 bytes, synchronous DMA at 896 bytes, and OCS partial rows below 1 KiB use
the CPU. These are shared cutoffs, not CPU-specific tuning. Some cases regress:
the worst measured case is a patterned synchronous 770-byte 68000 clear,
3,264 to 3,596 cycles (10.2%). Large asynchronous clears add about 28–32 cycles.
The complete results and limitations are retained in the producer's
`Docs/NativeBltClearMedium20261101.md` and
`Docs/Measurements/NativeBltClearMedium20261101.json`.

Ownership, Agnus fencing, wait flags, patterns, packed sizes, valid word alignment,
Fast RAM and OCS/ECS behavior are preserved by the tested cases. The standard
ROM is exactly **524,288 bytes**, with **522,008 used**, 2,280 padding bytes,
108 bytes before the footer and checksum FFFFFFFF. Its 876-byte generated kernel
fits the existing ROM gap. No engine, scheduler, CPU timing, extension bank or
executable RAM changes are included.

Validation passes 919 selected compiler cases, 228 frozen graphics cases and
three working-source image checks. Nineteen optional graphics cases are
unavailable; historical expanded-image and unrelated ColorMap32 exclusions
are unchanged. Both production solutions build (12 existing producer warnings,
no consumer warnings).
All 11 app native desktop/DMA checks and 69 launcher/update checks pass; eight
optional host cases remain unavailable.

Paired A1200 desktop replays match CPU, full Chip RAM, pixels, audio, display
registers, milestones and Tab input. Readiness remains 12.0792278880542 emulated
seconds. Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

ROM SHA-256: `25746125D5321953CA8C588936B2FEC10BD2FF8B5117F41E76A3159BA8C2340E`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The manifest and project hash gate select `.57`; the built ROM/HDF/manifest
resources have been read back and verified. Qualification uses the frozen `.56`
composition and `.49` reset input; concurrent producer development is excluded.
Exact sources, runtime/ROM receipts, profiles and tests remain in producer
`artifacts/graphics-bltclear-medium-20261101/`; paired captures remain under that
path in this consumer. ROMs/media/artifacts remain uncommitted. No package is published.
