# P1-07 installed sleep/recovery observations — 2026-10-18

**PARTIAL.** CopperStart survives real active and paused S3 transitions on this
PC, and the owner confirms audible game audio afterward. The complete
CopperStart/CopperOS sleep and audio-device recovery matrix is **not qualified**.
The raw attempt remains FAILED after Windows rejected the codec-disable command;
the subsequent elevated endpoint probe was cancelled before execution.
Release budgets remain OPEN. Lightweight owns every guest operation; Legacy
remains unavailable and no guest host-DOS shims are restored.

The [hash-bound record](INSTALLED_RECOVERY_2026-10-18.json) retains the failed
attempt, completed observations, raw power events, app snapshots/traces, human
observations, cleanup and pending work. The
[protocol guide](INSTALLED_RECOVERY.md) describes the diagnostic-only commands
and scoped Windows host helper. Earlier dated evidence is unchanged.

## Candidate and preparation

Signed local diagnostic `1.1.32.0`:

- Identity: `CopperScreen.Test.29d66b7daf334d8cb16743b0e0cfed8b`.
- MSIX SHA-256: `aaefced0ea003e8ea74a9a8a98b1e0de858d2af1bb56c9671043fd8344555e6d`.
- Production Release build: zero warnings/errors; recovery observer excluded.
- Focused runtime/audio controls: nine passed, none skipped or failed.
- All 266 signed payload files and 175 frozen source inputs verified.

The only changes from the `1.1.31.0` packaged source are the diagnostic soak
request/dispatch extension and a new recovery observer. Production runtime/audio,
CPU, chipset, disk and guest assets are unchanged. The observer uses ordinary
runtime/toolbar pause, resume and checkpoint commands; OS power/device actions
run in a separate elevated helper. The app itself is non-elevated and genuinely
installed in WindowsApps. No source/version is published.

The PC supports S3; wake timers were already enabled, and no power plan or signing
trust was changed. The owner confirmed availability to wake/unlock and verify
sound. The frozen standalone ROM, read-only Lotus II image/input sequence and
bounded Lightweight A500 configuration are retained from the earlier hour run.
CopperStart reaches 14,000 runtime fields before these tests begin.

## Completed CopperStart observations

| Observation | Result |
| --- | --- |
| Physical audio baseline | Owner: “Yes, audible.” |
| Active S3/resume | Windows suspend/resume events recorded; guest remains active and frames/audio callbacks resume. |
| Paused S3/resume | Paused state retained; frame number stays exactly `16690 → 16690`. Explicit Resume restores execution and audio delivery. |
| Physical audio after both transitions | Owner: “Yes, audible again.” |

Both transitions retain Kernel-Power events 42 and 107, with effective state 4
on resume. Windows defines that value as S3 in its
[power-state enumeration](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ne-winnt-system_power_state).
Both `powercfg /lastwake` receipts identify the physical power button. Although
a 40-second absolute wake timer was armed, automatic wake or a fixed sleep
duration is not claimed. These are actual operator-assisted S3 observations.

The active request's before/after frame numbers are `16185 → 16348`; these
snapshots span awake work around the actual sleep and do not measure guest work
while suspended. The post-resume snapshot windows record:

| Window | Frame delta | Output callbacks | Missing sample values | Callback exceptions |
| --- | ---: | ---: | ---: | ---: |
| After active S3 | 307 | 394 | 5,706 | 0 |
| After paused S3 and explicit Resume | 363 | 393 | 21,972 | 0 |

These approximately eight-second windows establish functional progression and
resumed nonzero PCM, not zero-underrun or performance-budget acceptance. Actual
snapshot times and all intervening 250-ms samples are retained. Deliberately
paused intervals have separate queue behavior and are not relabelled as steady
active playback. Catch-up/pacing observations remain provisional; no numeric
shipping limit or physical glitch-free result is inferred.

## Device attempt and pending continuation

The pinned `NVIDIA High Definition Audio` codec returned a CIM “Generic failure”
to `Disable-PnpDevice`. No valid disabled-state receipt was produced. The controller
eventually timed out waiting for that receipt and retained its FAILED result.
This does not establish an application recovery failure or successful device
interruption. The owner was notified, and the codec remained/restored enabled.

A separate render-endpoint method probe was prepared. Its administrator launch
reported cancellation before execution; it produced neither a process ID nor a
probe receipt. Whether that cancellation was intentional or a consent timeout
remains awaiting a direct owner reply. No elevation retry or alternative device
mutation followed that cancellation.

The repository harness now reports a rejected host action directly, supports
the specifically named render endpoint and avoids enabling an already enabled
device after a rejected disable request. Explicit case selection and skipped
sleep coverage support scoped continuations without claiming a full matrix.
Those post-failure host/controller changes parse, but their physical behavior
has **not** been qualified. The first attempt's frozen scripts remain unchanged.

Still required:

- Retry the scoped elevated endpoint probe when the operator confirms readiness;
  establish a supported interruption method and verify restoration.
- Qualify actual audio-device loss/recovery and obtain audible confirmation in
  CopperStart; qualify callback recovery independently for the quiet CopperOS CLI.
- Run CopperOS active and paused S3 transitions with native saved-work verification.
- Finish graceful checkpoint/export, process restart and profile/settings/storage
  readback after recovery. The first attempt stopped before these final checks.

## Cleanup and evidence limits

Independent checks confirm the codec and render endpoint are enabled, the host
helper stopped and this run's diagnostic registration is absent. Private test
profiles/settings/logs and raw snapshots remain outside package storage in the
ignored evidence directory. The protected original Lotus II ADF still matches
its qualified hash. No customer package/profile or workspace was replaced.

Local artifacts are retained under
`artifacts/p1-07-recovery-2026-10-18-29d66b7daf334d8cb16743b0e0cfed8b/`.
The record contains 266 hash references, including the original FAILED protocol
record and both successful S3 receipts. This partial continuation does not close
P1-07, establish other device/OS compatibility, freeze release budgets or qualify
the complete sleep/recovery matrix.

The earlier [`1.1.31.0` WACK](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md),
[signed update/persistence](AUDIO_MSIX_VALIDATION_2026-10-18.md) and
[installed hours](INSTALLED_AUDIO_PACING_2026-10-18.md) retain their exact scope.
The new `1.1.32.0` diagnostic package has no fresh WACK or signed-update verdict.
