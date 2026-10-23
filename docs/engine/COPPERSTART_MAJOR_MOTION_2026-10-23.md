# CopperStart Major Motion: opening gameplay — 2026-10-23

The current CopperStart ROM boots the supplied Major Motion ADF and reaches
one-player road gameplay with a visible car, traffic and HUD. Acceleration,
steering, machine-gun fire, scrolling, score updates, braking, F2 pause/resume,
double-F1 mission abort, restarting one player and F3 sound toggling work.
Kickstart 1.3 on the same engine/profile completes the same bounded controls.
No CopperStart compatibility defect requiring a ROM or engine correction was
established. Loading is substantially slower than the native control, and
full route/mission completion remains unqualified.

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
The supplied archive `OK/Major Motion (1988)(Microdeal).zip` contains one
901120-byte `Major Motion (1988)(Microdeal).adf`, SHA-256
`3dba00f0525c3599ed530c359469ff4e6d7555cda3b752d2d95e84aa02fd756e`.
Its identity matches the [earlier native corpus](NATIVE_CORPUS_2026-09-21_MEDIA.json).
The filename alone does not certify pristine original protection behavior.
No disk swaps, guest-memory patches, trainer, cheat, saved-game load or
guest-state restoration are used.

## Loading and reference-ROM observations

CopperStart remains on its blank DOS display while loading `major`.
Snapshots establish progress through the 102680-byte executable: file
position 45056 at field 5000 and 96256 at field 16000. It subsequently enters
the game, with a clean title observed at field 25000. Kickstart 1.3 already
shows a clean title at field 5000. This is a remaining guest loading-performance
limitation, not a reproduced boot hang; no artificial disk-delay reduction,
extra memory or guest-state shortcut is applied.

The initial Kickstart 3.1 comparison displays corrupt title/attract graphics.
The separate no-input diagnostic continues to field 36000 and retains 1805
capture files with no unsupported-operation flags. Its cause is not assigned
to the game, ROM or engine. It is unresolved coverage, not a successful visual
replay. Kickstart 1.3, used by the [earlier working native replay](NATIVE_CORPUS_2026-09-21.md),
provides the clean reference for this qualification. This does not establish
general Kickstart 3.1 compatibility or physical-hardware correctness.

## Native replay observations

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 25000, 26000, 36000 | Clean title artwork after DOS loading. |
| 38000 | One-player road gameplay after raw key 1 at 36002. Visible car, traffic, weapons panel, score and remaining-car icons. |
| 38400 | Continued play after left-mouse fire input. |
| 38800 | Acceleration/fire input; traffic, road scrolling and score advance. |
| 39000, 39200 | Left/right input changes the player's position; roadside and traffic remain coherent. |
| 39400, 39600 | F2 pauses play; these full rasters are byte-identical within each ROM run. |
| 39800 | F2 resumes play; left/back and back input exercise steering/braking. |
| 40400 | Two F1 presses end the attempt and return to the title. |
| 41400 | Key 1 starts another one-player attempt. |
| 41600, 41800 | Continued gameplay with sampled PCM silent after F3. |
| 42000 | F3 restores nonzero PCM; paired machine-gun shots are visible ahead of the player's car during up/fire input. |
| 42400 | Continued road gameplay after right/fire and braking input. |

The [preserved manual](https://www.lemonamiga.com/doc/major-motion/1039)
documents joystick acceleration/braking/steering, machine-gun fire, F2 pause,
double-F1 abort and F3 sound toggling. Default weapons and one-player controls
are retained. Differences in startup timing, traffic, scores and subsequent
road position mean complete cross-ROM image/state equality is not claimed.

## Verification and provenance

All three final reset-based runs exit successfully at field 42400, with no
unsupported operation reported. Each retains 2125 files across 425 captured
fields. CopperStart has nonzero PCM in 174 fields per mode; Kickstart 1.3 has
382, reflecting their different startup intervals and game state. CopperStart
normal and scalar captures are byte-identical. Each final run also reproduces
all 2125 files from its corresponding discovery run, including Kickstart 1.3.

The full [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/major-motion-copperstart-play-v1.json)
and immutable exit-control file are written before final process creation.
Discovery and qualification are separate runs. Captures retain state, both
RAM banks, the full raster and field PCM at field 1 and every 100 fields
through 42400. The mute/unmute checks and pause-image checks are asserted
against the final reset captures.

The [machine-readable record](COPPERSTART_MAJOR_MOTION_2026-10-23.json)
retains actual commands, ROM/media/input/binary identities, capture manifests,
the separate Kickstart 3.1 diagnostic and retained harness source/build
provenance. The unchanged functional harness is not rebuilt. Production code
and ROM are unchanged, so production builds and emulator suites are not rerun
for this documentation/workload change. The preceding Worms record retains
those separate qualified results. ROMs, media, captures and build outputs
remain local.

Complete routes/missions, weapon-truck boarding and upgrades, additional
weapons, water sections, two-player mode, key reassignment and persistent
saving remain unqualified. Nonzero PCM and mute switching establish sampled
output behavior, not listening or fidelity. No host throughput or complete
hardware-timing acceptance claim is made.
