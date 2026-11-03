# Paired Lemmings interface cache measurement 2026-11-03

**VALID native comparison; no FPS benefit established.** The cached engine has a
paired throughput estimate of **−0.7082%**, with a two-sided 95% interval of
**−2.0616% to +0.6640%**. This series establishes neither a speedup nor a slowdown.
The one-sided 95% upper frame-time change is **+1.8026%**, so native retention
within the 1% budget remains **INCONCLUSIVE**. No full three-workload or release
performance acceptance is claimed.

The [machine-readable record](LEMMINGS_CACHE_2026-11-03.json) binds the raw results,
independent calculations, telemetry and frozen inputs. The
[earlier isolation report](INTERRUPT_BOOKKEEPING_2026-11-02.md) remains a separate
loaded artificial-poll diagnostic; its per-poll observations are not substituted
for these native results.

## Results

| Engine | Samples | Mean FPS | FPS range | Mean ms/field |
| --- | ---: | ---: | ---: | ---: |
| Correct interrupt sampling without cache | 6 | 329.4533 | 326.67–336.46 | 3.03565 |
| Correct interrupt sampling with cache | 6 | 327.1117 | 323.99–331.54 | 3.05722 |

| Pair | Uncached FPS | Cached FPS |
| --- | ---: | ---: |
| 1 | 327.20 | 327.47 |
| 2 | 326.67 | 323.99 |
| 3 | 328.57 | 325.50 |
| 4 | 327.34 | 331.54 |
| 5 | 336.46 | 327.77 |
| 6 | 330.48 | 326.40 |

All twelve observations are retained without trimming. The point estimate is
the geometric mean of paired cached/uncached FPS ratios, rather than a ratio of
the arithmetic means. Frame-time ratios are uncached FPS / cached FPS. Student-t
intervals use paired log ratios with df=5: 2.0150483733330233 for the declared
one-sided 95% bound and 2.570581835636305 for the separately reported two-sided
95% interval. The paired frame-time point estimate is +0.7132%.

## Frozen comparison

The separately versioned
[`measure-lemmings-cache-v1.ps1`](../../scripts/measure-lemmings-cache-v1.ps1)
compares the qualified November 2 capsules. Both contain the corrected sampling
behavior and public Copper68k 1.5.4. Runner, dependency DLLs, runtime configuration
and other capsule files match byte for byte; only the engine DLL differs.
No rebuild, emulator test suite or profile runs during this measurement.

| Engine DLL | SHA-256 |
| --- | --- |
| Uncached | `859B6D399CE169189C27581D634A7DCC6165503B186828D7BCE00050F4380A98` |
| Cached | `8056A36DBABF5604408914C09F7FDD0AC7FF8D07D60929A764AF3AD8C6332AE5` |

The order is R1/C1/C2/R2/R3/C3/C4/R4/R5/C5/C6/R6, where R is uncached and C is
cached. Every process uses 14,520 warmup and 3,600 measured complete fields with
the same native Kickstart 1.3 ROM, Lemmings disk archives and reviewed portable
input script. Profile is PAL OCS/68000, 512 KiB Chip plus 512 KiB slow RAM,
interpreter conservative batching, full 908 by 313 pixels and real stereo PCM.
This is uncapped engine throughput, not desktop presentation FPS.

Both roles must match one common complete identity established before timing:
cycle `2574756906`, CPU `0x0BC2B7554DA315AB`, hardware `0x8F41668D6DF17C3C`,
output `0x36816A410DDEEE52`, 284,204 pixels and 1,924 final audio samples. Every
run matches, reports zero measured steady-state allocation and has no unsupported
feature. Raw stdout and all frozen files are independently rechecked afterward.

## Measurement validity

Host is the rechecked homogeneous, single-group Ryzen 5 5600X. All runs retain
CPU 2, protected SMT pair 2–3, Normal priority, Balanced power and .NET 10.0.12.
Production runtime defaults apply with no diagnostic/JIT overrides. All binary,
runtime, ROM/media/script and harness hashes remain unchanged.

There are **682 valid telemetry intervals**, **zero competing build/test-job
intervals** and **zero host-load policy-v2 violations**. Selected-CPU interference
excluding the benchmark peaks at 22.89%, sibling activity at 20.73% and package
interference at 39.45%. The package peak is transient; it does not persist for
the rejection period. Duration-weighted package interference is 8.95%. Ten-second
preflight/cooldowns, in-process placement checks, native counters and the unchanged
sustained-load rejection thresholds remain enabled.

The cache removes a repeated interface check, but this complete native series
does not demonstrate a throughput benefit. Normal desktop activity, runtime/JIT
placement and unmeasured clock/thermal effects can still contribute variation
within the accepted host policy. The reported interval, including the faster
fifth reference run, remains the result. No source, golden or prior evidence is
changed to obtain a different conclusion.

Raw evidence is under `artifacts/lemmings-cache-20261103/`. The optimization source
is unchanged by this benchmark-only task.
