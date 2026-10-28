# Bitmap-copy DMA setup checkpoint — 2026-10-25

The graphics-copy-dma.17 development bundle reduces repeated BltBitMap DMA
configuration, hardware field widths and register saves. It follows
[text-setup.16](COPPEROS_GRAPHICS_TEXT_SETUP_2026-10-25.md). The
[producer record](../../CopperStart/Docs/NativeGraphicsCopyDma20261025.md) links
the implementation, original trace, ABI, controls and measurement protocol.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `65b5906b4d497dbe045495833cc70fdd856a40d11c12702dc5ed45066b599376` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The contract and build hash gate select this ROM. Reset stays exact .9–.16;
GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks need no
HDF migration. Standalone/resident graphics shrink by 46 bytes to 23,382/27,289
bytes.

Common blitter configuration is reused until the source mode changes between
normal, zero and one planes. B's data/shifter input, pointers and size are still
refreshed per plane. Storage/alias/hardware admission, CPU fallback and the
asynchronous final DMA remain covered. The composer explicitly recognizes the
tailored entry's register preservation, avoiding a duplicate save wrapper found
in the first candidate's profile.

Completed median guest cycles, including public WaitBlit:

| BltBitMap | Before (.16) | After (.17) | Kickstart |
| --- | ---: | ---: | ---: |
| 16×8 | 5,602 | 5,132 | 2,416 |
| 184×64 | 16,666 | 16,246 | 14,580 |
| 600×64 | 43,742 | 43,324 | 41,674 |

The small-copy gain is 8.4%. All 34 comparisons retain selected original V40.63
state/pixel/result matches, and the independent build reproduces every native
median. Substantial performance gaps remain.

All **100 producer graphics tests pass**, zero skips. Three new matrix cases
cover eight-plane mode transitions, all minterms, masks, both shifts, fallback,
source preservation and original-ROM controls in allocated workspace. The
independent build passes three installed comparison facts and 15 image/copy
cases, zero skips. Ten pre-staging pixel tests pass. Source ownership checks
201 retained imports exactly once. CopperScreen Release builds cleanly; the
full producer solution retains its three existing Shell/DOS API errors.
After staging, all 11 bundled native tests pass with zero skips, including
desktop boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution control agree at 764 fields /
108,565,932 cycles, including registers, RAM, pixels and PCM. Cold readiness is
15.063979622 guest seconds; Tab redraw is 12 PAL fields / 0.240382645 seconds.
The total is 16 cycles below .16 with unchanged frame counts; no desktop
frame-count speedup is claimed. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain unattributed in this observer.

Local evidence: `../CopperStart/artifacts/graphics-copy-dma/qualified.rom`,
`qualified-emission`, `qualified-full-calls`, `qualified-reproduction`, paired
`qualified-desktop-*` reports and producer/consumer TRXs. The ignored staged
bundle is the app input. Complex/unmeasured APIs, contention throughput and five
existing flat-text overflow exclusions remain outside the comparison. These
are guest cycles, not host acceptance or physical timings.
