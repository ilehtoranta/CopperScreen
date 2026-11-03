# CopperOS A1200 text optimization — 2026-10-30

Qualification `diskboot-2026-10-30-a1200-desktop-single-bank.39` follows the
[fill optimization](COPPEROS_FILL_2026-10-30.md). It uses the same frozen reset
inputs and unchanged `exec-preemption.10` desktop HDF. Concurrent producer
Exec/DOS/Classic ROM migration changes are outside this image.

The standard file remains **524,288 bytes**, including padding. Used code/data,
vectors, native command bridge and footer total **519,930 bytes**, leaving
**4,358 padding bytes**. Native graphics shrinks 186 bytes to 25,332 bytes at
`$FF9C00`; there are 248 bytes before the fixed footer. Checksum is `$FFFFFFFF`.

| Artifact | SHA-256 |
| --- | --- |
| A1200 ROM | `D509215BD65E379F2041D34DD530B1DEF907B75027BE5C105045F981023959C2` |
| Unchanged HDF | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

AUTOINIT publishes the immutable release callback once. Uncontended ownership
retains its reservation, queue and lifetime checks. Text caches four glyph
offsets across scanlines, retains a direct one-row path, and uses smaller WORD
hardware setup and common selected-plane bounds. Glyph alignment and byte-index
calculation share a rotate/compare; the maximum aligned offset remains supported.

All 34 installed-call cases match original Kickstart v40.63 pixels/state:

| Call, median MC68000 guest cycles | Kickstart | `.38` | `.39` |
| --- | ---: | ---: | ---: |
| OwnBlitter, uncontended | 62 | 276 | 254 |
| Text, 8 characters | 7,912 | 9,712 | 9,174 |
| Text, 64 characters | 30,480 | 31,712 | 30,512 |
| SetRast 640x70, depth 2 | 23,904 | 24,892 | 24,866 |

The 168-row comparison covers both CPUs, counts 1..80 at selected boundaries,
heights 1/8/32 and default characters. It has no state/pixel mismatches or cycle
regressions against `.38`. The graphics selection passes 191 tests, with 7
optional cases skipped; the five installed Layers checks pass separately.
That gives 196 unique passing cases with two optional crossover cases unrun. The four
previously documented extended-image identity failures remain explicitly excluded.

Installed, non-nested MC68000 layer Text 8 improves 19,034 to 18,494 cycles
(original 10,136); Text 64 improves 42,174 to 40,946 (original 32,258).
Nested cases also retain state and improve; substantial Layers overhead remains.

Profile and uninstrumented desktop control agree on CPU, complete Chip RAM,
pixels, audio, cycles, frames and milestones. The framebuffer remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Guest measurement | `.38` | `.39` |
| --- | ---: | ---: |
| Cold desktop ready | 14.583213769 s | 14.583213769 s |
| DrawChanges, Tab at field 1,000 | 41.534074 ms | 41.252701 ms |
| Key-down to DrawChanges return | 86.455900 ms | 86.174527 ms |

The eight-field input floor remains. These are guest-cycle diagnostics, separate
from host throughput. The 64-character call still trails Kickstart by 32 cycles;
short text, ownership, bitmap copies and several rectangle cases need further
work. The cold-boot regression also remains open.

The bundled app passes all 11 native graphics/text/desktop cases, including
editor, reset and cold reopen. Launcher/migration checks pass 69 cases, with
3 optional workspace cases unavailable. Both Release solutions build: producer
has 4 existing NuGet warnings; the consumer build has none. The consumer used
an isolated output directory because unrelated runners briefly held its normal
output DLLs open.

Ignored producer evidence, including exact inputs/source, is in
`CopperStart/artifacts/graphics-owner-20261030/text-rotate`. Consumer captures
are in `artifacts/graphics-text-columns-20261030/rotate-{control,profile}`.
No ROM/media or generated artifacts are committed or published.
