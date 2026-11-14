# Hires transparent-path regression diagnosis — 2026-11-04

The [valid paired benchmark](HIRES_TRANSPARENT_BENCHMARK_2026-11-04.md) establishes
a **−2.5702% hires DMA throughput change**, with a two-sided 95% interval of
**−4.8113% to −0.2764%**. It does not establish a Lemmings speedup or slowdown.

The strongest supported explanation is an unfavorable JIT inlining change on a
common fallback. The added empty-span path made the renderer smaller, but left
the active-sprite path doing more host work.

## What the actual hires workload executes

The fixture named `paula-dma-wide-hires` includes sprite DMA: the runner maps
`--synthetic-paula-dma` to `syntheticSpriteDma=true`, then configures synthetic
sprites and sprite DMA. It is not an empty-sprite workload.

A separate diagnostic source copy counts the guard outcomes over the complete
**600-warmup + 7,200-measured-field** run. Counts include warmup; they are not a
measurement-region-only distribution. The probe reproduces the full retained
CPU, hardware, output, cycle-count and allocation identity.

| Guard outcome | Count | Share |
| --- | ---: | ---: |
| Transparent span accepted | 184,589,112 | 58.1399% |
| Running sprite shifters require fallback | 119,799,061 | 37.7330% |
| Comparator due at either position | 13,103,026 | 4.1271% |
| Dirty state requires fallback | 1 | Less than 0.000001% |
| Line/order mismatch | 0 | 0% |
| Total guard entries | 317,491,200 | 100% |

Thus **41.8601%** of entries cannot use the new fast path. They pay its guard
and then execute the ordinary per-position operations and checks. Skipping
those shifters/comparators would change emulation behavior.

## Generated code for the same fixture

Fresh disassembly is collected from the **unchanged frozen reference and
candidate executables**, with the same hires arguments, .NET 10.0.12 and normal
tiering/PGO. Each complete run matches the retained identity. These inspection
runs provide no throughput result.

| Observed Tier1 `RenderHiresCck` | Reference | Candidate |
| --- | ---: | ---: |
| Code bytes | 2,221 | 1,217 |
| Out-of-line single-playfield compositor call sites | 0 | 2 |
| Vector insertion instructions in transparent output | 0 | 3 |

The reference inlines **both** `ComposeHiresColorIndexes` operations into the
renderer. The candidate's two fallback operations remain calls to that method.
The fast-path guard itself inlines. Consequently, the sizable fallback share
now combines extra guard work with call/argument overhead and the original
compositor checks. Multiplying the diagnostic fallback count by two gives
**265,804,176 compositor entries** for that path over the full diagnostic run;
this is an implied count from control flow, not a call counter from the timed
benchmark.

The earlier Lemmings-derived disassembly had a different PGO shape: reference
1,524 bytes with one compositor operation outlined, candidate 1,210 bytes. Those
bodies are not the right basis for explaining the hires-specific regression.
The fresh fixture-specific listings show why source-level call duplication and
total code size were misleading optimization targets.

## Additional output cost and limits of attribution

The transparent branch packs four separately looked-up palette values with a
load into `xmm0`, **three dependent `vpinsrd` insertions**, and one vector store.
The reference writes scalar palette results. A wider store is not an arithmetic
vectorization of the palette lookups, and it adds packing instructions. Its net
cost has **not** been isolated by a scalar-versus-vector ablation; it is a
secondary hypothesis, not a separately measured cause of the 2.5702% loss.

The diagnosis establishes frequent fallback and reproduces the unfavorable JIT
shape. It does not assign an exact percentage of the slowdown to guards, calls,
packing, branch layout or other compiler effects. Disassembly is from fresh
diagnostic reproductions, not captured inside all 36 timed processes; normal
dynamic PGO can vary. Probe instrumentation also changes generated code, so its
code shape and FPS are excluded. Only the uninstrumented frozen listings are
used for the inlining comparison.

I overestimated the benefit of merging the empty-span checks and did not account
for the resulting inlining loss on a common active path. The measured candidate
should be reverted or redesigned; smaller code is not an FPS result. This
diagnosis makes no production source change and does not introduce a new cache,
weaken interrupt sampling or remove hardware operations.

## Evidence and diagnostic boundary

The counter source, temporary project, built diagnostic DLL, raw logs and fresh
disassembly are under `artifacts/hires-regression-diagnosis-20261104/`. The CPU
remains the pinned development NuGet dependency; the temporary engine references
the unchanged frozen CopperDisk assembly. No sibling CopperMod source is used.
All counters and the process-exit report exist only in this artifact source copy.
Production Release source and the benchmark capsules remain unchanged.

The [machine-readable diagnosis](HIRES_TRANSPARENT_DIAGNOSIS_2026-11-04.json)
records the counts, code-generation observations and file hashes. The earlier
valid throughput report and invalid attempts keep their original dispositions.
