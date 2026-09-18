# CopperScreen standalone repository boundary

**Historical boundary:** this UI-only split was superseded by the owner's
complete-product decision. See [current ownership](../docs/PRODUCT_OWNERSHIP.md).
The app now references the local Lightweight and CopperDisk projects; only
genuinely shared libraries such as Copper68k remain external packages.
For the current source-build and test contract, use the repository
[README](../README.md). The package-extraction proposal and
package-only proof below are retained as historical audit evidence, not as
current migration or release instructions.

## Superseded decision and scope — 2026-09-16 (historical)

Prepare CopperScreen to move into its own repository without moving or copying
emulator implementation code. Keep Copper68k, CopperDisk and
CopperMod.Amiga.Lightweight in the library repository and consume explicit,
versioned packages from the application. No repository or package has been
published by this preparation step.

The application owns profiles, settings, raw-key names, presentation geometry,
windowing, input mapping, pacing and audio-device delivery. The engine owns all
emulated hardware. The split adds no per-cycle interfaces, dispatch, copying,
logging or diagnostics. Existing native-session pixel and audio paths are unchanged.

### Historical dependencies

The active host no longer references CopperMod.Amiga, CyberGraphics,
CopperMod.Amiga.Emulator, CopperStart or its SDK. The old Legacy emulator,
presentation-frame helper and CopperBench implementation remain excluded source
for a future optional adapter; their old type bindings must not be re-enabled
unchanged. Unsupported profiles retain their settings and fail explicitly.

CopperScreen uses public Copper68k and Lightweight APIs. Its CPU friend grants
were removed; internal engine-to-CPU timing hooks remain internal. Only profile
metadata and protocol constants were moved into the host, not execution classes.

The local proof uses these unpublished package versions:

| Package | Version |
| --- | --- |
| Copper68k | 1.4.1-boundary.1 |
| CopperDisk | 2.1.1-boundary.1 |
| CopperMod.Amiga.Lightweight | 0.1.0-preview.1 |

Engine and host package references use exact version ranges. These are local
verification versions, not claims that a public feed contains them. Do not
publish different binaries under an already published version.

## Historical build modes (superseded)

The package-only consumer mode described in this historical proposal is not
implemented by the current `CopperScreen.csproj`. In particular,
`UseEmulatorProjectReferences=false` does not remove its unconditional project
references, so that switch must not be treated as a supported current
package-consumer build.

Current monorepo development uses the commands in the [README](../README.md#tests-and-headless-execution).
For reference, the production build is:

```powershell
dotnet build CopperScreen.slnx -c Release
```

The old package-only packing flags and consumer command belonged to the
superseded proof. Local package creation, when explicitly needed, is provided by
`scripts/pack-engine.ps1`; it does not publish packages.

## Historical isolated verification (superseded)

The one-off verification script referenced by this historical record is not
present in the current repository. Do not use or recreate it as the current
verification procedure; follow the commands and test separation in the
[README](../README.md#tests-and-headless-execution) instead.

The historical native-replay procedure supplied all of `-NativeRom`, `-NativeAdf`, `-NativeScript`
with local paths to the native Kickstart 1.3 ROM, frozen Lemmings disk and
`CopperMod.Amiga.Lightweight.Runner/Workloads/lemmings-native-level1-exploratory.json`.
Without these paths the two native tests are explicitly skipped.

The historical proof created a fresh directory, packed the three libraries to a local feed,
uses a fresh package cache, copies only the app and focused host tests, and builds
and tests the copied consumer. There are no CPU, disk or engine source projects
beside it. It checks restored dependencies are packages and rejects forbidden
Legacy/CopperStart DLLs in host output. Logs and test results remain in the printed
evidence directory. It never publishes, commits, deletes evidence or touches a remote.

The retired proof accepted `-SourceRoot` to select an isolated source snapshot and
`-WorkDirectory` to choose a new evidence directory. Those were options of the
absent historical script, not current repository commands.

## Verification evidence

The 2026-09-16 proof uses accepted commit
`a7f30465f8e4a5021b3790f9259313cbac9dc793` plus this boundary change, in a detached
snapshot. Unrelated uncommitted CPU JIT, Legacy and CopperStart work was not packed.
Evidence is retained under `.codex-tmp/copperscreen-package-proof-20260916`.

- Package-only focused suite: **58 passed, 0 skipped, 0 failed**, including both
  14,520-field native Lemmings replays (with and without keyboard input).
- Accepted native fingerprints retained: cycle `2063321634`, ordinary replay CPU
  `6AE7090DA8AFB7E3`, and both replays' pixel/audio output `C65F87325946E5DA`.
  Audio was active and no unsupported feature was reported. The existing
  steady-execution allocation check also passed.
- After adding eight host-owned RAM metadata checks, package-only fast suite:
  **64 passed, 2 native tests skipped, 0 failed**. The native tests above were not
  repeated for this test-only addition and source whitespace cleanup.
- Consumer assets list all three emulator libraries as packages, not projects;
  host assembly and output checks find none of the forbidden dependencies.
- Package metadata pins exact CPU/Disk versions. Negative diagnostic and
  source-dependency pack attempts fail without emitting a package. A guarded
  Release package succeeds.

The pack logs retain the existing CPU build-tool dependency NU1902 warning for
Microsoft.Build.Tasks.Git 10.0.300. The host's optional Git metadata command also
reports Git unavailable in its build-command environment. Neither is hidden or
treated as an execution defect; build-tool dependency maintenance remains separate.

This is dependency/correctness verification, not a new throughput gate; no new FPS
claim or historical gate disposition follows from it.

## Superseded repository migration checklist (historical)

The following checklist records the abandoned UI-only repository/package split.
It is retained for audit history and is not an open current-work list.

1. Commit the scoped boundary change without unrelated work.
2. Choose the new repository owner/name and visibility, then create it with
   authorization. Preserve the app's history where practical.
3. Move the app, profiles/assets, focused host tests and app documentation. The
   focused tests currently link four files from CopperScreen.Tests; include those
   in the move or place them directly in the new test project. Do not move the
   historical Legacy test project and its execution dependency graph.
4. Publish the tested library package versions to the chosen feed; select final
   versions and change standalone default to package mode. Include license,
   notices, solution, CI and application packaging in the new repository.
5. Verify a clean checkout using packages, then repeat native boot/gameplay,
   input, sound, reset and media smoke checks before releasing that checkout.

CopperStart restoration remains separate. Do not add automatic engine fallback
or restore Legacy dependencies to the common host to make that work compile.
