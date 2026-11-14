# Transparent hires sprite benchmark — 2026-11-04

This is a fresh paired measurement of the frozen engine candidate from the
[2026-11-03 implementation record](HIRES_TRANSPARENT_2026-11-03.md). The earlier
invalid samples and their records remain unchanged and are excluded here.

**Measurement VALID. No Lemmings speedup is established. Hires DMA regresses.**
The candidate does not establish the full three-workload 1% retention target.
These results do not justify retaining it as an FPS optimization.

## Scope and qualification

Both capsules use the same unpublished Copper68k
**1.5.5-prefetch-metadata-dev.1** package, CPU DLL SHA-256
`20B4E1002E8F7B2994EB139E04905BE1B5AC2970C360C4E2B1F682CA14029133`.
Only the engine DLL differs. This isolates the transparent hires sprite change
after the CPU metadata cleanup; it does not measure the default published CPU
package or combine this result with the earlier CPU-package gain.

The prior production builds, **1,287 passing engine cases** (seven optional
groups unavailable), collision-guard mutation check and **320 matched native
capture files** apply to the unchanged frozen pair. They are retained evidence,
not newly run suites. No new build or tests run during this measurement.

The old qualification's source/checkout hashes no longer all match after the
subsequent checkout commit and formatting. A fresh qualification records those
six changed metadata entries and their old/current hashes, pins the original
qualification, and verifies every frozen executable, native capture and test
record unchanged. It does not replace historical hashes or claim a rebuild of
the current checkout. Fresh file pins then remain enforced throughout timing.

## Measurement

The existing [versioned protocol](../../scripts/measure-hires-transparent-v2.ps1)
plans **six pairs per workload, 36 complete runs**. Each round runs lores DMA,
hires DMA and native Lemmings. Odd pairs run reference/candidate; even pairs run
candidate/reference. Preflight and ten-second cooldowns retain host-load policy
v2 and the separately recorded idle-build-node classifier.

Native Lemmings uses the retained ROM, two disk archives and portable input
script, **14,520 warmup plus 3,600 measured fields**, PAL OCS/68000, 512 KiB Chip
plus 512 KiB slow RAM, full **908×313** output, conservative interpreter batching
and real stereo PCM. Synthetic runs use **600 warmup plus 7,200 measured fields**
and the previously retained lores/hires identities. Exact CPU/hardware/output
fingerprints, final cycles, field counts and zero steady-state allocation are
required for each result.

Topology is rechecked on the homogeneous Ryzen 5 5600X. Placement is logical CPU
2, protected SMT pair 2–3, Normal priority, Balanced power, and .NET 10.0.12.
No workload, fingerprint, placement requirement or host-load threshold changes.

All **36 runs complete** and match their full retained identities, with **zero
measured steady-state managed allocation** and no unsupported operation. All
frozen files remain unchanged. No sample is trimmed, rejected or pooled with
earlier attempts.

## Results

Each row contains six pairs. Mean FPS values are arithmetic means; paired FPS
changes and confidence intervals use the geometric paired ratios.

| Workload | Reference mean FPS | Candidate mean FPS | Paired FPS change | Two-sided 95% interval |
| --- | ---: | ---: | ---: | ---: |
| Lemmings | 356.7050 | 357.5133 | +0.2248% | −1.2826% to +1.7551% |
| Hires DMA | 344.4300 | 335.5733 | −2.5702% | −4.8113% to −0.2764% |
| Lores DMA | 366.3683 | 367.9067 | +0.4224% | −0.5241% to +1.3780% |

Lemmings and lores intervals include zero, so neither establishes a speedup or
slowdown. The hires interval lies below zero and establishes a slowdown for this
frozen workload. The smaller inspected renderer from the implementation record
does not translate into a demonstrated native FPS benefit.

The declared retention test uses the one-sided 95% **upper frame-time change**,
which must be at most **+1%** for each workload:

| Workload | Paired frame-time change | One-sided 95% upper bound | Retention |
| --- | ---: | ---: | --- |
| Lemmings | −0.2242% | +0.9680% | PASS |
| Hires DMA | +2.6380% | +4.5275% | NOT ESTABLISHED |
| Lores DMA | −0.4206% | +0.3214% | PASS |

Overall retention is **NOT ESTABLISHED** because of hires DMA. Native retention
passing does not establish a native speedup. These measurements also do not
establish release acceptance or performance with the default published CPU.
No engine source is changed in this benchmark task; the measured candidate is
still present in the checkout. Reverting or redesigning it is warranted before
treating it as an optimization.

Statistics use reference FPS / candidate FPS as the frame-time ratio and
Student-t intervals on six paired log ratios, **df=5**. The critical values are
**2.0150483733330233** for the declared one-sided 95% frame-time bound and
**2.570581835636305** for the two-sided 95% throughput interval.

## Telemetry and independent checks

Independent analysis rereads all raw stdout, verifies exact identities and file
hashes, recomputes the paired statistics, checks every measurement/cooldown
segment, and reapplies policy v2 and the build-node classifier. All **1,330
telemetry intervals** pass, with **zero violations, zero competing-job intervals,
zero raw build-job presence and zero idle-node exclusions**. Thus the result
does not depend on excluding idle build processes.

Peak selected-CPU other activity is **11.25%**, sibling activity **17.44%**, and
package interference **42.08%**. The package peak is transient and does not reach
the ten-second sustained rejection period. Missing/invalid intervals remain
fail-closed. The earlier apparent +16.4317% native change from invalid loaded-host
data is not validated by this clean series; the earlier records keep their
original INVALID / RERUN disposition.

## Evidence

Raw stdout/stderr, native telemetry, frozen-file checks, checkout revalidation
and independent paired analysis are retained under
`artifacts/hires-transparent-benchmark-20261104/`. The original evidence remains
under `artifacts/hires-transparent-20261103/`.
The [machine-readable report](HIRES_TRANSPARENT_BENCHMARK_2026-11-04.json) contains
every pair, host/runtime settings, revalidation metadata and 651 evidence-file
hashes. Historical records are verified unchanged.
