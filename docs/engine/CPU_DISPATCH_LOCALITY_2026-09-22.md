# Native CPU dispatch and code locality — 2026-09-22

The accepted Copper68k binary has a concrete optimization target: unconditional
stack initialization in its scalar instruction-entry and dispatch methods.
In the native Lemmings capture, **339 of 2,194 cycle samples (15.45%)** fall in
three stack-zeroing loops. The three methods write **4,744 bytes of zeros** on
a path entering all three, before their instruction-specific work.

This identifies executed overhead, not a promised 15% speedup. PMU samples are
imprecise, and some initialization remains necessary. No production source or
package pin changed. The isolated helper-boundary experiment initially could not
execute because of Windows Application Control. After the owner's setting change,
the retry ran successfully: the instruction-body frame and initialization shrank,
and native, lores and hires complete fingerprints matched with zero allocations.
The subsequent [validation and complete benchmark](CPU_PREFETCH_BOUNDARY_2026-09-22.md)
passed the available correctness checks but **did not pass the 1% performance
gate**: native improves, while lores and hires exceed their upper-bound limits.
Production remains unchanged; no exception has been granted.

[Machine-readable evidence](CPU_DISPATCH_LOCALITY_2026-09-22.json) retains ranges,
sample offsets, prologue assembly, fingerprints, hashes and the failed experiment.
This follows the [aggregate counter investigation](MICROARCHITECTURE_2026-09-22.md).

## Successful retry after the Windows setting change

The isolated candidate runs now. It changes exactly one method attribute:
`TopUpPrefetchAtRetirement`, `AggressiveInlining` to `NoInlining`. A freshly built
unchanged control uses the same archived source and build configuration. The
archived source has LF line endings; its content matches the pinned checkout
after CRLF normalization. Source deltas and binary hashes are recorded in JSON.

The native replay matches the accepted complete fingerprint for both builds.
Candidate lores and hires also match their complete accepted fingerprints.
All four replays report zero measured allocations and no unsupported operation.

| Native Tier1 method / measure | Rebuilt control | Isolated candidate |
|---|---:|---:|
| `ExecuteInstructionBody` code size | 7,632 bytes | 6,020 bytes |
| `ExecuteInstructionBody` stack reservation | 3,160 bytes | 1,912 bytes |
| `ExecuteInstructionBody` unconditional zero stores | 2,864 bytes | 1,776 bytes |
| Out-of-line `TopUpPrefetchAtRetirement` unconditional zero stores | 104 bytes | 104 bytes |

This confirms a **1,088-byte reduction in the instruction body's entry-time
initialization**, plus a 21.1% reduction in its generated code size for these
compilations. The helper already exists out of line in the control for other
callers; preventing its inlining introduces a call on the studied common path.
Accounting for one additional helper entry's 104 bytes leaves 984 fewer bytes
of initialization on that path. This is not a whole-program byte-traffic count:
path frequencies, nested calls and actual instruction work also matter.

`ExecuteSingleInstruction` still clears 1,000 bytes and `TryExecutePlannedKind`
still clears 880 bytes in both new captures. This experiment addresses one
contributor to the original three-method finding, not all dispatch overhead.
JIT layouts continue to vary; the original PMU percentages are not reassigned
to these new layouts.

The diagnostic native FPS was 268.04 for the candidate and 263.23 for the control.
These are **unpaired observations with disassembly enabled**, without retention
telemetry or a confidence bound. They establish no speedup or 1% gate result.
No new PMU capture was made. The earlier blocked attempts remain in the record.
The candidate remains confined to ignored artifacts: no production CPU pin,
engine implementation or published package changed.

## Direct observations

The steady-state CLR method ranges match the Tier1 code sizes printed by the JIT
in the **same process** as the PMU capture:

| Method | Code bytes | Stack reservation¹ | Unconditional zero stores | Cycle samples in zero loop / all main-thread samples |
|---|---:|---:|---:|---:|
| `ExecuteSingleInstruction` | 3,260 | 1,360 | 1,000 bytes | 69 / 2,194 = 3.14% |
| `ExecuteInstructionBody` | 7,616 | 3,160 | 2,864 bytes | 211 / 2,194 = 9.62% |
| `TryExecutePlannedKind` | 10,193 | 1,304 | 880 bytes | 59 / 2,194 = 2.69% |

¹ The `sub rsp` reservation, excluding saved-register pushes. Stack stores are
neither managed heap allocations nor a measurement of traffic reaching DRAM.
The 4,744-byte sum applies to this three-method scalar dispatch path, not every
possible batched, stopped or exception execution path.

Each prologue runs a loop of three 16-byte stores, advancing 48 bytes per
iteration. Iteration counts are 20, 59 and 18 respectively. Additional fixed
zero stores account for the table's totals. The loops execute before the method's
first conditional branch; even an inexpensive planned instruction pays the
kind dispatcher's initialization cost.

For example, `ExecuteInstructionBody` includes:

```asm
sub      rsp, 0xC58
; fixed initialization omitted
mov      rax, -0xB10
; 59 iterations:
vmovdqa  xmmword ptr [rbp+rax-0x40], xmm4
vmovdqa  xmmword ptr [rbp+rax-0x30], xmm4
vmovdqa  xmmword ptr [rbp+rax-0x20], xmm4
add      rax, 48
jne      SHORT -5 instr
```

The loop covers offsets `0x3A` through `0x51`; its sampled offsets are `0x40`
(88 samples), `0x46` (40), and `0x4C` (83). The other loops cover `0x40–0x57`
and `0x32–0x49`. The JSON retains complete prologues and offset counts.
Across entire prologues, including register saves and setup, there are 354 cycle
samples (16.14%). These percentages describe sample locations, not exact stall
time or savings available by removing an instruction.

## Dispatch and locality interpretation

Copper68k already has a 65,536-entry byte opcode-kind table. Its normal KindTable
route calls `TryExecutePlannedKind`; unsupported entries fall back to the scalar
opcode-line decoder. A second packed-plan representation already exists too.
Adding an opcode-kind cache is therefore not a new first target.

The native code footprint is substantial: the three methods above total 21,069
bytes, before instruction handlers. Sampled handlers include `ExecutePlannedMove`
(15,215 bytes), `DecodeLine4` (19,957), and `ResolvePlannedEa` (7,338).
Of 2,277 main-thread samples from Windows' named `IcacheMisses` source,
`ExecuteInstructionBody` has 249, `DecodeLine4` 115, `ExecutePlannedMove` 106,
and `TryExecutePlannedKind` 96.

However, **293 IcacheMisses samples land in the same tiny zeroing loops**.
The interrupt can be delivered after the event that triggered it. These locations
do not prove that the zeroing stores caused instruction-cache misses, nor do
method sizes alone prove cache-capacity pressure. Cache stall penalties and
recoverable locality cost remain unmeasured. The strongest first target is the
observed entry-time work; smaller code would be a possible additional benefit.

A second unchanged-binary native replay emitted actual instruction bytes and
again matched the complete workload fingerprint with zero allocations. Its
Tier1 instruction-body size was 9,068 bytes and kind-dispatch size 10,107 bytes;
large zeroing loops remained. The synthesized-PGO/inlining results can vary
between compilations. Consequently the PMU offsets are attributed only against
the original capture's matching method ranges and disassembly, never the second
run's layouts. The second run is not a performance comparison.

## Specific next experiment

Source inspection uses Copper68k commit
`ada86020ad7a9b298cd7f689c3731d8d779684d1`, the recorded source for
`1.4.1-trace.1`; the core source hash matches the existing
[trace-fix record](copper68k-trace-development-2026-09-18.json).
The package metadata's earlier repository revision is not substituted for that
recorded source. The sibling checkout was inspected read-only; no runtime
reference to it was added.

`M68kCore.cs` has these relevant boundaries:

- `ExecuteSingleInstruction` (line 2769) includes exception retirement paths.
- `ExecuteInstructionBody` (6496) combines opcode fetch, planned dispatch,
  retirement and scalar fallback.
- `TryExecutePlannedKind` (6605) includes aggressively inlined specialized
  instruction helpers, alongside calls to larger handlers.
- `CaptureInstructionFetchPublicationContext` (13132) returns a large value
  snapshot. Its explicit plain-bus guard returns `default` for Lightweight.
- `TopUpPrefetchAtRetirement` (13158) is aggressively inlined and constructs,
  retains and passes publication context through the refill paths.

The initial hypothesis was that inlining these prefetch/retirement paths multiplies
temporary value-structure storage in common callers. A plain-bus null check can
skip metadata construction at runtime while the containing method still pays
for stack initialization on entry. The successful retry isolates a contribution
from `TopUpPrefetchAtRetirement`; the origins of the remaining large frames and
the benefit of deeper publication-context separation remain open.

The first bounded experiment changed only `TopUpPrefetchAtRetirement` from
`AggressiveInlining` to `NoInlining` in an archived, ignored source copy.
Release compilation succeeded with zero errors and two NU1902 warnings for the
transitive build dependency `Microsoft.Build.Tasks.Git` 10.0.300. The isolated
runner then failed before emulation with Windows Application Control error
`0x800711C7` loading the new Copper68k assembly. No security policy was changed,
and there is no experimental replay, frame-size result or speed result to accept.

The owner-requested retry on 2026-09-22 failed at the same load boundary.
Windows Code Integrity events 3033 and 3077 identify a signing-policy rejection
of the isolated DLL (SHA256
`2710185867F6E2D4B219704F6B1670BA4F012B24686284CE458B33F87B1E7E23`).
The assembly is unsigned and has no `Zone.Identifier` download marker to remove.
The retry and event details are retained in the JSON record and local
`isolation/retry-evidence.json`. Execution requires an approved signing or
application-control allowance; no policy was modified during the retry.

The later [validation record](CPU_PREFETCH_BOUNDARY_2026-09-22.md) completes CPU,
engine, disk and native host checks and the full six-pair comparison. It retains
the initial transient receiver allocation failure and all subsequent checks.
The native upper bound is -1.5164%, but lores +6.5470% and hires +4.4964% exceed
the owner's limit. Adoption would therefore require explicit scoped acceptance;
continuing optimization is recommended. The implementation sequence considered
after the diagnostic retry was:

1. Run affected CPU prefetch/interrupt/trace tests against the candidate and the
   required production/engine/disk/host checks before adopting a development pin.
2. Run the unchanged complete three-workload performance protocol. Added helper
   calls could outweigh frame savings; all three upper regression bounds must pass.
3. Investigate remaining frame initialization separately if warranted. Preserve
   transfer order, pending-fetch cancellation, retirement, IPL sampling and trace
   exceptions. Do not substitute skipped local initialization for a correctly
   factored path.

This is a narrow shared-CPU optimization candidate, not a decoder rewrite or a
removal of cycle accounting. The owner's one-sided 95% upper regression bound
of **1% per retained workload** remains unchanged; a larger result requires
acceptance. No previous exception transfers and no package publication is implied.

## Capture and reproducibility

Accepted engine SHA256:
`8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.
Copper68k SHA256:
`0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC`.
The JSON preserves all five assembly identities and the full native fingerprint.

The native replay uses Kickstart 1.3, the existing Lemmings media and bounded
level-one input script, 10,920 warmup frames and 3,600 measured frames. Normal
priority and LP2 placement were checked on the Ryzen 5 5600X; LP3 is its sibling.
No build or test ran concurrently with the PMU capture. The complete identity
matches, measured allocations are zero, and the trace has zero lost events.

The adapted local collector records `PmcProfile`, process/thread/image events,
and CLR GC/loader/JIT events (keyword `0x39`). Its strict sampled sources are
`IcacheMisses` every 8,192 events and `TotalCycles` every 16,777,216 events.
Actual trace source IDs are 9 and 19. JIT disassembly was enabled without changing
the code-generation policy or enabling CPU diagnostic counters that can change
batch admission.

The original runner thread is identified from thread creation. The two forced
generation-2 collections immediately before timing identify the measurement
phase. As in the earlier counter record, intersect all possible placements of
the measured duration between final forced GC end and process exit, trim 100 ms
at each end, and take its middle eight seconds. This window starts 53.373981
seconds after process start. It contains no GC or new JIT method loads.

All selected samples report LP2. There are 2,194 cycle samples (11 unresolved)
and 2,277 named instruction-cache samples (16 unresolved). Method attribution
requires both address containment and a preceding CLR method-load event;
unresolved addresses remain separate. This is one observation, without a
confidence interval. Hardware overflow sampling is imprecise and not a
cycle-accurate attribution of individual instructions.

The converted ETW process-start wall time differs from the collector's process
start by 60.000065 seconds. Host telemetry is therefore aligned using elapsed
time from that shared process-start event, not by directly comparing the two
wall clocks. Six matching telemetry observations show LP2 at 100% and sibling
LP3 at 0.33% mean / 2% maximum. The clock discrepancy does not change the
internally ETW-derived sample window, relative timings or method attribution.

Local raw evidence is under ignored `artifacts/cpu-dispatch-2026-09-22/`:
`collect.ps1`, `locality.wprp`, `launch.ps1`, `capture/`, `native-jit.txt`,
`analyze-locality.py`, `locality.json`, `baseline-bytes.txt` and `isolation/`.
The profile XML, important disassembly and all artifact hashes are retained in
the JSON record. To reproduce locally, run `launch.ps1` from an
administrator PowerShell, convert `capture/counters.etl` using `tracerpt` to
`capture/events.xml` plus `capture/summary.txt`, then run `analyze-locality.py`.
The collector validates pinned inputs and stops only its own WPR instance;
the recorded instance stopped successfully. No ROM or game media is included.

## Primary references

- Microsoft [sampled-counter profile schema](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/sampledcounters).
- Microsoft [.NET method events](https://learn.microsoft.com/en-us/dotnet/fundamentals/diagnostics/runtime-method-events).
- Microsoft [PerfView kernel trace parser](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/Parsers/KernelTraceEventParser.cs), `PMCCounterProfTraceData` payload layout.
- .NET runtime [JIT disassembly options](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md).
