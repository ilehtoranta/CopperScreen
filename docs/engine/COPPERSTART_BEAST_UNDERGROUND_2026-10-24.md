# CopperStart Shadow of the Beast: underground continuation — 2026-10-24

The supplied PNA release now has bounded underground coverage with the current
rebuilt CopperStart ROM: opening the tree entrance, loading the staircase
transition, walking through the underground area, descending two ladders,
scrolling between platforms, and encountering the green creatures. Attack,
jump and crouching-attack inputs are exercised; the attempt ends in death and
loads the death artwork. Native Kickstart 1.3 reaches the same milestones.
No additional CopperStart, CPU or engine compatibility defect was established.

This extends the [October 17 opening test](COPPERSTART_BEAST_2026-10-17.md).
It does not qualify the underground area's completion, keys, weapons, boss,
castle, later areas or a complete game. No successful creature kill is claimed.

## Exact ROM and execution boundary

The 524288-byte ROM has SHA-256
`fefe13e86ad00e3609ad4e385b5e3752e2407e1b5a9b67affa904d2160d9e8e7`.
It is the rebuilt image retained by the
[Full Contact continuation](COPPERSTART_FULL_CONTACT_2026-10-24.json), with
native emitter producer `0057fb9c38d82d38122ddf69a4aa960573beedd7` and retained
compiled guest root from `cf7c9b54aefea723b018bd68e10e829e67684250`. This
continuation reuses that image; its checksum and equality to the retained
independent composition are checked again. Neither producer nor runtime source
is rebuilt or changed here. Public API checks are not altered without a
demonstrated incompatibility.

The runtime remains the exact merged CopperScreen
`29bc8635fe33ae9e267846ade511e9b368f7158d` binaries and pinned, unpublished
Copper68k `1.5.5-nitro.1`. The existing functional harness captures every 100
fields. The evidence parent is `9a7f254478fc0ea29523b966eede7ae481f56f45`.
The profile is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
Both supplied PNA ADFs remain unchanged. There is no saved state, trainer
selection, media edit, guest-memory/register injection or host OS shim.

## Reviewed route

The preserved [publisher manual](https://www.lemonamiga.com/doc/shadow-of-the-beast/1426)
describes Up for side entrances, Up/Down for ladders, fire attacks and Escape
restart. A [supporting walkthrough](https://www.lemonamiga.com/doc/shadow-of-the-beast/1427)
identifies the red-arrow tree to the left and the first two ladder directions.
The observed game, rather than the walkthrough, establishes each milestone.

| Fields | Reviewed result under both ROMs |
| ---: | --- |
| 14000–24000 | Retained three-prompt disk sequence reaches the outdoor WELCOME screen. |
| 32000–33900 | Left movement and jumping traverse the forest. The exploratory route passes the entrance instead of entering it. |
| 33900–41000 | Escape ends the attempt; death artwork and fresh Disk 1 then Disk 2 prompts are reached. |
| 44000–47200 | Reloaded title/story screens return to outdoor play through ordinary fire input. |
| 48400–48700 | Short walking adjustments align with the doorway; Up produces `IT OPENS`. |
| 49200–50000 | Staircase artwork/text and stage loading. |
| 50500–51100 | Underground play, attack poses, horizontal movement and scrolling. |
| 51200–51400 | First ladder descent and vertical scrolling to the lower platform. |
| 51500–51800 | Left movement and the second ladder descent reach another lower platform. |
| 51900–52700 | Right movement, attacks, jump/crouching-attack attempts and green-creature encounters. The attempt loses its remaining health. |
| 54000–55000 | The game loads and displays its death artwork. |

The first entrance attempt held Up while moving left, so the character jumped
past the doorway. The successful retry stops on the ground and aligns before
pressing Up. The full traces retain both attempts and the ordinary restart;
there is no state restoration. A P/fire pause attempt at 52700–52920 overlaps
the end of the attempt: the displayed health is zero at 52800–52900 and the
death sequence follows. Identical stills do not establish pause/resume coverage.

Some native-ROM image previews appeared black or partial. Reopening the stored
BMP as RGB PNG showed the complete image. The field-51000 BMPs are byte-identical
under the two ROMs, with SHA-256
`5d3d09a7c229566b407ff6bcbeb918587bf315b7c0d05927c53bde0264a7476f`.
This preview observation did not justify an emulator or ROM change.

## Verification and remaining scope

Three independent runs start from reset and replay complete frozen inputs
through 55000 fields: CopperStart normal, CopperStart scalar and native
Kickstart 1.3. Each retains 551 sampled fields and 2755 BMP/state/Chip RAM/slow
RAM/PCM files. All 2755 CopperStart capture files match between CPU modes.
The normal and native reset replays also match all files of their respective
exploratory runs. All three exit zero with null captured CPU/video unsupported
indicators. The reviewed route, not those indicators alone, establishes play.
Eleven reviewed milestone BMPs from door opening through death artwork are
also byte-identical between the two ROMs. This is a bounded image comparison,
not an assertion of equal cross-ROM CPU or hardware trajectories.

The [companion JSON](COPPERSTART_BEAST_UNDERGROUND_2026-10-24.json) records the
exact ROM, runtime, media, harness, inputs, logs and capture manifests. Committed
workloads contain the exact UTF-8/LF input bytes used by the final runs. The
normal and native traces differ during one outdoor movement segment; scalar
uses the normal trace unchanged. Cross-ROM CPU trajectories are not asserted
equal. This same-engine native control is supporting compatibility evidence,
not independent physical-hardware timing proof.

Producer/runtime source is unchanged. Prior production, engine, disk and
native-ROM tests remain retained evidence and are not counted as fresh suite
passes. This evidence-only change checks input ordering, binary/media identity,
ROM checksum, complete capture comparisons and documentation links. Sampled
nonzero PCM establishes sample presence, not audio fidelity. Original protection,
installed-app admission and host throughput remain unqualified. ROMs, media,
raw captures and temporary tools stay local under
`artifacts/copperstart-beast-underground-2026-10-24/`; no package is published.
