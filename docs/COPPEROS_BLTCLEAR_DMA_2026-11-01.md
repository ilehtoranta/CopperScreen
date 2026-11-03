# CopperOS C# BltClear DMA qualification — 2026-11-01

Development bundle `diskboot-2026-11-01-a1200-desktop-single-bank.55` adds blitter
clears to the compiled C# graphics implementation. Even Chip RAM spans of at
least 512 bytes use DMA when enabled. Smaller spans, Fast RAM, odd-count extensions
and disabled DMA use the improved longword CPU loop. Ownership, wait flags,
pending-blit ordering, fill patterns, packed sizes and caller registers are retained.
ECS size registers and bounded OCS transfers are exercised on both CPUs.

The ROM is exactly **524,288 bytes**, with **521,768 used**, 2,520 padding bytes
and 108 bytes free before the footer. The 636-byte compiled clear and helpers
execute from existing ROM padding before the graphics initializer. No executable
RAM copy or host graphics gateway is introduced.

| Completed 4 KiB clear, flags 0 | Previous `.54` | New `.55` | Kickstart 40.63 |
| --- | ---: | ---: | ---: |
| 68000 | 65,798 | 9,584 | 8,796 |
| EC020 | 53,610 | 9,656 | unavailable |

These are median guest cycles including public BltClear and WaitBlit, with output
and memory guards checked. The producer's `compiled-raster-v3-bltclear-dma` record
contains 180 rows, three warmups and 21 samples, plus a forced CPU control. This
is diagnostic timing, not a host-throughput acceptance result. Tiny clears regress
from the added dispatch and pending-DMA fence checks: a 2-byte 68000 clear rises
from 294 to 594 cycles. Large clears improve substantially; Kickstart remains
faster. The 512-byte cutoff wins against the improved CPU control in every measured
DMA case, including separate OCS/EC020 tests with awkward transfer sizes.

The compiler change fixes deletion of a live AND-mask constant used by a later
comparison; 905 selected compiler regression cases pass. The clear remains C#,
including CPU/DMA selection and hardware setup. The original source, detailed
protocol, limitations and measurements are in the producer's
`Docs/NativeBltClearDma20261101.md` and
`Docs/Measurements/NativeBltClearDma20261101.json`.

Paired normal/scalar A1200 desktop replays match CPU state, full Chip RAM, pixels,
audio, display state, milestones and scripted Tab input. Readiness remains
12.0792278880542 emulated seconds. Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
No boot-speed improvement is claimed.

Both production solutions build. Validation passes 225 frozen graphics cases
across the main run and a corrected harness-output rerun, three working-source
image checks, 11 app native desktop/DMA checks and 69 launcher/update checks.
Nineteen optional graphics cases and eight optional host cases are unavailable;
historical expanded-image and unrelated ColorMap32 exclusions are unchanged.
Two final installed benchmark runs agree on all 180 rows. The ROM/HDF/manifest
resources have been read back and verified from the built application.

ROM SHA-256: `DE62F11BE95EBB9E37DD3B8E579E6A8502D143380191B3DAEC53B9C6288580C4`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The manifest and project hash gate select the staged `.55` ROM. This qualification
applies the change to the frozen `.54` graphics composition and `.49` reset input;
concurrent producer development is excluded. Exact source/runtime/ROM receipts
and tests are retained in the producer's `artifacts/graphics-bltclear-dma-20261101`;
paired desktop captures are retained under the same path in this consumer.
ROM/media/build artifacts remain local and uncommitted; no package is published.
