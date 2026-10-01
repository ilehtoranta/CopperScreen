# AGA HAM and dual-playfield composition

This follows the [initial PAL A1200 milestone](AGA_INITIAL_2026-10-01.md), on
CopperScreen `7187e55`. Copper68k remains the public **1.5.1** package. No CPU
package release, machine layout or bus policy changes are required. The retained
[machine-readable record](AGA_HAM_DUAL_2026-10-01.json) identifies the supplied
inputs, original probe executables, final engine/runner and every native capture.

## Implementation

Lisa now composes HAM6/HAM8 and two four-plane playfields in lores, hires and
SuperHires, through the existing eight shifters and accepted DMA pipeline.

- HAM6 selects RGB24 base colours directly and expands a modified component's
  four-bit value. HAM8 uses BP2/BP1 as control, selects one of 64 base colours
  and replaces only the high six bits of a component. A visible line starts
  from COLOR00; hidden pixels do not seed its hold. Sprite output overlays the
  result without becoming the held colour.
- Four-plane PF1/PF2 use odd/even bitplanes. Raw zero decides transparency
  before palette-address XOR. PF2OF adds the selected offset to the winning
  PF2 colour. PF2PRI changes the winner, while both opaque fields contribute
  their sprite masking threshold. The existing 35 ns scroll fields retain
  their independent parity reloads, including whole-pixel delays over 16 pixels.
- BPLCON2/PF2OF update a compact 256-entry lookup at the existing control
  deadline. HAM XOR changes control and direct selection while modification
  data remains raw. EHB tests its colour address after XOR. These paths do not
  allocate during steady Release execution.
- Hires/SuperHires playfield samples share a legacy-width sprite sample at its
  selected rate. Each subpixel has its own mask, using the actual sprite group
  even for attached sprites. Every covered subpixel feeds the existing six-plane
  collision logic; planes 7/8 collision control remains a separate missing feature.

Primary sources are Commodore's
[AA functional specification](https://shanson.com/spencer/Amiga-AA-Chipset.pdf),
printed page 4 (HAM control/component format), and
[Lisa specification](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Aaa_lisa_specification.pdf),
section 2.1 (plane grouping, PF2OF, priority and palette address fields).
Commodore's [modeid.h](https://www.theflatnet.de/pub/cbm/amiga/amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_3._guide/node0661.html)
defines the stock PAL HAM ModeID used by the guest.

Legacy was audited at CopperMod `8fb826e`: `Display.Bitplanes.cs` supplies the
same odd/even grouping and PF2OF interpretation; `Display.Sprites.cs` contains
the older RGB12 HAM6 decoder. The new HAM8/RGB24 path follows the specification,
without importing its scheduler or presentation state. XOR and sprite details
were cross-checked against WinUAE `drawing.cpp`; the exact supporting source
identity is retained in the JSON record. Emulator agreement is supporting
research, not a physical A1200 trace. Undocumented transition phases remain open.

## Native validation

The [original guest probes](../../scripts/probes/AgaDisplay/README.md) boot the
supplied A1200 Kickstart 3.0 (39.106) through CopperHDF/DOS. They call the native
V39 chipset activation API and validate returned dimensions/depth before writing
their proof. The RGB24 default still generates the original Hunk and pristine
FFS image byte for byte.

HAM6/HAM8 open stock PAL `$21800` screens at 320×256 with depth six/eight and
draw 256 encoded tiles through graphics.library. Kickstart rotates the HAM8
bitmap pointers: the probe translates raw Lisa codes to logical drawing pens.
The independent reader checks every tile interior against specified component
semantics, including held low bits, rather than accepting a screenshot hash.

The separately named `programmed-dual` probe opens/draws a stock eight-plane
screen, writes its DOS proof, then takes chipset ownership. Its original Copper
list restarts the bitmap pointers, sets DPF, PF2OF=32 and XOR=128, and changes
PF2PRI at line 180. Two grids each contain all 256 PF1/PF2 colour combinations.
This is a native guest hardware probe, not a stock graphics.library DPF screen
or a third-party compatibility replay. Its proof establishes preceding native
allocation/drawing/DOS I/O; the pixel check independently establishes DPF output.

| Check | Result |
| --- | --- |
| Production Release build | Passed, zero warnings/errors |
| Engine diagnostics in separate outputs | 954 passed, zero skipped |
| AGA diagnostics within that suite | 70 passed, including 42 new composition cases |
| Ordinary host suite | 149 passed; six optional native cases unavailable |
| Supplied native A1200 desktop persistence/cold reopen | One test passed, two boots, zero skipped |
| CopperDisk suite | 74 passed, zero skipped |
| Native regression tooling controls | 21 passed; no ROM/media replay |
| Synthetic AGA pixel-checker controls | Five tests passed, including corruption, lost HAM8 low bits and wrong DPF priority |
| Native HAM6 and HAM8 | 1200 fields per normal/scalar run; 180,224 exact interior pixels per run |
| Native programmed DPF | 1200 fields per normal/scalar run; 98,304 exact interior pixels per run |
| Normal/scalar parity | All 31 capture files and final HDF identical for each pair |
| Initial RGB24 AGA control | 1200 fields; all 31 capture files and final HDF identical to the initial record |
| Native OCS/ECS HD controls | 2400 fields each; all 35/30 capture files and final HDF identical to the initial AGA record |
| Steady HAM6/HAM8/DPF allocation | Zero bytes across four warmed fields per mode |

The new tests discriminate component preservation, direct palette selection,
left/line hold reset, sprite overlay, all 256 colour combinations in both priority
orders, all eight PF2 offsets, independent wide scrolling, XOR, sprite masks,
SuperHires collision subpixels and disabled-plane native setup. The initial
implementation failed 28 of the first 31 tests; allocation tests already passed.
Two later collision tests failed before the subpixel correction. Those failures
remain under the ignored local artifacts, separate from final evidence.

Native replay found that Kickstart briefly sets HAM with BPU=0 while constructing
its display. That produces no HAM pixels and is now accepted. Nonzero unsupported
plane counts continue to report a missing feature. No guest patch or title-specific
exception was added. FFS proof files are verified independently of CopperDisk.

These are correctness diagnostics. No host-throughput acceptance measurement was
run, and historical performance protocols/fingerprints remain unchanged.

## Remaining limits

Native game/demo compatibility, stock dual-playfield OS screens, and native
hires/SuperHires HAM/DPF coverage remain open; those resolutions currently have
focused diagnostic coverage. Enhanced sprite widths/resolutions, unequal sprite
palette banks, CLXCON2 planes 7/8, palette readback and scan doubling remain
unavailable. Combined HAM/DPF, nonstandard HAM plane counts and DPF priorities 5–7
are explicitly unverified. Fine positioning/window transitions, manual wide
BPLDAT writes, genlock and physical bus/hold/priority phases need further evidence.
The machine remains the initial PAL A1200, without a new ROM, ATA or expansion
profile. See [the current issue register](ISSUES.md).
