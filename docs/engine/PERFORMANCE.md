# Lightweight performance measurements

Correctness, host throughput and interactive compatibility are separate results.
G6/G7 Legacy cutover is retired. Lightweight is already the active engine; ordinary
development does not repeat that cutover or the completed H-stage sequence.

This guide separates the still-useful host-load policy v2 rules from retired G6
acceptance instructions. Historical placement and thresholds remain attached to
their original protocols. The separately versioned homogeneous-host protocol below
supports current-machine comparisons without reclassifying historical evidence.

## Current availability

As audited on 2026-09-17, this machine is an AMD Ryzen 5 5600X (6 cores / 12 logical
CPUs). The retained native scripts explicitly require a hybrid P/E-core topology
and reject a homogeneous topology. Their frozen workload hashes and the retention
script's expected output also predate the current portable native-test workload.
They are not ready-to-run formal current-build measurements on this host.

Use [native validation](../NATIVE_VALIDATION.md) for portable correctness replays.
Short runner smokes and profiled execution can support diagnosis, but cannot be
reported as formal retention or acceptance measurements. Missing local media is
unavailable coverage, not a passing native replay.

| Retained tool | Purpose and boundary |
| --- | --- |
| [run-lightweight-native-retention.ps1](../../scripts/run-lightweight-native-retention.ps1) | Frozen Lightweight-to-Lightweight comparison. Historical hybrid-host requirement and older exact fingerprints; needs a separately versioned portability update. |
| [run-lightweight-native-paired.ps1](../../scripts/run-lightweight-native-paired.ps1) | Historical Lightweight/Legacy comparison, requiring an external frozen Legacy runner. Not a product release prerequisite. |
| [run-lightweight-h2-controlled-performance.ps1](../../scripts/run-lightweight-h2-controlled-performance.ps1) and H3/H4 wrappers | Historical synthetic stage workloads; their CPU/efficiency defaults and workload contracts are not current-host defaults. |
| [agnus-g6-host-load.ps1](../../scripts/agnus-g6-host-load.ps1) | Shared host telemetry used by the Lightweight scripts. Its G6 name does not make it disposable. |
| [run-lightweight-homogeneous-retention-v1.ps1](../../scripts/run-lightweight-homogeneous-retention-v1.ps1) | Current homogeneous Windows host comparison; full lores/hires fixtures and portable native Lemmings, with the per-change 1% budget below. |

The old G6 controlled runner and contention-test script cited in historical policy
are not included here. Their recorded tests are historical evidence, not locally
available checks. The new protocol independently checks current topology and telemetry.

## Measurement integrity retained from policy v2

Use frozen Release binaries without diagnostic instrumentation. Record all relevant
assembly identities, source state, .NET/runtime, OS, CPU topology, affinity, Normal
priority and active power plan. Verify actual placement, including SMT siblings,
at launch and throughout sampling. Historical P-core requirements remain attached
to their protocols; do not substitute an old logical-CPU number on another host.

Normal desktop applications are allowed. Process presence or another process's
affinity mask alone does not establish interference. Another active test, explicit
build or benchmark invalidates a series; idle reusable build-server nodes do not.

A series is INVALID / RERUN if any signal stays at or above its limit for 10
continuous seconds. Each duration accumulator resets below its threshold.

| Signal | Limit |
| --- | --- |
| Selected logical CPU activity excluding the pinned benchmark | 25% of that logical CPU |
| Activity on any SMT sibling | 25% of that logical CPU |
| Aggregate CPU activity excluding the benchmark | 25% of total logical-CPU capacity |

Use monotonic elapsed time and native per-logical-CPU accounting. Sample regularly
(approximately 1–2 seconds in retained protocols), including warmup and the final
partial interval. Missing/stale/nonmonotonic counters, invalid deltas, gaps over
10 seconds or unsupported multi-group mapping fail closed. Preserve at least
10 seconds of monitored preflight and the protocol's cooldown. Do not ignore
contaminated intervals or select favorable samples from an invalid series.

Aggregate load is a CPU-pressure proxy, not a thermal/clock measurement or proof
that a particular application caused a slowdown. Keep full-precision telemetry
for decisions. Preserve prior results with their original classifications.

## Workloads and thresholds

The native Lemmings protocol boots to checkpoint 10,320, warms up 600 gameplay
fields, then measures fields 10,921–14,520 (3,600 fields). All CPU work, DMA,
908×313 output and real 48 kHz stereo PCM remain enabled and consumed. Three
samples per build use R1/C1/C2/R2/R3/C3 order, deterministic within-build results,
zero measured steady-state allocation and no unsupported bypass. Either build's
spread must be at most 10% of its median.

The accepted Lightweight native objective was at least 200 FPS (5 ms/field), with
at least 95% matched-reference retention for later active-path checks. Preserve
those criteria when describing that protocol. The old G6 45 FPS requirement and
Legacy-default switch condition are retired. Historical inactive-path 97% and
other H-stage budgets remain with their specific workloads, not universal new gates.

The 200 FPS objective was satisfied in the recorded original-host experimental
comparison; it is not a measured property of this Ryzen host. Establish a fresh
same-host baseline before drawing performance conclusions. If a current-host
target or topology rule changes, version that policy explicitly and explain it;
do not lower a threshold or relabel a v2 result to obtain PASS.

Synthetic STOP, synthetic CPU loops and native gameplay execute different work.
The roughly 450 FPS STOP fixture cannot be compared directly with active native
gameplay. Record milliseconds per complete field as well as FPS. Inclusive CPU
samples include device work during bus waits; do not add them again as device cost.

## Homogeneous-host retention v1

The new `run-lightweight-homogeneous-retention-v1.ps1` is separate from the retained
hybrid scripts. It requires one processor group and one CPU-set efficiency class,
checks topology against native per-CPU telemetry, pins one declared logical CPU at
Normal priority and protects every SMT sibling sharing that physical core. The
Ryzen 5600X validation selected logical CPU 2 with sibling 3. It uses the unchanged
host-load policy v2 helper, monitored ten-second preflight/cooldowns and placement
checks throughout every run. Missing-telemetry and sustained-noise rejection probes
plus live telemetry validation run before measurement. This does not certify other
topologies automatically.

Freeze Release reference/candidate directories before measurement. For an engine-only
change, use the identical runner and dependencies in both directories, changing only
the engine assembly. Record source changes separately as well as all assembly/input
hashes. Never build or run tests while a measured series is active. Evidence directories
are immutable; invalid series retain their raw logs and INVALID disposition.

Three complete workloads run six paired samples each, in
R1/C1/C2/R2/R3/C3/C4/R4/R5/C5/C6/R6 order:

- `--synthetic-paula-dma --wide-output --warmup 600 --frames 7200`.
- The same with `--hires` replacing `--wide-output`.
- Portable native Lemmings: checkpoint 10,320, 600 gameplay warmup fields and
  3,600 measured fields, with the current native regression fingerprints below.

All include CPU/device work, full pixels and real stereo PCM. Every reference and
candidate sample must produce the same complete fingerprint within each workload,
zero measured allocations and no unsupported-mode bypass. Native inputs include
the ROM, first disk, script and every script-referenced media file; actual hashes
are recorded. Unchanged full-work fingerprints guard against skipping work.

For the dual-playfield change the user specified a **1% maximum regression** and
confirmed that acceptance requires the **one-sided 95% upper bound at most 1%**.
Compare per-pair frame times (`reference FPS / candidate FPS`) using the geometric
mean across six pairs. Report the increase and its one-sided 95% Student-t upper
bound in log space (df=5). PASS requires the upper bound at most 1%; a measured
increase over 1% requires owner acceptance, and a point estimate within 1% with an
upper bound above it is INCONCLUSIVE. This is a stricter frame-time interpretation
of a throughput limit, not a universal new H-stage threshold. Both test fixtures
measure retention of existing supported work; newly enabled dual-mode correctness
and cost are separate from an impossible pre-feature native rendering comparison.

The harness writes full-precision load samples, per-run outputs, sample FPS/ms per
field, hashes and summary disposition. Runner FPS is rounded to two decimals;
this quantization is much smaller than 1% at these rates. Statistical confidence
does not prove absence of every thermal/clock/systematic effect, nor measure desktop
presentation, device pacing or every native title. Keep those limits explicit.

### Dual-playfield first candidate, 2026-09-17

The first complete v1 series used Ryzen 5600X logical CPU 2 / sibling 3,
Normal priority, Balanced power, Windows 26200 and .NET runtime 10.0.11.
All 36 samples passed telemetry, placement, zero-allocation and matching-output
checks. Engine-only frozen comparison: reference SHA256
`9186289E9887460C5C6CE8BAB858B940DF3DE41CFDA4DB3738F3373A63D0D7C5`,
first candidate `826699509BF85AF5ADECAB5F07669DCE43A920B21C1D1E9CD2B017F08017517F`.
The identical runner SHA256 was
`40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF`.

| Workload | Paired mean frame-time change | One-sided 95% upper bound | 1% disposition |
| --- | ---: | ---: | --- |
| Lores full pipeline | +0.627% | +1.757% | INCONCLUSIVE |
| Hires full pipeline | +0.868% | +2.706% | INCONCLUSIVE |
| Native Lemmings gameplay | −0.316% | +0.322% | PASS |

These means do not show a regression above 1%, but rendering variability prevents
certifying that ceiling. No performance waiver or global PASS is implied. The
inline-table optimization trial reached +0.852% mean and +3.211% upper bound in
its completed lores pairs, also failing the criterion. That trial was deliberately
stopped and discarded; its incomplete/INVALID full-series status remains recorded
under `artifacts/dual-playfield/retention-inline-v1/`. The original, fully measured
implementation was restored before the further optimization below. This first
candidate did not meet the lores/hires confidence bounds; its average alone was
insufficient under the confirmed criterion. Its local raw evidence is in
`artifacts/dual-playfield/retention-v1/`, including all samples, full-precision
telemetry and input/build hashes. Frozen binaries and these generated artifacts
are local evidence, not files shipped with the repository.

### Dual-playfield optimized candidate, 2026-09-17

The retained optimized engine SHA256 is
`5F6E27939729365C6AE470D4605E7FB3EB9C3981B093FB59A67D48D839595C1B`.
It uses the same original reference, identical runner, inputs, host placement,
Normal priority and unchanged homogeneous-retention-v1 protocol above. All 36
samples passed telemetry, placement, matching full-output and zero-allocation
checks. No samples were excluded from this complete series.

| Workload | Paired mean frame-time change | One-sided 95% upper bound | 1% disposition |
| --- | ---: | ---: | --- |
| Lores full pipeline | −1.707% | −1.153% | PASS |
| Hires full pipeline | −2.506% | −1.344% | PASS |
| Native Lemmings gameplay | −2.100% | +1.431% | INCONCLUSIVE |

**Accepted with a scoped native exception on 2026-09-17.** After reviewing these
results, the owner explicitly accepted the native Lemmings +1.43% upper-bound
exception for engine `5F6E2793…`. Its raw statistical disposition remains
INCONCLUSIVE; the faster mean does not meet the 1% upper-bound requirement.
Lores and hires pass without an exception. This acceptance is limited to this
build, host and native workload; it does not relax future 1% checks or transfer
the earlier candidate's native PASS. Desktop presentation and other hosts are
outside this measurement.

The retained changes pack the six shifters into 96 bits in pixel order, deposit
plane data at the existing reload phase, replace the parity loop with independent
phase checks, and avoid calling the input-application method when no input is due.
No CPU/device work, pixels or PCM are skipped. The Release solution builds cleanly;
all 446 engine tests pass with intrinsics enabled and disabled. The native
dual-playfield intro retains its exact fingerprints; see
[native validation](../NATIVE_VALIDATION.md#dual-playfield-verification-2026-09-17).

Profiling found most samples in the inlined device/video loop. Discarded diagnostic
trials included skipping hidden-pixel color extraction, UInt128 helper-based
packing and an out-of-line renderer. The intermediate packed-shifter build
`9FAD41675F947CAE91230B2CA4EE0B2B47A1522BDD9C65763BE028BE5FCB6D35`
completed six lores pairs at −0.86% mean / +1.35% upper bound. That series was
deliberately stopped because lores already failed; its incomplete/INVALID status
remains under `artifacts/dual-playfield/retention-packed-phases-v1/`.
Short diagnostics are engineering evidence, not acceptance measurements.

The final complete raw series is `artifacts/dual-playfield/retention-due-input-v1/`:
`summary.json`, `samples.json`, `telemetry.jsonl`, per-run logs and `protocol.log`
retain exact results and environment/build/input identities. The frozen bundle is
`candidate-due-input/`; `source-due-input.patch` and
`source-due-input-manifest.json` record source identity in the same local evidence
root. All earlier evidence remains unchanged. Generated evidence and media are
not committed. The owner's decision is recorded separately in
`artifacts/dual-playfield/acceptance-due-input-2026-09-17.txt`; raw summaries are
unchanged.

## OCS completion work, 2026-09-18 — accepted with scoped exceptions

The ongoing collision/UART/write candidate is measured against the accepted
dual-playfield engine `5F6E27939729365C6AE470D4605E7FB3EB9C3981B093FB59A67D48D839595C1B`,
using the identical frozen runner. The earlier native +1.431% exception applies
only to the preceding build. The default requirement remains a one-sided 95%
upper bound at or below 1% for each workload, subject to the explicit scoped
exceptions recorded below.

Short engineering trials exposed collision-path overhead and guided optimization.
They lack full host-load telemetry and cannot establish acceptance. An initial
candidate load was blocked by Windows Code Integrity; the owner's later retry
succeeded. The first controlled retry was interrupted to repair a native collision
discrepancy and remains incomplete. The corrected, further optimized candidate
completed the unchanged full protocol in `retention-hires-collision-work/`.
Its engine SHA256 is
`754C8A3DE511F65DD82E52F4485F89E888E0A7E6921736A019CC4B44BA5EB7C0`.

| Workload | Paired frame-time change | One-sided 95% upper bound | Result under the owner's limit |
| --- | ---: | ---: | --- |
| Lores | +0.333% | +1.696% | Does not pass; harness labels INCONCLUSIVE |
| Hires | -1.155% | +0.198% | PASS |
| Native Lemmings | +1.296% | +3.582% | Does not pass; ACCEPTANCE_REQUIRED |

All 36 samples retain the expected complete-workload fingerprints, zero measured
allocations and no unsupported-mode reports. Topology, Normal priority, affinity
and host-load checks passed. On 2026-09-18 the owner explicitly accepted the
lores +1.696% and native Lemmings +3.582% upper-bound exceptions for this exact
build. **Performance accepted with scoped exceptions.** The raw harness labels
and measurements remain unchanged; this is neither a blanket future waiver nor
an OCS-completeness claim. The decision is separately recorded in
`artifacts/ocs-completion-2026-09-18/acceptance-hires-collision-work-2026-09-18.txt`.

Current throughput on this host, arithmetic means of the six candidate samples:

| Workload | Mean FPS | Observed FPS range |
| --- | ---: | ---: |
| Lores synthetic complete workload | 370.13 | 358.89–379.25 |
| Hires synthetic complete workload | 334.92 | 327.63–340.79 |
| Native Lemmings gameplay | 248.48 | 239.65–254.27 |

These are uncapped engine-throughput measurements with real PCM work, not desktop
presentation rates. Normal PAL playback remains approximately 50 fields/second.
Trial paths, build identities, correctness evidence and the original blocked runs
are in [OCS_COMPLETION.md](OCS_COMPLETION.md). Earlier evidence remains intact.

## Historical portability boundary

The v1 harness above supplies a separate homogeneous-core/SMT placement rule,
Normal priority and the same host-load integrity controls. The historical scripts
below remain frozen; do not silently relax their requirements or replace hashes.

Use portable media parameters and disk-swap paths, record actual input hashes and
validate the declared native workload against current regression expectations.
Both retained native scripts demand script hash `B2CFB175...`; neither checked-in
level-1 script matches it. The runner exploratory version also embeds an old
`D:/TestData` path; the host-test version uses a repository-relative media path.

The historical retention script expects cycle `2063321672` and output
`514760AD9BC62BCE`. Current native-test expectations are cycle `2063321634`, ordinary
replay CPU `6AE7090DA8AFB7E3`, output `C65F87325946E5DA`. These are distinct recorded
contracts. Independently validate any new harness's expectations; never overwrite
historical fingerprints just to make a changed build pass.

Freeze both reference and candidate Lightweight builds on this host and use a fresh
evidence directory. Each result must record date/host, source and assembly hashes,
ROM/media/script identities, runtime, protocol version, interval, samples,
fingerprints, allocations, telemetry and disposition. A hardware correction may
justify new fingerprints only with its own evidence and regression coverage.

## Historical evidence index

The [archive](../history/amiga/README.md) preserves full protocols, hashes and sample
tables. Referenced original-machine raw logs and frozen directories are not in this
checkout; these rows summarize recorded evidence, not fresh verification.

| Record | Recorded finding | Classification |
| --- | --- | --- |
| [450 FPS audit, 2026-09-14](../history/amiga/LIGHTWEIGHT_A500_450_FPS_AUDIT.md) | Original-runner matched work: 447.43 vs 445.31 FPS; wider display/device work explains much of the changed synthetic cost. | Provisional engineering evidence. |
| [Native profile, 2026-09-14](../history/amiga/LIGHTWEIGHT_A500_NATIVE_GAMEPLAY_PROFILE.md) | CPU was the largest sampled native cost; retained CPU changes included a 347.58 vs 310.03 FPS matched comparison. | Dated profiling and optimization evidence, not today's hotspot ranking. |
| [Staged execution record](../history/amiga/LIGHTWEIGHT_A500_ENGINE_PLAN.md) | Complete-workload comparison: 343.10 FPS Lightweight vs 28.41 FPS frozen Legacy. | Recorded valid experimental comparison on the original host. |
| [Supported-v1 acceptance, 2026-09-15/16](../history/amiga/LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md) | Three final retention series remained host-invalid; owner accepted the frozen build by a one-off waiver. | Build accepted; underlying INVALID / RERUN labels unchanged. No blanket future waiver. |

Neither consolidation nor the machine move reopens those accepted sessions or
requires reruns solely to recreate historical gates.
