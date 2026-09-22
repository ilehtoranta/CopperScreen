# Device/video optimization trials — 2026-09-22

**No runtime change retained.** Following the [execution profile](PROFILE_2026-09-22.md),
five implemented approaches to reducing device/video overhead were explored.
The completed short comparisons did not demonstrate a dependable improvement.
The final compilation-mode candidate was blocked by Windows Application Control
before emulation. Engine and test sources were restored to `9be9fee`, and the
restored production Release solution built with zero warnings/errors.

This is a negative optimization result, not a new performance acceptance or a
change to the owner's one-sided 95% upper regression limit of 1%. No package,
release or commit was published during this work.

## Trial design and results

Reference: accepted WORDSYNC engine
`8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.
Each candidate used the same frozen runner, CPU and disk dependencies, replacing
only the engine DLL. This also avoided an application-control block on a newly
built, unchanged CopperDisk assembly; no security policy or trust metadata changed.

The existing short `artifacts/beam-opt-2026-09-19/compare.ps1` used Normal priority,
CPU 2 / sibling 3 (topology rechecked during profiling), 600 warmup fields and
7,200 measured fields. Order was R1/C1/C2/R2. Shared host-load policy telemetry
was collected, with no simultaneous builds or tests. These two-pair diagnostics
lack the complete six-pair protocol and cannot establish its confidence bound.

Positive numbers below mean slower frame time. The statistic is the geometric
mean of the two paired reference-FPS/candidate-FPS ratios, not a ratio of means.
Every completed sample matches its accepted complete CPU/hardware/output
fingerprint, reports real PCM, zero measured allocations and no unsupported mode.

| Approach | Workload | R1 / C1 FPS | R2 / C2 FPS | Paired frame-time change | Decision |
| --- | --- | ---: | ---: | ---: | --- |
| Precomputed full-window range, separate border helper | Lores | 374.34 / 370.78 | 384.67 / 376.20 | +1.604% | Discarded |
| Same range, border helper inline | Lores | 378.24 / 372.46 | 392.07 / 386.40 | +1.510% | Discarded |
| Prevent video Step inlining into device loop | Lores | 377.72 / 370.41 | 373.83 / 374.54 | +0.886% | Discarded |
| Packed sprite-result return instead of two output locals | Lores | 382.93 / 389.75 | 389.56 / 379.37 | +0.444% | No dependable gain |
| Same packed sprite-result return | Hires | 344.35 / 347.96 | 353.16 / 351.38 | -0.268% | No dependable gain |
| Keep playfield pixels packed through sprite composition | Lores | 382.42 / 381.61 | 382.59 / 374.13 | +1.232% | Discarded |
| Compile TickDevices directly with AggressiveOptimization | Lores | R1 only: 386.05 | Unavailable | Unavailable | Candidate blocked; discarded |

The packed sprite-result variant reduced observed Tier1 TickDevices code from
3,607 to 3,167 bytes, and its stack reservation from 136 to 72 bytes. It still
did not establish a throughput gain. Generated-code size and removed stack writes
are not sufficient grounds to retain an optimization.

The above-limit short trials were discarded rather than proposed for acceptance.
No scoped exception is requested or implied. No formal acceptance series was
started for an unpromising or blocked candidate, and no samples were trimmed,
substituted or pooled across candidates.

## Correctness checks and blocking evidence

Production Release builds succeeded for every approach. Four temporary
regression cases covered mid-line DIW changes at even/odd boundaries in narrow
and wide output. The separate-video-Step candidate passed 127 focused cases;
the packed sprite-result candidate passed all 673 diagnostic cases, including
those four temporary cases. These results apply only to those identified
candidates. The temporary tests are preserved as a patch with the discarded
source changes, rather than left as an unrelated repository edit.

Other test launches were blocked before discovery by Windows Application Control
(`0x800711C7`, confirmed by Code Integrity event 3077). A CopperDisk run reported
74 failures, all at assembly loading; the log remains a failed run, not a pass.
Native-enabled host tests were blocked before discovery on both attempts, so
this pass provides no new native coverage. The final compilation-mode candidate
also failed to load on a bounded smoke retry. No test assertion was weakened,
and blocked runs are not reclassified using earlier candidates' passing results.

## Resumption

The accepted engine remains the starting point. Before further development,
resolve the local application-control restriction on newly built .NET assemblies
through the machine's supported trust/approval process. Further device-loop work
should separate the remaining inlined costs more precisely before another rewrite.
Native CPU dispatch remains a separate measured opportunity; it was not changed
or benchmarked in this pass.

[Machine-readable trial evidence](VIDEO_OPTIMIZATION_TRIALS_2026-09-22.json)
records exact samples, candidate engine hashes and dispositions. Ignored evidence
under `artifacts/video-opt-2026-09-22/` retains frozen candidates, source/test
patches, build/test logs, disassembly, load telemetry and application-control
events. Earlier profiling and accepted performance records remain unchanged.
