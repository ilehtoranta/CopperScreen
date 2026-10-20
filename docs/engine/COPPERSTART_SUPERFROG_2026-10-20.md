# CopperStart Superfrog qualification — 2026-10-20

Superfrog SPS 35 boots from its original Disk One, reaches the menu and loads
Magic Woods 1-1 with the rebuilt CopperStart ROM. Recorded joystick input
produces movement, jumping, camera scrolling and collection up to score 250.
The spike-pit attempt loses a life and returns to the starting terrain; this
is not a successful crossing or level completion. A fresh Kickstart 1.3 control
reaches the same bounded states with the same input.

A separate reset from the Story Disk plays the opening prince/princess scene
and witch text. Fire skips the rest of the film to **Insert disk 1 and press
fire**. Physical ejection, insertion of Disk One and fire then load the game's
features screen and credits under both ROMs. Disk Two is supplied and hashed,
but is not mounted in either replay.

## ROM and engine selection

The unchanged 524288-byte ROM has SHA-256
`83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`,
producer `b78ddc09302cd506687327be1c1434891444cc4a`. The preceding
[Xenon 2 qualification](COPPERSTART_XENON2_2026-10-20.md) records its fresh
compilation, independent relink and component/full-ROM checks. Those checks
were not rerun for this unchanged image. No CopperStart source correction or
new ROM build was needed here.

Initial preflights reused the older Hired Guns probe and reproduced a repeated
bottom viewport on **both** ROMs. That engine predates the existing
[LWA-VIDEO-010 correction](SUPERFROG_VIEWPORT_2026-10-16.md): the Copper's nominal
horizontal position must wrap at the line boundary before WAIT/SKIP comparison.
Those preliminary runs are retained as diagnostic evidence and excluded from
the final visual qualification.

The final six runs restart from reset using that record's retained corrected
candidate engine, SHA-256
`c7b6c5edb7c2f20cbe036a5ea877c3390d992743afd0ae9cbb92d1320188848c`,
CopperDisk `d12859a172db1d6eae9d6b35a42fc01fd20525afdd368b482dd67fbca337317f`
and pinned Copper68k `f445a7d289cf6545c46731a0d94e59e7c8c56b189a0c43768f4009874c0cb6f2`.
The live-control probe was rebuilt against those exact binaries; its Release
build passes with zero warnings/errors. The corrected full-raster captures
have continuous terrain below scanline 255. No engine/CPU source change,
cropping, title recognition, host OS shim or guest-memory injection is used.

## Verification and limits

| Original media replay | Fields per mode | Matching normal/scalar capture files |
| --- | ---: | ---: |
| Disk One startup and bounded Magic Woods 1-1 input | 15200 | 765 |
| Story opening, skipped remainder, physical Disk One handoff | 13000 | 655 |

All 1420 files per CopperStart mode match byte-for-byte: full 908×313 BMPs,
CPU/hardware JSON, Chip RAM, slow RAM and PCM snapshots. Captured unsupported
and video-unsupported flags are null. Nonzero PCM occurs in 124 gameplay and
97 story/handoff snapshots per CopperStart mode; listening and fidelity are
unassessed. All six final processes exit successfully. Native controls reach
the same bounded milestones, but timing/state differences mean cross-ROM
byte equality is not claimed.

Two additional reset-based diagnostics capture adjacent fields 14500–14504.
The player character is not clearly visible in either ROM's images; those
fields do not distinguish animation/visibility state from a shared rendering
limitation. Character appearance remains unresolved and is not qualified by
the score/camera/input observations above. These diagnostics are separate
from the 1420-file normal/scalar comparison.

The machine remains PAL OCS 68000, 512 KiB Chip plus 512 KiB slow RAM, no Fast
RAM, one read-only DF0 and RTC disabled. This is compatibility coverage, not a
throughput measurement or full hardware-conformance result. Combat/weapons,
level completion, Disk Two, other worlds, saving, the complete story and full
Workbench remain unqualified. No new defensive caller checks were added.

[The evidence record](COPPERSTART_SUPERFROG_2026-10-20.json) preserves media,
ROM, binary, probe-source, actual input-segment and capture-manifest identities.
The physical workloads are
[gameplay](../../CopperMod.Amiga.Lightweight.Runner/Workloads/superfrog-copperstart-gameplay-v1.json)
and [story handoff](../../CopperMod.Amiga.Lightweight.Runner/Workloads/superfrog-copperstart-story-handoff-v1.json).
Diagnostics and captures remain locally under
`artifacts/copperstart-superfrog-2026-10-20`. No ROMs, game media, build outputs
or credentials are committed.
