# CopperStart: Railroad Tycoon — 2026-10-18

Railroad Tycoon v855.01 exercises the native AmigaDOS startup path rather than
immediately replacing the operating system. Testing the original two-disk ADF
release exposed general DOS, shell and graphics compatibility defects. This
record distinguishes those corrections from qualification of the game itself.

**Partial progress; the game remains unqualified.** The rebuilt ROM reaches the
initial mouse/keyboard menu and accepts the recorded choice. At field 20006,
its version-zero `OpenLibrary("intuition.library")` returns NULL. The standalone
ROM does not publish that library; the game takes its failure path and returns
to the CLI. Native Intuition integration is the next required compatibility stage.

Source commit
[`ffdeb9ab1d3a28e84fd299b7900d3714cb050705`](https://github.com/ilehtoranta/CopperStart/commit/ffdeb9ab1d3a28e84fd299b7900d3714cb050705)
is pushed on `codex/copperstart-railroad-20261018`. The exact 524,288-byte ROM has
462,852 compiled code bytes, 26,868 native graphics/bootstrap bytes and SHA-256
`CE38802911F61D58A4EFD7C074B48F0C3F386A1198E2786E87E13AD7075A7D60`.
The [companion JSON](COPPERSTART_RAILROAD_2026-10-18.json) records exact local
inputs, artifacts and commands. ROMs, game media and generated files remain local.

## Machine and controls

All media runs use the retained native Lightweight runner, PAL OCS 8371/8362,
MC68000, 512 KiB Chip plus 512 KiB A501 RAM, no Fast RAM and read-only DF0.
The runner, CPU, engine and disk binaries are unchanged. These are compatibility
diagnostics, not host-throughput acceptance measurements.

The original disk-one startup sequence is used unchanged:

```text
binddrivers
path RAM: add;
game
```

The retained corpus input sequence presses 1 and Return, swaps to disk B,
advances the subsequent screens, returns to disk A and clicks again. Its native
Kickstart 1.3 control completes 36,000 fields, reaches the region/menu, disk-B
prompt, credits, generated Eastern USA map and Rail News Weekly newspaper.

CopperStart initially loads much more slowly. The original disk swap at field
6500 interrupts its executable read, so a second protocol shifts **every event
by exactly 16,000 fields** and runs 56,000 fields. A separate Kickstart 1.3
control with that same delayed sequence reaches the same reviewed milestones.
This timing change is explicit; neither media nor guest executable is patched.
The controls do not establish train operation, saves or completion of the game.

CopperStart shows its readable selection menu at fields 12000–16000. At 20400,
after the first key, it displays “ONE MOMENT PLEASE” and returns to the CLI.
Its version-zero Intuition open is positively captured in a separate read-only
trace; absence of a sampled call is not used as evidence. The native control's
later credits, map and newspaper milestones are not attributed to CopperStart.

## Compatibility corrections

| Area | Correction and observed reason |
| --- | --- |
| Internal Path and command search | Implement the real CLI PathLock chain, RAM: device and command lookup through the current directory, path and C:. Path on this disk is a shell operation. |
| Relative directories | Query the current directory without replacing it with NULL; use its lock's handler rather than an empty default filesystem port. |
| HUNK format | Accept DEBUG/SYMBOL before payloads and memory attributes on payload tags. Honor the header's Chip allocation class; a native A501 probe previously placed Chip code in slow RAM. |
| Loader memory | Stream ordinary LoadSeg through at most 16 KiB of input workspace rather than retaining a full second image while allocating every hunk. The 178-hunk game and an authored memory-pressure case expose this difference. |
| OFS/FFS reads | Retain the current validated extent table within a sequential read. Re-reading the header and extension chain for each data sector caused excessive seeks. |
| Command stack | Put the requested stack size at 4(SP), with private recovery storage outside the public return-address/size pair. The earlier ROM exposed a recovery value of 192256 instead of 16384 bytes. |
| CLI metadata | Set the program-name BSTR and cli_Module before command entry, then clear/restore them on return. An empty name made the game's C startup underflow an unchecked copy loop and overwrite DOS library vectors. |
| Console | Publish pr_ConsoleTask, resolve the current-console name `*`, and open RAW: with raw character delivery. The game opens `RAW:0/0/640/200/RAILROAD TYCOON`. |
| BltClear | Implement the standalone ULONG count, packed rows/bytes-per-row and fill-word forms, without the older 64 KiB admission limit or caller-allocation ownership requirement. |

All these operations execute inside the guest ROM. RAM: has its own DOS packet
handler and Exec-owned storage. Unsupported RAM filesystem operations still
report ERROR_ACTION_NOT_KNOWN; disk writes, a full Shell and an Intuition console
window are outside that implementation. No host OS shim, title-specific bypass,
CPU change or chipset scheduling change is introduced.

The standalone graphics stage moves from $FF0000 to $FF8000 to accommodate the
larger compiled DOS stage. Its vector map, code envelope and command-stack
adapter are recorded in the exact composition manifest.

## Evidence and remaining scope

The before/after native probes distinguish public Amiga behavior from private
state validation. Checks on CopperStart-owned records, documented file layout,
allocation failure and physical hardware constraints remain. Compatibility is
not inferred from graceful handling of caller misuse.

The intermediate EA0B9E22… image passed 86 authored/native-component cases but
failed the real game. A read-only instruction trace caught the first DOS Open
vector overwrite at field 11390 in the game's name-copy DBRA loop at $C31A12.
The later host keyboard-queue exception followed that guest corruption and was
not the original defect. The final CLI regression snapshots the BSTR while the
command is active, before the shell clears it on return.

Both Release solution builds succeed without warnings or errors. The exact
composed image passes **87/87 retained native/component cases, zero skipped**:
77 guest-ROM cases and ten host filesystem/composition cases. This includes
Chip-only and A501 command-name, module, stack and return-cleanup probes. The
initial default CopperScreen restore failed because this isolated worktree lacks
the configured relative local feed; using the retained absolute restore config
then produced the clean build. No dependency version was changed.

Focused host suites pass 90 command-stack cases, 33 streaming-loader/directory
cases and 46 console cases. They overlap and are not added together. The
retained compiled callback checks pass one MC68000 case and two 020/040 cases;
those fixture results remain separate from the standalone ROM replay. Exact
test identities, negative-before cases and their result files are in JSON.

| Retained replay, normal CPU | Fields | Reviewed result with this exact ROM |
| --- | ---: | --- |
| Inside the Machine | 18,000 | Portrait/effects at 4000/8000, 3D at 12000, final DESiRE logo at 18000 |
| Full Contact | 24,000 | Title at 8000, fight at 16000, reduced health/fallen fighter at 24000 |
| Lotus III | 32,000 | Disk-two prompt, menus/music, race at 28000, advancing speed/score at 30000, result at 31200 |
| Arte | 40,000 | Effects at 8000, credits at 22000, textured corridor at 32000, artist page at 40000 |

Scene timing differs from earlier ROMs. These are sampled retained milestones,
not complete-game, continuous audio-fidelity or hardware-cycle qualifications.
The initial 16,000-field Railroad normal/scalar pair has 205 identical capture
files and matching final execution fingerprints. Both 56,000-field runs also
complete with all **705 capture files identical** between modes. Their final
cycle is `7957712010`, CPU `694575FC553F50C7`, hardware `41605918A1A6440F`,
output `9FA42D5E094F0173`. Completing these runs does not change the game's
failure status: native Intuition is still required for further progress.

Host semantic checks, compiled CPU fixtures, exact-ROM native probes and media
replays are reported separately in JSON. The retained native suite excludes the
14 standalone polled-reader cases; they are unavailable fresh coverage, not
additional passing cases. Raw replay manifests retain their automatic
`unreviewed` label; reviewed milestones belong to this report.

## References

- [AmigaDOS Developer's Manual](https://www.pagetable.com/docs/amigados_tripos/amigados_manual.pdf), page 160: command entry stack and register ABI.
- [Writing a UserShell](https://wiki.amigaos.net/wiki/Writing_a_UserShell): program-name, module and RunCommand responsibilities.
- [AmigaOS command reference](https://wiki.amigaos.net/wiki/AmigaOS_Manual:_AmigaDOS_Command_Reference): Path and its options.
- [DOS reference manual](https://developer.amigaos3.net/sites/default/files/downloads/2024-10/Amiga_ROM_Kernel_Reference_Manual_DOS.pdf): directory and handler resolution.
- [BltClear autodoc](https://d0.se/autodocs/graphics.library/BltClear): byte count and flags.
- [Previous Thunderbolt qualification](COPPERSTART_THUNDERBOLT_2026-10-18.md).

The follow-on [native Intuition qualification](COPPERSTART_INTUITION_2026-10-18.md) records the next ROM and Railroad replay. This earlier record retains its original image and outcome.
