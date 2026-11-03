# CopperOS shared rectangle geometry — 2026-11-01

Development qualification `.53` follows [`.52` flat drawing](COPPEROS_FLAT_CALLS_2026-11-01.md).
It preserves the earlier ownership, DMA and cold-boot work and the existing HDF.
Concurrent graphics features and system-ROM changes remain outside the frozen
composition.

RectFill prepares its word span once for CPU admission and DMA. CPU plane loops
keep depth and Mask:Pen in registers; two-word paths select complement outside
the plane loop and pair rows when at least six rows qualify. Odd heights retain
an initial single row. Plane/row access order, masks, ABI, Fast RAM fallback,
ownership and preceding/final DMA fences remain. The disabled-DMA unaligned CPU
extension is retained. No memory-type queries or scheduler changes are added.

Implementation: [producer report](../../CopperStart/Docs/NativeGraphicsRectGeometry20261101.md),
[CPU words](../../CopperStart/native/CopperStart.Graphics/Raster/RectangleWords.cs),
[DMA](../../CopperStart/native/CopperStart.Graphics/Blitter/RectangleFillDma.cs).

## Comparison

The unchanged protocol measures completed guest cycles with PAL 8375/OCS Denise,
2 MiB Chip RAM, depth 2, stride 80, display DMA off, IPL 7, three warmups and
21 samples. Final DMA completion is included. Flat/Layer coordinates match;
pixel/guard hashes agree with Kickstart v40.63.

| 68000 completed cycles | Kickstart | `.52` | `.53` |
| --- | ---: | ---: | ---: |
| RectFill 16×8, flat | 2,362 | 2,510 | 2,424 |
| RectFill 16×8, Layer | 3,570 | 4,972 | 4,894 |
| RectFill 184×32, flat | 6,456 | 6,760 | 6,752 |
| RectFill 184×32, Layer | 7,278 | 8,448 | 8,460 |

Small flat fills improve **3.4%**, remaining **2.6% behind Kickstart**. EC020
small flat fills improve 2,564 → 2,436 (5.0%). Large calls are effectively
unchanged; the 68000 Layer case is 12 cycles slower. Independent runs have
identical medians. Staged small-flat controls measure 2,476 after geometry/plane
preparation, 2,446 after paired rows, and 2,424 with mode selection included.
The attributed row-loop cost falls 616 → 536 cycles.

The separate 864-case row/edge/depth/mode comparison matches pixels throughout:
810 faster, 12 equal, 42 slower. The worst regression is **20 cycles (1.1%)**
in a tiny one-plane inverse fill. These are guest-cycle diagnostics, not
active-display or host-throughput acceptance results.

## Bundle and validation

The ROM is exactly **524,288 bytes**, with **521,218 used**, 3,070 padding bytes
and **22 contiguous bytes before the footer**. Checksum: `FFFFFFFF`.
The manifest, staging script and build gate pin SHA-256
`CF4C19DB8A94E8237993F9E0CDAFAD7123567A11C47E4E59E7044721D1D53895`.
The unchanged 8 MiB HDF SHA is
`DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The actual built app's embedded ROM/HDF/manifest are inspected and hashed.

Both production solutions build. The frozen graphics suite passes **224 tests**;
five conditional entries are skipped in that run, with the new row comparison
executed separately. Older optional probes remain unavailable. Nine working-source
identity/color-map/owner/row-count cases pass separately; historical expanded
identity exclusions remain unchanged. App checks pass **11 native boot/DMA**
and **69 launcher/update** tests; three optional workspace cases are unavailable.

Paired scalar/normal A1200 replays match milestones, CPU state, full Chip RAM,
pixels, audio, display registers, PNG and final frame/cycle. Ready time remains
**12.0792278880542 guest seconds**; aligned interaction finishes at field 1,008 /
cycle 143,238,816. Desktop pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
No boot-time gain is claimed. Fine GUI/Home maps remain unavailable in
optional-symbols-v4; no DrawChanges duration is claimed.

Local evidence is under consumer `artifacts/rect-geometry-20261101/` and producer
`artifacts/rect-geometry-20261101/`, including stage source/runtime/ROM snapshots,
maps, traces, shape-grid regressions, size proof, embedded hashes and replay
parity. ROM/media and artifacts remain uncommitted; no package is released.
