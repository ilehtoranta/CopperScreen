# Removing repeated hires mode selection — 2026-11-03

The first cleanup from the
[Lemmings bottleneck investigation](LEMMINGS_BOTTLENECKS_2026-11-03.md) selects
single- versus dual-playfield mode once for an ordinary visible hires CCK.
Previously, each of its two pixel-pair operations repeated that selection.
The shared single-playfield helper retains the original compositor and writes;
window-boundary pairs keep individual mode selection.

**Correctness PASS; native comparison VALID.** The paired FPS estimate is
**+0.2172%**, with a two-sided 95% interval of **−0.8976% to +1.3446%**: no
speedup or slowdown is established. The one-sided 95% upper frame-time change is
**+0.6621%**, within the native 1% limit. Complete three-workload retention and
release acceptance remain unmeasured for this change.

[`AGENTS.md`](../../AGENTS.md) now states the Lightweight execution principles
explicitly: keep one owner/clock, preserve required hardware work, exclude unused
optional metadata, specialize fitted traits outside recurring work, inspect
generated code as well as allocations, and measure throughput separately.
The existing ownership, build, package and measurement rules remain in force.

This change adds no cache, mirrored device state, counter, callback or CPU-package
change. Inactive/masked IPL sampling remains intact. Both sprite comparator,
collision and shifter operations still execute in their original order, with
the same four output pixels. Register-input, shift/reload, display-window and
border phases are unchanged. Only the mode decision spans the proven stable CCK.

## Validation

- Production Release solution build passes with **zero warnings/errors**, using
  `--artifacts-path artifacts/lightweight-hires-20261103/build -m:1`.
- **174 focused engine cases pass**, with zero failures/skips. Existing cases
  cover hires fetch/output, attached sprites, second-subpixel priority/collision,
  dual playfields, window edges, scroll/reload, ECS display and repeated interrupt
  sampling. Tests use the separate `artifacts/diagnostic-tests` outputs.
- Normal and scalar full **18,120-field native Lemmings captures** each match all
  **160 retained files**, including BMP pixels, JSON CPU/device state, Chip/slow
  RAM and real stereo PCM. There are zero file mismatches or unsupported features.
- The full **600-warmup/7,200-field synthetic hires** workload matches its retained
  complete identity with zero measured steady-state allocations in both capsules.

Native captures use zero warmup and include capture allocation/I/O. Their output
hash is `0x72FF28A9A6D931E7`, separate from the benchmark's accumulated measured
output hash. Their CPU, hardware and final cycles remain `0x0BC2B7554DA315AB`,
`0x8F41668D6DF17C3C` and `2574756906`. Capture FPS is excluded from performance.

## Generated code

Selected JIT disassembly runs separately from throughput, under normal .NET
10.0.12 tiering/PGO defaults on the same frozen Release capsules. The inspected
workload is full synthetic hires, not a hardware-cycle cost measurement.

| `RenderHiresCck` Tier1 body | Reference | Candidate |
| --- | ---: | ---: |
| Native bytes | 2,323 | 2,238 |
| Dual-playfield mode tests | 2 | 1 |
| Stack reservation, excluding saved registers | 136 bytes | 120 bytes |
| Conditional branches | 72 | 71 |
| Calls | 12 | 12 |

The helper inlines in this inspected body; the cleanup adds no surviving helper
call. Different workloads and PGO observations can produce different body sizes.
These numbers verify removal of redundant work, not an FPS gain.

An earlier shared-local prototype passed the focused cases but enlarged code
and stack use by keeping more values live across sprite calls. The retained
change is narrower. Prototype evidence remains separate under
`prototype-shared-setup/`. One subsequent disassembly attempt appended to that
prototype's output and failed the single-body check; a fresh `candidate-mode`
capture supplies the accepted candidate code evidence.

## Native throughput comparison

The separately versioned
[`measure-lightweight-hires-v1.ps1`](../../scripts/measure-lightweight-hires-v1.ps1)
compares six balanced engine-only pairs, ordered
R1/C1/C2/R2/R3/C3/C4/R4/R5/C5/C6/R6. Runner, CPU/disk dependencies, runtime
configuration and all other capsule files match byte for byte; only the engine
DLL differs. Qualification binds the 174 cases, 320 matching capture files,
generated-code inspections, sources and both frozen capsules before timing.

The workload retains 14,520 warmup plus 3,600 measured fields, full 908×313
pixels, real stereo PCM and the existing ROM/media/input script. The expected
complete benchmark identity comes from the independently retained current
correctness contract. No new golden is learned from the candidate.

The homogeneous-host protocol rechecks topology and retains CPU 2, protected SMT
pair 2–3, Normal priority, Balanced power and .NET 10.0.12, with host-load policy
v2, native counters and preflight/cooldowns. The owner has authorized diagnostics
under current load; all samples and any violations are retained. No emulator
build, test or profile runs concurrently with timing.

All twelve complete identities match with zero measured steady-state allocations.
Independent analysis reproduces the recorded statistics, rechecks every raw
result and frozen file, and reapplies policy v2 to all **725 telemetry intervals**:
**zero violations and zero competing-job intervals**. Peak selected-CPU other
activity is 9.09%, sibling activity 12.35%, and package interference 30.09%.
The package peak is transient; the sustained rejection threshold is not reached.

| Pair | Reference FPS | Candidate FPS |
| --- | ---: | ---: |
| 1 | 327.64 | 331.47 |
| 2 | 328.83 | 326.37 |
| 3 | 325.71 | 324.67 |
| 4 | 327.15 | 332.79 |
| 5 | 331.32 | 332.72 |
| 6 | 331.79 | 328.75 |
| Arithmetic mean | 328.7400 | 329.4617 |

Paired changes use geometric ratios, not a ratio of these arithmetic means.
Frame-time ratios are reference FPS / candidate FPS; the paired frame-time point
estimate is −0.2168%. Student-t intervals on the six log ratios use df=5,
2.0150483733330233 for the declared one-sided 95% bound and 2.570581835636305
for the separately reported two-sided 95% interval. No samples are trimmed or
pooled with other series. The native-only upper bound is within 1%; it does not
establish the complete three-workload retention or release performance gate.

The retained change removes verified redundant work with less generated code
and stack use. A measurable native FPS benefit is not claimed.

Raw evidence is under `artifacts/lightweight-hires-20261103/`. The
[machine-readable record](HIRES_MODE_SELECTION_2026-11-03.json) binds the final
results and evidence hashes. Copper68k's unused publication-context copies and
the active OCS bitplane candidate remain separate follow-up work.
