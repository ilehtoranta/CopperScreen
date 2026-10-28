# Native CopperOS A1200 guest boot profile

This diagnostic executable profiles the actual bundled A1200 ROM/HDF pair in
modeled guest cycles. It does not measure host throughput, physical A1200 timing,
or release performance acceptance. Production execution has no added hooks.

Arguments, in order:

1. Combined 1 MiB A1200 ROM.
2. Raw DOS1 FFS HDF (`DH0:2:32:0:255`), opened read-only.
3. That ROM's compiler map.
4. Exact `L:CopperGUI` HUNK.
5. That GUI's compiler map.
6. Exact `L:CopperHome` HUNK.
7. That desktop's compiler map.
8. Output directory.
9. `profile` or `control`.
10. Optional graphics emitter map (`ADDRESS SIZE CopperGraphics.symbol`).
11. Optional `tab` scenario (requires the graphics map).

Build with `dotnet build scripts/probes/CopperOsBoot/CopperOsBoot.csproj -c Release`.
Run the resulting executable once in each mode with identical inputs. `control`
uses public `ExecuteFrame`; `profile` binds the current private scalar-loop
methods and preserves both outer and inner interrupt dispatch points. This tool
must be reviewed when the engine loop changes; do not adapt it by changing
interrupt sampling. RAM modules are recognized by their image prefix and
relocated native SDK-base store, then assigned their actual relocated map.

`profile.json` records input hashes, guest cycles, sampled function costs and
boot milestones. Require matching completed fields/cycles, CPU registers, Chip
RAM, framebuffer and PCM hashes between profile and control before interpreting
the profile. Module discovery runs every 20 fields, so loaded-module milestones
are quantized to that interval; public desktop readiness is checked every field.
Function costs exclude time executing their callees and are not inclusive call
durations. Unmapped code remains explicitly unattributed.
The optional graphics map selects protocol v2, attributing the hand-emitted
graphics bank and recording native bitmap-copy argument shapes. V1 records and
their inputs remain unchanged. Graphics maps must belong to the exact ROM;
their hashes are recorded separately. Build-time symbol export is diagnostic
only and must reproduce identical emitted bytes before profiling a baseline.
The observer also counts real BLTSIZE instruction writes and records BLTCON0,
BLTCON1 and BLTSIZE. These remain external diagnostics. Protocol v3 injects raw
Tab after cold readiness, waits for both native selection and completed repaint,
and reports that interaction separately. `seconds` remains cold boot duration;
the final machine state/field count includes the interaction. Profile and control
must use the same scenario. Unmapped page totals identify remaining native
trampolines without assigning them to drawing functions.

Protocol v4 retains the v3 input and completion conditions and adds method-entry
visit counts, a larger exclusive-cost table, and observed return-address call
edges for the redraw interval. Entry visits at internal graphics labels can be
loop iterations; they are not necessarily function calls. Return-address edges
are diagnostic observations, with unmapped/tail-call cases left unattributed.
The v4 baseline must reproduce the earlier v3 final state before its new counters
are used. The normal ExecuteFrame control must still match the profiled run.

Use immutable input copies. Do not profile a shared staging path while changing
its bundle; such a sample is INVALID even if it happens to boot successfully.

The machine configuration matches the normal PAL Alice/Lisa 68EC020 interpreter,
2 MiB Chip RAM, no slow/Fast RAM, empty DF0 and one FFS HDF. The DOS1 type is
explicitly supplied through `AmigaFfsPartition.ParseGeometry`; omission can select
the wrong filesystem and is not a valid boot sample. Original and optimized
2026-10-25 records are described in
[the boot-speed record](../../../docs/COPPEROS_DESKTOP_BOOT_SPEED_2026-10-25.md).
The subsequent drawing optimization is in
[the GUI-speed record](../../../docs/COPPEROS_DESKTOP_GUI_SPEED_2026-10-25.md).
The previous blitter and interactive repaint results are in
[the blitter record](../../../docs/COPPEROS_DESKTOP_BLITTER_2026-10-25.md).
The text DMA and memory-query removal results are in
[the text record](../../../docs/COPPEROS_DESKTOP_TEXT_2026-10-25.md).
The current repeated-validation removal results are in
[the redraw record](../../../docs/COPPEROS_DESKTOP_REDRAW_2026-10-25.md).
