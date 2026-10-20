# Host audio recovery fix — 2026-10-18

**Implemented and signed; installed rerun pending.** A retained `1.1.32.0` run
observed a post-S3 interval with responsive window snapshots but unchanged guest
frame and audio callback counts. The full audio queue stranded the runtime's
indefinite demand wait. Signed diagnostic `1.1.33.0` adds bounded owner-thread
recovery. It has not yet completed installed S3/device or save/reopen validation.
Release budgets remain **OPEN**; Lightweight remains the sole engine and Legacy
remains unavailable.

The [hash-bound record](INSTALLED_RECOVERY_FIX_2026-10-18.json) retains the failed
attempt, implementation delta, regression results, source snapshot, signed
package, payload verification and prepared version-2 run.

## Observed failure and retained earlier attempts

In the failed run, CopperStart's pre-sleep snapshot was frame `14694`, with
`16114` audio callbacks. After Windows resumed, snapshots at `12:18:31.683Z`,
`12:18:32.233Z` and `12:18:40.416Z` all remained at frame `14794` and `16220`
callbacks. The window stayed visible, pause remained false, and no guest fault
or callback exception was reported. Actual S3 suspend/resume events and the
power-button wake source are retained. The controller reported “Guest did not
run after active-sleep”, stopped its helper and removed its own registration.

That older observer did not record native device state. These observations do
not establish the precise Windows/driver/backend cause. A later, separate
preflight found the HDMI endpoint unavailable (`Unknown`, problem 45), then
enabled again. The codec remained enabled. That availability observation is not
silently assigned to the earlier failing interval.

Two other attempts expired at physical-sound coordination gates. One stopped
before any interruption while the owner was away. Another completed both
CopperStart S3 software checks and actual endpoint disable/restore, but timed out
before export/reopen. Its owner replied “Yes, audible again” after cleanup; the
exact listening time is unrecorded. The reply is retained separately and is not
presented as a physical-sound observation for the new candidate. All FAILED raw
records and the original [partial evidence](INSTALLED_RECOVERY_2026-10-18.md)
remain unchanged.

## Implementation and controls

The runtime's full-queue wait now has a 250-ms timeout. Its owner services the
audio output after a second without callbacks, attempting native stop/start at
most once a second. Healthy callback-driven pacing, the queued PCM and guest
machine remain intact. Device lifecycle calls stay outside native callbacks,
following [miniaudio's guidance](https://miniaud.io/docs/manual/index.html).
These retry intervals are implementation choices, not frozen release budgets.

The version-2 diagnostic observer records native device state, callback age,
restart attempts and results. A bounded `audio-stop` command stops the native
sink on the runtime owner; production recovery must restart it. The installed
controller checks that injected control separately from actual Windows S3 and
endpoint interruptions. Physical-sound observations are independent evidence;
the optional `-WaitForCopperStartSound` gate is disabled by default so delayed
replies cannot prevent automatic checkpoint/export/reopen.

Validation completed:

- The new stopped-demand regression failed against the old wait loop, exposing
  the missing recovery service. Its raw failed TRX and assemblies are retained.
- `dotnet build CopperScreen.slnx -c Release`: zero warnings and errors.
- Production assembly: eight focused runtime/queue controls passed; two
  diagnostic-only controls explicitly skipped because instrumentation is absent.
- Packaged diagnostic assembly: all ten focused runtime/queue/telemetry controls
  passed, with zero skips or failures. Its app DLL hash matches the signed payload.
- All 266 payload files, the package signature and the pinned Copper68k DLL hash
  were verified. No native device restart, fixed-candidate guest replay, physical
  sound, new installed hour, MSIX update or WACK result is claimed by these controls.

## Exact candidate and remaining work

Signed local diagnostic package:

- Version: `1.1.33.0`.
- Identity: `CopperScreen.Test.29d66b7daf334d8cb16743b0e0cfed8b`.
- MSIX SHA-256: `e6b68cb397ca5ed1fb6f1961b94f2038061be5ba629688d9ef301b1d7f8f11d4`.

The candidate is built from the frozen `1.1.32.0` source inventory. Only four
files differ: runtime/output recovery and the two diagnostic observers. Engine,
disk, guest metadata, profiles and UI sources are unchanged, excluding unrelated
changes in the live working tree. The embedded minimal-boot source and notices
were recovered byte-for-byte from the earlier signed payload. Restore/cache-path
preparation failures and the packaging continuation are retained separately;
they are not application failures. No package version was published or replaced.

The [version-2 protocol](INSTALLED_RECOVERY.md) and fresh run are prepared under
ignored local artifacts. The owner starts `artifacts/start-recovery-host.ps1`
from an existing Administrator PowerShell because automatic consent launches
previously produced no visible prompt. The helper must see the pinned endpoint
enabled before starting; the controller and app remain non-elevated. No helper
or installed app is running at the time of this record.

Still required: the injected native-stop control, both boot paths' active/paused
S3 and endpoint recovery, native saved-work/export and fresh-process profile/settings
readback, plus a separately recorded sound observation for the fixed candidate.
Fresh exact-package update/WACK evidence and the wider P1-07 matrix remain open.
Earlier [`1.1.31.0` evidence](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md) retains its
original scope. This record does not certify the Store release.
