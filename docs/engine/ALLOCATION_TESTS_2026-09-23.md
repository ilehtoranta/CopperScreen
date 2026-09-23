# Diagnostic allocation-counter correction — 2026-09-23

The intermittent serial, receiver and disk zero-allocation failures were caused
by background-GC allocation-context accounting, rather than objects allocated by
those measured hardware loops. The diagnostic test project now uses blocking GC.
All measured loops and exact zero-byte assertions remain unchanged. Production
engine, CPU, desktop and benchmark runtime settings are unchanged.

## Evidence and cause

The original [batch-prefetch comparison](CPU_BATCH_PREFETCH_2026-09-23.md) retains
its failing full-suite runs. An additional unchanged candidate run reproduced the
serial failure with 7,928 reported bytes (`untraced-3.trx`). Other unchanged runs
passed, so a passing retry alone did not resolve the issue.

A native CLR profiler enabled `COR_PRF_MONITOR_OBJECT_ALLOCATED` and
`COR_PRF_ENABLE_OBJECT_ALLOCATED`, with thread-local markers around the existing
counter windows. Two serial windows reported 3,360 and 1,256 bytes respectively,
while the profiler recorded **no object allocations** inside either window.
A deliberate `new byte[1000]` control produced an allocation callback, the correct
managed stack and a matching 1,024-byte counter delta. Profiling was diagnostic;
none of these runs are throughput measurements.

A separate program exercised the actual `LightweightDiskReceiver` source:
12 workers each ran 10,000 measurement windows of 2,000 receiver advances. Small
arrays allocated *before* the windows left partially used thread allocation
contexts. A separate thread created allocation pressure and requested generation-2
collections. This isolated the runtime effect from xUnit and Copper68k execution:

| Configuration | Nonzero windows | Deliberate allocation inside window |
| --- | ---: | --- |
| Blocking collection requests | 0 / 36,000 | None |
| Background collection requests | 455 / 120,000 | None |
| Background requests, concurrent GC disabled | 0 / 120,000 | None |
| Concurrent GC disabled, positive control | 120,000 / 120,000 | Exactly 1,024 bytes in every window |

The installed runtime is .NET 10.0.11, Windows x64. Its
[allocation-counter implementation](https://github.com/dotnet/runtime/blob/v10.0.11/src/coreclr/vm/comutilnative.cpp)
returns allocated bytes minus unused space in the current thread's allocation
context. At the end of background marking,
[`background_mark_phase` calls `repair_allocation_contexts(FALSE)`](https://github.com/dotnet/runtime/blob/v10.0.11/src/coreclr/gc/gc.cpp),
which invokes `void_allocation`. That clears the allocation pointer and limit
without subtracting the unused space from `alloc_bytes`. The counter can therefore
increase by the discarded space even when no object is allocated by the thread.
Ordinary blocking collection repairs the accounting differently. A collection-count
comparison alone is insufficient: background marking can finish inside a window
even when the collection started before it.

## Correction and validation

`CopperMod.Amiga.Lightweight.Tests.csproj` sets
`ConcurrentGarbageCollection=false`. The emitted test runtime configuration was
checked for `System.GC.Concurrent: false`. GC remains enabled; parallel tests
remain enabled. There is no byte allowance, retry-until-pass logic or suppression
of allocation assertions. The positive control establishes that real allocations
are still counted exactly.

An additional mutation check inserted `new byte[1000]` into the serial measurement
in an ignored copy of the tests with the corrected GC setting. Its unchanged
zero-byte assertion failed with **expected 0, actual 1,024**, as required. The
mutation is confined to the probe copy and is not present in repository tests.

The production Release build succeeds with zero warnings and errors. The normal
isolated diagnostic build and subsequent full-suite repetitions pass **669/669**
five times per CPU, totaling **6,690 passed executions** with no skipped tests.
Results are recorded in
the [machine-readable evidence](ALLOCATION_TESTS_2026-09-23.json). Reference and
candidate runs use identical test outputs except for `Copper68k.dll`:

- Reference: `982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F`.
- Candidate: `881982995B5FBA962E53BBDC83DD64E4893C7B5699802E0C3D31DC33C022DE13`.

This correction does not change or rerun the retained FPS comparison. The original
failed runs stay failed in their historical record; the corrected-host runs are
new evidence. It does not activate the candidate CPU package or change native
compatibility coverage.

Raw probes, profiler source, positive controls, generated runtime configuration,
build logs and TRX files are retained locally under
`artifacts/allocation-probes-2026-09-23/`. The standalone `ReceiverProbe` program
links the actual receiver source and takes `--allocate` for its positive control;
`DOTNET_gcConcurrent=0` selects the corrected runtime behavior for that probe.
The test project selects the setting itself; normal diagnostic test commands need
no environment override.
