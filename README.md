# CopperScreen

Complete native Amiga emulator product: desktop application, independent
[Lightweight engine](CopperMod.Amiga.Lightweight), disk library, tests and headless runner.

Supported machine: PAL OCS A500, 68000, 512 KiB Chip RAM, 512 KiB slow RAM,
native Kickstart 1.3 and one read-only standard ADF drive. CopperStart, Legacy,
ECS/AGA, accelerator profiles and hard disks are not included in this build.
Unsupported settings are rejected rather than silently changing the machine.

## Build and run

Requires the .NET 10 SDK. The app builds the engine and CopperDisk directly from
this repository. The shared CPU remains a pinned
[Copper68k 1.4.1-boundary.1](https://www.nuget.org/packages/Copper68k/1.4.1-boundary.1)
package dependency. No CopperMod checkout or local package feed is required.
Package locks pin external dependencies. Restore uses the public feed and an
ignored repository-local package cache to avoid older experimental packages in
the user's global cache.

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

Supply your own ROM and game media. They are not distributed here. Profiles for
unsupported configurations remain recognizable for future restoration, not as
claims of support. Residual interlace instability and uncertain hardware edges
remain disclosed in the [engine issue register](CopperMod.Amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).

## Validation and packaging

The focused suite covers session routing, input, presentation, audio buffering,
allocation guards and dependency isolation. Two optional native Lemmings replays
require local ROM/media; normal CI explicitly skips them. See
[native validation](docs/NATIVE_VALIDATION.md).

Run the 382 engine timing checks with isolated diagnostic build outputs:

```powershell
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --synthetic-rom-loop --frames 10
```

The diagnostic test project is deliberately outside the production solution so
parallel solution builds cannot overwrite the lean engine with a tracing build.
CI runs diagnostic checks in a separate job. The short runner example is a smoke
test, not an FPS gate; native performance harnesses are under `scripts/` and the
unchanged acceptance record is under `CopperMod.Amiga/`.

Create a local Windows build (does not publish a release):

```powershell
./scripts/package.ps1
```

CI builds and runs the focused tests on pushes to `main` and pull requests, and
can also be started manually. The repository move does not establish a new FPS
result or close hardware uncertainty.

## Source history and ownership

The application history was extracted from `ilehtoranta/CopperMod` through commit
`713ad6c1bc1bc31efc038996c50faccec31208d6`, preserving 90 CopperScreen commits.
Focused tests were imported from that same committed snapshot. Emulator source,
disk support, engine tests, runner and performance records were subsequently
moved here from CopperMod commit `0978025c4786cd924ef99fb82bd6508c50888494` after
the owner clarified that the split covers the whole emulator, not only its UI.
Uncommitted experiments, ROMs and game media were not imported.

The old excluded Legacy/CopperBench source is retained for reference; it does not
participate in the native build. Restoration requires an optional adapter, not
new Legacy dependencies in the common host. See [migration notes](docs/MIGRATION.md).
The [product ownership record](docs/PRODUCT_OWNERSHIP.md) supersedes the earlier
UI-only package boundary. Published preview packages remain available to external
consumers; they are not the app's engine-development boundary.

MIT license; see [LICENSE](LICENSE) and [dependency notices](THIRD-PARTY-NOTICES.md).
