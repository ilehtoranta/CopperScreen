# CopperStart Lotus Esprit Turbo Challenge: driving controls — 2026-10-23

The current CopperStart ROM boots the supplied Lotus Esprit Turbo Challenge
ADF and reaches one-player racing. Options, soundtrack selection, normal
Up acceleration, automatic gear changes, steering, braking, Escape back to
the presentation and a second race with alternative Fire acceleration work.
Kickstart 1.3 on the same engine/profile reaches the same bounded milestones.
No compatibility defect requiring a ROM or engine correction was established.
Pause and lap/race completion remain unqualified.

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
The supplied archive and its single ADF are labeled
`Lotus Esprit Turbo Challenge (1990)(Gremlin)[cr Angels][b doscopy]`.
The 901120-byte ADF has SHA-256
`6a7c7cac2d122795c3e254348e4519f0838ed7e535ba10c60406c1adffe1c02d`,
matching the [earlier native corpus](NATIVE_CORPUS_2026-09-23_MEDIA.json).
The crack and bad-copy labels remain explicit provenance limits. This does
not qualify pristine original-media protection behavior or every part of
the supplied copy. No additional media/guest-memory patch, trainer, cheat,
saved-game load, guest-state restoration or disk swap is used.

## Native replay observations

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 5000 | Animated car presentation with coherent artwork and text. |
| 7000 | In-game instructions for normal and alternative joystick controls. |
| 8000–8200 | Options respond to direction input. A temporary Medium selection is restored to Easy; one player, computerised gears and normal controls are selected. |
| 9000, 9300 | Music selector; Right changes track 00 to 01. Fire starts the race. |
| 11000, 12000 | Race scene, visible player car, MPH/gear/fuel/position HUD and lower garage display; MPH is zero before acceleration. |
| 12400 | Held Up increases speed and advances automatic gears; the road and scenery scroll. |
| 12600, 12800 | Right/left steering changes car/road alignment; roadside contact and speed reduction are visible. |
| 13200 | Back braking reduces the MPH display to zero. |
| 13800, 14000 | Motion continues after a short F10 tap. This does not establish pause. |
| 14600, 14800 | Motion also continues after a short P tap; pause remains unqualified under both ROMs. |
| 16000 | Escape returns to the car presentation. |
| 17000, 17600 | Re-entry through the instructions and options; Easy/one player/computerised gears are retained. |
| 17700, 18400 | Alternative control is selected; the music selector retains track 01. |
| 20000 | A second race starts with the changed control method. |
| 20400, 20700 | Fire accelerates and shifts automatically; Fire with left/right exercises steering. |
| 21200 | Fire with Back brakes to zero MPH; the race display remains coherent. |

The [preserved instructions](https://www.lemonamiga.com/doc/lotus-esprit-turbo-challenge/1023)
describe the options and gear choices. The captured in-game instruction
screen supplies the joystick definitions: normal Up/Back acceleration and
braking, versus alternative Fire and Fire-with-Back. That screen states that
manual gear-change combinations have no effect with computerised gears
selected; manual gear behavior is not qualified here.

F10 and P are retained as attempted inputs, without counting them as
successful pause checks or assigning a CopperStart-specific defect. Escape
works in both ROMs. Later speeds, race positions and collision phases differ
between the ROMs, so cross-ROM image/state equality is not claimed. Neither
attempt completes a lap or race.

## Verification and provenance

All three final reset-based runs exit successfully at field 21200, with no
unsupported operation reported. Each retains 1065 files across 213 captured
fields. CopperStart has nonzero PCM in 194 fields per mode; Kickstart 1.3 has
196. CopperStart normal and scalar captures are byte-identical. Every final
filename, size and SHA-256 also matches its corresponding discovery manifest,
covering all 1065 files per run, including Kickstart 1.3.

The full [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/lotus1-copperstart-driving-v1.json)
and immutable exit-control file are written before final process creation.
Discovery and qualification are separate runs. Captures retain state, both
RAM banks, the full raster and field PCM at field 1 and every 100 fields
through 21200.

The [machine-readable record](COPPERSTART_LOTUS1_2026-10-23.json) retains
actual commands, ROM/media/input/binary identities, capture manifests and the
retained harness source/build provenance. The unchanged functional harness
is not rebuilt. Production code and ROM are unchanged, so production builds
and emulator suites are not rerun for this documentation/workload change.
The preceding Worms record retains those separate qualified results. ROMs,
media, captures and build outputs remain local.

Pristine original media, pause, completed laps/races, championship/license
progression, later circuits, practice, manual gears, mouse controls,
two-player play, pit/refuelling behavior and persistent saving remain
unqualified. Nonzero PCM establishes sample presence, not listening or
fidelity. Native Kickstart on this engine is not a physical hardware oracle.
No host throughput or complete hardware-timing acceptance claim is made.
