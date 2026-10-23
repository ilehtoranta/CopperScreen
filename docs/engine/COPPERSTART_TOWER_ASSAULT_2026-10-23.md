# CopperStart Tower Assault: Civilian Zone 1 — 2026-10-23

The current CopperStart ROM boots original Tower Assault SPS 279, accepts the
physical Disk 2 swap, and runs the crash-landing area. Movement, scrolling,
pickups, firing, mine/projectile effects and pause/resume work. The player
passes the first fence, follows the access roads, receives the Civilian
Facility PDC message and enters Civilian Zone 1. Indoor gameplay, its PDC
message and subsequent movement/fire also work. No new compatibility defect
requiring a ROM or engine correction was established. Civilian Zone 1
completion and later campaign progression remain unqualified.

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
All three supplied IPF identities match the SPS 279 OCS/AGA release in
[MAME's original-media manifest](https://github.com/mamedev/mame/blob/master/hash/amigaocs_flop.xml).
The [publisher manual](https://evercade.info/wp-content/uploads/2023/03/AlienBreed-TowerAssault_manual.pdf)
describes a 1 MiB Amiga minimum and automatic chipset detection. Only the
original Disk 1 and Disk 2 ZIP entries are mounted in this OCS replay. Disk 3
and AGA behavior are not qualified here. No guest-memory patches, extra RAM,
protection bypass or title-specific emulation behavior is added.

The engine already includes the [OCS identification correction](TOWER_ASSAULT_INVESTIGATION.md)
and its subsequent retained changes. That older native Kickstart record is
separate evidence; this run adds replacement-ROM coverage and the transition
beyond the crash site. It does not repeat historical throughput acceptance.

## Native replay observations

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 4000, 5000 | Disk 2 prompt. Disk 1 is ejected at 5002 and Disk 2 inserted at 5202. Fire at 8002 continues post-swap startup. |
| 10000, 11000 | High scores and keyboard controls. |
| 13000 | Options: one player, Normal difficulty, joystick control. |
| 15000 | Crash Landing Site briefing, after Escape and empty-code Return. No mission password is supplied. |
| 16000, 17000 | Player, wrecked dropship, pickups and animated effects in gameplay. |
| 17200 | Movement, scrolling and nearby pickups disappearing on contact. |
| 17400, 17600 | M does not display a map in this outdoor state under either ROM. Hand-map coverage remains unqualified. |
| 17800, 18000 | P pauses the game; these full rasters are byte-identical. |
| 18100, 18700 | P resumes play. Movement/fire, projectiles and explosions continue; the first fence is passed. |
| 19500, 19700 | Civilian Facility PDC text after following the road. |
| 20000, 21000 | Fire returns from PDC text to the facility's access road. |
| 23000, 24000 | Civilian Zone 1 briefing after leaving the crash-landing area. |
| 25000, 25500 | Indoor gameplay with the visible player and a different room layout. |
| 25700, 25800 | Civilian Zone 1 PDC story message triggered by movement. |
| 26000–26300 | Return to indoor movement, scrolling and firing. |
| 26500 | Submitted S input reopens the current PDC text under both ROMs; this does not qualify a statistics screen. |
| 28000 | Continued indoor gameplay after dismissing the recalled PDC text. |

The [preserved controls](https://www.lemonamiga.com/doc/alien-breed-tower-assault/50)
describe joystick movement/fire, pause and the PDC functions. The default
one-player settings are retained. No trainer, cheat, mission password,
saved-game load or guest-state restoration is used. Both M attempts (a short
press and a held key) remain in the workload rather than being counted as a
successful map check. Full keyboard/statistics behavior is not qualified.

Native A500 Kickstart 3.1 on the same engine/profile and media also completes
the disk swap, crash-site traversal, pause/resume, PDC messages, Civilian
Zone 1 loading and indoor gameplay. Its complete state and RAM are not
required to match CopperStart's OS allocations and startup timing.

## Verification and provenance

All three final reset-based runs exit successfully at field 28000, with no
unsupported operation reported. Each retains 1405 files across 281 captured
fields; 188 fields contain nonzero PCM. CopperStart normal and scalar captures
are byte-identical. Each final run also reproduces all 1405 files from its
corresponding discovery run, including the native Kickstart reference.

The full [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/tower-assault-copperstart-civilian-v1.json)
and immutable exit-control file are written before final process creation.
Discovery and qualification are separate runs. Captures retain state, both
RAM banks, the full raster and field PCM at field 1 and every 100 fields
through 28000.

The [machine-readable record](COPPERSTART_TOWER_ASSAULT_2026-10-23.json)
retains actual commands, ROM/media/input/binary identities, capture manifests
and the retained harness source/build provenance. The unchanged functional
harness is not rebuilt. Production code and ROM are unchanged, so production
builds and emulator suites are not rerun for this documentation/workload
change. The preceding Worms record retains those separate qualified results.
ROMs, media, captures and build outputs remain local.

Civilian Zone 1 completion, later areas, Disk 3/AGA, map/statistics functions,
two-player play, saving and full shop/weapon coverage remain unqualified.
Nonzero PCM establishes sample presence, not listening or audio fidelity.
Native Kickstart on this engine is not a physical hardware oracle. No host
throughput or complete hardware-timing acceptance claim is made.
