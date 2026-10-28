# Live bitmap-copy geometry — 2026-10-26

The graphics-copy-admission.21 development bundle reduces repeated BltBitMap
geometry work. It follows
[graphics-text-planes.20](COPPEROS_GRAPHICS_TEXT_PLANES_2026-10-26.md).
The [producer record](../../CopperStart/Docs/NativeGraphicsCopyAdmission20261026.md)
links the implementation, controls and remaining work.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `190c7adac8477e6011107f522d0379b20da4356752a5e06305b6921a677d8587` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. Reset remains exact
.9–.20. GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks
need no migration. Standalone/resident code shrinks by 60 bytes to
24,012/27,919 bytes.

BltBitMap retains coordinates, extents and unsigned strides in scratch registers
through private DMA geometry preparation. It shares row-byte/height calculations
and adds plane extents directly from memory. Alignment, DMA limits, Chip-RAM and
alias admission, ownership, waits, constant planes, public register preservation
and CPU fallback remain.

Completed median guest cycles include public WaitBlit:

| Copy | Before (.20) | After (.21) | Kickstart |
| --- | ---: | ---: | ---: |
| 16×8 | 5,096 | 4,802 | 2,416 |
| 184×64 | 16,244 | 15,984 | 14,580 |
| 600×64 | 43,324 | 43,068 | 41,674 |

These are 5.8%, 1.6% and 0.6% reductions under the unchanged MC68000/PAL protocol.
All 34 selected original V40.63 state/pixel/result comparisons match; an independent
build reproduces every native return/completed median. Small copies remain
almost twice Kickstart's cost. Other graphics performance gaps remain active.

All **118 producer graphics tests pass**, zero skips. Three new cases cover
independent strides, shifts, copy/XOR, upper argument bits, height 1024 and late
DMA refusal. Very large and odd strides have separate native-only coverage;
the original control excludes those extensions after an exploratory disagreement,
retained and explained in the producer report. Existing ownership/IRQ, copy,
text and 2,016-case crossover coverage remains. Independent verification passes
three installed comparisons and ten identity/geometry cases. Ten pre-staging
pixel tests pass. Source ownership verifies 202 retained imports. CopperScreen
Release builds with zero warnings/errors; the full producer solution retains
three existing Shell/DOS API errors.
After staging, all 11 bundled native tests pass, zero skips, including desktop
boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution agree at **764 fields / 108,565,962 cycles**,
including registers, RAM, pixels and PCM. Cold readiness is 15.063979199 guest
seconds; Tab redraw is 12 PAL fields / 0.240387296 seconds, unchanged from .20.
Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics attribution remains unavailable in this observer.

Evidence: `../CopperStart/artifacts/graphics-copy-admission`, including the immutable
qualified ROM, full comparison, independent reproduction, compositions, crossover,
desktop reports and TRXs. The ignored staged bundle is the app input.
Complex APIs and five existing flat-text overflow exclusions remain outside
these comparisons. These are guest cycles, not host acceptance or physical timing.
