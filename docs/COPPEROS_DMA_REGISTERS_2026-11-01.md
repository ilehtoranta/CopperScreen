# CopperOS solid-fill DMA registers — 2026-11-01

Development qualification `.51` bundles the shorter native DMA fill path. It
preserves [`.50` Layer ownership](COPPEROS_LAYER_OWNER_2026-11-01.md), the
[`.49` cold-boot fix](COPPEROS_COLD_BOOT_2026-10-31.md), and the existing desktop HDF.
The qualified source freezes the previous Graphics/Layers composition and changes
executable code only in [RectangleFillDma.cs](../../CopperStart/native/CopperStart.Graphics/Blitter/RectangleFillDma.cs).
Concurrent new graphics features and system-ROM work remain separate.

Selected-plane Chip/Fast classification happens before drawing. The committed
DMA path keeps geometry in registers and saves only public A0. It programs ECS
BLTSIZV once and BLTSIZH per plane when enhanced Agnus/Alice is present; OCS
retains classic BLTSIZE and its hardware limits. Valid Fast RAM bitmaps retain
CPU fallback. Modes, masks, ABI, Layer ownership, preceding-blit fences and the
asynchronous final plane remain intact.

The existing small-fill CPU crossover stays: 128 measured CPU/DMA control cases
match pixels, and the common two-plane 16×8 rectangle still favors CPU words.
OCS/ECS tests additionally cover maximum size encodings, classic-limit fallback,
effective signed modulo, planes, modes and actual size-register writes.

## Kickstart comparison

The retained `rectfill-flat-layer-profile-v1` protocol measures completed guest
cycles, including the last blit. PAL 8375/OCS Denise, 2 MiB Chip RAM, depth 2,
stride 80, display DMA off, IPL 7, three warmups and 21 samples are unchanged.
The flat and single visible Simple Layer use identical coordinates. `.50` is
the retained prior measurement; `.51` and Kickstart were measured in this work.

| MC68000 completed cycles | Kickstart v40.63 | `.50` | `.51` |
| --- | ---: | ---: | ---: |
| RectFill 16×8, flat | 2,362 | 2,856 | 2,854 |
| RectFill 16×8, Layer | 3,570 | 5,106 | 5,108 |
| RectFill 184×32, flat | 6,456 | 6,960 | 6,824 |
| RectFill 184×32, Layer | 7,278 | 8,648 | 8,516 |

Large flat calls improve **2.0%**, layered calls **1.5%**. The isolated large-fill
DMA helper drops **1,736 → 1,604 cycles (7.6%)**; its Layer entry drops
**1,694 → 1,560 (7.9%)**. Small CPU fills are effectively unchanged. EC020 large
flat/Layer medians fall 7,016 → 6,850 and 8,786 → 8,630. Pixel/guard hashes match
the Kickstart control. Large MC68000 calls remain 5.7%/17.0% slower than Kickstart;
parity is not claimed. Active-display and host-throughput acceptance were not measured.

## ROM and app replay

The embedded ROM is exactly **524,288 bytes**, with **521,024 used**, 3,264 padding
bytes and **216 contiguous bytes before the footer**. It is 24 bytes smaller
than `.50`; the ROM checksum is `FFFFFFFF`. ROM SHA-256:
`E4C346913C1D8A4805BFE6A38DF2F83337CA3583A50E0C3C1F021157898BD939`.
The unchanged 8 MiB HDF SHA-256 is
`DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.

The manifest, staging check and build gate pin this ROM. The actual built app's
embedded ROM/HDF/manifest were extracted through resource streams and hashed.
Independent scalar/normal A1200 replays agree on readiness, milestones, CPU
state, full Chip RAM bytes, pixels, audio, display registers and final frame/cycle.
They finish at field 1,008 / cycle 143,238,816 after aligned input. Ready time
remains **12.0792278880542 guest seconds**; no boot-time reduction is claimed.
Desktop pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
The v4 replay omits unavailable fine GUI/Home maps, so no DrawChanges duration
is claimed.

The production solution builds. App checks pass: **11 native boot/DMA tests**
and **69 launcher/update tests**. Three optional workspace cases are unavailable.
The producer solution also builds with 14 existing warnings and no errors.
The frozen producer graphics suite passes **218 tests** plus two additional
signed-modulo boundary cases, with four optional probes unavailable;
the independent crossover/size run and working-source
identity/color-map checks each pass five. Historical expanded-code identity
exclusions and unrelated working-tree features remain outside this qualification.

Local evidence: consumer `artifacts/dma-registers-20261101/` contains build/test
logs, embedded resource hashes, paired replay outputs and `replay-parity.json`.
Producer `artifacts/dma-registers-20261101/` contains exact frozen source/runtime
inputs, diagnostic controls, native comparisons, attribution, size proof and
logs. See [the producer report](../../CopperStart/Docs/NativeGraphicsDmaRegisters20261101.md).
No ROM/media, build artifacts or package release are committed or published.
