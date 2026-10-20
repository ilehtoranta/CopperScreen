# Installed audio recovery diagnostics — 2026-10-18

**Physical recovery remains open.** Signed `1.1.34.0` failed its first active S3
follow-up. Signed diagnostic `1.1.35.0` adds native error logging and a full native
device-close control; both CopperStart and CopperOS pass those software-only
controls, export and fresh-process persistence checks. Its production recovery
algorithm is unchanged from `1.1.34.0`. No fixed S3/device verdict is claimed.
Release budgets remain **OPEN**, Lightweight remains the sole engine, and Legacy
remains unavailable.

The [hash-bound record](INSTALLED_RECOVERY_DIAGNOSTICS_2026-10-18.json) retains
405 source/evidence references. Earlier dated records remain unchanged. ROMs,
media, signed packages and private workspaces remain ignored local artifacts.

## Retained `1.1.34.0` failure

Preflight initially found newly connected WH-CH720N headphones selected for all
Windows playback roles. No app or interruption ran in that condition. After the
owner disconnected them, the pinned 24G42E HDMI endpoint was the only active
playback endpoint and the default for all three roles. The scoped elevated helper
was ready; the app/controller ran without elevation.

The native stopped-sink control passed. Real S3 followed, and Windows recorded
the helper's wake timer as the wake source. After resume, snapshots at
`14:18:20.650Z`, `14:18:20.961Z` and `14:18:29.768Z` stayed at frame `15341` and
`15004` callbacks. Native initialization was unavailable; the last result was
`MA_ERROR` (`-1`). Twenty reopen attempts had occurred by the last snapshot.
Snapshots remained responsive and reported no guest fault. The raw controller
result is **FAILED: Guest did not run after active-sleep**.

The controller stopped the helper, preserved private test state and removed its
own registration. The HDMI endpoint remained enabled. Paused S3, endpoint cycling,
export/reopen and CopperOS were not reached in that run. Physical sound was not
observed. The earlier [native reopen correction](INSTALLED_RECOVERY_REOPEN_2026-10-18.md)
remains an implementation record, not successful installed S3 qualification.

## Discriminating controls and observer changes

Small native probes used the pinned Miniaudio-CS `1.0.4` library, reporting native
miniaudio `0.11.22`. Fresh WASAPI opens succeeded with/without explicit COM
initialization and with null/explicit context configuration. Repeated reuse of
one device from an STA initializer to an MTA owner also succeeded, including the
app's exact 1024-frame period. These probes did not reproduce the installed S3
failure; they do not establish that every COM/driver condition is safe. No
production COM change was made from that unproven hypothesis.

Version 3 of the [recovery protocol](INSTALLED_RECOVERY.md) adds up to 64 native
warning/error messages and a diagnostic `audio-close` command. The runtime owner
uninitializes the device; production recovery must recreate it while retaining
the queued PCM and guest. Normal production builds omit this observer and command.
The new `-NativeControlsOnly` controller mode requests no elevated helper, sleep
or endpoint change and explicitly declares incomplete physical coverage.

## Exact signed `1.1.35.0` results

Two separate installed software-only runs used normal windows and fresh owned
state, with two process activations per boot path. All four processes exited
normally with code zero. Each run passed its active baseline, native stop/restart,
full native close/reopen, checkpoint/export and independent fresh-process readback.

Eight-second control windows are observations, not approved release budgets:

| Boot path | Injected native control | Frames advanced | Callbacks | Missing sample values |
| --- | --- | ---: | ---: | ---: |
| CopperStart | Stop/restart | 385 | 365 | 0 |
| CopperStart | Close/reopen | 377 | 357 | 3514 |
| CopperOS | Stop/restart | 391 | 367 | 0 |
| CopperOS | Close/reopen | 377 | 354 | 0 |

Every window ended with a started native device and fresh callbacks. Both reopen
controls counted a real production reinitialization. No callback exception or
native warning/error was recorded. CopperStart's 3514 missing values remain a
finding; these results do not qualify glitch-free output or physical audibility.
The CopperOS CLI is a quiet workload.

CopperStart's original image, both exports and fresh-process disk retained SHA-256
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
CopperOS completed native storage verification before export and after reopening;
the saved image and both exported workspace archives contain the qualified image
SHA-256 `e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
Three profile/settings JSON files matched across each boot path's process restart.
This validates archive creation/content and restart readback, not a new archive
import/restore or MSIX-update test. Private state and exports were retained before
removing each owned diagnostic registration; HDMI remained enabled.

The production solution build passed with zero warnings/errors. All ten focused
runtime/queue/telemetry controls passed against the exact packaged diagnostic
assemblies, with zero skips/failures. Signature and all 266 payload files verified;
the pinned Copper68k DLL hash is unchanged. Of the 175 frozen app/engine/disk inputs,
only four diagnostic observer files differ from the earlier candidate; 171 inputs
are unchanged, including engine/disk sources. Explicit isolated inventories provide
source attribution; the packaging tool's generated parent-Git list does not.

Diagnostic MSIX SHA-256:
`47a63049b2367a765dc3b948e24ef225685eeb3b56b0afcb20350e918e2ab762`.
Identity: `CopperScreen.Test.29d66b7daf334d8cb16743b0e0cfed8b`.
No package was published or existing signed version overwritten.

## Prepared physical continuation and limits

The failed timer wake suggests a host condition worth distinguishing:
[Windows does not automatically turn on the display after unattended wake](https://learn.microsoft.com/en-us/windows/win32/power/system-wake-up-events).
The earlier run did not capture panel state, so this is a possible explanation,
not a proven cause of its initialization failure. The prepared version-3 physical
controller makes a one-shot `ES_DISPLAY_REQUIRED` request after each actual S3
receipt, records the request and allows three seconds for display/link setup
before the unchanged follow-up window. It changes no power plan or unlock policy.
The [API request](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)
succeeded in a separate control; that does not confirm a panel is physically on
or that the session is unlocked. The physical and software-only controller hashes
are retained separately.

The fresh two-case physical run still needs the owner-started administrator helper
and wake/unlock availability. Failure cleanup stopped the earlier helper; previous
automatic consent launches produced no visible prompt. The prepared launcher is
`artifacts/start-recovery-host.ps1`. No new physical test has started at this record.
Delayed sound replies do not block automated checkpoint/export/reopen.

Still open: active and paused S3 plus actual HDMI disable/restore for both boot
paths on the exact candidate, physical sound observation, fresh update/WACK
evidence and the wider P1-07 release matrix. Earlier
[`1.1.31.0` hour/update/WACK evidence](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md)
retains its original scope; it is not inherited by the new hash.
