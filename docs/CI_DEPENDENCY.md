# CI development-dependency bootstrap — 2026-09-22

## Failure and cause

Both jobs in [CI run 35686134348](https://github.com/ilehtoranta/CopperScreen/actions/runs/35686134348)
fail during restore, before compilation or test execution. The logs report
`NU1301`: the local `artifacts/development-packages` source does not exist.
That ignored feed contains the exact development Copper68k `1.4.1-trace.1`
dependency on the development machine. Neither the feed nor the package is
present in a clean Actions checkout. Creating an empty directory alone would
still leave the pinned package unavailable. NuGet.org currently provides
`1.4.1-boundary.1`, but not the trace fix; downgrading would lose validated
Operation Thunderbolt behavior.

The existing [trace record](engine/CPU_TRACE.md) already documented this clean-CI
limitation. README's assertion that all packages restore from NuGet.org was
incomplete and is now corrected. This is a dependency-delivery defect, not an
observed engine compilation failure.

## Correction

Both jobs now invoke [restore-development-package.ps1](../scripts/restore-development-package.ps1)
before locked restore. It obtains the original package from the
[CopperMod GitHub development prerelease](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-1.4.1-trace.1), checks its recorded
SHA256 before placing it in the local feed, and refuses an existing file with
different bytes. An offline `-PackagePath` option performs the same verification.
Failed downloads and hash mismatches leave no staged package. No package lock,
CPU version, engine implementation or performance protocol changes.

The exact 315,281-byte package has SHA256
`8C2B30CD902AD9F3A8C7D1938BC78DE3E4D12F9C1B59A2F56629931AF5E72747`.
It contains package metadata, README, icon, CPU DLL and XML documentation; no
ROM/media or credentials. Its corrected source is CopperMod commit
`ada86020ad7a9b298cd7f689c3731d8d779684d1`; embedded package metadata retains the
pre-commit base revision, as previously documented. Uploading these original
bytes avoids repacking an existing version and preserves the locked content hash.

With explicit owner approval on 2026-09-22, the original package was uploaded
as a public GitHub development prerelease asset. A fresh unauthenticated download
through the bootstrap script matches the recorded SHA256. It is not a NuGet.org
publication, and no existing package was overwritten.

## Local verification

A fresh archive of `dfe9709` with an empty package cache reproduces NU1301.
After preparing the exact package, both production and isolated diagnostic
locked restores succeed without modifying their lock files. The production
Release build passes with zero warnings/errors, host tests pass **92** with
**three optional native cases unavailable**, CopperDisk passes **74**, and the
synthetic runner smoke completes without unsupported features.

The first diagnostics run, concurrent with the production build, reports two
allocation-assertion failures (Paula serial: 3,808 bytes; floppy controls: 8,168).
An unchanged isolated repeat passes **669/669**. Both logs are retained; the
initial failures are not relabeled as passes. Their intermittent cause remains
unresolved. No assertion was weakened and no automatic test retry was added to CI.
Remote execution after dependency delivery is still required.

Bootstrap checks cover the valid package, repeat invocation, altered input,
preservation of a mismatched existing file, the download branch with a simulated
response, and download-failure cleanup. Evidence is under ignored
`artifacts/ci-investigation/`. No native media replay or performance measurement
was performed for this CI-only change.
