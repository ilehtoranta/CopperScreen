# CopperOS desktop graphics completion probe

This diagnostic runs the qualified PAL A1200 desktop with 2 MiB Chip RAM and
read-only DH0. It requires local ROM/HDF/GUI/Home files and their matching maps.
It is separate from host-throughput acceptance and production execution.

```powershell
dotnet build scripts/probes/CopperOsGraphicsCompletion/CopperOsGraphicsCompletion.csproj -c Release
dotnet scripts/probes/CopperOsGraphicsCompletion/bin/Release/net10.0/CopperOsGraphicsCompletion.dll ROM HDF RESET_MAP GUI GUI_MAP HOME HOME_MAP OUTPUT profile GRAPHICS_MAP tab
```

Use a fresh output directory for each run. Replace `profile` with `control` to
run normal `ExecuteFrame`; compare readiness, cycles, CPU/RAM, pixels and PCM.
The profile's scalar loop preserves interrupt sampling and hardware advancement.
Instruction/map observations never modify guest state or instrument the engine.

Protocol `copperos-a1200-graphics-completion-v2` retains the historical four-field
Tab press and four-field release. Its `interaction.seconds` therefore has an
eight-field minimum. `drawingBoundaries` independently records exact entry/return
cycles for Home's `DrawChanges`, including callees, hardware stalls and preemption,
plus latency from key-down. Return matching includes the stack pointer. The short
`NativeDesktopProcess::Changed` method only sets dirty state; its return is not
the drawing completion marker. Pending boundaries are reported explicitly.

An optional final integer sets the minimum absolute field count before key-down.
For example, `1000` lets both desktops settle before input and selects the separate
`copperos-a1200-graphics-completion-aligned-v3` protocol. Readiness is still recorded
at its original observation. The historical protocol and earlier evidence stay
unchanged. Scheduling phase still affects latency; lower isolated call costs do
not establish lower desktop latency.

These are instruction-boundary completion measurements, not pixel presentation
latency. Scanout may finish later; shortening the input sequence can capture a
partially updated framebuffer even after the drawing task has completed.

During profiling, `interaction.taskCycles` attributes executed cycles to ThisTask;
STOP time is excluded. It is a read-only diagnostic. A failed run saves
`failed-chip.bin` and reports CPU/Exec state for debugging; a failed capture is
not accepted performance evidence.
