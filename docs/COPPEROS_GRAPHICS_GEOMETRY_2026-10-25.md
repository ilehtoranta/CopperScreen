# Shared graphics geometry checkpoint — 2026-10-25

The graphics-geometry.10 development bundle computes BltBitMap DMA geometry once
per call, defers CPU-only preparation until fallback, and lets the final
RectFill/SetRast blit run after return as original Kickstart does. The
[producer record](../../CopperStart/Docs/NativeGraphicsGeometry20261025.md)
contains the original-controlled implementation, measurements and limitations.
Remaining slower ownership and drawing calls stay active optimization work.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `41ccc36a055e002b35e4a12f38fe502aa11ffe5fdf51600cf2e7ca3078fd7b90` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The reset bank is byte-identical to graphics-fastcalls.9; no further Exec work
enters this bundle. Standalone/resident graphics grow by 138 bytes to
23,628/27,535 bytes. HDF, GUI/Home, CPU/chipset execution and package pins remain
the qualified inputs. The firmware contract and build hash gate select this
ROM. Existing personal disk data do not require an HDF migration.

## Result and verification

The 34-case real-kernel matrix matches selected state/pixels/defined results
against local original V40.63. A fresh isolated build reproduces every native
median. Selected guest-cycle improvements against .9:

| Call | Before return / completed | After return / completed | Kickstart return / completed |
| --- | ---: | ---: | ---: |
| BltBitMap 16x8 | 7846 / 7930 | 6654 / 6738 | 2332 / 2416 |
| RectFill 184x64 | 14386 / 14470 | 9842 / 13136 | 6974 / 11174 |
| RectFill 600x64 | 34640 / 34724 | 19962 / 33416 | 17148 / 31518 |
| SetRast 640x70 | 28048 / 28132 | 16478 / 26616 | 12604 / 23904 |

Completed includes WaitBlit; asynchronous return is not DMA completion. Tiny
changes on unchanged paths reflect bus phase and are not claimed as speedups.

Protocol-v4 desktop profiler and normal ExecuteFrame control agree at
**765 fields / 108,708,033 cycles**, including CPU registers, RAM, pixels and
PCM. Cold readiness improves **15.124078 → 15.084012 guest seconds**. Tab
redraw remains **12 PAL fields / 0.240382 seconds**; this checkpoint does not
claim a redraw-field reduction. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain explicitly unattributed in the desktop observer.

- **36 producer tests pass**, zero skips, including mixed DMA/CPU/constant/alias
  planes and original-controlled pending fill DMA on both CPU models.
- **10 pre-staging pixel oracles pass**, followed by **11 bundled native
  tests**, zero skips, including the full desktop, Editor/input, reset and
  writable cold reopen. Pixel tests cover masks, modes, shift directions,
  Simple/Smart clips, ROM fonts and long CPU fallback strings.
- CopperScreen production Release build passes with zero warnings/errors.
  Producer source ownership checks 200 integration/graphics imports once each.
- Fresh reproduction passes three installed tests and seven image tests; its
  compiler dependency retains the existing nullable warning. The complete
  producer solution still has the three existing Shell/DOS API errors
  (`LoadSegmentNative`, `UnloadSegmentNative`, `UnloadDosProcessSegment`).

Local evidence is under `../CopperStart/artifacts/graphics-geometry`, the
producer/consumer TRXs and the staged ignored bundle. Existing five flat-text
overflow exclusions remain unresolved. Complex/unmeasured APIs and layer/queue
throughput are outside this measurement. This is guest-cycle and compatibility
evidence, not host-throughput acceptance or full Kickstart parity.
