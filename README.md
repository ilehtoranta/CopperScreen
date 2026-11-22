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

Install the .NET 10 SDK, then restore the locked dependencies, build, and open the launcher.

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet run --project CopperScreen -c Release --no-build
```

To launch with your own Kickstart 1.3 ROM and floppy image, pass their paths.

```powershell
dotnet run --project CopperScreen -c Release --no-build -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

- [Launcher setup and everyday controls](docs/ONBOARDING.md)
- [CopperStart Mini and CopperOS build options](docs/COPPERSTART_APP.md)
- [Filesystem-service package and offline staging](docs/DISK_IMAGE_MANAGER.md#development-package)
- [Hard disks](docs/engine/STORAGE.md) and [shared folders](docs/engine/HOST_DIRECTORIES.md)
- [CPU and memory configuration](docs/engine/CPU_MEMORY_UPGRADES.md)

## Tests and headless execution

After building the solution in Release, run the host and disk tests. Engine diagnostics build separately under `artifacts/diagnostic-tests`.

```powershell
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
```

Run a 10-frame synthetic smoke test without supplying a ROM or disk image.

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release --no-build --no-restore -- --synthetic-rom-loop --frames 10
```

- [Native validation setup](docs/NATIVE_VALIDATION.md) and [regression replays](docs/engine/NATIVE_REGRESSION.md)
- [Performance workloads and measurement requirements](docs/engine/PERFORMANCE.md)
- [Engine architecture](docs/engine/ARCHITECTURE.md) and [known issues](docs/engine/ISSUES.md)

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
