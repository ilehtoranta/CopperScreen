# AGA enhanced sprites and eight-plane collisions

This follows [HAM and dual-playfield composition](AGA_HAM_DUAL_2026-10-01.md),
on CopperScreen `71fdbb1`. The shared CPU package remains public Copper68k
**1.5.1**. The [machine-readable record](AGA_SPRITES_2026-10-01.json) pins supplied
inputs, original probe executables, source, engine/runner and final native captures.

## Implementation and evidence

Alice sprite DMA now captures FMODE with its accepted address, samples memory
at the existing output phase and advances by the selected aligned stride.
The control slots use the first fetched word and the same width-dependent
stride. Data slots deliver 16/32/32/64 bits for the four fetch modes through
the existing Lisa input deadline. Accepted transfers survive later pointer,
FMODE and DMA-enable writes. The widened read shares the existing bitplane
alignment/replication implementation.

The AGA-only compositor samples every 35 ns output position independently of
the playfield. Comparator-latched data, width and sample step give 140/70/35 ns
sprites and both fine-position bits. Even/odd channels select separate palette
banks; attached pairs always select the odd bank, including transparent odd
data or a later odd comparator. Playfield XOR does not alter sprite addresses.
Raw playfield codes feed a 256-entry collision lookup before display priority;
CLXCON2 enables/matches planes 7/8. CLXCON resets that extension at the same Lisa
input phase. Existing CLXDAT peeks, read-clear and odd-sprite enables are retained.
Border sprites respect ECSENA and border blanking. OCS/ECS keep their compositor.

Primary sources are Commodore's [Lisa specification](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Aaa_lisa_specification.pdf),
section 2.1 and its attached-bank revision note, and the
[AA functional specification](https://shanson.com/spencer/Amiga-AA-Chipset.pdf),
printed pages 4–5, including the CLXCON compatibility reset. The read-only Legacy
audit at CopperMod `8fb826e` found its even/odd bank selection in
`Display.Sprites.cs`. Frozen WinUAE `custom.cpp` and `drawing.cpp` support the
alignment, control stride, ATT and collision audit; their identities are in
the record. They are implementation comparisons, not physical hardware traces.

## Native probes and verification

Four original [guest probes](../../scripts/probes/AgaDisplay/README.md) cover
16-bit, 32-bit bus, 32-bit page and 64-bit fetches. Native A1200 Kickstart 3.0
opens an eight-plane screen, initializes RGB24 colours and writes a DOS proof.
The guest then allocates aligned Chip RAM through Exec, copies sprite streams
and installs its own Copper list. All eight channels appear across six bands;
resolution changes occur between inactive bands. Separate banks 2/11 and
playfield XOR 128 discriminate palette selection. The last attached pair has
different fine positions. The bitmap holds raw planes 7/8 high.

The independent checker verifies complete sprite intervals, transparency, gaps,
widths and positions at 53,376 pixels per checkpoint. Native CLXDAT reads are
stored in Chip RAM after a unique marker. Six words distinguish matching planes
7/8 from individual mismatches: `0067 0199 0000 0000 0060 0180`, excluding the
unused high bit. DOS proof establishes preceding native screen/disk work;
pixel and hardware-word checks establish programmed sprite/collision behavior.

| Check | Result |
| --- | --- |
| Production Release build | Passed, zero warnings/errors |
| Engine diagnostics in separate outputs | 1037 passed, zero skipped |
| AGA cases within that suite | 154 passed, including 85 new sprite/collision cases |
| Ordinary host suite | 149 passed; six optional native cases unavailable |
| Supplied native A1200 desktop cold reopen | One test passed, two boots, zero skipped |
| CopperDisk suite | 74 passed, zero skipped |
| Native regression tooling controls | 21 passed; no ROM/media replay |
| Independent AGA checker controls | Ten tests passed, including five new sprite/proof controls |
| Four native sprite fetch modes | 1200 fields per normal/scalar run, four checked checkpoints each |
| Normal/scalar parity | All 31 captured files and final HDF identical for each pair |
| Initial RGB24, HAM6, HAM8 and programmed DPF controls | 1200 fields each, all captures and final disks identical to the preceding record |
| Native OCS/ECS HD controls | 2400 fields each, all 35/30 files and final disks identical to the preceding record |
| Warmed wide-sprite execution | Zero bytes over four fields, with sprite DMA restarted each field |

The native experiment caught an attached-bank error before the odd comparator;
focused transparent-odd/delayed-comparator checks retain that distinction. A
final border audit added five checks that distinguish ECSENA and border blanking.
An earlier allocation check accidentally measured its assertion overhead; the
final check measures execution alone and confirms a sprite remains visible.
Earlier experiments are retained separately from the final record.

The first guest used `$30F9` (MOVE.W absolute-long to postincrement memory),
whose A1200 timing is unsupported in public Copper68k 1.5.1. The original probe
now uses equivalent supported register-mediated moves. No CPU package change
or timing certification is claimed; the failed experiment remains preserved.

These are correctness diagnostics, not host-throughput acceptance measurements.
Historical protocols and fingerprints remain unchanged. No ROM or media is committed.

## Remaining limits

Third-party AGA games/demos and stock OS extended-sprite APIs still need native
coverage. Sprite/playfield resolution independence has focused coverage across
all playfield resolutions; the native sprite probes use lores playfields.
Active sprite resolution/fetch changes, mixed-width A/B inputs, horizontal
comparator rewrites and physical sub-CCK phases remain unverified. The output
phase retains the engine's +1 lores raster convention. CPU/Copper SPR32 word
replication has a focused check; page-mode bus residue remains explicitly
unsupported. Palette readback, scan doubling, fine DIW transitions, manual wide
bitplane input, genlock, nonstandard HAM/DPF combinations and physical A1200
bus/cache timing remain open. See [the current issue register](ISSUES.md).
