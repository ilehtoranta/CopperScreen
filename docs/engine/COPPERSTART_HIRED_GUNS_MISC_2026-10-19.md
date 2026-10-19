# CopperStart: native misc.resource and Hired Guns continuation

2026-10-19. Native `misc.resource` now passes its Kickstart 1.3 comparison.
Hired Guns proceeds beyond its previous missing-resource error and returns to
the CLI with **ERROR #0045 : COULD NOT OPEN CIAA.RESOURCE**. Game entry remains
unqualified. The [companion JSON](COPPERSTART_HIRED_GUNS_MISC_2026-10-19.json)
pins the source, ROM, tests, media, physical input and complete mode comparison.
This follows the [BCPL startup work](COPPERSTART_HIRED_GUNS_BCPL_2026-10-19.md).

## Resource behavior

CopperStart [6c34d3b](https://github.com/ilehtoranta/CopperStart/commit/6c34d3b9052067b7b89aeef265d3e1a53a06549d)
installs the service through guest Exec.AddResource during native bootstrap.
Its ROM code is 258 bytes at `$FF2800`; guest Exec allocates the two-vector
negative area and the public 50-byte MiscResource structure. No host service
participates in resource ownership.

The four serial/parallel slots store caller name pointers. A free slot returns
null and records the name; a busy slot returns its current name; release clears
the slot. Forbid/Permit protects updates while preserving existing task nesting.
The service adds no allocator-ownership or name-legitimacy admission check.
The [Commodore includes](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node010E.html)
and [autodocs](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/misc.html)
provide the API contract.

The authored guest probe passes against Kickstart 1.3 and CopperStart. It holds
all four slots simultaneously, checks busy-name results, releases/reclaims each
slot and checks nested Forbid state. The same probe fails against the preceding
CopperStart ROM because OpenResource returns null. This before-correction failure
is expected evidence, not a failure of the final ROM.

## Build and replay

The rebuilt 524288-byte ROM SHA-256 is
`fa1b6051a3eebf71372edc31a49d252f306ee99f8ddd9bc2120a917aecc70e36`.
The previously compiled main body is reused unchanged; the native composition
and bootstrap changed. No fresh main-ROM compilation is claimed. A clean
Release solution build completes with zero warnings/errors using the recorded
dependency root and existing project-reference outputs. The production emitter
produces the identical whole ROM. All 12 qualification input hashes are checked.

The final exact-ROM suite passes **103 cases, zero failures and zero skips**.
The new focused case also passes separately. The native reference probe passes
one case. Fourteen optional standalone polled-reader cases remain excluded,
not counted as passes. An initial test invocation with an incorrectly relative
replay-root path is retained as a host diagnostic, not guest qualification.

Normal and scalar runs cold-start the original Hired Guns v1.08.39.25 disk 1
on PAL OCS 68000, 512 KiB Chip/512 KiB slow RAM, one read-only floppy drive and
RTC disabled. They use the same physical CLI command as the preceding follow-up,
typed from field 2100. The command reaches the original utility, reports the
missing CIAA resource and returns to `1.SYS:>` by field 12000.

Both runs stop cleanly at 12000 with host exit zero. All **605 capture files per
mode** match: BMP, CPU/hardware/cycle state, Chip RAM, slow RAM and PCM. No
captured unsupported hardware/video path is reported. Mode agreement and host
exit status do not turn the game-entry failure into a pass.

`ciaa.resource`, the other CIA interrupt services and `workbench.task` remain
open dependencies. No new native game-launch control, Hired Guns gameplay or
Railroad route replay is claimed for this ROM. Earlier route evidence retains
its own ROM identity. These are correctness diagnostics, not throughput
measurements. ROMs, media, local build binaries and captures are not committed.
