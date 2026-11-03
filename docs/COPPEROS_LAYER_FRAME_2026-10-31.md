# CopperOS Layer drawing frames - 2026-10-31

Development qualification `.48` follows [bitmap memory dispatch](COPPEROS_MEMORY_DISPATCH_2026-10-31.md).
It retains `.47`'s frozen compiler/reset, graphics composition and Layers platform
inputs, together with the unchanged exec-preemption.10 desktop HDF. Three guest
source files change; unrelated color-map/backfill and system-ROM work are preserved.

## Changes and guarantees

The public drawing wrapper already preserves D5-D6/A3, and those registers have
no live wrapper values across the private owner calls. The ROM owner kernel now
uses them as scratch and saves only A2/A4. Public graphics register preservation
is unchanged. The wrapper also reuses the owner already in D0 and omits the
extra release-time A6 save/restore: every release caller immediately restores
A6 from the public drawing frame, including the failure/trap path.

Ordinary RectFill does not replace the caller's bitmap or invoke a backfill hook.
It therefore reads the bitmap directly without creating a restoration snapshot.
EraseRect retains its snapshot and hook restoration. Smart-storage translation
reuses the signed ClipRect minima already loaded during intersection, removing
two repeated coordinate reads/sign extensions and four stack reloads.

The private small-word entry assumes valid, word-aligned bitmap storage and an
even stride. It skips the selected-plane alignment walk and shares the existing
pixel-writing loop. A small prefix is emitted from the same geometry/crossover
source for public and private callers; the public CPU fallback retains its
unaligned-storage extension. The ROM grows only 12 bytes overall.

Layer clipping, high-WORD Smart-coordinate behavior, modes/pens/masks, private
schema/lifetime admission, queue and grant-token checks, public semaphore writes,
Forbid/Permit boundaries and asynchronous blitter ownership/completion remain.
No scheduling boundary or destination fence is removed. Small fills retain the
measured CPU-word crossover; larger supported shapes still use DMA. There are
no new memory-type queries.

The implementation is in [DrawingBridge.cs](../../CopperStart/native/CopperStart.Graphics/Layers/DrawingBridge.cs),
[RectangleWords.cs](../../CopperStart/native/CopperStart.Graphics/Raster/RectangleWords.cs) and
[LayersTaskLockCore.GraphicsKernel.cs](../../CopperStart/src/CopperStart.Layers/LayersTaskLockCore.GraphicsKernel.cs).

## Original-controlled measurements

`rectfill-flat-layer-profile-v1`: PAL 8375/OCS, 2 MiB Chip, display DMA off, IPL 7,
depth 2, stride 80, identical flat/Layer coordinates, three warmups and 21 samples.
Numbers are median completed guest cycles, including final DMA completion.

| MC68000 call | Kickstart | `.47` | `.48` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,854 | 2,854 |
| RectFill 16x8, layered | 3,570 | 6,342 | 5,936 |
| RectFill 184x32, flat | 6,456 | 6,960 | 6,960 |
| RectFill 184x32, layered | 7,278 | 9,202 | 9,128 |

Small layered RectFill is **6.4% faster**. Its extra cost above the flat control
falls **3,488 to 3,082 cycles**, an **11.6% reduction**. Large layered latency
falls **0.8%**. The small layered result remains **1.66x Kickstart**, large
**1.25x**; parity is not claimed. Original v40.63 SHA is
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.

On 68EC020, small layered falls **6,562 to 6,126 cycles (6.6%)**, with flat
unchanged at 2,878. Large layered falls **9,358 to 9,266**; the flat control is
7,002 versus 7,016, including bus-phase differences. Original-020 timing is not
claimed.

The MC68000 small-call instruction trace separates the remaining costs:

| Through-return bucket | `.47` | `.48` |
| --- | ---: | ---: |
| Layer owner kernel | 1,830 | 1,732 |
| Layer routing/clipping/frames | 1,690 | 1,616 |
| CPU word fill | 1,618 | 1,384 |
| Of word fill: admission/preflight | 400 | 166 |
| Of word fill: actual inner row loops | 616 | 616 |

Exec Forbid/Permit remains 228 cycles, blitter ownership 494, WaitBlit 84 and
raster entry/mode/frame 300. Trace return totals are 6,258 versus 5,850; each
instruction is accounted for. These single traces have different bus phases
from the completed medians. The map tool verifies exact emitted ROM bytes and
the diagnostic kernel mirror; the public ABI checks are retained on graphics
calls. Only explicit private-kernel tests permit the additional scratch registers.

## Validation and limits

The final frozen selection passes **212 graphics tests**, with zero failures.
Three skipped entries represent **two optional crossover benchmarks** and the
missing native-range theory fixture (its two CPU cases are unavailable). The
frozen range artifact directory was absent; it was not regenerated or counted as
successful coverage. Existing private owner range/schema/lifetime, nesting,
queue-arrival/publication, callback re-admission and Forbid-depth controls pass.

All **34 flat-call rows** match original. All **16 owned Layer rows** preserve
baseline state/pixels; MC68000 also matches original. All **168 text rows** and
**16 idle/pending pipeline rows** preserve pixels. The two new Layer word-storage
cases exercise **64 mode/mask/CPU combinations** with independently mapped Chip
and Fast planes, complete buffers/guards, no-DMA assertions, caller RastPort and
public register preservation. The existing Simple/Smart multi-clip and extreme
WORD-coordinate controls pass.

Both Release solutions build. Producer retains existing warnings; consumer has
none. The separate working composition passes **six identity/color-map checks**.
The app passes **11 native graphics/desktop checks** and **69 launcher/update
checks**, with three optional workspace cases unavailable. Native app coverage
includes desktop boot, Tab selection, editor launch, reset and reopening.
The four historical extended-image identity cases retain their expectations and
remain excluded; separate color-map tests run against their working composition,
as in `.47`. No emulator/CPU package or machine configuration is changed.

The old HDF artifact directory was missing. The exact seed was recovered from
the previous app build's embedded resource and verified against its existing SHA.
The earlier fine-grained desktop DrawChanges replay was not rerun because its
GUI/Home binary/map fixture set was absent. No new desktop redraw duration or
paired scalar/control claim is made. These results are guest-cycle diagnostics,
separate from historical host-throughput acceptance protocols.

## Bundle and evidence

| Fact | Value |
| --- | --- |
| Qualification | `diskboot-2026-10-31-a1200-desktop-single-bank.48` |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used / padding | 520,646 / 3,642 bytes |
| ROM SHA-256 | `2EA533FB2F6D09EA26C94A3C407DB3DBB42FE7158FD7DC196B56F3C1E8BA5E3A` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Graphics totals 26,048 bytes: a 928-byte initializer at `$FF9860` and 25,120
contiguous bytes at `$FF9C00`. Footer headroom is 460 bytes; checksum is
`$FFFFFFFF`. Raw compiler/reset SHA remains
`8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`.
All new code executes in ROM. The local app bundle and build-time hash pin select
this exact image. No binaries, media, artifacts or credentials are committed or
published.

Exact source/emitter/diagnostic snapshots, hash manifests, ROM/map/size proof,
original comparisons, traces and logs are retained in producer
`artifacts/layers-frame-20261031/rom-qualified` and its parent. Consumer build,
native/host test logs and captures are retained under
`artifacts/layers-frame-20261031` and the test output directories. Earlier trial
results remain distinct from the final ROM identified above.
