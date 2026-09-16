# Retired CopperMod CopperScreen workspace — 2026-09-16

The owner authorized archiving unfinished work and removing the six obsolete
CopperScreen projects from CopperMod. This archive is recovery/history material,
not an active solution, supported engine or runnable standalone distribution.

## Contents and integrity

[source-snapshot.zip](source-snapshot.zip) contains **123 exact working files**:

- All 108 tracked files in CopperScreen, CopperScreen.Tests,
  CopperScreen.Benchmarks, CopperScreen.Headless, CopperScreen.Headless.Cli and
  CopperScreen.Headless.Tests, including the seven modified tracked files.
- The five previously untracked replay/test/planning files in those projects.
- Eight retired Legacy test/benchmark launch scripts, including their uncommitted
  changes, plus two shared host-load support scripts copied for context.
- In addition to those 123 files, `_archive/manifest.json` records each file's
  SHA-256 and the source base commit; `_archive/working-tree.patch` records the
  tracked working changes relative to that commit.

Base: CopperMod `30615e8317a4069e879b585049ed97ffdfecd4fe`.
Archive SHA-256: `C3B019C0B950F0EBCA9A016FDD36C3E3CFA4CCF848BB2E474DE2DC521B0C45A0`.
Every archived source entry was read back and checked against its original hash
before removing anything from CopperMod. No ROM, disk image, generated build,
credential or test-output directory is included. Ignored local files remain
untouched in the original checkout.

## What the unfinished changes cover

- Legacy session-interface adaptation and a 68040 JIT frame-run-ahead correction.
- Legacy gameplay replay/measurement plus validation of its input/protocol.
- Display/bus profiling and vAmigaTS blitter/Copper/interrupt diagnostics.
- Legacy/CopperStart launch tests, updated default routing expectations and a
  phosphor hold/resume regression test.
- Old headless-runner planning, CopperStart coverage and CPU/DMA corpus notes.

The phosphor regression test is now active in
[`CrtPhosphorComposerTests.cs`](../../CopperScreen.Lightweight.Tests/CrtPhosphorComposerTests.cs).
It passed without changing production code. Lightweight routing already has
focused coverage in the current suite. The rest stays archival; it is not
silently imported into the lean engine.

The [CopperStart feature matrix](COPPERSTART_FEATURE_MATRIX.md) is also available
as a readable document for future restoration. Its historical implemented/partial
labels describe the old workspace, not the native-only product.

## Recovery

Extract the ZIP into a **new empty directory** to inspect all preserved files.
The snapshot already contains the working edits: do not apply the included
patch on top of it. To reconstruct the old checkout, use a separate checkout at
the base commit and overlay the snapshot's source files (not `_archive/`).
Other unfinished Legacy/CopperStart changes outside these folders remain in
CopperMod's original working tree and are not included here, so this snapshot
alone is not promised to build. Do not overwrite a dirty checkout.

The current application remains the repository-root `CopperScreen.slnx`.
Archived scripts use historical paths and are not current gate entry points.
No hardware/FPS result was changed by retirement.
