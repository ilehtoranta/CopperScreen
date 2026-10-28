# F17 Challenge: known RAM inside the bootstrap reservation

2026-10-17, **LWA-NATIVE-013**. The supplied SPS 1703 two-disk IPF release
exposes a false one-megabyte memory rejection in the Lemmings-qualified
CopperStart ROM. Native Kickstart 1.3 reaches the title, disk-two prompt, menus
and driving on the same PAL OCS 68000 A500 with 512 KiB Chip and 512 KiB slow
RAM. The [evidence record](COPPERSTART_F17_2026-10-17.json) identifies the exact
sources, ROMs, media, inputs, binaries, native probes and reviewed captures.

The first incorrect operation is the boot loader's `TypeOfMem($C01000)` call.
Native Kickstart returns `$00000005` (`MEMF_PUBLIC | MEMF_FAST`); CopperStart
returns zero. The other probes, `$201000` and `$81000`, correctly return zero
under both ROMs. In the replacement-ROM trace the failing call is at `$284CA`,
field 181, returning at `$284CE`. The original game then displays
"NOT ENOUGH MEMORY / THIS GAME REQUIRES 1 MEG OF RAM". Runner completion does
not identify that as successful game execution.

The executed frozen component places the A501 MemHeader at `$C20000`, with
lower bound `$C20020` and upper bound `$C80000`. The private bootstrap arena
below that lower bound is still physical RAM. The
[TypeOfMem autodoc](https://www.theflatnet.de/pub/cbm/amiga/amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node059D.html)
describes a query of RAM attributes through the system memory lists, not an
allocation request or a requirement that the address belong to a free block.
The native control independently establishes the result for the loader's
particular address. Firmware layouts need not classify every reserved address
identically.

The native disk handoff now publishes `$C00000` as the existing detected A501
header's lower bound. It leaves the free-chunk pointer and free-byte count
unchanged. Private allocations remain occupied, and absent A501 RAM does not
acquire a fabricated header. The public `TypeOfMem` implementation and allocator
are unchanged. This is guest bootstrap metadata initialization; there is no
title/PC/media patch, host OS shim, CPU or engine correction.
The producer fix is committed as
[`82ba142`](https://github.com/ilehtoranta/CopperStart/commit/82ba142)
in [CopperStart PR #6](https://github.com/ilehtoranta/CopperStart/pull/6), stacked
on the existing Lemmings change.

An original generated boot disk checks `$C01000` and an odd address, rejects
unfitted addresses, verifies that AllocAbs inside the occupied ExecBase still
fails, and allocates/clears/frees ordinary A501 memory with unchanged final free
bytes. Native Kickstart passes both fitted/absent cases. The preceding ROM fails
the fitted-address query and passes the absent case. The corrected source ROM
passes both cases using the exact externally identified image.

The fresh image includes the committed Lemmings Intuition fix at producer base
`244e2e969dea613cc078ec9a63d0fef32012333c`. It rebuilds the native root over the
same eleven verified frozen Exec/DOS/device, SDK/compiler and graphics inputs;
the corrected disk source is projected into the qualification namespace. The
Intuition source is unchanged. This is not a clean build of the moving producer
tree or a standard-app firmware admission. The native root has no managed
allocations or framework features.

The corrected 512 KiB ROM was built at `2026-10-17T18:04:13.7162391+00:00`.
Its SHA-256 is
`6AD458F6E8F2823F3E978ED13AE567574B545557536C789E0EF99DA9E45B51CA`,
with 457,314 compiled code bytes and the unchanged 26,098-byte graphics resident.
The freshly rebuilt unchanged baseline is byte-identical to the preceding
Lemmings-qualified ROM (`05936F…961E6A`) and reproduces the memory error.

The retained input is
[`copperstart-f17-race-v1.json`](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-f17-race-v1.json).
It swaps the original disk-two payload at the prompt, chooses the race, then
uses Fire for the configured button accelerator with left/right steering. The
[game manual transcript](https://www.lemonamiga.com/doc/f17-challenge/598)
describes the button/stick accelerator setting. The earlier up-only input
reached the track but left the native car stationary; that exploration is
retained separately and is not an emulator failure.

Both final F17 ROM pairs run for 26,000 fields and match all **1,305** captured
BMP/state/Chip/slow-RAM/sampled-PCM files between conservative-batch and scalar
CPU modes, including final CPU, hardware, output and cycle fingerprints.
Reviewed captures distinguish the disk prompt, track selection, briefing and
input-driven driving on the South African circuit. At field 21,000 the speed
display reads 214; steering changes position and track progress advances. Across
ROMs, 234 of 261 screenshots match, including every screenshot from field 4,300
through 26,000. The 27 earlier differences occur during boot/loading/title
animation. Full-lap/race completion and championship progress are unverified.

Lemmings is repeated in both CPU modes through 24,000 fields, with **605**
matching files and a 100% level-one rescue. Apidya's 12,000-field opening-stage
regression uses normal mode and reaches the continue prompt. All 121 Lemmings
screenshots and all 201 Apidya screenshots match their preceding qualified runs.
Public RAM metadata and boot code changed, so cross-ROM CPU/RAM identity is not
a gate; the record identifies the reviewed functional outcomes separately.

The production CopperScreen Release build passes without warnings. The separate
producer Release build passes with one existing sibling-compiler nullable
warning; the final native-test project builds without warnings. Those source
builds use separate outputs from the frozen qualification components. The replay
runner was frozen before the production build, and every run records its actual
binaries.

ROMs, media, generated disks and captures remain local under
`artifacts/copperstart-f17-2026-10-17/`. Disk two must be supplied unchanged at the
ignored `media/corpus-f17-disk2.ipf` alias; its payload/archive identities are in
the record. `--firmware native` only disables interpretation of private boot
receipts after takeover; ROM hashes identify the actual firmware. These are
bounded correctness checks, not audio-fidelity, hardware-timing or throughput
acceptance. Other hardware profiles, TLSF banks and save/write paths are
unverified.
