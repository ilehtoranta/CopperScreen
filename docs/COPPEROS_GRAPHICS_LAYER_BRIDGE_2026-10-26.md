# Layer bridge and provider optimization — 2026-10-26

The graphics-layer-bridge.25 ROM and layer-bridge.6 HDF follow
[graphics-layer-pin.24](COPPEROS_GRAPHICS_LAYER_PIN_2026-10-26.md).
The [producer report](../../CopperStart/Docs/NativeGraphicsLayerBridge20261026.md)
records implementation, controlled inputs and original Kickstart measurements.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `e6a335c408427fb6d062f944ba7eaa15ac11683d4e1cbee938f01d479ecbc69c` |
| DOS1 HDF | `de07a5d313e42c90013ed230c45cd907179e0598819ef900e86e5be564ca33b6` |
| L:CopperGUI | `91d8eeb662feddc410096e3642f37b5a76e307bfdfb1b66a389a3f739a97ff54` |
| L:CopperHome | `22ab094ac2f5f6e5a71afedc1c5b13cf378d04e0d5f92590048f7ff0c7ad7c5a` |
| C:Desktop | `e16bd06e979690af64effc6b5e99cab14d0c1add9b29edc9c370344264f51f8c` |

The ROM copies the layered rectangle's exact 100-byte RastPort in three MOVEM
groups instead of a 25-iteration loop. Its reset bank remains exact .9–.24.
Standalone/resident sizes are 25,694/29,601 bytes; extended images are unchanged.

Layers executes from **CopperGUI on the HDF**. Three overlaid existing methods
and two helpers reuse the root just validated at the private entry, before the
first wait. Resumes re-admit the root and live descriptor. Private ownership,
nesting, FIFO/grant checks, clipping and blitter completion remain.
Every other frozen body is preserved. The frozen GUI compiler uses FixedPoint
optimization; native GUI size falls 257,252→219,732 bytes. On-disk sizes remain
257,312/41,500/652 bytes, with Home's text offset and resident identity updated.

The new Layer comparison protocol v2 hashes baseline/candidate HDFs separately.
All 16 rows on MC68000/020 improve roughly 6.5–18%, with pixels, sentinels and
cursors matching eight original MC68000 controls. Representative completed cycles:

| Layered call, MC68000 | .24 | .25 | Kickstart |
| --- | ---: | ---: | ---: |
| RectFill 16×8 | 22,966 | 18,984 | 3,572 |
| Text 8 | 30,084 | 26,584 | 10,136 |
| Text 64 | 53,218 | 49,722 | 32,258 |

The desktop's profile and normal execution agree exactly at **702 fields /
99,755,625 cycles**. Cold readiness falls **14.90→13.90 guest seconds**;
Tab repaint falls **180→160 ms**, **nine→eight PAL fields**. The framebuffer
hash remains `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
An additional FixedPoint-only control's transient scanout difference is resolved
by identical final bitplanes and settled pixels; historical protocols stay intact.

All 144 producer graphics tests pass, zero skips. An independent build passes
three installed comparisons and 18 focused identity/Layer/copy tests and
reproduces all 34 v4 medians and 16 v2 rows. Expanded current-source Layer tests
retain the same nine pre-existing failed cases alongside 633 passes, including
two new resumed-grant checks; they are separate from native replay evidence.
The producer solution retains its three existing Shell/DOS API errors.
The consumer Release build passes with zero warnings/errors. All 11 staged
native desktop/input/reset/graphics cases and ten pre-stage cases pass, zero
skips. The host/migration run has 79 passes, zero failures and three unavailable
historical optional replays; the new redraw.5 upgrade test executes and passes.

The firmware contracts and build gates select the paired ROM/HDF. The checked
image recipe reproduces the HDF exactly. The existing atomic update also accepts
redraw.5 files, with an exact backup; unrelated changes survive, active disks are
deferred and customized system files are retained. App and migration test
counters are preserved in `../CopperStart/artifacts/graphics-layer-bridge/qualification.json`.
No media enters Git. Remaining Kickstart performance gaps remain active; these
guest-cycle diagnostics are separate from host-throughput acceptance.
