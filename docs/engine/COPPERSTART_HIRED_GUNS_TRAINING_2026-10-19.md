# CopperStart: Hired Guns Training gameplay

2026-10-19. **Single-player Exploration reaches actual Training gameplay.**
Four-character selection, original disk changes, four-view level entry,
an east-to-north turn and a forward step pass with the rebuilt CopperStart ROM.
Fresh Kickstart 1.3 controls produce identical full-raster bitmaps after both
gameplay actions. No new compatibility defect was reproduced, so no additional
implementation change was required.

This continues the [graphics startup qualification](COPPERSTART_HIRED_GUNS_GRAPHICS_2026-10-19.md).
The [companion JSON](COPPERSTART_HIRED_GUNS_TRAINING_2026-10-19.json) records exact
ROM/media/binary identities, source, inputs, observations, capture hashes and
limits. Source changes for this continuation are compatibility documentation;
the two new physical-input fixtures preserve the tested path.

The later [Fire Fight continuation](COPPERSTART_HIRED_GUNS_COMBAT_2026-10-20.md)
qualifies combat and Training level completion with the same pinned ROM.
The bounded Exploration evidence and limits below retain their original scope.

## Machine and unchanged ROM

All three runs cold-reset the same PAL OCS 68000 machine: 512 KiB Chip RAM plus
512 KiB slow RAM, no Fast RAM, one read-only DF0, RTC disabled and the full
908 by 313 raster. The original supplied Hired Guns v1.08.39.25 media is used.
Disks **1, 2 and 3** are mounted in order; the five-disk inventory does not
qualify disks 4 or 5.

The ROM remains the preceding 524288-byte composition, SHA-256
`94ebb5161e593cfcc40e5de18780101e3c9cc5c70e0a467381b9ac3f111eaca5`,
from producer `23faf28236ef6f2987956207e93f849b89779bfe`. Its native AddGList,
Intuition, CIA and graphics implementations are unchanged. The previous
production build and 110-case exact-ROM suite retain their original evidence;
they are not fresh tests for this continuation. No new ROM compilation,
emulator build, host shim or public-argument check change is claimed.

The fresh reference is original Kickstart 1.3. Physical Ctrl-D interrupts its
startup, then the same original SystemTakeover command and language options
are typed. Both ROMs accept the warning and English chooser. CopperStart's
first 1405 captures through field 28000 are byte-identical to the preceding
verified replay, before the new single-player input extends it.

## Observed progression

| Observation | CopperStart field | Kickstart field |
| --- | ---: | ---: |
| Training Exploration selection | 30000 | 24000 |
| Original character-selection screen | 34000 | 28000 |
| Four-character team complete | 36000 | 30000 |
| Disk 1 data loading | 37000 | 31000 |
| Request original disk 2 | 40000 | 34000 |
| Request original disk 3 | 45000 | 39000 |
| Four-view Exploration level and animated party | 51000 | 45000 |
| First character turns east to north | 51500 | 45500 |
| First character steps forward | 52000 | 46000 |

The selected party uses the first four characters, chosen with Enter and Right,
then Enter to finish. DF0 is physically ejected/mounted at each game prompt;
Space resumes the loader. Original disk 3's prompt matches the native control
exactly. The game owns its exception vectors and clears SysBase after takeover.

Read-only inspection of the original game's mouse-input code identifies its
H/V words and signed JOY0DAT deltas. Physical mouse movement targets H/V
`145,95` for a left-turn click and `207,75` for a forward-step click. The initial
mouse positions differ between the ROMs, so their first relative movement is
adjusted to reach the same target. Normal/scalar inputs remain identical.
There is no guest-memory injection. The compass changes from E to N, the first
view rotates, and the forward action changes its scene geometry.

The full BMPs after turning match between ROMs, SHA-256
`2b53cf666205150fbb4ba5370d206c1bcda120a01e1d41ac126957c13a671f83`.
After stepping they match again, SHA-256
`6322049055504426ec1b495518c4745926cc00881ffa406607b0aa02da3f83a1`.
This qualifies the stated observations, not cross-ROM RAM or PCM identity.

## Replay fixtures and verification

The [CopperStart input](../../CopperMod.Amiga.Lightweight.Runner/Workloads/hiredguns-copperstart-training-v1.json)
has 663 ordered physical events and ends at field 52000. The separate
[Kickstart CLI control input](../../CopperMod.Amiga.Lightweight.Runner/Workloads/hiredguns-kickstart13-training-cli-v1.json)
has 662 events and ends at 46000. For replay, copy the input JSON beside the
supplied original disk 2/3 ZIPs, boot original disk 1 separately and use the
ROM/machine/binary identities recorded here. Published diskPath selectors use
ZIP basenames relative to the input file. Only path spelling changes from the
executed absolute selectors; ADF entry names and physical events are preserved.
The ledger retains the actual loaded segments and their hashes.

Both CopperStart modes stop cleanly at 52000 fields with exit zero. All
**2605 capture files per mode** match byte for byte: BMP, CPU/hardware state,
Chip RAM, slow RAM and stereo PCM. The native control stops cleanly at 46000,
exit zero, with 2305 capture files. All captured unsupported-hardware/video
fields are null. CopperStart has nonzero audio in 227 snapshots, including
level entry and both actions. PCM samples are captured, not a fresh live
listening or audio-quality qualification. These are correctness diagnostics,
not throughput acceptance.

Coverage is bounded Exploration entry and movement. Combat, throwing,
inventory, saving, level completion, campaign and other training levels remain
unqualified. Full Workbench and workbench.task remain unavailable; this uses
the original utility's genuine CLI path. Existing modal-window/display bounds
and the separate CIA Kickstart 3.1 removal-mask discrepancy remain unchanged.
ROMs, media, binaries, captures and extracted game-code analysis stay local.
