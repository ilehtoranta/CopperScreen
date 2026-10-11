# ECS/AGA without an inactive-feature cost on OCS

Date: 2026-10-03. Status: **isolated prototype; correctness checks pass;
throughput UNVERIFIED**. Production engine source is unchanged. This record
describes a possible implementation, not a new supported profile or an accepted
performance result.

This record preserves the initial isolated experiment. The subsequent
[integrated regression candidate](CHIPSET_REGRESSION_2026-10-03.md) implements
this approach plus Copper/sprite geometry specialization and scroll caching;
its verification and performance disposition are recorded separately.

Select fitted hardware at machine construction and compile the frequent device
paths for those fixed traits. Keep live guest controls in the existing causal
state machine. Share the clock, bus arbitration, peripheral state and source
implementation across OCS, ECS and AGA.

This can remove work for hardware that is absent. It cannot promise that active
eight-plane, RGB24 or SuperHires output costs the same as a simpler OCS display.
Nor does fewer generated instructions establish a zero-overhead engine: entry
dispatch and code locality still need measurement.

## Why this is the first target

The [idle source-stage investigation](PERFORMANCE_INVESTIGATION_2026-10-03.md#idle-six-round-comparison)
holds the runner and dependencies constant. Current source takes 9.6786% more
lores frame time and 5.4656% more hires frame time than the pre-ECS snapshot.
Those comparisons identify a source cost; they do not assign all of it to one
method or establish the gain available from this prototype.

Inspection finds enhanced beam/display checks on the OCS path, and AGA transfer
width/stride handling inside ordinary 16-bit bitplane and sprite completion.
These are concrete candidates because fitted chipset identity cannot change
during execution. The existing register deadlines and accepted DMA state remain
necessary even when an extension is absent.

## Execution design

| Decision | When to make it | Required behavior |
| --- | --- | --- |
| Fitted Agnus and Denise/Lisa capabilities | Construction; configuration changes restart the machine | Preserve independent chip identities and supported mixed configurations. |
| OCS/ECS/AGA hot code path | Select at clock entry; compile fixed traits into the loop | No chipset selector, delegate, boxing or virtual media/device call added per CCK. |
| Resolution, plane count, scroll, palette and priority | Existing effective register-input phase | Mid-line CPU/Copper writes retain their implemented deadlines. |
| DMA address, FMODE and transfer width | Existing acceptance phase | A later control write cannot change an already accepted transfer. RAM remains sampled at output. |
| Programmable beam geometry | Existing beam-control deadline | ECS/AGA retain variable timing; OCS keeps fixed PAL geometry and its external-sync behavior. |
| Optional extension storage | Construction | Allocate only for fitted hardware; no allocation on display or DMA execution. |

The prototype uses readonly value-type traits and constrained generic methods:

```csharp
internal interface ILightweightExecutionPath
{
    static abstract bool EnhancedDisplay { get; }
    static abstract bool Aga { get; }
}

internal readonly struct OcsExecutionPath : ILightweightExecutionPath
{
    public static bool EnhancedDisplay => false;
    public static bool Aga => false;
}

// Shared implementation, specialized by the JIT for each value-type path.
internal void TickDevices<TPath>(long cycle)
    where TPath : struct, ILightweightExecutionPath
{
    // Existing device order and deadlines; fixed traits guard extensions.
}
```

Matching ECS and AGA traits supply `true/false` and `true/true`. One immutable
selector chooses `AdvanceTo<TPath>` and `AdvanceToCpuGrant<TPath>` at their entry;
the type flows through CCK completion, device dispatch, video, bitplane DMA and
sprite DMA. This is shared source with specialized hot methods, not three copies
of the machine. Copper68k and CopperDisk boundaries stay unchanged.

The two traits are a minimal experiment. The enhanced path still checks the
individual fitted ECS Agnus/Denise flags where their behavior differs. A final
implementation must preserve those identities rather than assume that an ECS
Denise implies an ECS Agnus. Specialize only supported combinations and useful
hot methods to control generated code size.

The OCS DMA completion paths use the existing standard-word acceptance and
constant two-byte stride. They omit FMODE decoding, wide reads, variable shifts
and scan-doubling checks. AGA retains captured FMODE, wide payloads and the live
beam-dependent modulo decision at its original output phase. BPLCON0 and FMODE
remain independently pending controls. Cold field-finalization and manual data
writes retain their chipset-aware handling.

The machine still has one canonical integer clock and one hardware owner.
External-sync holds, accepted outputs, CPU grants and device ordering are
preserved. No scheduler, extra timeline, speculative work or diagnostic polling
is introduced. The public API is unchanged.

## Generated-code evidence

Release Tier1 code captured on the audited Ryzen 5 5600X / .NET 10.0.12 host:

| Method | Reference bytes | Prototype OCS bytes | Observed change |
| --- | ---: | ---: | --- |
| Machine `TickDevices` | 3,756 | 3,716 | Enhanced prior-raster call eliminated from the OCS path. |
| Bitplane `CompleteOutput` | 787 | 530 | No AGA wide-transfer calls or variable-width shift. |
| Sprite DMA `CompleteOutput` | 909 | 683 | No AGA wide-transfer calls or variable-width shift. |
| Clock `AdvanceToCpuGrant` entry | 1,266 | 1,197 | Prototype entry still selects a path and may inline part of it. |
| Separate OCS generic `AdvanceToCpuGrant` body | Not applicable | 1,131 | Additional compiled body; entry and body sizes are not a combined reduction. |

This confirms constant folding in the inspected OCS methods. It does not measure
execution time, whole-program code size or instruction-cache behavior. The
remaining per-entry selection is especially relevant because CPU grants are
frequent. Avoid adding an indirect callback just to hide that selection; compare
the emitted direct-entry alternatives and measure the complete workload.

## Correctness verification

The isolated Release engine build passes with zero warnings/errors. Its separate
diagnostic test run passes **1,080 tests**, zero failed and zero skipped.

Four complete OCS retained-workload replays preserve their independently recorded
current identities: lores and hires each use 600 warm-up / 7,200 measured fields;
native Lemmings uses 14,520 warm-up / 3,600 measured fields in both conservative
batch and scalar modes. CPU, hardware, output, cycle and audio identities match,
with zero measured allocation and no unsupported continuation. Their timings are
excluded because these are correctness replays under concurrent host activity.

Three existing native display fixtures run in reference, prototype and prototype
scalar modes:

| Fixture | Fields per run | Captured files per run | Result |
| --- | ---: | ---: | --- |
| ECS PAL SuperHires, Kickstart 3.1 | 2,400 | 30 | All files and final hardfile bytes match. |
| ECS programmed NTSC SuperHires, Kickstart 3.1 | 1,200 | 18 | All files and final hardfile bytes match. |
| AGA 256-colour RGB24, Kickstart 3.0 / EC020 | 3,600 | 43 | All files and final hardfile bytes match. |

All nine runs complete. The 182 file comparisons cover reference-to-prototype
and prototype-to-scalar images, state, RAM, PCM and display-profile sidecars.
Each run uses a disposable copy of its pristine hardfile. Native inputs and
hashes are retained locally; no ROM or media is included in tracked changes.

These are bounded parity checks against previously validated native fixtures,
not new independent hardware measurements or comprehensive ECS/AGA conformance.
The [ECS display record](ECS_DISPLAY_2026-10-01.md),
[initial AGA evidence](AGA_INITIAL_2026-10-01.md) and
[issue register](ISSUES.md) retain their original limits, including unresolved
physical transition timing and broader compatibility.

## Throughput disposition

Two fresh attempts at `chipset-specialization-probe-v1` are **INVALID / RERUN**.
The intended diagnostic has two balanced pairs for each retained workload, with
the audited homogeneous-host CPU 2 placement, Normal priority and unchanged
host-load policy v2. Active competing .NET build/test jobs invalidate both
attempts. Ordinary background process presence is not the reason.

The first attempt completes only reference lores D1 before the candidate run is
interrupted; the retry is interrupted during reference lores D1. Incomplete
samples are neither pooled nor used for a speedup or regression estimate. Both
attempts, scripts, topology, hashes and telemetry remain available locally.
Even a complete two-pair diagnostic would not replace the formal gate.

Before promoting a production candidate, freeze its complete binaries and run
the separately versioned, validated current-host six-pair retention comparison
for lores, hires and native Lemmings, without concurrent builds/tests. Require
matching complete identities, zero measured steady-state allocations, valid
telemetry and the **one-sided 95% upper frame-time regression bound <= 1% for
each workload**. Ask acceptance if a valid bound exceeds that limit. Previous
exceptions do not apply; historical hybrid-host protocols remain unchanged.

For this task's stricter aim of no added OCS cost, report the paired change and
uncertainty explicitly rather than equating a passing 1% gate with zero overhead.
Measure active ECS/AGA workloads separately at fixed modes and complete outputs.

## Remaining concrete targets

1. Extend the fixed-trait path to Copper input classification. OCS uses 227 CCK
   per PAL line, while `ClassifyInput` currently receives dynamic geometry and
   retains variable-length arithmetic. Keep variable geometry in the enhanced
   path and preserve the external-sync phase adjustment in both.
2. Predecode effective scroll/resolution controls at the existing BPLCON0 and
   BPLCON1 input deadlines. `ShiftPlayfieldPixel` and `ShiftAgaPixel` repeatedly
   decode these values at reload. Width-dependent masks must still use the
   accepted payload width, not a later FMODE write.
3. Separate unfitted extension register decoding from common OCS writes while
   retaining exact masks, shared CPU/Copper side effects, strobes and delayed
   inputs. Measure Copper-heavy workloads as well as the retained set.
4. Specialize fixed OCS sprite/request geometry where it remains in the shared
   scheduling helpers. Do not cache a future slot or advance a device early.
5. Allocate enhanced video arrays only for fitted hardware. OCS currently creates
   2,752 bytes of AGA array payload plus 64 bytes of ECS colour payload, excluding
   array headers, and clears/rebuilds AGA data on reset. This is a construction
   and reset improvement; it is not evidence of a steady-state FPS gain.

Implement and compare these in small stages. Inspect generated code for removed
work and accidental code growth, then retain only changes supported by full
correctness and valid paired measurements. Active rich display work remains
necessary; removing inactive feature checks is the first achievable objective.

## Reproducibility and local artifacts

Reference source: `d9beae8b88be24032221e3482942a249c03c27d3`. Dependencies:
Copper68k `1.5.1`, CopperDisk `3.0.0`. The source-only prototype patch and all
build/test/replay evidence are under ignored
`artifacts/chipset-specialization-2026-10-03/`; production engine files were not
edited. A compact tracked [evidence manifest](CHIPSET_SPECIALIZATION_2026-10-03.json)
preserves hashes, verification counts and dispositions.

- `source.zip`, `source/`, `prototype.patch`, `prototype-source-hashes.json`:
  archived reference and seven modified/new prototype files.
- `release-build.log`, `engine-tests.log` and the isolated `engine.trx`: build
  and diagnostic outcomes.
- `candidate-jit.txt`, `reference-clock-jit.txt`, extracted Tier1 assembly and
  `jit-summary.json`: generated-code inspection, excluded from timing.
- `retained-correctness.json` and four `correctness-*.log` files: complete OCS
  identities and allocation results.
- `native/parity.json`, fixture logs, disposable hardfiles and captures: native
  inputs, scripts/options, hashes and nine replay outcomes.
- `performance-probe-v1/` and `performance-probe-retry-1/`: invalid attempts;
  `INVALID.txt`, frozen identities, scripts and host telemetry are preserved.

No package publication, source pin change, production engine integration or
performance acceptance is part of this investigation.
