# Lotus III on experimental 020: lost fire edge — 2026-09-26

Follow-up: the [Chip RAM cache and decoder correction](LOTUS_III_020_CACHE_2026-09-26.md)
resolves this prompt stall. The `.50` findings and failed experiments below remain
the evidence from this earlier checkpoint.

The disk-two stall is localized to Lotus's fire-edge polling loop. The emulated
input reaches the game, but its main loop clears the new edge before consuming
it. The traced presses do not reach the subsequent disk-ID check. The underlying
020 instruction/bus/interrupt timing discrepancy was left **OPEN** here; this
investigation does not establish a justified timing correction or gameplay pass.

This follows the [experimental CPU implementation](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md)
using unpublished `Copper68k 1.4.2-ocs020.50`. There are no production CPU,
chipset, disk or input changes in this investigation. The retained
[68000 bus-spacing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md) is unchanged.

## What the native trace establishes

The observer runs outside the engine, using its existing instruction/interrupt
boundary methods only around the scripted press windows. Normal execution
continues elsewhere. Its 68000 field-9,000 and 68020 field-10,000 state JSON,
chip RAM and screenshot bytes match their uninstrumented captures. This bounds
observer parity at those checkpoints, rather than claiming an exhaustive proof.
Source and raw traces remain under `artifacts/lotus3-020-investigation/` and are
bound by the [evidence manifest](LOTUS_III_020_INVESTIGATION_2026-09-26.json).

The relevant guest sequence is:

1. `$69B42` clears the edge byte at `$C0881A`, then calls the polling function.
2. `$741B0` tests bit 4 of that byte; `$741B6` branches using the resulting Z flag.
3. The Copper interrupt reads the fire pin and stores the new edge at `$75C92`.
4. `$741B8` consumes a detected edge by advancing the prompt timer.
5. Only after the prompt completes does `$69B5A` call the disk-ID reader.

| CPU / scripted field | Interrupted PC | Result after the interrupt |
| --- | --- | --- |
| 68000 / 6,500 | `$741C0` | New edge cleared on the next loop; missed |
| 68000 / 8,500 | `$741C0` | New edge cleared on the next loop; missed |
| 68000 / 10,500 | `$741AA` | Bit test sees the edge; `$741B8` consumes it |
| 68020 / 6,500 | `$741B6` | Previous bit test already missed; restored Z remains set |
| 68020 / 8,500 | `$69B42` | Returns directly to the instruction clearing the edge |
| 68020 / 10,500 | `$69B42` | Returns directly to the instruction clearing the edge |

For example, at 68020 field 10,500 the interrupt stores `0x10` in the edge byte
at motherboard cycle `1492186524`. RTE returns to `$69B42` at `1492187858`, and
that instruction clears it. On 68000, the bit test at the corresponding press
sees `0x10`, then reaches `$741B8` at cycle `1492053374`; the timer becomes
`$FFF4`, enabling completion. These are observations of different CPU executions,
not an aligned cross-model first-divergence comparison.

The 020 has `CACR=1` throughout the traced windows; the guest has enabled its
instruction cache. Both pin input and edge generation work. A clear of a nonzero
edge is not necessarily a lost press: the 68000 also clears the byte after
consuming it, so the observer's raw `discarded` counter alone is insufficient.

## Input variants and independent reference

- Moving the first press from field 6,500 to 6,501 still leaves the 68020 at the
  disk-two prompt after 18,000 fields, with the original final fingerprints.
- Twenty joystick-only presses, every five fields from 6,500 through 6,595,
  held for two fields each, also leave the prompt at field 18,000. The observer
  directly traces the first five lost edges; later captures establish the final
  stall. This does not exhaust every possible input schedule.
- WinUAE 6.0.3, with the same ROM/media, PAL OCS, 512 KiB chip + 512 KiB slow RAM,
  68020 at 14,187,580 Hz, CPU compatibility/cycle-exact/memory-cycle-exact enabled
  and JIT disabled, passes the prompt after Return, disk-two insertion and a fire
  press. The next register capture is at `$69CC6` in later game code, followed by
  execution in the disk loader at `$69E`. Runtime configuration queries and
  debugger output are retained. CACR is also `1` in this native reference run.

The WinUAE run uses manually timed IPC input and warp pacing, not the fixed-field
script. It supports media/020 continuation compatibility, but is not identical
hardware, a matching initial cache/prefetch/beam state, or verified driving.
The reference processes created for these checks were closed afterward.

## ROM-free probe and validation

The [Copper polling diagnostic](../../scripts/probes/CopperPollingTiming/README.md)
now accepts an optional CPU choice. All choices generate the same ROM SHA-256:
`B4BBF99E61796E74CA96A880080B944B68D8EE3B3BFA22BA56CE49076426373B`.
Its IRQ sets an edge every field; unlike Lotus, it does not require a new physical
button press. Its instruction cache is disabled, so it does not isolate Lotus's
cache-enabled timing.

| CPU | Fields per mode | Consumed edges | Final cycle / PC | Normal/scalar RAM |
| --- | --- | --- | --- | --- |
| 68000, default argument | 100 | 50 | `14210206` / `$1108` | Identical |
| 68EC020 | 100 | 35 | `14210207` / `$1118` | Identical |
| 68020 | 100 | 35 | `14210207` / `$1118` | Identical |

The 68000 interrupted-PC distribution remains the retained 50/50 `$110A`/`$100A`
result, with byte-identical chip and slow RAM to the earlier retained probe.
Both advanced CPUs share the distribution recorded in the manifest.
WinUAE's independently started 020 probe visits different interrupted PCs.
Neither that difference nor the number of consumed edges establishes which
instruction is wrong. The production Release build and probe's locked restore
pass. Existing engine/host/native regression results remain the earlier
implementation's evidence; these diagnostic-only changes do not rerun or replace
them. No throughput measurement or golden update was performed.

## Next bounded correction at this checkpoint

Capture a small loop from controlled CPU, cache, bus and beam states, including
the interrupt return and the clear/test sequence. Compare native CPU clocks and
actual transfer/interrupt boundaries against an independently specified 020
accelerator bus and the Motorola timing/cache rules. A failure in CPU semantics,
prefetch or instruction timing belongs in CopperMod/Copper68k; a demonstrated OCS
transfer-boundary error belongs in the host adapter. Require a discriminating
regression before changing either. Changing an instruction cost, interrupt phase
or guest code merely to move this prompt would not establish correctness.
