# CopperStart: Hired Guns BCPL startup follow-up

The [native misc.resource continuation](COPPERSTART_HIRED_GUNS_MISC_2026-10-19.md)
implements the next service and records the later CIAA dependency. This record
retains the preceding ROM's results and limits.

2026-10-19. The original two BCPL Echo commands now run and the boot CLI remains
interactive after LoadWB fails. **Hired Guns still fails before CopperStart game
entry.** Its next observed CLI blocker is `misc.resource`. The
[companion JSON](COPPERSTART_HIRED_GUNS_BCPL_2026-10-19.json) records the new ROM,
source, complete input history, native comparison, binaries, captures and limits.

This follows the [original Echo fault](COPPERSTART_HIRED_GUNS_2026-10-19.md).
The [BCPL changes and rebuilt-ROM Railroad replay](COPPERSTART_BCPL_RAILROAD_2026-10-19.md)
are qualified separately. The old record remains evidence for its older ROM.

## Progress and next failures

The new ROM is SHA-256
`7f0c04308f3429fa396e778a4c1913d2148e0077f5bc02817d9f40569e6ccddf`,
from CopperStart [403e684](https://github.com/ilehtoranta/CopperStart/commit/403e684d114a06c70301673bcbac876cc551a4bf).
Both normal and scalar runs cold-start the original Hired Guns v1.08.39.25 disk 1
on the same bounded PAL OCS 68000, 512 KiB Chip/512 KiB slow RAM configuration.

At field 2000, both original Echo banners are visible and a `1.SYS:>` prompt
accepts physical input. LoadWB returns failure because its requested
`workbench.task` resident is absent. The trace identifying that resident is
retained locally. No successful placeholder replaces the desktop task.

At field 6000, the original `c:systemtakeover ?` utility prints its help and
returns to the prompt. A later physical CLI command uses the original icon's
memory settings and relocation/unpacking/system-takeover switches, with relative
CODE/DATA names on the same original disk. At 26000 it reports
`ERROR #0004 : COULD NOT OPEN MISC.RESOURCE` and returns to the prompt.
That missing service remains open; no CopperStart game-entry pass is claimed.

All **1305 capture files per mode** match byte for byte, including the complete
diagnostic command history. Both runs stop cleanly at 26000 with host exit zero.
Mode agreement and a clean host exit do not turn the guest failure into a pass.

## Native CLI comparison and limits

A separate Kickstart 1.3 run physically interrupts the original startup with
Ctrl-D, then types commands at the native CLI. The original utility rejects
`KEY=value` and explicit `YES` after Boolean switches. Those exploratory command
errors are not CopperStart compatibility defects. The accepted syntax uses
`KEY value` pairs and naked Boolean switches; the exact command is in the JSON.

At field 51000, that syntax reaches the original SystemTakeover confirmation
requester. After ordinary mouse input accepts it and Space advances the intro,
the display remains an incomplete/garbled menu at 74000 and 78000. This CLI
variant is a diagnostic control, **not successful native game-entry coverage**.
The earlier native Workbench-path main-menu control remains separately recorded.
No fresh native gameplay result is claimed here.

The follow-up mounts only original disk 1. It changes no media or executable
bytes and uses no guest RAM/register injection, forced entry point or host OS
shim. Native BCPL/input probes establish their specific contracts; they do not
qualify a complete BCPL runtime or Workbench. These are correctness diagnostics,
not throughput measurements. ROMs, media and local artifacts are not committed.
