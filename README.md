# CopperScreen

CopperScreen is a native Amiga 500 emulator with an Avalonia desktop interface,
a lightweight emulation engine, a disk-image library and a headless runner.

## Supported machine

- PAL OCS A500 with a Motorola 68000 by default; experimental 68EC020, 68020, 68030 and 68040 choices are available in Settings.
- 512 KiB Chip RAM and 512 KiB slow RAM, with optional 512 KiB–8 MiB Autoconfig Fast RAM.
- Native Kickstart 1.3 (v34) or A500 Kickstart 3.1 (v40.63) ROM, supplied by the user. Workbench 3.1 floppy boot is verified with and without 2 MiB Fast RAM; see the [native boot record](docs/engine/KICKSTART_31_2026-09-30.md).
- One to four floppy drives (DF0–DF3): standard 880 KiB ADF with explicit Save ADF, and read-only IPF, including selected ZIP entries. The default remains one write-protected drive.
- File-backed CopperHDF virtual hard disks with partition/RDB discovery, native Kickstart 1.3 OFS boot and [Kickstart 3.1 FFS boot/write/reopen](docs/engine/KICKSTART_31_HD_2026-09-30.md). See the [storage contract and incomplete IPF compatibility validation](docs/engine/STORAGE.md).
- Mouse and keyboard input, framebuffer output and stereo audio.

CopperStart, Legacy, ECS/AGA, other accelerator profiles and physical IDE/SCSI controllers are not supported by
the current application. Unsupported settings are reported explicitly rather
than silently changing the configured machine.

Compatibility and timing accuracy are still being developed. Known limitations,
including residual interlace instability, are recorded in the
[engine issue register](docs/engine/ISSUES.md).
ROMs, operating-system files and game media are not included.

## Build and run

Requires the .NET 10 SDK. External dependencies are pinned by package locks.

CopperScreen pins the public mainline [Copper68k `1.5.0`](https://www.nuget.org/packages/Copper68k/1.5.0)
release for 040 instruction-fetch corrections, experimental 030/040 and a separate
060 diagnostic core. A clean checkout restores from NuGet.org without a local
candidate feed. See the [mainline release verification](docs/engine/COPPER68K_MAINLINE_1_5_0.md).
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
Browse identifies native 1.3/3.1 ROMs and selects the matching Kickstart choice.
For native 3.1 from the command line, select its profile explicitly:

```powershell
dotnet run --project CopperScreen -c Release -- --profile lightweight-a500-kickstart31 --rom "path/to/kickstart-3.1-a500.rom" "path/to/Workbench31.adf"
```

CPU changes require restarting the emulated machine. The 020 choices use the
existing OCS/RAM profile with the selected native ROM; they do not select an A1200 or AGA machine.
Settings > Memory accepts Fast RAM sizes 0, 512, 1024, 2048, 4096 or 8192 KiB.
Kickstart assigns the address; size changes require restarting the machine.
The runner accepts `--fast-ram-kib` with the same sizes.

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
