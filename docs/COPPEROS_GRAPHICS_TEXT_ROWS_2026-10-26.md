# Packed text rows and default-glyph reuse — 2026-10-26

The graphics-text-rows.23 development bundle reduces repeated text staging work
and default-glyph validation. It follows
[graphics-copy-words.22](COPPEROS_GRAPHICS_COPY_WORDS_2026-10-26.md).
The [producer report](../../CopperStart/Docs/NativeGraphicsTextRows20261026.md)
links code, original controls and measurements.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `1beb5820077670856816305a6acaad37fea082c9fddb690c9b8bef86fca1b62e` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The contract/build hash gate select this ROM. Reset remains exact .9–.22.
GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks need no
migration. Standalone/resident images are 25,536/29,443 bytes.

Packed text chooses group/tail variants once per call and advances its padding
pointer with each row. A redundant leading clear is omitted only for the packed
path. Character indexes use bounded WORD arithmetic. First use validates a default
glyph and switches to a loop reusing it, with no later cache-state checks. Unused
defaults are never validated, and glyph index zero remains valid. Clip/plane
admission, ownership and completion before stack-template release remain.

Completed median v4 MC68000 cycles include WaitBlit:

| Text | Before (.22) | After (.23) | Kickstart |
| --- | ---: | ---: | ---: |
| 8 characters | 10,798 | 9,894 | 7,912 |
| 64 characters | 34,200 | 31,874 | 30,480 |

The v4 font/string includes repeated default glyphs. A new, separate 168-case
matrix also measures strings containing only in-range glyphs: eight characters
improve 10,780→9,970 cycles (7.5%); 64 improve 34,040→33,104 (2.7%).
All pixels/font-source bytes/cursor positions match the baseline and 84 original
MC68000 controls. Across both CPUs, 160 matrix rows improve, two tie and six
one-row cases cost up to ten extra cycles. Text still exceeds Kickstart's cost.
Independent verification reproduces all 34 v4 native medians and all 168 matrix
rows exactly, including available original controls.

All **133 producer graphics tests pass**, zero skips. Expanded tests cover every
length 1..80, all packed tails, high unsigned font modulos, an unused non-eight-pixel
default and a default sharing glyph zero. Existing ownership/IRQ, raster, copy,
font and original controls remain, along with both earlier crossover protocols.
A fresh build passes three installed comparisons and 24 identity/text/matrix
cases. Ten pre-staging pixel tests pass. Source ownership verifies 203 imports.
CopperScreen Release builds with zero warnings/errors; the producer solution
retains three existing Shell/DOS API errors.
After staging, all 11 bundled native tests pass, zero skips, including desktop
boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution agree at **764 fields / 108,565,931 cycles**,
including registers, RAM, pixels and PCM. Cold readiness is 15.063987798 guest
seconds; Tab redraw remains 12 PAL fields / 0.240374327 seconds. The total is
31 cycles below .22 with unchanged field counts; no desktop speedup is claimed.
Pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics attribution remains unavailable.

Final local input: `../CopperStart/artifacts/graphics-text-rows/qualified-default.rom`.
Final evidence uses `qualified-text-*` directories; row-only and sentinel-cache
intermediates remain separately retained. The ignored staged bundle is the app
input. Remaining performance gaps and previous comparison exclusions stay open.
These are guest cycles, not physical timing or host acceptance.
