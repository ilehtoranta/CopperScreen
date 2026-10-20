# P1-07 signed audio/pacing MSIX WACK validation — 2026-10-18

**WACK overall PASS on the exact signed `1.1.31.0` audio/pacing candidate.**
All **13 required checks pass**, including DPI awareness and user-account run
level. Ten optional checks pass; the optional blocked-executable reference scan
reports FAIL with the same 24 messages as the earlier `1.1.26.0` run. This is the
kit's overall verdict, not an all-checks-pass or Microsoft Store certification
claim. Release budgets remain **OPEN**. Lightweight remains the active engine;
Legacy stays unavailable and no host DOS shims are added.

The [hash-bound record](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.json) retains all
checks/messages, exact package/report hashes, frozen scripts and fixtures,
signature/payload verification, optional-finding analysis, traces and cleanup.
Earlier [signed upgrade/persistence](AUDIO_MSIX_VALIDATION_2026-10-18.md),
[audio/pacing correction and full hours](INSTALLED_AUDIO_PACING_2026-10-18.md) and
[`1.1.26.0` WACK evidence](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) remain
unchanged. The earlier installed/hour reports' `wackRun=false` and then-open WACK
gate describe those earlier runs; this separate continuation supplies the new
exact-package verdict.

## Exact candidate and run

| Item | Value |
| --- | --- |
| Package | `CopperScreen-1.1.31.0-win-x64.msix` |
| Identity | `CopperScreen.Test.af6f00c7f3b04b94ab921e70b62d2229` |
| Publisher | `CN=CopperScreen Installed Tests` |
| Package SHA-256 | `640d6040998d7d5fca19207224a7cd5627f46045dc39f989e74dae24004ae01e` |
| XML report SHA-256 | `5c9f0fabed000bad4b41f30eb9c21ab500117b8576972457b04c7baffcc05138` |
| Host | Windows 11 Home x64, build 26200 |
| WACK | `10.0.26100.8249`; Centennial; `PARTIAL_RUN=FALSE` |
| Start / completion | `2026-10-18T11:19:15.9780326+03:00` / `2026-10-18T11:28:08.7036608+03:00` |
| Exit / overall | `0` / `PASS` |
| Checks | 24 total: 13 required PASS, 10 optional PASS, 1 optional FAIL |

The package is byte-identical to the completed installed-hour candidate and the
accepted signed `1.1.30.0 → 1.1.31.0` update. No payload is rebuilt, repacked or
patched. All 266 payload files match the retained package record. Fresh Windows
SDK SignTool `/pa /v` verification succeeds with zero warnings/errors. The
already-trusted local certificate remains unchanged; no private key is exported.
It expires `2026-10-30T21:06:30Z`; the existing signature is not timestamped.

WACK runs elevated and explicitly reports **tests without application deployment**.
Its complete static report is separate from the matching installed v4 acceptance:
six non-elevated native CopperOS activations, 18 launcher observations,
workspace/profile/settings persistence through restart/update, denied-save
recovery and clean-reinstall backup restoration. The same hash also has the two
completed hour-long sessions with zero software queue starvation/callback errors.
Those installed/native/audio tests are not rerun or relabelled by this WACK run.

## Findings and corrections

There are **no required failures to fix**. The optional `Blocked executables`
scan remains nonpassing. All 24 original messages are unchanged from the prior
WACK report, and there is no new `CopperScreen.dll` finding. The messages and
differences are retained rather than suppressed.

The scan names 18 components: 17 bundled runtime/framework/dependency files are
byte-identical to the earlier package. The remaining component is the generated
`CopperScreen.exe` apphost, whose file version changes from `1.1.26.0` to
`1.1.31.0`; it retains the same `ShellExecuteW` reference finding. This comparison
does not claim that file version is its only binary difference.

All 174 frozen candidate source inputs match their recorded hashes. A focused
CopperScreen/CopperDisk C# scan finds no direct `Process.Start`, constructed
`Process`, `ShellExecute`, `cmd.exe`, `reg.exe` or `bash.exe` references. That scan
does not qualify every dependency call path or establish S-mode compatibility.
These unchanged static references do not establish an actionable audio/pacing
source defect. The optional finding remains open for final-package dependency
review. No check is disabled and no runtime/dependency binary is stripped or
patched to change the verdict.

The first **preparation** attempt omitted fixtures needed by the copied tooling
tests and stopped before WACK started. The corrected frozen copy preserves the
scripts hierarchy and includes the executable manifest, package manifest and
icon. All **64 tooling controls** then pass; scripts parse successfully. Both
preparation attempts remain separately identified. The actual WACK run completes
successfully without a product-code correction.

The signed-file task takes longer than the earlier run but completes normally
within the unchanged 1,200-second timeout. The original kit trace is retained;
validation options, signing trust and revocation policy are not weakened.

## Cleanup and remaining release gates

The harness removes only a registration it owns, if any. Final checks confirm
the candidate identity and its family-private storage are absent. All six
distinct external original/export files from the installed update run still
match their hashes; historical dated evidence remains unchanged.

Raw logs/XML, kit traces and frozen inputs remain under the ignored directory
`artifacts/p1-07-audio-wack-2026-10-18-fe0f7d20f31f47ce801f2aad4d3d41e4/`. No package, signing material or proof media is published or
committed. A final reference/link audit accompanies the report.

This closes fresh local WACK for this **diagnostic package hash**. Final
production/Store-identity WACK, admitted shipping assets, Store acquisition/update,
clean-machine/separate-account validation and the optional dependency finding
remain open. P1-07 also retains its physical audio/video/HID/sleep/mixed-DPI and
manual picker/recovery checks, native Kickstart hour coverage, minimum/reference
hardware and numeric budgets. See the [qualification guide](RELEASE_QUALIFICATION.md)
and [productization roadmap](PRODUCTIZATION_ROADMAP.md).
