# CopperStart Lotus III: player-controlled Arcade racing — 2026-10-23

The current CopperStart ROM boots the supplied Lotus III: The Ultimate
Challenge two-ADF release and reaches player-controlled one-player Arcade
racing. Disk swapping, setup, car/music selection, Fire acceleration,
automatic gears, steering, braking, F10 pause/resume, a Turbo Zone and
timeout through score screens back to setup work. Native Kickstart 1.3 on
the same engine/profile reaches the same bounded milestones. No new
CopperStart or engine compatibility correction was established.

## Configuration and media

The unchanged 524288-byte CopperStart ROM comes from producer commit
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The engine retains the [Worms sprite/audio DMA correction](WORMS_SPRITE_DMA_2026-10-22.md),
source `4b71596195a96c4cbbb1abe7a3e673588d8fbc9f`, binary SHA-256
`4c6a6129ae498031ac2bb321bcbb33b90ba2d766068edf7a36b2a2e27d4c81ff`.
The [F17 replay](COPPERSTART_F17_2026-10-23.md)'s Copper68k, CopperDisk,
CopperFloat and functional host harness binaries are retained unchanged.

The profile is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
The supplied archives and ADFs are labeled
`Lotus III - The Ultimate Challenge (1992)(Gremlin)(Disk n of 2)[cr FLT - Crack Inc]`.
Each ADF contains 901120 bytes:

| Disk | ADF SHA-256 |
| --- | --- |
| 1 | `557979cb5fa775e1fa2e2864bb4800667fb3a24b782daa6900157fa6990d8546` |
| 2 | `cde309be4d5d6508ddf4c825f174ff990f61552ce233999b5723939334996887` |

The supplied cracked release and its manual-word screen are explicit
provenance limits. Return advances that screen without entering a word.
This does not qualify original-media protection behavior. No additional
media/guest-memory patch, trainer, cheat, saved-game load or guest-state
restoration is used.

## Loading and input alignment

The [earlier disk-two investigation](LOTUS_III_INVESTIGATION_2026-09-25.md)
identified a game polling loop that can discard a received Fire edge.
The [generic CPU bus-spacing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md)
is already present in this runtime and remains unchanged. The current
CopperStart run consumes the Fire press at 6502; native Kickstart 1.3 still
shows the prompt at 8500 and consumes the repeated press at 8502. Later
presentation/menu phases therefore differ. This is not evidence for a new
CopperStart-specific defect or a reason to patch the loader.

An additional native-only Fire press/release at 11502/11522 exits its later
attract-demo phase. Both ROMs are at the same setup screen by field 12000.
All subsequent physical inputs are identical. Separate
[CopperStart](../../CopperMod.Amiga.Lightweight.Runner/Workloads/lotus3-copperstart-driving-v1.json)
and [native Kickstart 1.3](../../CopperMod.Amiga.Lightweight.Runner/Workloads/lotus3-kickstart13-driving-v1.json)
workloads retain this two-event difference; whole-boot cross-ROM input
identity is not claimed.

## Native replay observations

| Completed field | Reviewed observation under both ROMs unless specified |
| ---: | --- |
| 5000 | Supplied release's cracked manual-word screen. |
| 6000 | Insert-disk-two prompt; disk 1 is ejected at 6002 and disk 2 mounted at 6202. |
| 8500 | CopperStart credits; native Kickstart still at the prompt before the repeated Fire. |
| 10000 | CopperStart attract driving demo; this is not player-controlled racing. |
| 12000, 12100 | Setup responds to joystick navigation; one player, Arcade, automatic P1 gears and Fire acceleration selected. |
| 13000 | Car selection displays the M200; Fire confirms. |
| 14500, 15000 | Music selector; Right changes the selection and Fire starts loading the race. |
| 17000 | Opening race, visible player M200, zero KMH, first gear and timer 66. |
| 17400 | Held Fire raises speed and shifts automatically; cars, road and scenery move. |
| 17500, 17700 | Right then left changes player-car/road alignment. |
| 18200, 18400 | Fire-with-Back then Back alone reduce speed to 14 KMH and first gear. A zero-speed stop is not established. |
| 18600 | Fire accelerates again to fourth gear. |
| 18700–19000 | F10 (raw `$59`) displays PAUSED; raster and timer remain frozen. |
| 19200 | Second F10 tap resumes; Fire acceleration, road motion and timer countdown return. |
| 19400 | TURBO ZONE, visible high-speed driving. |
| 20600–20800 | Timer 02, then 00, then OUT OF TIME; speed falls to zero. |
| 21400 | Current Scores: 49460 under CopperStart, 49910 under native Kickstart. |
| 22600 | Easy Level High Scores after Fire. |
| 23200 | Another Fire returns to setup with automatic P1 gears, Fire acceleration and one-player Arcade options retained. |

The [preserved Amiga instructions](https://www.lemonamiga.com/doc/lotus-3-the-ultimate-challenge/1024)
describe the setup choices, driving controls and timed Arcade mode. The
F10 result is established by the captured PAUSED text, frozen scene/timer
and subsequent resumed driving. Speeds, scenery phases and scores differ
between ROMs, so cross-ROM pixel/state equality is not claimed. The timed
failure is verified; no successful race or stage completion is claimed.

This follow-up also corrects a key label in the
[earlier Lotus Esprit Turbo Challenge record](COPPERSTART_LOTUS1_2026-10-23.md):
its raw `$5F` input is Help, not F10 (`$59`). The historical workload and
capture hashes are unchanged, and pause remains unqualified for that title.

## Verification and provenance

All three final reset-based runs exit successfully at field 23200, with no
unsupported operation reported. Each retains 1165 files across 233 captured
fields. CopperStart has nonzero PCM in 143 fields per mode; native Kickstart
1.3 has 123. CopperStart normal and scalar captures are byte-identical.
Every final filename, size and SHA-256 also matches its corresponding
discovery manifest, including native Kickstart 1.3.

Within each run the four captured rasters at 18700, 18800, 18900 and 19000
are identical during pause. The resumed raster at 19200 differs. The full
input and immutable exit-control files are written before final process
creation; discovery and qualification are separate reset-based runs.
State, both RAM banks, the full raster and field PCM are retained at field
1 and every 100 fields through 23200.

The [machine-readable record](COPPERSTART_LOTUS3_2026-10-23.json) retains
actual commands, ROM/media/input/binary identities, capture manifests,
the per-ROM input difference and retained harness source/build provenance.
The functional harness is unchanged and is not rebuilt. Production code
and ROM are unchanged, so production builds and emulator suites are not
rerun for this documentation/workload change. ROMs, media, captures and
build outputs remain local.

Pristine original media, completed races/stages/championships, later courses,
other cars, manual gears, split-screen, RECS construction/codes and
persistent saving remain unqualified. Nonzero PCM establishes sample
presence, not listening or fidelity. Native Kickstart on this engine is
not a physical hardware oracle. No host throughput or complete
hardware-timing acceptance claim is made.
