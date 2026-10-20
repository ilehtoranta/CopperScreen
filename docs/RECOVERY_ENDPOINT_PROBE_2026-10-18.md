# HDMI endpoint interruption method — 2026-10-18

**PASS for the scoped Windows host method. Application recovery remains
unqualified.** The owner ran the prepared endpoint probe from Administrator
PowerShell after two automatic consent launches ended before execution. The
probe successfully disabled and restored the named HDMI render endpoint using
`Disable-PnpDevice` / `Enable-PnpDevice`; no native-command fallback was needed.

The [hash-bound record](RECOVERY_ENDPOINT_PROBE_2026-10-18.json) retains the exact
probe, configuration, raw receipt and independent restoration check. The endpoint
is `24G42E (NVIDIA High Definition Audio)`, PnP class `AudioEndpoint`, instance
`SWD\MMDEVAPI\{0.0.0.00000000}.{D9AADFBA-92DF-4F27-B98D-739A08DB7421}`.

| Observation | Result |
| --- | --- |
| Before | Enabled: `Status=OK`. |
| During | Disabled: `Status=Error`, PnP problem `22`. |
| After | Restored: `Status=OK`, problem `0`. |
| Recorded probe interval | `2026-10-18T10:36:20.1134530Z` to `10:36:24.8715835Z`. |
| Independent follow-up | Both endpoint and NVIDIA HDMI codec enabled; diagnostic registration absent. |

The disabled interval was deliberately brief. This proves a usable host
interruption method on this PC; no installed application was running for the
probe, and it establishes no callback, physical sound, guest continuity, save,
restart, update or sleep/resume result. The [earlier partial recovery evidence](INSTALLED_RECOVERY_2026-10-18.md)
and [failed consent retry](INSTALLED_RECOVERY_RETRY_2026-10-18.md) remain unchanged.

A fresh full CopperStart/CopperOS recovery run is prepared using the exact signed
diagnostic `1.1.32.0` MSIX (`aaefced0ea003e8ea74a9a8a98b1e0de858d2af1bb56c9671043fd8344555e6d`).
The separate helper now requires an actual PnP problem-22 state before issuing
its disabled-state receipt. The controller keeps the recovered CopperStart
window open for a direct physical-sound observation. These harness changes parse;
their full installed matrix has not run at the time of this record.

The owner must start the prepared bounded helper from the existing Administrator
PowerShell using `artifacts/start-recovery-host.ps1` and leave that terminal open.
The controller and installed app run without elevation. The helper supports only
the pinned audio endpoint, sleep and stop commands; it restores its endpoint on
cleanup and never changes a GPU/display device, signing trust or power policy.
The [recovery protocol](INSTALLED_RECOVERY.md) defines the remaining physical
transitions and persistence checks. No further automatic elevation retry was
attempted. Release budgets remain **OPEN**; Lightweight remains the only engine,
and Legacy remains unavailable.
