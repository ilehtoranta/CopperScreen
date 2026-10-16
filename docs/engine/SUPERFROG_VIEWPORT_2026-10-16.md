# Superfrog: repeated bottom viewport

2026-10-16, **LWA-VIDEO-010**, compatibility case 5. The repeated strip in the
website's Superfrog picture is an emulator defect. It reproduces with both
Kickstart 1.3 and CopperStart. Wrapping the Copper's horizontal comparison
counter removes it without changing the display window, cropping the picture,
patching the game or changing firmware.

The [machine-readable evidence](SUPERFROG_VIEWPORT_2026-10-16.json) records exact
binaries, ROM/media/input identities, test results, reviewed captures and the
website replacement. Local captures and frozen runners are under
`artifacts/superfrog-viewport-2026-10-16/`.

## Cause and correction

The existing Copper input sequencer maps physical CCKs to a nominal horizontal
counter with a three-CCK offset and a line-length wrap. `ComparisonSatisfied`
used the offset without the wrap. At physical PAL H225 it compared HP `$E4`
instead of `$01`. The [Commodore HRM, chapter 2](https://bastya.net/AmigaDevDocs/hard_2.html)
defines the ordinary horizontal counter's maximum as `$E2` and describes WAIT's
beam-counter comparison.

Superfrog uses five planes and DDF `$28`–`$D8`. DMA contention delays a late
WAIT's comparison into that wrap interval. The impossible HP value incorrectly
releases the WAIT, allowing the final display-list block to reload the playfield
pointers at line 256. A bounded scalar diagnostic starts at exactly the native
field-7,320 capture's PC/cycle and observes BPL1 return to `$2A72E` at that boundary.
No guest memory is modified by the diagnostic.

The fix wraps HP at the current line length before the existing WAIT/SKIP mask
and comparison. Bus acceptance, pending-output ordering, vertical comparison
and display geometry retain their existing implementation. The new four-case
regression exercises late five/six-plane waits and earlier zero/four-plane
controls. Both late cases fail before the correction; all four pass afterward.
This does not claim new physical proof for every coincident Copper/DMA edge.

At field 7,320, each paired before/after ROM replay has identical pixels above
raster line 256. Changes are confined to `x=242..907, y=256..299`: underground
terrain continues instead of repeating the upper scene. In the website's
unchanged row-doubled viewport, the faulty strip began at image row 460.

## Native validation

The supplied SPS 35 archive's `SuperFrog_DiskOne.ipf` runs on the PAL OCS 68000
A500, 512 KiB Chip plus 512 KiB slow RAM, one read-only drive. CopperStart is the
qualified **dev.3** ROM selected at the start of this investigation, SHA-256
`1950255822e4d4571dc9d96d510c792d57d3c801fb11568f20155527fc3de444`.
Later concurrent app/firmware qualification edits are not relabeled as this ROM's
evidence. Native Kickstart 1.3 is the independent boot-path control.

- Both ROMs reproduce the old strip in 8,000-field, no-input replays. Both show
  the corrected viewport with the candidate engine.
- CopperStart's corrected 8,000-field batch/scalar replays match all **675**
  captured image/state/RAM/PCM files.
- The new [gameplay script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-superfrog-gameplay.json)
  opens Start Game at field 3,300 and selects it at 3,500. Both ROMs load
  **Magic Woods 1-1**. The camera stays at the start before the right input;
  movement then scrolls the scene and collects items, taking the score to 350.
  Up input changes the vertical camera position. These are reviewed input
  responses, not an attract-mode gameplay claim.
- CopperStart's 12,000-field controlled batch/scalar runs match all **505**
  capture files. The sustained movement sequence eventually returns to the
  title; a completed level or game is not claimed. Disk two and the story disk
  are not used. An earlier 16,000-field menu-timing probe is retained separately
  and is not counted as controlled-play qualification.

The production Release solution builds with **zero warnings/errors**. All **38
Copper tests** pass. The separate engine diagnostic suite reports **1,245 passed,
zero failed, seven skipped**. The seven optional native cases lack configured
inputs; their skips are unavailable coverage. ROM/media replays above are
separate evidence. No formal host-throughput acceptance measurement, WinUAE run
or audio listening/fidelity assessment was performed.

## Website capture

The replacement attract image comes from the corrected CopperStart field-7,320
BMP, SHA-256
`A4063A8C5744392A96749AB306358EF0526AB0FFD2D89B12FB659689AC8B2A9A`.
The full application PAL viewport transform remains `x=196, y=26, 712×285`,
with rows doubled to 712×570 and a proportional 356×285 thumbnail. There is no
additional crop, retouching or recoloring. The caption still says attract mode.
The reproduction script and [website provenance](../WEBSITE.md) identify the new
source; the historical September capture retains its original attribution.
Gallery generation validates all 18 entries. The replacement is included on
this development branch; GitHub Pages deployment follows the main-branch workflow.
