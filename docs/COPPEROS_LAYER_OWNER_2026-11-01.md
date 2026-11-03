# CopperOS certified Layer ownership - 2026-11-01

Development qualification `.50` preserves [the `.49` cold-boot fix](COPPEROS_COLD_BOOT_2026-10-31.md)
and the unchanged exec-preemption.10 desktop HDF. It shortens the drawing-owner
path introduced by [the Layer frame work](COPPEROS_LAYER_FRAME_2026-10-31.md).
The reset/compiler image remains `.49`; unrelated color-map/backfill and whole
system-ROM source changes remain outside this frozen qualification.

## Ownership guarantees and changes

Graphics already holds a permanent OpenLibrary pin. While publishing that pin
under Disable, it now certifies the owner's live native state and immutable root
schema, version, size, profile and library binding once. It publishes the root
cache before the library pointer. Concurrent surplus opens retain the existing
balanced close/publication path. Unrecognized roots leave the cache zero and use
the checked entry.

Valid Layers supplied by this certified owner have its established descriptor
layout. A successful immediate lock protects that descriptor until release;
the library pin protects the root. Dedicated ROM drawing entries use those
facts without re-reading private headers or revalidating the issued handle.
Callbacks discard that handle, and resumed acquisitions issue none. Both retain
full checked release/admission. The checked generic and checked-held entries
remain available, including their private corruption and lifetime handling.

The uncontended transaction updates only fields that change:

- A nested acquisition/release updates nesting without re-publishing the owner.
- An empty queue's public QueueCount is already -1; it is not rewritten.
- An immediately issued grant already has a zero grant token; release does not
  clear it again.
- Free acquisition/final release still publish the changed owner and nest.

The shared queue/grant representation remains compatible with the existing
Layers entrypoints. Nonempty queues, foreign owners, pending grant tokens and
the retained high-bit nesting fallback go through the existing shared state
machine before any short-path mutation. Public ABI, clipping, FIFO handoff,
public semaphore values, blitter fences and scheduling boundaries remain.
The shared fallback receives the original sign-extended SHORT operation.

These short transactions do not wait or call guest callbacks. Therefore their
empty task queue cannot gain another task before Permit. This follows the
[Forbid contract](https://developer.amigaos3.net/autodocs/exec.library/Forbid.html);
interrupts remain enabled, but interrupt code cannot call waiting or Forbid-based
system routines ([Exec interrupt restrictions](https://wiki.amigaos.net/wiki/Exec_Interrupts)).
The [LockLayer contract](https://developer.amigaos3.net/autodocs/layers.library/LockLayer.html)
still requires exclusion and waiting when another task owns the Layer. The
optimization retains both; it does not introduce cooperative yielding.

A four-byte certified-root cache extends the standalone graphics allocation from
2,316 to 2,320 bytes. The original 2,316-byte lifecycle envelope remains supported
by the general initializer. A regression test exposed the initial minimum-size
change; it was corrected rather than changing the old expectation. The
standalone factory guarantees the extended size, avoiding a size check per draw.
No memory-type queries or caller-validation checks are added to the hot path.

Implementation: [DrawingBridge.cs](../../CopperStart/native/CopperStart.Graphics/Layers/DrawingBridge.cs),
[owner kernel](../../CopperStart/src/CopperStart.Layers/LayersTaskLockCore.GraphicsKernel.cs),
[lifecycle layout](../../CopperStart/native/CopperStart.Graphics/Abi/NativeGraphicsLibraryLifecycleLayout.cs),
and [image builder](../../CopperStart/native/CopperStart.Graphics/Library/NativeGraphicsLibraryImageBuilder.cs).

## Kickstart comparison

The retained `rectfill-flat-layer-profile-v1` protocol uses PAL 8375/OCS,
2 MiB Chip RAM, display DMA off, IPL 7, depth 2, stride 80, identical flat/Layer
coordinates, three warmups and 21 samples. Completed medians include final DMA
completion. `.49` was remeasured with its exact ROM and owner-kernel bytes.

| MC68000 completed guest cycles | Kickstart | `.49` | `.50` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,854 | 2,856 |
| RectFill 16x8, layered | 3,570 | 5,936 | 5,106 |
| RectFill 184x32, flat | 6,456 | 6,960 | 6,960 |
| RectFill 184x32, layered | 7,278 | 9,128 | 8,648 |

Small layered RectFill is **14.0% faster**. Its extra cost above the flat control
falls **3,082 to 2,250 cycles (27.0%)**. The flat small control changes by two
cycles of bus phase; its drawing algorithm is unchanged. Large layered latency
falls **5.3%**. Small layered remains **1.43x Kickstart**, large **1.19x**;
parity is not claimed. Original v40.63 SHA remains
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.

On 68EC020, small layered falls **6,126 to 5,280 cycles (13.8%)**, large
**9,266 to 8,786 (5.2%)**. Flat controls remain **2,878/7,016**. Original-020
timing is not claimed.

The matching MC68000 instruction traces show:

| Through-return bucket | `.49` | `.50` |
| --- | ---: | ---: |
| Owner kernel | 1,732 | 874 |
| Layer routing/clipping/frames | 1,616 | 1,644 |
| CPU word fill | 1,384 | 1,384 |
| Of word fill: actual inner row loops | 616 | 616 |
| Exec Forbid/Permit | 228 | 228 |

The owner-kernel cost falls **49.5%**. The wrapper pays 28 additional cycles for
selection of the certified entry. Trace returns total **5,850 versus 5,022**;
every executed instruction is accounted for. Completed medians and individual
traces have different bus phases. The map tool checks full emitted code against
the ROM and verifies the independently labeled diagnostic kernel mirror.

## Validation

The final frozen selection passes **214 graphics tests**, with zero failures.
Three skipped entries represent two optional crossover benchmarks and the
unavailable native-range fixture (two CPU cases). Existing historical extended
image identities remain excluded with unchanged expectations. Three separate
color-map cases run against the working composition, which passes **six
identity/color-map checks**.

The new certified-entry control compares valid free/nested lock and release
states against the checked kernel on both CPUs, including high nesting. It also
uses a real allocated waiter to check queued release, FIFO ownership/token
handoff, freeing/waking, public semaphore fields, balanced exclusion and the
fallback's full operation value. Existing callback re-admission, private schema/
lifetime, corruption, queue-arrival/publication and caller-register controls pass.
Public graphics register assertions retain their ordinary ABI requirements.

All **34 flat-call rows** match original. All **16 owned Layer rows** preserve
baseline state/pixels; MC68000 also matches original. All **168 text rows** and
**16 idle/pending pipeline rows** preserve pixels. The library initializer tests
retain the old minimum size and add the new extended size with complete-image
and boundary guards. The shared lock schema, geometry, CPU/DMA crossover and
pixel-writing loops are unchanged.

Both Release solutions build successfully. Producer retains existing warnings;
consumer has none. The app passes **11 native graphics/desktop checks** and **69
launcher/update checks**, with three optional workspace cases unavailable.
No emulator/CPU implementation, package pin or machine configuration changes.
These are guest-cycle diagnostics, separate from historical host-throughput
acceptance protocols.

## Desktop and bundle

The paired A1200 normal/scalar runs match exactly in CPU, full Chip RAM, pixels,
audio, cycles, fields, display registers and milestones. Cold-ready is
**12.079227888 guest seconds**, versus `.49`'s **12.099259775**: one PAL field
earlier. The `.49` cold-boot recovery is preserved. Fixed Tab input redraws
successfully. Optional disk-module symbols remain omitted in the same v4
protocol; no fine-grained DrawChanges duration is claimed. Pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Bundle fact | Value |
| --- | --- |
| Qualification | `diskboot-2026-11-01-a1200-desktop-single-bank.50` |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used / padding | 521,048 / 3,240 bytes |
| ROM SHA-256 | `CA1E4DD76EECE569B30A773A012DB40C87A79924936E58D1D6485D69B0BF0411` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Graphics totals **26,316 bytes**: a 928-byte initializer at `$FF9860` and 25,388
contiguous bytes at `$FF9C00`. Footer headroom is **192 bytes**; checksum is
`$FFFFFFFF`. Overall used space grows **268 bytes** against `.49`. Its raw
compiler/reset SHA remains
`35A190D6B7621E257CFD001E5DCF8DADBCDB15374532E79088FE77C6FA5AD321`.
All added code executes from ROM. The local app bundle and build hash pin select
this exact image. No ROMs/media, artifacts or credentials are committed or
published.

Exact source/emitter/diagnostic snapshots, hashes, baseline kernel, raw reset,
ROM/map/size proof, traces, original comparisons and logs are retained in producer
`artifacts/layers-owner-20261101`. Consumer paired captures, verification and app
build/test evidence are under `artifacts/layers-owner-20261101` and test outputs.
The initializer-regression log and earlier trials remain distinct from the final
ROM identified above.
