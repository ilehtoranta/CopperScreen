# P1-06 signed MSIX WACK validation — 2026-10-17

**WACK overall PASS** on the exact signed P1-06 diagnostic update `1.1.24.0`.
All 13 required checks pass, including DPI awareness. Of 11 optional checks,
ten pass and the blocked-executable scan reports FAIL. This is the kit's actual
overall verdict, not an all-checks-pass or Microsoft Store certification claim.
Lightweight remains the sole guest engine; Legacy and host OS shims remain unavailable.

The [machine-readable evidence](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.json)
retains the package/report hashes, every check and its original messages,
frozen harness inputs, fresh signature verification and cleanup results.
The earlier [P1-06 installed acceptance](MEDIA_MSIX_VALIDATION_2026-10-17.md)
and [WACK/DPI evidence](MSIX_WACK_VALIDATION_2026-10-17.md) remain unchanged.
The earlier P1-06 record's `wackRun=false` describes that earlier run; this
separate continuation supplies WACK evidence for its exact update package.

## Candidate and run

| Item | Value |
| --- | --- |
| Package | `CopperScreen-1.1.24.0-win-x64.msix` |
| Identity | `CopperScreen.Test.8d0a9bf60d3d4b7cbf8cd470ed20d9fc` |
| Publisher | `CN=CopperScreen Installed Tests` |
| Package SHA-256 | `0d11e9de6e032de49d391c819ce847e74023d72f76b85f7474e2192f9eb9f1cc` |
| XML report SHA-256 | `d042260fe4ec2ab68122e6e575eaf5ca360d16775065023c7e7d033f1f76e9d4` |
| Host | Windows 11 Home x64, build 26200 |
| WACK | `10.0.26100.8249`; Centennial; `PARTIAL_RUN=FALSE` |
| Run | 2026-10-17, 18:07:02–18:08:40, Europe/Helsinki (`+03:00`) |
| Exit / overall | `0` / `PASS` |
| Checks | 24 total: 13 required PASS, 10 optional PASS, 1 optional FAIL |

The package was reused without rebuilding or changing any payload bytes. It is
the same update accepted by the six-activation `copperscreen-installed-msix-v3`
media/storage workflow. That earlier record verifies all 266 payload files and
binds loaded native replay binaries to the signed package.

Fresh Windows SDK SignTool `/pa /v` verification passes with zero warnings or
errors. The existing local test certificate, thumbprint
`510C8C13859A287EFFE367DB25E2BB05E9AA34AB`, remains trusted and expires
`2026-10-30T21:06:30Z`. No trust setting changed and no private key was exported.
The local test signature is not timestamped. WACK ran elevated; the app retains
its `asInvoker`, no-UI-access and PerMonitorV2 executable manifest.

## Optional finding and disposition

There are **no required failures or warnings to fix**. The optional
`Blocked executables` check still reports references to process-launch APIs and
command names in the self-contained .NET/Avalonia/HidSharp dependencies and the
generated apphost. Its 24 messages are preserved verbatim in the new JSON record
and raw XML. Compared with the earlier accepted diagnostic `1.1.12.0` report,
the `CopperScreen.dll` / `ReG` match is absent; all remaining messages match.

A fresh focused product-source scan finds no direct `Process.Start`, constructed
`Process`, `ShellExecute`, `cmd.exe`, `reg.exe` or `bash.exe` references in
CopperScreen/CopperDisk C# sources. That does not establish every dependency call
path or Windows S-mode compatibility. No check was disabled, no runtime library
was patched or removed, and no product code or package changed for this result.
The optional finding remains documented for final-package review.

## Verification and release limits

All 49 existing MSIX tooling controls pass, and the certification driver/module
parse without errors. The raw report confirms the exact identity/version and a
complete run with every required check passing. Final checks confirm no test
registration or old run-specific data directory remains. All six distinct
external original/export files from the installed P1-06 run still match their
recorded hashes. Earlier evidence hashes remain unchanged.

WACK explicitly reports **running without application deployment**. Its static
checks are separate from the earlier actual package install/update, non-elevated
windows, native CopperOS writes/readback and backup/recovery/reinstall tests.
Those six native boots were not repeated in this continuation. With no binary
changes, this continuation required no emulator rebuild or new throughput run.

Reproduce against a trusted signed candidate in an elevated PowerShell 7 shell,
with no pre-existing registration of its identity and a new evidence directory:

```powershell
./scripts/test-msix-certification.ps1 -PackagePath '<signed P1-06 update.msix>' -OutputDirectory '<new evidence directory>' -TimeoutSeconds 1200
```

Local packages, frozen harness, XML and logs remain under
`artifacts/p1-06-wack-2026-10-17-53271050dc9f4259a27edf6c5b69a8cd/`;
generated binaries/media stay ignored. No package was published or submitted.

This qualifies the recorded diagnostic package only. Fresh WACK on the final
production package/Store identity, approved shipping assets, Store acquisition
and updates, clean-machine/separate-account acceptance, physical mixed-monitor
DPI checks and native file-picker/recovery-confirmation walkthroughs remain
release gates. There is no new performance or wider guest compatibility claim.
