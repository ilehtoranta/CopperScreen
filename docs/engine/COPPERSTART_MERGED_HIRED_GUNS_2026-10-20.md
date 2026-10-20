# CopperStart: merged-ROM Hired Guns combat and completion

2026-10-20. **The freshly compiled merged-source ROM completes single-player
Training zone 3, Fire Fight.** Desverger reaches the exit after hostile combat,
key use, the teleporter and block bridge. CopperStart normal/scalar score 93;
the fresh Kickstart 1.3 control scores 78. Both show the original mission-objective
completion screen. Their original-game starting directions and combat states
are different; cross-ROM bitmap or score identity is not claimed.

This continues the [earlier pinned-ROM completion](COPPERSTART_HIRED_GUNS_COMBAT_2026-10-20.md).
Its old ROM and results remain historical evidence. The [companion JSON](COPPERSTART_MERGED_HIRED_GUNS_2026-10-20.json)
pins this new ROM, fresh compiler inputs, physical events, actual loaded segments,
full capture fingerprints, test results and limitations.

## Fresh producer and corrections

CopperStart producer `e62e3468d94f532da1537db74ab237cd00798456` includes main merge `0e64392`.
The complete main image was compiled again, then linked with the freshly built
native services. No earlier compiled main or ROM was reused. The final
524288-byte ROM has SHA-256 `2259a7ee830b68f2bdf5ba5884728144e2d16d7a083e0a56b99e3341fb508be8` and 476086 compiled code bytes.
An independent invocation of the standalone graphics linker reproduces it exactly.

The merged main initially ended at $FF43EE and collided with the old native
service positions. misc.resource now starts at $FF4400, BCPL at $FF4600 and
Intuition at $FF5000; the boot path uses the new Intuition address. CIA remains
at $FF7B00 and graphics at $FF8000. The final compiled main ends at $FF43BE,
leaving 66 bytes before misc.resource. Private composition checks remain required.

Empty RethinkDisplay/RemakeDisplay calls now load Intuition's empty View when
MrgCop returns MCOP_NOP. They no longer leave an application's previous display
active, and RemakeDisplay does not try to build a nonexistent first screen.
Both emitted-code regression cases fail with the prior emitter and pass with
the correction. The native boot probe now tests the real linked libraries;
[RethinkDisplay has no defined D0 return before V39](https://developer.amigaos3.net/autodocs/intuition.library/RethinkDisplay.html).
[Commodore's view.h](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_3._guide/node05ED.html)
defines MCOP_NOP for a merge without Copper lists.

Trackdisk read and geometry calls no longer reject a caller's buffer because
it overlaps CopperStart's own allocation. A fixed $10000 read now returns the
complete requested payload. The [CMD_READ contract](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node0520.html)
describes the supplied buffer and sector-aligned length/offset; CopperStart's
private allocation placement is not an additional admission rule. Existing
length/offset, geometry and private image checks remain. A duplicate merged
filesystem-test field was also removed.

## Native replay

The frozen runner uses PAL OCS 68000, 512 KiB Chip plus 512 KiB slow RAM,
no Fast RAM, RTC off, one read-only DF0 and the full 908 by 313 raster. Original
Hired Guns v1.08.39.25 disks 1, 2 and 3 are mounted. The real SystemTakeover CLI
and English chooser run in the guest. There is no host OS shim or guest-state
injection.

The merged ROM reaches its CLI later. Early typing filled the host key queue;
the replacement fixture waits for the observed prompt. An initial long route
was stopped after Training directions differed from its assumptions. The final
segmented replay observes original mouse/character fields without advancing the
CPU, then sends ordinary signed mouse deltas, button edges and keys. Different
starting directions are corrected through the game's turn controls.

The replay equips the sniper rifle, picks up the key, defeats the two combat-room
hostiles, uses the wall control and teleporter, pulls/pushes the block into the
gap, crosses the bridge, uses the key automatically, reloads and fights the exit
guard. The first attempt dies in both ROMs. One physical Ctrl-R retry completes;
reloading before the gate, stepping back and firing through the open gate defeats
the guard. The final wall control opens the passage to the exit.

| Checkpoint | CopperStart field | Kickstart 1.3 field |
| --- | ---: | ---: |
| Training entry | 65000 | 45000 |
| Key collected, first attempt | 66600 | 46600 |
| First two hostiles defeated | 67400 | 47400 |
| Teleporter | 68200 | 48200 |
| Block bridge built | 69400 | 49400 |
| In-game retry ready | 74600 | 54600 |
| Exit guard defeated, successful attempt | 82000 | 62000 |
| Exit control reached | 82600 | 62600 |
| Mission objective completed by Desverger | 83200 | 63200 |

All **4165 files per CopperStart mode match byte for byte**: full BMPs, RAM,
hardware state and PCM. There are 383 nonzero-PCM snapshots.
All captured unsupported/video-unsupported flags are null. The three final runs
exit zero. The actual loaded segments equal the saved published revisions and
combined portable fixtures: 1310 CopperStart events and 1308 Kickstart events,
including the retry. The new fixtures are
`hiredguns-copperstart-merged-firefight-v1.json` and
`hiredguns-kickstart13-merged-firefight-v1.json` under the runner's Workloads folder.
They use relative ZIP basenames/ADF entries; place them beside the original disk
2/3 archives and boot disk 1 separately.

A separate fresh long Kickstart replay also completed with score 94; all 6265
of its capture files match its earlier reference. That is a separate input
protocol and is not relabelled as the new route or as CopperStart coverage.

## Validation and bounds

The initial merged Release solution build passes. Final affected projects are
rebuilt after correction. Fresh checks pass **466 cases**: 324 instruction/semantic
cases and 142 exact-ROM boot cases, including the fresh compile. There are no
failures or skips in those runs. The 21 separate standalone-reader-ROM cases
are excluded and provide no fresh coverage here. Compiler/SDK binaries are pinned
local development outputs; no package publication or published-toolchain claim
is made. The old g20 SDK cannot resolve the merged resident API.

This qualifies Fire Fight combat and one participant's level completion. It does
not qualify campaign/full game, saving, other Training levels, full-party survival,
full Workbench, unused disks 4/5, audio listening quality or latency, hardware
conformance, or host throughput. The two standalone native boot-block experiments
that missed their markers are not used as success evidence. ROMs, game media,
binaries and captures remain local.
