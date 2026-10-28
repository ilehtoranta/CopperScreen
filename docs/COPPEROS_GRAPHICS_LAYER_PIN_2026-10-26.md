# Permanent graphics Layers dependency — 2026-10-26

The graphics-layer-pin.24 development bundle follows
[graphics-text-rows.23](COPPEROS_GRAPHICS_TEXT_ROWS_2026-10-26.md).
The [producer record](../../CopperStart/Docs/NativeGraphicsLayerPin20261026.md)
links implementation, benchmark protocol and original Kickstart controls.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `e979b03c414de730128ca1011d0d9eb89b321d1b69650faf9e65c19fad4438bb` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The permanent standalone graphics library retains one open Layers reference in
the SDK `gb_LayersBase` field. It previously opened and closed that library on
every layered draw. Atomic first-use publication retires competing surplus opens;
missing Layers retains the public semaphore fallback. Private live Layer
admission, nested/contended locking, clipping and blitter completion remain.
The unloadable extended graphics composition is unchanged.

The actual desktop already blits text: all sampled calls use compatible fixed
eight-pixel topaz cells, and native BLTSIZE writes confirm DMA. The larger cost
was surrounding library and ownership dispatch.

A new installed benchmark measures real owned Layers separately from the flat
v4 protocol. Across 16 rows on MC68000/020, with and without outer Layer locks,
warm completed call costs fall approximately 34–55%. Pixels, sentinels and cursor
positions match all eight original MC68000 controls. Representative cycles:

| Layered call, MC68000 | .23 | .24 | Kickstart |
| --- | ---: | ---: | ---: |
| RectFill 16×8 | 50,124 | 22,966 | 3,572 |
| Text 8 | 57,242 | 30,084 | 10,136 |
| Text 64 | 80,364 | 53,218 | 32,258 |

The desktop profile and normal execution agree exactly at **753 fields /
107,002,809 cycles**. Cold readiness falls 15.064→14.904 guest seconds;
Tab repaint falls **240→180 ms**, **12→9 PAL fields**. Pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
The new observer uses verified entry markers and inclusive call latencies;
older profiling protocols and their unavailable graphics attribution are unchanged.

All 140 producer graphics tests pass, zero skips. A separate build passes three
installed comparisons and 14 identity/dependency/Layer tests, reproducing all
34 v4 medians and 16 new Layer rows exactly. Ten pre-staging consumer pixel tests
and 11 staged native desktop/graphics tests pass. CopperScreen Release builds
cleanly. Source ownership verifies 203 imports. The full producer solution still
has three pre-existing Shell/DOS API errors.

Final local input: `../CopperStart/artifacts/graphics-layer-pin/qualified.rom`.
The firmware contract/build gate pin this image; binaries remain ignored.
Reset remains exact .9–.23; HDF/GUI/Home and CPU/chipset execution are unchanged.
Personal disks need no migration. Standalone/resident code is 25,676/29,583 bytes.
This is a development qualification. Guest cycles are separate from host
throughput acceptance; remaining Kickstart performance gaps stay active.
