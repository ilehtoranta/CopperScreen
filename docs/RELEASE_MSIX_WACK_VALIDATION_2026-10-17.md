# P1-07 signed MSIX WACK validation — 2026-10-17

**WACK overall PASS** for the exact signed P1-07 launcher-layout update
`1.1.26.0`. All 13 required checks pass, including DPI awareness and user-account
run level. Ten optional checks pass; the optional blocked-executable reference
scan reports FAIL with the same 24 messages as the P1-06 run. This is the kit's
overall verdict, not an all-checks-pass or Microsoft Store certification claim.
Lightweight remains the active engine; Legacy and host OS shims remain unavailable.

The [machine-readable evidence](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.json)
retains the exact package/report hashes, every check and its original messages,
frozen harness inputs, signature verification and cleanup results. The earlier
[signed P1-07 installed record](RELEASE_MSIX_VALIDATION_2026-10-17.md),
[source qualification](RELEASE_QUALIFICATION_2026-10-17.md) and
[P1-06 WACK evidence](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md) remain unchanged.
The installed record's `wackRun=false` describes that earlier run; this separate
continuation supplies fresh WACK evidence for its exact update package.

## Candidate and run

| Item | Value |
| --- | --- |
| Package | `CopperScreen-1.1.26.0-win-x64.msix` |
| Identity | `CopperScreen.Test.37654a7e355b4dafab059fac783ecb6a` |
| Publisher | `CN=CopperScreen Installed Tests` |
| Package SHA-256 | `4a930e9cd6724983fc682f87a1d7fb4d77cfd8313a95e4552ab6f88e21227a01` |
| XML report SHA-256 | `976010cd591ffd0a12114eabfe1ee061931f89caf5167c4e4edf4596c07897c6` |
| Host | Windows 11 Home x64, build 26200 |
| WACK | `10.0.26100.8249`; Centennial; `PARTIAL_RUN=FALSE` |
| Run | 2026-10-17, 19:48:05–19:49:37, Europe/Helsinki (`+03:00`) |
| Exit / overall | `0` / `PASS` |
| Checks | 24 total: 13 required PASS, 10 optional PASS, 1 optional FAIL |

The package was reused without rebuilding or modifying any payload. Fresh Windows
SDK SignTool `/pa /v` verification passes with zero warnings/errors. All 266
payload files were rechecked against the signed archive and package record.
The already-trusted local test certificate remains unchanged; no key was exported.
Its thumbprint is `510C8C13859A287EFFE367DB25E2BB05E9AA34AB`, expiring
`2026-10-30T21:06:30Z`. The local signature is not timestamped.

WACK ran elevated. The package retains the previously verified PerMonitorV2,
`asInvoker`, no-UI-access executable manifest. The kit explicitly reports
**running without application deployment**. Its static checks are separate from
the exact-package `copperscreen-installed-msix-v4` acceptance: six non-elevated
native CopperOS activations, 18 installed launcher layout observations, unchanged
workspace/profile/settings hashes across restart/update, and backup recovery and
uninstall/reinstall restoration. Those native/layout tests were not repeated in
this continuation.

## Finding and disposition

There are **no required failures or warnings to fix**. The optional
`Blocked executables` scan reports references in bundled .NET/Avalonia/HidSharp
dependencies and the generated apphost. All 24 messages are unchanged from the
P1-06 WACK result and retained verbatim in the new JSON and raw XML. There is no
new `CopperScreen.dll` finding attributable to the launcher-layout observer.

A focused scan of the frozen package's CopperScreen/CopperDisk C# sources finds
no direct `Process.Start`, constructed `Process`, `ShellExecute`, `cmd.exe`,
`reg.exe` or `bash.exe` references. This does not qualify every dependency call
path or establish Windows S-mode compatibility. The static references do not
establish a new product-source defect to correct. The optional finding remains
open for final-package review. No test was disabled and no dependency, runtime
payload or product code was patched or removed to change the verdict.

## Verification and release limits

All **64 MSIX tooling controls** pass; the certification scripts parse without
errors. Candidate identity/version, complete run, required results and report
hash are verified. Final checks confirm no diagnostic registration or old
run-specific private directory remains. All six distinct external original/export
files from the installed P1-07 run still match their hashes. All eight retained
earlier evidence documents match their pre-run hashes.

The frozen harness, raw XML and logs remain under the ignored local directory
`artifacts/p1-07-wack-2026-10-17-8be26fa27bcf409e836ec6f4cf727f8d/`.
No binary correction was needed, so this continuation required no emulator
rebuild or performance measurement. No package or proof asset was published.

Reproduce in an elevated PowerShell 7 shell with the trusted signed candidate,
no pre-existing registration of its identity, and a new evidence directory:

```powershell
./scripts/test-msix-certification.ps1 -PackagePath '<signed P1-07 update.msix>' -OutputDirectory '<new evidence directory>' -TimeoutSeconds 1200
```

This qualifies the recorded diagnostic package. Final production/Store-identity
WACK, admitted shipping assets, Store acquisition/update and clean-machine or
separate-account acceptance remain open. P1-07 still requires physical
HID/audio/sleep/mixed-DPI behavior, manual file-picker/recovery walkthroughs,
installed long sessions and frozen release hardware/budgets. See the
[qualification protocol](RELEASE_QUALIFICATION.md) and
[productization roadmap](PRODUCTIZATION_ROADMAP.md).
