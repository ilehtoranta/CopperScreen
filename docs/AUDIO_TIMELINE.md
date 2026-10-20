# Bounded installed audio timeline

`copperscreen-audio-owner-timeline-v1` is diagnostic instrumentation for P1-07
refill investigations on Lightweight. It is excluded from ordinary production
builds. It does not change guest CPU/chipset ordering, audio generation or queue
capacity, and does not qualify a release budget or historical performance gate.

## Collection

Two independent single-writer rings record runtime-owner and native-consumer
events. Each retains its first 4096 records and most recent 16384 records. Any
overwritten middle range is explicit; missing coverage never means no work or no
underruns. Recording uses preallocated value-type slots, QPC timestamps, existing
queue counters and, for field/command intervals, current-thread CPU accounting.
There are no stack samples, cross-thread suspensions, file writes, locks or
managed allocations in the ring writer. Diagnostic allocation still has a cost:
storage is reserved before guest execution, and the observer is not assumed free.

From diagnostic 1.1.43.0, the first active underrun arms incident retention. The
owner stores 4096 further events and the callback stores 1024, then each stops
replacing its retained tail. This preserves the first episode's preceding and
following context through a long native save/readback workflow. The trigger
does not copy or allocate; the same preallocated storage is used. Subsequent
events are counted in `notRecordedAfterIncident`, and `observedTotal` includes
them while `total` counts events admitted to the ring. They are unavailable
coverage, distinct from the ring's overwritten middle range.

`firstIncidentQpc` is zero when no underrun triggered retention.
`finalCumulativeMissingValues` keeps updating independently of event retention;
use it and the ordinary output counters to assess total loss. A retained-event
sum can be smaller after recording stops. Earlier version 1 exports omit these
extensions and keep the original startup/final-tail policy. The reader preserves
that distinction; older coverage losses and verdicts remain unchanged.

The installed workflow completes measurement, checkpoint/export and normal pause,
then freezes both trace writers on the runtime owner and stops the native consumer before exporting
`<case>-<phase>-audio-timeline.json`. Serialization and file I/O happen after that
boundary. `nativeStopped` and each ring's `incomplete` count must establish a
complete snapshot before analysis. The execution owner remains alive for the
window's final ordinary save/close commands; stopping it at export would strand
those commands. Ordinary application shutdown joins the owner afterward.

## Fields and interpretation

All stage times use the same monotonic QPC frequency. The UTC anchor is only a
convenience for locating the run; QPC supplies interval comparisons. Queue depths
are concurrent observations, not an atomic cross-thread transaction. Actual
callback `MissingValues` remains authoritative for an underrun.

| Owner kind | Captured work |
| --- | --- |
| 1: field | Start before command draining and presentation reservation; reservation complete; guest field complete; PCM generated/mixed/submitted; publication complete. Includes queue depths, cumulative missing values, guest field ordinal and cumulative GC collection counts. |
| 2: command | Owner command execution and its requested state publication. Includes wall interval, thread CPU accounting and queue/missing counts. Controller command receipts identify requested native controls separately. |
| 3: demand wait | `WaitAny` interval and result (command wake, consumer wake or timeout), with queue/missing counts. Includes intentional waiting and any delay before the owner runs again; it does not identify kernel scheduling decisions. |
| 4: recovery | Native recovery calls lasting at least one millisecond. CPU accounting is unavailable for these entries. |

Version 1 recovery entries do not capture pre-call queue/missing counters. Their
raw before fields are placeholders; the reader exposes them as unavailable
(`null`). Use overlapping callback records and adjacent field/command counters,
never a placeholder zero, to assess loss during recovery.

New captures also include `PublicationCoreMilliseconds` on owner records. For a
field with a reserved presentation slot this measures state capture and queue
publication before invoking the notification handler; `-1` means no reserved slot.
Older version 1 records omit it and the reader exposes it as unavailable. The
QPC interval after PCM submission still includes the notification handler. Use
both intervals to distinguish publication work from synchronous notification
delay; neither alone proves a particular Windows wait cause.

Callback records retain QPC bounds, requested sample values, queue depths,
cumulative/individual missing values and observed pause expectation. Existing
active underrun accounting remains separate from deliberate paused silence,
unavailable-time suppressed PCM and discarded unread values. Native stop/reopen
can cause an intentional callback gap even when its underrun count is zero.

CPU counters are cumulative kernel plus user time from
[GetThreadTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getthreadtimes),
in 100 ns units; `-1` means unavailable. Units do not imply 100 ns precision.
OS accounting can be coarse, so short-interval CPU and wall differences are not
precise preemption measurements. Large wall/CPU differences can narrow the next
probe, but cannot distinguish GC suspension, kernel blocking and host scheduling
without further evidence. GC collection counts do not measure pause duration.

## Running and reading

Build a fresh signed diagnostic candidate with `-InstalledTestBuild`. Run the
[installed recovery controller](../scripts/test-msix-recovery.ps1) with
`-NativeControlsOnly -AudioTimeline -RefillObservationSeconds 120` and a fresh configured run
directory. The default refill observation remains eight seconds for existing
callers. No S3, PnP or display interruption is requested in this mode; use a scoped
controller wake request (provided by native-only mode) for an unattended native audio trace. Keep all package,
source, controller and media hashes with the run.

After a process exports its trace, run:

```powershell
./scripts/qualification/Read-CopperScreenAudioTimeline.ps1 -Path '<exported timeline.json>' -OutputPath '<new analysis.json>'
```

The reader preserves retention losses, separates startup field bands, lists long
owner intervals and correlates missing-callback episodes with overlapping and
adjacent owner work. It rejects incomplete/unjoined traces and refuses to replace
an existing analysis. Its output is diagnostic evidence, not an automatic cause
or release verdict. [Tooling controls](../scripts/tests/test-audio-timeline-tools.ps1)
check units/correlation, zero-loss input and incomplete-trace rejection. Packaged
host tests separately check ring retention, concurrent snapshot integrity and
allocation-free writes. Normal-build observer exclusion is checked independently.

Keep prior [display recovery](AUDIO_DISPLAY_RECOVERY_2026-10-18.md) and
[EventPipe findings](AUDIO_REFILL_TRACE_2026-10-18.md) unchanged. P1-07 and release
budgets remain OPEN until separately qualified.
