# CopperStart F17 Challenge race controls — 2026-10-23

The current CopperStart ROM boots the supplied original F17 Challenge IPFs,
loads Disk 2 after a physical DF0 swap, and reaches the opening South Africa
World Championship race. Driving exercises acceleration, automatic gears,
steering, collisions and braking. Keyboard pause/resume, status-panel hiding
and restoration, and Escape to the race-results screen also work. No new
compatibility defect requiring a ROM or engine correction was isolated.
The race is deliberately aborted; no completed lap, race win or championship
completion is claimed.

The unchanged 524288-byte ROM is produced by CopperStart commit
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The engine includes the [Worms sprite/audio DMA correction](WORMS_SPRITE_DMA_2026-10-22.md),
source `4b71596195a96c4cbbb1abe7a3e673588d8fbc9f`, binary SHA-256
`4c6a6129ae498031ac2bb321bcbb33b90ba2d766068edf7a36b2a2e27d4c81ff`.
The qualified Copper68k, CopperDisk and CopperFloat binaries are retained.
No argument checks, guest-memory patches, protection bypass, extra RAM,
display cropping or host OS shims are introduced.

## Native media replay

The machine remains PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus
512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313
raster. The ZIP's original `F17Challenge_Disk1.ipf` and
`F17Challenge_Disk2.ipf` are used without extraction or modification.

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 2000, 4000 | Helmet presentation and credits. |
| 5000 | Black startup transition; not counted as gameplay. |
| 7000 | Visible Disk 2 request. Disk 1 is ejected at 7002; Disk 2 is inserted at 7202. |
| 10000 | Swap awaits acknowledgement; fire at 10002 continues loading. |
| 13000 | Interactive main menu. |
| 15000 | World Championship, South Africa, circuit length 3472 m. |
| 15400 | South Africa briefing and sunny weather report. |
| 18000 | Starting grid, zero speed, first gear, zero damage, lap 1/5. |
| 19400 | Holding fire accelerates to 273 km/h and fifth gear. |
| 19600–20000 | Steering and contact change the view, reduce speed and increase damage. Acceleration recovers speed; Down braking stops the car. |
| 20200, 20400 | P pauses the game; these full rasters are byte-identical. |
| 20800 | P resumes driving, with advancing lap time and recovered speed. |
| 21000, 21200 | I hides and restores the status panel while driving continues. |
| 23000 | Escape has left the race and the finishing-results screen is visible. |

The [preserved publisher manual](https://www.lemonamiga.com/doc/f17-challenge/598)
describes selectable button/stick acceleration, joystick steering and braking,
and the P/I/Escape keys. Up alone initially leaves the car stationary with
the current settings; holding fire accelerates. This is a control-mode
distinction, not an API or hardware failure. No trainer, name cheat, difficulty
change or saved-game load is used.

Native Kickstart 3.1 on the same machine and original media also passes the
disk swap, menus, driving controls, keyboard operations and race exit. Native
OS allocations, startup timing and finishing-driver order differ; native and
CopperStart RAM/state are not expected to match.

## Verification and provenance

All three final reset-based runs exit successfully at field 23000, with no
unsupported operation reported. Each retains 1155 files across 231 captured
fields; 129 fields contain nonzero PCM. CopperStart normal and scalar captures
are byte-identical. Each final run also reproduces all 1155 files from its
corresponding discovery run, including the native Kickstart reference.

Discovery and qualification are separate runs. The final three runs receive
the complete identical physical-input workload before launch,
including the original Disk 2 ZIP selector. They exit using an immutable
control file written before process creation; no guest state is restored.
Snapshots include state, both RAM banks, the complete raster and field PCM
at field 1 and every 100 fields through 23000.

The local functional media harness is rebuilt against the unchanged qualified
runtime, with zero build warnings or errors. Its only source change retries
a host control-file read after a Windows sharing IOException, resolving the
earlier interactive harness race.
This adds no guest validation or emulation behavior. Its source, build log,
binary, ROM, media and input identities are retained in the
[machine-readable record](COPPERSTART_F17_2026-10-23.json).

This repository change adds evidence and the replay workload only. Production
code and the ROM are unchanged, so the production build and emulator suites
are not rerun for documentation. The preceding Worms record retains their
qualified results; this harness build is a separate result. These runs assess
bounded compatibility, not audio fidelity, complete hardware timing or host
throughput. ROMs, IPFs, build outputs and captures remain local. Later circuits,
completed laps/races, pit repair, rain, saves and championship progression
remain unqualified.
