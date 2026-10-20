# Installed warmup and audio publication investigation — 2026-10-19

Status: **BOUNDED_WARMUP_AND_REFILL_CONTROLS_PASSED** on the provisional PC.
P1-07, release budgets and release acceptance remain **OPEN**. The
[machine-readable record](AUDIO_WARMUP_REFILL_2026-10-19.json) binds the
packages, frozen sources, tests, controllers, raw traces and persistent state.
The qualification date uses Europe/Helsinki; individual receipts retain UTC timestamps. Lightweight remains the engine; Legacy is unavailable.

## Findings and corrections

Signed diagnostic **1.1.38.0** captured **234,178** missing sample values during
CopperOS startup. Fields 9–30 averaged 43.94 ms of guest work and 39.77 ms of
thread CPU; fields 31–100 averaged 46.77 ms and 40.18 ms respectively. Later
fields fell to roughly 8–9 ms. The application, engine and pinned Copper68k
images were IL-only. This is consistent with slow initial compilation-tier
execution, rather than a publication wait dominating that startup episode.

The desktop now disables Quick JIT. Its IL methods compile with optimization on
first use; framework ReadyToRun tiering remains possible, as described in the
[.NET compilation configuration](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation).
The bounded **1.1.39.0** controls support this diagnosis: early startup loss
disappeared on both native paths and both processes. Compilation cost moves to
first use: one retained first guest field took about 452 ms before playback was
primed. This does not qualify startup latency or a release budget.

Version 39 still reproduced **23,634** late missing values during the fresh
CopperStart process. Its three episodes coincided with **175.42, 229.38 and
238.08 ms** after-submission publication intervals, while guest work remained
**10.12, 11.05 and 11.07 ms**. Thread CPU accounting was small and coarse. The
timeline localizes those stalls to publication; it does not identify a particular
kernel wait or GC pause. The old handler called `Dispatcher.Post` synchronously
on the PCM owner. The exact
[Avalonia 12.0.3 implementation](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.3/src/Avalonia.Base/Threading/Dispatcher.Queue.cs)
uses a dispatcher lock and platform wake signal in that path.

Rendered fields now use nonblocking presentation reservation/publication when
audio exists. UI contention may drop presentation while the owner still renders
every guest field, pixel and PCM value. One reusable coalesced work item performs
UI dispatch away from the PCM owner, at the existing UI priority. Three blocked-UI
regressions **fail against the exact signed 39 payload and pass against signed 43**.
Silent execution keeps its existing presentation backpressure. At that stage queue capacity,
MMCSS policy, GC policy and guest timing are unchanged.

The first trace exporter incorrectly stopped the owner before ordinary closing
save commands. Version 38 remains **FAILED**, with its completed native checkpoint,
export, trace and narrowly scoped owned-process termination retained. The fixed
exporter freezes diagnostic writers, joins the native consumer and keeps the
owner alive for normal save/close. Its regression fails against 38 and passes
against the later payloads. **1.1.40.0** was superseded before installed testing
when broader host tests exposed an older default custom-output pause regression;
41 restores discard-on-pause for that contract. Native output already overrides it.

## Retained CopperOS catch-up burst

Version 41 passed native save/readback but retained **2,926** missing values in
the CopperOS extended reopen window. Guest fields stayed around 10–12 ms and
publication around hundredths of a millisecond. Nearby GC counts stayed constant.
After a 78 ms callback gap, twelve 2048-value native requests arrived in three
four-call groups over **47.4253 ms**. The producer submitted four complete fields
between the groups. There is no long owner publication interval at this episode.
This is native catch-up demand, distinct from the earlier blocked-UI correction;
the trace does not identify why Windows/backend delivery became bursty.

The conserved starting availability is **13,958 values**, reconstructed from
24,576 requested minus 2,926 missing minus 7,692 produced. This is a reconstructed
aggregate, not a retained dump of PCM or every original slot length. A deterministic
replay preserves that demand/production order and conservative PAL block lengths.
Eight blocks reproduce the exact **2,926** deficit, nine leave **1,004**, and ten
complete without loss while preserving PCM order. The production-policy assertion
fails against the exact signed 41 payload and passes against 43.

The desktop reserve is therefore **ten guest-field blocks instead of eight**.
This adds roughly **40 ms of queued PAL audio**, in addition to native device
buffering. It does not remove generated samples, stretch audio or execute guest
hardware on another thread. Latency and release budgets remain OPEN; this is a
bounded correction for the observed demand, not protection against an unbounded
device/OS stall. The 41 residual and all normal-exit/persistence receipts remain
retained without a zero-loss verdict.

## Running commands and first-incident retention

Version **42** still reported **144,130** missing values in CopperOS warmup.
Only **92,316** remained in the callback tail at final export, and the associated
owner context had been overwritten. The missing **51,814** values and owner
coverage are unavailable evidence; no precise retrospective cause is assigned.
CopperOS nevertheless completed both normal exits and matching native saved-state
readback, with zero missing values in its fresh process. The superseded run was
then intentionally closed normally during early CopperStart startup; its raw
root status remains **FAILED** and CopperStart is **NOT QUALIFIED** on 42. The
scoped close receipts and raw cleanup record are retained. Additional diagnostic
control compilation occurred after the initial failure; no throughput verdict
is inferred from that candidate.

Inspection found a remaining blocking path: command publication still acquired
the UI presentation lock before and after copying pixels. The CopperOS native
inspection workflow regularly issues owner commands. Two discriminating controls
hold that lock at each position: **both fail against exact signed 42 and pass
against 43**. Running command publication now uses nonblocking locks too. Commands
still execute and return their owner-captured results; visual state may be
published by a subsequent field. Paused and silent publication retain their
waiting behavior. This corrects a demonstrated blocking path without claiming
that unavailable 42 owner intervals prove its precise historical cause.

Diagnostic 43 arms retention at the first active underrun: owner and callback
rings keep 4096 and 1024 following events respectively, then stop replacing the
incident's tail. This uses the same preallocated storage and does not copy or
allocate on the trigger. Later unstored events are counted explicitly;
`finalCumulativeMissingValues` remains independent of retained-event sums. The
reader and regression controls distinguish final loss from unavailable coverage.
Earlier rolling-tail captures and their losses are preserved unchanged.

## Final installed controls

Signed diagnostic **1.1.43.0** SHA-256:
`7a34dc3fbe845959d1ea94f1e85e5b61031e55064797a9f73a42d098319cad02`.
Its 190 frozen source inputs and all 266 payload files were verified, as were
signature, installed binaries and the unchanged pinned Copper68k fingerprint.
The frozen continuation retains the prior host basis, including a dormant input
release helper with no callers in that window. It does not package the unrelated
shared-tree work currently in progress.

| Native path | First-process warmup missing values | Fresh-process final missing values | Full native reopen including 120-second follow-up | Native save/export/readback |
| --- | ---: | ---: | ---: | --- |
| copperos | 0 | 0 | 0 | PASS |
| copperstart | 0 | 0 | 0 | PASS |

All four processes have zero final cumulative missing values in their completed
callback timelines, zero audio submission failures and zero callback errors.
Baseline, native restart and extended reopen windows have zero missing values;
their suppression and discard counters remain explicit in the JSON. Intentional
paused silence and retirement of unread PCM during lifecycle changes are separate.
Both normal exits per path return zero. Native disk/workspace hashes and all
three profile/settings JSON files match fresh-process readback. Both CopperStart
exports match the protected imported ADF. Both CopperOS archives contain the
native saved `System.adf` and export manifest. Owned diagnostic registration is
removed after private-state evidence is retained.

The production solution builds with zero warnings/errors. **28 focused normal
host checks**, **33 checks against the exact packaged binaries**, and **five
timeline-tool controls** pass without skips. The updated reader controls check final counters and unavailable coverage after incident retention. These are overlapping scopes, not
61 distinct host tests. The normal build excludes the diagnostic timeline type.
No engine execution changes were made for this investigation, so no new engine
suite or chipset correctness verdict is claimed.

## Limits and follow-up

The [bounded timeline protocol](AUDIO_TIMELINE.md) retains startup and tail ranges;
overwritten middle events, later unrecorded events and incomplete counts are explicit. Final cumulative
callback counters are retained independently of individual-event retention.
There is no stack sampling or trace serialization during the measured playback.
The instrumentation still has a cost and is diagnostic-only.

The shared host reached full CPU utilization. Presentation progress slowed while
guest/audio progress continued. This is **INVALID / RERUN for formal performance
acceptance**, not a controlled performance comparison or responsiveness budget.
Physical sound was not observed in this continuation. No display-off, S3, endpoint
PnP, MSIX update, WACK or full-hour qualification was performed for this new hash.
Repeat those exact-candidate checks separately before release acceptance.

The original **748-value CopperOS episode on 37 has no retrospective owner trace**;
its precise cause is not established by a non-recurrence. The corrected shared
publication path passes these new CopperOS controls. The previous 20 dated
evidence files, including the [display recovery](AUDIO_DISPLAY_RECOVERY_2026-10-18.md)
and [EventPipe investigation](AUDIO_REFILL_TRACE_2026-10-18.md), remain unchanged.