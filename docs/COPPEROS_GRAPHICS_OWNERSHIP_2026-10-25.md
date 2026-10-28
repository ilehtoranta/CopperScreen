# Graphics ownership and text setup checkpoint — 2026-10-25

The graphics-ownership.11 development bundle removes repeated public pointer
certification from ownership, uses EXG around real Exec vectors, narrows fill
save frames and reuses text preflight/flat geometry. Private lifetime/ownership
checks, contended FIFO paths, blitter synchronization and layer locks remain.
The [producer record](../../CopperStart/Docs/NativeGraphicsOwnership20261025.md)
contains instruction profiles, source changes, original controls and limitations.
The remaining performance objective stays active.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `7481a9910931074a7f188bf5a1e526b87ebe753b0be9434ec146932e729e0be5` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The reset bank is byte-identical to .9/.10. Standalone/resident graphics shrink
by 128 bytes to **23,500/27,407**. HDF/GUI/Home, CPU/chipset execution and package
pins remain the qualified inputs. The firmware contract and build gate select
this ROM; personal disks do not require an HDF migration.

## Result and verification

All 34 installed-library cases match selected original V40.63 state, pixels and
defined returns. A fresh isolated build reproduces every native median exactly.
Selected **completed median guest cycles** (drawing includes WaitBlit):

| Call | Before (.10) | After (.11) | Kickstart |
| --- | ---: | ---: | ---: |
| OwnBlitter | 768 | 578 | 62 |
| DisownBlitter | 764 | 574 | 64 |
| Text 8 characters | 16466 | 14258 | 7912 |
| Text 64 characters | 42638 | 40434 | 30480 |
| RectFill 8x8 | 5136 | 4694 | 2270 |
| RectFill 1x1 | 4114 | 3670 | 2266 |
| BltBitMap 16x8 | 6738 | 6358 | 2416 |

Protocol-v4 desktop profiler and normal ExecuteFrame control agree at
**764 fields / 108,565,932 cycles**, including CPU registers, RAM, pixels and
PCM. Cold readiness improves **15.084012 → 15.063983 guest seconds**. Tab
redraw remains **12 PAL fields / 0.240379 seconds**. Selected pixels retain
SHA-256 `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain explicitly unattributed in the desktop observer.

- **40 producer tests pass**, zero skips, including seven image identities,
  original-controlled comparisons, private lifecycle/FIFO cases and new
  MC68000/68EC020 nesting/register/fill-fallback tests.
- **10 pre-staging pixel oracles pass**, followed by **11 bundled native
  tests**, zero skips, including full desktop, Editor/input, reset and writable
  cold reopen. Pixel coverage retains modes/masks, both shift directions,
  Simple/Smart clipping, ROM fonts and long CPU fallback strings.
- CopperScreen production Release builds with zero warnings/errors. Source
  ownership checks 200 integration/graphics imports exactly once.
- Fresh reproduction passes its three installed tests and seven image tests.
  Its compiler dependency retains the existing nullable warning. The complete
  producer solution retains three unrelated Shell/DOS API errors
  (`LoadSegmentNative`, `UnloadSegmentNative`, `UnloadDosProcessSegment`).

Local evidence is under `../CopperStart/artifacts/graphics-ownership`, the
producer/consumer TRXs and the staged ignored bundle. Ownership, text and small
drawing calls remain slower; the five flat-text overflow exclusions remain
unresolved. Layer/queue throughput and unmeasured API families are outside this
measurement. These guest-cycle results do not establish physical hardware timing,
host-throughput acceptance or full API parity.
