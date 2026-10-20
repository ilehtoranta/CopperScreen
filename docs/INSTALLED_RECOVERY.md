# Installed sleep and audio-device recovery qualification

P1-07 now uses `copperscreen-installed-recovery-v3` for bounded observations on the
owner's current Windows 11 PC. Release budgets remain **OPEN**. This is separate
from installed-hour, MSIX-update, WACK and historical engine throughput protocols.
Lightweight retains sole ownership of the guest; Legacy stays unavailable.

## Scope and prerequisites

The diagnostic package contains the same qualified standalone CopperStart ROM,
bounded PAL OCS A500 configuration and development CopperOS storage pair as the
installed-session protocol. CopperStart uses the frozen read-only Lotus II ADF
and input sequence; CopperOS uses a newly owned workspace with native Shell
storage mutations. Originals and earlier package evidence must remain unchanged.

Use an exact signed diagnostic package and unregistered owned test family. A
fresh attempt may reuse an unchanged package after verified cleanup; preserve
the earlier attempt and use a new external output directory. Retain exact
package/source/payload hashes, executable manifest, signing verification and
normal-production observer isolation. The `Recovery` request flag defaults to
false, preserving the earlier soak protocol. Diagnostic recovery commands are
limited to snapshot, pause, resume, checkpoint, finish and diagnostic `audio-stop`
and `audio-close`,
with sequential IDs. Version 2 adds the native stopped-sink control and requires
the new diagnostic candidate; older version-1 records remain unchanged.
They use the normal runtime/toolbar paths and cannot execute arbitrary host code.
Version 3 adds a fully closed native-device control and bounded native warning/error
messages. The runtime owner closes the device; production recovery must reopen it
without changing the guest or queued PCM. This control is not an OS interruption.
The diagnostic log is capped at 64 messages and absent from normal production builds.
Earlier version-1/version-2 packages, protocols and FAILED records remain unchanged.

The current host-specific helper requires an initially enabled NVIDIA HDMI audio
codec in PnP class `MEDIA` or its named `AudioEndpoint`, pinned by instance ID in
the run configuration. It
must never disable a display adapter or GPU. Its separate elevated process owns
only the bounded power/device actions; the installed app runs without elevation.
Other device families and default-device switching require separate qualification.
`-Cases` and `-SkipSleep` permit explicitly scoped continuations. Such records
declare their selected cases and missing sleep coverage; they do not independently
qualify the full two-case matrix or silently turn unavailable coverage into PASS.
`-NativeControlsOnly` skips the elevated helper and all physical interruption
gates. It runs the native stop/reopen controls followed by checkpoint/export and
fresh-process readback. Its record declares no requested or observed sleep/device
coverage and `FullMatrixRequested=false`; a passing software-only run cannot close
the physical recovery matrix. Requested and observed physical coverage are separate
fields in version 3.
Physical audibility is separate operator evidence. By default it does not block
the automated checkpoint/export/reopen sequence. `-WaitForCopperStartSound`
optionally holds the recovered CopperStart window at `audio-observation`; its
bounded coordination timeout is not an application recovery verdict. Retain late
replies with their receipt time and an explicit unknown observation time rather
than claiming that the speaker was heard at receipt time or in a later rerun.

An operator must be available to wake/unlock the PC if necessary and confirm
physical sound. Verify available sleep states, current wake-timer policy, power
requests and the original enabled device state before proceeding. This host
supports S3 and already enables wake timers; no power-plan change is needed.

## Procedure

[`test-msix-recovery.ps1`](../scripts/test-msix-recovery.ps1) installs the exact
candidate, activates each boot path through COM and waits for its usable native
workload. CopperStart reaches 14,000 runtime fields using the retained input;
CopperOS creates/verifies and checkpoints native files/settings. The controller
stops at a named `go-<case>-<step>.json` gate before each physical interruption so
the operator can be notified. A gate is test coordination, not passing evidence.

For each CopperStart and CopperOS process:

1. Retain an active baseline with frame progression and output-callback counters.
   CopperStart must supply nonzero PCM; the quiet CopperOS CLI is not an audible
   workload. Obtain operator confirmation of CopperStart sound separately.
   Version 2 also stops the native sink through a diagnostic owner-thread command
   and verifies that production recovery restarts callbacks and guest execution.
   That injected control is explicitly separate from real Windows interruptions.
   Version 3 additionally closes/uninitializes the native device and verifies a
   counted production reopen, a started output and fresh callbacks.
2. Suspend the active process with a 40-second absolute UTC wake timer. Resume,
   record actual Windows power events, observe responsive snapshots and active
   frame/audio delivery, and retain timing/counter changes.
   The version-3 physical controller requests the display on after the S3 host
   receipt with one-shot `SetThreadExecutionState(ES_DISPLAY_REQUIRED)`, then
   allows a three-second display/link settle before the unchanged follow-up
   observation. It records the request separately and changes no power plan or
   unlock policy. An accepted API request does not confirm the physical panel or
   unlock state. This is explicit attended HDMI test preparation:
   [automatic timer wake does not turn on the display](https://learn.microsoft.com/en-us/windows/win32/power/system-wake-up-events),
   and [the one-shot display flag resets its idle timer](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate).
   Earlier failures did not capture panel state and are not retroactively assigned
   to this possible host condition. The earlier version-3 software-only harness
   has no sleep action and does not use this display preparation; both harness
   hashes are retained independently.
3. Pause through the ordinary toolbar/runtime command, settle the pause, then
   suspend/resume again. The paused guest frame number must remain unchanged and
   pause must persist. Resume explicitly and observe active delivery afterward.
4. Temporarily disable the pinned HDMI audio device in Windows, retain its
   disabled state and an in-app snapshot, then re-enable it in `finally`. Verify
   original enabled state, recovered callbacks and CopperStart PCM. This tests
   real Windows device availability changes, not physical cable or USB removal.
5. Checkpoint, verify native CopperOS storage again, export the workspace or
   protected game image, close with exit zero and reopen in a fresh installed
   process. Compare media, personal-profile and settings hashes.

The [host helper](../scripts/qualification/Invoke-CopperScreenRecoveryHost.ps1)
uses `SetSuspendState` with hibernation and wake-event disabling both false.
It enables shutdown privilege only in its own token and closes its wake timer
after returning. The absolute timer matters because current Windows excludes
low-power time from relative timers. See Microsoft's
[suspend API](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-setsuspendstate)
and [waitable-timer API](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-setwaitabletimer).
Device removal/restoration uses the documented
[PnP device controls](https://learn.microsoft.com/en-us/powershell/module/pnpdevice/disable-pnpdevice).

## Evidence and interpretation

Retain raw Windows suspend/resume events and wake-source records; calling an API
alone does not prove sleep. Never relabel a paused interval or simulated suspend
as a physical transition. Capture actual app identity/version, loaded binaries,
process ID/exit, window state, guest frame/CPU state, callback counts, guest PCM,
queue misses, callback exceptions, submit failures and scoped steady windows.
Retain snapshots and the full 250-ms trace, including real sleep/device gaps.

The hour-trace validator intentionally rejects long gaps and paused frames; do
not apply it to this different recovery protocol. Silence/missing-queue counts
while deliberately paused or without a device are separate from misses during
the recorded active recovery windows. Callback progress and nonzero software PCM
do not by themselves establish speaker audibility or OS/driver glitch counts.
Preserve direct operator observations as a separate result.

Test waits and watchdog limits bound liveness and cleanup; they do not freeze
shipping startup, recovery, pacing, audio-latency or memory budgets. Record actual
timing/catch-up observations without converting them into an unapproved budget.
Unsupported transitions or missing evidence remain unavailable coverage.

### Host audio recovery implementation

The signed `1.1.32.0` rerun also exposed a post-S3 interval with responsive app
snapshots but unchanged guest frame and callback counts. Its raw FAILED result
is retained. That observer did not record native device state, so it does not
establish the exact driver/backend cause. A later preflight separately observed
the HDMI endpoint unavailable (problem 45), then enabled again.

The `1.1.33.0` host fix bounds the full-queue demand wait at 250 ms. On timeout,
the runtime owner services the output: after a second without callbacks it
attempts native stop/start, at most once a second, retaining the queue and machine.
Healthy callback-driven pacing is unchanged. No timer executes guest hardware
and no device lifetime operation runs inside an audio callback. This follows
[miniaudio's callback/lifetime guidance](https://miniaud.io/docs/manual/index.html).
These are implementation retry intervals, not approved release recovery budgets.

The subsequent `1.1.33.0` installed run exposed repeated `MA_UNAVAILABLE`
restart results after S3, followed by frozen frames/callbacks across endpoint
restoration. Its failed record remains intact. The next host correction reopens
the default native device when restart fails, joining old callbacks before
reinitializing; failed opens remain eligible for bounded retry. The PCM queue,
gain and guest machine survive this operation. Diagnostic snapshots synchronize
with device lifetime operations, and record reinitialization counts. Version-2
follow-up checks now also require a started output and a fresh callback;
a short burst followed by another stop cannot
pass solely because cumulative frame/callback counts increased.
Recovery preserves the original backend rather than falling back to a silent
null sink during endpoint absence. Last native results remain historical
telemetry; a stale error is not itself proof that current callbacks are unhealthy.

Version 2 records native device state, callback age, restart attempts/results and
the injected native-stop control. The fixed signed candidate and its installed
rerun require their own evidence; earlier audio, WACK and update verdicts do not
extend to the new hash.

### Current-PC administrator launch limitation

The [endpoint retry](INSTALLED_RECOVERY_RETRY_2026-10-18.md) ended before probe
execution. The owner subsequently confirmed that no administrator prompt appeared
for that retry either. Read-only follow-up checks found UAC enabled, secure-desktop
prompting configured, the Application Information service running and valid
signatures on both the launching PowerShell and Windows PowerShell. These checks
do not establish why consent failed. Both HDMI audio devices remain enabled;
at that point no manual probe result had been produced. No further automatic
elevation retry followed that confirmation. The dated retry and partial recovery
records remain unchanged.

To continue on this PC, the owner can open PowerShell as administrator and run
the prepared local `artifacts/retry-recovery-endpoint.ps1` from the repository.
It checks the elevated token, creates a fresh evidence directory and invokes the
same scoped endpoint probe. Review its actual disabled/restored receipt before
resuming installed device-recovery cases. No UAC, signing or power-policy change
is required by this procedure.

The subsequent [owner-launched probe](RECOVERY_ENDPOINT_PROBE_2026-10-18.md)
successfully disabled the endpoint (PnP problem 22) and restored it (problem 0).
This qualifies the scoped host method only. A fresh installed matrix is prepared;
the owner starts `artifacts/start-recovery-host.ps1` from the existing Administrator
PowerShell and leaves it open. That launcher invokes the frozen PowerShell 7
helper directly with the inherited administrator token, avoiding another consent
launch. The separate app/controller remain non-elevated. Earlier installed
attempts expired at baseline and post-recovery sound coordination gates; preserve
their FAILED raw records and completed observations. The optional post-recovery
sound wait now requires `-WaitForCopperStartSound`, so human reply latency does
not prevent the default automated save/reopen checks. Independent physical-sound
evidence remains required for any claim of audible recovery.

The controller removes only its own registration and retains private diagnostic
state externally before uninstalling. The host helper restores a device it
disabled even after a failed case, and never changes signing trust or power
policy. Verify restoration independently. Keep all media, packages, captures,
workspaces and signing material under ignored local artifacts, never in Git.

Fresh WACK and signed-update persistence remain exact-package gates for any new
diagnostic or production binary. This protocol does not extend an earlier hash's
certification, physical-device scope or performance verdict.
