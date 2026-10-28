# Text staging checkpoint — 2026-10-25

The graphics-text-setup.16 development bundle reduces fixed-cell Text setup and
template staging. It follows [fill.15](COPPEROS_GRAPHICS_FILL_2026-10-25.md).
The [producer record](../../CopperStart/Docs/NativeGraphicsTextSetup20261025.md)
links the code, instruction profile, controls and measurement protocol.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `21ddca8044d272bef1401b954338f663ee5857bf0bab856b0f96eb7d4e9cc6af` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. Reset stays exact
.9–.15; GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks
require no HDF migration. Standalone/resident graphics shrink by 24 bytes to
23,428/27,335 bytes.

Font/template row pointers replace per-row multiplications. The private helper
saves only its live fallback input instead of a second full register frame.
Packed counters are computed once, cached glyph indexes load in groups, and
unsigned byte range selection retains default glyphs. Text still stages ROM
fonts into Chip RAM and renders through the blitter. Complete preflight and DMA
completion before releasing the stack template remain covered.

Completed median guest cycles, including public WaitBlit:

| Text, fixed 8-pixel cells | Before (.15) | After (.16) | Kickstart |
| --- | ---: | ---: | ---: |
| 8 characters | 13,656 | 11,774 | 7,912 |
| 64 characters | 38,290 | 35,182 | 30,480 |

These gains are 13.8% and 8.1%. All 34 benchmark comparisons retain selected
original V40.63 state/pixel/result matches, and an independent build reproduces
every native median. Substantial performance gaps remain.

All **97 producer graphics tests pass**, zero skips. Seven new cases cover
row strides/heights, packed tails, all byte values across five font ranges,
late fallback and original-ROM controls in allocated workspace. Large unsigned
row strides use the native pixel oracle rather than an original-ROM parity
claim. The independent build passes three installed comparison facts and 14
image/staging cases, zero skips. Ten pre-staging consumer pixel tests pass.
Source ownership checks 201 retained imports exactly once. CopperScreen Release
builds with zero warnings/errors; the full producer solution retains its three
existing Shell/DOS API errors.
After staging, all 11 bundled native tests pass with zero skips, including
desktop boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution control agree at 764 fields /
108,565,948 cycles, including registers, RAM, pixels and PCM. Cold readiness is
15.063979763 guest seconds; Tab redraw is 12 PAL fields / 0.240384759 seconds.
The total is 36 cycles below .15 with unchanged frame counts; no desktop
frame-count speedup is claimed. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain unattributed in this observer.

Local evidence: `../CopperStart/artifacts/graphics-text-setup/qualified.rom`,
`qualified-emission`, `qualified-full-calls`, `qualified-reproduction`, paired
`qualified-desktop-*` reports and producer/consumer TRXs. The ignored staged
bundle is the app input. Complex/unmeasured APIs, contention throughput and five
existing flat-text overflow exclusions remain outside the comparison. These
results are guest cycles, not host acceptance or physical timings.
