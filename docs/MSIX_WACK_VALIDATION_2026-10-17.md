# Windows App Certification Kit validation — 2026-10-17

**WACK overall PASS** for both signed x64 packages after correcting the executable
DPI manifest. Each complete report contains 24 checks: all 13 required checks
pass, ten optional checks pass, and the optional blocked-executable scan reports
FAIL. This is WACK's own overall verdict; it is not an all-checks-pass or Microsoft
Store certification claim. Lightweight remains the sole guest engine.

The [machine-readable evidence](MSIX_WACK_VALIDATION_2026-10-17.json) binds exact
package and report hashes, per-check results/messages, frozen harness inputs and
installed results. Earlier [signed installed evidence](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
and [native storage qualification](COPPEROS_STORAGE_2026-10-16.json) remain unchanged.

## Candidates and results

Host: Windows 11 Home x64, build 26200. WACK: `10.0.26100.8249`, Centennial
workflow, `PARTIAL_RUN=FALSE`. The test harness ran elevated; the app's manifest
requests `asInvoker`, with no UI access. The already authorized local test
certificate/publisher was reused; Developer Mode was unchanged.

| Candidate | Overall | Required checks | Optional checks |
| --- | --- | --- | --- |
| Original signed diagnostic `1.1.10.0` | WARNING; harness rejected it | 12 PASS, DPI WARNING | 10 PASS, blocked-executable FAIL |
| Corrected signed diagnostic `1.1.12.0` | PASS | 13 PASS | 10 PASS, blocked-executable FAIL |
| Corrected signed production shape `1.1.13.0` | PASS | 13 PASS | 10 PASS, blocked-executable FAIL |

| Corrected package | SHA-256 |
| --- | --- |
| `CopperScreen-1.1.12.0-win-x64.msix` | `1be278958cc15fe11c6ec3defee5c735e24260af8b93cb5115f2ff9789c4bb1f` |
| `CopperScreen-1.1.13.0-win-x64.msix` | `8a1f0334795a620676b165c5bd77f3ceb56ed66d9ca49d42184d9ae9a6fdd014` |

The diagnostic identity remains
`CopperScreen.Test.0ec4c2e7adf54d4bb7b271cbff72c5f3`. The production-shape identity
is `CopperScreen.Wack.3d3fc2376ef940ab8cb5cc393b86fb98`; this is a local validation
identity, not the eventual Partner Center identity. Both use
`CN=CopperScreen Installed Tests`. Signature verification passed for both.
The production package excludes the installed probe, diagnostic marker and guest
proof media; the diagnostic package includes the exact previously qualified pair.
Neither package is authorized for Store publication by this result.

## Correction and tooling

The original report identified missing PerMonitorV2 awareness in `CopperScreen.exe`.
The executable now embeds `CopperScreen/app.manifest`, declaring PerMonitorV2,
the legacy per-monitor fallback, Windows 10 compatibility and `asInvoker` without
UI access. Both corrected WACK reports mark `DPIAwarenessValidation` PASS.
Microsoft recommends [declaring process DPI awareness in the application manifest](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process).

Packaging extracts the **published executable's** manifest with Windows SDK MT
and validates it before creating the MSIX. Package provenance now hashes the
manifest source; the workflow retains the extracted manifest. This checks the
compiled result rather than assuming the source was embedded.

The WACK harness retains tool/version, report hash and every check's result and
messages. It verifies exact package identity/version, a complete run and passing
required checks, then applies the existing overall-PASS requirement. The original
WARNING report remains rejected evidence. Tooling tests also cover an actual
report shape where an optional FAIL coexists with overall PASS.

## Optional executable-reference finding

The retained optional scan flags process-launch APIs and command-name matches in
the self-contained .NET/Avalonia/HidSharp payload, including the generated apphost.
The packaged executables are `CopperScreen.exe` and the .NET `createdump.exe`.
A focused CopperScreen/CopperDisk source search found no direct
`Process.Start`/`ShellExecute` or `cmd.exe`/`reg.exe`/`bash.exe` references.
This does not prove every third-party call path or establish Windows S-mode
compatibility. Original messages and optional flags remain in the evidence.

Microsoft describes [optional desktop bridge checks](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-desktop-bridge-app-tests)
as informational for Store onboarding. In these actual reports WACK returns
overall PASS while retaining the optional FAIL. No runtime DLLs were patched to
remove scanner matches, and no check was disabled.

## Installed behavior after the correction

Post-fix installed persistence rerun: **PASSED**. Signed baseline `1.1.9.0`
updates to the exact WACK-tested diagnostic `1.1.12.0`. Three native boots verify
creation, process restart and package-update readback with the same workspace,
seed pair and complete image hash. Personal profile, last-machine, recent-session
and bundled preset bytes remain unchanged. All four app activations open a
non-elevated window and exit with code zero. Clean uninstall/reinstall passes;
the final test registration and private data are removed, and the external
synthetic original remains intact.
WACK itself explicitly ran without application deployment, so its static package
checks are separate from real app activation and native guest persistence.

The first installed rerun retained a passing native restart report but failed
the old five-second process-exit allowance. That FAILED run is preserved.
Installed protocol `copperscreen-installed-msix-v2` uses an explicit 30-second
exit bound, retains the process handle and requires exit code zero for every
phase. Real subprocess controls verify zero exit, nonzero exit and timeout.
The accepted rerun's observed post-report exit waits are 2.060, 10.450, 7.384
and 2.843 seconds, each with exit code zero. The 30-second bound accommodates
these observed clean exits; it does not accept a timed-out or failing process.

## Verification and limits

Production Release solution build: PASS, zero warnings/errors. MSIX tooling:
39 controls passed, including executable-manifest failures, WACK completeness and
required-test controls, preserved optional findings and real subprocess exits.

Local raw reports, package records, signed packages, source snapshots and logs
remain under
`artifacts/msix-wack-2026-10-17-3f8c119d3fa344b08144c7fc168a011f/`.
The original report is `Attempt1/`; corrected reports are `DiagnosticDpi/` and
`ProductionDpi/`. Generated binaries/media remain ignored local artifacts.

Final Store identity/submission, Store acquisition/update, clean-machine and
separate-account checks, cleared bundled guest assets/onboarding, broader ABI and
resource budgets, and physical mixed-monitor DPI behavior remain open. No
Legacy restoration, host OS shims or performance acceptance is part of this work.
