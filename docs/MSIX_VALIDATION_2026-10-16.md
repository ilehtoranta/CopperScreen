# MSIX implementation validation — 2026-10-16

This records the initial [packaging and installed pipeline](MSIX_PACKAGING.md).
P1-02 release acceptance remains open: installed update persistence and WACK
could not run on this host. No package was published or submitted to Store.

## Inputs and artifacts

The builders recorded HEAD `2cf9980fc8ec204b93fc245e0aad9dabe1b0866f` with a dirty
working tree, including this implementation. That commit alone does not reproduce
the binaries; each output's `package-record.json` retains source-input and payload
hashes, with private restore locks beside it. SDK: .NET `10.0.401`; MakeAppx:
Windows SDK `10.0.26100.8249`. Target: self-contained Windows desktop x64,
minimum Windows 11 build 22000. The frozen CPU remains
`Copper68k 1.5.4-qualified-dev.1`; no dependency was repacked or published.

The diagnostic pair shares identity
`CopperScreen.Test.7a4a64a2dd7047c08dba38f507b924e0` and publisher
`CN=CopperScreen Installed Tests`. The normal package uses
`CopperScreen.LocalPackageValidation`, not the final Store identity.

| Artifact under `artifacts/msix-p1-02/` | SHA-256 |
| --- | --- |
| `BaselineFinal/CopperScreen-1.1.3.0-win-x64.msix` | `8DA8D8ADE7D486DBF265D50BFB5402335BA21C6B851E2E5538BC1D1AA64C2799` |
| `Update/CopperScreen-1.1.4.0-win-x64.msix` | `1D2E34CD21A5B887D03656D1DE96AB144EE059B29BB8480FE4473581DC0A56C0` |
| `ProductionShape/CopperScreen-1.1.3.0-win-x64.msix` | `6E7D7C30C5F16B8754954124EDEA32E9ED20BBDAE7E941878287CCB56B4B79E3` |

These are unsigned local artifacts. They contain no supplied CopperStart/CopperOS
assets or commercial ROM/game media. Earlier exploratory outputs are not the
candidate artifacts identified above.

## Results

| Check | Disposition |
| --- | --- |
| Production solution Release build, isolated outputs | PASS; zero warnings/errors. |
| MakeAppx semantic validation/packing | PASS for the baseline, update and normal package; 263 payload files per diagnostic package, 262 in the normal package. |
| Runtime/native files, icons and notices | Required layout inputs present; PNG dimensions checked; symbols retained outside the MSIX. This is not a complete licence audit. |
| Package locks | Private RID locks produced; existing locked versions/content hashes retained and checked-in lock files unchanged. |
| Diagnostic separation | Managed metadata confirms probe type present in diagnostic assemblies and absent from the normal package; markers match package kind. |
| Tooling negative/asset/report controls | 22 passed, including downgrade/identity mismatch rejection, DTD rejection and incomplete/failed WACK report rejection. Synthetic ZIP controls are not deployment evidence. |
| Unpackaged diagnostic EXE | Correctly fails the package-identity requirement and writes a failing report. It cannot stand in for installed validation. |
| Host tests | 220 passed; 6 optional native-media cases skipped. |
| Disk tests | 74 passed; no skips. |
| Signed install/update/uninstall/reinstall | UNAVAILABLE: no signed diagnostic pair trusted for installation on this host. No installed phases passed. |
| Developer registration | UNAVAILABLE: Developer Mode is not enabled. No registration/activation attempted after the preflight failure. |
| Windows App Certification Kit | UNAVAILABLE: unsigned package and no elevated WACK context. No WACK PASS or Store certification claimed. |

The host/disk tests use `artifacts/msix-p1-02/production-check/`. The new code
changes application startup/packaging only; engine execution is unchanged. No
native gameplay or throughput acceptance was performed. Skipped optional cases
remain unavailable native coverage.

Ignored output retains package records, publish/MakeAppx logs, symbols, locks,
host/disk test logs, `unpackaged-negative.json`, and the `InstallUnavailable/`,
`DevelopmentRegistration/` and `WackUnavailable/` records. Prerequisite gaps
produce `UNAVAILABLE` with failing script exit behavior, never a successful run.

## Remaining acceptance

Run the [manual installed workflow](../.github/workflows/msix-installed.yml) on a
dedicated interactive Windows machine with the matching signing certificate
already trusted and the frozen feed available. Complete the actual
seed/update/persistence/uninstall/reinstall sequence and WACK on the identified
package. The activated app must run non-elevated even when WACK requires an
elevated test shell.

Final Partner Center identity, Store acquisition/update, a clean machine without
an SDK, separate Windows accounts, portable-installation migration and qualified
guest assets remain separate release requirements. Any later developer
registration must retain its explicitly limited coverage classification.
