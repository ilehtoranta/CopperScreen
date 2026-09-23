# Copper68k batch-prefetch comparison — 2026-09-23

**The complete performance series passes the default 1% upper-bound gate for
all three workloads. Native Lemmings gains 2.76% paired FPS.** CPU and native-host
checks pass. Initial full candidate engine-suite runs had allocation-only failures;
the subsequent [diagnostic-host correction](ALLOCATION_TESTS_2026-09-23.md) resolves
the cause and records five clean full runs per CPU. Original failed runs remain
failed in the evidence below.
The production package and pin remain unchanged.

## Candidate and reference

The owner's uncommitted CopperMod change is based on
`986ca2b0306c52bd782333847eb0059a2b9f03e8`. It retains the previous no-argument
interrupt-sample guard, adds the equivalent guard/non-inlined helper for the
fixed-batch context, and extracts the general bus-fetch tail of
`ReadFixedBatchPrefetchWord` into `ReadFixedBatchBusPrefetchWord`, also non-inlined.
The admitted-window path remains in the caller; bus operations and their order
remain in the extracted helper. No emulator source was edited for this comparison.

The reference is the shipped `1.4.1-locality.1` CPU, not the unaccepted
[retirement-only candidate](CPU_INTERRUPT_RETIREMENT_2026-09-22.md). This measures
the combined changes against production; it does not isolate the incremental
effect of the latest two edits.

| Identity | SHA256 |
| --- | --- |
| Reference Copper68k.dll | `982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F` |
| Candidate Copper68k.dll | `881982995B5FBA962E53BBDC83DD64E4893C7B5699802E0C3D31DC33C022DE13` |
| Candidate M68kCore.cs | `51F1CCBD1CEB2087656D7921E61B640D5C7C449F5BDCC0A9F74CFD600454BBBA` |
| Frozen engine, both roles | `8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5` |
| Frozen runner, both roles | `40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF` |

The two frozen directories differ only in `Copper68k.dll`. A Release build
reproduced the owner's documented candidate binary exactly. The same candidate
DLL was verified in the CPU tests, engine tests, host tests and retained runner.
The [JSON record](CPU_BATCH_PREFETCH_2026-09-23.json) preserves the exact source
patch, all samples, failure messages, identities and artifact hashes.

## Validation and unresolved allocation probes

| Check | Result |
| --- | --- |
| Production and isolated diagnostic Release builds | Pass; zero warnings/errors |
| CPU Release build | Pass; one existing NU1902 dependency warning |
| CPU tests | 1,499 passed; six optional external-corpus cases unavailable |
| Host tests with supplied ROM/media | 95 passed, none skipped |
| Full engine reference-control suite | 669 passed |
| Full engine candidate suites | 668/1, 666/3, 668/1 passed/failed; all failures are allocation assertions |
| Focused affected allocation cases | Four passed with reference, four with candidate; earlier serial-only check also passed |
| Complete lores, hires and native replay verification | Retained full fingerprints match; zero measured allocations |

The initial engine run reported 5,816 bytes in
`SteadyTransmitAndReceiveAllocateNothing`. The next full candidate run reported
7,928 bytes in that test, 4,264 in the fast no-flux receiver case and 8,016 in
`DensitySeekKeepsElapsedSpindleTimeWithoutAllocations`. The final full run failed
only the no-flux receiver allocation assertion, with 7,864 bytes. All original
logs and TRX files remain intact; no tests or assertions were weakened.

These measurement loops advance serial/disk hardware directly and do not execute
the changed CPU helpers. A similar serial-allocation failure was retained during
the earlier locality work. The affected focused checks pass with both CPUs,
but at the time of this comparison the exact allocation source was unresolved;
**no clean full candidate engine-suite pass was obtained in its original runs**.
The subsequent [investigation](ALLOCATION_TESTS_2026-09-23.md) identifies runtime
allocation-context accounting and records corrected-host full-suite passes.
The formal benchmark separately requires zero
steady-state allocation and passed that requirement on every sample.

CopperDisk source is unchanged and its standalone suite was not rerun for this
CPU-only comparison. Optional external CPU corpora remain unavailable coverage.

## Performance protocol and result

The [versioned harness](../../scripts/run-lightweight-cpu-batch-prefetch-comparison-v1.ps1)
changes only candidate identity, protocol naming and explanatory text from the
preceding comparison. The shipped reference CPU remains unchanged. The protocol
retains six balanced pairs, 600/7,200 synthetic and 10,920/3,600 native
warmup/measurement frames, ten-second cooldowns, Normal priority, verified LP2
with LP3 protected, host-load policy v2 and the one-sided 95% Student-t upper
regression bound (df=5, t=2.015048). The limit remains **at most 1% per workload**.

Preflight passed pinned builds, homogeneous Ryzen 5 5600X topology, live telemetry
and identity/noise rejection probes. Attempt 1 completed all 36 samples with
matching complete workload fingerprints and zero measured allocations. All 1,433
telemetry intervals are valid. Neither selected nor sibling CPU crossed the
interference threshold; the longest continuous package exceedance was 1.167
seconds, below the unchanged ten-second rejection limit. No builds, tests or
profiles ran concurrently. No samples were omitted, substituted or pooled with
older measurements. Statistics and telemetry were recalculated from raw files.

| Workload | Reference mean FPS | Candidate mean FPS | Paired FPS gain | Paired frame-time change | One-sided 95% upper regression bound | Gate |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Lores | 389.83 | 394.73 | +1.2616% | -1.2458% | +0.0898% | PASS |
| Hires | 354.33 | 356.04 | +0.4873% | -0.4849% | +0.6199% | PASS |
| Native Lemmings | 330.64 | 339.77 | +2.7634% | -2.6891% | -1.7755% | PASS |

FPS columns are arithmetic means. Paired gains/bounds use log frame-time ratios;
positive frame-time changes mean slower execution. Every native pair favors the
candidate. Lores and hires keep the CPU stopped in steady-state measurement and
remain retention checks, not attribution of active CPU-dispatch gains. These
results concern this frozen engine/runner and host, not every game or desktop FPS.
The CPU-only benchmark's larger cached-loop gains are not application FPS claims.

No performance exception is required. The separate engine allocation-test caveat
was subsequently resolved by the diagnostic-host correction linked above;
no publication, production adoption, commit or push is included.
Earlier unaccepted and invalid records retain their original classifications.

Evidence is under ignored `artifacts/cpu-batch-prefetch-2026-09-23/`, with frozen
`reference` and `candidate` directories and the complete series in `attempt-1`.
To reproduce, use a fresh evidence directory:

```powershell
./scripts/run-lightweight-cpu-batch-prefetch-comparison-v1.ps1 `
  -LogicalProcessor 2 `
  -ReferenceDirectory artifacts/cpu-batch-prefetch-2026-09-23/reference `
  -CandidateDirectory artifacts/cpu-batch-prefetch-2026-09-23/candidate `
  -RomPath C:/Data/ROM/Kickstart_13.rom `
  -AdfPath 'C:/Data/TestImages/Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip' `
  -InputScriptPath CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json `
  -EvidenceDirectory artifacts/cpu-batch-prefetch-2026-09-23/attempt-2
```
