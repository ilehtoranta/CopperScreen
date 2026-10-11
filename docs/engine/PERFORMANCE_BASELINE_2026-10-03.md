# Current Lightweight throughput baseline — 2026-10-03

The production Release solution builds with zero warnings and errors at HEAD
`d9beae8b88be24032221e3482942a249c03c27d3`. This record captures six full
current-build runs each for the retained synthetic lores and hires workloads.
It is a **descriptive baseline only**: there is no reference build, paired
comparison or 1% performance-gate decision.

## Synthetic results

| Workload | Mean FPS | Range | Mean ms/frame | Two-sided 95% CI for mean ms/frame |
| --- | ---: | ---: | ---: | ---: |
| Lores full pipeline | 341.63 | 334.88–348.66 | 2.92782 | 2.87698–2.97867 |
| Hires full pipeline | 318.68 | 314.71–321.83 | 3.13814 | 3.10987–3.16640 |

Each run used the unchanged synthetic Paula/DMA fixture with 600 warmup and 7,200
measured fields, real PCM, zero measured allocations and `unsupported=none`. All
six fingerprints per workload match the independent identities recorded for
these fixtures. Confidence intervals use the six per-run frame times, Student-t
with five degrees of freedom; they describe this build's mean and are not paired
regression bounds.

The host was an AMD Ryzen 5 5600X on Windows `10.0.26200`, .NET runtime `10.0.12`
and SDK `10.0.401`. The run used logical CPU 2 at Normal priority and protected
SMT sibling 3 under the Balanced power plan. The homogeneous topology matched
native per-CPU telemetry. All 388 telemetry intervals passed host-load policy v2;
no selected-core, sibling, package or competing-build/test signal crossed its
10-second invalidation rule.

The measured build pins Copper68k `1.5.1` and CopperDisk `3.0.0`. Its assembly
identities are:

| Assembly | SHA-256 |
| --- | --- |
| `CopperMod.Amiga.Lightweight.Runner.dll` | `6120A86EA6CA102BC9E2BF7833A70CAA97E7644B517EC1F415E67FC2125BBC53` |
| `CopperMod.Amiga.Lightweight.dll` | `73FC5119409A4C21DBB3AF1093EF9A6BE73AC5538CF12A1B9F03B72AA065C1CF` |
| `Copper68k.dll` | `99173918A4DA1CD5446D8F6E115E6B5C4D0A8F1C7F9173261C754988D3CBCB8F` |
| `CopperDisk.dll` | `A93A9CD6F41221C0F87C841B5E513DFDE509B20F6F2E48004A0A074D5750CE2A` |

## Native Lemmings status

The current Release runner completed one 14,520-field Lemmings sample at 343.49
FPS, but its complete state differs from the independently recorded native
identity. The runner reported cycle `2063189026`, CPU `0F38F24EA7FE54A7`, hardware
`0AB8B571B4B0F355`, output `DB09C0F1938DBD23` and 1,922 audio samples. The focused
`NativeGameplayThroughHostAdapterPreservesValidatedCpuAndOutput` test reproduced
the cycle mismatch: it expected `2063189662` and received `2063189026`.

That one timing sample is excluded from the baseline. The native identity remains
unresolved and unaccepted; the complete native throughput gate is unavailable.
The mismatch was not explained by this benchmark, and no golden was changed.

The subsequent [investigation](PERFORMANCE_INVESTIGATION_2026-10-03.md) identifies
the obsolete input script as the cause. The current build with the already
validated September 25 bus-timing script matches that record's complete native
identity and both gameplay screenshots exactly. The active host tests now use
that workload and its pre-existing expectations. This does not admit the excluded
sample above or turn it into gameplay throughput evidence.

Raw per-run logs, JSON results, telemetry and the frozen runner bundle are local
under `artifacts/current-synthetic-baseline-2026-10-03/`. The separate invalid
full-suite attempt and native host-test result are under
`artifacts/current-baseline-2026-10-03/`; none of those local artifacts are
committed.
