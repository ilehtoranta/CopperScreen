# CopperStart original Worms loader audit — 2026-10-22

**Worms remains incompatible at its main executable loader.** The rebuilt ROM
now runs the Ocean and Team17 intros from the supplied original SPS 230 Disk 1,
but its main `LoadSeg` fails with `ERROR_NO_FREE_STORE`. Copy protection, menus,
gameplay and Disk 2/3 handoffs are unqualified on CopperStart. Native Kickstart
1.3 reaches the protection screen without input (Table A, row 32, column C).

The source correction is [CopperStart f1c9a5e](https://github.com/ilehtoranta/CopperStart/commit/f1c9a5ee6bf6d0ca9755904d06baa00deb40117a).
Follow-up commits through `a32bac5f807d4a483864e2e8ad3302c7b94bba06` correct
BCPL field annotations only. The 524288-byte ROM has SHA-256
`3e4680beed7daa1684d731cebb01a9fa363dc22b4eaa7e441812ff520cda175f`;
its main image contains 478788 code/data bytes and native graphics occupies
30582 bytes. Independently linking a fresh copy of the compiled main image
produces exactly the same final ROM bytes. ROMs and media remain local.

## Compatibility changes

- Native BCPL GV entries now expose the task's message port, RootNode,
  indexed longword access, Exec calls, BSTR comparison, and get/set
  `pr_Result2`/`pr_CurrentDir`. The latter are raw public Process fields,
  independently compared with native 1.3. They are not stream selectors.
- The resident Segment chain is published as a BPTR in `DosInfo.di_NetHand`
  (+16), with BSTR names. Permanent negative use counts remain negative;
  the disabled count -999 remains disabled.
- `Execute` runs command prefixes and the selected input stream in a native
  CLI Process. Shared input/output handles are accepted. Leading redirections
  scope and restore streams. Native internal `Run` starts a background CLI
  with duplicated streams. Child CLIs inherit `cli_DefaultStack`; commands
  use its longword value rather than an unconditional 16 KiB command stack.
- The HUNK reader accepts consecutive CODE/DATA/BSS payloads with implicit
  intermediate HUNK_END, matching native 1.3. It retains file/relocation bounds
  and unknown-record errors. Its staging buffer falls from 16 KiB to 512 bytes.
- Native `MoveScreen` and `ScreenToFront/Back` rebuild the front viewport.
  Signed vertical movement below the raster is accepted, without an invented
  255-line ceiling. Native 1.3 and 3.1 both accept the authored 1000-line move.
- Exec initializes public Quantum/Elapsed fields and schedules expired
  equal-priority tasks, including after polling following Permit. DOS Delay
  waits on guest PAL VBlank interrupts instead of returning immediately.
- Private service stacks and workspaces are smaller, with private boot and
  command storage placed to preserve A501 program space. Script line capacity
  stays 4096 bytes; the actual workspace layout fits in 6152 bytes. Font ROM
  columns are shared, with the original 224 glyphs and standard public CharLoc
  entries preserved. The font descriptor does not require Chip RAM.
- OCS PAL detection observes the maximum beam line over a complete frame;
  counting sampled transitions undercounted when the probe ran from Chip RAM.

These are native guest ROM changes. The build-time graphics emitter lives under
`host/`, but its output executes as MC68000 code in the ROM. No host OS shim,
title recognition, game/protection patch, extra RAM, or engine change is added.
The smaller private stacks pass the bounded checks below; this does not prove
maximum stack depth for all future software.

## Remaining memory failure

A read-only instruction trace stops at field **6073**, PC **$F89336**, immediately
after the native CLI's fallback LoadSeg returns null and before child workspace
retirement. Its DOS process payload LastError at `$C13020` is **103**.
The captured largest free chunks are **329176 bytes of slow RAM** and
**349112 bytes of Chip RAM**. WORMS requests unflagged hunks of **72, 331528 and
348332 bytes**, before loader/allocator overhead. From those free lists, neither
large hunk fits slow RAM and only one fits the largest Chip block. Reducing the
remaining private footprint/fragmentation is still required. More defensive
argument checks do not resolve this failure.

A separate authored large-hunk fixture succeeds on both native 1.3 and the new
CopperStart ROM without the live intro/CLI allocations. That passing fixture
is not a successful original Worms replay. The original media failure remains
visible and repeatable.

## Validation and repeatability

The machine stays PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM, RTC disabled, one read-only DF0 and a full 908×313 raster.
Only original Disk 1 is mounted. The supplied ZIP and all three IPF entry hashes
are retained in the [machine-readable record](COPPERSTART_WORMS_2026-10-22.json).

| Check | Completed result |
| --- | --- |
| Production `dotnet build CopperScreen.slnx -c Release` | Pass, zero warnings/errors. |
| Final native ROM/instruction/disk/script suite | 510 passed, zero failed/skipped. |
| Focused host cores, including command-stack behavior | 94 passed, zero failed/skipped. |
| Independent ROM recomposition | Byte-identical, SHA-256 above. |
| Original Worms, normal/scalar through field 10000 | All 505 files per mode match; Team17 logo persists after main-load failure. |
| Fresh Worms reset repeats | All 505 files match each corresponding normal/scalar/native control run. All six processes exit zero. |
| Apidya regression through field 6500 | Normal meadow combat, player/projectiles and score 60; all 330 normal/scalar files match. Native control also reaches combat. All three processes exit zero. |

Capture files include full-raster BMP, selected CPU/hardware state, Chip RAM,
slow RAM and PCM. Every sampled unsupported/video-unsupported flag is null.
Worms has no nonzero PCM snapshots; Apidya has 49 of 66. Listening/audio fidelity
are unassessed. Normal/scalar equality and repeatability do not establish native
ROM timing equality or successful gameplay. Overlapping suite/compile runs are
not summed as additional unique coverage.

The [canonical no-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/worms-copperstart-disk1-boot-v1.json)
reproduces the boot failure. The retained runner is the frozen live replay
binary from the Superfrog correction: engine SHA-256
`9dfa51ccc81f9407fbb757b27967b1b2de4aa18fcba5a16d5ad0b8db2a97bcc2`.
Runner, Copper68k, CopperDisk, command lines, process exits and capture-manifest
hashes are recorded separately for every run. These are compatibility runs,
not host throughput or historical performance acceptance measurements.

## Build provenance and limits

The full CopperStart solution build is **not passing**: restore reports six
errors involving unavailable `CopperSharp.Sdk.Amiga`/`.Support`
`0.1.0-preview.1-g20` dependencies and a downgrade through CopperOS.MuiMaster.
Affected components were built separately. C# compilation uses two frozen SDK
DLL snapshots copied read-only from an existing CopperSharp68k working build
(HEAD `daab9bcf82ef6cf54b391cea8ff5e556cb352776` plus unrelated WIP), with
an artifact-local MSBuild reference override. This is not a clean released SDK
producer. Their exact hashes and override text are recorded in JSON. No sibling
source reference or package publication is introduced.

The ROM compiler/runtime remain the retained qualified toolchain. Native tests
use an older retained fixture engine; original media uses the frozen live engine
above. Their results are separate coverage. The JSON preserves both identities
and all twelve producer DLL hashes from the ROM manifest.

Legacy BCPL process creation through GV +$84 and explicit old `C:Run` remain
unsupported. Full Shell trailing redirects/pipelines/continuations, complete
multi-screen reveal, oversized/interlaced screens, general timer.device and
NTSC delay units are unqualified. Native reference probes separately establish
BCPL fields, packed HUNK boundaries, shared Execute streams (3.1), screen moves,
CLI stack units and 25/100-tick PAL delays; they do not certify every OS API.

Primary references: [RKRM DOS](https://developer.amigaos3.net/sites/default/files/downloads/2024-10/Amiga_ROM_Kernel_Reference_Manual_DOS.pdf)
§§11.5.4, 15, 17.3; [MoveScreen autodoc](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node0598.html);
[Exec task scheduling](https://wiki.amigaos.net/wiki/Exec_Tasks);
[internal Run](https://wiki.amigaos.net/wiki/AmigaOS_Manual%3A_AmigaDOS_Command_Reference).
