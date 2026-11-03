# Interrupt bookkeeping isolation and interface cache

The engine now resolves `IM68000InterruptRecognition` once when constructing its
fixed CPU core. Each poll still reads the live status register, calls the same
stateful sampler with the same inputs, applies the same acceptance checks and
advances the same interrupt-entry tail. The change removes a repeated runtime
interface check; it does not skip inactive or masked samples.

**Correctness checks pass. Performance acceptance remains pending.** The isolated
loaded-host observations favor the cache on average, but all 68 telemetry intervals
violate host-load policy v2. The series remains **INVALID / RERUN** and supplies no
causal speedup or native FPS claim. The [machine-readable record](INTERRUPT_BOOKKEEPING_2026-11-02.json)
retains source, binaries, raw probes and verification hashes. The preceding
[source-stack investigation](LEMMINGS_PERFORMANCE_2026-11-02.md) remains unchanged.

## Isolation

The three frozen variants use the same runner, CopperDisk and public Copper68k
1.5.4 dependency. Only the engine DLL differs:

- Gated control: current source with only the old interrupt-gating order restored.
  This known incorrect variant is confined to an ignored diagnostic worktree.
- Reference: the current corrected adapter before this optimization.
- Cached: the same corrected adapter with its interface capability cached.

The [standalone probe](../../scripts/probes/InterruptBookkeeping/README.md) invokes
the real adapter with inactive IPL or masked IPL3. Every poll gets a new late
sample cycle with no deferred-fetch token. Twenty million polls per scenario run
in six balanced permutations: GRC, CRG, RCG, GCR, CGR, RGC. All eighteen processes
reject every request and allocate zero bytes in their measured loops. Reference
and cached variants finish with exactly matching IPL pipeline states in both
scenarios in all six rounds. The gated control leaves the sampler unevaluated.

This is an artificial CPU-interface diagnostic. Common sample-setter, delegate
and loop costs are included in every variant. No guest instructions, DMA, pixels
or PCM execute in this probe. Thread CPU time and thread cycles reduce the effect
of descheduling in the reported counters, but do not remove SMT, frequency or
other interference. These observations cannot be converted into a native FPS gain.

| Variant | Inactive mean thread ns/poll | Masked mean thread ns/poll |
| --- | ---: | ---: |
| Incorrect gated control | 4.17 | 6.12 |
| Correct reference | 18.10 | 17.45 |
| Correct cached interface | 16.41 | 15.89 |

These are arithmetic means over all six rounds without trimming. The observed
reference-minus-gated differences are 13.93 and 11.33 ns/poll. The observed
reference-minus-cache differences are 1.69 and 1.56 ns/poll. They are loaded-host
diagnostic estimates, not established causal costs or performance acceptance.

The separately versioned
[`measure-interrupt-bookkeeping-v1.ps1`](../../scripts/measure-interrupt-bookkeeping-v1.ps1)
pins Normal priority, CPU 2 and SMT pair 2–3 on rechecked homogeneous topology,
.NET 10.0.12, all binaries/runtime/tool hashes, ten-second preflight/cooldowns
and unchanged policy-v2 thresholds. Explicit loaded diagnostics preserve all
violations; missing counters, placement, changed files and probe-contract failures
still stop. Peak selected-CPU interference reaches 93.92%, sibling activity 100%
and package interference 97.40%. Weighted package interference is 70.49%.
This task runs no builds, tests or other workloads during the cost series.

Tiering is disabled for this FullOpts isolation; production dynamic PGO is not
modeled. Separate optimized-code dumps confirm removal of
`CORINFO_HELP_ISINSTANCEOFINTERFACE` from the poll while retaining the sampler
call. The adapter decreases from 257 to 239 bytes. This establishes the removed
work, not its native throughput contribution.

## Behavior verification

The production Release solution builds with zero warnings/errors. All five
focused interrupt cases pass. The full isolated engine suite passes 1,285 tests;
seven optional native groups remain unavailable. Both repeated-request cases
fail on the gated diagnostic control: the second stacked PC is `$1108` instead
of `$110A`. They pass on the cache candidate, confirming why gating cannot be
restored as an optimization.

Both complete 18,120-field Lemmings replays, normal and scalar, match all 160
baseline capture files per mode: CPU/device JSON, Chip/slow RAM, framebuffer and
PCM. Their final cycle, CPU, hardware and full-run output checksums also match.
Full lores and hires workloads each retain the reference's complete identity,
real PCM, zero measured allocations and no unsupported feature. Captured-run
allocations and FPS are diagnostic and are not substituted for the steady-state
runner protocol.

A separate production replay using 14,520 warmup and 3,600 measured fields also
reproduces the existing complete native benchmark identity with real PCM,
zero measured allocations and no unsupported feature. Its unmonitored loaded
FPS is excluded from performance conclusions.

The fixed CPU is not replaced by reset or task-context copying. Caching its
interface therefore preserves capability identity for the machine's lifetime;
register values and sampler state remain live. No CPU package, device ordering,
guest inputs or golden is changed. Native/three-workload performance acceptance
remains open until an uncontaminated paired comparison is available.

Local evidence stays under `artifacts/interrupt-bookkeeping-20261102/`. ROMs,
media, captures, control worktrees and generated binaries remain ignored.
