# Recovering OCS throughput after ECS/AGA integration

Date: 2026-10-03. Status: **integrated working-tree candidate; correctness PASS;
current-source retention PASS; pre-ECS recovery verification pending at owner request**. Reference source is
`d9beae8b88be24032221e3482942a249c03c27d3`; Copper68k remains `1.5.1` and
CopperDisk `3.0.0`. No package publication or dependency change is included.

The [source-stage investigation](PERFORMANCE_INVESTIGATION_2026-10-03.md#idle-six-round-comparison)
measures an accumulated OCS frame-time cost of +9.6786% lores and +5.4656% hires
against pre-ECS source with common dependencies. This candidate applies the
[specialization prototype](CHIPSET_SPECIALIZATION_2026-10-03.md) to the active
engine and removes additional unnecessary work. A passing comparison against
current source alone does not prove recovery of the entire pre-ECS difference.

## Implementation and timing contracts

Immutable value-type traits select the OCS, ECS or AGA shared generic clock path
at clock entry. The JIT removes unfitted hardware work from specialized device
methods. Selection does not run once per CCK, allocate, box traits or introduce
a virtual device callback. The machine retains one clock and one owner.

- Standard OCS/ECS bitplane and sprite transfers use a constant 16-bit payload
  and two-byte stride. AGA captures FMODE at acceptance and samples RAM at output,
  preserving wide transfers and the existing modulo phase.
- OCS Copper classification and sprite slot scheduling use fixed PAL geometry.
  Enhanced paths retain programmed geometry and individual Agnus/Denise identity
  checks. External synchronization, beam offsets and accepted deadlines remain
  live state.
- Video uses the fitted renderer path. Standard DMA delivery avoids repeated
  AGA dispatch; manual data writes retain chipset-aware handling.
- Scroll decoding moves to the existing effective BPLCON0/BPLCON1 deadline.
  AGA reload delays are cached without their payload-width mask; that mask uses
  the accepted width at reload. A later FMODE write cannot reinterpret accepted
  data.

There is no CPU execution batching change, future DMA lookup, event scheduler,
device-order change or public API change. Mixed supported chip configurations
retain their separate capabilities. Optional extension arrays and cold register
decoding have not been refactored in this change.

The first integrated candidate R1 includes Copper specialization and effective
scroll caching. R2 additionally specializes sprite slot scheduling. Only R2 is
the current working-tree candidate; R1 evidence remains separately preserved.

## Correctness

| Check | R2 result |
| --- | --- |
| Production Release solution | PASS; zero warnings/errors |
| Isolated engine diagnostics | 1,080 passed; zero failed/skipped |
| CopperDisk tests | 74 passed; zero failed/skipped; disk source unchanged |
| Full native-enabled desktop host suite | 163 passed; zero failed/skipped |
| Complete retained OCS workloads | Lores, hires, native Lemmings batch/scalar identities match; zero measured allocations |
| Native ECS/AGA display fixtures | Six candidate runs; 182 captured-file comparisons and final hardfile hashes match existing reference |

ECS PAL SuperHires runs 2,400 fields per mode; ECS programmed NTSC SuperHires
runs 1,200; AGA RGB24 runs 3,600. Batch and scalar executions match reference
images, state, RAM, PCM and display-profile sidecars. Disposable hardfile copies
preserve their source files. These checks preserve previously validated fixture
behavior; they do not establish comprehensive ECS/AGA hardware conformance.
The [issue register](ISSUES.md) and earlier native evidence retain their limits.

R1's first native-enabled host invocation rejected an older Workbench hardfile
hash before guest boot. The required v2 pristine image was located and the
affected tests passed on retry. R2 subsequently passes all 163 host tests in one
clean invocation with the pinned inputs. Failed-input evidence is preserved;
expected identities were not changed.

## Generated code

Release Tier1 inspection on Ryzen 5 5600X / .NET 10.0.12:

| Method | Current reference bytes | R2 OCS bytes |
| --- | ---: | ---: |
| Machine device dispatch | 3,756 | 3,685 |
| Bitplane completion | 787 | 530 |
| Sprite DMA completion | 909 | 684 |
| Sprite input scheduling | 276 | 185 |

The inspected OCS DMA methods omit AGA wide-transfer calls and variable-width
shifts. Sprite scheduling omits variable geometry division. Smaller individual
methods do not establish whole-program locality or FPS: enhanced specializations
add code, and clock entry still selects a path.

## Performance disposition

The R2 comparison uses six balanced pairs for each complete lores, hires and
native Lemmings workload, with identical frozen runner and dependencies. It
pins .NET 10.0.12, uses audited homogeneous-host logical CPU 2 / protected SMT
pair 2,3, Normal priority and unchanged host-load policy v2. Identity, placement,
telemetry and negative rejection probes precede measurement. No builds, tests
or profiling run concurrently with timing.

Acceptance requires all complete identities, zero measured steady-state
allocations, valid telemetry, immutable inputs/capsules and **every one-sided
95% upper frame-time regression bound <= 1%**. Previous exceptions do not apply.
Historical hybrid-host protocols remain unchanged. Supplementary comparisons
against pre-ECS source must be separately identified rather than mixing samples
from different reference builds or timing windows.

Both R1 timing attempts are **INVALID / RERUN**: measured sibling CPU activity
stays above 25% for 10.4599954 and 10.5133816 seconds respectively. The first
completes eight lores samples; the retry completes four. Their partial samples
are excluded and never pooled into a speedup estimate. Ordinary application
presence was not the cause. R2 uses a fresh evidence directory after the owner
requested pausing competing work and rerunning.

The first R2 attempt is also **INVALID / RERUN**: a competing `dotnet` build/test
process appears during lores C6, after ten completed samples. Those samples are
excluded. After that process exits, a fresh R2 retry starts in a separate
evidence directory.

The R2 retry completes **VALID / PASS**: all 36 samples preserve the complete
identities and zero measured allocations. Independent recomputation agrees
with the protocol statistics and validates all 1,484 telemetry intervals.
Maximum sustained interference is 0 seconds selected CPU, 1.269437 seconds
sibling and 1.303010 seconds package, below the unchanged 10-second threshold.

| Workload | Current reference mean FPS | R2 mean FPS | Paired throughput gain | Paired frame-time change | One-sided 95% upper change | Gate |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Lores | 342.31 | 354.06 | +3.4316% | -3.3177% | -2.2619% | PASS |
| Hires | 316.64 | 330.01 | +4.2319% | -4.0601% | -2.6499% | PASS |
| Native Lemmings | 326.12 | 330.41 | +1.3155% | -1.2984% | +0.0117% | PASS |

The six paired log frame-time ratios use a one-sided Student t bound with five
degrees of freedom. FPS columns are arithmetic means; the paired percentages
are geometric estimates. No trimming, sample pooling or exception is used.
The native mean improves, while its upper bound permits a very small slowdown;
a passing 1% bound is not proof of zero overhead on every possible workload.

A separately versioned [pre-ECS recovery comparison](../../scripts/run-lightweight-chipset-recovery-comparison-v1.ps1)
uses rebuilt `68c2bac` with the same current dependencies,
frozen runner, workloads, six pairs, placement and host policy. Its input
compatibility was verified independently before timing, including complete
native Lemmings. Matching pre-ECS build symbols accompany that reference DLL.
This measures source recovery rather than historical package combinations.
The attempt is **INVALID / RERUN** after 14 completed samples because a
competing .NET test job appears during hires C2. Every partial timing is
excluded, including the unusually slow candidate sample discussed during the
run. The owner then requests keeping the validated candidate and leaving
pre-ECS recovery verification pending. No quantitative recovery estimate is
made from this invalid attempt. Full recovery of the pre-ECS regression is
not established.

## Frozen evidence

The [manifest](CHIPSET_REGRESSION_2026-10-03.json) records pins and dispositions.
Ignored local artifacts are under `artifacts/chipset-regression-2026-10-03/`
for R1 and `artifacts/chipset-regression-r2-2026-10-03/` for R2. Each preserves
the complete source archive/manifest (including the new trait file), engine
patch, frozen runtime capsules, test logs, native captures, JIT output and
measurement telemetry. R2's `retention-v2-retry-1/` is the complete valid
current-source comparison; `pre-ecs-recovery-v1/` is the invalid recovery attempt,
including its reason and all partial samples. ROMs, game media and build
artifacts are not tracked.

R2's source manifest and capsules are frozen before timing. Its separately
versioned [comparison script](../../scripts/run-lightweight-chipset-specialization-comparison-v2.ps1)
preserves the validated host policy and six-pair design. R1 uses the retained
[v1 script](../../scripts/run-lightweight-chipset-specialization-comparison-v1.ps1).
