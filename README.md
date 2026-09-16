# CopperScreen

Native Amiga desktop emulator powered by the independent
[CopperMod Lightweight engine](https://github.com/ilehtoranta/CopperMod/tree/main/CopperMod.Amiga.Lightweight).

Supported machine: PAL OCS A500, 68000, 512 KiB Chip RAM, 512 KiB slow RAM,
native Kickstart 1.3 and one read-only standard ADF drive. CopperStart, Legacy,
ECS/AGA, accelerator profiles and hard disks are not included in this build.
Unsupported settings are rejected rather than silently changing the machine.

## Build and run

Requires the .NET 10 SDK. Copper68k, CopperDisk and the Lightweight engine are
versioned package dependencies; no sibling source checkout is required.

**Migration status:** the three dependency preview packages have been verified
locally; public-feed publication is pending. Until then, restore requires the
local verified feed via `-p:RestoreAdditionalProjectSources=PATH_TO_FEED`.

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

Supply your own ROM and game media. They are not distributed here. Profiles for
unsupported configurations remain recognizable for future restoration, not as
claims of support. Residual interlace instability and uncertain hardware edges
remain disclosed in the [engine issue register](https://github.com/ilehtoranta/CopperMod/blob/main/CopperMod.Amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).

## Validation and packaging

The focused suite covers session routing, input, presentation, audio buffering,
allocation guards and dependency isolation. Two optional native Lemmings replays
require local ROM/media; normal CI explicitly skips them. See
[native validation](docs/NATIVE_VALIDATION.md).

Create a local Windows build (does not publish a release):

```powershell
./scripts/package.ps1
```

CI is manual-only until public dependency publication is confirmed. The repository
move does not establish a new FPS result or close hardware uncertainty.

## Source history and ownership

The application history was extracted from `ilehtoranta/CopperMod` through commit
`713ad6c1bc1bc31efc038996c50faccec31208d6`, preserving 90 CopperScreen commits.
Focused tests were imported from that same committed snapshot. Emulator source,
uncommitted experiments, ROMs and game media were not imported.

The old excluded Legacy/CopperBench source is retained for reference; it does not
participate in the native build. Restoration requires an optional adapter, not
new Legacy dependencies in the common host. See [migration notes](docs/MIGRATION.md).

MIT license; see [LICENSE](LICENSE) and [dependency notices](THIRD-PARTY-NOTICES.md).
