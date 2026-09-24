# CI development-dependency bootstrap — 2026-09-22

> **Superseded on 2026-09-24:** Copper68k `1.4.1` is now the stable NuGet.org
> dependency, pinned exactly in project files and lockfiles. CI restores directly
> from NuGet.org; the development-feed bootstrap below records the earlier
> prerelease transition and remains historical evidence. Current CPU guidance is
> in [CPU_TRACE.md](engine/CPU_TRACE.md).

## Development dependency at the 2026-09-22 milestone (historical)

The production engine, runner and desktop now pin **Copper68k
`1.4.1-locality.1`**. Both CI jobs use the same
[bootstrap script](../scripts/restore-development-package.ps1), now bound to the
[locality hash manifest](engine/copper68k-locality-development-2026-09-22.json).
It obtains the original 315,868-byte package from the
[CopperMod development prerelease](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-1.4.1-locality.1)
and verifies SHA256
`4005DB85A1E7F288376AC65678BD28B29B30EFF811C25EC31EB6BA4533DFA8CE`
before staging it. Offline bootstrap requires the same original bytes.
An anonymous download from the published release matches that SHA256.

This pin retains the trace correction and integrates the validated CPU-locality
optimization. Its source commit is `b59985f682e342b9b830771c09f617bb547e4116`;
the package was built before that commit, so embedded metadata still records
the base `ada86020ad7a9b298cd7f689c3731d8d779684d1`. The release preserves the
package without repacking it. It is a GitHub development prerelease, not a
NuGet.org publication. See the
[current integration evidence](engine/CPU_PREFETCH_LOCALITY_2026-09-22.md) and
[package and restore history](engine/CPU_TRACE.md).

The sections below preserve the original trace-package bootstrap investigation
and its hosted CI result. They do not claim hosted validation of the newer pin.

## Original failure and cause

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

## Original correction

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

## Original local verification

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
Remote execution subsequently passed as recorded below; the initial local
allocation failures remain preserved as separate evidence.

Bootstrap checks cover the valid package, repeat invocation, altered input,
preservation of a mismatched existing file, the download branch with a simulated
response, and download-failure cleanup. Evidence is under ignored
`artifacts/ci-investigation/`. No native media replay or performance measurement
was performed for this CI-only change.

## Original hosted verification

[CI run 35692065083](https://github.com/ilehtoranta/CopperScreen/actions/runs/35692065083)
on commit `ea12edbc1abd5094e4dd9b63f0e186615c056977` passed both jobs on
2026-09-22. Each clean Windows runner downloaded the original package and
verified the expected SHA256 before successful locked restore.

- Production Release build and synthetic runner smoke: passed.
- Host tests: **92 passed**, **three optional native cases unavailable**.
- CopperDisk: **74 passed**.
- Isolated engine diagnostics: **669 passed**, no skips or failures.

No CI rerun, assertion relaxation, dependency downgrade or engine change was
needed. This verifies the dependency-delivery correction; it is not new native
gameplay or performance evidence. Downloaded job logs are retained locally as
`artifacts/ci-job-106630963243.log` and `artifacts/ci-job-106630963269.log`.
