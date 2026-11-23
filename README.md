# CopperScreen

CopperScreen is an Amiga emulator with an Avalonia desktop interface,
a lightweight emulation engine, a disk-image library and a headless runner.

## Download CopperScreen 1.2.1

Download the package for your platform. Each includes CopperStart Mini.
[Release notes](https://github.com/ilehtoranta/CopperScreen/releases/tag/v1.2.1) · [SHA-256 checksums](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/SHA256SUMS.txt)

| Platform | Download |
| --- | --- |
| Windows x64 | [ZIP](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/CopperScreen-1.2.1-win-x64.zip) |
| macOS Apple Silicon | [Arm64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/CopperScreen-1.2.1-osx-arm64.tar.gz) |
| macOS Intel | [x64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/CopperScreen-1.2.1-osx-x64.tar.gz) |
| Linux x64 | [x64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/CopperScreen-1.2.1-linux-x64.tar.gz) |
| Linux Arm64 | [Arm64 archive](https://github.com/ilehtoranta/CopperScreen/releases/download/v1.2.1/CopperScreen-1.2.1-linux-arm64.tar.gz) |

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
- **Hard disks and shared folders.** Attach raw HDFs and WinUAE VHDs, and share host folders with native Kickstart. Browse, copy, and inspect files in Disk Image Manager. [Storage](docs/engine/STORAGE.md) · [Disk Image Manager](docs/DISK_IMAGE_MANAGER.md)

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

Stage the qualified CopperStart Mini ROM and license, then create a local Windows x64 ZIP. [CopperStart Mini staging guide](docs/COPPERSTART_APP.md#copperstart-mini-bundle)

```powershell
./scripts/package.ps1
```

See the [MSIX packaging guide](docs/MSIX_PACKAGING.md) for Microsoft Store packages and installed validation.

## Components

- [CopperScreen desktop app](docs/ONBOARDING.md)
- [Lightweight Amiga engine](CopperMod.Amiga.Lightweight/README.md)
- [CopperDisk image library](CopperDisk/README.md)
- [Headless runner and workloads](docs/engine/PERFORMANCE.md)
- [Copper68k CPU emulation package](docs/engine/CPU_SUPPORT.md)

## License

MIT; see [LICENSE](LICENSE) and [dependency notices](THIRD-PARTY-NOTICES.md).
