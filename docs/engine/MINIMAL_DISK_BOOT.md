# Minimal disk boot — 2026-10-04

CopperScreen supplies an original, deliberately limited boot firmware for demos
and games that replace the operating system after boot. Select **Minimal disk
boot (experimental)** on the welcome screen or in Settings > Setup > Boot method.
An ADF in DF0 is required; a Kickstart ROM is not required. The application
remembers this explicit choice. Native Kickstart remains the default.

The equivalent command line is:

```powershell
dotnet CopperScreen.dll --minimal-disk-boot 'C:\path\demo.adf'
```

`--profile minimal-disk-boot` also selects the shipped
`lightweight-a500-minimal-disk-boot` profile. Supplying a native ROM and the minimal
boot option together is rejected. A remembered ROM path is retained for switching
back to native boot, together with its version metadata, but is not opened or
passed to the minimal boot session.

## Current product scope — 2026-10-05

Legacy boot restoration is not required. CopperScreen's boot paths are native
Kickstart and this bounded firmware, both on Lightweight. Full replacement-OS
services belong to CopperStart. The [old restoration plan](../../CopperScreen/COPPERSTART_RESTORATION.md)
is superseded; its host build errors are not unfinished minimal-boot work.

## Ownership

This is a small CopperScreen boot component, not CopperStart or the former
CopperMod host-trap shim. Its source is
[`MinimalDiskBoot.s`](../../CopperScreen/Firmware/MinimalDiskBoot.s), packaged as
text and assembled at construction by
[`CopperScreenBootAssembler`](../../CopperScreen/CopperScreenBootAssembler.cs).
The narrow assembler accepts only this internal source; it is not a general
application API. No compiled ROM or third-party firmware is committed.

The resulting 256 KiB image enters the existing `LoadKickstart` reset/overlay
path. Every instruction, CIA operation, disk transfer and wait executes through
the existing Copper68k/Lightweight clock and bus. There are no instruction hooks,
host OS callbacks, direct sector-copy shortcuts or changes to engine timing.
Host code checks for the firmware's explicit fault loop once per output frame.
It does not infer OS retirement from the program counter leaving the boot block.

The broader replacement services and boot controller now belong to CopperStart;
see [the completed source move and remaining consolidation boundary](COPPERSTART_BOUNDARY.md).
The host integration remains outside the portable/ROM project graph and is not
available in the standard application.

## Supported boot contract

- PAL OCS A500, 68000, 512 KiB Chip RAM, zero or 512 KiB slow RAM, no Autoconfig
  Fast RAM or hard drives. Additional connected drives remain physical devices
  that guest takeover code can operate; the shim's trackdisk service serves DF0.
- Standard 880 KiB ADF, including a selected ZIP entry. Initial media validation
  requires a DOS boot identifier and the end-around-carry boot checksum.
  IPF bootstrap through the shim is unavailable; use native Kickstart.
- Firmware reads track zero through Paula disk DMA, decodes and verifies the
  AmigaDOS MFM header/data checksums, copies the original 1 KiB boot block to
  `$006800`, and executes its entry at `$00680C` in user mode. A1 points to a
  completed read I/O request, A6 to Exec, and address 4 contains the same Exec base.
- Reset re-reads the currently mounted disk through DMA. No original disk payload
  is embedded in the generated image, and no decoded track cache survives a DoIO.
  Disk swap and direct guest disk operations therefore cannot stale a host cache.
- Supervisor executes the A5 callback through a guest TRAP/RTE transition.
  Disable/Enable control the Amiga interrupt master with a nest count;
  Forbid/Permit track the single boot context without a task scheduler.
- AllocMem implements PUBLIC/CHIP/FAST/CLEAR with 8-byte alignment and failure
  on unavailable pools/unsupported flags. FreeMem maintains sorted/coalesced free
  chunks. AvailMem supplies current free memory or the largest free chunk.
  The guest-visible memory headers own the free lists and expose physical bounds,
  including already occupied boot memory, for takeover loaders.
- FindTask(NULL) returns the boot task; named tasks are absent. AddTail/Remove
  and AddPort/RemPort perform actual linked-list changes. There is no scheduler,
  task creation, Wait/Signal or asynchronous I/O/message completion.
- OpenLibrary/OldOpenLibrary recognize Exec and the bounded graphics surface,
  with version checks and reference counts; absent libraries return NULL.
  CloseLibrary accepts those opened bases. Graphics supports LoadView(NULL) and
  WaitTOF through guest register writes and beam polling. Non-null views and
  drawing operations are unsupported.
- OpenDevice recognizes `trackdisk.device`, unit 0. DoIO supports aligned CMD_READ
  across sector, side and cylinder boundaries, CMD_UPDATE/CMD_CLEAR without a
  write cache, and TD_MOTOR through CIA pins. WaitIO observes the completed
  synchronous request. Writes, other commands, SendIO and CheckIO are explicitly
  unsupported. This limitation does not affect a game's own direct disk hardware
  access or the application's ADF write-protect controls.

Missing library vectors record the library/LVO and caller address, then enter the
fault loop. Unsupported commands and reads overlapping resident service memory
are reported explicitly. Normal I/O address/length/read failures use I/O error
and actual-byte fields. Returning from a boot block reports that DOS/Workbench
requires native Kickstart; it does not invent a filesystem or start address.

## Memory lifetime

Raw DMA workspace occupies `$001000–$004FFF`; decoded track workspace occupies
`$005000–$0065FF`. The ordinary Chip allocation pool is `$007000–$06FFFF`.
Resident service state, negative vectors, libraries, boot I/O request and user
stack occupy reserved memory at `$075800–$07BFFF`. The supervisor stack starts at
`$000400`; the original boot block occupies `$006800–$006BFF`. Keeping the boot
block below the allocation pool also avoids raw loader buffers near the top of
Chip RAM. Reads into ordinary takeover-loader memory above the allocation pool
are allowed when they avoid resident services; the original 1 KiB boot buffer
can also be re-read through its I/O request.
Slow allocations use `$C00000–$C7FFFF` when fitted.

A loader that has retired OS use may reclaim the physical memory described by
the headers, including the shim's RAM. Calling services after overwriting their
state/vectors is unsupported. The host never attempts to restore that memory or
turn the services off automatically. This is essential for hardware takeover.

## Verification and limits

Release production solution: zero warnings/errors. Host suite: 171 passed;
six optional native cases skipped in that ordinary run are unavailable coverage.
The 17 new tests use original synthetic boot blocks and cover boot registers,
checksums, real DMA reads across heads/cylinders, post-boot second-stage calls,
allocator coalescing/header consistency, Supervisor, reset after disk swap,
settings persistence, the public Exec list register ABI, CPU scope, and explicit unsupported-operation faults.
Headless UI checks cover ROM-free startup, disk requirement, return to native
boot, and settings cancellation.

The complete generated instruction stream was independently assembled by vasm
1.9, Motorola 68000, with optimizations disabled: all **9,274 bytes** match.
SHA-256 of both streams:
`16812a3b749a6eaff724a9215c76d01da04dcad5c9d32e6ca621e549ca579f68`.
This checks instruction encoding separately from execution fixtures.

Bounded local media checks, **3,000 output fields per title**, without input:

| Media | SHA-256 of decoded ADF | Result |
| --- | --- | --- |
| Arte (Sanity) | `eb7521fc1de886c69706ab0e21898a89199fb12256db40743f3ed190957e896a` | Original boot/loader followed by animated output and audio; 2,270 fields with non-silent audio. Final PC `$C07C40`. |
| Desert Dream (Kefrens), disk A | `25738c042efdf2202e67a5bd5a40a08766384dfc31a92ed8e248d1b12e26e1e3` | Original boot/loader followed by introductory effects and audio; 1,622 fields with non-silent audio. Final PC `$060910`. |
| Full Contact (1991, Team17), disk 1 | `ad8c7ddc0ff38479f3b741c078b114390a9df30b9710824f95b082d73ecd5623` | Original boot/loader followed by intro credits and music; 2,763 fields with non-silent audio. Final PC `$021F2E`. |

These are startup/early-sequence checks, not complete demo qualification, disk-B
coverage, a gameplay claim or a throughput measurement. Full Contact exposed the
initial high boot-block placement overlapping its raw DMA buffer; moving the
original boot block to reserved low memory fixes that general layout problem.
No disk bytes or loader instructions are patched. The supplied Lotus Turbo
Challenge 2 image still raises a guest exception during its takeover startup;
it remains incompatible/unqualified. Do not silently add title-specific patches
or fall back to native Kickstart.

Ignored local evidence is under `artifacts/minimal-boot-preview/` and
`artifacts/ui-welcome-preview/`. ROM/media, generated firmware, captures and tool
downloads remain outside version control. The engine diagnostic suite is not
needed for this host/guest-firmware change: no engine or CPU implementation is
modified by it. Separately, three native Kickstart 1.3 regressions were run with
the local ROM, Lemmings disk set and the existing
`lemmings-cpu-bus-timing-v1.json` script: gameplay CPU/output identity, keyboard
power-up/Caps Lock/reset, and keyboard press/release with continued gameplay all
pass. The gameplay replays retain the expected cycle count, output identity and
CPU identity where asserted. The A500 3.1/A1200 optional cases were not exercised
for this change; optional skips never count as successful replays.

## References

The boot invocation follows Commodore's [Bootblock Booting contract](https://amigadev.grimore.org/Devices_Manual_guide/node007d.html).
Disk transfers follow Commodore's [DSKLEN DMA sequence](https://scratch.grimore.org/Hardware_Manual_guide/node0192.html).
The existing CopperDisk encoder/decoder provides the repository's standard MFM
format reference; the firmware decodes real DMA memory rather than calling it
during emulation.
