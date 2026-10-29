# CopperStart Lotus 2: checkpoint and Night Course — 2026-10-24

Lotus Turbo Challenge 2 now has a recorded first-checkpoint crossing with the
current rebuilt CopperStart ROM. The opening-course attempt subsequently runs
out of time before checkpoint two. A separate use of the game's `TWILIGHT`
password loads the Night Course and reaches driving, pause and resume. Native
Kickstart 1.3 reaches the same bounded milestones on the same runtime. No
additional CopperStart, CPU or engine compatibility defect was established.

This extends the [earlier opening-race test](COPPERSTART_LOTUS2_2026-10-18.md).
It does not establish natural completion of the opening course or a normal
transition into the Night Course. Password selection and checkpoint progression
are recorded separately.

## ROM, runtime and media

The ROM is the exact rebuilt image retained by the
[Full Contact continuation](COPPERSTART_FULL_CONTACT_2026-10-24.json): 524288
bytes, SHA-256
`fefe13e86ad00e3609ad4e385b5e3752e2407e1b5a9b67affa904d2160d9e8e7`.
Its owning native emitter source is CopperStart
`0057fb9c38d82d38122ddf69a4aa960573beedd7`, composed with the retained compiled
guest root from `cf7c9b54aefea723b018bd68e10e829e67684250`. This continuation
reuses that rebuilt ROM and its independent byte-identical composition; its
checksum and both local image identities are checked again. No producer source
or compiled guest root is rebuilt here.

Engine, CopperDisk and CPU binaries remain those qualified from merged
CopperScreen `29bc8635fe33ae9e267846ade511e9b368f7158d`. The evidence parent is
`efe0b0063b552d57eba0fb8121a8d35c12361367`. Copper68k remains the pinned,
unpublished `1.5.5-nitro.1` package. A local functional harness is built against
these exact binaries with captures every 25 fields. Its only change from the
retained harness source is the capture interval. All 595 common capture files
through field 11800 match the earlier 100-field-cadence prefix under each ROM,
including BMP, state, RAM and PCM. This is an observer-prefix check, not a
throughput measurement or an assertion about later differing inputs.

The machine is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
`Lotus Turbo Challenge 2 (Magnetic Fields + Gremlin).zip` is unchanged from the
earlier test. Its single 901120-byte ADF has SHA-256
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
There is no disk swap, media edit, trainer, saved state, guest-memory injection
or host OS shim. The password is typed through the emulated keyboard into the
game's ordinary options screen.

## Reviewed progression

The preserved [publisher manual](https://www.lemonamiga.com/doc/lotus-turbo-challenge-2/1025)
describes the options for automatic gears, forward/fire acceleration, the
password field and F10 pause. The recorded options select automatic gears and
fire-button acceleration. Menu moves later use short pulses to select the
password field; longer exploratory pulses had repeated through multiple items.

| CopperStart field | Reviewed result |
| ---: | --- |
| 9500–9800 | Options and changed player-one gear selection. |
| 11800–14000 | Sustained acceleration, automatic gear changes, steering and traffic in the opening course. |
| 14500–14525 | First checkpoint: timer changes from 08 to 42, with the checkpoint gantry and subsequent scenery visible in adjacent captures. |
| 16500–18000 | Timer approaches zero, followed by the timeout/result sequence, Game Over and high scores. Checkpoint two is not reached. |
| 19200–20100 | Options return; physical keyboard input enters `TWILIGHT` in the password field. |
| 20500–21500 | The game accepts the password and displays the Night Course briefing and city artwork. |
| 22000–22500 | Night Course countdown and driving, changing road position, speed and score. |
| 22550–22625 | F10 pause is displayed. All four sampled complete BMPs are byte-identical while the machine continues executing fields. |
| 22750–23500 | F10 resumes play; acceleration and steering continue, the timer counts down and the score changes. |

The native comparison reaches its opening checkpoint earlier: the reviewed
timer changes from 11 at field 14350 to 43 at 14475. It also times out before
checkpoint two, accepts `TWILIGHT`, enters Night Course play, and pauses/resumes.
Each ROM has its own later steering and menu input trace. Cross-ROM trajectories,
scores, startup phases and pixels are not asserted equal.

The external driver reads rendered BMP road colors and submits brief joystick
holds. It changes no guest state other than physical input. The frozen resulting
events are the replay inputs; the independent qualification does not run the
adaptive driver. The pause evidence is stronger than a single still image:
four full rasters match during pause under each ROM, then change after resume.

## Verification and unsuccessful attempts

Three independent runs start from reset and execute complete recorded input
through 23500 physical fields: CopperStart normal, CopperStart scalar and native
Kickstart 1.3. Inputs and the exit control are written before process creation.
Each retains 941 sampled fields and 4705 BMP/state/Chip RAM/slow RAM/PCM files.
All 4705 CopperStart files match between CPU modes. The normal and native reset
replays also match every filename, size and SHA-256 in their respective final
exploratory runs. All three exit zero with null captured CPU/video unsupported
indicators. Reviewed gameplay establishes the milestones; exit status and those
indicators alone do not establish compatibility.

The [companion JSON](COPPERSTART_LOTUS2_GAMEPLAY_2026-10-24.json) records exact
media, ROM, binary, harness, input, capture-manifest and log identities. The two
committed workload files are the exact UTF-8/LF input bytes used by the final
normal/native runs; scalar uses the normal input unchanged.

The initial coarse-driving attempt times out before checkpoint one under both
ROMs. A 50-field steering attempt crosses checkpoint one but also times out
before checkpoint two. These are driving limits, not established replacement-ROM
faults. The first 25-field attempt stops at field 11900 because its host control
segment places a release event at the excluded endpoint. That rejected segment
does not execute. The corrected script caps the hold one field earlier and the
complete retry is the qualified sequence above. The failed harness attempt is
retained locally and excluded from passing replay counts.

The local harness builds report zero warnings/errors. Production, CPU and
producer source is unchanged. The preceding production/engine/disk and focused
native-ROM test results remain retained evidence; they are not counted as fresh
tests here. Input ordering, exact identities, ROM checksum, cadence-prefix
retention, full capture comparisons, pause/resume and documentation links are
checked for this evidence-only change.

Later checkpoints, natural course completion, a natural Night Course handoff,
later courses, manual gears, multiplayer/link mode, audio fidelity, original
protection, hardware conformance, installed-app admission and host throughput
remain unqualified. Nonzero sampled PCM establishes sample presence only. ROMs,
media, raw captures and temporary diagnostic programs remain local under
`artifacts/copperstart-lotus2-gameplay-2026-10-24/`.
