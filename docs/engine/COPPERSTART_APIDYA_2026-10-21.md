# CopperStart Apidya opening combat — 2026-10-21

The rebuilt CopperStart ROM boots the supplied original Apidya SPS 764 Disk
One into illustrated story scenes, the title and options, and the opening
meadow stage. The bounded Normal/five-life run exercises movement, shooting,
enemy hits, life loss, Game Over and Continue, then resumes playable combat.
No new compatibility defect requiring a ROM or engine correction was isolated.
Stage completion and later-disk loading are not qualified.

The unchanged 524288-byte ROM has SHA-256
`83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`,
producer `b78ddc09302cd506687327be1c1434891444cc4a`. The frozen engine is the
one qualified with the [Superfrog sprite DMA correction](SUPERFROG_CHARACTER_2026-10-20.md),
SHA-256 `9dfa51ccc81f9407fbb757b27967b1b2de4aa18fcba5a16d5ad0b8db2a97bcc2`.
The pinned Copper68k and CopperDisk binaries are unchanged. No argument checks,
title-specific behavior, guest-memory patches or host OS shims are added.

## Reset-based qualification

The profile is PAL OCS, 68000 interpreter, MOS 8371 Agnus and MOS 8362 Denise,
512 KiB Chip plus 512 KiB slow RAM, no Fast RAM, RTC disabled and one read-only
DF0. Only `Apidya_Disk1.ipf` from the supplied two-entry ZIP is mounted during
the qualified run. Captures retain the full 908×313 raster.

The final input first presses fire during the story to return to the title,
then presses again while the title is present. The title otherwise returns
to its attract story. An early exploratory run repeatedly missed that brief
title interval; read-only inspection of the guest handler at `$C16–$CC8`
identified the return-to-title versus start-game branch. This was an input
timing mistake, not an API guard or compatibility fix. The final run starts
again from reset with the corrected physical input sequence.

The options screenshot explicitly shows **Normal**, **five lives**, team mode
**No**, extra selection **Auto** and **Joystick** control. Defaults are left
unchanged; Start is selected. No trainer or level code is used. The
[preserved manual](https://www.lemonamiga.com/doc/apidya/82) describes ordinary
joystick control, fire presses and longer hold/release shots. Both shorter
press sequences and longer holds are exercised here; fully charged-weapon
timing and power-up selection are not separately certified.

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 5000 | Illustrated-story text before the return-to-title input. |
| 5100 | Apidya title with PUSH START BUTTON. |
| 5200 | Normal/five-life options and Start selection. |
| 5600 | Black game-loading transition; not counted as gameplay. |
| 6500 | Meadow, player-ready sequence, visible shot and score 60. |
| 6700 | Enemy-hit score reaches 180. |
| 6820 | Vertical input has moved the player near the bottom of the full raster; shot-charge glow is visible. |
| 7230 | Continued meadow combat and score 300. |
| 7640–8000 | Life loss, transition effect and GAME OVER PLAYER ONE; score 480. |
| 8600 | CONTINUE YES NO, three credits, Yes selected. |
| 8800 | Accepted continue enters a loading transition. |
| 9100–9180 | Meadow restarts with reset score, renewed lives, moving enemies and visible projectiles; CopperStart ends at score 40. |

Normal and scalar modes match **all 480 capture files per mode** through field
9180: 96 full-raster BMPs plus the corresponding selected CPU/hardware state,
Chip RAM, slow RAM and PCM. Every captured unsupported/video-unsupported flag
is null. Nonzero PCM occurs in 73 snapshots; listening and audio fidelity are
unassessed. Both processes and the native control exit with code zero.

The canonical workload combines the initial boot input and all **11 actual
interactive segments**, totaling **270 physical joystick events**. Fresh
normal/scalar/native processes then replay the entire workload from reset
without interactive pauses. All **465 files per reset replay** match that
ROM/mode's interactive run at every common capture. Normal and scalar reset
replays also match each other. These overlapping captures verify repeatability,
not additional unique gameplay. All six qualification/repeat processes exit
with code zero; no optional replay is substituted.

## Native control and limits

Native Kickstart 1.3 reaches the same title/options, meadow combat, life loss,
Game Over, three-credit Continue prompt and restarted gameplay. It is not an
exact cross-ROM timing or pixel comparison: the first sampled BMP difference
after field 5500 occurs at 6100. Enemy/shot positions and later outcomes differ;
native reaches Game Over at score 460 and ends continued gameplay at score 60,
versus CopperStart's 480 and 40. Both use identical physical input. The native
reset replay reproduces its own states. The difference is disclosed without
inferring a firmware or chipset correction from it alone.

The earlier exploratory run's story/title cycles and first meadow attempt are
retained as diagnostics, separate from the final reset qualification. A
separate cold boot with Disk Two leaves CopperStart waiting without a game
display and native Kickstart at its insert-disk screen; this is not a successful
Disk Two replay. No disk swap is needed to reach the qualified opening stage.
The initially timed-out host wait for field 24500 did not alter emulation:
all three exploratory processes subsequently reached that same paused field.

Power-up collection/selection, full stage or boss completion, later stages,
Disk Two handoff, saving, simultaneous players, the full story and general
Workbench remain unqualified. No ROM rebuild, production rebuild or diagnostic
suite was rerun for this evidence-only continuation; the earlier build/test
record retains its original provenance. This is native-media compatibility
coverage, not throughput acceptance or complete chipset conformance proof.

The [evidence record](COPPERSTART_APIDYA_2026-10-21.json) preserves ROM/media,
source/binary, input, actual-segment, capture-manifest, verification and
milestone identities. The [canonical input](../../CopperMod.Amiga.Lightweight.Runner/Workloads/apidya-copperstart-meadow-combat-v1.json)
contains only physical joystick input. Local captures and probes remain under
`artifacts/copperstart-apidya-2026-10-21`; ROMs, media, local build artifacts and
the diagnostic disassembler dependency are not committed. No package is
published.
