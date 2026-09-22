# Hardware-counter investigation — 2026-09-22

**Nine complete diagnostic captures; no engine change or demonstrated speedup.**
The accepted engine is not proven maximally efficient. Counters now provide
stronger evidence than sampled stacks or operation counts, but do not establish
an individual safe optimization or a 5% saving.

All nine complete workload fingerprints match, with zero measured allocations.
Every trace reports zero lost events. Every analyzed eight-second window has
nonzero counters, verified ordering and uninterrupted thread/counter attribution.
These are diagnostic observations, not FPS acceptance measurements.

## Observations

IPC ranges span three separate counter-group captures, not confidence intervals.
Cache columns retain Windows event names without claiming a verified cache level.

| Workload | Instructions / cycle, observed range | Branch mispredictions / branches | DcacheMisses / 1,000 instructions | IcacheMisses / 1,000 instructions |
| --- | ---: | ---: | ---: | ---: |
| Lores | 3.51–3.81 | 0.575% | 1.171 | 0.01085 |
| Hires | 3.95–4.05 | 0.405% | 0.632 | 0.00414 |
| Native Lemmings | 2.52–3.03 | 0.539% | 2.688 | 0.21605 |

**Host qualification:** lores instruction counters had 16.3% mean / 45% maximum
sampled sibling-CPU load; native branch counters had 39.6% / 56%. Their low IPC
cannot be attributed solely to the engine. Other windows had 0.7–9.2% mean sibling
load. All observations and telemetry remain in the JSON; nothing is silently
removed or pooled. These snapshots are not policy-v2 acceptance telemetry.
A bounded repeat of the two affected counter groups was prepared, but Windows
reported cancellation of its administrator prompt. No repeat traces were taken;
the original observations retain their host-load qualification.

Most measured branches predict successfully. The assumption that device execution
is dominated by branch misprediction is unsupported. Misses are not free: their
recovery-cycle cost was not measured.

Native execution has higher cache-event rates and lower IPC. Combined with the
[sampled native CPU profile](PROFILE_2026-09-22.md), this supports investigating
Copper68k dispatch's instruction footprint and access patterns, particularly
`ExecuteSingleInstruction`, `ExecuteInstructionBody` and `TryExecutePlannedKind`.
**This is a lead, not attribution of cache misses to those methods.** The next
measurement should locate misses/stalls in generated code before changing
classification, dispatch or layout. Higher IPC alone does not establish that the
engine executes the minimum necessary instructions.

This pass does not measure memory-stall time, execution-port saturation,
branch-specific penalties or potential speedup. Cache misses are not equivalent
to DRAM accesses. The generic cache events' CPU-specific cache level/speculation
semantics remain unverified. There is no basis to repeat the discarded shifter
change or promise a 5% gain.

## Collection and analysis

Host: Ryzen 5 5600X, 6 cores / 12 logical processors, Windows 10.0.26200.
WPR 10.0.26100 recorded strict groups of four counters on context switches.
[collect-lightweight-pmu.ps1](../../scripts/collect-lightweight-pmu.ps1) pins all
five assembly hashes, applicable media and full workload identities. Engine SHA256:
`8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.
The [JSON evidence](MICROARCHITECTURE_2026-09-22.json) includes every identity,
raw counter total, interval, trace hash and host-load summary.

Current topology confirms LP2 / sibling LP3. Normal priority and LP2 placement
are inherited and checked. No builds, tests or offline analysis ran concurrently
with the nine captures. Workloads retain 600 warmup / 7,200 measured frames for
lores and hires, and 10,920 / 3,600 for native Lemmings with its disk swap and input
script. All hardware behavior and runner code are unchanged.

[lightweight-pmu.wprp](../../scripts/lightweight-pmu.wprp) records TotalCycles and
InstructionRetired in every group, plus BranchInstructions/BranchMispredictions,
DcacheAccesses/DcacheMisses, or IcacheIssues/IcacheMisses. The trace's PmcCtrConfig
supplies actual ordering: WPR sorted the names, so declaration order is unsafe.
`tracerpt` exposes PMC extended data in XML. Newer thread-event schema warnings
are handled using retained raw event payloads for IDs. CLR GC fields are read
from UserData; the parser was corrected to support this XML form.

Thread creation identifies the runner's original execution thread. Counter deltas
between consecutive LP2 context switches belong to the outgoing thread. Only
complete main-thread slices inside the window are accumulated. Missing counters,
resets, thread discontinuities or lost events reject analysis. Kernel/interrupt
execution in those slices can contribute; this is not pure managed-code attribution.

The frozen runner forces two generation-2 collections immediately before starting
its stopwatch. External CLR GC events identify this boundary without instrumentation.
Let g be the final forced GC end, e process exit and d the shortest measured duration
consistent with rounded FPS. The continuous measured loop is inside [g,e], so every
possible placement contains [e-d,g+d]. After trimming 100 ms from each end, analysis
takes the middle eight seconds of that common interval. This excludes boot, warmup
and teardown without guessing their duration. Both forced collections and the
interval are verified in every trace. Windows contain 65–951 complete main-thread
slices; exact coverage is in JSON.

Counter groups ran separately and need not cover identical emulated frames.
Per-instruction normalization is used; counts are not pooled across runs.
One observation per group supplies no confidence bound.

## Attempts and reproduction

Ordinary recording initially failed with 0x80070005. After owner approval,
collection used the normal administrator prompt. No policy, driver or trust setting
changed. Each capture uses a unique WPR instance and stops only its own successfully
started recording; replay timeout is 300 seconds. The final series recording stopped.

The first elevated replay saved a trace and matched its fingerprint, but its wrapper
stalled serializing PowerShell file-provider metadata. It was stopped after recording
ended. The collector now reads plain CLR strings. A separate successful lores branch
pilot measured 3.775 IPC / 0.580% branch misses; it is not pooled with the series.
The first series launch was cancelled at the Windows prompt; the owner requested
reopening it, and the subsequent series completed. All attempts are preserved under
ignored local artifacts/pmu-2026-09-22/.

From an administrator PowerShell at the repository root:

```powershell
./scripts/collect-lightweight-pmu.ps1 -Workload lores -CounterGroup Branches
```

Other workloads: hires, lemmings. Other groups: Data, Instructions. ValidateOnly
checks inputs without recording. Offline conversion uses tracerpt with XML output
and a summary, then local extract.py and analyze.py; final script hashes are in JSON.
The temporary GC-metadata repair decodes existing XML and changes no captured event.

Engine and test sources remain unchanged. Any future implementation still requires
correctness checks and the unchanged one-sided 95% upper frame-time regression bound
of 1% per retained workload. No historical exception transfers.

## Primary references

- Microsoft [PMU recording](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/recording-pmu-events) and [WPR examples](https://devblogs.microsoft.com/performance-diagnostics/recording-hardware-performance-pmu-events-with-complete-examples/).
- Microsoft [PMC payload](https://learn.microsoft.com/en-us/windows/win32/api/evntcons/ns-evntcons-event_extended_item_pmc_counters) and [CSwitch fields](https://learn.microsoft.com/en-us/windows/win32/etw/cswitch).
- Microsoft [.NET GC events](https://learn.microsoft.com/en-us/dotnet/fundamentals/diagnostics/runtime-garbage-collection-events).
