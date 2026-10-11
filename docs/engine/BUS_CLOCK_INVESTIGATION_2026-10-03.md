# Bus and clock optimization investigation — 2026-10-03

The bus path has concrete simplification opportunities. The strongest general
candidate is to distinguish **pending ownership of the following CCK** from
**completed ownership of the current CCK**, using the timestamps already held
by the devices. CPU address classification and repeated clock checks are a
second, narrower target. Neither target had an implemented or measured gain at
this investigation stage. The subsequent
[following-slot prototype](BUS_PHASE_OPTIMIZATION_2026-10-04.md) implements the
first bounded simplification. The later combined following-slot/CPU-transfer/
grant-locality candidate [passes the performance gate](GRANT_LOCALITY_2026-10-04.md);
this investigation’s diagnostic timings remain excluded and individual phase
gains are not isolated.

This investigation keeps the [validated R2 candidate](CHIPSET_REGRESSION_2026-10-03.md)
unchanged. The owner reports background load. The captures below are diagnostic
profiles and generated-code inspection, not FPS measurements or a 1% acceptance
comparison. Pre-ECS recovery verification remains pending at the owner's request.

## Fresh workload evidence

Two complete workloads ran against the frozen R2 engine, Copper68k 1.5.1,
CopperDisk 3.0.0 and the common retained runner on .NET 10.0.12:

- Native Lemmings: 14,520 warmup fields and 3,600 measured fields, with the
  corrected `lemmings-cpu-bus-timing-v1.json` input and retained disk swap.
- Synthetic lores: 600 warmup fields and 7,200 measured fields, Paula DMA and
  wide output enabled.

Both complete identities match the retained CPU, hardware, output, cycle,
pixel and PCM fingerprints, with zero reported steady-state allocations and
no unsupported feature. A separate native generated-code replay matches the
same complete identity. All eight pinned binary/media/input hashes and all
47 frozen engine source hashes match after the investigation.

The `dotnet-sampled-thread-time` profiles use a seven-second interior tail,
ending one second before the last engine sample. The tail fits inside measured
execution with margin. These are approximate exclusive sample shares under
load; inlining and sampling attribution prevent treating them as exact costs
or speedup bounds. No throughput result is inferred from the diagnostic FPS.
There was no formal host-load telemetry or acceptance placement verification.

| Exclusive sample group | Native Lemmings | Synthetic lores |
| --- | ---: | ---: |
| CPU core | 33.09% | No sampled CPU-core leaf |
| Device dispatch and inlined video | 23.86% | 56.11% |
| Clock and CPU bus waits | **10.73%** | **4.98%** |
| Bitplane work outside dispatch | 8.14% | 11.70% |
| Blitter | 8.07% | 3.58% |
| Copper | 1.51% | 10.91% |
| CPU bus and memory helpers | **3.10%** | No sampled leaf in this group |

The windows contain 991 native and 615 lores interval slices. Native
`AdvanceToCpuGrant` is 9.24% exclusive and 47.40% inclusive; the latter includes
necessary device execution and must not be called arbitration overhead.
Likewise, the 56.11% lores dispatch leaf includes inlined rendering. The native
CPU-core group also excludes its child calls into the engine.

Raw traces, full runner output, launch scripts, input hashes and the analysis
script are under `artifacts/bus-clock-investigation-2026-10-03/`. The companion
[JSON record](BUS_CLOCK_INVESTIGATION_2026-10-03.json) preserves the profile
summaries and generated-code identities. Local ROMs and game media remain
untracked.

## First target: queries for a specific physical phase

The arbitration helpers in
[LightweightA500Machine.cs](../../CopperMod.Amiga.Lightweight/LightweightA500Machine.cs)
already distinguish callers' phases, but their device predicates do not:

- `CanCopperOwnOutputSlot`, `CanBitplaneOwnOutputSlot`,
  `CanSpriteOwnOutputSlot` and `CanBlitterAdvanceControl` ask about
  `clock.Cycle + 2`. Device `OwnsOutputSlot` predicates test both a pending
  output and the last completed output. On the monotonic owner clock, a
  completed timestamp cannot equal that following CCK. Its comparison is
  redundant in these calls.
- `IsCpuOutputOwned` runs after clock advancement has retired due devices.
  `IsHigherPriorityOutputOwned` also runs during blitter fairness evaluation,
  after higher-priority devices have stepped. Pending outputs at this current
  CCK should therefore already be represented by completed timestamps. This
  half of the proposed simplification needs a caller/order invariant check
  before implementation.

Use existing pending-output state for following-slot acceptance and existing
completion history for current-slot arbitration. Retain refresh checks,
priority, accepted/canceled outputs and blitter phases that actually use the
bus. Do not add a slot cache, ownership mask, per-CCK stores or a forecast.
Copper WAIT/control behavior remains as implemented; this proposal does not
infer ownership merely from a RAM read.

The following-slot simplification is the best first experiment: its invariant
is narrower, and it applies to bitplane, Copper, sprite and blitter arbitration
even when the CPU is stopped. Removing the current-slot pending checks can be
evaluated separately after proving retirement at every production caller.

## Second target: CPU transfer bookkeeping

`GrantCpuWord` checks one or two CIA byte lanes before classifying the Agnus
bus. Implemented CIA addresses occupy the BFD/BFE regions; chip decode is below
200000, slow RAM starts at C00000, and custom registers occupy DFF000–DFF1FF.
These regions are disjoint, including the address preceding a CIA word lane.
The common chip/slow/custom path can be classified before trying CIA decoding.
Preserve 24-bit aliases, wrap, ROM overlay and unfitted-bank behavior.

An Agnus grant already advances the canonical clock to `grantedCycle`.
Every public CPU read/write then calls `AdvanceHardwareTo(grantedCycle)` again.
The generated native `ReadWord` still emits this redundant compare/branch
at block `IG50`, offset `0393`. A grant operation whose explicit contract
includes advancement to the data strobe could absorb the required CIA and
non-Agnus advancement and remove the duplicate check at its callers.

The memory effect must still occur at the grant, followed by advancement to
completion. Longwords retain two physical word phases and their existing
address spacing. CIA accesses retain their ten-cycle peripheral grid. This
removes host bookkeeping, not emulated clocks or bus contention. It mainly
benefits CPU-active workloads; the synthetic stopped-CPU profile cannot
establish its native benefit.

## Third target: generated-code locality

The native Tier1 dump contains address decoding and the OCS grant loop inlined
into multiple CPU read/write callbacks. The shared source method is not one
shared native body at every call site.

| Native Tier1 method | Code bytes |
| --- | ---: |
| `GrantCpuWord` | 1,187 |
| `ReadWord` | 1,709 |
| `ReadByte` | 1,508 |
| `WriteWord` | 2,014 |
| `WriteByte` | 2,538 |
| `ReadLong` | 2,974 |
| `WriteLong` | 3,601 |
| OCS `AdvanceToCpuGrant<TPath>` | 3,706 |

Inspect a bounded inlining boundary or outline uncommon address/device work
after the bookkeeping simplifications. Smaller code may improve locality but
adds call/return and spill costs; it is a hypothesis, not a demonstrated cache
bottleneck. Code sizes include cold blocks, and this native JIT layout must not
be compared as throughput evidence with the earlier synthetic JIT layout.

## Work already avoided and verification needed

The current grant loop already stays within physical CCK completion while the
CPU waits. `RefreshNextDeviceCycle(afterDeviceTick: true)` already skips the
eight-device minimum scan when active video supplies the next CCK. The standalone
`LightweightBusArbiter.GrantCpuWord` helper is not the production machine's
grant path. Replacing these with a generic event queue, schedule table or
speculative batching is not supported by this investigation.

A prototype must preserve current/following-slot equivalence across refresh,
accepted DMA, DMA disable/cancellation, CPU read strobes, nice/nasty fairness,
partial CPU phases, line/field boundaries, external synchronization and ECS/AGA
timing. Use the existing discriminating bus, DMA and CIA tests, add a focused
phase-invariant check where needed, then run the production Release build,
isolated engine diagnostics, disk/host checks and affected retained native
replays. No engine code changed here, so emulator suites were not rerun merely
to write this record.

Keep experiments separate and compare each frozen candidate with validated R2
when the host is available. Require the complete retained lores, hires and native
Lemmings protocol, matching identities, zero measured allocations, valid
telemetry and a one-sided 95% upper frame-time regression bound no greater than
1% for every workload. No gain estimate or exception is accepted by this record.
