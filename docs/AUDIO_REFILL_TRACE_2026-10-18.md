# Bounded installed audio refill profiling — 2026-10-18

Status: **residual loss reproduced; cause OPEN**. Release budgets and P1-07 remain
OPEN. This follows the [signed display/audio correction](AUDIO_DISPLAY_RECOVERY_2026-10-18.md)
without changing production behavior or replacing its evidence. Lightweight only;
Legacy remains unavailable. Nothing was published.

The [machine-readable record](AUDIO_REFILL_TRACE_2026-10-18.json) binds the unchanged
signed `1.1.37.0` MSIX, frozen smoke controller, local tool, analyzer source/output,
EventPipe files, raw audio telemetry, exports and normal-exit receipts. This is
protocol `copperscreen-audio-refill-eventpipe-v1`, for diagnosis on the provisional
PC. It is **INVALID / RERUN for performance acceptance**: no qualified host-load,
placement or release-budget series was collected. Earlier results are unchanged.

## Captures

`dotnet-trace 10.0.750501` was installed into an ignored repository artifact
directory, with no global tool or application dependency change. The analyzer
uses its bundled TraceEvent 3.1.23. Both captures report zero lost events and
successful tool exits. No administrator helper, sleep or device interruption ran.

| Capture | Scope | Result |
| --- | --- | --- |
| Managed stack samples + GC, 180 seconds | Fresh installed CopperStart startup/warmup | Seven GC starts. Six `SuspendForGC` sequences have a maximum upper bound of 189.8215 ms; one GC-preparation sequence is separate. Another 16214 `SuspendOther` sequences total about 15.28 seconds, with a maximum 266.1047 ms. |
| GC only, 120 seconds | Already scheduled fresh-process CopperStart reopen | Three collections and three `SuspendForGC` sequences: 18.0656, 6.3156 and 4.1093 ms. No stack sampling or `SuspendOther` sequence is recorded. |

The analyzer measures `GCSuspendEEStart` through `GCRestartEEStop`, including the
rendezvous; this is a conservative interval, not pure collection CPU time.
Runtime suspension reasons are retained individually. The stack-sampling report
also includes waiting threads; its percentages are not kernel CPU utilization.
The numerous suspensions while sampling was enabled prevent treating that run's
increased underruns as normal application behavior. See Microsoft's
[profiling limitations](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-highcpu)
and [tool reference](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

The GC-only snapshots from warmup 25.03 through 135.39 seconds retain the same
410910 cumulative missing sample values: **zero additional loss in that retained
interval**. Startup loss precedes those snapshots. Snapshot observation UTCs are
coarse reads after creation, not exact callback timestamps. This neither
reproduces nor explains the late CopperOS gap from the earlier recovery run.
The two captures differ in process and time, without controlled host-load
equivalence; do not attribute their differing counts solely to the profiler.

## Completion and remaining work

The smoke controller runs a 60-second CopperStart session after native warmup,
then a fresh-process 10-second readback after its own warmup. The first measured
window retains 7600 missing values, after stack collection had ended. Local trace
analysis overlapped later diagnostic execution; no uninstrumented acceptance is
inferred. Both normal exits, native media exports and matching profile/settings
hashes are checked in the JSON record. The owned diagnostic registration is
removed after private state is copied to the evidence root. `Smoke=true` and
`Acceptance=false` remain explicit.

There is no demonstrated basis here for a GC-policy, buffer-capacity or guest
timing change. [LWA-HOST-004](engine/ISSUES.md) remains OPEN: use a bounded,
low-intrusion owner/command/queue timeline or Windows scheduling trace around a
new residual occurrence, retaining profiler effects separately. Exact-candidate
full hours, S3/PnP, signed update persistence and WACK remain separate, unrun gates.
