# P1-07 endpoint probe retry — 2026-10-18

**UNAVAILABLE: the elevated probe did not execute.** The owner reported that no
administrator prompt was visible for the previous launch and explicitly requested
“Retry now.” The retry again returned Windows error “Käyttäjä peruutti toiminnon”
(the operation was cancelled), without creating a probe process or result receipt.
The cause of the consent failure is unresolved; it is not attributed to an
intentional owner cancellation or an application recovery failure.

The [hash-bound retry record](INSTALLED_RECOVERY_RETRY_2026-10-18.json) retains the
authorization, exact helper/configuration, failed launch and independent device
checks. Windows consent was observed in the interactive session while the launch
waited. The endpoint remained enabled throughout the observed checks. No second
launch was started after this retry failed.

Both the NVIDIA HDMI codec and its named `24G42E` render endpoint are enabled
(`Status=OK`), and the diagnostic package registration remains absent. The
probe produced no passing disabled/restored receipt. No new installed session,
sleep transition, checkpoint, export or reopen test ran during this retry.

The original [partial recovery evidence](INSTALLED_RECOVERY_2026-10-18.md) and its
JSON record remain byte-identical. Its two CopperStart S3 observations remain
valid within their recorded scope. Actual device recovery, CopperOS active and
paused S3, and final recovery/save/reopen qualification remain open. Release
budgets remain **OPEN**; Lightweight remains the only engine and Legacy remains
unavailable. No application code, package, signing trust or power policy changed.

The next step is to resolve the Windows consent launch or run the same scoped
probe from an owner-opened Administrator PowerShell. The local convenience script
`artifacts/retry-recovery-endpoint.ps1` checks the administrator token and invokes
the retained probe in a new output directory. It does not bypass Windows consent.
Only a verified disable/restore receipt permits the installed device-recovery
tests to continue. Other devices and default-device switching remain outside this
probe's scope; the [recovery protocol](INSTALLED_RECOVERY.md) still applies.
