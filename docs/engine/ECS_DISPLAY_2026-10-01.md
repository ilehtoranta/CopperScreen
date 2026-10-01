# ECS display on the Lightweight engine

Implemented 2026-10-01 on top of `68c2bac` (explicit PAL Agnus/Chip RAM).
The desktop and runner now select 8362 OCS or 8373 ECS Denise independently
of the supported Agnus revision. Copper68k remains pinned to public `1.5.0`;
this change does not require package publication. Source and validation
identities are retained in [the machine-readable record](ECS_DISPLAY_2026-10-01.json).

## Implementation and reuse audit

The Legacy source audited was CopperMod commit
[`8fb826e`](https://github.com/ilehtoranta/CopperMod/tree/8fb826eacf8e9dc32948b482c878079448bd6e61/CopperMod.Amiga).
MIT register masks, display-window decoding, SuperHires palette multiplexing,
sprite positioning, Copper phase classification and the horizontal-position
clamping policy were adapted from `CustomChips/Denise/Display.Geometry.cs`,
`Display.Bitplanes.cs`, `Display.Registers.cs`, `Display.Sprites.cs`, and
`CustomChips/Agnus/Registers.cs`, `CopperDmaPhases.cs`, `Timing.cs`.
Its scheduler and renderer dependencies were not imported. The production
solution has no sibling CopperMod references or Legacy fallback.

Implemented behavior includes:

- Two-plane SuperHires DMA/shifters, independent adjacent 35 ns color samples,
  70 ns sprites with SPRxCTL bit 4 positioning, and preserved lores/hires,
  HAM, EHB, dual-playfield, priority and collision behavior.
- Independent ECS Denise identification and BPLCON3, explicit DIWHIGH validity
  and upper window bits, border controls, and digital genlock keys in alpha.
- ECS beam register masks, programmable totals/blank/sync comparators, HHPOSR
  and HHPOSW, NTSC alternating line lengths and LOLDIS, and blitter DOFF
  memory-output suppression while retaining D phases and pointer/busy behavior.
- Standard PAL/NTSC sprite control reload on lines 25/20, followed by visible
  output on lines 26/21 and the corresponding desktop border/standard crop.
- Rephased disk, sprite, audio and CIA/TOD calendars on the canonical clock.
  An accepted DMA address/output survives subsequent timing or pointer writes.
- Reusable completed field buffers with their own dimensions, safe desktop
  frame leases during resizing, and host pacing from the selected raster.

The register and mode expectations use the
[Commodore ECS appendix](https://bastya.net/AmigaDevDocs/hard_c.html).
The ECS genlock truth table is also documented in Commodore's
[1991 graphics notes](https://github.com/rkrajnc/minimig-mist/blob/master/doc/amiga/aga/RandyAGA.txt).
Palette bit 15/selected-plane keys mean transparency; the default COLOR00 key
applies when the palette key enable is clear. Alpha is available to callers;
the desktop currently presents an opaque raster.

The board remains PAL: selecting NTSC display counters does not replace its
oscillator or change physical CPU/CIA/audio clock frequencies. A mode change
can straddle a field; completed buffer geometry converges at following field
boundaries. Stable geometry allocates no memory during engine execution.

The PAL/NTSC hardwired RVB comparator positions also agree with the timing
research comments in [WinUAE's custom-chip source](https://github.com/tonioni/WinUAE/blob/master/custom.cpp).
This is supporting emulator research, not an independent hardware trace.

## Validation

| Check | Result |
| --- | --- |
| `dotnet build CopperScreen.slnx -c Release` | Passed, zero warnings/errors |
| Engine diagnostics, separate `artifacts/diagnostic-tests` outputs | 884 passed, zero skipped |
| Focused host tests | 137 passed; 5 optional native cases unavailable in the ordinary run |
| CopperDisk tests | 74 passed, zero skipped |
| Native regression tooling negative controls | 21 passed, zero skipped; no ROM/media replay |
| Supplied native HD desktop boot/write/reopen matrix | 8 configurations passed, zero skipped |
| Default OCS native control | All 35 frame/state/RAM/PCM files identical to the preceding Agnus baseline; final HDF also identical |
| ECS HD boot/write | Native proof verified independently; normal/scalar captures, ECS metadata and final HDF identical |
| Native display probes | Stock PAL SuperHires progressive/interlaced and guest-programmed NTSC/Productivity passed; each normal/scalar pair has identical captures and final HDF |

The 49 new ECS tests discriminate register masks/ID, extended-window validity,
totals and resizing, shortened-line completion, NTSC long-line TOD deadlines,
horizontal repositioning, PAL/NTSC sprite reload and first visible line,
SuperHires fetch order/modulo/palette/sprite placement,
collision clipping, DOFF, wrapped blank/sync intervals and genlock keys. They
also preserve existing-mode pixels, retain an accepted read across a timing
change, and check zero steady-state allocations. The host regression saves and
reloads the ECS profile and holds an older frame lease while newer geometry is
published; a programmable raster with PAL dimensions also remains uncropped.
The host additionally checks the NTSC border and 200-line standard window.
Interactive Settings/desktop presentation was not tested here.

The original probe in [scripts/probes/EcsDisplay](../../scripts/probes/EcsDisplay/README.md)
uses native Intuition/graphics/DOS calls and supplied NDK vectors. It writes a
proof only after OpenScreenTagList returns the requested dimensions. Drawing
finishes with WaitBlit before any programmed probe takes chipset ownership.
Programmed probes then install their own Copper list and hold it in an
unprivileged guest loop. ROM/media bytes remain local and uncommitted.

| Native mode | Requested screen | Completed raster | Coverage |
| --- | --- | --- | --- |
| PAL SuperHires | 1280×256, ModeID `$29020` | 1816×313, BPLCON0 `$2240` | Stock native Intuition/graphics mode, 2400 fields |
| PAL SuperHires interlace | 1280×512, ModeID `$29024` | 1816×313 per field, BPLCON0 `$2244` | Stock native mode with alternating field lengths, 1200 fields |
| Programmed NTSC SuperHires | 1280×200 PAL allocation, then BEAMCON0 `$0000` | 1824×263, BPLCON0 `$2241` | Native guest drives alternating 227/228-CCK geometry, 1200 fields |
| Programmed Productivity | 640×480 PAL virtual allocation, then HTOTAL 113 / VTOTAL 524 / BEAMCON0 `$5B80` | 912×525, BPLCON0 `$2271` | Native guest drives 31 kHz timing and extended window, 1200 fields |

Proofs are checked by the independent bounded FFS reader, separately from
engine register captures and reviewed panel pixels. Captures include five
checkpoints per replay. The native desktop HD test additionally boots a fresh
machine from the persisted copy and verifies the guest's reopen marker.

## Verification limits

The subsequent [ECS-required media replay](ECS_REQUIRED_MEDIA_2026-10-01.md)
tests Final Fight: Enhanced — Final Edition through its opening level and
continue screen. It supplies native 2 MiB Chip RAM and ECS border-blanking
coverage with an OCS Denise control; it does not extend third-party coverage
to SuperHires or programmable timing.

Stock NTSC SuperHires and stock VGA Productivity are **unavailable native
coverage** with the supplied Workbench/Install fixtures. Their monitor
launchers are absent. The diagnostic guest reports ModeNotAvailable `$00000002`
(no monitor) for NTSC `$19020`, and `$FFFFFFFF` (no display record) for
VGAPRODUCT_KEY `$39024`; OpenScreenTagList returns null in both cases.
See the primary [ModeID definitions](https://d0.se/include/graphics/modeid.h)
and [availability flags](https://d0.se/include/graphics/displayinfo.h).
Guest-programmed probes establish hardware execution and do not stand in for
successful stock monitor-driver use.

Analog genlock/composite sync pins, an external synchronized image source,
A2024/UHRES scan conversion, arbitrary monitor pixel aspect and exhaustive
silicon transition phases are outside this record. The standard 114-CCK /
525-line Productivity presentation compensates for progressive host row
duplication; other custom geometries remain uncropped. Existing VHPOSW/VPOSW
counter repositioning gaps and physical synchronization uncertainties remain
in [ISSUES.md](ISSUES.md).

This is correctness evidence. Runner FPS/allocation diagnostics are not
performance acceptance. Historical protocols, fingerprints, placement and
host-load policy remain unchanged; see [PERFORMANCE.md](PERFORMANCE.md).
