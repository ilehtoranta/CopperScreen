# Minimal disk boot

CopperScreen supplies an original, deliberately limited boot firmware for demos
and games that replace the operating system after boot. Select **Minimal disk
boot — Free** in the launcher or Configure Amiga > Setup > Boot method.
An ADF in DF0 is required; a Kickstart ROM is not required. The application
remembers a separate setup for each model and boot method. Native Kickstart remains
the default. The bootstrap is experimental; individual disks must support the
selected machine and the bounded services below.

The equivalent command line is:

```powershell
dotnet CopperScreen.dll --minimal-disk-boot 'C:\path\demo.adf'
```

`--profile minimal-disk-boot` also selects the shipped
`lightweight-a500-minimal-disk-boot` profile. Supplying a native ROM and the minimal
boot option together is rejected. A remembered ROM path is retained for switching
back to native boot, together with its version metadata, but is not opened or
passed to the minimal boot session.

## Current product scope — 2026-11-03

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

- Standard PAL profiles: OCS A500 / 68000 / 512 KiB Chip RAM and zero or 512 KiB
  slow RAM; ECS A500+ / 8375 (318069-10) / 68000 / 1 MiB Chip RAM;
  Alice/Lisa A1200 / 68EC020 / 2 MiB Chip RAM. A500+/A1200 use no slow RAM.
  No Autoconfig Fast RAM, hard drives or shared folders. Other CPU/memory/chipset
  combinations are rejected with an explanation. Additional connected drives remain physical devices
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
  A1200 boot entry uses the 020 format-zero exception frame, exposes the 68010/020
  AttnFlags, and reads the saved PC at the 020 exception-frame offset on faults.
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
Slow allocations use `$C00000–$C7FFFF` when fitted. A500+/A1200 add a second Chip
free chunk at `$080000` through the end of fitted RAM (`$100000`/`$200000`, exclusive).
The low pool and resident services keep their addresses. AllocMem, FreeMem,
AvailMem, the memory headers and trackdisk read bounds use the fitted Chip size.
Memory and CPU traits specialize the guest source once at construction; there
are no extra per-cycle host branches.

A loader that has retired OS use may reclaim the physical memory described by
the headers, including the shim's RAM. Calling services after overwriting their
state/vectors is unsupported. The host never attempts to restore that memory or
turn the services off automatically. This is essential for hardware takeover.

## A500+/A1200 verification — 2026-11-03

The original A500 generated instruction stream remains byte-identical to the
pre-change baseline, SHA-256
`d6141aeac4380561fa1831eb5d15bf7c064cf2476f3d623d441ba4f88a4ace48`.
This is the current stream including the October null-sentinel correction;
it is distinct from the older vasm comparison below.

The model regressions execute original synthetic boot blocks through actual guest
disk DMA. Coverage includes user-mode entry, Supervisor stack frames and returns,
CPU flags, VBlank interrupt takeover/RTE, full extended-pool CLEAR/allocation/free/
coalescing, executing a second stage loaded at the top of Chip RAM across track
boundaries, disk swap/reset, and explicit unsupported-service/overlap/guest faults.
Launcher and Configure Amiga checks exercise both extended models, disk-required
and ready states, switching boot methods with saved media, and rejected Fast RAM.
Both public and CopperStart-enabled editions expose free boot independently of
the full-ROM build flag.

Validation: `dotnet build CopperScreen.slnx -c Release` succeeds with zero warnings
and errors. 51 targeted host tests pass (39 minimal-boot cases plus model/CPU
configuration tests); public-edition boot UI tests pass 5/5. The enabled edition's
launcher/configuration suite passes 89 cases, with 3 optional workspace cases
skipped (unavailable coverage). Native replay results are recorded separately below.

Original-media checks run without input for up to **3,000 output fields**:

| Model | Media | Result |
| --- | --- | --- |
| A500+ | Arte (Sanity) | 3,000 fields; 2,270 with non-silent audio, final PC `$083908`; captured artwork displayed. |
| A500+ | Desert Dream disk A | 3,000 fields; 1,623 with non-silent audio, final PC `$060910`; captured introductory ship scene. |
| A1200 | Desert Dream disk A | 3,000 fields; 1,655 with non-silent audio, final PC `$061E46`; captured introductory ship scene. |
| A1200 | Arte (Sanity), before Copper correction | **FAIL in the initial replay**: after 101 completed fields the guest loader at `$0010CE` encounters `disk FIFO overrun timing`. Corrected in the separate follow-up below. |

Decoded media SHA-256 values are the same as in the historical table below.
Generated 256 KiB firmware SHA-256, no slow RAM:
A500+ `20cde1d9a4af89fc76971caf6ece7e3a59452690c8f100ba2f5b2c1dfef60105`;
A1200 `1761e23454a76bec0a5de21e38a225c30b6ea9d4b9c51754bd3b64ab3b7a2b2b`.
The captures establish early progression, not full demo/game compatibility, AGA-only
software compatibility, cycle accuracy or performance. This initial model extension
did not change the engine or shared CPU. Its Arte A1200 failure is preserved as
before-fix evidence; the subsequent general Copper correction is recorded below.

Ignored evidence is in `artifacts/minimal-boot-models-20261103/`: baseline source,
TRX results, per-model `replay.json` and PNG checkpoints. Repeat the functional
tests with `dotnet test CopperScreen.Lightweight.Tests -c Release --filter
FullyQualifiedName~CopperScreenMinimalBootTests`. The optional
`MinimalBootNativeReplayTests` requires `COPPERSCREEN_MINIMAL_BOOT_ADF` (ADF or
ZIP-entry path); set `COPPERSCREEN_MINIMAL_BOOT_OUTPUT` for captures/results.
Unset media means skipped/unavailable native coverage, never a pass.

## Arte A1200 Copper SKIP correction — 2026-11-03

The FIFO failure was downstream of an incorrect Copper protection check.
An external instruction-boundary probe reproduced the first stop at canonical
cycle 14,604,961, CPU PC `$0010CE`. The beam was held at V59/H0, disk DMA was
active with 6,400 words remaining, and three received words were waiting with
no eligible input slot. BPLCON0 was `$06EA`, including ERSY. Disk rotation/input
continued while Agnus DMA was held by absent external sync, eventually filling
the FIFO. The normal session reported this at its next output boundary.

Earlier in the same unmodified guest, COP1LC points to `$0013EE`. The Copper
interprets `$48E7/$3030` as WAIT, `$3D79/$00DF` as SKIP, then `$F00A/$063C`
as a MOVE to protected offset `$00A`. Lightweight suppressed that MOVE before
checking protection, and continued into more CPU instructions. The later pair
`$6100/$06EA` at `$001402` writes BPLCON0, observed by cycle 10,826,564, stopping
sync at the line boundary. Different beam phase explains why the A500+ replay did not
reach the same failure.

The independent [vAmigaTS copskip4 description](https://github.com/dirkwhoffmann/vAmigaTS/blob/93fbdd2b0a842d28f35a6a36081967c86c946ef5/Agnus/Copper/Skip/README.md#copskip4)
and [probe source](https://github.com/dirkwhoffmann/vAmigaTS/blob/93fbdd2b0a842d28f35a6a36081967c86c946ef5/Agnus/Copper/Skip/copskip4/copskip4.s)
show that a skipped illegal write still stops Copper. The correction moves the
existing protection check ahead of SKIP suppression. It retains the existing
second-word output phase, address mask and COPCON limits. No disk FIFO behavior,
sync behavior, CPU, bootstrap firmware or media bytes are changed, and there is
no title-specific branch or new runtime diagnostic.

Twelve focused regressions cover OCS, ECS and AGA, high instruction-address bits,
COPCON danger limits, permitted skipped writes and recovery by COPJMP. Seven
illegal-destination cases fail before the correction; all twelve pass afterward.
All 39 Copper tests and the full diagnostic suite pass: **1,299 passed, 7 optional
native cases skipped**. Skips are unavailable coverage. The production Release
solution builds with zero warnings/errors.

Original Arte, using the same ADF and generated firmware hashes listed above,
now completes **3,000 fields on A1200**, with **2,381 fields of non-silent audio**,
animated multicolour captures and no unsupported-feature/fault stop. Final cycle
426,306,001, PC `$0011BA`. A500+ also passes 3,000 fields; its four checkpoint PNGs,
cycle/PC/colour checkpoints and 2,270 audible fields retain the initial result.
These are boot/early-sequence checks, not a complete-demo or performance claim.
The copskip4 reference supports the shared semantic correction; no new physical
AGA timing measurement was made.

Local evidence: `artifacts/arte-a1200-fifo-20261103/` contains frozen source,
external trace/history and RAM captures (ignored), before/after test results,
and `arte-after/model-{1,2}/replay.json` plus PNG checkpoints. Reproduce with:

```powershell
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
$env:COPPERSCREEN_MINIMAL_BOOT_ADF = 'C:/Data/TestImages/Arte (Sanity).zip#/Arte (Sanity).adf'
$env:COPPERSCREEN_MINIMAL_BOOT_OUTPUT = 'artifacts/arte-a1200-replay'
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --filter FullyQualifiedName~MinimalBootNativeReplayTests
```

## Arte full A1200 replay — 2026-11-03

The follow-up runs the original Arte ADF through its complete sequence, ending
credits and automatic restart with **minimal disk boot on PAL A1200** (68EC020,
Alice/Lisa, 2 MiB Chip RAM, no Fast/Slow RAM). A PAL A500+ (68000, ECS, 1 MiB
Chip RAM, no Fast/Slow RAM) is the control. Both use the production host session,
read-only DF0 and no user input. Neither uses a licensed Kickstart/CopperStart
ROM, disk patches or host OS services. The preceding Copper protection correction
is retained; this follow-up requires **no additional emulator or firmware fix**.

Each run executes 40,000 fields, approximately **13 minutes 21 seconds of
emulated time**, including one complete pass and the start of a second pass.
There are no captured Copper68k exceptions, CPU halts, minimal-boot service
faults, unsupported-feature stops or disk FIFO failures. Continuous 48 kHz stereo
PCM and 800 PNG/checkpoint pairs per run are retained locally. The ending and
restart, rather than the field budget alone, establish completion:

| Profile | Observed completion checkpoints |
| --- | --- |
| A1200 | 22,500: end scroller and 3D objects; 25,000: credits/greetings; 29,750–30,000: “ARTE is rebooting in a few seconds”; 30,750: opening presentation again; 31,250: opening tunnel again. |
| A500+ | 29,500–29,750: reboot message; 30,500: opening presentation again; 31,000: opening tunnel again. |

Intermediate A1200 captures also show the opening tunnel/cubes, sphere, Einstein
picture, starfield, kaleidoscope, face zoom, dot landscape, plasma, rubber sphere,
later artwork, particle effects and the final loading transition. The demo's
own “No AGA” text appears before the end section; it continues into the credits.
The earlier A1200 FIFO stop and historical A500 late SOS exception do not recur.

The [machine-readable record](ARTE_A1200_FULL_2026-11-03.json) retains input,
firmware, engine, PCM and checkpoint hashes, final CPU/cycle state and selected
scene fingerprints. Local captures, RAM and PCM are under
`artifacts/arte-a1200-full-20261103/{discovery,a500-plus-control}/`; media and
generated artifacts remain ignored. This is sampled visual progression with
continuous PCM capture, not a pixel-perfect or listening-based audio comparison,
physical-hardware certification, desktop UI soak test or host throughput result.

The external [ArteFullReplay probe](../../scripts/probes/ArteFullReplay/Program.cs)
retains the investigation without adding diagnostics to production execution.
It checks the qualified ADF hash, records CPU exceptions, captures every 50 fields
and exits nonzero on a fault. Model values are `0` (A500), `1` (A500+) and `2`
(A1200); use a fresh output directory for each replay:

```powershell
dotnet run --project scripts/probes/ArteFullReplay/ArteFullReplay.csproj -c Release --artifacts-path artifacts/arte-full-build -- 'C:/Data/TestImages/Arte (Sanity).zip#/Arte (Sanity).adf' artifacts/arte-full-a1200 2 40000
```

The retained probe builds and passes a 151-field A1200 smoke run, including the
final partial capture interval. The production Release solution builds with zero
warnings/errors. The diagnostic-suite result in the preceding section belongs
to the Copper correction; no engine implementation changed in this follow-up.

## Historical A500 verification — 2026-10-04/08

Release production solution: zero warnings/errors. Host suite: 171 passed;
six optional native cases skipped in that ordinary run are unavailable coverage.
The 17 new tests use original synthetic boot blocks and cover boot registers,
checksums, real DMA reads across heads/cylinders, post-boot second-stage calls,
allocator coalescing/header consistency, Supervisor, reset after disk swap,
settings persistence, the public Exec list register ABI, CPU scope, and explicit unsupported-operation faults.
Headless UI checks cover ROM-free startup, disk requirement, return to native
boot, and settings cancellation.

The original 2026-10-04 generated instruction stream was independently assembled by vasm
1.9, Motorola 68000, with optimizations disabled: all **9,274 bytes** match.
SHA-256 of both streams:
`16812a3b749a6eaff724a9215c76d01da04dcad5c9d32e6ca621e549ca579f68`.
This checks that earlier instruction encoding separately from execution fixtures;
the 2026-10-08 null-sentinel correction is not part of that frozen vasm comparison.

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

The [2026-10-08 Arte follow-up](ARTE_MINIMAL_BOOT_CRASH_2026-10-08.md) reproduces
a later SOS address exception with 22,008 completed fields under original minimal
boot. A null-list traversal followed the handler pointer placed at RAM address
zero into boot-ROM instructions. The firmware now clears that RAM longword,
matching observed native Kickstart state; all 18 minimal-boot tests pass, including
a synthetic traversal that fails before the correction. Corrected minimal boot
and native Kickstart 1.3 each complete 40,000 diagnostic fields on the same
engine/media with no captured address exception or unsupported-feature stop.
The startup result above is retained within its original scope; the new replay
is bounded sampled progression, not unlimited or hardware-certified playback.
The separate reported Avalonia/CLR desktop termination remains unreproduced
headlessly.

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

The 020 entry frame follows the [MC68020 User's Manual](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf),
section 6.4.2 and table 6-5: normal format-zero RTE consumes SR, PC and the
format/vector word (eight bytes).
The boot invocation follows Commodore's [Bootblock Booting contract](https://amigadev.grimore.org/Devices_Manual_guide/node007d.html).
Disk transfers follow Commodore's [DSKLEN DMA sequence](https://scratch.grimore.org/Hardware_Manual_guide/node0192.html).
The existing CopperDisk encoder/decoder provides the repository's standard MFM
format reference; the firmware decodes real DMA memory rather than calling it
during emulation.
