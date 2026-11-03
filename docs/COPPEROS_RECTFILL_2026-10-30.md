# CopperOS RectFill qualification — 2026-10-30

Qualification `diskboot-2026-10-30-a1200-desktop-single-bank.41` follows
[the Layers checkpoint](COPPEROS_LAYERS_2026-10-30.md). The desktop HDF is
unchanged. This development image isolates graphics/Layers changes on the same
frozen reset/compiler inputs; concurrent producer ROM migration is outside its
qualification. Native system changes in this image execute from ROM.

Clipped rectangles now pass selected bitmap storage directly to the shared
raster body. RectFill keeps the original RastPort, removes its 100-byte copy
and public redispatch, and reduces clipping transfers and zero-offset arithmetic.
EraseRect retains its private pen/mode state and callback handling. Shorter
Layers branches retain every private admission check and ownership/scheduling
transition. DMA, word/pixel/byte fallbacks, masks and completed pixels remain
covered by the original controls.

| MC68000 guest cycles, no outer lock | Kickstart | `.40` | `.41` |
| --- | ---: | ---: | ---: |
| RectFill 16x8 | 3,570 | 8,676 | 7,824 |
| RectFill 184x32 | 7,278 | 11,508 | 10,672 |
| Text 8 | 10,136 | 14,208 | 14,176 |
| Text 64 | 32,258 | 36,644 | 36,626 |

The rectangle reductions are 9.8% and 7.3%. Small layered RectFill still costs
2.19x Kickstart; the owner kernel alone accounts for 2,846 of 7,734 cycles in
the final instruction trace. Private admission and locking remain significant.
Protocol `installed-owned-layer-drawing-cycles-v1` is unchanged: PAL 8375/OCS,
2 MiB Chip, display DMA off, IPL 7, 3 warmups, 21 samples, completed median.
These are guest-cycle diagnostics, separate from host-throughput acceptance.
All 16 layer rows match baseline state/pixels across both CPUs and nesting;
MC68000 also matches original v40.63. All 34 flat-call rows match original.

| Bundle identity | Value |
| --- | --- |
| ROM bytes | 524,288 (one standard 512 KiB file) |
| Used code/data, vectors, command bridge, footer | 520,404 bytes |
| Padding | 3,884 bytes (102 more than `.40`) |
| ROM SHA-256 | `53543CB03E846E05B59F606BD9EF29C504D4B37E4934DE99A367D76618C84F71` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Graphics uses 25,806 bytes, including the 928-byte initializer in the checked
preceding gap. Checksum is `$FFFFFFFF`; footer headroom is 702 bytes.
The app bundle and its build-time hash pin select this exact image.

The desktop profile and control agree exactly on CPU, full Chip RAM, pixels,
audio, cycles, frames, display registers and milestones. Framebuffer SHA-256
remains `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| A1200 guest measurement | `.40` | `.41` |
| --- | ---: | ---: |
| Cold ready | 14.563181882 s | 14.563181882 s |
| DrawChanges, Tab at field 1,000 | 29.160717 ms | 28.329849 ms |
| Key-down to DrawChanges return | 74.082543 ms | 73.251675 ms |

The eight-field input scenario is unchanged. Producer graphics validation passes
199 tests; 2 optional crossover cases remain unavailable and 4 known extended
identity failures remain explicitly excluded without changing expectations.
New rectangle controls cover 432 draws per implementation across modes, masks,
Simple/Smart storage and single/multiple clips; baseline and candidate run on
both CPUs, original on MC68000. Existing lock, waiter, nesting, fallback, address
range and 168-row text controls pass. An initial missing output setting was
corrected and that control rerun successfully.

The bundled app passes 11 native graphics/text/desktop checks and 69
launcher/migration checks; 3 optional workspace checks remain unavailable.
Both Release solutions build (producer 13 warnings, consumer zero, no errors).
The producer's current-source solution build is separate from the frozen-reset
image qualification. Exact image, inputs, source and performance records are in
`CopperStart/artifacts/graphics-rectfill-20261030/rom-final`; consumer captures are
under `artifacts/graphics-rectfill-20261030/final-{profile,control}`.
ROM/media and local artifacts are not committed or published.
