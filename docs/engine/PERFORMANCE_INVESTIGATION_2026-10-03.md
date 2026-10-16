# Baseline investigation — 2026-10-03

The native Lemmings mismatch in the [current baseline](PERFORMANCE_BASELINE_2026-10-03.md)
comes from an obsolete input/test contract. No new engine defect is established
by that failure. The current build preserves the independently validated native
identity and gameplay images with the later workload. The synthetic FPS decline
is investigated separately below.

## Native workload diagnosis and repair

Commit `ca8c8b70497db20017604424c5af017651244201` corrected the 68000 bus-transfer
spacing on September 25. Its [hardware and regression evidence](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md)
and [performance preflight](LOTUS_III_PERFORMANCE_2026-09-25.md) already document
that `lemmings-native-level1.json` exchanges disks during the introduction with
the corrected engine and later captures a black loader screen. Its apparent FPS
is not the retained gameplay workload.

That correction introduced [lemmings-cpu-bus-timing-v1.json](../../CopperScreen.Lightweight.Tests/Workloads/lemmings-cpu-bus-timing-v1.json),
delaying every event from the original field 3,600 onward by 3,600 fields. Warmup
is 14,520, measurement is 3,600 and completion is 18,120 fields. Both builds were
validated before the September timing comparison. The October baseline and
optional host tests had continued using the earlier script and expectations.

The current production build at `d9beae8b88be24032221e3482942a249c03c27d3`, with
Copper68k 1.5.1 and CopperDisk 3.0.0, repeats the existing corrected identity:

```text
warmup=14520 frames=3600 completed=18120 cycle=2574756850
cpu=0x505451D28DF1C053 hardware=0x20084440BFE4BC5A output=0x9DE548805866DC63
pixels=284204 audioSamples=1922 pcm=real allocated=0 adf=True unsupported=none
```

Normal and scalar complete-workload results match, excluding only their CPU-mode
and FPS labels. All nine captured BMP/JSON/Chip RAM files at fields 1, 13,920 and
18,120 match between those executions. Both current gameplay BMPs also match the
September 25 validation captures byte for byte:

| Field | Gameplay | BMP SHA-256 |
| --- | --- | --- |
| 13,920 | Two lemmings out; time 4:55 | `7F6D2FD0CFEAAF788A4C7005EBD828CF719F086397198038CE24B9BD5C18669A` |
| 18,120 | Ten lemmings out; time 3:31 | `0919509F56690100BF0E74604EE95E55C3504DCF9007BD98F8E8565A9A146D58` |

The active host test now uses the corrected 18,120-field contract and its
pre-existing September expectations. Its separate Return press/release moves to
fields 11,500/11,506, before menu selection, and guest acknowledgement is checked
at 11,515. The old script is rejected before its replay. The
[native setup instructions](../NATIVE_VALIDATION.md) select the corrected script.
Historical scripts and their original identities are preserved. No expected
identity was learned from a timing sample, and no engine or package code changed.

Production Release builds with zero warnings/errors. The host suite passes 152
tests, including ordinary and keyboard Lemmings replays; three separate supplied
Kickstart 3.x cases are unavailable coverage. These functional checks do not
constitute a throughput gate. The original invalid native baseline sample stays
excluded. Native diagnostic FPS is also excluded from acceptance.

## Synthetic source isolation

Source-stage measurements are diagnostic only and do not evaluate the default
one-sided 95% upper regression bound of 1%. Their evidence is local under
`artifacts/baseline-investigation-2026-10-03/`, together with the native logs,
captures, build log and host TRX. Source snapshots use this repository only;
no sibling source reference, runtime dependency or package publication is added.

Five source stages were prepared: pre-ECS `68c2bac`, ECS `525f5d3`, initial AGA
`7187e55`, AGA sprites `df5e653`, and current `d9beae8`. Older engines were rebuilt
from archived repository source with Copper68k 1.5.1. All capsules use the same
frozen September 25 runner, current Copper68k DLL and CopperDisk 3.0.0 DLL;
compatibility smokes pass. These capsules isolate source changes, not historical
package/version combinations.

The intended `source-stage-diagnostic-v1` comparison uses two forward/reverse
rounds per stage/workload, the full 600/7,200 synthetic intervals, existing
complete fingerprints, Normal priority, rechecked Ryzen CPU 2 / sibling 3,
one-second telemetry, ten-second cooldowns and unchanged host-load policy v2.
All three attempts stop **INVALID / RERUN**:

| Attempt | Reason |
| --- | --- |
| Initial | Competing `dotnet`/`testhost` job during ECS lores |
| `stage-retry-1` | Sibling activity at least 25% for 10.15634 seconds |
| `stage-retry-2` | Sibling activity at least 25% for 10.43748 seconds; package load peaks at 94.29% |

No samples are pooled, trimmed or promoted to a stage result. The completed
October synthetic baseline remains valid for its own recorded interval. At that
point, the decline relative to earlier dates could not be attributed
quantitatively to a particular commit, CPU package or host change. The later
[idle comparison](#idle-six-round-comparison) measures the source-stage cost
with common dependencies. The default 1% candidate gate remains unchanged;
no fresh acceptance exception is inferred.

## Concrete optimization candidate

Current optimized JIT assembly confirms that `LightweightBitplanes.CompleteOutput`
does AGA work on the OCS path: it decodes `_pendingMode` into a 16/32/64-bit width,
then performs a variable shift of the word before storing its data latch. OCS
always transfers 16 bits here. The shared body also checks `_aga` for reading,
output routing and pointer increment. These operations execute at the accepted
DMA output phase and can be isolated without changing that phase, the sampled
RAM word, pointer update, terminal modulo or Denise input deadline.

Tier-1 disassembly from bounded 600-warmup/600-field diagnostic runs shows:

| Method | Pre-ECS code bytes | ECS code bytes | Current code bytes |
| --- | ---: | ---: | ---: |
| `TickDevices` | 3,580 | 3,676 | 3,756 |
| Bitplanes `TryAcceptInput` | 231 | 228 | 356 |
| Bitplanes `CompleteOutput` | 534 | 534 | 787 |
| `OnBitplaneDataOutput` | 115 | 115 | 157 |
| Sprite DMA `CompleteOutput` | 705 | 705 | 909 |

These are generated code sizes, including cold branches, not execution times.
JIT inlining and synthesized PGO limit comparisons of method boundaries. The
disassembly nevertheless demonstrates the redundant width decode and variable
shift in the current OCS execution path. The first candidate is to move AGA-wide
read/width/pointer handling into its own completion path while retaining compact
OCS word handling. Sprite completion and control decoding are secondary targets.
No engine optimization has been applied by this investigation.

Separate full-workload `dotnet-sampled-thread-time` captures also preserve the
synthetic lores identity with zero measured allocations. A seven-second interior
tail of the current main engine thread has 439 interval slices: device dispatch
and inlined video account for 56.17%, out-of-line bitplane work for 13.16%, Copper
8.13%, clock/waits 6.75%, audio 5.10%, sprites 4.74% and blitter 4.38%. These
sampled managed thread-time shares help prioritize work; they are neither PMU
cycle counts nor proof of a speedup or the cause of the FPS decline. Background
activity, profiling and inlining prevent using these diagnostic run times for
the retention gate.

The local evidence retains source archives, frozen capsules, JIT text and parsed
summaries, both sampled traces, native capture parity, Release/host logs and the
three invalid source-comparison attempts. ROMs, media and artifacts stay ignored.

## Background exploratory follow-up

The owner subsequently requested benchmarking despite background activity. The
separate local `source-stage-background-exploratory-v1` protocol continues through
measured interference and competing jobs while logging the unchanged policy v2
verdicts. This is an exploratory run; it does not change the formal protocol,
the 1% limit or the three INVALID / RERUN attempts above.

It uses the same five frozen capsules and complete synthetic workload identities,
two forward/reverse rounds for lores and hires, plus two current-engine native
Lemmings runs with the validated 14,520/3,600-field workload. CPU 2, sibling 3,
Normal priority, full rendering, real PCM, fingerprints and allocation checks
are retained. Evidence is under the separate local
`artifacts/baseline-investigation-2026-10-03/background-exploratory-v1/` directory.

The complete sequence has **22 of 22 matching fingerprints**, zero measured
steady-state allocations, real PCM and no unsupported feature. Native Lemmings
preserves the September 25 gameplay identity in both samples. Its run times are
for actual level-one gameplay, unlike the excluded obsolete-script baseline.

| Frozen source stage | Lores FPS, rounds 1 / 2 | Mean lores FPS | Hires FPS, rounds 1 / 2 | Mean hires FPS |
| --- | ---: | ---: | ---: | ---: |
| Pre-ECS `68c2bac` | 313.90 / 327.38 | 320.64 | 334.99 / 336.24 | 335.62 |
| ECS `525f5d3` | 300.24 / 323.17 | 311.71 | 305.08 / 297.79 | 301.44 |
| Initial AGA `7187e55` | 298.85 / 328.51 | 313.68 | 323.08 / 323.48 | 323.28 |
| AGA sprites `df5e653` | 292.01 / 283.00 | 287.51 | 319.82 / 321.33 | 320.58 |
| Current `d9beae8` | 344.81 / 292.05 | **318.43** | 321.79 / 277.00 | **299.40** |

Current native Lemmings measures **290.70 / 322.86 FPS**, arithmetic mean
**306.78 FPS**. Displayed FPS means use the retained two-decimal samples and
decimal rounding. No samples are dropped or combined with earlier attempts.

The 781 valid telemetry intervals include 409 with competing build/test jobs.
Maximum selected-CPU other activity is 32.57%, sibling activity 81.61% and
package other activity 76.22%. The longest continuous excursions at or above
25% last 51.27 seconds on the sibling and 39.34 seconds on the package.
Consequently the formal policy rejects these host conditions, as expected; the
owner requested completing the exploratory sequence through them. The protocol
hash is `37095B00115839D7B5F1B32CD7B97841D1A4C4683BE731AB144CD340AA3B7A26`.
The policy helper remains unchanged at
`7E1F03FC09E80199B8A18BB32781C967C5035300E5C79B1227EA0DD5F8F1D8C6`.

Within the same current binary, lores and hires change by roughly 14–15% between
rounds. This variation and the evolving load prevent attributing the differences
between stage averages to engine changes. The table supplies rough rates under
the observed background work; it establishes neither a causal regression nor
compliance with the 1% confidence-bound limit. It does not replace the earlier
valid idle synthetic baseline. The redundant OCS width/shift work remains a
concrete code simplification candidate, with its speedup still unmeasured.

Raw logs, sample order, per-sample load summaries, all interference verdicts,
runtime/topology/build/input hashes and independent result recomputation are
retained locally in `exploratory-results.json` and its linked evidence files.
No builds or tests were launched by this task during the timing sequence; the
detected jobs belonged to concurrent work. Engine and package code are unchanged.

## Idle six-round comparison

After the owner reported the machine idle, a fresh
`source-stage-idle-diagnostic-v1` series completed **VALID** under unchanged
host-load policy v2. It measures six runs of each synthetic workload across all
five frozen source stages, plus six current-engine Lemmings runs: **66 runs**.
Rounds 1/3/5 use forward stage order and rounds 2/4/6 reverse it. Each round runs
lores, then hires, then current Lemmings. Every sample and cooldown is retained;
none is dropped or pooled with the background series or earlier attempts.

The five capsules are identical to those described above. All share Copper68k
1.5.1, CopperDisk 3.0.0 and the frozen September 25 runner. The CPU package,
media and .NET 10.0.12 runtime are fixed while engine source varies. The host is
Ryzen 5 5600X, Windows 11 Home 10.0.26200, Balanced power plan, CPU 2 with SMT
sibling 3 protected and Normal priority. Topology and processor telemetry are
rechecked before timing. Synthetic warmup/measurement remains 600/7,200 fields;
native uses the independently validated 14,520/3,600-field script. Ten-second
preflight/cooldowns and one-second observations remain in effect.

All **66 complete fingerprints match** their pre-existing expected identities,
with **zero measured steady-state allocations**, real PCM and no unsupported
feature. All 44 frozen binary/runtime/input file hashes match again after the
sequence. Engine and package code are unchanged.

| Frozen source stage | Mean lores FPS | Mean hires FPS | Paired lores frame-time change vs pre-ECS | Paired hires frame-time change vs pre-ECS |
| --- | ---: | ---: | ---: | ---: |
| Pre-ECS `68c2bac` | 371.15 | 335.64 | Reference | Reference |
| ECS `525f5d3` | 356.52 | 325.58 | +4.1031% | +3.0817% |
| Initial AGA `7187e55` | 347.79 | 321.84 | +6.7160% | +4.2775% |
| AGA sprites `df5e653` | 346.64 | 321.50 | +7.0682% | +4.3862% |
| Current `d9beae8` | **338.45** | **318.22** | **+9.6786%** | **+5.4656%** |

| Current workload | Mean FPS | Observed FPS range | Mean frame time |
| --- | ---: | ---: | ---: |
| Lores | 338.45 | 326.22–345.70 | 2.955811 ms |
| Hires | 318.22 | 312.82–322.67 | 3.142826 ms |
| Native Lemmings gameplay | **324.31** | 312.96–330.24 | 3.084413 ms |

FPS means use all six retained two-decimal results and decimal rounding.
Frame-time comparisons pair equal round numbers and use the geometric mean of
the six candidate/reference ratios. Individual one-sided 95% upper bounds use
Student-t with five degrees of freedom (`t = 2.015048373333`) on the paired log
ratios. They are diagnostic source comparisons; native has only a current
baseline. The complete three-workload reference/candidate retention protocol is
still required for a future optimization's 1% acceptance decision.

| Source interval | Workload | Paired frame-time change | One-sided 95% upper bound |
| --- | --- | ---: | ---: |
| Pre-ECS → ECS | Lores | +4.1031% | +4.8352% |
| Pre-ECS → ECS | Hires | +3.0817% | +4.3109% |
| ECS → initial AGA | Lores | +2.5100% | +3.5327% |
| ECS → initial AGA | Hires | +1.1600% | +2.6577% |
| Initial AGA → AGA sprites | Lores | +0.3300% | +1.3966% |
| Initial AGA → AGA sprites | Hires | +0.1042% | +1.4333% |
| AGA sprites → current | Lores | +2.4380% | +4.1787% |
| AGA sprites → current | Hires | +1.0340% | +2.3678% |
| Pre-ECS → current | Lores | **+9.6786%** | **+11.3194%** |
| Pre-ECS → current | Hires | **+5.4656%** | **+6.6803%** |

The source cost persists under valid host conditions. The largest adjacent
increase is the ECS interval; initial AGA adds another measurable lores cost.
The sprite interval has a small mean with wider bounds, so these results do not
establish that it is cost-free. The later current interval adds cost but contains
several source changes and does not identify one responsible method. These
measurements isolate the source intervals using common dependencies; they do not
reconstruct historical package combinations or attribute all differences from
older published FPS to one feature.

The previously demonstrated redundant AGA width/shift operations on OCS bitplane
completion remain a concrete first simplification candidate. This comparison
supports investigating OCS execution costs introduced by wider chipset support;
the gain from that individual simplification remains unmeasured.

The independent check verifies all **2,220 valid telemetry intervals**, with
**zero competing test/build intervals** and no other runner IDs. Peak selected
CPU other activity is 21.69%, sibling 36.90% and package other activity 34.70%.
The longest continuous excursions at or above 25% are 0 seconds selected,
2.543353 seconds sibling and 1.302069 seconds package, below the 10-second
threshold. Telemetry includes warmup, measured execution and cooldowns. Empty
runner-ID lookups serialize as `[null]`; the independent check requires every
entry in those arrays to be null. The actual competing-work arrays are empty.

The unchanged policy helper hash is
`7E1F03FC09E80199B8A18BB32781C967C5035300E5C79B1227EA0DD5F8F1D8C6`.
The frozen local protocol hash is
`B624167922FC37ACA61EBD4B016AE0A1C1A41CFC06AB0EA2CE7881B0E08BA6A3`.
Raw sample logs/order, telemetry, expected fingerprints, full source commits,
binary/runtime/media/script hashes and independent calculations are retained in
`artifacts/baseline-investigation-2026-10-03/idle-source-comparison-v1/`, including
`environment.json`, `stage-samples.json`, `telemetry.jsonl`, `analyze-idle.py` and
`idle-results.json`. No builds, tests or profiling ran during timing. Earlier
valid, invalid and background records retain their original dispositions.
