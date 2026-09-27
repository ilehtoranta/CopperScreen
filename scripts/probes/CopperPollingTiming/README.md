# Copper interrupt / CPU polling diagnostic

This standalone probe builds an independently authored ROM, then runs a
clear/check loop with one Copper interrupt per field. It records interrupted
PCs and consumed edge flags in slow RAM. It contains no Kickstart or game bytes.

```powershell
dotnet run --project scripts/probes/CopperPollingTiming -c Release -- artifacts/new-polling-run
dotnet run --project scripts/probes/CopperPollingTiming -c Release -- artifacts/new-020-polling-run 68020
```

The optional CPU argument accepts `68000` (the default), `68ec020`, `68020`,
`68030` or `68040`.
Use a new output directory for each run; existing evidence is never overwritten.
The current engine's pinned Copper68k package must be available for restore.

Each invocation runs 100 fields in scalar and normal modes, checks chip/slow RAM
parity, and saves the ROM, both RAM images and JSON results identifying the CPU,
cycle, final PC, interrupted-PC distribution and consumed-edge count. CPU choice
does not change the generated ROM. The two 020 models use the engine's
experimental `ocs-accelerator-v1` timing policy.

These distributions are diagnostics, not hardware golden values. More consumed
edges does not imply more accurate timing. A WinUAE comparison needs an explicit
CPU/frequency/cache/bus configuration; independently started long-running
captures are not a comparison from identical execution states. See the
[68000 bus-spacing investigation](../../../docs/engine/LOTUS_III_CPU_BUS_TIMING_2026-09-25.md)
and [020 follow-up](../../../docs/engine/LOTUS_III_020_INVESTIGATION_2026-09-26.md).
The [040 fetch correction](../../../docs/engine/LOTUS_III_040_FETCH_2026-09-27.md)
uses the unchanged ROM: the 040 edge count changes from 33 to 51 while the 030
control stays byte-identical. These counts characterize the declared policies;
they do not specify how many edges a physical 040 should consume.
