# Detroit AGA native startup probe

## Current qualification candidate

The current worktree pins unpublished `1.5.4-qualified-dev.1`. Its general
logical handler already supports the Detroit EORI operand. The package and
same-binary CPU/consumer validation are recorded in the
[candidate qualification record](../../../docs/engine/COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.md).
With the frozen package in the qualification feed, set `$Rom`, `$Disk1` and
`$Disk2` to the same media described below, then run:

```powershell
dotnet build CopperScreen.slnx -c Release
$gameArgs = @('--rom', $Rom, '--disk', $Disk1, '--disk1', $Disk2,
    '--drives', '2', '--cpu', '68ec020', '--agnus', 'alice', '--denise', 'lisa',
    '--chip-ram-kib', '2048', '--slow-ram-kib', '0', '--frames', '34000',
    '--boot-probe-interval', '200', '--boot-probe-extended',
    '--input-script', 'scripts/probes/Detroit/gameplay-input.json')
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @gameArgs --boot-probe artifacts/qualified-detroit-normal > artifacts/qualified-detroit-normal.log 2>&1
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @gameArgs --scalar-cpu --boot-probe artifacts/qualified-detroit-scalar > artifacts/qualified-detroit-scalar.log 2>&1
python scripts/probes/Detroit/analyze_gameplay.py --normal artifacts/qualified-detroit-normal --scalar artifacts/qualified-detroit-scalar --normal-log artifacts/qualified-detroit-normal.log --scalar-log artifacts/qualified-detroit-scalar.log --cpu-version 1.5.4-qualified-dev.1 --output artifacts/qualified-detroit-summary.json
```

Use fresh output directories. The input script and 34,000-field scope are
unchanged. Inspect the new milestone screens; normal/scalar parity alone does
not establish gameplay.

## Historical corrected development gameplay replay

The original replay pins unpublished `1.5.4-detroit-dev.2`. Pack its original corrected CopperMod
`Copper68k/Copper68k.csproj` in Release into this repository's absolute
`artifacts/detroit-fix-2026-10-08/feed` path before restoring a fresh worktree.
Retain already-built packages; changed content needs a new version.

Set `$Rom`, `$Disk1` and `$Disk2` to the inputs described below:

```powershell
dotnet build CopperScreen.slnx -c Release -p:Copper68kBoundaryVersion=1.5.4-detroit-dev.2 -p:RestoreAdditionalProjectSources=artifacts/detroit-fix-2026-10-08/feed
$gameArgs = @('--rom', $Rom, '--disk', $Disk1, '--disk1', $Disk2,
    '--drives', '2', '--cpu', '68ec020', '--agnus', 'alice', '--denise', 'lisa',
    '--chip-ram-kib', '2048', '--slow-ram-kib', '0', '--frames', '34000',
    '--boot-probe-interval', '200', '--boot-probe-extended',
    '--input-script', 'scripts/probes/Detroit/gameplay-input.json')
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @gameArgs --boot-probe artifacts/detroit-game-normal > artifacts/detroit-game-normal.log 2>&1
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @gameArgs --scalar-cpu --boot-probe artifacts/detroit-game-scalar > artifacts/detroit-game-scalar.log 2>&1
python scripts/probes/Detroit/analyze_gameplay.py --normal artifacts/detroit-game-normal --scalar artifacts/detroit-game-scalar --normal-log artifacts/detroit-game-normal.log --scalar-log artifacts/detroit-game-scalar.log --output artifacts/detroit-game-summary.json
```

Use fresh directories. Inspect milestone contents separately: the analyzer checks
complete output/parity, not the meaning of screens. See
[the correction and gameplay record](../../../docs/engine/DETROIT_AGA_FIX_2026-10-08.md).

## Historical 1.5.3 failing replay

The supplied DLM disk-one release boots its intro, but leaving it reaches an
unsupported Copper68k 1.5.3 A1200 timing form. This is a failing compatibility
probe, not gameplay coverage. See [the result](../../../docs/engine/DETROIT_AGA_2026-10-08.md).

Set `$Rom`, `$Disk1` and `$Disk2` to the local A1200 Kickstart 3.0 revision 39.106
ROM ZIP and the two Detroit AGA disk ZIPs. From the repository root:

```powershell
dotnet build CopperScreen.slnx -c Release -p:Copper68kBoundaryVersion=1.5.3
$machineArgs = @('--rom', $Rom, '--disk', $Disk1, '--disk1', $Disk2,
    '--drives', '2', '--cpu', '68ec020', '--agnus', 'alice', '--denise', 'lisa',
    '--chip-ram-kib', '2048', '--slow-ram-kib', '0', '--frames', '8000',
    '--boot-probe-interval', '200', '--boot-probe-extended',
    '--input-script', 'scripts/probes/Detroit/start-input.json')
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @machineArgs --boot-probe artifacts/detroit-normal
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @machineArgs --scalar-cpu --boot-probe artifacts/detroit-scalar
```

Use fresh capture directories. On the original 1.5.3 package both commands exit unsuccessfully
after 1,996 completed fields at PC `$1F2D4`, opcode `$0A18`:
`EORI.B #$9E,(A0)+`. The exception capture is labelled field 1,996 but its
beam/state describe the partially executed next field. Snapshot `unsupported`
can be null despite the thrown timing exception; retain stderr and the exit code.
Never use unsupported continuation to count this as a successful run.
Rebuild with the default development pin after running this historical control.

For the no-input intro control, omit `--input-script` and use `--frames 6000`.
Native screenshots/RAM/audio and copyrighted ROM/media remain under ignored
local artifacts. Capture-inclusive throughput is not a performance result.
