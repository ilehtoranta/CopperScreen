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
| [run-lightweight-homogeneous-retention-v1.ps1](../../scripts/run-lightweight-homogeneous-retention-v1.ps1) | Homogeneous Windows host retention; full lores/hires fixtures and the portable pre-ERSY native Lemmings identity, with the per-change 1% budget below. |
| [run-lightweight-ersy-comparison-v1.ps1](../../scripts/run-lightweight-ersy-comparison-v1.ps1) | Explicit comparison of accepted `67c13f6` with the ERSY hardware correction: matching synthetic states and independently verified per-build native states. Same six-pair timing, host-load rules and 1% upper-bound limit. |

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

## Storage extension 2026-09-18 — native acceptance required

The [storage candidate](STORAGE.md) is compared against accepted commit `6b5cb1b`.
Previous performance exceptions do not transfer. Every workload must have a
one-sided 95% upper frame-time regression bound at or below 1%; a harness label
of INCONCLUSIVE is not acceptance under this requirement.

The complete frozen reference is in local `artifacts/storage-2026-09-18/reference`.
Candidate `candidate2` retains the **identical frozen runner and CPU dependencies**
and replaces only the engine and its changed CopperDisk dependency:

| Binary | SHA-256 |
| --- | --- |
| Reference engine | `754C8A3DE511F65DD82E52F4485F89E888E0A7E6921736A019CC4B44BA5EB7C0` |
| Candidate engine | `5EB543BCF6AD41E718D72C9ADA2445C64D05341BF233AD59584C83318A076FAA` |
| Candidate CopperDisk | `4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F` |
| Frozen runner, both sides | `40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF` |

`retention-1` is INVALID: candidate startup failed because an internal constructor
signature used by the frozen runner had changed. The original binary entry point
was restored; a mount-time rejection for IPF cell intervals below one CCK was also
added before the new freeze. Its incomplete samples are not pooled with later runs.
`retention-2` completed the unchanged homogeneous-retention-v1 six-pair protocol, Normal
priority, freshly verified logical CPU 2/SMT sibling 3, host-load policy v2, all
complete-workload fingerprints and zero measured allocations. Builds/tests are
excluded from the series. All 36 samples are valid, have matching complete-workload
fingerprints and report zero measured allocations. The result is **not accepted**
under the 1% requirement: native Lemmings needs an explicit exception or further
optimization. Acceptance was requested; no earlier exception is carried forward.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 375.69 | 382.88 | −1.89% | −0.39% | PASS |
| Hires | 337.94 | 345.84 | −2.31% | +0.60% | PASS |
| Native Lemmings | 255.27 | 252.57 | +1.10% | +3.05% | ACCEPTANCE_REQUIRED |

Candidate ranges are 379.93–386.45, 340.29–349.37 and 238.41–257.03 FPS,
respectively. Frame-time statistics use all six paired log ratios and the frozen
one-sided Student-t calculation, not ratios of mean FPS. These are uncapped engine
throughput results with real PCM; normal PAL presentation remains about 50 fields/s.
Local raw `retention-2/samples.json`, `summary.json`, workload logs, `protocol.log`
and `telemetry.jsonl` retain every sample, placement check and host-load observation.

Active IPF and HDF require separate diagnostics because the reference does not
execute these features. The active-IPF fixture adds continuously serviced disk
DMA to the synthetic wide Paula workload using Full Contact disk 1. The HDF
[probe](../../scripts/probes/CopperHdfThroughput/Program.cs) measures validated
512-byte synchronous gateway transfers with OS file caching; it is not emulated
FPS. Neither result substitutes for native compatibility or retention acceptance.

Final candidate diagnostics, logical CPU 2 and Normal priority, run after the
retention series with no concurrent tests/builds/probes: active IPF **263.53 FPS**,
HDF **54.732 MiB/s write / 75.796 MiB/s read**, all with **zero measured allocations**.
IPF runs 600 warmup + 3,600 measured frames; cycle `596828538`, CPU
`F86637B0C371B6EF`, hardware `C6EC0E064201FD0F`, output `97B8ED42D20D7163`.
HDF measures 131,072 validated 512-byte requests per direction, with successful
readback and Update. Logs are `ipf-active-final.log` and `hdf-active-final.log`.
These bounded diagnostics do not collect the formal host-load telemetry and are
not additional acceptance samples.

## Copper WAIT border correction 2026-09-18 — INVALID / RERUN

The [Beast border correction](../NATIVE_VALIDATION.md#copper-wait-wake-up-correction-2026-09-18)
supersedes the storage engine above with
`AF2550B2F47AC851AD2E29B5ECED7F7B39BB2944E63DC9C0931BB2C7ED38DAAF`.
CopperDisk and the frozen runner remain unchanged. A fresh unchanged six-pair
homogeneous-retention-v1 series against `6b5cb1b` is recorded under local
`artifacts/beast-border-2026-09-18/retention/`. Previous exceptions do not transfer;
the preceding storage +3.05% native result does not establish acceptance for this
engine. The complete candidate is frozen; this task ran no builds or tests while
measurements were active. The first series stopped during native R3 because host-load
policy v2 detected a competing `dotnet.exe[25020]` test/build/benchmark. It is
**INVALID / RERUN**, with raw samples and telemetry retained; no results are
pooled with the fresh `retention-retry/` series. That retry also stopped, during
lores R1, on competing `dotnet.exe[16996]`. Neither series establishes acceptance.
Do not repeatedly wait for or terminate unrelated work; rerun the same frozen
candidate in a fresh directory when concurrent tests/builds have finished.

For diagnostic completeness only, the first series completed six lores and hires
pairs before the interruption. Lores averaged 367.33 reference / 373.42 candidate
FPS, paired frame time −1.71%, upper bound +2.19%; hires averaged 321.97 / 334.90
FPS, paired frame time −3.90%, upper bound −1.10%. Only two native pairs completed.
**These are partial results from an invalid series, not acceptance measurements.**
The earlier progress report's hires PASS was provisional and is superseded by the
whole-series INVALID status. All 28 completed samples matched their complete-workload
fingerprints and reported zero measured allocations, but the missing valid full
series leaves the owner's ≤1% requirement unresolved. No exception is requested
from invalid evidence, and no earlier exception transfers to this candidate.

## Trace development pin and Paula transition 2026-09-18 — INVALID / RERUN

The [trace fix](CPU_TRACE.md) supersedes the preceding candidate. Frozen CPU
SHA-256 is `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC`;
engine SHA-256 is `280B6B78321145C018135406409189B68F22AE85944285DFFA93E3294E876903`.
CopperDisk remains `4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F`.
The retained runner, baseline `6b5cb1b`, native input and six-pair protocol remain
unchanged. The fresh candidate directory is
`artifacts/trace-exception-2026-09-18/candidate/`; raw evidence is `retention/`
under the same dated directory.

Topology/placement and telemetry were rechecked by homogeneous-retention-v1,
on logical CPU 2 with SMT sibling 3 protected and Normal priority. This task ran
no builds, tests or other native runners during measurement. Host-load policy v2
invalidated the series during low-resolution sampling on a competing
`dotnet.exe[11284]` test/build/benchmark. Preserve `INVALID.txt`, `samples.json`
and `telemetry.jsonl`; do not pool this attempt with earlier series or calculate
an acceptance bound from its partial samples.

**The one-sided 95% upper bound ≤1% requirement is not established.** No exception
is requested from invalid data. Rerun the unchanged frozen candidate in a fresh
evidence directory when competing work is finished. The completed production,
CPU, engine, disk and host checks and bounded Thunderbolt gameplay are correctness
evidence only. Earlier approvals do not transfer to this candidate.

### Owner acceptance, 2026-09-18

After the trace/Paula result above, the owner explicitly accepted this unresolved
gate and authorized committing and pushing the current changes, followed by a
fresh benchmark run. This is a one-off acceptance of the identified frozen
candidate (CPU `0B9F1B4C...`, engine `280B6B78...`, CopperDisk `4F02010A...`).
It permits committing the current development build without a valid ≤1% bound.
The measurements remain **INVALID / RERUN**; this acceptance does not turn them
into passing measurements, approve future builds or close unverified native
coverage. Follow-up benchmarks retain the same baseline, workloads and protocol.

### Post-push benchmarks, 2026-09-18

CopperScreen source was pushed as `7eda19961226da6baf3791f78db193faab0ae185`
and Copper68k as `ada86020ad7a9b298cd7f689c3731d8d779684d1` on
`codex/68000-trace-exception`. The frozen accepted binaries above were retained
unchanged; no package was repacked or published. The follow-up six-pair run used
the same `6b5cb1b` reference, workload inputs, CPU 2 / protected sibling 3, Normal
priority and host-load policy v2. It stopped during lores R1 on sustained sibling
activity ≥25% for 10.215 seconds. This run is **INVALID / RERUN**; it has no
completed formal samples or confidence bound. Evidence is
`artifacts/trace-exception-2026-09-18/retention-post-commit/`.

Separate single-sample diagnostics then ran the complete unchanged workload
lengths: 600 warmup + 7,200 measured fields for each synthetic mode, and 10,920
warmup + 3,600 measured native Lemmings fields. These are **raw exploratory
observations, not a valid reference/candidate performance comparison**. The
diagnostic collector continued to completion after recording interference;
the formal harness and its rejection rules were not changed.

| Workload | Reference FPS | Current FPS | Timing classification |
| --- | ---: | ---: | --- |
| Lores, Paula DMA / wide output | 315.26 | 319.21 | Current sample INVALID: sustained sibling interference. Reference has one exploratory sample only. |
| Hires, Paula DMA / wide output | 287.28 | 341.69 | Reference sample INVALID: sustained sibling interference. Current has one exploratory sample only. |
| Native Lemmings | 252.95 | 252.14 | Both INVALID: competing test/build processes (`21508`, `11588`). |

All six samples completed without unsupported features or measured steady-state
allocations. Reference/current complete-workload fingerprints match in each mode;
native Lemmings retains cycle `2063321634`, CPU `6AE7090DA8AFB7E3`, hardware
`334A819F6FDFB1CA` and output `C65F87325946E5DA`. No speedup, regression percentage
or confidence-bound conclusion is drawn from these contaminated single pairs.

Separate active-media diagnostics, on the same core/priority, completed with
no host-load rejection recorded in their short observation windows:

- Active IPF: **243.90 FPS**, 600 warmup + 3,600 measured fields, zero allocations;
  cycle `596828538`, CPU `F86637B0C371B6EF`, hardware `C6EC0E064201FD0F`, output
  `97B8ED42D20D7163`, `unsupported=none`. Full Contact disk one is the supplied
  hashed media, with continuously serviced disk DMA and synthetic Paula/wide output.
- CopperHDF: **55.134 MiB/s write, 75.078 MiB/s read**, 131,072 × 512-byte requests
  per direction, zero allocations, readback and Update both pass. This is cached
  synchronous gateway service, not guest FPS or physical-disk throughput.

These short active-media observations are not six-pair acceptance measurements.
The first HDF executable run also passed (54.286 / 75.138 MiB/s), but the diagnostic
wrapper rejected Windows CRLF line endings while parsing the allocation output.
The corrected parser reran only HDF on a new disposable file; both logs remain.
No engine code changed or measurement was pooled.

Local raw outputs, binary/argument manifests and per-sample telemetry are in
`diagnostics-post-commit/` and `diagnostics-post-commit-hdf-retry/` under the dated
trace directory. `run-post-commit-diagnostics-original.ps1` preserves the original
collector and `run-post-commit-diagnostics.ps1` its line-ending correction. This
task ran no concurrent local builds/tests/probes. The owner's one-off acceptance
above still applies; these results do not convert any invalid sample into PASS.

## Lean engine optimization, 2026-09-19 — accepted with an unresolved measurement gate

The owner requested a modest 5% throughput improvement. This comparison starts
from the accepted trace/Paula engine `280B6B78…` above, with the same CPU, disk and
runner assemblies. It does not restart from `6b5cb1b` or inherit the preceding
build's scoped acceptance. The 5% objective and the per-workload one-sided 95%
upper frame-time regression limit of 1% remain unverified for this candidate.

Sampled-thread profiling of the actual lores and native Lemmings workloads found
the device/video loop, bitplane work and CPU dispatch as the main costs. Profiles
include startup/warmup and are diagnostic evidence, not steady-state timing.
The small candidate changes are:

- Keep paired playfield codes in their original adjacent six-bit lanes, with a
  register-derived mask. Retain the scalar reload path and physical pixel order,
  including independent odd/even scroll, HAM and dual-playfield composition.
- Store a full wide-output CCK with one vector store; preserve narrow output and
  the portable intrinsic fallback. No pixels, shifts or collision work are skipped.
- Load validated big-endian memory words directly rather than joining two bytes.
- Calculate the clock-loop endpoint once and retain the CPU/state references
  within a scalar execution quantum. Device deadlines and execution order remain
  unchanged; no scheduler, callback, diagnostic counter or allocation is added.

Frozen engine SHA256:
`382E455B98BCECC7E4E1B66ABE86A99738F46D46DCD5CFABE99153426777FE54`.
The candidate was built from `acb9411` plus the recorded source patch; the performance
reference is the unchanged accepted engine binary. `source-final.patch` and
`manifest-final.json` under local `artifacts/lean-2026-09-19/` record source and
complete frozen binary identities. `reference/` and `candidate-final/` use the
identical runner and dependencies. No package was repacked or published.

Engineering trials are separate from acceptance. A pending-reload shortcut was
discarded after inconsistent timing; passing pixel values by reference was
discarded after generated-code inspection exposed extra stack traffic. A palette
range-check trial was also removed. The final packed representation removes the
conversion without those spills; vector stores use a shuffle to avoid a chain of
four scalar lane insertions. Generated-code size alone is not a speed measurement.

Short four-sample trials retain every completed sample and any recorded host-load
failure. `direct-trial`, `direct-native-trial` and `packed-trial` encountered active
build/test or sustained package/SMT interference. Other short trials varied enough
to obscure a modest gain even without a telemetry rejection. No favorable subset,
profiled FPS or short-trial average establishes the 5% objective or the 1% ceiling.
The formal comparison used the unchanged homogeneous-retention-v1 protocol in
`retention-final/`: freshly verified CPU 2 / protected sibling 3, Normal priority,
ten-second preflight/cooldowns and host-load policy v2. Lores R1 completed at
372.37 FPS (2.6855 ms/field). During C1, sustained sibling activity exceeded 25%
for 10.159 seconds; the harness stopped and marked the series **INVALID / RERUN**.
There is no completed pair, candidate FPS result or confidence bound. The solitary
reference sample cannot establish any speedup or regression. This task ran no
builds, tests or other probes during the formal series. Earlier evidence and
acceptance decisions are unchanged; no exception for this candidate is implied.

The production Release solution builds with zero warnings/errors. All 557 engine
tests pass both with hardware intrinsics enabled and with
`DOTNET_EnableHWIntrinsic=0`; this includes all 256 odd/even scroll combinations
in both lores and hires. All 93 host tests pass with both native gameplay and
keyboard replays enabled, and all 74 CopperDisk tests pass; none were skipped.
These results establish correctness coverage, not the outstanding performance
bound. Logs are retained under the dated local evidence directory.

Separate correctness-only runs in `final-fingerprints/` completed the full retained
lores, hires and native Lemmings workloads. Every complete fingerprint matches
the accepted trace/Paula engine, with real PCM, zero measured steady-state
allocations and no unsupported-mode reports. Native cycle `2063321634`, CPU
`6AE7090DA8AFB7E3`, hardware `334A819F6FDFB1CA` and output `C65F87325946E5DA`
are unchanged. These uncontrolled run times are excluded from performance claims;
they do not repair the invalid formal series. Its 5% objective / 1% regression
bound remain unverified; the owner's subsequent acceptance is recorded below.

The owner-requested retry in `retention-retry-1/` reverified all frozen source,
binary and six protocol/media input identities before running the same protocol
on CPU 2 / sibling 3. It completed all twelve lores samples and seven hires
samples, each with matching complete fingerprints and zero measured allocations.
During hires R4, sustained sibling activity exceeded 25% for 10.394 seconds.
The harness stopped with **INVALID / RERUN**; native measurements never started,
and there is no complete-series result or acceptance from measurement. Completed samples are not
pooled with the earlier attempt or promoted to an overall performance claim.

A subsequent twelve-second idle-core survey found the selected pair still had
the lowest average background activity (7.56%, versus 8.14% for CPU 6/7), so it
did not identify a quieter alternative. Placement and thresholds were unchanged.
`retry-core-survey.json` and `retry-core-ranking.json` retain this diagnostic;
the short survey cannot establish quiet conditions for a full benchmark run.

### Owner acceptance, 2026-09-19

After reviewing the retry, the owner explicitly accepted this unresolved gate
and authorized committing and pushing the optimization candidate, engine SHA256
`382E455B98BCECC7E4E1B66ABE86A99738F46D46DCD5CFABE99153426777FE54`.
This is a scoped acceptance of that build despite the unavailable performance
bound. Both formal attempts remain **INVALID / RERUN**. Neither a 5% gain nor
compliance with the 1% regression ceiling has been demonstrated. The acceptance
does not waive future changes, certify remaining OCS behavior or authorize package
publication. Source and correctness evidence above remain unchanged.

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

## ERSY absent-source beam synchronization — 2026-09-19

The [beam record](BEAM_SYNC.md) defines the bounded hardware correction and
preserves old/new native fingerprints. Reference is accepted commit `67c13f6`;
previous performance exceptions do not transfer. Candidate engine SHA-256 is
`2E87659F28E3A4BE25DA0D7C348B947A4FF15B08634C91329102E3FE7D95F4DB`;
reference is `382E455B98BCECC7E4E1B66ABE86A99738F46D46DCD5CFABE99153426777FE54`.
Frozen directories are `artifacts/beam-sync-2026-09-19/reference` and `candidate-request-fix`.
Both use the same retained runner
`40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF`, CPU and disk DLLs.
The production runner's new short/empty-PCM checksum guard is deliberately absent
from these frozen measurement bundles.

Production Release build: no warnings/errors. Isolated engine: 573 passed plus
one additional zero-allocation sync-loss test (574 distinct checks);
CopperDisk: 74 passed; host: 94 passed including both supplied-media native
replays, no skips. The unchanged 10,920 + 3,600 Lemmings workload executes without
unsupported features or steady-state allocations, but its fingerprints change
because Kickstart now detects missing external sync. The existing native
performance protocol must reject that change, not silently adopt new expected
values. Diagnostic throughput is not performance acceptance.

The initial `retention-v1` series was deliberately withdrawn after source review
found an early period-modulation DMA request on sync recovery. Its original
`candidate` engine (`D8046708A681F32D52005EA65EF381A1902C51085FFC23751E227B83C225AD6C`)
and raw partial samples remain intact; they do not accept the replacement build.

The unchanged homogeneous-retention-v1 retry is recorded under
`artifacts/beam-sync-2026-09-19/retention-request-fix`: Normal priority, CPU 2 with SMT
sibling 3 protected, Ryzen 5 5600X topology rechecked, host-load policy v2, six
balanced pairs per workload. No concurrent builds or tests. The series stopped
after hires C5 with **INVALID / RERUN**: sustained package interference ≥25% for
10.9098 seconds. The raw label, samples and telemetry remain unchanged. No native
sample was reached in this series; its old expected fingerprint is incompatible
independently of the host-noise failure.

All six lores pairs completed before the later interference episode, with matching
complete-workload fingerprints and zero steady-state allocations. Their descriptive
result is **+1.8214% mean frame time, +2.4675% one-sided 95% upper bound** (the
unchanged paired-log/t-distribution calculation). Arithmetic mean throughput was
386.49 reference versus 379.59 candidate FPS. This exceeds the owner's ≤1% rule;
it does not relabel the full invalid series as PASS. Hires has only five pairs
and the interference episode; no retained bound or acceptance result is claimed.

| Pair | Lores reference FPS | Lores candidate FPS | Hires reference FPS | Hires candidate FPS |
| --- | ---: | ---: | ---: | ---: |
| 1 | 380.53 | 374.85 | 345.95 | 341.81 |
| 2 | 386.86 | 381.87 | 346.43 | 336.31 |
| 3 | 385.69 | 381.81 | 356.09 | 348.97 |
| 4 | 392.70 | 386.02 | 348.77 | 346.18 |
| 5 | 386.15 | 374.10 | 348.98 | 324.31 |
| 6 | 387.02 | 378.86 | unavailable | unavailable |

The owner requested further optimization; this candidate received **no waiver**.
The default 1% gate did not pass. No commit, push, package publication or
carry-over waiver is implied.

### ERSY optimization and comparison v1

The next frozen engine was
`87E9E1463FF999383A8C93B5854CDAA9EEAF9F3F3793172CF52DCA4CD93FB5E2`.
Its unchanged homogeneous-retention-v1 attempt is retained under
`artifacts/beam-opt-2026-09-19/retention-final`. All six lores pairs completed:
**+0.6382% mean frame time, +2.2692% one-sided upper bound**, matching state and
zero allocation. Hires stopped after one pair; native was not reached. The
series was deliberately withdrawn to continue optimization, not classified as
host-noise invalid or accepted. Do not pool its samples with another build.

| Pair | Lores reference FPS | Lores candidate FPS |
| --- | ---: | ---: |
| 1 | 385.95 | 385.44 |
| 2 | 393.08 | 381.47 |
| 3 | 381.29 | 390.59 |
| 4 | 386.81 | 387.78 |
| 5 | 387.91 | 384.24 |
| 6 | 391.45 | 382.18 |

The [ERSY comparison v1](../../scripts/run-lightweight-ersy-comparison-v1.ps1)
explicitly versions the hardware correction's native fingerprint change. It
retains six balanced pairs per workload, 600/7,200 synthetic and 10,920/3,600
native warmup/measurement lengths, ten-second cooldowns, Normal priority,
verified homogeneous CPU/SMT placement, host-load policy v2 and the paired-log
one-sided Student-t bound (df=5, 2.015048). The acceptance limit remains ≤1% for
every workload. This does not replace historical retention protocols.

Synthetic fingerprints must match across builds. Native samples must match their
independently established complete identities in [BEAM_SYNC.md](BEAM_SYNC.md),
including the changed cold-boot cycles and PCM count. No expected identity is
learned from timing samples. The harness pins the accepted `67c13f6` reference
and common runner/CPU/disk assemblies. Its preflight rejects role-swapped states,
altered CPU/hardware/output hashes, output length, frame count, PCM mode and
allocation. Missing telemetry and sustained interference rejection are also
exercised. This is a **changed-state native comparison**, not a claim of
cross-build native state equality.

Validation passed on the Ryzen 5 5600X, CPU 2 with sibling 3 protected, under
`artifacts/beam-opt-2026-09-19/terminal-validation`. Protocol SHA-256:
`9F02EB004B06C5392DD6CF892B805B98115A189D2E34707303964EC711B6078C`.
The original homogeneous-retention-v1 remains
`F6F56FB703DACC45A2D70C8266190322841EF3658AD59D9EA5160471425A6D30`;
the unchanged host-load helper remains
`7E1F03FC09E80199B8A18BB32781C967C5035300E5C79B1227EA0DD5F8F1D8C6`.

The subsequent frozen engine is
`BB3F5233EA3FB132E89C018BB76B1CB4260144EFC61CE5A558D67CF7B3A0D7CA`
(`candidate-terminal`). Production Release has no warnings/errors; engine 577,
disk 74 and host 94 tests pass, including both native host replays with no skips.
The preceding 575-test build passes full Debug; the final 47 focused Copper/beam
Debug cases also pass. The retained changes and discarded experiments are
described in the beam record. Its comparison evidence is
`artifacts/beam-opt-2026-09-19/terminal-comparison-v1`.

That complete series is **valid**, with matching synthetic fingerprints, exact
per-build native identities, zero measured steady-state allocation and valid
placement/host telemetry throughout. No builds, tests or profiles ran concurrently.
The candidate does **not** pass the owner's gate:

| Workload | Reference mean FPS | Candidate mean FPS | Mean frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 380.59 | 381.63 | -0.2817% | +1.1250% | INCONCLUSIVE; above limit |
| Hires | 349.41 | 343.48 | +1.7254% | +2.4179% | ACCEPTANCE_REQUIRED |
| Native Lemmings | 254.19 | 262.11 | -3.0368% | -1.3769% | PASS for defined changed-state comparison |

| Pair | Lores R / C FPS | Hires R / C FPS | Native R / C FPS |
| --- | --- | --- | --- |
| 1 | 380.96 / 382.70 | 344.21 / 339.15 | 255.95 / 260.16 |
| 2 | 381.55 / 379.19 | 350.36 / 344.43 | 256.21 / 262.76 |
| 3 | 369.62 / 382.65 | 351.87 / 343.53 | 255.90 / 261.96 |
| 4 | 384.01 / 384.51 | 349.33 / 348.31 | 243.68 / 261.99 |
| 5 | 381.41 / 380.59 | 351.68 / 342.52 | 257.48 / 263.50 |
| 6 | 385.97 / 380.14 | 348.99 / 342.95 | 255.93 / 262.28 |

No exception was accepted. Further optimization continues from this measured
candidate; a later build cannot inherit its native PASS without remeasurement.

#### Compact bitplane fetch order

Preparing the eight-slot fetch order and terminal modulo offset when effective
BPLCON0 changes removes a per-input mode branch/table load. Input/output phases
and plane ordering are unchanged. A separate scroll-decoding cache was discarded:
its short hires improvement came with slower lores results.

Final engine SHA-256:
`7778A0F26FCC2D2EE1F66C69172F48D7DFB6E61D2CD6623693A47FBDFC7D4C85`.
Frozen directory: `artifacts/beam-opt-2026-09-19/candidate-fetch-order`.
Reference and shared assemblies remain the same as above. The source patch at
freeze is `fetch-source.patch`, SHA-256
`F4AD62063F2311701B1C6CDACFFA2E6CBC0B6EC9559249FE1C20B69D5A060B5D`;
the separate new beam test source is `terminal-BeamSyncTests.cs`, SHA-256
`FB03646311942B868BD3B2C7F59F72A98378215523D616200453BE6FE56324C6`.

Production Release builds with zero warnings/errors. The complete engine suite
passes **577 in Release and 577 in Debug**; host **94** passes with both native
replays enabled and no skips. CopperDisk's unchanged source/dependency retains
its 74 passing tests. Local TRX files are `fetch-final-engine.trx`,
`fetch-final-debug.trx`, `fetch-final-host.trx` and `final-disk.trx` under the
optimization evidence directory. One initial serial zero-allocation assertion
failed with 8,088 bytes; the isolated repeat and final full suites pass. Its
failure log remains unchanged. No acceptance measurement permits allocation.

Two short balanced pairs against the terminal-only build favored the fetch-order
candidate in both synthetic workloads, with exact fingerprints and zero
allocation. They remain diagnostic, not acceptance evidence. The full frozen
six-pair series is recorded separately under `fetch-comparison-v1`.

The complete final series is **valid**: CPU 2 / protected sibling 3, Normal
priority, verified topology/placement, valid host-load policy v2 telemetry, no
concurrent builds/tests/profiles. All synthetic complete-workload fingerprints
match; every native sample matches its independently recorded per-build identity.
Every measured sample has zero steady-state allocation and no unsupported feature.

| Workload | Reference mean FPS | Candidate mean FPS | Mean frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 386.39 | 387.08 | -0.1755% | +0.7894% | PASS |
| Hires | 350.28 | 349.19 | +0.3736% | +3.7272% | INCONCLUSIVE; above limit |
| Native Lemmings | 254.83 | 259.90 | -1.9442% | -0.3364% | PASS for defined changed-state comparison |

| Pair | Lores R / C FPS | Hires R / C FPS | Native R / C FPS |
| --- | --- | --- | --- |
| 1 | 382.89 / 390.78 | 349.27 / 356.98 | 257.05 / 261.37 |
| 2 | 386.17 / 388.72 | 348.67 / 357.21 | 254.54 / 261.96 |
| 3 | 385.81 / 383.68 | 352.46 / 352.65 | 254.00 / 260.62 |
| 4 | 387.79 / 382.54 | 350.63 / 323.71 | 257.22 / 260.46 |
| 5 | 389.89 / 389.11 | 348.83 / 356.66 | 255.51 / 252.31 |
| 6 | 385.82 / 387.65 | 351.84 / 347.90 | 250.68 / 262.68 |

The slow hires C4 sample did not cross the protocol's sustained-interference
threshold. It remains in the calculation; no trimming, substitution or pooling
with another attempt was used. The mean hires change is below 1%, but the owner
requires the **upper bound**, so the complete candidate did not pass the default
gate. Lores/native passes do not waive the hires +3.7272% result.

On 2026-09-19 the owner explicitly accepted this specific hires exception and
authorized commit and push. Acceptance applies to the frozen
`7778A0F26FCC2D2EE1F66C69172F48D7DFB6E61D2CD6623693A47FBDFC7D4C85`
candidate and this complete comparison. The measured hires classification remains
INCONCLUSIVE; no sample, confidence bound or earlier evidence is reclassified.
This is a scoped acceptance, not a higher default budget or a waiver for future
changes. Package publication is not included.

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
