# CopperStart Miami Chase: opening street gameplay — 2026-10-23

The current CopperStart ROM boots the supplied Miami Chase ADFs, accepts the
physical Disk 2 swap and reaches one-player street gameplay. Directional
driving, scrolling, firing, explosions, the police-stop message, radar,
pause/resume and continued play with the Shield indicator work. Escape returns through the
high-score screen to the main menu. Native A500 Kickstart 3.1 on the same
engine/profile also completes these transitions. No new compatibility defect
requiring a ROM or engine correction was established. Mission completion
remains unqualified.

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
The supplied `downloads_miamichase_ADFs.zip` mixes an unlabeled Disk 1 and a
Disk 2 labeled `[cr AGL]`; the loaded menu displays QUARTEX. These are the
same supplied entries identified in the [earlier native corpus](NATIVE_CORPUS_2026-09-21_MEDIA.json).
This replay establishes compatibility with those bytes, not pristine
original-media protection behavior. The test applies no additional media or
guest-memory patches, trainer, cheat, saved-game load or guest-state restoration.

| ZIP entry | Bytes | SHA-256 |
| --- | ---: | --- |
| `Miami Chase (1990)(Codemasters)(Disk 1 of 2).adf` | 901120 | `080a79c0eb2be31892df55c47aefc0f0ec2656e81d53e98ca79d134006b0cbaf` |
| `Miami Chase (1990)(Codemasters)[cr AGL](Disk 2 of 2).adf` | 901120 | `563662cd9705d5f4e510456368278251bfa71712fcbe1b46b1cd182d27e18ff4` |

The engine already includes the [final-row blitter modulo correction](BLITTER_FINAL_MODULO.md)
that repaired the earlier accumulating menu text. The menu, credits and
high-score screens clear between transitions here. This run extends that
older native menu-only evidence into replacement-ROM gameplay; it does not
repeat historical throughput acceptance.

## Native replay observations

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 5000 | Miami Chase title and car presentation. |
| 8000 | Introductory mission briefing. |
| 9000 | Disk 2 prompt. Disk 1 is ejected at 9300, Disk 2 inserted at 9400 and acknowledged with fire at 10000. |
| 11000, 12000 | Main menu and credits, without accumulated text from the preceding screen. |
| 12500 | Fire has returned from credits to the main menu, with Start Game highlighted. Fire at 12502 selects it. |
| 14500 | Visible player car, traffic, streets and HUD in gameplay. |
| 15000 | Police-stop dialogue; fire returns to the streets by 15500. |
| 15600–16000 | Up/fire input, traffic movement and increased score. |
| 16100–16800 | Radar Scanner after Space; the map contains flashing traffic points. Fire does not dismiss it. |
| 17000 | Space closes radar; down input moves the car along the road and scrolls the scenery. |
| 17400 | Right input turns/moves the car to the roadside; scenery and HUD remain coherent. |
| 17600, 17800 | P pauses play. These full rasters are byte-identical under each ROM. |
| 17900 | P resumes play; left/fire input produces movement and a visible explosion. |
| 18200 | Another police-stop message during continued directional/fire input. |
| 18500, 18600 | Return to street gameplay and further driving/fire; the Shield indicator appears by 18600. A life-loss count is not established. |
| 20000, 21200 | Escape ends the attempt, loads the high-score screen and returns to the main menu. No return disk swap is needed. |

The [preserved instructions](https://www.lemonamiga.com/doc/miami-chase/1073)
describe joystick movement and menu selection, and Space to display radar.
The replay leaves the one-player defaults in place. It does not establish
criminal-gang clearance, a mission exit, garage purchases or later cities.
Native Kickstart completes the same bounded controls and transitions; its OS
allocations, cycle origin and complete RAM/state need not match CopperStart.

## Verification and provenance

All three final reset-based runs exit successfully at field 21200, with no
unsupported operation reported. Each retains 1065 files across 213 captured
fields; 148 fields contain nonzero PCM. CopperStart normal and scalar captures
are byte-identical. Each final run also reproduces all 1065 files from its
corresponding discovery run, including the native Kickstart reference.

The full [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/miami-chase-copperstart-play-v1.json)
and immutable exit-control file are written before final process creation.
Discovery and qualification are separate runs. Captures retain state, both
RAM banks, the full raster and field PCM at field 1 and every 100 fields
through 21200.

The [machine-readable record](COPPERSTART_MIAMI_CHASE_2026-10-23.json)
retains actual commands, ROM/media/input/binary identities, capture manifests
and the retained harness source/build provenance. The unchanged functional
harness is not rebuilt. Production code and ROM are unchanged, so production
builds and emulator suites are not rerun for this documentation/workload
change. The preceding Worms record retains those separate qualified results.
ROMs, media, captures and build outputs remain local.

Pristine original media, mission completion, later cities, garage purchases,
two-player play and disk saving remain unqualified. Nonzero PCM establishes
sample presence, not listening or audio fidelity. Native Kickstart on this
engine is not a physical hardware oracle. No host throughput or complete
hardware-timing acceptance claim is made.
