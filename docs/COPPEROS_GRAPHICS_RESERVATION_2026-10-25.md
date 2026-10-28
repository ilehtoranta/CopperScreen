# Blitter ownership reservation checkpoint — 2026-10-25

The graphics-reservation.13 development bundle removes interrupt masking from
uncontended OwnBlitter/DisownBlitter. Atomic reservation/release and the existing
serialized dispatcher protect pending work. Private tag, lifetime and owner
checks remain. Both slow admission points honor reservations and older waiters;
task grants remain separate from waiting for previous DMA. The
[producer record](../../CopperStart/Docs/NativeGraphicsReservation20261025.md)
contains the protocol, regressions, source links and remaining work.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `c38af77aaa241a86c2a7b6b04cab2f81fc741d9ea299bd667241e90aee7ab5ab` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The reset bank remains byte-identical to .9–.12. Standalone/resident graphics
are **23,602/27,509 bytes** (+76). HDF/GUI/Home, CPU/chipset execution and package
pins remain qualified inputs. The firmware contract and build hash gate select
this ROM; personal disks do not require an HDF migration.

## Result and verification

All 34 installed-library cases match selected original V40.63 state, pixels and
defined results. An isolated rebuild reproduces every native median exactly.
Selected **completed median guest cycles** (drawing includes WaitBlit):

| Call | Before (.12) | After (.13) | Kickstart |
| --- | ---: | ---: | ---: |
| OwnBlitter | 578 | 356 | 62 |
| DisownBlitter | 574 | 338 | 64 |
| RectFill 1x1 | 2262 | 1806 | 2266 |
| RectFill 8x8 | 4422 | 3964 | 2270 |
| BltBitMap 16x8 | 6358 | 5904 | 2416 |
| Text 64 characters | 38752 | 38290 | 30480 |

Protocol-v4 desktop profiler and normal ExecuteFrame control agree at
**764 fields / 108,565,938 cycles**, including CPU registers, RAM, pixels and
PCM. Cold readiness remains **15.063979 guest seconds**; Tab redraw remains
**12 PAL fields / 0.240384 seconds**. This checkpoint does not claim a desktop
frame-count reduction. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain explicitly unattributed in the desktop observer.

- **78 producer tests pass**, zero skips, including 34 new instruction-boundary,
  FIFO, cancellation, lifetime and DMA cases across both CPUs. Shared emitted
  bodies also run on Lightweight. These are software-preemption probes, separate
  from hardware IRQ timing; existing installed IRQ controls remain in place.
- **10 pre-staging pixel oracles pass**, followed by **11 bundled native
  tests**, zero skips, covering masks/modes, shifts, Simple/Smart clipping,
  ROM fonts, long strings, full desktop, Editor/input, reset and cold reopen.
- CopperScreen Release builds with zero warnings/errors. Source ownership
  checks 201 imports exactly once. Final fresh reproduction passes three
  installed tests and 41 image/protocol cases. Its compiler retains the existing
  nullable warning. The full producer solution retains the three existing
  Shell/DOS API errors (`LoadSegmentNative`, `UnloadSegmentNative`,
  `UnloadDosProcessSegment`).

Final local evidence is under `../CopperStart/artifacts/graphics-reservation`:
`qualified.rom`, `qualified-emission`, `final-calls`, `qualified-reproduction`
and paired `qualified-desktop-*` reports, plus producer/consumer TRXs and the
staged ignored bundle. Prototype failures remain separately identified.
Ownership, other fills/copies and text still have measured gaps. Contention
throughput, complex/unmeasured APIs and five flat-text overflow exclusions
remain outside the parity claim. These results are not physical hardware timing
or host-throughput acceptance.
