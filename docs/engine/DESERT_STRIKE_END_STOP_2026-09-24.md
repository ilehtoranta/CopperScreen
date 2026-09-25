# Desert Strike: drive end-stop recovery

Desert Strike's disk-two seek stop was caused by treating a bounded drive's
inward end stop as an unsupported feature that stops execution. The game also
over-seeks in WinUAE, which lets its loader continue to the disk-three prompt.
The production correction clamps the head at the existing model limit and lets
execution continue. It does not edit the game's track caches.

This supersedes the media-boundary disposition in the
[earlier three-title investigation](THREE_NATIVE_INVESTIGATIONS_2026-09-24.md#desert-strike-separate-guest-track-caches).
That investigation's traces and guest-cache counterfactual remain valid historical
evidence. A stale cache alone does not establish bad media: the same state occurs
in the successful reference-emulator run.

## Reference comparison

Installed WinUAE **6.0.3.0** executable SHA256:
`F3E55700FD811CD543FDFEBF6C3221CFAA3D385828F110C6D3EB534C64503123`.
The upstream `6030` tag resolves to commit
`857f48fcb09399d4d95527e29a2fdeaedb41f827`. The reference profile was PAL OCS A500,
68000 with compatible and CPU/memory/blitter cycle-exact settings, no JIT,
512 KiB chip plus 512 KiB slow RAM, Kickstart 1.3, one write-protected DD drive
at 100% floppy speed. Warp changed host presentation speed. Live configuration
queries confirmed the significant settings.

The crack intro requires a mouse click. After clicking through it, joystick fire
continued the intro/menu, disk one was replaced with disk two, and fire selected
the campaign and dismissed its briefing. These were interactive inputs, not a
frame-aligned comparison with Lightweight. No guest memory was patched. Read-only
debugger memory/register dumps and disk logging used WinUAE's own named-pipe API.
Breakpoint attempts did not provide an instruction-stop trace and are not used
as evidence of exact instruction timing.

With the PDX disk one and the same unlabeled disk two used by Lightweight:

- At the briefing, `$C1CAC6` still held `$000B`, `$01A970` held `$0050`, and the
  physical drive was at cylinder 40/head 0. This reproduces the separate-cache
  state from the earlier investigation.
- On briefing exit, WinUAE logged inward over-seeks at PC `$C1CA48` and a subsequent
  read of track 167 (cylinder 83/head 1). Its loader continued to **INSERT DISK 3**.
- WinUAE's [drive_step implementation](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/disk.cpp#L1820)
  clamps inward movement at `hard_num_cyls + 3` and logs over-seeks without
  stopping the emulator. For this profile the maximum observed cylinder was 83.

The [Commodore disk-interface description](https://bastya.net/AmigaDevDocs/hard_8.html)
defines STEP, DIRECTION, TRACK0 and the other drive pins, including outward
track-zero handling. It does not specify our former host execution stop or a
universal inward cylinder limit. WinUAE provides supporting software evidence;
neither emulator establishes the exact mechanical travel of every physical drive.

## Media and isolated trials

The [machine-readable record](DESERT_STRIKE_END_STOP_2026-09-24.json) retains exact
archive/entry identities. All four ADF entries are 901,120 bytes. ROM SHA256:
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.
No media was modified or committed.

| Run | Result |
| --- | --- |
| Pre-correction Lightweight, plain disk one + disk two | Stops during field 22,084 at the ADF cylinder limit |
| Pre-correction Lightweight, QTX disk one + disk two | Same stop during field 22,084 |
| Pre-correction Lightweight, PDX disk one + disk two | Same stop during field 22,084 |
| WinUAE 6.0.3, PDX + same disk two | Unpatched guest reaches disk-three prompt after briefing |
| Isolated Lightweight clamp at cylinder 79 | Unpatched guest reaches disk-three prompt at 26,000 fields |
| Isolated Lightweight clamp at cylinder 83 | Unpatched guest reaches disk-three prompt at 26,000 fields |

The trials separate execution-stop behavior from extending head travel. Both
recover, so the production correction retains the existing ADF/empty-drive
maximum of 79 and IPF maximum of 83. This is a bounded model choice, not a claim
that mechanical travel should depend on media format. Extended ADF geometry,
physical settling/rate limits and no-flux behavior beyond recorded media remain
outside this correction. The normal native ADF geometry remains 80 cylinders.

## Implementation and validation

[LightweightFloppyDrive](../../CopperMod.Amiga.Lightweight/LightweightFloppyDrive.cs)
now accepts a clamped STEP without returning early. A simultaneous side change
still updates the selected track. The caller in
[LightweightA500Machine](../../CopperMod.Amiga.Lightweight/LightweightA500Machine.cs)
no longer reports this condition as unsupported. The internal control method
returns `void`; no title-specific branch, new device state, logging, allocation
or scheduling path was added. Outward homing, pin updates and disk-change behavior
continue through the existing control path.

The Release solution build passes with zero warnings and errors. All **719 engine
diagnostic cases pass**, with zero skips, using the separate diagnostic output
directory. Three discriminating cases failed before the fix and pass afterward:
over-seek through CIA with mounted/empty media, and a side change on a clamped
STEP. The old unsupported-limit expectation was replaced with bounded travel and
track-zero recovery assertions. Existing IPF, multiple-drive, serial/DMA and
allocation cases also pass. Host/disk tests were not rerun for this engine-only
control-path correction.

The final production engine SHA256 is
`66CF94D3AF712F7BB12822A44FFCD7A4DD0E2C45B5899A87A9B9C70E7D2AEC5C`;
it retains the preceding WORDSYNC fix and stable Copper68k 1.4.1. Both normal
and scalar production replays complete **26,000 fields** and visibly request
disk three, with no unsupported feature or guest-memory patch. All **104** saved
image/state/chip/slow-memory files match between those runs. The same 104 files
match the isolated cylinder-79 trial, and all **88** pre-stop checkpoint files
through field 22,000 match the frozen pre-correction PDX replay. These checkpoint
comparisons do not claim instruction-by-instruction equivalence between samples.

Native replays use the unchanged
[Desert Strike input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-desert-strike.json),
PAL OCS, 512 KiB chip plus 512 KiB slow RAM, Kickstart 1.3 and read-only DF0.
The script clicks at field 1,200, swaps to disk two at 18,120 after ejecting at
18,000, and presses fire at 20,000 and 22,000. Local captures, configuration,
logs, source copies, frozen binaries and diagnostic probes are retained under
`artifacts/desert-strike-reference/` and are not repository media fixtures.

The initial WinUAE startup stalled before creating a window; the owned process
was stopped and restarted with `-norawinput_all -nodirectinput`. A native attempt
to open the ADF held exclusively by WinUAE failed availability checks; the fresh
PDX native run read the identical ZIP entry instead. Neither failed attempt is
counted as a replay pass.

Disk three is unavailable. The verified compatibility boundary is the disk-three
prompt, not mission loading or gameplay. No formal host-throughput comparison was
run for this correction. Diagnostic replay times are not acceptance measurements;
the earlier WORDSYNC **INVALID / RERUN** result and package-specific performance
exceptions retain their original scope.
