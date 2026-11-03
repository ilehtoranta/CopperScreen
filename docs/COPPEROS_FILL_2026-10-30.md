# CopperOS A1200 fill optimization — 2026-10-30

Qualification `diskboot-2026-10-30-a1200-desktop-single-bank.38` updates native
graphics in the [previous single-bank image](COPPEROS_SINGLE_BANK_2026-10-29.md).
The standard file remains **524,288 bytes**, including padding, with used
code/data, vectors, native command bridge and footer totaling **520,116 bytes**.
The graphics resident grows 58 bytes to 25,518 bytes. There are 4,172 padding
bytes overall and 62 bytes between the fixed graphics resident and the footer.

| Artifact | SHA-256 |
| --- | --- |
| A1200 ROM | `3FA5AC6DCBCD1455ECF55581BA64AD143518631423190D84248E6B578DD7E758` |
| Unchanged HDF | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The reset/compiler image, app files and `exec-preemption.10` HDF migration are
unchanged. Concurrent producer Exec ROM migration and Classic Intuition source
changes are outside this qualification. The machine remains PAL Alice/Lisa,
68EC020, 2 MiB Chip RAM, with one F80000 ROM and no companion bank.

SetRast now reuses whole-bitmap geometry. Shared plane-range arithmetic removes
repeated additions while retaining DMA alignment and range constraints. The
caller already waits for an idle blitter, so the first plane omits its duplicate
wait; subsequent planes still synchronize. Hardware size/modulo setup uses WORD
operations. Solid full-word fills skip unused A/C channel setup; masked and
complement operations retain it.

The unchanged installed-call protocol compares median MC68000 guest cycles
through completion against original Kickstart v40.63, with display DMA off.
All 34 rows match state/pixels:

| Call | Kickstart | Previous `.37` | New `.38` |
| --- | ---: | ---: | ---: |
| SetRast 640x70, depth 2 | 23,904 | 25,498 | 24,892 |
| RectFill 184x64 | 11,174 | 12,140 | 11,988 |
| RectFill 600x64, unaligned | 31,518 | 32,392 | 32,294 |

The producer graphics selection passes 186 tests and skips 8 unavailable
optional cases. Four previously documented extended-image identity failures
are explicitly excluded; their expectations were not changed. Native and
original controls cover selected-plane masks, maximum DMA sizes, earlier busy
DMA, prior custom-chip state, exact Chip RAM end and fallback arguments.

The bundled app passes all 11 native graphics/desktop cases, including editor,
reset and cold reopen. Launcher/migration checks pass 69 cases, with 3 optional
workspace cases unavailable. The consumer Release solution builds without
warnings or errors. The producer Release solution also builds, with 11 existing
warnings. Its first attempt encountered a concurrent A500 boot migration's
temporarily missing CallThunkAddress; the retry after that source update passes.
That migration is not included in the frozen qualified reset image.

The desktop boots and completes Tab selection. The profile and uninstrumented
control agree on CPU, complete Chip RAM, pixels, audio, cycles, frames and
milestones. Pixels match `.37` exactly:
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Guest measurement | Previous `.37` | New `.38` |
| --- | ---: | ---: |
| Cold desktop ready | 14.583213769 s | 14.583213769 s |
| DrawChanges, Tab at field 1,000 | 41.633880 ms | 41.534074 ms |
| Key-down to DrawChanges return | 86.555706 ms | 86.455900 ms |

The input scenario retains its eight-field floor. These are guest-cycle
diagnostics, separate from host throughput. SetRast remains 4.13% slower than
Kickstart; the broader graphics and cold-boot performance goal remains open.

Ignored local producer evidence is under
`CopperStart/artifacts/graphics-rast-20261029`, including final `registers`
ROM/inputs/map/manifest, benchmark JSON and TRX logs. Consumer captures are
under `artifacts/graphics-fill-20261030/{control,profile}`. No ROM, media or
generated build artifacts are committed or published.
