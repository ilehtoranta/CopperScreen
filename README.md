# CopperScreen

CopperScreen is an Amiga emulator with an Avalonia desktop interface,
a lightweight emulation engine, a disk-image library and a headless runner.

## Download CopperScreen 1.2.0

Download the package for your platform. Each includes CopperStart Mini.
[Release notes](https://github.com/ilehtoranta/CopperScreen/releases/tag/v1.2.0) · [SHA-256 checksums](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/SHA256SUMS.txt)

| Platform | Download |
| --- | --- |
| Windows x64 | [ZIP](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/CopperScreen-1.2.0-win-x64.zip) |
| macOS Apple Silicon | [Arm64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/CopperScreen-1.2.0-osx-arm64.tar.gz) |
| macOS Intel | [x64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/CopperScreen-1.2.0-osx-x64.tar.gz) |
| Linux x64 | [x64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/CopperScreen-1.2.0-linux-x64.tar.gz) |
| Linux Arm64 | [Arm64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.0/CopperScreen-1.2.0-linux-arm64.tar.gz) |

## Getting started

1. Extract the download for your platform and open CopperScreen.
2. Choose **Amiga 500**, **Amiga 500+**, or **Amiga 1200**.
3. Select **Start Amiga**. The bundled CopperStart Mini ROM starts by default and prompts for a bootable floppy image if DF0 is empty.

To run Workbench or attach an HDF, use **Choose ROM…** to select an original Kickstart ROM. Use **Use built-in ROM** to switch back to CopperStart Mini.

CopperScreen saves settings per model and ROM. Input, display, volume, and floppy changes apply live. Select **Restart Amiga** to apply pending hardware changes. See the [onboarding guide](docs/ONBOARDING.md) and [ROM and HDF setup](docs/COPPERSTART_APP.md).

## Supported machine

- **PAL Amiga 500, 500+, and 1200.** Includes OCS, ECS, and AGA profiles, configurable Chip and Fast RAM, and an optional A501 RTC. [Machine configurations](CopperMod.Amiga.Lightweight/README.md)
- **CPU upgrades.** Includes an optional 68040 JIT. The 68060 boots on a PAL A1200 with CopperStart Mini or native Kickstart 3.1. [CPU support](docs/engine/CPU_SUPPORT.md)
- **Floppy drives.** Connect up to four DD or HD drives. Open ADF, ADZ, DMS, extended ADF, IPF, and SCP images, including supported images inside ZIP archives. [Formats and write support](docs/engine/DISK_FORMATS.md)
- **Hard disks and shared folders.** Attach HDF images and share host folders with native Kickstart. Browse, copy, and inspect files in Disk Image Manager. [Storage](docs/engine/STORAGE.md) · [Disk Image Manager](docs/DISK_IMAGE_MANAGER.md)

See the [engine issue register](docs/engine/ISSUES.md) for compatibility and timing limits.

## Build and run

Requires the .NET 10 SDK. External dependencies are pinned by package locks.

Copper68k is pinned to published [version `1.5.7`](https://www.nuget.org/packages/Copper68k/1.5.7).
Normal restore uses NuGet.org and requires no private qualification feed or
sibling project reference. Package locks retain the public package identity;
published package versions remain immutable. See the
[release and consumer record](docs/engine/COPPER68K_1_5_4_RELEASE_2026-10-22.md) and
[CPU support, validation and remaining limits](docs/engine/CPU_SUPPORT.md).
The explicit A1200 EC020 profile and native ROM operand forms
are recorded with [AGA verification](docs/engine/AGA_INITIAL_2026-10-01.md); preceding
CPU release evidence remains in [the 1.5.0 record](docs/engine/COPPER68K_MAINLINE_1_5_0.md).
The [Brian the Lion investigation](docs/engine/BRIAN_THE_LION_2026-10-03.md)
records preceding CPU corrections now included in 1.5.3, and Paula's software request bit 14.
Advanced CPU profiles remain experimental;
physical accelerator timing and full compatibility are not certified. The desktop 68060 boots a PAL A1200 with CopperStart Mini or native Kickstart 3.1. The FPU starts disabled. See [030/040/060 evidence and limits](docs/engine/CPU_OPTIONS_030_040_060_2026-09-27.md)
and the [020 CopperHDF release](docs/engine/COPPERHDF_020_2026-09-27.md).
The [040 fetch correction](docs/engine/LOTUS_III_040_FETCH_2026-09-27.md)
passes the original Lotus III input through a driving demo without changing its press timings.

An offline restore can stage the pinned filesystem-service archive
before restoring. See the [Disk Image Manager build instructions](docs/DISK_IMAGE_MANAGER.md#development-package).

```powershell
./scripts/stage-filesystem-service.ps1 -PackagePath "path/to/CopperStart.FileSystem.Service.0.5.0-dev.4.nupkg"
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

To enable licensed CopperStart, stage its qualified inputs and opt in explicitly:

```powershell
./scripts/prepare-copperos-bundle.ps1 -CopperStartA1200Rom "path/to/CopperStart-A1200.rom" -CopperOsHdf "path/to/CopperOS.hdf"
dotnet build CopperScreen.slnx -c Release -p:CopperScreenEnableCopperStart=true
```

Use the same property with `dotnet run`, `test` and `publish`. Existing
CopperStart/CopperOS qualification suites require the enabled build. The public
boot-option UI checks can run with the default disabled build using
`dotnet test CopperScreen.Desktop.Tests -c Release --filter Area=BootOptions`.
ZIP and MSIX packaging scripts expose the opt-in as `-EnableCopperStart`;
omitting it produces a public build. They create local artifacts only.

Bundle preparation is a developer build step using the qualified producer outputs;
it is not an end-user setup step. Inputs are hash checked and staged under ignored
`artifacts/copperos-bundle`, then embedded in build and publish outputs. CI can
set `CopperOsBundleDirectory` to an equivalent staged directory. Missing or
unqualified inputs fail a CopperStart-enabled build. Default builds require no
CopperStart bundle. Media binaries are not committed.
The [desktop HDF recipe and verification](docs/COPPEROS_DESKTOP_A1200.md) identify
the native desktop payloads, reproducible composition command and workspace migration.

Starting without arguments opens the start screen. Native Amiga choices ask for
your Kickstart ROM; CopperOS needs no file selection.
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

CopperScreen CI publishes self-contained desktop archives for Apple Silicon
and Intel Macs, plus x64 and Arm64 Linux. Each job opens the application from
the extracted archive and runs a synthetic 68040 JIT workload on the native
runner. Download the archives and their SHA-256 checksums from the
[CopperScreen CI runs](https://github.com/ilehtoranta/CopperScreen/actions/workflows/ci.yml).
Archives built from `main` include the qualified CopperStart Mini ROM and
its license. Each packaged app must reach Mini's insert-disk screen in CI.
The macOS app bundles come without Developer ID signing or notarization.

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
