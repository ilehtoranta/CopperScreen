# CopperScreen

CopperScreen is a native Amiga 500 emulator with an Avalonia desktop interface,
a lightweight emulation engine, a disk-image library and a headless runner.

## Supported machine

- PAL OCS A500 with a Motorola 68000.
- 512 KiB Chip RAM and 512 KiB slow RAM.
- Native Kickstart 1.3 ROM, supplied by the user.
- One to four floppy drives (DF0–DF3): standard 880 KiB ADF with explicit Save ADF, and read-only IPF, including selected ZIP entries. The default remains one write-protected drive.
- File-backed CopperHDF virtual hard disks with partition/RDB discovery and native Kickstart 1.3 OFS boot. See the [storage contract and incomplete IPF compatibility validation](docs/engine/STORAGE.md).
- Mouse and keyboard input, framebuffer output and stereo audio.

CopperStart, Legacy, ECS/AGA, accelerators and physical IDE/SCSI controllers are not supported by
the current application. Unsupported settings are reported explicitly rather
than silently changing the configured machine.

Compatibility and timing accuracy are still being developed. Known limitations,
including residual interlace instability, are recorded in the
[engine issue register](docs/engine/ISSUES.md).
ROMs, operating-system files and game media are not included.

## Build and run

Requires the .NET 10 SDK. External dependencies are pinned by package locks.
The Copper68k `1.4.1-locality.1` development pin needs a verified GitHub prerelease package in the
local feed before restore; see [dependency bootstrap and availability](docs/engine/CPU_TRACE.md#restore-on-another-machine).
Other packages restore from NuGet.org.

```powershell
./scripts/restore-development-package.ps1
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

Starting without arguments opens Settings. Choose your ROM and disk image there.

## Tests and headless execution

Run the host and disk-library tests after building the solution:

```powershell
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build --no-restore
```

Two optional native Lemmings replay tests require local ROM and disk images.
See [native validation](docs/NATIVE_VALIDATION.md) for setup. Normal CI skips
these tests when the media is unavailable.

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

Create a local Windows build:

```powershell
./scripts/package.ps1
```

This creates build artifacts; it does not publish a release.

## Components

| Component | Purpose |
| --- | --- |
| CopperScreen | Desktop interface, input, presentation and audio delivery. |
| [CopperMod.Amiga.Lightweight](CopperMod.Amiga.Lightweight/README.md) | Independent A500 engine with a single execution clock. |
| [CopperDisk](CopperDisk/README.md) | Disk-image decoding and hardfile metadata/range handling. Library floppy formats remain broader than the application's ADF/IPF support. |
| CopperMod.Amiga.Lightweight.Runner | Headless execution and workload measurement. |
| [Copper68k](https://github.com/ilehtoranta/CopperMod/tree/main/Copper68k) | Shared CPU interpreter, consumed as a pinned NuGet package. |

## License

MIT; see [LICENSE](LICENSE) and [dependency notices](THIRD-PARTY-NOTICES.md).
