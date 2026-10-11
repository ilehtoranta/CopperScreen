# Kick Off 3 AGA blank-field investigation

These host tools reconstruct a bounded Practice-menu replay for the supplied
DLM disk set. They do not patch the guest, alter timing, or establish football
gameplay. The historical October 3 input script is unavailable; this is a new,
explicitly versioned input sequence. Results belong in
[the investigation record](../../../docs/engine/KICK_OFF_3_BLANK_FIELDS_2026-10-08.md).

Use native A1200 Kickstart 3.0 revision 39.106, PAL, 68EC020, Alice/Lisa,
2 MiB Chip RAM and no Slow/Fast RAM. ROMs and game media are local inputs,
never repository files. Python tools use only its standard library.

## Reproduction

Set `$Rom`, `$Disk1` and `$Disk2` to the supplied ROM and DLM disk archives.
From the repository root:

```powershell
python scripts/probes/KickOff3/prepare_replay.py --disk2 $Disk2 --output artifacts/kickoff-input.json
python scripts/probes/KickOff3/prepare_diagnostics.py --output artifacts/kickoff-diagnostic
dotnet build artifacts/kickoff-diagnostic/runner/CopperMod.Amiga.Lightweight.Runner.csproj -c Release --artifacts-path artifacts/kickoff-build
dotnet build CopperMod.Amiga.Lightweight.Runner/CopperMod.Amiga.Lightweight.Runner.csproj -c Release

$machineArgs = @('--rom', $Rom, '--disk', $Disk1, '--drives', '2',
    '--cpu', '68ec020', '--agnus', 'alice', '--denise', 'lisa',
    '--chip-ram-kib', '2048', '--slow-ram-kib', '0', '--frames', '15000',
    '--boot-probe-interval', '100', '--boot-probe-extended',
    '--input-script', (Resolve-Path artifacts/kickoff-input.json).Path)
$env:KICKOFF_TRACE = Join-Path (Get-Location).Path 'artifacts/kickoff-trace.csv'
dotnet artifacts/kickoff-build/bin/CopperMod.Amiga.Lightweight.Runner/release/CopperMod.Amiga.Lightweight.Runner.dll @machineArgs --boot-probe artifacts/kickoff-normal
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll @machineArgs --scalar-cpu --boot-probe artifacts/kickoff-scalar
python scripts/probes/KickOff3/analyze_replay.py --trace artifacts/kickoff-trace.csv --normal artifacts/kickoff-normal --scalar artifacts/kickoff-scalar --output artifacts/kickoff-summary.json
```

The diagnostic preparer requires a new directory under ignored `artifacts/`.
It copies the current engine and runner, then inserts bounded logging into
custom-register writes, successful bitplane output and completed-field handling.
The production source and package pin are unchanged. Its markers fail visibly
if source changes invalidate instrumentation; it never overwrites prior evidence.
Diagnostic execution is not suitable for packaging or performance measurement.

## Trace interpretation

- `W`: upcoming completed field, owner cycle, beam line/CCK, CPU or Copper,
  CPU PC, register offset/value and Copper PC. Values are the submitted writes.
- `F`: completed field, owner cycle, CPU PC, DMACON, BPLCON0, fetch count,
  nonzero fetch count, per-plane counts, BPL1 pointer and Copper PC.
- `R`: completed field and the number of nonblack RGB samples at every sixteenth
  framebuffer element. This distinguishes large blank intervals; sparse pixels
  could escape the sampler, so it is not a complete pixel-conformance check.

`F` covers fields 6,001–15,000; `W` and `R` cover 8,801–15,000. Palette writes
are included. The analyzer requires complete, unique field coverage and native
snapshots through field 15,000, requires the field 9,500 cursor inside the guest's
Practice hit rectangle and Practice menu ID `$15` at every captured field from
10,000 onward, rejects unsupported execution, reports black/low-output ranges,
and compares every capture file against the unmodified scalar runner. Comparison establishes
probe parity and execution determinism, not independent hardware correctness.
