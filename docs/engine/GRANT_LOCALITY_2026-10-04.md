# Shared CPU grant code — 2026-10-04

The third [bus investigation](BUS_CLOCK_INVESTIGATION_2026-10-03.md) target is
implemented as a bounded candidate. Explicit `NoInlining` boundaries keep the
private CPU word-grant helper and typed clock wait kernel shared. The CIA and
non-contended paths also share one advance-to-strobe fallback. Native generated
code confirms the boundaries. **The combined performance gate passes** against
validated chipset R2 on the complete six-pair comparison. All three one-sided
95% upper frame-time regression bounds are ≤1%, with no exception. Code size
alone does not establish an FPS improvement.

The incremental code reference is the frozen selected
[CPU-transfer R1](CPU_TRANSFER_OPTIMIZATION_2026-10-04.md), engine `DB373E35…`.
The complete overall comparison uses unchanged, validated
[chipset R2](CHIPSET_REGRESSION_2026-10-03.md), engine `ED53DA30…`. The complete
candidate includes the preceding following-slot and CPU-transfer changes.
Earlier performance results and exceptions do not transfer. Pre-ECS recovery
remains pending at the owner's request.

## Boundaries and timing

The six CPU read/write callbacks call one private `GrantCpuWord` body. Byte and
word operands call it once; long operands retain two calls with their existing
separated memory effects and grants. Address classification, CIA alignment,
blitter request state and arbitration are no longer copied into those callbacks.

The fitted-path selector reaches one `AdvanceToCpuGrant<TPath>` kernel per
OCS/ECS/AGA value-type specialization. That kernel's existing ownership/CCK loop
is unchanged. Its boundary also prevents wait-loop copies in the selector and
word-grant helper. Fitted traits remain specialized; no interface dispatch or
profile selection is added inside the physical CCK loop.

Hardware still advances to the data strobe before the memory/register effect
and to completion afterward. The shared fallback retains CIA's ten-clock grid,
ROM/expansion timing, partial CPU phases and requested/current-cycle clamp.
Refresh, current-slot ownership, accepted DMA, nice/nasty fairness, cancellation,
read strobes and line/field synchronization retain their existing ordering.
Only two engine source files change from CPU-transfer R1. No device state,
allocation, scheduler, public API, package pin or CPU-core semantics are added.

## Generated code

Complete native Lemmings replays captured Tier1 code for the frozen reference
and final production candidate on .NET 10.0.12 at Normal priority. Full identities
match with zero measured allocations. All diagnostic timings are excluded
because of background load and concurrent validation.

| Captured Tier1 body | CPU-transfer reference bytes | Selected candidate bytes |
| --- | ---: | ---: |
| Private `GrantCpuWord` | 655 | 644 |
| `ReadByte` | 894 | 469 |
| `ReadWord` | 1,098 | 608 |
| `ReadLong` | 2,254 | 1,300 |
| `WriteByte` | 2,021 | 1,717 |
| `WriteWord` | 1,415 | 896 |
| `WriteLong` | 2,900 | 1,883 |
| Clock grant selector | 1,228 | 30 |
| OCS typed wait kernel | 1,162 | 1,162 |
| Nine-body sum | **13,627** | **8,709** |

The seven machine grant/callback bodies shrink **33.1%**; including the selector
and OCS kernel, the nine matched bodies shrink **36.1%**. Every callback has the
expected one or two calls to the shared helper and no grant/wait copy. In this
final capture the helper calls a separate 30-byte selector, which tail-jumps to
the three typed kernels. The typed OCS kernel retains its inspected size and
static branch/call counts. These sums exclude outlined callees, Tier0 bodies,
stubs and the rest of the engine/core. They do not measure total code footprint,
cache misses, branch misses or throughput. Fresh baseline sizes reflect this
capture; earlier generated-code records remain unchanged.

A first variant bounded only the private grant. It removed callback copies but
left larger wait-loop copies in the helper/selector; its nine captured bodies
totaled 13,886 bytes. It remains frozen and unselected. Its raw host suite passed
168 cases, but an end-pin check failed after the kernel-probe source change and
a concurrent production rebuild. Final host coverage is repeated against the
selected engine. Both variants' immutable native replays and code captures are
retained. An isolated two-boundary probe also passes its full native identity;
because its binary identity differs from the production build, the final code
capture uses the production binary independently.

## Verification

| Check | Selected production candidate |
| --- | --- |
| Production `CopperScreen.slnx` Release build | PASS, zero warnings/errors |
| Isolated engine diagnostics | **1,089 passed**, zero failed/skipped |
| CopperDisk tests | **74 passed**, zero failed/skipped |
| Native-enabled host suite | **168 passed**, including all 163 prior cases and five existing recent-session cases |
| Retained lores, hires, Lemmings and scalar Lemmings | Complete identities match; zero measured steady-state allocations |
| ECS SuperHires, programmed NTSC SuperHires and AGA RGB24, normal/scalar | **182 captured-file hashes match**; all six final disposable hardfiles match |

The existing discriminating refresh, CPU spacing, longword boundary, DMA
contention, blitter fairness, CIA alignment/interrupt, custom-read strobe,
synchronization and accelerator checks pass. No implementation-mirroring test
or golden-output rebase is added for the inlining flags. Frozen source and capsule
manifests, native inputs and host engine/dependency pins verify. The engine DLL
SHA-256 is `109CD5ACDBF4C070195B138249A98F8A1F60A5CB1C3A7D797852832590D6A409`.
Only engine DLL/PDB differ in the retained comparison bag; common XML remains
identical to the reference and current generated XML is preserved separately.

The [grant-locality v1 comparison](../../scripts/run-lightweight-grant-locality-comparison-v1.ps1)
preserves the validated homogeneous-host v2 complete workloads, six balanced
pairs, placement, Normal priority, runtime/input pins, full fingerprints,
zero-allocation checks, negative probes and host-load policy. Only labels,
the topology type name and frozen candidate pins change. AST/logic checks pass.

## Complete performance comparison

`r2/retention-v1-6/` is **VALID / PASS**: 36 complete samples, six balanced pairs
per workload, Normal priority, CPU 2 and protected SMT pair 2–3 on the rechecked
AMD Ryzen 5 5600X topology. All complete workload identities match, measured
steady-state allocations are zero and all 1,407 telemetry intervals pass the
unchanged host-load policy v2. Runtime/capsule pins pass before and after the
run; independent post-run input/harness hashes and all 47 current source hashes
match the frozen candidate. No concurrent build/test or competing runner is
detected in the accepted series.

| Workload | Reference mean FPS | Candidate mean FPS | Paired throughput gain | One-sided 95% upper frame-time change | Gate |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 352.6633 | 353.6333 | +0.2824% | +0.7642% | PASS |
| Hires | 328.4350 | 330.6800 | +0.6848% | +0.9947% | PASS |
| Native Lemmings | 333.9550 | 341.5783 | +2.2646% | −0.4413% | PASS |

The independent analysis reproduces the harness’s paired log frame-time ratios
and Student t one-sided 95% bound (six pairs, df=5, critical value 2.015048).
Hires passes narrowly at the unrounded **+0.9946842849830535%** upper bound.
Native’s negative upper frame-time bound supports an improvement; the small
synthetic mean gains do not independently establish speedups. These results
measure the complete following-slot, CPU-transfer and grant-locality candidate
together, without isolating each phase’s contribution. Pre-ECS recovery remains
pending; no historical result or exception is replaced.

Raw paired samples, full fingerprints, summary, protocol, telemetry and
`independent-analysis.json` are retained in the accepted evidence directory.
The JSON record contains all six pairs per workload and their evidence hashes.

The owner accepted this combined candidate and its complete benchmark result
on **2026-10-04**. No performance exception is required or granted; the default
1% upper-bound limit remains in force. Acceptance applies to the frozen
`109CD5AC…` engine and the `retention-v1-6` comparison.

## Earlier invalid attempts

The first live preflight verifies runtime/input pins and homogeneous AMD topology
(CPU 2, protected CPUs 2–3), but is **INVALID / RERUN**: package interference
remained ≥25% for 10.48 seconds. No timing samples were accepted. A subsequent
five-second observation measured 12.12 CPU-seconds from Panthers. The rejected
preflight is preserved under `r2/preflight-1/`. A second short preflight
(`r2/preflight-2/`) passed, but the full comparison (`r2/retention-v1-1/`)
was then **INVALID / RERUN** during its own preflight: package interference
remained ≥25% for 10.43 seconds. Both rejected attempts ended before collecting
any timing samples. At that stage the complete comparison remained pending; the passing short
preflight provides no FPS or performance acceptance result. The owner-requested
retry (`r2/retention-v1-2/`) also ended during preflight: package interference
remained ≥25% for 10.57 seconds, with zero timing samples. The follow-up
five-second check measured 12.06 CPU-seconds from Panthers. All three rejected
attempts remain preserved; no FPS or gate result is inferred from them.

After the owner paused Panthers, `r2/retention-v1-3/` passed preflight and
completed all 12 lores samples plus the first hires reference sample. The
comparison became **INVALID / RERUN** in that sample’s cooldown when a separate
`dotnet.exe[94456]` build/MSBuild job started. The final telemetry interval
recorded 40.48% package interference. All **13 partial timings are excluded**;
no paired FPS gain or confidence gate is inferred. A fresh complete series
requires other build/test jobs to remain paused throughout. A further retry
(`r2/retention-v1-4/`) passed preflight but encountered another brief
`dotnet.exe[48384]` build in its first lores sample’s cooldown. Its one partial
timing is also excluded; the process had exited by the follow-up inventory.
After a short quiet interval, `r2/retention-v1-5/` passed preflight but was
interrupted during the first lores sample by `dotnet.exe[18464]`, before a
completed sample. A follow-up inventory observed an active CopperOS
`CopperOS.Commands.Dir.csproj` publish and CopperSharp compiler process; the
rejected PID had exited, so its command line was not established. The final
interval recorded 34.52% package interference. All **14 completed partial
samples** across the interrupted attempts remain excluded. No valid comparison
or gate result was inferred from those attempts. They remain separate from the
complete accepted `retention-v1-6` series.

Require a **one-sided 95% upper frame-time regression bound ≤1% for each retained
workload**. Outlined calls, argument/result traffic and stack use can offset
locality savings; the result above is the measured combined gate, with no
exception and no attribution of the gains to individual phases.

The [JSON record](GRANT_LOCALITY_2026-10-04.json) preserves source/binary/media
pins, raw test outcomes, code-body sizes and explicit call-target checks.
Selected production evidence is under `artifacts/grant-locality-2026-10-04/r2/`;
the first-boundary experiment is retained in its parent directory.
