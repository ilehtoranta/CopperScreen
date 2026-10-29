# CopperStart Lemmings: Fun 1 completion — 2026-10-23

The current CopperStart ROM boots the supplied Skid Row Lemmings two-ADF
release, reaches mouse-controlled gameplay and completes Fun 1, “Just dig!”,
with 100% rescued. Disk swapping, one-player selection, P pause/resume,
live digger assignment, terrain removal, rescue counting, the success/access-code
screen and progression to the Fun 2 briefing work. Native Kickstart 1.3 on
the same engine/profile reaches the same bounded milestones with identical
physical inputs. No new CopperStart or engine correction was established.

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
`Lemmings (1991)(Psygnosis)(Disk n of 2)[cr SR]`.
Each ADF contains 901120 bytes:

| Disk | ADF SHA-256 |
| --- | --- |
| 1 | `a4fdba69017f1a760bba3bb06c55f7e434226c498c9d22c1f627212b08d24f91` |
| 2 | `1526000b96d196efecab9c63aa72c0ce6257649a4993141b00ead8738fd97a65` |

The supplied cracktro/release remains explicit provenance; pristine original
media and protection behavior are not qualified. No additional media or guest
memory patch, trainer, cheat, saved-game load, password entry or guest-state
restoration is used. Disk 1 is physically replaced with disk 2 in DF0.

## Native replay observations

| Completed field | Reviewed observation under both ROMs |
| ---: | --- |
| 1000 | Animated Skid Row cracktro with coherent text and effects. |
| 4000, 5000 | DMA Design/presentation phases and the animated Lemmings introduction; the two ROMs' presentation phases differ. |
| 6000 | Insert-disk-two prompt. Disk 1 is ejected at 6002, disk 2 mounted at 6202 and mouse-click confirmation sent at 6502. |
| 8500 | Animated main menu. Physical mouse movement and a click select one player. |
| 9500, 10100 | Fun 1, “Just dig!”: ten lemmings, 10% rescue target, release rate 50 and five minutes. The later capture shows the continue prompt. |
| 11000 | Gameplay after the click at 10402: visible walkers, OUT 5, IN 00%, timer 4:49 and skill panel. |
| 11200–11400 | P pauses the level: walkers, release count and timer are frozen before further mouse input. |
| 11500, 11600 | Mouse motion while paused scrolls the view. After resuming, a panel click selects Digger; a second pause leaves OUT 6, timer 4:48 and Digger selected. |
| 11800 | After resume and the click at 11624, the digger count falls from 10 to 9 and excavation begins. |
| 12000, 12400 | A hole opens through the upper terrain; lemmings descend to the lower route. |
| 13200 | Rescue counter reaches 30%; seven lemmings remain outside. |
| 14000–15000 | “All lemmings accounted for”: 10% needed, 100% rescued, success message and next-level access code. |
| 16600 | After another mouse click, Fun 2, “Only floaters can survive this”, briefing. |

The [preserved publisher manual](https://www.dosdays.co.uk/media/games/lemmings/Lemmings%20-%20Manual.pdf),
Amiga sections, documents mouse selection/skill assignment, P pause, disk
replacement and completion passwords. PC function-key controls are not used.

The early mouse click at 9502 did not advance the briefing; the P tap at
10102 also occurred before gameplay. They remain recorded physical input,
without being counted as successful gameplay or pause checks. The click at
10402 starts the level. Skill selection/assignment while paused is not
claimed: the successful Digger panel click follows resume at 11502, and
the successful lemming assignment follows resume at 11612.

## Verification and provenance

All three final reset-based runs exit successfully at field 16600, with no
unsupported operation reported. Each retains 835 files across 167 captured
fields. CopperStart normal and scalar captures are byte-identical. Every
final filename, size and SHA-256 also matches its corresponding discovery
manifest, including native Kickstart 1.3.

Within each run the captured rasters at 11200, 11300 and 11400 are identical
during pause. Later resume and skill interaction change the raster and
advance the game. The complete shared
[physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/lemmings-copperstart-fun1-v1.json)
and immutable exit-control file are written before final process creation.
Discovery and qualification are separate reset-based runs. State, both RAM
banks, the full raster and field PCM are retained at field 1 and every 100
fields through 16600.

The [machine-readable record](COPPERSTART_LEMMINGS_2026-10-23.json) retains
actual commands, ROM/media/input/binary identities, nonzero PCM counts,
capture manifests, pause checks and retained harness source/build provenance.
Nonzero PCM establishes sample presence, not listening or fidelity. The
functional harness is unchanged and is not rebuilt. Production code and ROM
are unchanged, so production builds and emulator suites are not rerun for
this documentation/workload change. ROMs, media, captures and build outputs
remain local.

This replay supplements the earlier bounded native Lemmings acceptance in
the [historical readiness record](../history/amiga/LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md).
It does not modify the historical performance workloads, intervals,
fingerprints or acceptance dispositions. No throughput result is claimed.

The [October 25 Fun 2 continuation](COPPERSTART_LEMMINGS_FUN2_2026-10-25.md)
uses the later rebuilt ROM, assigns all ten Floaters, completes Fun 2 with 100%
rescued and advances to Fun 3's briefing. Fun 3 gameplay remains unqualified.

This original replay did not qualify Fun 2 gameplay/completion, later
levels/ratings, every skill, two-player mode, persistent saving/password reload
or pristine original media. Native Kickstart on this engine is not a physical hardware
oracle; the replay does not establish complete hardware timing correctness.
