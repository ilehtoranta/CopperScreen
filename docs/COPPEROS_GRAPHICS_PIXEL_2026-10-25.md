# Pixel fills and text loops checkpoint — 2026-10-25

The graphics-pixel.12 development bundle adds synchronized CPU bit operations
for one-pixel RectFill, shares fill span/channel setup, reduces duplicate raster
work and tightens bounded text loops. Larger rectangles keep the blitter path.
Private lifetime/ownership checks, IRQ/FIFO behavior, layer locks and raster
synchronization remain. See the
[producer record](../../CopperStart/Docs/NativeGraphicsPixel20261025.md) for
source, original controls, corrected font probes and remaining work.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `19e030ecf5bf3d019265c05463e57c89b2629a2f94218051debf512dbd96a6d5` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The reset bank remains byte-identical to .9/.10/.11. Standalone/resident graphics
grow by 26 bytes to **23,526/27,433**. HDF/GUI/Home, CPU/chipset execution and
package pins remain qualified inputs. The firmware contract and build hash gate
select this ROM. Personal disks do not require an HDF migration.

## Result and verification

All 34 installed-library cases match selected original V40.63 state, pixels and
defined results. A fresh isolated build reproduces all native medians exactly.
Selected **completed median guest cycles**, including WaitBlit for drawing:

| Call | Before (.11) | After (.12) | Kickstart |
| --- | ---: | ---: | ---: |
| RectFill 1x1 | 3670 | 2262 | 2266 |
| RectFill 8x8 | 4694 | 4422 | 2270 |
| Text 64 characters | 40434 | 38752 | 30480 |
| Text 8 characters | 14258 | 14112 | 7912 |

One-pixel fills now have effectively the same cost as Kickstart. Long text is
4.2% faster than .11. Other measured gaps remain active optimization work.

Protocol-v4 desktop profiler and normal ExecuteFrame control agree at
**764 fields / 108,565,953 cycles**, including CPU registers, RAM, pixels and
PCM. Cold readiness remains **15.063985 guest seconds**; Tab redraw remains
**12 PAL fields / 0.240381 seconds**. This checkpoint does not claim a desktop
frame-count reduction. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain explicitly unattributed in the desktop observer.

- **44 producer tests pass**, zero skips. New pixel cases match 1,280 original
  mode/pen/mask/position combinations; both native CPU models also verify raster
  guards and waiting for a real earlier 128 KiB DMA clear before CPU access.
- The matrix retains scalar/IRQ controls and now includes 55 in-bounds text
  combinations plus 48 fresh-font layout/spacing controls. Font construction
  precedes selection; glyph-width semantics are unchanged.
- **10 pre-staging pixel oracles pass**, followed by **11 bundled native
  tests**, zero skips, covering masks/modes, shifts, Simple/Smart clipping,
  ROM fonts, long strings, full desktop, Editor/input, reset and cold reopen.
- CopperScreen Release builds with zero warnings/errors. Producer ownership
  checks 201 integration/graphics imports exactly once. Fresh reproduction
  passes three installed tests and seven image tests; its compiler dependency
  retains the existing nullable warning. The full producer solution retains
  the three existing Shell/DOS API errors (`LoadSegmentNative`,
  `UnloadSegmentNative`, `UnloadDosProcessSegment`).

Local evidence is under `../CopperStart/artifacts/graphics-pixel`, the
producer/consumer TRXs and staged ignored bundle. Ownership, other fills/copies
and text remain slower in the cases recorded by the producer. Five flat-text
overflow exclusions and unmeasured layer/queue/complex API cases remain outside
the parity claim. These are modeled guest-cycle results, not physical hardware
timing or host-throughput acceptance.
