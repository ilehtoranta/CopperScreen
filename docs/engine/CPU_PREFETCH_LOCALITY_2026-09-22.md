# Copper68k prefetch locality candidate — 2026-09-22

**Correctness validated; all three workloads pass the default 1% performance gate.**
Native Lemmings gains 26.47% paired FPS in the frozen benchmark candidate.
The tested source change is committed in CopperMod. Subsequent owner-authorized
[package activation](CPU_PREFETCH_ACTIVATION_2026-09-22.md) now pins the published
development package `1.4.1-locality.1`; the original benchmark identities and
results below remain unchanged.

## Change and rationale

The [source patch](patches/copper68k-prefetch-locality.patch) applies to Copper68k
commit `ada86020ad7a9b298cd7f689c3731d8d779684d1` in CopperMod. It changes exactly
three `MethodImpl` attributes from `AggressiveInlining` to `NoInlining`:

- `FullPrefetch(uint address)`.
- `TopUpPrefetchOne(out long requestedCycle)` — only this overload.
- `TopUpPrefetchAtRetirement()`.

No executable statement, CPU behavior, public API, bus operation or cycle ordering
changes. There is no new allocation, capability switch, title-specific path or
sibling project reference. The existing plain-bus metadata guard remains intact.

The [native dispatch investigation](CPU_DISPATCH_LOCALITY_2026-09-22.md) found large
unconditional stack-initialization loops in common instruction dispatch. Inlined
prefetch helpers spread their large publication-context temporaries into those
frames, including paths that do not need a refill. Keeping these three boundaries
separate confines that work to the helpers that need it. This is a targeted code
generation change, not a replacement prefetch implementation.

The previous [retirement-only candidate](CPU_PREFETCH_BOUNDARY_2026-09-22.md) failed
the default performance gate and was not adopted. Its results remain unchanged;
they neither validate nor waive the combined candidate's gate. Two intermediate
experiments isolated full refill and then extension refill before combining them
with retirement. Both completed the native fingerprint check.

## Captured code generation

These are actual Tier1 native code shapes from the accepted CPU capture and the
combined candidate's native replay on .NET 10 / Ryzen 5 5600X.

| Common dispatch method | Accepted code bytes | Candidate code bytes | Accepted stack reservation | Candidate stack reservation | Accepted unconditional zero stores | Candidate unconditional zero stores |
|---|---:|---:|---:|---:|---:|---:|
| `ExecuteSingleInstruction` | 3,260 | 2,170 | 1,360 | 224 | 1,000 | 168 |
| `ExecuteInstructionBody` | 7,616 | 4,620 | 3,160 | 392 | 2,864 | 344 |
| `TryExecutePlannedKind` | 10,193 | 9,774 | 1,304 | 936 | 880 | 504 |

Unconditional initialization across a path entering all three dispatch methods
falls from **4,744 to 1,016 bytes (78.6%)**. This is not a 78.6% reduction in total
CPU work or a measured FPS improvement. Extracted helpers still run, initialize
their own temporary state and add call overhead. In this capture, full refill
initializes 272 bytes on entry, retirement 104 bytes, and the out-parameter
extension wrapper has no unconditional entry-zero stores. Its conditional work
still exists. Additional overloads are recorded in the JSON.

No new hardware-counter cost attribution was collected. Runtime tiering and
profile-guided optimization can change code shape across processes and hosts.

## Correctness and build checks

| Check | Result |
|---|---|
| CPU Release build | Pass; two NU1902 warning occurrences for existing `Microsoft.Build.Tasks.Git` 10.0.300 dependency; zero errors |
| Production Release solution, isolated outputs | Pass, zero warnings/errors |
| Engine diagnostic Release build, separate outputs | Pass, zero warnings/errors |
| Copper68k tests using frozen candidate DLL | 1,499 passed; six optional external-corpus cases unavailable |
| Engine diagnostics using frozen candidate DLL | 669 passed on full rerun; initial allocation failure recorded below |
| CopperDisk tests | 74 passed |
| Host tests with supplied native ROM/media/scripts | 95 passed, none skipped |
| Complete lores, hires and native frozen-runner replays | Accepted CPU/hardware/output identities match; zero measured allocations |

The first engine run passed 668 tests and failed
`LightweightPaulaSerialTests.SteadyTransmitAndReceiveAllocateNothing`, reporting
7,928 allocated bytes instead of zero. This test advances hardware directly and
does not execute the modified CPU helpers. A focused candidate rerun passed,
followed by complete accepted-CPU control and candidate runs passing all 669 tests.
The precise transient allocation origin is unresolved. The initial failure is
retained, with no assertion weakened and no engine/test code changed.

CPU tests were exported from the pinned source into the ignored experiment tree.
Production and diagnostic builds used separate output trees and the accepted
package pin. Only the CPU DLL in the isolated host/engine test outputs was then
replaced; those tests ran without rebuilding/restoring. The CPU test, host test,
engine test and frozen runner DLL hashes all match:

`658025CFD4ADC26C275559B6CC171220110820B412BFEE8D1B4013095F781AD4`

All other DLLs in the frozen runner match the accepted reference. No cached
published package was overwritten. The independently checked
[JSON evidence record](CPU_PREFETCH_LOCALITY_2026-09-22.json) contains source/patch
hashes, generated-code prologues, test results and complete replay identities.

## Performance result and reproduction

Attempt 1 is **VALID and complete**, with six balanced pairs per workload:

| Workload | Reference mean FPS | Candidate mean FPS | Paired FPS gain | Paired frame-time change | One-sided 95% upper regression bound | Gate |
|---|---:|---:|---:|---:|---:|---|
| Lores | 378.10 | 380.69 | +0.6905% | -0.6857% | +0.9525% | PASS |
| Hires | 341.45 | 349.72 | +2.4300% | -2.3723% | -0.2033% | PASS |
| Native Lemmings | 262.76 | 332.31 | +26.4696% | -20.9296% | -20.2527% | PASS |

Positive frame-time changes mean slower execution. FPS columns are arithmetic
means of six runs; paired gains and bounds use the six log frame-time ratios.
The requested 5% improvement is exceeded on retained native Lemmings, not on
every workload. No claim is made about unmeasured games or hosts.

All 36 complete workload identities match their independently retained expected
values, and all measured allocation counts are zero. The 1,503 telemetry intervals
are valid, with no concurrent builds, tests or profiling. Longest continuous
threshold exceedances were 1.129 seconds on the selected CPU, 1.150 seconds on its
sibling and 3.429 seconds across the package, each below the unchanged ten-second
limit. Short spikes remain included; no samples were dropped or pooled.
Statistics, exact assembly hashes, workload identities and telemetry were checked
independently against the raw records. No performance exception is needed.

A short screen of the intermediate full-refill-only candidate completed four
lores and four hires runs. Hires varied sharply; an overlapping five-second host
observation found Blender consuming 9.48 CPU-seconds. The screen was stopped,
and its completed samples and cancelled native run remain under
`artifacts/cpu-refill-boundary-2026-09-22`. This screen had no continuous policy-v2
telemetry and is not acceptance evidence. Its timings are not used to rank or
accept candidates. The combined candidate's disassembly and correctness runs
likewise supply no performance conclusion.

The owner initially requested continued development with the final benchmark
pending, then reported Blender paused and explicitly requested the benchmark.
The [comparison harness](../../scripts/run-lightweight-cpu-locality-comparison-v1.ps1)
changes only candidate identity and protocol naming from the preceding comparison.
Workloads, six balanced pairs, LP2/LP3 protection, Normal priority, cooldowns,
host-load policy v2, full fingerprints and statistical calculation are unchanged.
Live preflight passed topology, telemetry and rejection probes before the complete
series. Lores and hires keep the CPU stopped during steady-state measurement;
their results remain retention checks, not attribution of active CPU dispatch cost.

The completed series is under `artifacts/cpu-prefetch-locality-2026-09-22/attempt-1`.
To reproduce from the repository root, use a fresh evidence directory:

```powershell
./scripts/run-lightweight-cpu-locality-comparison-v1.ps1 `
  -LogicalProcessor 2 `
  -ReferenceDirectory artifacts/wordsync-investigation/bench-candidate `
  -CandidateDirectory artifacts/cpu-prefetch-locality-2026-09-22/runtime `
  -RomPath C:/Data/ROM/Kickstart_13.rom `
  -AdfPath 'C:/Data/TestImages/Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip' `
  -InputScriptPath CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json `
  -EvidenceDirectory artifacts/cpu-prefetch-locality-2026-09-22/attempt-2
```

Require the one-sided 95% upper frame-time regression bound to be at most 1%
for **each** retained workload. No earlier exception transfers. This candidate
meets that requirement. The source-commit stage is recorded below; subsequent
publication and pin activation have a [separate record](CPU_PREFETCH_ACTIVATION_2026-09-22.md).

## Source integration and commit

The owner approved committing and pushing after the completed gate. The exact
three-attribute source change is committed in CopperMod as
[`b59985f`](https://github.com/ilehtoranta/CopperMod/commit/b59985f682e342b9b830771c09f617bb547e4116)
on `codex/cpu-prefetch-locality`, with source version `1.4.1-locality.1` explicitly
marked unpublished. A normalized text comparison confirms `M68kCore.cs` matches
the benchmark candidate exactly. The package version/release notes and README
were updated separately from the executable source change.

The integrated source passed all 1,499 available CPU tests; six optional external
tests remain unavailable. Release packing succeeded, and the package's CPU DLL
matches the DLL used by that CPU test run. The existing NU1902 dependency warning
remains. Local integration artifacts are under
`artifacts/cpu-prefetch-locality-2026-09-22/integration` and are not committed.

The locally rebuilt development DLL is
`982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F`;
the local package SHA256 is
`4005DB85A1E7F288376AC65678BD28B29B30EFF811C25EC31EB6BA4533DFA8CE`.
These differ from the isolated benchmark assembly. The throughput record above
continues to identify the original frozen `658025...` DLL; it has not been
relabelled as a benchmark of this newly packaged binary. At this source-commit
stage, no package had been published or copied over an existing released version,
and CopperScreen remained on `1.4.1-trace.1`. The later
[activation record](CPU_PREFETCH_ACTIVATION_2026-09-22.md) documents publication,
the new production pin, executable comparison and integration checks.
