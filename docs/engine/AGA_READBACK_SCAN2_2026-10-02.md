# AGA palette readback and scan doubling

This follows [enhanced sprites](AGA_SPRITES_2026-10-01.md) on CopperScreen
`df5e653`. Copper68k remains public **1.5.1**; this chipset slice requires no
package release. The [machine-readable record](AGA_READBACK_SCAN2_2026-10-02.json)
pins supplied inputs, original guest probes, source, binaries and native captures.

## Implementation and authority

BPLCON2 RDRAM changes colour-table accesses to reads. BANK selects one of eight
blocks and LOCT selects the high or low RGB nibbles. High readback includes the
transparency bit; low readback has no transparency bit. RDRAM inhibits colour
writes from CPU and Copper. High writes with RDRAM clear retain the existing
compatibility rule of initializing both halves. AGA colour reads with RDRAM clear
return `$FFFF`; unused readback bits return zero. These are bounded register/bus
policies, not independent certification of electrical undriven-bus behavior.

FMODE BSCAN2 selects BPL1MOD for all planes when DIWSTRT's vertical parity matches
the live beam; otherwise every plane uses BPL2MOD. The existing accepted fetch
retains its FMODE, while modulo values and vertical parity are sampled at the
modulo phase. A final fetch group that crosses horizontal sync therefore uses
the new beam row rather than the row that started DDF. Existing refresh ownership
still applies to wrapped slots.

FMODE SSCAN2 makes SH10 a per-sprite scan-double flag. Flagged active data slots
run only when the vertical beam and the sprite start have matching parity.
Alternate rows retain Lisa's full previous payload; ordinary sprites and POS/CTL
reloads continue normally. Lisa removes SH10 from the horizontal comparator while
SSCAN2 is set. Start/stop parity is equal in the probes, as required by Commodore.
No secondary clock, raster frequency change, Legacy scheduler or Release logging
is introduced.

Primary sources are Commodore's [AA functional specification](https://shanson.com/spencer/Amiga-AA-Chipset.pdf),
printed pages 4–5, and [Lisa specification](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Aaa_lisa_specification.pdf),
section 2.1 plus its R1 SH10 comparator revision. Frozen WinUAE `custom.cpp` at
`387a1a29dbf80e6998fe0c0d084f2a3a86bb3b19` provides a supporting comparison for
read direction, data-slot suppression and live modulo parity. Its identity is
in the record; it is not a hardware trace. Read-only Legacy register/owner files
were inspected; no Legacy execution path was imported.

## Native proof and validation

Five original [guest probes](../../scripts/probes/AgaDisplay/README.md) boot native
A1200 Kickstart 3.0 (39.106), activate the chipset, open a depth-eight PAL screen,
draw through native graphics.library and write a DOS proof. The palette probe
reads 512 colour halves, including a distinct bank-7 transparency value, and
attempts inhibited writes. Its readbacks remain in Chip RAM; the native OS Copper
list restores the RGB24 display palette on later fields.

Four scan probes program BSCAN2 and SSCAN2 with 16-bit, 32-bit bus, 32-bit page
and 64-bit sprite fetches. A native bitmap with distinct rows demonstrates
bitplane row duplication. Doubled and ordinary sprites run together, with odd
and even starts, all eight channels, three resolutions, fine positions, banks
and an attached pair. An independent reader checks 139,000 pixels at every
checkpoint. A file proof alone does not establish the display/register result.

| Check | Result |
| --- | --- |
| Production Release build | Passed, zero warnings/errors |
| Engine diagnostics in separate outputs | 1076 passed, zero skipped |
| AGA test classes | 192 passed, including 40 new readback/scan cases |
| Ordinary host suite | 149 passed; six optional native cases unavailable |
| Supplied native A1200 desktop cold reopen | One test passed, two boots, zero skipped |
| Independent AGA reader controls | 16 passed, including six new positive/negative tests |
| Five original native probe modes | 1200 fields per normal/scalar run; checkpoints at 300/600/900/1200 |
| Native palette proof | 512 words and 180,224 exact RGB24 pixels at every checkpoint |
| Native scan proof | 139,000 exact pixels at every checkpoint in each fetch mode |
| Normal/scalar parity | All 31 capture files and final HDF identical for each pair |
| Initial RGB24/HAM6/HAM8/programmed DPF and wide-sprite controls | All captures and final HDF identical to earlier records |
| Native OCS/ECS HD controls | 2400 fields each; all 35/30 files and final disks identical to earlier records |
| Warmed active scan doubling plus CPU palette reads | Zero allocated bytes over four fields; doubled sprite remains visible |

The focused tests failed before implementation. A later row-wrap test exposed
selection by DDF owner rather than the live beam; its final version also retains
the refresh owner's stolen slot. Earlier native fixture experiments caught an
attached pair with mismatched vertical ranges and an incorrect assumption that
the OS Copper list would retain a temporary palette modification. Those inputs
remain separate from the final record. The allocation test measures execution
alone, with assertions outside the measured interval.

These are correctness diagnostics, not host-throughput acceptance measurements.
No ROM, OS, media, NDK bytes or local build artifacts are committed. Historical
performance protocols and fingerprints are unchanged.

## Remaining limits

Native third-party AGA games/demos and stock scan-doubled/VGA monitor drivers
still need supplied fixtures. These programmed PAL probes do not certify physical
monitor timing, analog/genlock output, colour bus residue, or undocumented active
scan-control/position rewrites. Selected accepted-transfer transitions have focused
ordering coverage; broad active-transition conformance remains open. CPU/Copper
page-mode sprite bus residue, manual wide bitplane input, nonstandard HAM/DPF,
fine DIW transitions and physical A1200 CPU/cache/bus timing remain unverified.
The existing raster output phase remains +1 lores pixel. See [current issues](ISSUES.md).
