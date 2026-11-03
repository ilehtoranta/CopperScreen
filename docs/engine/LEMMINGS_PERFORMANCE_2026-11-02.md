# Lemmings performance investigation 2026-11-02

**A source regression remains unestablished.** The complete six-pair diagnostic
series preserves every expected workload identity, but all timing evidence remains
**INVALID / RERUN** under host-load policy v2. The owner explicitly requested
continuing diagnostics under the current load. A subsequent fresh strict attempt
also stops on sustained interference. No optimization gain, 1% retention decision
or release acceptance is claimed.

The [machine-readable record](LEMMINGS_PERFORMANCE_2026-11-02.json) binds the raw
series, profiles, generated-code observations and tooling. The preceding
[correctness investigation](LEMMINGS_CORRECTNESS_2026-11-02.json) establishes the
separate per-build identities. Historical expectations and protocols are unchanged.

## Comparison scope

Reference is the reconstructed October 12 source at
`575d5cf81681c7ffa5c7d0cd40bbda6a7d16af53`, using the exact historical Copper68k
`1.5.4-detroit-dev.2` DLL. Candidate is the frozen current runner built at
`214d58a37d67c73256cd1fc2192d19da040ec967`, using public Copper68k `1.5.4`.
This compares complete source stacks, including their runner, engine and disk
dependencies; it does not isolate an engine-only or CPU-only cost. Reconstructed
reference assembly hashes are distinct from the historical October capsule.

The separately versioned
[`measure-lemmings-regression-v1.ps1`](../../scripts/measure-lemmings-regression-v1.ps1)
uses PAL OCS/68000, 512 KiB Chip plus 512 KiB slow RAM, interpreter conservative
batching, 908 by 313 pixels and real stereo PCM. Both roles use the unchanged
`lemmings-cpu-bus-timing-v1.json`, native Kickstart 1.3 and retained disk archives.
Each process completes 14,520 warmup plus 3,600 measured fields. Per-build complete
identities are pinned from earlier independent validation, never learned from
timing samples.

The six-pair order is R1/C1/C2/R2/R3/C3/C4/R4/R5/C5/C6/R6. Single-group homogeneous
topology, CPU 2 with SMT pair 2–3 protected, Normal priority, Balanced power and
.NET 10.0.12 are recorded. Native telemetry, monitored ten-second preflight and
cooldowns, full input/runtime/capsule hashes and placement checks remain enabled.
Explicit loaded diagnostics continue only host-load policy violations; missing
telemetry, placement, changed files and identity failures still stop the run.
No other job was paused or terminated, and this task ran no builds, tests or
profiles during either timing series.

## Complete loaded observations

All twelve processes reproduce their independently pinned identities, real PCM,
zero measured allocations and no unsupported features. All frozen files remain
unchanged and all twelve stdout identities were independently rechecked.

| Pair | Reference FPS | Current FPS |
| --- | ---: | ---: |
| 1 | 155.90 | 166.79 |
| 2 | 233.18 | 205.07 |
| 3 | 244.48 | 70.36 |
| 4 | 325.62 | 177.35 |
| 5 | 333.94 | 327.71 |
| 6 | 334.57 | 326.42 |

Every observation is retained. The reference arithmetic mean is 271.2817 FPS and
the current mean is 212.2833 FPS; these loaded means do not estimate a causal
source slowdown. The current build wins the first pair and loses the remaining
pairs, with large changes in exposure to interference. The unchanged reference
varies by over twofold and the unchanged candidate by over fourfold.

The series retains 782 telemetry intervals and 540 load-policy violations.
Competing build/test jobs occur in 371 intervals. Peak selected-CPU interference
excluding the runner is 95.8904%, SMT-sibling activity reaches 100%, and package
activity excluding the runner reaches 98.6758%; duration-weighted package activity
is 53.2780%. One mid-run reference observation records 60.5781 CPU seconds over
97.1233 wall seconds, about 62.37% of one core's time. Host scheduling therefore
measurably delays this CPU-bound workload. This is not a measurement of thermal
or frequency effects or attribution to an individual background application.

The original strict preflight rejects competing tests before timing. After the
loaded series, temporarily quieter telemetry permits one fresh strict attempt.
It completes R1 at 328.40 FPS and C1 at 330.25 FPS, then stops during C2 when
package interference remains at or above 25% for 10.5321 continuous seconds.
The entire attempt remains INVALID / RERUN; its two complete samples are neither
pooled into the loaded series nor selected as a valid comparison. The partial C2
has no complete-workload timing result. All frozen files from both series verify
unchanged independently after completion.

## Diagnostic profiles and source costs

One complete sampled replay per build reproduces its expected identity with
zero measured allocations. `dotnet-trace` 10.0.745401 collects
`dotnet-sampled-thread-time` and converts to Speedscope. Collector and child inherit
CPU 2 and Normal priority. These runs are diagnostic and separate from timing;
they do not have complete continuous host-load telemetry.

[`analyze-lemmings-profile-v1.ps1`](../../scripts/analyze-lemmings-profile-v1.ps1)
checks monotonic/LIFO events and selects the engine thread's CPU_TIME stacks.
Its approximate interior tail is half the reported measured duration, ending one
second before the final sampled ExecuteFrame, as in the earlier profiling method.

| Observation | Reference | Current |
| --- | ---: | ---: |
| Selected window | 10.1250 seconds | 6.3690 seconds |
| Attributed stack intervals | 132 | 91 |
| Largest sampled interval | 162.02 ms | 117.76 ms |
| TickDevices exclusive attributed share | 27.28% | 28.16% |
| RenderHiresCck exclusive attributed share | 6.36% | 6.98% |

Device stepping, video and CPU execution remain the principal sampled costs.
Inclusive parents overlap children; inlining and long sample intervals limit
method attribution. These short windows cannot resolve a small per-change cost.
Interrupt recognition is not separately sampled in the selected current window;
that absence is insufficient to establish its cost or exclude inlined work.

Source and separate optimized-code inspection identify a concrete added cost:
the corrected interrupt adapter now gets CPU state, checks the recognition
interface and calls the stateful IPL sampler even for inactive/masked requests.
The old adapter usually returned before that work. The sampler must continue
receiving those inputs to preserve correctness. Stable Copper68k also retains
the required memory-BTST final fetch after waited reads. Their individual runtime
contributions have not been isolated.

Separate one-field FullOpts inspections disable tiering and use no PGO. The
adapter bodies are 270 bytes reference and 257 bytes current. The smaller current
body still performs the extra work on the formerly cheap path; code size alone
does not establish a gain or loss. Neither dump substitutes for the production
profile or a timed source-stage comparison.

## Disposition

Keep the validated hardware/CPU corrections. The observations demonstrate severe
host interference and identify added interrupt bookkeeping as a candidate for
further source-stage measurement; they do not establish it as the regression
cause. No production engine code, package or golden was changed. A complete fresh
strict series is still needed to quantify a small source-stack difference. The
new native-only protocol does not replace the historical three-workload gate.

Local raw evidence is under `artifacts/lemmings-performance-20261102/`. ROMs,
media, traces, generated binaries and captures remain ignored.
