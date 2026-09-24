# Scoped scalar-local initialization

2026-09-24. **VALID / PASS: every workload meets the unchanged 1% gate.**
The owner accepted the complete candidate on 2026-09-24, including the Tower
Assault correction and retained optimizations. Acceptance applies to engine
SHA256 `683C0AB519FBCCDE85770977269BB965FF88C39D20411C7550ACF62597B7A5E8`
and its v8 evidence below. No performance exception is needed; the default 1%
limit and documented hardware uncertainties remain unchanged.

The preceding [fixed-bounds candidate](FIXED_BOUNDS_2026-09-24.md) passes hires
and native Lemmings, but its lores upper bound is +2.2979%. That complete valid
comparison remains unchanged and is not pooled with this candidate.

## Observed cost and bounded change

The captured Tier-1 device dispatcher clears 40 bytes in its prologue on every
call: one eight-byte store and one 32-byte vector store, plus two register clears.
Those slots hold scalar locals, including the two sprite-composition outputs.
The outputs are assigned by every returning path of the compositor before they
are read. The inspected caller also initializes its other scalar locals before
use. No emulated state depends on the prologue's earlier zeros.

The candidate applies `SkipLocalsInit` only to `LightweightA500Machine.TickDevices`,
`LightweightVideo.Step` and `LightweightVideo.RenderLowResPair`. The first two
include the rendering path through inlining. The project enables the compiler's
unsafe option because the attribute requires it; there is no assembly-wide or
type-wide attribute and no new pointer arithmetic, `Unsafe.SkipInit`, stack
allocation or uninitialized read. GC reference handling remains the JIT's
responsibility. The existing CopperHDF sector buffer is in a separate, unchanged
method and is outside these scopes.

These scopes must continue to assign every local before reading it. In
particular, do not introduce uninitialized stack storage or a helper that returns
unassigned `out` values. Microsoft's [attribute documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute?view=net-10.0)
and [unsafe-code guidance](https://github.com/dotnet/docs/blob/main/docs/standard/unsafe-code/best-practices.md)
describe the initialization obligation and the need to verify an actual generated
code benefit. This change follows observed prologue stores, not an assumption
that all local initialization is expensive.

Device ordering, sprite comparison and collision side effects, pixel shifts,
DMA completion phases and memory contents are unchanged. The preceding
single-word DMA latch, fixed-size DMA RAM access and checked constant array
extents remain in place. No CPU package or scheduler behavior changes.

## Generated-code evidence

Both complete synthetic workloads retain their exact fingerprints and zero
measured steady-state allocation during sequential .NET 10.0.12 disassembly
captures. The new device-dispatch prologue removes all four identified clearing
instructions in both workloads. Its stack reservation remains 136 bytes.

| Tier-1 method | Fixed-bounds predecessor, bytes | Three-scope candidate, bytes |
|---|---:|---:|
| Device dispatch, lores | 3595 | 3580 |
| Video step, lores | 2154 | 2138 |
| Lores pair | 929 | 926 |
| Device dispatch, hires | 3526 | 3511 |
| Video step, hires | 2076 | 2060 |
| Hires CCK | 2250 | 2303 |
| Hires visible pair | 919 | 919 |

Dynamic PGO can alter the remaining generated code, as the hires CCK and lores
pair captures illustrate. These sizes and instrumented FPS are diagnostic
evidence, not a throughput result. The initial six-scope experiment additionally
annotated three render methods; those annotations were removed before correctness
and performance acceptance checks because they were unnecessary for the observed
hot-prologue benefit. Its diagnostic captures remain separate.

The final candidate engine SHA256 is
`683C0AB519FBCCDE85770977269BB965FF88C39D20411C7550ACF62597B7A5E8`.
It uses SDK 10.0.401 and the unchanged frozen runner, Copper68k, CopperDisk and
CopperFloat dependencies. The accepted-source reference remains
`1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`,
built with SDK 10.0.400. Both run explicitly on pinned .NET 10.0.12.

## Correctness and performance verification

Production Release builds with zero warnings/errors. All 705 isolated engine,
74 disk and 95 native-enabled host tests pass with no skips. All four complete
workload identities (lores, hires, Lemmings and Tower Assault) match and report
zero measured steady-state allocations. The detailed 20,000-field Tower Assault
replay retains all 1005 byte-identical capture files: 335 states, 335 images and
335 Chip RAM dumps. Capture runs are diagnostic and make no allocation/FPS claim.
This retains bounded disk-two/opening-level evidence, not whole-game or physical
open-bus certification.

[Comparison v8](../../scripts/run-lightweight-ocs-identification-comparison-v8.ps1)
retains the v7 measurement body with a new identifier and candidate pin. It uses
six balanced pairs per workload, CPU 2 with sibling 3 protected, Normal priority,
pinned .NET 10.0.12 and host-load policy v2. Every one-sided 95% upper frame-time
regression bound must be at most 1%. Earlier binaries and samples are not pooled.
The comparison measures the complete candidate against the accepted-source
reference; it does not isolate the attribute's speedup from the other retained
DMA and fixed-bound changes.

The complete six-pair comparison is VALID. All 36 complete identities match,
measured steady-state allocations are zero and host-load policy v2 telemetry
passes. An independent calculation agrees with the protocol's three results;
the [machine-readable record](SCOPED_LOCALS_2026-09-24.json) preserves every
sample, source/build/protocol hash and local evidence hash.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
|---|---:|---:|---:|---:|---|
| Lores | 395.16 | 403.35 | -2.0306% | -0.8096% | Pass |
| Hires | 359.19 | 363.49 | -1.1815% | -0.6618% | Pass |
| Native Lemmings | 335.30 | 338.26 | -0.8746% | +0.2946% | Pass |

Paired FPS changes are +2.0727%, +1.1956% and +0.8823%, respectively. The lores
and hires bounds establish an improvement in this comparison. Native passes the
1% regression limit but does not establish a positive speedup at this confidence
level. These are headless engine throughput results, not desktop presentation FPS.

All valid samples remain included, including the slower native candidate C5
(332.13 FPS versus reference R5 at 337.30 FPS). There are 1,438 valid telemetry
intervals over 1,561.736 seconds, with a largest interval of 1.165 seconds. No
interval reaches 25% selected/sibling/package background load, and there are no
competing build/test/runner workloads. No exception is needed or granted.

The protocol was invoked directly with its streams redirected to the outer log.
That log contains the success stream, complete sample progress and final result;
the inner PowerShell transcript retains the expected negative-probe exceptions.
The recorder initially looked for success messages in the inner transcript and
stopped before writing a result. Its log routing was corrected, with both original
logs retained and hashed. No raw sample, telemetry, protocol rule or result was
changed. Earlier comparisons retain their original dispositions and are not pooled.

Local source snapshots, binaries, diagnostic
captures and logs remain under ignored `artifacts/scoped-locals-optimization/`.
The `narrow-candidate` and `narrow-source` directories identify the three-scope
candidate; `candidate` identifies the preliminary six-scope diagnostic binary.
No local media, binaries or capture files are included in the source change.
