# CopperStart Overdrive Arcade controls and disk swaps — 2026-10-23

The current CopperStart ROM boots the original Overdrive IPFs, loads Disk 2
after a physical DF0 swap, and runs the first Arcade race. The red player
car stays visible while accelerating, steering in both directions, braking
and scrolling through the circuit. P pauses and resumes play. Escape aborts
the race, requests Disk 1 and returns to the main menu after the return swap.
No new compatibility defect requiring a ROM or engine correction was found.
No completed lap, race win or campaign completion is claimed.

## Configuration and observations

The unchanged 524288-byte CopperStart ROM comes from producer commit
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The engine retains the [Worms sprite/audio DMA correction](WORMS_SPRITE_DMA_2026-10-22.md),
source `4b71596195a96c4cbbb1abe7a3e673588d8fbc9f`, binary SHA-256
`4c6a6129ae498031ac2bb321bcbb33b90ba2d766068edf7a36b2a2e27d4c81ff`.
The [F17 replay](COPPERSTART_F17_2026-10-23.md)'s qualified Copper68k,
CopperDisk, CopperFloat and host harness binaries are retained unchanged.

The profile is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus
512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0, and the full
908×313 raster. `Overdrive_Disk1.ipf` and `Overdrive_Disk2.ipf` are loaded
directly from the supplied ZIP. Guest execution uses the standalone ROM;
no guest-memory patch, protection bypass or title-specific behavior is added.

| Completed field | Reviewed observation |
| ---: | --- |
| 1000 | Team17 presentation. |
| 6000, 7000 | Main menu, with Start selected. |
| 9000, 10000 | Disk 2 request. Disk 1 is ejected at 10002, Disk 2 inserted at 10202, then fire acknowledges at 10402. |
| 13000 | Arcade Race 1 selection. CopperStart offers Buggy / City circuit 1, against Burt and Dan. |
| 15000 | City circuit briefing and preview. |
| 16000 | Race grid with player and opponent cars visible. |
| 16500, 17000 | Fire accelerates the visible player car and the course scrolls. |
| 17100–17400 | Right steering changes the car's heading and path; Down applies the brake. |
| 17600, 17800 | P pauses the game. These complete rasters are byte-identical. |
| 18000, 18400 | P resumes the lap clock and movement; acceleration and left steering work. |
| 19400 | Escape has left the race and the Disk 1 request is visible. |
| 22000, 23000 | Title art and credits after Disk 2 ejection at 19402, Disk 1 insertion at 19602 and fire acknowledgement at 19802. |
| 24000 | Fire at 23002 skips the repeated presentation and returns to the main menu. |

The [preserved manual](https://www.lemonamiga.com/doc/overdrive/1183)
describes fire acceleration, left/right steering and back braking. The default
Arcade mode is retained; no trainer, cheat, difficulty change or saved-game
load is used.

Native A500 Kickstart 3.1 on the same engine/profile/media also completes both
disk swaps, race startup, driving controls, pause/resume and return to the
main menu. Its first offered scenario is 4x4 / Sand circuit 1, against the
same drivers. The differing vehicles, circuits and startup timing make this
a comparison of bounded behavior, not a requirement for identical race images
or guest RAM between the two ROMs.

## Verification and provenance

All three final reset-based runs exit successfully at field 24000, with no
unsupported operation reported. Each retains 1205 files across 241 captured
fields. CopperStart normal and scalar captures are byte-identical; each final
run also reproduces all 1205 files from its corresponding discovery run.
CopperStart retains 118 fields with nonzero PCM; native Kickstart retains 117.

The complete identical physical-input workload is supplied to all three final
runs before process creation. An immutable exit-control file is also written
before launch. Discovery and qualification are separate runs; no guest state
is restored. Captures retain state, both RAM banks, the full raster and field
PCM at field 1 and every 100 fields through 24000.

The [machine-readable record](COPPERSTART_OVERDRIVE_2026-10-23.json) records
the actual commands, ROM/media/input/binary hashes, capture manifests and
retained harness source/build provenance. The harness was already built and
qualified in the F17 replay; it is not rebuilt for this unchanged replay.
Production code and ROM are unchanged. Production builds and emulator suites
are not rerun for this documentation/workload change; the preceding Worms
record retains those separate results. Local ROMs, IPFs, captures and build
outputs are not committed.

This establishes bounded compatibility only. Later races, completed laps,
qualifying, other game modes, serial multiplayer and saving remain unqualified.
Nonzero PCM establishes sample presence, not listening or audio fidelity.
Native Kickstart on this engine is not a physical hardware oracle. No complete
hardware-timing or host-throughput acceptance claim is made.
