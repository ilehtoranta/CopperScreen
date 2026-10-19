# CopperStart: Hired Guns startup comparison

2026-10-19. **CopperStart fails before game entry.** Kickstart 1.3 reaches the
input-driven main menu from the same original disk. This is the next title after
the verified [Railroad Tycoon route and arrival](COPPERSTART_RAILROAD_ROUTE_2026-10-19.md).
The [companion JSON](COPPERSTART_HIRED_GUNS_2026-10-19.json) records exact inputs,
ROMs, media, binaries, trace sources and capture identities.

## First failure

Disk 1's original startup sequence runs two Echo commands, then LoadWB and
EndCLI. Its C:Echo is an old BCPL executable. CopperStart loads it and enters
its first instruction through the C RunCommand startup ABI. At field 687,
PC `$C2D614` executes `movea.l $164(a2),a4`, with A2 `$C297FD` instead of a
BCPL global-vector base. The address error enters the ROM exception handler at
`$FBDED8`; later repeated exceptions corrupt guest state. The trace's 96-byte
entry prefix matches the original disk Echo code. No game code has run.

This identifies missing native BCPL command startup/runtime, rather than a
defensive rejection of valid caller data. The current boot command bridge calls
RunCommand with the C entry contract. Its existing internal Path command even
documents the distinction from older disk BCPL commands. Support for custom
BCPL handler binding elsewhere does not establish a working system BCPL CLI
runtime.

Implementing and qualifying that native runtime remains open. LoadWB/Workbench
and later Hired Guns requirements need separate testing after this first fault
is fixed. They have not been reached under CopperStart. No guest or engine fix,
ROM rebuild, successful placeholder, disk alteration, forced entry point or host
OS shim was made for this diagnostic.

## Native control and reproducibility

All runs cold-start the bounded PAL OCS 68000 machine with 512 KiB Chip plus
512 KiB slow RAM, one read-only DF0 and RTC disabled. Disk 1 is the original
Hired Guns v1.08.39.25 ADF selected from its supplied ZIP; all five supplied
archives and ADF entries are hashed in the JSON. Only disk 1 is mounted in these
runs. Media and ROM binaries are not committed.

The exact previously rebuilt CopperStart ROM is SHA-256
`334cd0e9d2ad25410ab449f79e42e2b254e8085dbbf94e2d89c86f4db8b24d13`,
from source `1b8313985a9e8f95165e9cd973cd231af64e8a39`. Normal and scalar
execution both stop at field 6000. All **305 capture files per mode** match
byte for byte: BMP, CPU/hardware state, Chip RAM, slow RAM and PCM. This proves
the failure reproduces; it is not a successful boot or gameplay qualification.

Kickstart 1.3 shows the Workbench disk icon at field 2000. At field 6000 the
retained physical-input menu script is replayed with every event shifted by
4000 fields: open the disk and game icons, accept SystemTakeover, then Space.
Field 13000 shows MAIN MENU with F1 new game, F2 saved game and F3 Workbench.
Its BMP SHA-256 is
`ef2b4c42053e2a0ad965ac3c4159fca43596f26be38208e5fd0407af6fdb735b`,
also matching the historical native menu fingerprint. This is a fresh native
main-menu control, not fresh native gameplay coverage.

All three host processes stop cleanly with exit code zero. The captured
unsupported-hardware/video fields are null, but neither those fields nor host
exit status detect this guest ABI fault. The diagnostic instruction trace uses
the same frozen engine and CPU and observes guest state without injecting it.
Its host-only build has one unused-variable warning; no guest build or prior
unit-test result is relabeled as a fresh test. These are correctness diagnostics,
not throughput measurements.
