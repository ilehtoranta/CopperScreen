# Rectangle-fill setup checkpoint — 2026-10-25

The graphics-fill.15 development bundle reduces RectFill/SetRast register
frames, mask arithmetic and hardware-register addressing. It follows
[copy.14](COPPEROS_GRAPHICS_COPY_2026-10-25.md). The
[producer record](../../CopperStart/Docs/NativeGraphicsFill20261025.md) links
the implementation, SDK layout, tests and measurement protocol.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `72720bf3160bdee59a677c77e6d050b6158d37e08eadabd804ed6b96620ba3ad` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. Reset stays exact
.9–.14; GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks
require no HDF migration. Standalone/resident graphics shrink by 60 bytes to
23,452/27,359 bytes. DMA admission, complete selected-plane preflight, CPU
fallback geometry and the final asynchronous blit remain covered.

Completed median guest cycles, including public WaitBlit:

| Call | Before (.14) | After (.15) | Kickstart |
| --- | ---: | ---: | ---: |
| RectFill 1×1 | 1,806 | 1,774 | 2,266 |
| RectFill 8×8 | 3,964 | 3,578 | 2,270 |
| RectFill 16×4 | 3,980 | 3,594 | 2,270 |
| RectFill 16×8 | 3,994 | 3,594 | 2,368 |
| RectFill 64×2 | 3,982 | 3,594 | 2,274 |
| RectFill 184×64 | 12,472 | 12,232 | 11,174 |
| RectFill 600×64 unaligned | 32,782 | 32,514 | 31,518 |
| SetRast 640×70, depth 2 | 25,878 | 25,604 | 23,904 |

All 34 comparisons retain selected original V40.63 state/pixel/result matches;
an independent build reproduces every native median. The small fill gains 9.7%.
Substantial setup and ownership gaps remain.

All **90 producer graphics tests pass**, zero skips. Seven new cases cover
edge masks, eight-plane selection, early/late fallback, original pen retention,
maximum DMA size and original-ROM controls. The independent build passes three
installed comparison facts and 14 image/fill-setup cases, zero skips. Ten
pre-staging consumer pixel tests pass. Source ownership checks 201 retained
imports exactly once. CopperScreen Release builds with zero warnings/errors;
the full producer solution retains three existing Shell/DOS API errors.
After staging, all 11 bundled native tests pass with zero skips, covering pixel
oracles and desktop boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution control agree at 764 fields /
108,565,984 cycles, including registers, RAM, pixels and PCM. Cold readiness is
15.063982018 guest seconds; Tab redraw is 12 PAL fields / 0.240387578 seconds.
This is 46 total cycles above .14 with unchanged frame counts, so no desktop
speedup is claimed. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain unattributed in this observer.

Local evidence: `../CopperStart/artifacts/graphics-fill/qualified.rom`,
`qualified-emission`, `qualified-full-calls`, `qualified-reproduction`, paired
`qualified-desktop-*` reports and producer/consumer TRXs. The ignored staged
bundle is the app input. Complex/unmeasured APIs, contention throughput and five
existing flat-text overflow exclusions remain outside the comparison. These
results are guest cycles, not host acceptance or physical timings.
