# CopperScreen

CopperScreen is an Amiga emulator with an Avalonia desktop interface,
a lightweight emulation engine, a disk-image library and a headless runner.

## Supported machine

- PAL A500 with OCS 8362 Denise and a Motorola 68000 by default; ECS 8373 Denise and experimental 68EC020, 68020, 68030 and 68040 choices are available in Settings.
- Explicit PAL Agnus: 8371 with 512 KiB Chip RAM, 8372A with 512 KiB or 1 MiB, or 8375 (318069-10) with 1 or 2 MiB. The default remains 8371 / 512 KiB Chip + 512 KiB slow RAM. Larger Chip RAM layouts use no slow RAM; optional Autoconfig Fast RAM is separate, up to 8 MiB with 24-bit CPUs or 1 GiB with full 32-bit CPUs. See [CPU/memory configuration and native evidence](docs/engine/CPU_MEMORY_UPGRADES.md).
- Native Kickstart 1.3 (v34) or A500 Kickstart 3.1 (v40.63) ROM, supplied by the user. Workbench 3.1 floppy boot is verified with and without 2 MiB Fast RAM; see the [native boot record](docs/engine/KICKSTART_31_2026-09-30.md).
- Optional [Minimal disk boot](docs/engine/MINIMAL_DISK_BOOT.md): experimental ROM-free ADF bootstrap for takeover demos/games on the default PAL 68000 A500. It supplies a small boot-service surface; DOS and Workbench require native Kickstart.
- Explicit [CopperStart standalone ROM](docs/COPPERSTART_APP.md) boot on Lightweight: qualified experimental PAL OCS 68000 A500, 512 KiB Chip + 512 KiB slow RAM and read-only DF0 by default. Supply the matching versioned ROM; explicitly admitted CopperOS workspaces can be writable.
- One to four floppy drives (DF0–DF3): 880 KiB DD / 1760 KiB HD ADF with explicit Save ADF, plus read-only ADZ, DMS, modern extended ADF, IPF and SCP, including selected ZIP entries. SCP uses a decoded first revolution. See [format support and limits](docs/engine/DISK_FORMATS.md). The default remains one write-protected drive. Select DD or HD per drive in **Settings > Floppy Drives** and restart the Amiga. Drive types are saved in profiles and the remembered last machine. The runner accepts repeatable `--drive-type 0 HD` (units 0–3; `DD` is the default); connect external units with `--drives`.
- File-backed CopperHDF virtual hard disks with partition/RDB discovery, native Kickstart 1.3 OFS boot and [Kickstart 3.1 FFS boot/write/reopen](docs/engine/KICKSTART_31_HD_2026-09-30.md). See the [storage contract and incomplete IPF compatibility validation](docs/engine/STORAGE.md).
- [Media protection and recovery](docs/MEDIA_PERSISTENCE.md): new imports are read-only, ADF replacement requires consent, and CopperOS provides versioned workspace backups, import and previous-checkpoint recovery.
- Live [shared folders](docs/engine/HOST_DIRECTORIES.md), read-only by default, with native DOS access, optional Workbench folder boot, persistent comments/protection and reversible Windows filename mappings. Mount changes require restart; users supply the OS files.
- Mouse and keyboard input, framebuffer output and stereo audio.
- Optional Legacy-compatible A501 RTC, selected in Settings > Memory > RTC clock
  or runner `--rtc`. Host local time and guest-set session offsets; see
  [RTC behavior and limits](docs/engine/RTC.md).
- Initial PAL A1200 profile: Alice/Lisa, 68EC020, 2 MiB Chip RAM and native Kickstart 3.0 (39.106). Eight-plane, 256-colour RGB24 output and native CopperHDF boot/persistence are verified. HAM8, dual playfields and enhanced sprites remain next steps; see [AGA evidence and limits](docs/engine/AGA_INITIAL_2026-10-01.md).

Legacy host services, full AGA compatibility, A2024 external scan conversion, other accelerator profiles and physical IDE/SCSI controllers are not supported by
the current application. Unsupported settings are reported explicitly rather
than silently changing the configured machine.

Compatibility and timing accuracy are still being developed. Known limitations,
including residual interlace instability, are recorded in the
[engine issue register](docs/engine/ISSUES.md).
ROMs, operating-system files and game media are not included.

## Build and run

Requires the .NET 10 SDK. External dependencies are pinned by package locks.

Copper68k's published baseline is [version `1.5.3`](https://www.nuget.org/packages/Copper68k/1.5.3).
This development worktree pins the unpublished `1.5.4-qualified-dev.1` candidate
from the ignored `artifacts/qualified-cpu-20261011/feed` directory. Put the frozen
package identified by the qualification record there before restoring this
worktree; it is not yet available from NuGet.org. Rebuilt package content needs
a new development version and regenerated locks. No sibling project reference
is used. See the
[candidate qualification record](docs/engine/COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.md) and
[CPU support, validation and remaining limits](docs/engine/CPU_SUPPORT.md).
The explicit A1200 EC020 profile and native ROM operand forms
are recorded with [AGA verification](docs/engine/AGA_INITIAL_2026-10-01.md); preceding
CPU release evidence remains in [the 1.5.0 record](docs/engine/COPPER68K_MAINLINE_1_5_0.md).
The [Brian the Lion investigation](docs/engine/BRIAN_THE_LION_2026-10-03.md)
records preceding CPU corrections now included in 1.5.3, and Paula's software request bit 14.
Advanced CPU profiles remain experimental;
physical accelerator timing and full compatibility are not certified. The 060 is
unavailable in the desktop: Kickstart 1.3's task frames are incompatible with its
FPU state format. See [030/040/060 evidence and limits](docs/engine/CPU_OPTIONS_030_040_060_2026-09-27.md)
and the [020 CopperHDF release](docs/engine/COPPERHDF_020_2026-09-27.md).
The [040 fetch correction](docs/engine/LOTUS_III_040_FETCH_2026-09-27.md)
passes the original Lotus III input through a driving demo without changing its press timings.

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

Starting without arguments opens Settings. Choose your ROM and disk image there.
Browse identifies native 1.3/3.1 ROMs and, with the A1200 profile selected,
validates its 3.0 ROM. It selects the matching Kickstart choice.
For native 3.1 from the command line, select its profile explicitly:

```powershell
dotnet run --project CopperScreen -c Release -- --profile lightweight-a500-kickstart31 --rom "path/to/kickstart-3.1-a500.rom" "path/to/Workbench31.adf"
```

Use `--profile a1200-aga-pal --rom "path/to/A1200-39.106.rom"` for the initial AGA profile.

Storage settings include **Shared folders**. Desktop and runner accept repeatable
`--host-dir HOST0 Exchange "path/to/folder"` (read-only) or `--host-dir-rw` with
the same three arguments. Add `--host-dir-boot-priority HOST0 0` to boot a folder
containing your Workbench installation. Native Kickstart is required; Minimal disk
boot has no DOS. See [folder behavior and native verification](docs/engine/HOST_DIRECTORIES.md).

Desktop hardfile imports use `--hdf "path/to/disk.hdf"` read-only by default.
For CopperStart, use `--hdf-rdb "path/to/disk.hdf"` (or
`--hdf-rdb-writable`) to discover FFS partitions from the RDB and use its boot
priorities. Use `--hdf-ffs "path/to/disk.hdf" DH0:2:32:4:58` (or
`--hdf-ffs-writable`) to specify an existing 512-byte FFS partition explicitly.
The app and runner share this syntax; see the
[RDB and hard-disk settings guide](docs/COPPERSTART_RDB_2026-10-18.md).
`--hdf-writable` explicitly allows direct changes to that file; copy it first.
Existing profiles retain their explicit write permissions. Ordinary floppy writes
stay in memory until **Save ADF**; use **Export workspace** for a CopperOS backup.

The welcome screen and Settings > Setup offer Amiga 500 (default), Amiga 500
with ECS and experimental Amiga 1200, with CPU and optional-feature controls.
CPU changes require restarting the emulated machine and preserve the selected
motherboard. Both A500 and A1200 support CPU upgrades and configurable Fast RAM.
The RAM selector offers standard power-of-two sizes from 512 KiB to the CPU's
limit: 8 MiB for 68000/68010/68EC020, or 1 GiB for full 68020/030/040.
Banks above 8 MiB require native Kickstart 3.x; see [choices and verification limits](docs/engine/CPU_MEMORY_UPGRADES.md).
Kickstart assigns the address; size changes require restarting the machine.
The runner accepts `--fast-ram-kib` with the same sizes, up to 1048576 KiB.
The Memory page also selects Agnus and compatible Chip RAM sizes. The runner
accepts `--agnus 8371|8372a|8375-318069-10`, `--chip-ram-kib 512|1024|2048`
and `--slow-ram-kib 0|512`; unsupported combinations fail explicitly.
Settings > Memory also selects 8362 OCS or 8373 ECS Denise; the runner accepts
`--denise 8362|8373`. ECS display supports SuperHires, extended windows and
programmable timing with safe output resizing. Agnus, Denise and RAM changes
require restarting the machine. The board oscillator remains PAL; guest ECS
counters can select NTSC geometry. See [ECS display evidence and monitor-driver
coverage limits](docs/engine/ECS_DISPLAY_2026-10-01.md).

## Tests and headless execution

Run the host and disk-library tests after building the solution:

```powershell
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build --no-restore
```

Optional native Lemmings replay and input tests require local ROM and disk images.
See [native validation](docs/NATIVE_VALIDATION.md) for setup. Normal CI skips
these tests when the media is unavailable.

The [native regression suite](docs/engine/NATIVE_REGRESSION.md) runs six pinned
game/demo replays with supplied local media, checks normal/scalar execution and
produces an HTML report against a reviewed baseline. Missing media is reported
as unavailable coverage. Its Python tooling tests run in CI without game media.

Engine timing tests use separate diagnostic build outputs:

```powershell
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
```

Keep diagnostic outputs separate from the production build. The diagnostic test
project is intentionally outside the main solution; CI runs it in a separate job.

For a short headless smoke test without a ROM or game image:

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release --no-build --no-restore -- --synthetic-rom-loop --frames 10
```

This synthetic run is not a gameplay benchmark. Native performance harnesses
are under `scripts`; the [performance guide](docs/engine/PERFORMANCE.md)
describes workloads, measurement requirements and their current portability limits.
The retained native benchmark scripts have host and frozen-workload restrictions;
a smoke test or optional native replay does not establish formal throughput acceptance.

Engine development guidance is in [architecture](docs/engine/ARCHITECTURE.md) and
[known issues](docs/engine/ISSUES.md). Dated implementation, acceptance and
performance records are in the [history archive](docs/history/amiga/README.md).

## Windows package

The [Microsoft Store productization roadmap](docs/PRODUCTIZATION_ROADMAP.md)
tracks planned release work, priorities and acceptance criteria, including
CopperStart and CopperOS integration. It does not change current support claims.

The [CopperStart/CopperOS draft integration contract](docs/COPPEROS_INTEGRATION.md)
records the inspected artifacts, a bounded CLI-first target and the producer
work still needed. That inspection is frozen. The later
[original native boot proof](docs/COPPEROS_BOOT_PROOF_2026-10-16.md) and
[native storage/workspace continuation](docs/COPPEROS_STORAGE.md) define separate
development pairs and evidence. Store shipping/default activation remain closed.

Create a local Windows build:

```powershell
./scripts/package.ps1
```

This creates build artifacts; it does not publish a release.

For Microsoft Store MSIX generation, diagnostic install/update tests and Windows
App Certification Kit checks, see [MSIX packaging](docs/MSIX_PACKAGING.md).
See [onboarding and everyday launches](docs/ONBOARDING.md) for the native ROM,
CopperStart and CopperOS workspace flows and their remaining release checks.

The [simple CopperOS desktop implementation plan](docs/COPPEROS_DESKTOP_PLAN.md)
defines the native CopperStart prerequisites, desktop features, producer ownership
and qualification milestones for the itch.io experience.

Bundled machine presets are read-only defaults in `Presets/`. Saved profiles and
preferences use `%LOCALAPPDATA%/CopperScreen/`, so saving does not require write
access to the application folder. Existing portable profiles are copied without
overwriting originals or personal profiles; see the [storage and migration
contract](docs/PROFILE_STORAGE.md) for locations, recovery and verification limits.

## Components

| Component | Purpose |
| --- | --- |
| CopperScreen | Desktop interface, input, presentation and audio delivery. |
| [CopperMod.Amiga.Lightweight](CopperMod.Amiga.Lightweight/README.md) | Independent A500 engine with a single execution clock. |
| [CopperDisk](CopperDisk/README.md) | ADF/ADZ/DMS/IPF/SCP decoding used by the engine and host, plus hardfile metadata/range handling. |
| CopperMod.Amiga.Lightweight.Runner | Headless execution and workload measurement. |
| [Copper68k](https://github.com/ilehtoranta/CopperMod/tree/main/Copper68k) | Shared CPU interpreter, consumed as a pinned NuGet package. |

## License

MIT; see [LICENSE](LICENSE) and [dependency notices](THIRD-PARTY-NOTICES.md).
