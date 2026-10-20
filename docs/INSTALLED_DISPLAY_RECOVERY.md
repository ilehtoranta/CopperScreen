# Installed display and audio recovery

Protocol `copperscreen-installed-recovery-v4` extends the retained
[version-3 recovery procedure](INSTALLED_RECOVERY.md). Earlier signed packages,
raw failures and dated evidence keep their original protocol and verdict.
The [2026-10-18 continuation](AUDIO_DISPLAY_RECOVERY_2026-10-18.md) records the
exact signed candidates, passing display/persistence checks and residual audio
underruns; P1-07 and release budgets remain OPEN.
The [bounded EventPipe follow-up](AUDIO_REFILL_TRACE_2026-10-18.md) retains profiler
effects and GC-only observations separately; it adds no recovery acceptance.
The [bounded owner/callback timeline](AUDIO_TIMELINE.md) provides a later,
diagnostic-only investigation path without stack sampling. Native-only mode can
extend the reopen observation and request/verify trace exports. Its scoped wake
request prevents idle sleep/display-off only during that native-control run;
display-only and physical S3 modes retain their interruption behavior.
Use a fresh evidence directory and owned diagnostic identity. The app stays
non-elevated, on Lightweight; release budgets remain OPEN.

## Host audio contract

An audio endpoint is a host clock only while it delivers callbacks. If output is
unavailable, the runtime continues complete guest fields at its ordinary video
rate, without waiting for presentation or accumulating a catch-up burst. It still
renders/drains guest PCM, but omits unavailable-time playback and records those
values separately in diagnostic builds. Commands, native storage and pause state
remain under the runtime's single owner. The app does not turn the display on.

Windows output stays on WASAPI, including initial startup without a default
endpoint. It retries the current default endpoint without selecting a silent null
backend. This follows Microsoft's
[invalid-device recovery guidance](https://learn.microsoft.com/en-us/windows/win32/coreaudio/recovering-from-an-invalid-device-error).

A restarted valid sink can retain its existing reserve. Full native uninitialization
joins callbacks before resetting the queue's consumer indexes. A new or fully
reopened device waits for the existing eight-buffer reserve before playback starts.
This avoids starting an empty replacement; it does not increase the buffer count,
repeat/stretch samples or change emulated CPU/chipset ordering. Ordinary concurrent
pause/reset continues to use epoch discard. It never resets consumer indexes while
the callback can run.

The Windows PCM-producing runtime thread requests the best-effort MMCSS `Games`
task and reverts the registration on that thread at exit. Process priority and
affinity remain unchanged. Failure to register leaves normal scheduling usable.
See [MMCSS](https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service)
and [thread registration/lifetime](https://learn.microsoft.com/en-us/windows/win32/api/avrt/nf-avrt-avsetmmthreadcharacteristicsw).
This is an explicit desktop scheduling policy; retained headless performance
protocols and their placement/priority requirements are unchanged. Registration
alone is not evidence of better audio, and total CPU load does not establish
the cause of an earlier gap.

## Display-only installed control

Run `scripts/test-msix-recovery.ps1 -DisplayRecoveryOnly` with a signed diagnostic
package and a fresh configured run directory, using the same media/ROM and native
workspace inputs as the recovery procedure. This mode needs no elevated helper.
It declares `DisplayRequested=true`, observed `DisplayTested` separately, no
requested/tested S3 or PnP cycle, and `FullMatrixRequested=false`.

For each boot path, the controller runs native stop/restart and full close/reopen
controls, then waits at `display-off` for an announced interruption. The bounded
display test uses `SC_MONITORPOWER` to request display off and subscribes to
`GUID_CONSOLE_DISPLAY_STATE` through `PowerSettingRegisterNotification`. API success
alone is insufficient: Windows must report state 0 throughout the off window.
Guest frames must advance while the native clock is unavailable. A one-shot
`ES_DISPLAY_REQUIRED` request restores the display; state 1 and healthy native
callbacks must return. Cleanup requests display on even if the test fails. No
power-plan or unlock policy changes are made.

These are OS display-state receipts, not instrumentation of the physical panel.
User input or another application waking the screen invalidates the off interval.
The controller retains the failure and restores the display. See
[power notifications](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersettingregisternotification),
[display state values](https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids)
and [monitor-power command](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand).

The sequence then checkpoints/exports and independently reopens each native boot
path, comparing saved disk and profile/settings hashes. Physical audibility stays
separate owner evidence and does not block export/readback. Display-only success
does not establish fresh S3, endpoint PnP, update, WACK or full-hour acceptance.
The full recovery controller separately records its post-S3 display-on request;
that API request alone does not prove display state or unlocking.

## Audio observations

Diagnostic-only sequential commands `audio-scheduling-off` and
`audio-scheduling-on` permit a paired comparison on the same installed process.
The display-only CopperStart sequence records two eight-second windows in each
mode, aggregate system CPU time and exact snapshot times. Controls/snapshots can
add observer work. These short windows are not throughput or full-hour acceptance,
and cannot establish the cause of intermittent earlier gaps.

Active callback underruns remain `missingSamples`. Deliberate paused silence is
`pausedSilenceSamples`; neither hides the other. `suppressedSampleValues` records
guest PCM omitted while the output is unavailable. `discardedSampleValues` records
unread queue content removed after joining the old consumer. Maximum actual
callback gaps remain visible across native stop/reopen and display-off periods.
Zero callback underruns does not imply uninterrupted or glitch-free physical sound.
Retain warmup and interruption totals alongside steady follow-up windows.

For full reopen, compare the final missing count against the snapshot **before the
close command**, as well as the follow-up window. Otherwise a faster restart could
move an early underrun outside the selected window and conceal it.
