# Short rectangle word-fill checkpoint — 2026-10-25

The graphics-word-fill.18 development bundle selects short CPU word loops when
they beat DMA setup, retaining the blitter for larger shapes. It follows
[copy-dma.17](COPPEROS_GRAPHICS_COPY_DMA_2026-10-25.md). The
[producer record](../../CopperStart/Docs/NativeGraphicsWordFill20261025.md) links
the code, exact cutoffs, protocols, controls and remaining gaps.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `8fb86a5daedc94bdf5477cb5f40ad20362e33dd87c29f60493c4be116e59da63` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The contract and build hash gate select this ROM. Reset stays exact .9–.17;
GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks need no
HDF migration. Standalone/resident images grow by 856 bytes to 24,238/28,145
bytes.

The CPU loops retain inclusive edge masks, unsigned stride, effective pens,
write masks and draw modes. The caller still owns and waits for the blitter.
All selected planes pass alignment preflight before any write, preserving full
byte fallback on a later odd plane. No memory-type query or allocation is added.

The new crossover-v2 diagnostic tests 2,016 width/height/depth/alignment/mode
cases on both CPU models. All 808 cases selecting the CPU path improve, with a
minimum gain of 114 completed guest cycles. Pixel hashes match the prior image.
The protocol has display DMA disabled; active-display contention is separate
unmeasured coverage. Larger requests retain DMA.

Completed median guest cycles in the original-comparison v4 protocol:

| RectFill | Before (.17) | After (.18) | Kickstart |
| --- | ---: | ---: | ---: |
| 8×8 | 3,578 | 2,882 | 2,270 |
| 16×4 | 3,594 | 2,812 | 2,270 |
| 16×8 | 3,596 | 3,180 | 2,368 |
| 64×2 | 3,596 | 2,870 | 2,274 |

These are 11.6–21.8% gains. All 34 comparisons retain selected original V40.63
state/pixel/result matches, and a separate build reproduces every native median.
The dispatch tradeoff is recorded explicitly: the 1×1 sample costs six more
cycles (still faster than Kickstart), and 184×64 / 600×64 grow by 2/24 cycles.
Declined shapes can pay a small dispatch cost. Substantial gaps remain.

All **106 producer graphics tests pass**, zero skips. Five new correctness cases
cover modes/masks/edges, high unsigned strides, complete odd-plane fallback,
earlier DMA and original-ROM controls in allocated workspace. The crossover
matrix is a separate sixth test. Ten pre-staging consumer pixel tests pass.
Source ownership checks 202 retained imports exactly once. CopperScreen Release
builds cleanly; the full producer solution retains its three existing Shell/DOS
API errors.
The independent build passes three installed comparisons and 13 image/word/
crossover tests, zero skips, reproducing all 2,016 crossover rows exactly.
After staging, all 11 bundled native tests pass, zero skips, including desktop
boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution control agree at 764 fields /
108,565,937 cycles, including registers, RAM, pixels and PCM. Cold readiness is
15.063979622 guest seconds; Tab redraw is 12 PAL fields / 0.240383349 seconds.
The total is five cycles above .17 with unchanged frame counts; no desktop
frame-count speedup is claimed. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain unattributed in this observer.

Local evidence is under `../CopperStart/artifacts/graphics-word-fill`, including
`qualified.rom`, `qualified-emission`, `qualified-full-calls`,
`qualified-crossover`, `qualified-reproduction`, paired desktop reports and
producer/consumer TRXs. The ignored staged bundle is the app input. Complex
APIs, contention throughput and five flat-text overflow exclusions remain
outside this comparison. These are guest cycles, not host acceptance or physical
timings.
