# Bitmap-copy setup checkpoint — 2026-10-25

The graphics-copy.14 development bundle reduces BltBitMap argument setup,
plane-pointer addressing and bounded fallback counter costs. It follows
[reservation.13](COPPEROS_GRAPHICS_RESERVATION_2026-10-25.md). The
[producer record](../../CopperStart/Docs/NativeGraphicsCopy20261025.md) links
the implementation, ABI, tests and measurement protocol.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `1cd4a172519b6f2daa147a718a5a2e4cdf442c90a0e8cd209b3257f4f52e79cd` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. The reset bank stays
exact .9–.13; GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal
disks require no HDF migration. Standalone/resident graphics shrink by 90 bytes
to 23,512/27,419 bytes.

Selected completed median guest cycles, including public WaitBlit:

| Call | Before (.13) | After (.14) | Kickstart |
| --- | ---: | ---: | ---: |
| BltBitMap 16×8 | 5,904 | 5,604 | 2,416 |
| BltBitMap 184×64 | 16,892 | 16,668 | 14,580 |
| BltBitMap 600×64 | 43,996 | 43,742 | 41,674 |

All 34 measured calls retain selected original V40.63 state/pixel/result matches;
an independent build reproduces every native median. Other call medians are
unchanged. This is a 5.1% gain for the small copy; substantial gaps remain.

All 83 producer graphics tests pass with zero skips, including five new cases
for upper argument bits, saved registers and fallback counters beyond 255.
Original-ROM controls cover upper bits and 257-row overlap. Ten pre-staging
consumer pixel tests pass. The production Release build is clean. The full
producer solution retains its three existing Shell/DOS API errors; it is not
reported as passing.

After staging, all 11 bundled native tests pass with zero skips, covering pixel
oracles and desktop boot, Editor/input, reset and cold reopen. The independent
build also passes 12 image/setup cases with zero skips. Source ownership checks
201 retained imports exactly once.

Desktop profiler and normal execution control agree at 764 fields /
108,565,938 cycles, including registers, RAM, pixels and PCM. Cold readiness
remains 15.063979 guest seconds; Tab redraw remains 12 PAL fields / 0.240384
seconds. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
No desktop frame-count reduction is claimed. Graphics symbols remain
unattributed in this observer.

Local evidence: `../CopperStart/artifacts/graphics-copy/qualified.rom`,
`qualified-emission`, `final-calls`, `qualified-reproduction`, paired
`qualified-desktop-*` reports and producer/consumer TRXs. The ignored staged
bundle is the app input. Complex/unmeasured APIs, contention throughput and five
existing flat-text overflow exclusions remain outside the comparison. These
results are guest cycles, not host-throughput acceptance or physical timings.
