# Single-row word copies — 2026-10-26

The graphics-copy-words.22 development bundle adds measured CPU copies for
source-copy operations with one row and at most two destination words. Other
cases retain DMA or existing fallback. It follows
[graphics-copy-admission.21](COPPEROS_GRAPHICS_COPY_ADMISSION_2026-10-26.md).
The [producer report](../../CopperStart/Docs/NativeGraphicsCopyWords20261026.md)
links code, original controls and full measurements.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `5584795676c93abbfe7f9045765005a583921262ca0e7d30cc054ee6a383a8d4` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The firmware contract and build hash gate select this ROM. Reset stays exact
.9–.21. GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks
need no migration. Standalone/resident images are 25,076/28,983 bytes.

The short path loads each plane's required source window before writing,
supporting both shifts, constants, masks and horizontal overlap. WaitBlit precedes
CPU raster access. A late odd pointer resumes at that plane without replaying
earlier copies. No memory-type query is added. The rejected eight-row CPU
prototype was slower than DMA and was not bundled.

A new, separately recorded 840-row crossover tests both CPUs, three alignments,
seven widths, five heights and four depths. All pixels/source guards/plane counts
match the frozen .21 baseline; 84 one-row MC68000 comparisons also match original
V40.63. All **128 CPU selections** improve completed cycles by **26.9–63.4%**.
The other 712 cases retain DMA, with differences from −26 to +40 cycles.

Representative completed median MC68000 cycles, depth two, including WaitBlit:

| Copy | Before (.21) | After (.22) | Kickstart |
| --- | ---: | ---: | ---: |
| 8×1, x 3 → 7 | 4,762 | 3,102 | 2,266 |
| 16×1, x 3 → 7 | 4,786 | 3,202 | 2,270 |
| 24×1, x 15 → 3 | 5,750 | 3,318 | 2,198 |

These calls still exceed Kickstart's cost. The existing 34-call v4 comparison
retains selected state/pixel/result matches. Multi-row copy costs are effectively
unchanged; the report retains their exact cycle differences.
An independent build reproduces all 34 v4 medians and all 840 crossover rows,
including available original controls.

All **126 producer graphics tests pass**, zero skips. Seven new word-copy cases
include original pixels, aliases, constants, late fallback and pending DMA into
the source. Expanded late fallback coverage passes separately on both CPUs.
Independent verification passes three installed comparisons and fifteen
identity/word/crossover cases. Ten pre-staging pixel tests pass.
Source ownership verifies 203 retained imports. CopperScreen Release builds
with zero warnings/errors. The producer solution retains three existing
Shell/DOS API errors.
After staging, all 11 bundled native tests pass, zero skips, including desktop
boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution agree at **764 fields / 108,565,962 cycles**,
including registers, RAM, pixels and PCM. Cold readiness is 15.063979199 guest
seconds; Tab redraw remains 12 PAL fields / 0.240387296 seconds.
Pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics attribution remains unavailable.

Evidence: `../CopperStart/artifacts/graphics-copy-words`, including the immutable
qualified ROM, rejected prototype, full comparisons, independent reproduction,
compositions, crossover, desktop reports and TRXs. The ignored staged bundle is
the app input. Remaining performance gaps and previous comparison exclusions stay
active. These are guest cycles, not host acceptance or physical timing.
