# Empty bitplane shifter optimization — 2026-09-22

**DISCARDED at the owner's request on 2026-09-22.** The fast path was removed,
restoring the accepted engine source. No performance exception was granted.
The measurements and frozen candidate remain as evidence of the unsuccessful
experiment; this document does not describe a retained runtime optimization.

The discarded candidate avoided shifting and rewriting already-zero bitplane shifter state in
`LightweightVideo.ShiftPlayfieldPair`. The pending-reload path runs first, so the
optimization cannot suppress a reload or move its pixel boundary. The renderer
still receives the zero pixel pair and performs its ordinary palette, HAM,
sprite, collision and border processing. Hardware clocks and device ordering
are unchanged; no cached state, counters or allocation enter production execution.

The [operation-count investigation](OPTIMIZATION_TARGETS_2026-09-22.md) identified
this work in 41.99% of lores pair shifts, 42.17% of hires pair shifts and 71.73%
of native Lemmings pair shifts. These frequencies motivated the change; they
do not predict its FPS improvement.

## Candidate identity and correctness

- Accepted reference source: `9be9feef098c9850ee7d8e645121593551a11bb3`.
- Reference engine SHA256: `8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.
- Candidate engine SHA256: `25FC088CEEA8BFB738279A93277DE848DFD29675798BCD8792802AD3ACA72333`.
- The unchanged accepted runner, Copper68k, CopperDisk and CopperFloat binaries
  are shared by both comparison directories. Only the engine DLL differs.
- Production Release solution build: passed, zero warnings/errors.
- Isolated Release engine diagnostics: 669 passed, zero failed/skipped.
- Complete native Lemmings preflight: accepted CPU/hardware/output fingerprint,
  real PCM, zero measured steady-state allocations.
- Complete hires preflight and lores diagnostic screen: accepted complete
  fingerprints, real PCM, zero measured steady-state allocations.
- Baseline/candidate generated code confirms the zero-state path bypasses the
  two stores and shift/merge operations. The surrounding optimized device method
  grows from 3,607 to 3,625 bytes; code size alone is not a throughput result.

Existing diagnostics exercise the first nonzero DMA/manual reload, every odd/even
scroll combination in lores and hires, mode changes, HAM, dual-playfield priority,
sprites/collisions and window clipping. This is preservation of implemented
behavior; unresolved hardware edges in [ISSUES.md](ISSUES.md) remain unresolved.

## Performance verification

**The complete `comparison-retry5` series is VALID. The candidate does not pass
the owner's 1% gate.** All 36 samples match their complete accepted workload
fingerprints, produce real PCM and report zero measured steady-state allocations.
The 1,600 telemetry samples are valid. Maximum sustained threshold crossings were
0 seconds on the selected processor, 5.667 seconds on its sibling and 5.632 seconds
on the package, all below the unchanged ten-second rejection threshold.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 385.14 | 377.52 | +2.1054% | +6.6078% | ACCEPTANCE_REQUIRED |
| Hires | 345.18 | 347.63 | -0.7078% | +0.9404% | PASS |
| Native Lemmings | 256.13 | 257.59 | -0.5652% | +1.0573% | INCONCLUSIVE |

Positive frame-time changes are regressions. FPS columns are arithmetic means;
the paired change and confidence bound use the frozen log-ratio method with six
pairs and one-sided Student t (five degrees of freedom). An independent analysis
recomputed the statistics and checked all 36 full fingerprints, build hashes and
telemetry. No slow sample was dropped, and no earlier attempt was pooled into this
series. The final required cooldown also completed successfully.

Lores exceeds the limit on both its paired mean and upper bound. Native exceeds
the upper-bound limit despite a small average improvement. Hires passes the
retention gate, but its positive upper bound does not establish a speedup at the
one-sided 95% confidence level. The requested 5% performance gain is not achieved.
The owner rejected the candidate and requested its removal on 2026-09-22.
The runtime change was reverted; no above-limit result has been accepted.
The numerical dispositions above remain unchanged historical results.

The comparison used the frozen
[empty-shifter comparison v1](../../scripts/run-lightweight-empty-shifter-comparison-v1.ps1),
with unchanged timing, workload identities, balanced six-pair order, verified
homogeneous-host placement (LP2, sibling LP3), Normal priority and host-load policy
v2 from live-WORDSYNC comparison v1. Only the named build pins and provenance
differ from that earlier protocol. No historical script or result was modified.

## Earlier attempts and diagnostic evidence

A short two-pair lores
screen completed with valid telemetry and matching identities. Its paired
frame-time changes were -4.56% and -1.09%; this is diagnostic only and does not
establish a retained gain. The screen's first attempt was separately invalidated
by sustained sibling interference for 10.260 seconds.

| Attempt | Point reached | Invalidating sustained interference |
| --- | --- | ---: |
| `comparison` | Lores C1 | Package: 11.408 seconds |
| `comparison-retry1` | Lores C4 | Package: 10.049 seconds |
| `comparison-retry2`, owner-requested resumption | Lores R1 | Package: 11.212 seconds |
| `comparison-retry3`, fresh retry after a quiet host check | 35 of 36 samples; cooldown after native C6, before native R6 | Package: 10.312 seconds |
| `comparison-retry4`, owner-reported quiet system | Initial cooldown, before any measurement sample | Selected processor: 11.678 seconds |

Attempt `comparison-retry3` completed all lores/hires pairs and eleven native samples with
matching complete workload fingerprints and zero allocations. The subsequent
host-load violation invalidates the entire attempt. The provisional lores/hires
statistics reported while it was running are not acceptance results. Its final
telemetry reached 98.61% package load during the cooldown; the benchmark's own
CPU consumption is subtracted by the unchanged host-load policy.

All timing samples from these five invalid attempts remain excluded from acceptance,
including completed workload groups and partial pairs. No samples are dropped
within a series or pooled across attempts. These failures establish neither a
performance gain nor a regression, and do not justify an above-limit acceptance
request. The final reference sample cannot be appended to the invalid series.

A bounded process-counter investigation after retry2 observed a transient
PowerShell process at 200% processor time (approximately two logical processors),
but it exited before its source could be identified. Subsequent process readings
were quiet. Process-start event subscription was unavailable (the CIM call was
cancelled), so no command-line attribution is claimed. No background application,
service, security setting or measurement threshold was changed. The cause of the
recurring CPU bursts remains unidentified.

A separate 45-sample audit after retry4 recorded both native processor counters
and process CPU-time deltas. Its highest observed package load was 15.31%; the
earlier all-core burst did not recur. The diagnostic monitor was stopped before
retry5. The successful attempt used the unchanged formal harness; an outer failure
handler would capture process information only after an invalidation, but was not
invoked. No profiler, build or test ran alongside acceptance measurements.

The [machine-readable record](EMPTY_SHIFTER_OPTIMIZATION_2026-09-22.json) preserves
build identity, all valid paired samples and statistics, telemetry hashes,
references to invalid attempts, and the separate diagnostic screen. Raw partial
samples remain in their invalid attempt directories. Local build/test logs, frozen candidate and
source patch are retained under
`artifacts/empty-shifter-2026-09-22/`. No media or binaries are repository changes.
