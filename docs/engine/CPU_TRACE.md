# Copper68k development pin and trace evidence

The current exact CPU pin is **`1.4.1-locality.1`**, a development package
distributed as a GitHub prerelease asset. It retains the trace correction below
and integrates the [validated prefetch-locality optimization](CPU_PREFETCH_LOCALITY_2026-09-22.md).
The source is CopperMod commit
[`b59985f682e342b9b830771c09f617bb547e4116`](https://github.com/ilehtoranta/CopperMod/commit/b59985f682e342b9b830771c09f617bb547e4116),
branch `codex/cpu-prefetch-locality`. The
[current hash manifest](copper68k-locality-development-2026-09-22.json) identifies
the original package and CPU assembly. The package was built before that commit;
its embedded repository revision remains `ada86020ad7a9b298cd7f689c3731d8d779684d1`,
and its release notes retain the unpublished build-time description. The asset
preserves those original bytes. No existing package was overwritten, and this
version is not published on NuGet.org.

## Original trace correction — 2026-09-18

The earlier **`1.4.1-trace.1`** development package was distributed as a GitHub
prerelease asset with owner approval on 2026-09-22. It replaced
`1.4.1-boundary.1` for the Operation Thunderbolt vector-9 defect. No existing
published package was overwritten; this version is not published on NuGet.org.

Source lives in CopperMod, branch `codex/68000-trace-exception`, based on
`713ad6c1bc1bc31efc038996c50faccec31208d6`. The fix is now committed as
[`ada86020ad7a9b298cd7f689c3731d8d779684d1`](https://github.com/ilehtoranta/CopperMod/commit/ada86020ad7a9b298cd7f689c3731d8d779684d1).
The tested package was built from those source changes before committing; its
original bytes are retained rather than repacking the same version. Its embedded
repository metadata therefore still names the base revision.
The [dated hash manifest](copper68k-trace-development-2026-09-18.json) identifies
the source files, package and assemblies. Its `published: false` records the
original 2026-09-18 state; subsequent availability is preserved in
[the CI record](../CI_DEPENDENCY.md). Raw evidence is under ignored
`artifacts/trace-exception-2026-09-18/`. These trace results remain historical
evidence rather than new validation of the locality package.

## Restore on another machine

`NuGet.Config` adds the ignored `artifacts/development-packages` feed and retains
the isolated `artifacts/packages` cache. A fresh machine needs the exact development
package before locked restore. The [bootstrap script](../../scripts/restore-development-package.ps1)
checks the current locality manifest's SHA256, stages the package atomically and rejects an
existing package with different bytes. Both CI jobs run it before locked restore.

The original locality package is available from the
[CopperMod development prerelease](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-1.4.1-locality.1).
Normal bootstrap is:

```powershell
./scripts/restore-development-package.ps1
dotnet restore CopperScreen.slnx --locked-mode
```

For an offline copy of the same package, use:

```powershell
./scripts/restore-development-package.ps1 -PackagePath <path-to-original>/Copper68k.1.4.1-locality.1.nupkg
dotnet restore CopperScreen.slnx --locked-mode
```

The GitHub asset preserves the original package bytes and remains a
development dependency, not a NuGet.org release. Its exact package SHA256 is
`4005DB85A1E7F288376AC65678BD28B29B30EFF811C25EC31EB6BA4533DFA8CE`.
The bootstrap does not regenerate locks, downgrade Copper68k or disable validation.

Rebuilding source is not a drop-in bootstrap: the tested package predates its
source commit, and a repack can differ in metadata and archive bytes. A differing
development package must receive a new version and regenerated locks. Do not silently replace
an existing version. ROMs, media and generated packages remain untracked. There is
no source/project reference or runtime dependency on a sibling checkout.

## Corrected behavior and evidence

SR.T is sampled before execution; vector 9 follows instruction completion with
post-instruction SR/PC stacked. Enabling trace affects the following instruction;
clearing it still traces the current instruction. STOP wakes, RTE restores traced
user execution correctly, completed instruction traps precede trace, and aborted
instructions suppress it. Batch/JIT execution cannot bypass this boundary.

The primary architectural reference is [Motorola MC68000 User's Manual](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf),
sections 6.2.3/6.3.8. NOP plus trace entry is tested at 38 clocks. This is not
certification of every later CPU model's trace timing or T0 branch tracing.

CPU Release suite: **1,499 passed, six optional external-corpus cases unavailable**.
The ROM-free [integration probe](../../scripts/probes/Copper68kTrace/Program.cs)
now passes scalar and batched execution: D0=`$2A`, A7=`$6FFFA`, PC=`$FC0146`,
SR=`$2700`. The old package produced D0=1 and no exception frame.

Native Thunderbolt gets past its trace-based protection. This exposed a second
hang in the audio routine at `$9828`: after disabling DMA, writing AUDxDAT during
the last DMA word failed to enter CPU-fed playback or generate its interrupt.
Paula now retains that queued word through the shared byte loop. Four tests,
covering every channel and both byte phases, failed before this correction and
pass after it. Existing interrupt-sampling tests remain passing.
[Commodore HRM chapter 5](https://bastya.net/AmigaDevDocs/hard_5.html), audio state
machine/figure 5-8, supplies the common-loop and interrupt-transition evidence;
no new physical-hardware timing capture is claimed.

Native coverage and performance acceptance are recorded separately in
[NATIVE_VALIDATION.md](../NATIVE_VALIDATION.md) and [PERFORMANCE.md](PERFORMANCE.md).
