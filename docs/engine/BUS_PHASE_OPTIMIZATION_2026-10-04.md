# Following-slot bus optimization — 2026-10-04

Work started on 2026-10-03 and completed on 2026-10-04 in Europe/Helsinki.
Local artifact paths retain the starting date.

The first [bus investigation](BUS_CLOCK_INVESTIGATION_2026-10-03.md) target is
implemented as a bounded prototype. Following-CCK DMA acceptance queries check
retained **pending bus transfers**, without comparing completed-transfer history.
Correctness and generated-code checks pass. **Performance acceptance is pending**
because the owner reports background load; no speedup or passing 1% gate is claimed.

The reference is the unchanged, [validated chipset-regression R2 build](CHIPSET_REGRESSION_2026-10-03.md).
Its frozen binary remains available. Its passing performance result applies to R2,
not to this subsequent candidate. Pre-ECS recovery remains pending at the owner's
request.

The later [complete grant-locality comparison](GRANT_LOCALITY_2026-10-04.md)
includes this change and **passes all three 1% upper-bound gates** against
validated chipset R2. The frozen standalone variant described here remains
unmeasured; its individual contribution is not isolated. The pending language
below records that earlier stage, while the linked combined result is current.

## Change and physical-phase contract

`CanCopperOwnOutputSlot`, `CanBitplaneOwnOutputSlot`, `CanSpriteOwnOutputSlot`
and `CanBlitterAdvanceControl` ask about the following CCK on the monotonic owner
clock. A completed transfer cannot own that future timestamp. Five small inline
device predicates retain the existing pending-cycle, pending-valid and bus-use
checks while omitting completion history. Disk acceptance already used its
pending timestamp and is unchanged.

This removes 12 completed-history comparisons across the four source queries.
It adds no device fields, per-CCK stores, allocation, event queue or slot cache.
The clock, transfer phases, device priority, refresh rules, cancellation behavior,
CPU ownership/fairness queries and completion histories are unchanged. In
particular, pending blitter control/idle phases still do not claim the bus.
CPU transaction bookkeeping and inlining-boundary experiments remain separate.

The added `LineIdleOutputLeavesFollowingSlotAvailableToSprites` check passes
before and after the optimization. It distinguishes a pending internal line
phase, the following physical C read, and a completed C read followed by another
internal phase. It also retains current-slot CPU ownership of that completed read.

Four cases in the existing AGA wide-sprite theory queried the current completed
slot through a helper whose existing assertion required the following slot.
They now assert current ownership through `IsHigherPriorityOutputOwned`, plus
availability of the following slot after DMA disable. Accepted address, RAM data,
fetch mode, stride and pointer expectations are unchanged. The first failing run
and the corrected passing run are both retained.

## Verification

| Check | Result |
| --- | --- |
| Production `CopperScreen.slnx` Release build | PASS, zero warnings/errors |
| Isolated engine diagnostics | **1,081 passed**, zero failed/skipped |
| CopperDisk tests | **74 passed**, zero failed/skipped |
| Host coverage, including native fixtures | **163 distinct cases passed**, same case set as R2 |
| Retained lores, hires, native Lemmings and scalar Lemmings | Complete identities match; zero measured allocations |
| ECS SuperHires, programmed NTSC SuperHires and AGA RGB24, normal/scalar | **182 captured-file comparisons match**, all six final disposable hardfiles match |

Host verification was partitioned: 152 ordinary/Lemmings cases, three native
Workbench-floppy/A1200 cases and eight native Workbench hard-disk cases. The
first discovery omitted optional native environment variables; the expanded
cases subsequently ran. An initial HD attempt rejected the older `B2938CAE…`
fixture at its input-hash precondition before guest execution. The existing
pinned v2 `19A78C83…` fixture passes all eight cases. No expected media or output
identity was rebased. Raw discovery/failure logs remain available with their
resolutions in the [JSON record](BUS_PHASE_OPTIMIZATION_2026-10-04.json).

All 47 candidate engine source hashes and both frozen capsule manifests verify
after validation. Six engine files differ from R2. The retained comparison
runner, dependencies and other common input files are identical; only engine
DLL/PDB differ. The comparison capsules retain R2's common XML documentation
file, as in the earlier harness; current generated XML is preserved separately
and does not execute. The candidate engine DLL SHA-256 is
`344A003D44377FAD0825E03187058334590BE7A1A80F839B3C612493F88D4AF5`.

## Generated code and pending throughput gate

Two complete diagnostic lores replays captured Tier1 code on .NET 10.0.12 using
the frozen R2 and candidate capsules with the same retained inputs. Both full
identities match. Background load and concurrent correctness checks make their
timings unsuitable for throughput conclusions.

| OCS arbitration method | R2 bytes → candidate bytes | Static conditional branches |
| --- | ---: | ---: |
| Copper `AdvanceControlInput` | 596 → 496 | 30 → 21 |
| Copper `TryAcceptWord` | 118 → 98 | 9 → 6 |
| Blitter `AdvanceStartup` | 272 → 214 | 16 → 12 |
| Blitter `TryAcceptPhase` | 831 → 779 | 48 → 44 |
| Blitter `TryAcceptLinePhase` | 739 → 668 | 46 → 42 |
| Sprite DMA `TryAcceptInput` | 785 → 740 | 38 → 34 |
| Bitplanes `TryAcceptInput` | 240 → 219 | 10 → 9 |

The affected paths lose history checks without added calls. The main
`TickDevices<OCS>` body grows from 3,656 to 3,675 bytes and from 127 to 128 static
conditional branches in this capture. Other bodies show small layout differences.
Static code size and branch counts include cold paths and establish neither
overall speed nor cache/branch miss rates.

The prepared [bus-phase comparison v1](../../scripts/run-lightweight-bus-phase-comparison-v1.ps1)
retains the validated homogeneous-host v2 protocol's workloads, six-pair order,
placement, Normal priority, complete fingerprints, runtime pins, rejection
checks and host-load policy. Only descriptive labels and the frozen engine pair
change. Its AST parses; live topology/preflight and the complete comparison
remain pending until the host is available.

Require matching complete identities, zero measured allocations, valid telemetry
and a **one-sided 95% upper frame-time regression bound ≤1% on every retained
workload**. Earlier exceptions do not transfer. Raw builds, tests, replay scripts,
captures, hashes, the relative-to-R2 patch and JIT comparison are under
`artifacts/bus-phase-optimization-2026-10-03/`.
