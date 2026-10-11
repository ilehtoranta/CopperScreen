# Current-slot arbitration optimization — 2026-10-04

Current-slot CPU arbitration and blitter fairness now consult completed output
cycles after due DMA has retired. The prototype removes redundant pending-state
checks from those hot queries. **Correctness and retirement guards pass;
performance acceptance remains pending.** Generated-code reduction establishes
less host work, but does not establish an FPS gain.

The performance reference is the owner-accepted
[combined grant-locality candidate](GRANT_LOCALITY_2026-10-04.md), engine
`109CD5ACDBF4C070195B138249A98F8A1F60A5CB1C3A7D797852832590D6A409`.
Its result and approval remain unchanged. The new frozen candidate is
`F8F924F7C2B7F59E883DC49E46C1D3015D608C17BFABC13C28D43B4811E4BDC1`.
Earlier exceptions do not transfer; each retained workload still requires a
one-sided 95% upper frame-time regression bound ≤1%.

## Fresh profile and concrete target

Complete native Lemmings and synthetic lores replays profiled the immutable
accepted reference with `dotnet-trace` 10.0.745401, sampled thread time and
.NET 10.0.12. Both complete identities match, with zero measured steady-state
allocations. Seven-second interior tails exclude boot/warmup and the final
second of execution. All instrumented timings are excluded from throughput
conclusions; placement and host-load acceptance were not asserted for profiles.

| Approximate exclusive sample group | Native Lemmings | Synthetic lores |
| --- | ---: | ---: |
| Device dispatch and inlined rendering | 27.57% | 57.34% |
| Clock and CPU bus wait | 8.71% | 0.02% |
| CPU bus and memory helpers | 4.01% | 0.03% |

Native `AdvanceToCpuGrant` alone accounts for about 6.67% of exclusive samples.
Its inclusive time contains required device execution and is not an overhead
estimate. Sampling and inlining attribution make the percentages approximate;
the dispatch leaf also includes pixel work. The target is the specific redundant
pending-output predicates inside the native wait path, not the whole sampled
bus/device cost. All production engine source matches the accepted reference
before the change; an independently edited README is separately disclosed.

## Retirement contract

The only production callers of current-slot ownership are the CPU wait loop
and blitter fairness. Their ordering is:

| Caller | Retirement before the query |
| --- | --- |
| CPU `IsCpuOutputOwned` | Owner-clock advancement completes due device outputs, including blitter output, before grant testing. Each stolen CCK repeats physical completion before retesting. |
| Blitter `IsHigherPriorityOutputOwned` | `TickDevices` has stepped disk DMA, Paula, bitplanes, sprite DMA and Copper before blitter fairness runs. |

Each accepted address phase publishes a future output CCK. Device steps consume
outputs due at the current CCK before accepting new future transfers. Completed
output cycles therefore supply the current ownership decision. Refresh still
uses the live beam/sync state; nice/nasty fairness still uses its original CPU
request, candidate cycle and blitter bus-phase state.

`IsHigherPriorityOutputOwned` compares the existing disk, audio, bitplane,
sprite and Copper completion cycles. `IsCpuOutputOwned` adds the blitter's
existing last **bus** output cycle, so internal blitter line/control phases do
not occupy the bus. Device pending-aware queries and following-slot acceptance
keep their prior semantics. No slot cache, ownership mask, per-CCK store, new
state, allocation, profile switch, scheduler or timing shortcut is introduced.
Physical CCK completion, data strobes and longword spacing are unchanged.

Under `LIGHTWEIGHT_DIAGNOSTICS`, guards throw if either caller observes a
pending bus output at the current query cycle. They exercise the retirement
assumption through existing tests and full replays. The guard messages are
present in the frozen diagnostic assembly and absent from production Release;
the production path contains no guard calls or additional diagnostic work.

## Generated code and verification

Fresh complete native replays captured the accepted reference and candidate
Tier1 code on the same pinned runtime. Complete identities match. Diagnostic
timings are excluded; static code size and branches are not FPS evidence.

| Captured Tier1 body | Reference bytes / branches / calls | Candidate bytes / branches / calls |
| --- | ---: | ---: |
| OCS `AdvanceToCpuGrant<TPath>` | 1,162 / 45 / 22 | **1,005 / 37 / 22** |
| Blitter `Step` | 408 / 19 / 3 | 408 / 19 / 3 |

The inspected wait body loses 157 bytes (13.51%) and eight conditional branches
with no extra calls. These counts cover the captured bodies, not the complete
engine footprint, dynamic branch frequency or cache-miss rate. Only
`LightweightA500Machine.cs` changes from the accepted implementation; the
normalized source diff contains the two ownership-query changes and guards.

| Check | Result |
| --- | --- |
| Production solution Release build | PASS, zero warnings/errors |
| Focused bus/DMA/fairness diagnostics | 186 passed, zero failed/skipped |
| Complete isolated engine diagnostics, guards enabled | **1,089 passed**, zero failed/skipped |
| CopperDisk tests | **74 passed**, zero failed/skipped |
| Frozen native-enabled host suite | **185 passed**, zero failed/skipped; all 168 accepted-reference cases retained |
| Production lores, hires, native and scalar Lemmings | Four complete identities match; zero measured steady-state allocations |
| Diagnostic lores, hires, native and scalar Lemmings | All retirement guards pass; four complete identities match; zero measured allocations |
| ECS SuperHires, programmed NTSC SuperHires and AGA RGB24, normal/scalar | **182 captured-file hashes match**; all six final disposable hardfiles match |

Existing externally observable bus-spacing, DMA address/data, refresh,
disable/cancellation, nice/nasty fairness, internal blitter phases, CIA and
line/field/sync regressions are retained. No golden outputs or hardware
expectations are rebased, and no implementation-mirroring tests are added.
Source/capsule/media hashes verify. The host suite uses a frozen assembly bag,
with its engine/dependency and root-file hashes checked before and after execution.
The extra 17 host cases come from existing concurrent host development.

## Performance gate

The [current-slot comparison v1](../../scripts/run-lightweight-current-slot-comparison-v1.ps1)
preserves the accepted homogeneous-host protocol's six balanced pairs, full
lores/hires/Lemmings workloads, Normal priority, topology/affinity checks,
runtime/input pins, full fingerprints, zero-allocation checks, negative probes
and host-load policy v2. Normalized logic and PowerShell AST checks pass. Only
the reference/candidate pins, topology class name and descriptive labels change.
Common runner, dependencies and XML are identical; current generated XML is
preserved separately. The reference is the accepted combined candidate.

Other CopperSharp and Copper68k test jobs were active after correctness
verification. The live `preflight-1/` verifies runtime/input/capsule pins and homogeneous
AMD topology (CPU 2, protected SMT pair 2–3), then rejects
`dotnet.exe[23172]` as a competing build/test job: **INVALID / RERUN**, zero
throughput samples. Formal throughput verification is pending a valid quiet window;
none of the correctness, sampled-profile or JIT timings establish acceptance.
Require **every one-sided 95% upper frame-time regression bound ≤1%**. Above
that bound, report the complete comparison and request owner acceptance.
Pre-ECS recovery remains pending at the owner's request.

The [JSON record](CURRENT_SLOT_OPTIMIZATION_2026-10-04.json) retains all source,
capsule/input pins, raw test counters, full replay commands, diagnostic guards,
profile summaries and generated-code comparisons. Local evidence is under
`artifacts/current-slot-optimization-2026-10-04/`. ROMs/media remain untracked;
no commit, push or package publication is part of this optimization request.
