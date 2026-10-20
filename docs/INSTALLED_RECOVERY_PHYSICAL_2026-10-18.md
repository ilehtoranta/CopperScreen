# Installed physical recovery — 2026-10-18

Signed diagnostic `1.1.35.0` **passed the attended physical recovery matrix on
this PC**, with recovery and audio-quality findings retained. Both CopperStart
and CopperOS completed active S3, paused S3, actual HDMI endpoint disable/restore,
checkpoint/export and independent fresh-process persistence checks. Release
qualification and numeric budgets remain **OPEN**. Lightweight is the sole engine;
Legacy remains unavailable.

The [hash-bound record](INSTALLED_RECOVERY_PHYSICAL_2026-10-18.json) contains
227 references to the frozen controller, signed candidate, raw reports, Windows
receipts, screenshots, exports and prior evidence. Thirteen earlier evidence files
were checked unchanged. This continuation preserves the earlier FAILED results.
ROMs, media, signed packages and private workspaces remain ignored local artifacts.

## Candidate and host conditions

MSIX SHA-256:
`47a63049b2367a765dc3b948e24ef225685eeb3b56b0afcb20350e918e2ab762`.
The candidate, its 186 frozen source inputs and the physical controller/helper
hashes verified. Installed binary hashes matched the signed layout. Signature,
payload, build and packaged-control evidence is retained in the preceding
[diagnostic continuation](INSTALLED_RECOVERY_DIAGNOSTICS_2026-10-18.md).
The production recovery algorithm is unchanged from `1.1.34.0`.

The app and controller ran without elevation under a fresh, owned diagnostic
registration. The separately elevated, owner-started helper changed only the
pinned 24G42E HDMI audio endpoint and requested four S3 transitions. HDMI was the
default output for all three Windows roles before and after the run. Windows
suspend/resume receipts establish real S3; both endpoint interruptions reached
problem 22 and restored to OK/problem 0.

Each resume included the previously frozen one-shot `ES_DISPLAY_REQUIRED` request
and three-second settle, as described in the [protocol](INSTALLED_RECOVERY.md).
No power-plan or unlock policy was changed. Panel state and unlocking were not
instrumented. This is attended HDMI recovery evidence; it does not establish
recovery while the display remains off or prove the cause of earlier failures.

## Physical interruption results

Each row reports the eight-second active follow-up, after restoration or explicit
resume. These are functional observations, not approved performance budgets.

| Boot path | Interruption | Frames | Callbacks | Missing sample values |
| --- | --- | ---: | ---: | ---: |
| CopperStart | Active S3 | 401 | 381 | 8060 |
| CopperStart | Paused S3, then explicit resume | 412 | 388 | 0 |
| CopperStart | HDMI disable/restore | 408 | 383 | 3298 |
| CopperOS | Active S3 | 409 | 384 | 0 |
| CopperOS | Paused S3, then explicit resume | 411 | 387 | 0 |
| CopperOS | HDMI disable/restore | 420 | 392 | 0 |

All windows ended with a started native device and fresh callbacks, without a
guest fault or callback exception. CopperStart also advanced nonzero PCM
callbacks. Paused frames remained exactly `16397` for CopperStart and `24574` for
CopperOS across their S3 transitions, until explicit resume. Native stop/restart
and full native close/reopen controls passed for both boot paths before S3.

The owner confirmed CopperStart game/title audio **before sleep and after final
HDMI restoration, before the first recovery window closed**. The retrospective
reply and receipt time are retained separately; exact listening times were not
recorded. This establishes audible recovery, not glitch-free sound. CopperOS uses
the quiet native Shell workload; no audible-output observation is claimed for it.

## Findings retained

Active S3 temporarily invalidated native output in both cases. Logs captured a
failed native stop/start (`HRESULT = -2004287484`) and repeated failure to retrieve
`IMMDevice`. Production recovery eventually succeeded: cumulative counts after
active S3 were 14 attempts/13 reinitializations for CopperStart and 13/12 for
CopperOS, including the earlier injected controls. Subsequent checks remained
healthy without further attempts.

CopperStart's retained timeline shows unavailable output at `15:38:06.984Z`, a
display-on request at `15:38:07.180Z`, and started output with fresh callbacks by
`15:38:09.576Z`. That ordering is an observation, not a controlled demonstration
that display state caused the failure or recovery.

CopperStart also recorded missing sample values in the baseline (`90480`), native
stop/restart (`12558`) and full close/reopen (`17078`) windows. CopperOS recorded
zero in those three windows. Paused silence and interruption totals are retained
in raw counters and are not treated as steady-state audio-quality measurements.
The functional matrix passes its existing progress/state assertions; audio
continuity, host-load attribution and acceptable recovery latency remain open.
The new run does not replace the earlier exact-hash full-hour pacing evidence.

## Persistence, export and cleanup

All four installed processes exited normally with code zero. Both fresh processes
verified their saved disk, and all three profile/settings JSON hashes matched for
each boot path. CopperOS performed native file readback, retained the qualified
image SHA-256
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`, and produced
two valid workspace archives containing the matching 901120-byte `System.adf`
and `export.json`. CopperStart's original media, both exports and reopened disk
retained SHA-256
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.

The controller retained private test state and exports, removed only its owned
diagnostic registration and stopped the helper. HDMI returned to OK and remained
the default for all roles. The owner may close the waiting administrator terminal.

This run adds no archive import/restore, MSIX-update, fresh WACK, production/Store
identity, other-device or new full-hour acceptance result for this package hash.
Those gates, unattended/display-off recovery investigation and audio continuity
remain open under [P1-07](PRODUCTIZATION_ROADMAP.md). No source/package publication
is authorized by this evidence.
