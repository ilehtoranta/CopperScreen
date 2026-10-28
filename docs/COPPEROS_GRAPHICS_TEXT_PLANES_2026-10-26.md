# Shared text-plane setup — 2026-10-26

The graphics-text-planes.20 development bundle reduces repeated text setup.
It follows [graphics-release.19](COPPEROS_GRAPHICS_RELEASE_2026-10-26.md).
The [producer record](../../CopperStart/Docs/NativeGraphicsTextPlanes20261026.md)
links implementation, original controls and detailed evidence.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `d45e1746322d99fc8c4158268b1b6cfc88a2f7d73638b7e25c5351fbf7f55505` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. Reset remains exact
.9–.19. GUI/Home/HDF, CPU and chipset execution remain unchanged. Personal disks
need no HDF migration. Standalone/resident code shrinks by 158 bytes to
24,072/27,979 bytes.

Text uses the per-plane minterms maintained by the public pen/mode setters.
Preflight calculates its common plane extent once. Rendering configures the
region-constant blitter registers once and retains the source, size and offset
across planes. Per-plane BDATA reset and DMA completion before stack-template
release remain, along with destination admission, layer clipping and ownership.
The consumer pixel fixture now uses SetABPenDrMd instead of unsupported direct
pen-field stores; expected pixels remain independently checked.

Completed median guest cycles include public WaitBlit:

| Call | Before (.19) | After (.20) | Kickstart |
| --- | ---: | ---: | ---: |
| Text, 8 characters | 11,740 | 10,798 | 7,912 |
| Text, 64 characters | 35,148 | 34,200 | 30,480 |

These are 8.0% and 2.7% reductions under the unchanged MC68000/PAL/OCS protocol.
All 34 selected original V40.63 state/pixel/result comparisons match; a separate
build reproduces every native return/completed median. Text still exceeds
Kickstart's cost, and ownership/fill/copy gaps remain active work.

All **115 producer graphics tests pass**, zero skips. Three new cases exercise
eight-plane text modes, setter changes, masks, alignments and dirty prior blitter
state, including an original-ROM control. Existing staging, ownership, installed
IRQ and 2,016-case crossover coverage remains. Independent verification passes
three installed comparisons and ten identity/text-plane cases. Ten pre-staging
consumer pixel tests and all 11 bundled native tests pass, zero skips.
CopperScreen Release builds with zero warnings/errors. Source ownership verifies
202 retained imports. The full producer solution retains three existing Shell/DOS
API errors.

Desktop profiler and normal execution agree at **764 fields / 108,565,962 cycles**,
including registers, RAM, pixels and PCM. Cold readiness is 15.063979199 guest
seconds; Tab redraw is 12 PAL fields / 0.240387296 seconds. Frame counts are
unchanged; the 30-cycle total difference from .19 is not a desktop speedup claim.
Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics attribution remains unavailable in this observer.

Local evidence: `../CopperStart/artifacts/graphics-text-planes`, including the
immutable qualified ROM, full call comparison, independent reproduction,
compositions, crossover, desktop reports and TRXs. The ignored staged bundle is
the app input. Complex APIs and five existing flat-text overflow exclusions remain
outside these comparisons. These are guest-cycle results, not host acceptance or
physical timings.
