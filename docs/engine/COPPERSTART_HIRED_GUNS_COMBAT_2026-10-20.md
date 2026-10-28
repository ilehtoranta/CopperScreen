# CopperStart: Hired Guns Fire Fight combat and completion

2026-10-20; replay started 2026-10-19. **Single-player Training zone 3
(Fire Fight) passes hostile combat and successful level completion.** The
original game displays “Mission objective completed” with Desverger as the
winner, score 94. Original Kickstart 1.3 produces the identical full-raster
success bitmap. No new compatibility defect was reproduced, so the previously
rebuilt CopperStart ROM remains unchanged.

This continues the [Exploration gameplay qualification](COPPERSTART_HIRED_GUNS_TRAINING_2026-10-19.md).
The [companion JSON](COPPERSTART_HIRED_GUNS_COMBAT_2026-10-20.json) pins the ROM,
producer, media, frozen binaries, actual loaded segments, capture fingerprints,
observations and limits. Only documentation and physical-input fixtures change.

## Machine and exact ROM

All three cold boots use the same PAL OCS 68000 profile: 512 KiB Chip RAM plus
512 KiB slow RAM, no Fast RAM, one read-only DF0, RTC disabled and the full
908 by 313 raster. Original supplied Hired Guns v1.08.39.25 disks **1, 2 and 3**
are mounted in order. The five-disk identity inventory does not qualify disks
4 or 5. The original SystemTakeover CLI and English language chooser are used;
there is no host OS shim, executable patch or guest RAM/register injection.

The 524288-byte ROM has SHA-256
`94ebb5161e593cfcc40e5de18780101e3c9cc5c70e0a467381b9ac3f111eaca5`,
from producer `23faf28236ef6f2987956207e93f849b89779bfe`. Its prior production
build and 110-case exact-ROM suite retain their original evidence; no new build
or component/native-unit run is claimed here. The source branch separately
merged main as `0e64392e7186e378d75abf50ac83bf1190add4d2` during the replay.
This qualification applies to the pinned ROM, not a ROM rebuilt from that merge.

## Observed progression

The physical F3 selection enters Fire Fight. The replay covers equipment
selection, reloading, hostile attacks and kills, friendly fire, death and the
original game-over screen. Four physical Ctrl-R retries are preserved. Retrying
can change initial facing; read-only observations guide subsequent physical
mouse input. Route exploration and failed player actions are retained in the
fixture, not reported as CopperStart failures or removed from the executed path.

The required key is picked up in the combat room and used automatically at the
locked gate. This behavior agrees with the original [keys/access-wafers instructions](https://www.mocagh.org/miscgame/hiredguns-manual.pdf)
and reproduces with Kickstart. The replay also exercises the teleporter,
block pulling/pushing, gap crossing, the exit-room fight, exit control and exit
point. The successful attempt ends with one survivor; full-party survival is
not qualified. The original Training briefing states that the first participant
to reach the exit wins.

| Checkpoint | CopperStart field | Kickstart field |
| --- | ---: | ---: |
| Inventory selection | 51300 | 45300 |
| Hostile attack | 67200 | 61200 |
| Defeated hostile | 67800 | 61800 |
| Matching game-over screen during exploration | 118800 | 112800 |
| Final-attempt key pickup | 123700 | 117700 |
| Armed combat continuation | 124400 | 118400 |
| Exit-room reloading and combat | 128800 | 122800 |
| Exit guard defeated | 129800 | 123800 |
| Exit control reached | 130500 | 124500 |
| Mission objective completed: Desverger, 94 | 131200 | 125200 |

The final full BMP matches across ROMs, SHA-256
`74aa6fb1abf2a9a531566c90ece3f256a222f6804d1f83cd9c6438edc8b46105`.
Other explicitly verified full-bitmap controls are listed in the JSON. Cross-ROM
RAM or PCM identity is not claimed.

## Replay inputs and verification

The [CopperStart input](../../CopperMod.Amiga.Lightweight.Runner/Workloads/hiredguns-copperstart-firefight-v1.json)
contains **2837** ordered physical events and ends at field **131200**. The
[Kickstart CLI control](../../CopperMod.Amiga.Lightweight.Runner/Workloads/hiredguns-kickstart13-firefight-cli-v1.json)
has **2838** events and ends at **125200**. The ledger retains all 60 actually
loaded control segments per run and their identities. Portable fixtures use ZIP
basenames relative to the input JSON; place them beside the original disk 2/3
ZIPs and boot original disk 1 separately. Only path spelling changes from the
executed absolute selectors. Use the frozen harness and machine/binary pins in
the JSON. These complete inputs preserve exploration and the four retries.

Both CopperStart modes stop cleanly with exit zero. All **6565 capture files per
mode** match byte for byte: full BMPs, CPU/hardware state, Chip RAM, slow RAM and
stereo PCM. Their canonical capture-table SHA-256 is
`8e08538bb34f5d0f13d09a6553c790cbd30385aa78821b8837072d76957b3ac3`.
The Kickstart run stops with exit zero and **6265** capture files. Captured
unsupported-hardware/video fields are null. CopperStart has nonzero PCM in
**991** snapshots, including combat. This is captured audio evidence, not a new
live-listening, audio-quality or latency qualification.

The unchanged prefixes also match the previous Exploration replay: 1505 files
through CopperStart field 30000 and 1205 files through Kickstart field 24000.
These are correctness diagnostics, not hardware-conformance or host-throughput
acceptance measurements.

Coverage remains bounded to this single-player Fire Fight level and the stated
interactions. Campaign/full-game completion, other Training levels, saving,
later disks and complete-party survival remain unqualified. Full Workbench and
workbench.task remain unavailable. Existing display/modal-window bounds and the
separate CIA Kickstart 3.1 removal-mask discrepancy are unchanged. ROMs, media,
binaries, captures and extracted game-code/source analysis remain local.
