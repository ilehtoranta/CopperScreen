# P1-07 signed 1.1.43.0 MSIX WACK validation — 2026-10-19

**Fresh WACK overall PASS on the unchanged signed `1.1.43.0` candidate.** All
**13 required checks PASS**, including DPI awareness. Ten optional checks pass,
including user-account run level. The optional blocked-executable scan remains **FAIL**
with the same 24 messages as signed `1.1.31.0`; this is not an all-checks-pass or
Microsoft Store certification claim. Release budgets remain **OPEN**. Lightweight
remains the active engine; Legacy is unavailable and no host DOS shims are added.

The [hash-bound record](AUDIO_MSIX_WACK_VALIDATION_2026-10-19.json) retains every
test and message, package/report hashes, frozen tooling, signature and payload
verification, optional-finding comparison, kit traces and cleanup. Earlier dated
reports retain their original verdicts and scope.

## Exact candidate and result

| Item | Value |
| --- | --- |
| Package | `CopperScreen-1.1.43.0-win-x64.msix` |
| Identity | `CopperScreen.Test.40a6779884774716bfe9998be653a665` |
| Publisher | `CN=CopperScreen Installed Tests` |
| Package SHA-256 | `7a34dc3fbe845959d1ea94f1e85e5b61031e55064797a9f73a42d098319cad02` |
| XML report SHA-256 | `4715fbf55edc68f647d1099fb52723aa651bec58c32cff292532b57a011f82c4` |
| Host | Windows 11 Home x64, build 26200 |
| WACK | `10.0.26100.8249`; Centennial; `PARTIAL_RUN=FALSE` |
| Start / completion, Europe/Helsinki | `2026-10-19T23:17:04.3826023+03:00` / `2026-10-19T23:19:55.6381202+03:00` |
| Exit / overall | `0` / `PASS` |
| Checks | 24 total: 13 required PASS, 10 optional PASS, 1 optional FAIL |

The original signed package is neither rebuilt, repacked nor re-signed. Its hash
matches both the [audio continuation](AUDIO_WARMUP_REFILL_2026-10-19.md) and the
accepted [signed `1.1.42.0 → 1.1.43.0` update](AUDIO_MSIX_VALIDATION_2026-10-19.md).
All 266 payload files and 190 frozen source inputs match their recorded hashes.
Fresh SDK SignTool `/pa /v` verification succeeds with zero warnings or errors.
The existing trusted local certificate is unchanged; no private key is exported.
Its expiry is `2026-10-30T21:06:30Z`, and the existing signature is not timestamped.
All 64 synthetic packaging-tool controls pass; they do not substitute for
installed-app coverage.

WACK runs elevated and explicitly reports **tests without application
deployment**. This complete static report is separate from the matching installed
v4 acceptance: six non-elevated native CopperOS activations, 18 launcher
observations, restart/update persistence for workspace/profile/settings,
denied-save recovery and clean-reinstall backup restoration. Those phases are
not rerun or relabelled by WACK. The audio continuation supplies its bounded
native observations; no older full-hour or physical recovery verdict transfers
to this package merely because WACK passes.

## Findings and corrections

There are **no required failures to fix**, and no product-code correction is
needed for this run. The optional `Blocked executables` finding remains open.
All 24 messages match the previous [signed audio WACK report](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md),
with no new `CopperScreen.dll` finding. All tests remain enabled.

Of the 18 named components, 17 bundled framework/runtime/dependency files are
byte-identical to signed `1.1.31.0`. The generated `CopperScreen.exe` apphost
changes with the build and retains its `ShellExecuteW` reference finding. This
comparison does not claim that version metadata is its only binary difference.
The verified frozen CopperScreen/CopperDisk C# source has no direct
`Process.Start`, constructed `Process`, `ShellExecute`, `cmd.exe`, `reg.exe` or
`bash.exe` references under the retained focused scan. This does not qualify
every dependency call path or establish S-mode compatibility. The unchanged
static references do not identify an actionable defect in the audio correction;
final-package dependency review remains open. No runtime binary is stripped or
patched to alter the result.

## Evidence and remaining gates

Raw XML/logs, frozen inputs, optional comparison and current-run kit traces are
retained under the ignored directory
`artifacts/p1-07-audio-wack-2026-10-19-96abb69748ac47a68c8fffe3d2e9e712/`.
The harness cleans up only a registration it owns, if any. Final checks confirm
the candidate identity and family-private storage are absent. Prior audio and
installed-persistence evidence references are checked again, including retained
external originals and workspace exports. No package, proof media, signing
material or credentials are published or committed.

This closes fresh local WACK for this diagnostic package hash. Final
production/Store-identity WACK, admitted shipping assets, Store acquisition and
updates, clean-machine/separate-account validation and the optional dependency
finding remain open. P1-07 still needs exact-candidate full hours and physical
audio/display/S3/PnP/HID/mixed-DPI/manual workflow coverage. Minimum/reference
hardware, numeric performance and audio latency budgets remain **OPEN**; this
run adds no formal performance acceptance. See the
[qualification guide](RELEASE_QUALIFICATION.md), [packaging guide](MSIX_PACKAGING.md)
and [roadmap](PRODUCTIZATION_ROADMAP.md).
