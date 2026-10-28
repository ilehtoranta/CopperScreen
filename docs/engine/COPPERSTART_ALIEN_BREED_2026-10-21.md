# CopperStart Alien Breed first-deck completion — 2026-10-21

The rebuilt CopperStart ROM boots original Alien Breed SPS 998, accepts the
Game Disk One-to-Two swap, starts a one-player game and completes the first
deck through its marked lift. **MISSION BRIEFING LEVEL 2** and subsequent
second-deck gameplay are captured under both CopperStart and native Kickstart
1.3. Escape abort, high-score name entry, menu return and a fresh Level 1
briefing also work. No new compatibility defect requiring a ROM or engine
correction was isolated on this route.

The unchanged standalone ROM is 524288 bytes, SHA-256
`83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`,
producer `b78ddc09302cd506687327be1c1434891444cc4a`. The frozen Lightweight
engine is the [previous sprite DMA correction](SUPERFROG_CHARACTER_2026-10-20.md),
SHA-256 `9dfa51ccc81f9407fbb757b27967b1b2de4aa18fcba5a16d5ad0b8db2a97bcc2`.
Copper68k and CopperDisk are unchanged. No public-argument checks are added or
removed without a demonstrated compatibility requirement. No host OS shim,
guest-memory injection, trainer, level code or title-specific workaround is
used.

## Original-media route

The bounded profile is PAL OCS, 68000 interpreter, MOS 8371 Agnus and MOS 8362
Denise, 512 KiB Chip plus 512 KiB slow RAM, no Fast RAM, RTC disabled and one
read-only DF0. The full raw raster is 908×313. Both game IPFs come from the
supplied `AlienBreed_998.zip`; the separately supplied Story Disk is not run.

The reset begins with `AlienBreed_GameDisk1.ipf`. At field 5001 the game shows
DISK REQUEST / ALIEN BREED 2. Physical ejection at 5003 and insertion of
`AlienBreed_GameDisk2.ipf` at 5053 satisfy the request. A later fire press is
included before the title/menu sequence. All media operations are ordinary
drive/input actions, not loader interception or track rewriting.

One Player Game and Share Credits Off are retained. Three Down presses select
Start Game. From the landing bay, the route aligns with the west passage,
opens doors using the supplied keys, crosses the corridors while firing at
approaching aliens, and follows the DECK LIFT marking. The lift loads the
Level 2 briefing and a different deck with checkerboard flooring and ZONE 1
markings. Movement, firing and further door interaction are exercised there.

The [preserved Team17 manual](https://www.emuparadise.me/GameBase%20Amiga/Extras/Instructions/A/Alien%20Breed.txt)
documents joystick movement/fire and Escape to leave gameplay. Escape reaches
the top-ten name prompt after the second-deck input. Horizontal joystick input
selects characters and fire confirms them; confirmation to the end of the
entry returns to the menu. The next menu command starts a new Level 1 briefing.
High-score-table viewing and persistent high-score saving are not qualified.

| Completed field | Reviewed observation |
| ---: | --- |
| 5001 | Game Disk Two request, matching across all three modes/ROMs. |
| 11000 | Animated title after the disk swap and fire input. |
| 11200 | One-player menu; Share Credits Off. |
| 12500–14000 | First-deck landing bay and visible player/HUD. |
| 14100–14360 | Vertical/horizontal scrolling and passage/door traversal; key supply decreases. |
| 14450–14700 | Visible shots, approaching aliens and reduced energy while crossing toward DECK LIFT. |
| 14820 | Lift/loading transition; not counted as a gameplay image. |
| 15200–15600 | Explicit MISSION BRIEFING LEVEL 2. |
| 16800 | Second deck: player at the blue lift, ZONE 1 and different floor/wall layout. |
| 16900–17000 | Second-deck movement, shots and door interaction. |
| 19300 | PLAYER 1 ENTERED TOP TEN LIST: ENTER NAME after Escape. |
| 20050 | Joystick character selection visibly changes the current character. |
| 20600 | Main menu after name confirmation. |
| 21200 | New MISSION BRIEFING LEVEL 1 after the subsequent menu command. |

The route does not demonstrate a complete combat system or certify every
alien kill; its evidence is visible shots, alien encounters, health loss and
successful progress to the next deck. Level 2's core-computer objective is not
completed, and no Intex shop or map purchase is exercised.

## Repeatability and native comparison

All **1115 capture files per CopperStart mode** match byte-for-byte through
field 21200: 223 full-raster BMPs and their corresponding selected CPU/hardware
state, Chip RAM, slow RAM and PCM. All captured unsupported/video-unsupported
flags are null. Nonzero PCM occurs in 127 snapshots; listening and audio
fidelity are unassessed. Normal, scalar and native interactive processes exit
with code zero.

The canonical workload retains all **32 actual input segments**, totaling
**76 physical joystick/keyboard/media events**, including the ejection and
IPF insertion. Fresh normal/scalar/native processes replay the complete
sequence from reset without interactive pauses. All **1065 files per reset
replay** match that ROM/mode's interactive run at every common capture, and
normal/scalar replays match each other. These overlapping snapshots establish
repeatability rather than additional unique gameplay coverage. All six
qualification/repeat processes exit with code zero; no optional replay is
substituted.

Native Kickstart 1.3 reaches the same disk request, title/menu, first-deck lift,
Level 2 briefing/gameplay, high-score entry, returned menu and new Level 1
briefing. Exact cross-ROM CPU state, pixel timing and combat equivalence are
not claimed. The first sampled gameplay BMP difference after field 14000 is
at 14400 during the alien encounters; later enemy and animation/input phases
can differ. The Level 2 briefing is independently visible under both ROMs.
Each ROM's uninterrupted replay reproduces its own captured states; no firmware
or timing correction is inferred from cross-ROM phase differences alone.

## Scope and retained evidence

This qualifies the supplied game disks' boot/handoff, first-deck completion,
bounded second-deck play, Escape/high-score/menu flow and game restart on this
specific configuration and input. Later-deck completion, Intex transactions,
additional weapons, persistent saving, two-player mode, the Story Disk and
general Workbench remain unqualified. Broader sprite/manual-data/stolen-slot
timing edges retain their existing limits. This is native-media compatibility
coverage, not throughput acceptance or complete chipset conformance proof.

No ROM/engine source changed, so no new ROM or production build or diagnostic
suite was needed. Earlier build/test results retain their original provenance
and are not counted as fresh checks. JSON, workload history, binary/media
identities, capture parity and documentation links are checked for this
evidence-only follow-up.

The [evidence record](COPPERSTART_ALIEN_BREED_2026-10-21.json) retains source,
ROM/media, binary, input, actual-segment, capture-manifest, verification and
milestone identities. The [canonical workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/alienbreed-copperstart-first-deck-v1.json)
contains only ordinary physical input and disk insertion/ejection. Local
captures/probes remain under `artifacts/copperstart-alienbreed-2026-10-21`.
No ROMs, game media or build artifacts are committed; no package is published.
