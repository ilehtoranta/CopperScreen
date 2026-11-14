# Idle-host fallback inlining benchmark — 2026-11-04

**Measurement VALID. Three-workload retention: NOT_ESTABLISHED.**

**Hires performance recovered against the original pre-optimization reference.**
The fixed candidate is +3.0532% faster in hires DMA (two-sided 95% interval
+0.0119% to +6.1871%) and passes the 1% frame-time retention bound. Lemmings
shows no established speedup or slowdown. Lemmings and lores do not establish
their 1% retention bounds, so the complete three-workload target remains open.

This is a fresh full comparison of the unchanged frozen inlining fix against the same original pre-optimization reference. Earlier invalid attempts remain unchanged and are excluded.

| Workload | Reference mean FPS | Fixed mean FPS | Paired FPS change | Two-sided 95% interval |
| --- | ---: | ---: | ---: | ---: |
| lemmings | 353.3050 | 350.6717 | -0.7596% | -2.7803% to +1.3031% |
| hires | 334.2783 | 344.3217 | +3.0532% | +0.0119% to +6.1871% |
| lores | 360.3650 | 361.8800 | +0.3762% | -2.2489% to +3.0718% |

Means are arithmetic; changes use geometric paired ratios. Six pairs per workload, df=5, with Student-t critical values 2.570581835636305 for two-sided 95% throughput intervals and 2.0150483733330233 for the one-sided 95% frame-time bound. An interval crossing zero does not establish a speedup or slowdown.

| Workload | One-sided 95% upper frame-time change | 1% retention |
| --- | ---: | --- |
| lores | +1.7164% | NOT_ESTABLISHED |
| hires | -0.6571% | PASS |
| lemmings | +2.4035% | NOT_ESTABLISHED |

This tests the complete fixed candidate against the original baseline. It does not directly measure fixed-versus-regressed speedup; changes from separate series are not subtracted.

All 36 complete identities match, with zero measured steady-state allocations. All 1312 telemetry intervals pass, with zero competing jobs/violations. Every frozen file is unchanged. No samples are trimmed or pooled.

Launch requires at least 60 consecutive seconds without classified jobs and with selected/sibling/package utilization each below 10%. During timing, existing host-load policy v2 remains unchanged. Topology is rechecked on the homogeneous Ryzen host; placement remains CPU 2, protected SMT pair 2–3, Normal priority, Balanced power and .NET 10.0.12. Known native ROM probes and Unreal build/cook jobs are additionally rejected by the separately versioned v3 classifier.

Native Lemmings retains 14,520 warmup plus 3,600 measured fields, the reviewed ROM/two disks/script, PAL OCS/68000, 512 KiB Chip plus 512 KiB slow RAM, full 908×313 output, conservative interpreter batching and real PCM. Synthetic workloads retain 600 warmup plus 7,200 measured fields. Both roles use the same unpublished Copper68k 1.5.5-prefetch-metadata-dev.1 package; only the engine DLL differs.

Prior qualification remains 1,300 passing engine cases (seven optional groups unavailable), 320 matched native capture files and confirmed hires fallback inlining. No emulator source/build/test changes are made for this rerun. Release qualification and default-published-CPU throughput remain separate.

[Machine-readable evidence](HIRES_FALLBACK_IDLE_2026-11-04.json). Raw evidence: `artifacts/hires-fallback-idle-20261104/`. [Implementation and earlier invalid attempts](HIRES_FALLBACK_INLINE_2026-11-04.md).
