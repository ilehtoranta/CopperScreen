# CopperScreen boot scope / retired CopperStart restoration plan

2026-10-05: legacy boot is not required. The earlier plan to restore CopperStart
through a Legacy adapter is superseded. CopperScreen's active boot paths are
native Kickstart and [Minimal disk boot](../docs/engine/MINIMAL_DISK_BOOT.md), both
on Lightweight. The full replacement OS belongs to CopperStart; restoring
`AmigaBoot` or its old host service graph is not unfinished CopperScreen work.

The 2026-09-16 decoupling remains in place.
The standard host builds without `CopperMod.Amiga.Emulator` or any CopperStart
project. Legacy selection remains recognizable in settings and profiles, but
fails explicitly: it is not installed in this build. CopperBench shows an
unavailable message; use native Workbench to launch disk applications.

## Build and test independently

From the repository root:

```powershell
dotnet build CopperScreen.slnx -c Release
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release --no-build --no-restore -- --synthetic-rom-loop --frames 10
dotnet run --project CopperScreen -c Release -- --kickstart "path/to/Kickstart_13.rom" "path/to/disk.adf"
```

The diagnostic test project is intentionally outside the production solution and
uses separate build outputs. The synthetic runner invocation is a smoke test,
not a gameplay or throughput benchmark.

The historical `CopperMod.sln`, `CopperScreen.Tests` and Legacy benchmark
projects include the unfinished CopperStart migration. They are not the native
host build/test entry points. Their source is retained, not silently passed or
deleted. The native host suite links the focused existing session, presentation
and audio tests and adds build-boundary/media checks.

## Retained historical boundary

- `CopperScreenEmulator.cs` and the original `CopperBenchViewModel.cs` are
  excluded from compilation, not erased. Do not simply re-enable them: their
  old host types need explicit conversion to the neutral session boundary.
- `ICopperScreenSession` retains host-level input/frame/audio/control calls.
  No CPU/device execution dispatch was added. `CopperScreenAdfImage` owns
  standard 880 KiB ADF mount bytes; ZIP loading is bounded and does not extract
  arbitrary media to temporary files. Other disk formats are unsupported.
- Clipboard BGRA payload and allocator profile metadata are host-owned types.
  Their ownership does not depend on the retained replacement-OS sources.
- Filename-based disk navigation was extracted unchanged from Legacy.
- The package-boundary follow-up also removed `CopperMod.Amiga` and
  CyberGraphics build dependencies. Profile metadata, raw-key names and standard
  raster geometry are now host-owned. The old presentation-frame helper is
  excluded with the Legacy emulator. These common host types remain independent
  of the retained Legacy types.
- See `PACKAGE_BOUNDARY.md` for the historical package-extraction boundary.
  No emulator execution abstraction was added by that separation.

The earlier optional-adapter proposal is historical, not an implementation
requirement. Replacement-OS source consolidation and its separate tests belong
to CopperStart. CopperScreen's minimal firmware stays bounded to boot-block
execution and the small services needed before hardware takeover; it does not
grow into a DOS, Workbench or multitasking replacement.

The original host failed to build from untouched `e9a1ef47` due to 26 distinct
missing CopperStart/SDK compiler errors. Those old migration errors are retained
evidence, not a CopperScreen completion gate. Historical build/performance
acceptance and hardware uncertainty records are not relabelled by this split.
