# CopperOS flat drawing — 2026-11-01

Development qualification `.52` shortens small CPU fills and internal blitter
ownership. It preserves [`.51` register DMA](COPPEROS_DMA_REGISTERS_2026-11-01.md),
the earlier Layer ownership and cold-boot fixes, and the unchanged desktop HDF.
The frozen composition excludes concurrent graphics features and system-ROM work.

Small fills retain aligned geometry instead of recomputing it. Normal hardware
drawing uses the established valid-bitmap guarantees and skips the alignment
scan; explicitly disabled DMA retains the old unaligned CPU extension. Fast
RAM and mixed-memory bitmaps remain supported.

Explicit raster call sites now call internal ROM ownership entries. Public
OwnBlitter/DisownBlitter keep their tag checks; the shared owner body retains
A0 preservation, closed-state handling, atomic reservation, contention, FIFO
handoff and release. No scheduler changes, executable RAM or extra guest
allocation are introduced. New preemption probes verify queued arrivals at
every shared ownership boundary reached by the raster path.

Implementation and full evidence: [producer report](../../CopperStart/Docs/NativeGraphicsFlatCalls20261101.md),
[small fills](../../CopperStart/native/CopperStart.Graphics/Raster/RectangleWords.cs),
[ownership](../../CopperStart/native/CopperStart.Graphics/Blitter/OwnershipFast.cs).

## Measurements

The retained protocol measures completed guest cycles with PAL 8375/OCS Denise,
2 MiB Chip RAM, depth 2, stride 80, display DMA off, IPL 7, three warmups and
21 samples. Flat and single-visible-Simple-Layer cases use matching coordinates.
Final DMA completion is included. Pixel/guard hashes match Kickstart v40.63.

| 68000 completed cycles | Kickstart | `.51` | `.52` |
| --- | ---: | ---: | ---: |
| RectFill 16×8, flat | 2,362 | 2,854 | 2,510 |
| RectFill 16×8, Layer | 3,570 | 5,108 | 4,972 |
| RectFill 184×32, flat | 6,456 | 6,824 | 6,760 |
| RectFill 184×32, Layer | 7,278 | 8,516 | 8,448 |

Small flat calls improve **12.1%**, closing **69.9% of the Kickstart gap**.
They remain 6.3% slower; large flat calls improve 0.9% and remain 4.7% slower.
Independent runs have identical medians. Separate small-flat controls measure
2,622 cycles for preparation alone and 2,742 for ownership alone, versus 2,510
combined. The attributed preflight halves from 440 to 220 cycles; row loops
remain 616 cycles. EC020 small/large flat calls fall 2,878 → 2,564 and
6,850 → 6,788. These diagnostics do not establish active-display timing or
host-throughput acceptance.

## Bundle and validation

The ROM is exactly **524,288 bytes**, with **521,044 used**, 3,244 padding bytes
and **196 contiguous bytes before the footer**. The checksum is `FFFFFFFF`.
The manifest, staging script and build gate pin SHA-256
`380278E9A7485EBAA5F1EB5A3F50AE7876C956CD6CB03C529A52EA5671F1E556`.
The unchanged 8 MiB HDF SHA is
`DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The built app's actual embedded ROM, HDF and manifest were inspected and hashed.

Both production solutions build. The frozen graphics suite passes **222 tests**,
with four optional probes unavailable. Seven working-source identity/color-map/
internal-owner cases pass separately. Component benchmarks and an independent
full comparison also pass. Historical expanded-code identity exclusions and
unrelated working-tree features remain outside the frozen qualification.
The app passes **11 native boot/DMA tests** and **69 launcher/update tests**;
three optional workspace cases are unavailable.

Independent scalar/normal A1200 replays match milestones, CPU state, full Chip
RAM bytes, pixels, audio, display registers, PNG and final frame/cycle. Ready time
remains **12.0792278880542 guest seconds**; the aligned interaction finishes at
field 1,008 / cycle 143,238,816. Desktop pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Fine GUI/Home symbols remain unavailable in optional-symbols-v4; no DrawChanges
duration or boot-time gain is claimed.

Local consumer evidence is under `artifacts/flat-call-20261101/`, including
`replay-parity.json`, embedded hashes and build/test logs. Frozen source/runtime,
maps, instruction traces, individual controls and ROM proof are retained in
producer `artifacts/flat-call-20261101/`. ROM/media and artifacts remain
uncommitted; no package is released.
