# CopperOS direct clipped drawing - 2026-10-31

Development qualification `.46` follows [the owner-kernel work](COPPEROS_LAYER_KERNEL_2026-10-31.md).
It keeps `.45`'s frozen reset/compiler, graphics composition and Layers platform
inputs, with the unchanged exec-preemption.10 desktop HDF. Separate color-map/
backfill source work and the whole-system ROM migration remain outside this image.

## Guarantees used by the direct path

The Layer wrapper holds the Layer grant, selects visible/Smart storage, intersects
the request with its ClipRect and clips to that bitmap's physical envelope. Its
last Y emptiness check now computes height minus one in D6. A successful check
therefore establishes nonempty, nonnegative storage coordinates and a live height.

The private raster entry accepts those facts. The public graphics wrapper already
protects the caller's registers, so the inner frame saves only the four live Layer
context addresses A2-A5. D0-D7/A0-A1 are private scratch; A6 remains the GfxBase.
Public register preservation and the caller's RastPort are unchanged. Mode and pen
selection still occur per clip, preserving the existing callback/input boundaries.

ClipRect bounds are signed WORDs. After Smart-storage translation, local coordinates
can reach 65535. The private entry checks the maxima's WORD sign bit: when either
is set it retains the former full-frame WORD conversion/check path. Otherwise the
wrapper's ordering/clipping proves that all coordinates fit nonnegative WORDs.
It can skip repeated sign extension, X ordering and height calculation.

The word helper has an admitted entry after its repeated nonnegative-coordinate
check. DMA geometry is selected directly through a ROM continuation in A4: whole
bitmap, public rectangle, or Layer-admitted rectangle. Only the Layer entry skips
its repeated negative-coordinate rejection. CPU/DMA crossover decisions, stride,
height, word count, selected-plane alignment, Chip-range/end-wrap admission and
preflight-before-submission remain. Blitter ownership, destination/register fences,
no-effect completion and asynchronous final-plane behavior remain intact.

Pixel code is emitted from the same tiny source routine for the two epilogues.
The larger byte compositor stays shared. Its private frame selector uses bit 31
of the internal mode register, whose original value is a zero-extended UBYTE;
the compositor reads the ordinary low mode bits. This retains the public caller's
A0 without an extra spill. Word/DMA returns remain direct. No private ownership,
queue/lifetime admission or scheduling predicate was removed or cached.

## Measurements

| MC68000 completed median cycles | Kickstart | `.45` | `.46` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,856 | 2,856 |
| RectFill 16x8, layered | 3,570 | 6,520 | 6,342 |
| RectFill 184x32, flat | 6,456 | 7,200 | 7,212 |
| RectFill 184x32, layered | 7,278 | 9,570 | 9,442 |

Small layered latency falls **2.7%** and large layered latency **1.3%**.
The small trace's raster-entry/mode/frame bucket falls **444 to 300 cycles
(32.4%)**, and word admission/preflight **440 to 400**. The CPU row loops
remain 616 cycles; pixels are not omitted. The owner kernel is unchanged.
Small layered RectFill still takes **1.78x** Kickstart's cycles; large takes
1.30x. Parity is not claimed. Native 68EC020 completed layered medians fall
6,746 to 6,562 and 9,712 to 9,588; original-020 timing is not claimed.

Flat word timing is unchanged. The flat large-DMA control adds 12 MC68000
cycles, including dispatch/bus phase. In the separate stride-128 idle/pending
pipeline protocol, the flat odd-plane byte fallback increases 8,052 to 8,076
MC68000 cycles (**24 cycles, 0.3%**). The shared compositor's frame selection and
geometry dispatch have this bounded cost; the report does not claim every path
is faster. Its caller A0 and all memory guards remain preserved.

The final paired desktop replay agrees exactly between scalar profiling and
normal execution in CPU, full Chip RAM, pixels, audio, cycles, frames, display
registers and milestones. Pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
DrawChanges falls **26.126513 to 25.939307 ms**; key-down to return falls
71.048339 to 70.861133 ms. Cold ready remains 14.543149995 seconds. The same
fixed eight-field input sequence starts at absolute field 1,000.


## Image size and identity

| Item | Value |
| --- | --- |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used code/data, vectors, command bridge and footer | 520,688 bytes |
| Padding | 3,600 bytes |
| ROM SHA-256 | `B658DE652BDCD73F46C8038CEDA2EB6328CD8A8A1C070D8AAE7E4D2050BB5F0C` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The image grows by 242 bytes relative to `.45`. Graphics totals 26,090 bytes,
including the 928-byte initializer at `$FF9860` and 25,162 contiguous bytes at
`$FF9C00`. Footer headroom is 418 bytes; checksum is `$FFFFFFFF`.
The raw compiler/reset SHA remains
`8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`.
All new routine bodies execute in ROM. The app resource and build-time hash pin
select this exact image.

## Validation and limits

The frozen selection passes **210 graphics tests**, with **two optional crossover
benchmarks unavailable**. All **34 flat-call rows** match original. All **16 owned
Layer rows** match `.45` state/pixels, and MC68000 also matches original. The
**168 text rows** and **20 idle/pending pipeline rows** preserve pixels, together
with pending-blit overlap, caller/register, mode/mask, multi-clip storage,
backfill/callback, private admission/lifetime, nesting/queue/publication and
Forbid-depth controls. The new word-boundary theories cover 576 shape/mode/mask
combinations across both CPUs and pointer alignments, plus full-buffer guards.

Both Release solutions build successfully; producer retains existing warnings,
consumer has none. The app passes **11 native graphics/desktop checks** and
**69 launcher/update checks**, with three optional workspace cases unavailable.
The separate working-source image passes **six identity/color-map checks**.
The four previously documented extended-image identity cases retain their old
expectations and remain excluded. The three separate color-map tests are outside
this frozen selection, as described below. No emulator or CPU package is changed.


Four new theory cases cover both CPUs and even/odd selected planes. Across six
Smart-storage shapes, all eight draw modes and masks 0/2/3, independent pixel/
guard oracles check normal coordinates, 32767/32768 crossings and high WORD rows,
plus caller state. These controls retain the existing signed-WORD legacy behavior
for manually constructed extreme surfaces; original-ROM coverage comes from the
normal multi-clip Simple/Smart/mode and callback controls. Existing byte-fallback
A0 preservation tests caught an initial return-dispatch regression; the final
frame-selector scheme passes them and avoids that spill.

The selected drawing protocol remains `rectfill-flat-layer-profile-v1`; owned
Layer comparisons retain `installed-owned-layer-drawing-cycles-v1`: PAL 8375/OCS,
2 MiB Chip RAM, display DMA off, IPL 7, 3 warmups and 21 samples, median completed
guest cycles. Original v40.63 SHA is
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.
Original-020 timing and formal host-throughput acceptance are not claimed.
Every instruction-attribution sum equals the observed return, and the map tool
checks the complete emitted code against the ROM. The owner kernel bytes remain
identical to `.45`, although bus phase can change its attributed cycles.

`Freeze.targets` retains the prior composition/platform inputs and matching
qualified image identities. Normal working-source identities and the three
separate color-map controls use their distinct working image. The three color-map
cases are outside this frozen image. Four documented extended-image identity
cases remain excluded with unchanged expected hashes. Optional unavailable cases
are reported separately. No unrelated feature-source changes were reverted.

Exact ROM/map/size proof, source/emitter/diagnostic snapshots, hashes, before-image
kernel bytes, traces, original comparisons and logs are retained in producer
`artifacts/layers-direct-20261031/rom-qualified` and its parent. Consumer paired
captures are in `artifacts/layers-direct-20261031/release-{profile,control}`.
ROMs/media, artifacts and credentials are not committed or published.
