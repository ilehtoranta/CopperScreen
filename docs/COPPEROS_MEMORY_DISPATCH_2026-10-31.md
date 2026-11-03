# CopperOS bitmap memory dispatch - 2026-10-31

Development qualification `.47` follows [direct Layer drawing](COPPEROS_LAYER_DIRECT_2026-10-31.md).
It retains `.46`'s frozen compiler/reset, graphics composition and Layers platform
inputs and the unchanged exec-preemption.10 desktop HDF. The only guest code
change in this qualification is the shared RectFill/SetRast DMA setup.

## Valid inputs and memory selection

The DMA helper reads Exec's Chip RAM end once and compares each selected plane's
base against it. A selected Fast RAM plane sends the complete operation to the
CPU compositor before DMA geometry or submission. Masked-out planes do not affect
this decision. Independently allocated planes can mix Chip and Fast memory, so
checking only the first plane would be incorrect, especially for complement:
falling back after submitting earlier planes could apply their operation twice.
There are no TypeOfMem calls.

Valid caller allocations, plane alignment, even stride and sufficient storage are
assumed. Removed checks include selected-plane alignment, zero/odd stride,
full accessed-span calculation, ChipEnd-minus-span admission and wrap/extent
validation. The stack workspace shrinks by four bytes. No validation is added
inside the DMA submission loop.

Remaining decisions select an algorithm: DMA availability, representable blitter
height/word count/modulo, the measured small-fill crossover and the existing
signed-WORD translation fallback. They send unsupported DMA shapes to CPU drawing;
they do not reject an otherwise supported operation. Layer clipping still supplies
visible/Smart geometry. Blitter ownership, pending-transfer fences, asynchronous
final-plane completion and private Layer ownership/lifetime rules are preserved.

This follows the caller-validity policy in the producer AGENTS.md. The
[RectFill autodoc](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_3._guide/node0313.html)
requires ordered bounds. The
[original BltBitMap contract](https://developer.amigaos3.net/autodocs/graphics.library/BltBitMap.html)
explicitly assigns safe arguments/ranges to the caller; it is supporting policy,
not evidence that BltBitMap's particular size limits apply to RectFill. Fast RAM
CPU drawing is tested as the requested supported extension. Original timing and
pixel comparisons below use the retained Chip RAM fixtures.

## Measurements

`rectfill-flat-layer-profile-v1`: PAL 8375/OCS, 2 MiB Chip, display DMA off,
IPL 7, depth 2, stride 80, identical flat/Layer coordinates, three warmups and
21 samples; median completed guest cycles.

| MC68000 call | Kickstart | `.46` | `.47` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,856 | 2,854 |
| RectFill 16x8, layered | 3,570 | 6,342 | 6,342 |
| RectFill 184x32, flat | 6,456 | 7,212 | 6,960 |
| RectFill 184x32, layered | 7,278 | 9,442 | 9,202 |

Large flat latency falls **3.5%**, layered **2.5%**. Small CPU-word paths are
effectively unchanged. The large flat call remains **1.08x** original and layered
**1.26x**; small layered remains **1.78x**. Kickstart parity is not claimed.
The original v40.63 SHA is
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.

MC68000 instruction attribution puts large flat DMA setup/submission at **1,738**
cycles, down from **1,988**; layered falls **1,946 to 1,698**. The selected-plane
memory-class loop itself costs **190** cycles for these two selected planes.
Each attribution accounts for every observed instruction through return; the
map tool checks the complete emitted code against the ROM. The Layer owner
kernel bytes are unchanged. DMA wait cycles and bus phase still affect totals.

On 68EC020, large flat falls **7,226 to 7,016** and layered **9,588 to 9,358**;
small flat/layered remain **2,878/6,562**. Original-020 timing is not claimed.

The separately versioned `raster-pending-blit-overlap-v2-valid-bitmaps` records
16 rows. It removes v1's malformed odd Chip-plane fixtures; historical v1 evidence
is retained unchanged. Idle MC68000 DMA rectangle completion falls **7,176 to
6,928**, SetRast **68,730 to 68,588**. With an earlier transfer pending, rectangle
is **38,398 to 38,388**, while SetRast is **100,368 to 100,388**. Thus pending work
can absorb the setup saving, and that SetRast row is 20 cycles slower. No blanket
speedup or host-throughput acceptance is claimed.

The paired A1200 desktop captures match exactly in CPU, full Chip RAM, pixels,
audio, cycles, fields, display registers and milestones between profiling and
normal execution. With the fixed Tab input at field 1,000, DrawChanges falls
**25.939307 to 25.750974 ms**; key-down to drawing return falls **70.861133 to
70.672800 ms**. Cold-ready remains **14.543150 guest seconds**. Pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

## Qualification

The frozen selection passes **212 graphics tests**, with **two optional crossover
benchmarks unavailable**. All **34 flat-call rows** match original, all **16 owned
Layer rows** preserve baseline state/pixels (MC68000 also matches original), all
**168 text rows** preserve pixels, and all **16 v2 idle/pending rows** preserve
pixels. Mode/mask, edge, register/caller, pending-transfer, multiple clip storage,
backfill/callback and Layer queue/lifetime controls pass.

The two new Fast RAM theories run **768 operation/mode/mask/memory combinations**
across MC68000/68EC020, plus two exact ChipEnd boundary cases. They configure a
real mapped 512 KiB Fast bank, check full plane buffers and guards, preserve caller
registers and verify DMA is used only when every selected plane is Chip RAM.
This includes a late Fast plane under complement and an unselected Fast plane.
The app's bounded launch configuration remains unchanged.

Malformed odd-stride/odd Chip-plane DMA fixtures no longer define a graceful
fallback requirement. Existing unaligned CPU-compositor extension cases explicitly
disable DMA. Smart-storage boundary tests now compare aligned storage with DMA
available versus disabled. These test changes are explicit; no prior expected
pixels or historical records were relabeled.

Both Release solutions build successfully (producer's 13 existing warnings;
consumer zero). The app passes **11 native graphics/desktop checks** and **69
launcher/update checks**, with three optional workspace cases unavailable. The
separate working-source image passes **six identity/color-map checks**. The four
previously documented extended-image identity cases retain their expectations
and remain excluded; three separate color-map cases remain outside this frozen
composition. Unrelated feature work and the system-ROM migration were preserved.

| Bundle fact | Value |
| --- | --- |
| Qualification | `diskboot-2026-10-31-a1200-desktop-single-bank.47` |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used / padding | 520,634 / 3,654 bytes |
| ROM SHA-256 | `43B3EB26B206BF5F9DBD192402C5B767CC5CA9FD9902359DCE75717AB9BC0EF6` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The ROM shrinks **54 bytes**. Graphics totals 26,036 bytes: a 928-byte initializer
at `$FF9860` and 25,108 contiguous bytes at `$FF9C00`. Footer headroom is 472
bytes and checksum `$FFFFFFFF`. Raw compiler/reset SHA remains
`8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`.
All routine bodies execute in ROM. The app resource and build-time hash pin select
this exact image; binaries/media remain local and unpublished.

Exact source/emitter/diagnostic snapshots, hashes, ROM/map/size proof, original
comparisons, traces and logs are retained in producer
`artifacts/graphics-memory-dispatch-20261031/rom-qualified` and its parent.
Consumer captures and paired verification are in
`artifacts/graphics-memory-dispatch-20261031/qualified-{profile,control}` and
`paired-verification.json`. These are guest-cycle diagnostics, separate from
historical host-throughput acceptance protocols.
