# CopperStart: Hired Guns graphics startup and native gadget lists

Follow-up: [verified Exploration Training gameplay](COPPERSTART_HIRED_GUNS_TRAINING_2026-10-19.md).

2026-10-19. **Hired Guns passes its original warning and language chooser,
draws a complete main menu and responds to F1 through New Game and Training's
player-count menu.** Its main-menu image matches the retained Kickstart 1.3 fingerprint
exactly. This continues the [positioned window qualification](COPPERSTART_HIRED_GUNS_WINDOWS_2026-10-19.md).
The [companion JSON](COPPERSTART_HIRED_GUNS_GRAPHICS_2026-10-19.json) records
source, ROMs, build, tests, physical input, media, binaries, captures and limits.

## First divergence and correction

The preceding CLI command omitted the original icon's
`LANGUAGES=ENGLISH,FRENCH,GERMAN,ITALIAN,SPANISH` setting. Its incomplete menu also
reproduces under Kickstart; the coloured display before Space is the game's
animated introduction. These observations did not establish a chipset fault.
A fresh Kickstart 1.3 CLI control, interrupted with physical Ctrl-D and supplied
the original language options, accepts the warning and English chooser and
reaches a complete main menu at field 20000.

With those options, the previous CopperStart ROM fails opening the chooser.
Read-only scalar instruction inspection finds the first missing call at field
12191: `intuition.library` vector -438, `AddGList`, enters its distinct ILLEGAL
instruction at `$FF69B0`. The utility appends its linked selection gadgets with
position/count WORDs both `$FFFF`, then calls RefreshGList separately.

Native AddGList now splices caller chains into Window.FirstGadget or
Requester.ReqGadget according to GTYP_REQGADGET. It interprets the public WORD
arguments, returns the actual ordinal, appends at position `$FFFF`, and accepts
count -1 for the complete null-terminated chain. A finite insertion replaces
its last NextGadget with the old destination link even if that caller link was
nonzero. Guest Forbid/Permit protects the transaction from the input task.
Rendering remains a separate RefreshGList operation. These are the
[documented semantics](https://developer.amigaos3.net/autodocs/intuition.library/AddGList.html).
No caller allocation marker, zero-link admission or traversal budget is added.
No hardware engine change, host OS shim, media alteration or title-specific
behavior is used.

## Rebuilt ROM and controls

The 524288-byte ROM has SHA-256
`94ebb5161e593cfcc40e5de18780101e3c9cc5c70e0a467381b9ac3f111eaca5`.
Intuition is 10872 bytes at `$FF4000`; graphics remains 32300 bytes at `$FF8000`,
with CIA at `$FF7B00`. This composition reuses the unchanged, fresh unlinked
compiled main from the CIA continuation (467130 code bytes); no fresh main
compilation is claimed. The full CopperStart Release build succeeds with zero
warnings/errors using the recorded existing-reference flags. Production and
isolated emitters produce identical ROM bytes.

Two reset probes pass independently with Kickstart 1.3 and CopperStart. They
test window/requester heads, partial insertion from existing chains, link
severing, actual ordinals, append/full-chain forms and WORD arguments with
nonzero high register bits. Both fail with the previous ROM's missing vector.
All **110 exact-ROM cases pass, zero failures/skips**. The 14 optional standalone
polled-reader cases are excluded and are unavailable coverage. Initial draft
probe failures caused by an absent command name are retained separately and
are not qualification.

## Original-media replay

Both modes cold-reset the same PAL OCS 68000 machine: 512 KiB Chip plus 512 KiB
slow RAM, one read-only DF0, no Fast RAM and RTC disabled. Only original disk 1
is mounted; the five-media inventory does not imply five-disk coverage. The
physical CLI command is:

```text
c:systemtakeover CHIP 524032 EXCHIP 492500 ANY 524288 TEMP 153000 RAMDISK 170000 RELOCATE UNPACK KILLSYS LANGUAGES ENGLISH,FRENCH,GERMAN,ITALIAN,SPANISH CODE "Hired Guns" DATA "c:SystemTakeover.dat"
```

Typing begins at field 2100. The warning is visible at 12000 and the Accept
click ends at 12090. At 14000 the chooser renders all five choices with English
selected. Continue is clicked through 14066. The original loader then owns
the vectors and clears SysBase. Space at 22100 leaves the introduction; field
23000 shows the complete main menu. F1 at 24100 opens New Game and F1 at 26100
opens Training's player-count menu. No guest-memory writes launch or steer the game.

The menu BMP is SHA-256
`ef2b4c42053e2a0ad965ac3c4159fca43596f26be38208e5fd0407af6fdb735b`,
identical to the earlier verified native menu capture. Both replay processes
stop cleanly at **28000 fields**, exit zero. All **1405 capture files per mode**
match byte for byte: BMP, CPU/hardware snapshots, Chip RAM, slow RAM and PCM.
All captured unsupported-hardware/video fields are null. The loaded physical
inputs match between modes; the ledger uses actual loaded segments rather than
overwritten controls. The earlier command without LANGUAGES was also extended
through 40000 fields: 2005 captures per mode match, while its incomplete menu
remains a failure.

These are correctness diagnostics, not throughput acceptance. Unit fixtures
and media replays retain separately pinned frozen engine/CPU binaries; no new
CopperScreen emulator build is claimed. Training-level gameplay, completion
and disk transitions remain unqualified. Full Workbench and workbench.task
remain unavailable; this is the original utility's genuine CLI path. Existing
modal-window/display bounds remain, and requester list insertion does not
qualify general requester display or RemoveGList. The earlier CIA Kickstart 3.1
removal-mask discrepancy remains unresolved. No ROM, media, binary or capture
is committed.
