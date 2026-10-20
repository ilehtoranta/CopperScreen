# Superfrog Magic Woods 1-1 completion — 2026-10-20

CopperStart completes Magic Woods 1-1 on original Superfrog SPS 35 Disk One,
displays **WORLD 1-1 COMPLETE / SUPER BONUS AWARD**, collects the 40000-point
bonus and loads the next stage with score **57100** and a new 60-coin
requirement. A second, uninterrupted replay from reset reproduces that result.
No additional ROM or engine correction was needed for this route.

The run uses the already rebuilt 524288-byte standalone CopperStart ROM,
SHA-256 `83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`,
producer `b78ddc09302cd506687327be1c1434891444cc4a`, and the frozen engine from
`8b1fb4148d5763929e804e577d69b94d656a3768`. That engine contains the
[previous sprite DMA correction](SUPERFROG_CHARACTER_2026-10-20.md).
Neither source nor ROM changed in this follow-up, so a new ROM build and
diagnostic-suite run were unnecessary. Earlier build/test results retain
their original provenance; they are not counted as new tests here.

## Route and observed result

The session replays the earlier physical input from reset through field 12200,
then continues interactively. The inherited last-life attempt ends at Game
Over. Ordinary keyboard input enters the high-score name `CS`; the title and
options return successfully. The retry selects **five lives and Normal** in
the game's own options. This SPS release visibly offers 3/5/7 lives. No level
code, trainer, state restoration, guest-memory modification or host OS shim
is used. Media stays read-only; high-score saving is not exercised.

| Completed field | CopperStart observation |
| ---: | --- |
| 14000–16500 | Game Over, disk access, high-score name entry and title return. |
| 16890 | Options show five lives selected and Normal difficulty. |
| 19500 | New Magic Woods 1-1 attempt; four spare-life icons plus the active character. |
| 19860 | Both opening spike pits cleared; 86 coins still required. |
| 20580 | Hill and spring coin columns collected; 64 still required. |
| 21925–22005 | Hidden cave and spring traversal; 46 still required. |
| 23235–24375 | Spring bed, wings and Lucozade pickups, high wall and coin valley; 17 still required at the far tree. |
| 24985–27585 | Backtracking through the spring-bed coin groups; requirement reduced to two. |
| 28325–28515 | Lower tree-branch coins collected; HUD changes from one required coin to EXIT. |
| 29670–29940 | Spring bed and high wall crossed again; return to the open tree exit. |
| 30010, 30230 | WORLD 1-1 COMPLETE and Super Bonus Award; score 17100, eight credits, COLLECT 40000. |
| 30320 | Bonus collected; score 57100. |
| 31200–32400 | Next-stage terrain is loaded and active, with 60 coins required. No further gameplay input. |

Movement, variable-height jumps, camera scrolling, pickups, springs, damage,
spike deaths and checkpoint retries are observed along this route. Character
blinking and death poses are assessed over successive fields rather than
calling an isolated absent character a rendering defect. Wings are exercised
with explicit fire press/release events, following the
[preserved game instructions](https://www.emuparadise.me/GameBase%20Amiga/Extras/Instructions/S/Superfrog.txt).
Powered weapon kills and complete combat behavior are not qualified.

## Reproducibility and native control

The bounded machine is PAL OCS, 68000 interpreter, MOS 8371 Agnus and MOS 8362
Denise, 512 KiB Chip plus 512 KiB slow RAM, no Fast RAM, RTC disabled and one
read-only DF0 containing the original SPS 35 Disk One IPF. The full raw raster
is 908×313; no display crop or title-specific behavior is applied.

All **2300 capture files per CopperStart mode** match byte-for-byte through
field 32400: 460 full-raster BMPs and corresponding CPU/hardware state, Chip
RAM, slow RAM and PCM. All captured unsupported/video-unsupported flags are
null. Nonzero PCM is present in 431 snapshots; listening and audio fidelity
are unassessed. All three interactive processes exit successfully.

The canonical workload combines the original boot input with the **160 actual
input segments**, totaling **579 physical input events**. It retains the
failed inherited attempt, menu actions, five-life Normal retry and eventual
completion. Fresh normal/scalar/native processes then run that entire workload
from reset through field 32400 without interactive pauses. All **1625 files
per replay** match their corresponding interactive run at every common
capture, and normal/scalar replays match each other. These overlapping replay
captures are repeatability evidence, not additional unique gameplay coverage.
All six processes exit with code zero; no optional replay is substituted.

Native Kickstart 1.3 is a bounded comparison, not a matching completion result.
All sampled gameplay BMPs from fields 19500 through 24320 match CopperStart.
The first later sampled mismatch is at 24375: native terrain is horizontally
offset by about 14 raster pixels after the dense coin/speed-pill section,
with the same score and coin requirement. Later enemy encounters and movement
diverge; native finishes this input at the spring bed with one coin required,
without completing 1-1. Earlier transient differences at 16400, 17800, 17900
and 19100 occur during high-score/menu/loading transitions. Cross-ROM CPU
phase and retained CIA masks already differ before the terrain offset. Its
cause is not isolated; no ROM compatibility defect or timing correction is
inferred from the offset alone. The uninterrupted native replay reproduces
its own captured states, excluding interactive polling as its cause.

## Scope and retained evidence

This qualifies CopperStart's Magic Woods 1-1 completion, bonus collection and
next-stage startup on this ROM/media/machine/input combination. Native-ROM
completion, later-stage gameplay, Disk Two, saving, the full story, general
Workbench and broader sprite comparator/manual-data/stolen-slot timing edges
remain unqualified. This is native-media compatibility evidence, not a host
throughput acceptance measurement or complete hardware conformance proof.

The [evidence record](SUPERFROG_COMPLETION_2026-10-20.json) retains ROM, media,
binary, input, capture-manifest, verification and milestone identities. The
[canonical workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/superfrog-copperstart-magic-woods-completion-v1.json)
contains only physical input. Local captures and probes remain under
`artifacts/superfrog-completion-2026-10-20`; ROMs, game media and build artifacts
are not committed. No packages are published.
