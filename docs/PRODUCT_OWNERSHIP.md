# Complete CopperScreen product ownership — 2026-09-16

This record supersedes the earlier UI-only extraction. The owner's intended
boundary is the whole emulator product, except components needed by CopperMod's
music player. CopperScreen is the canonical development and release repository.

## Ownership

| Component | Canonical owner / dependency |
| --- | --- |
| Desktop application and focused host tests | This repository |
| Lightweight A500 engine and diagnostic timing tests | This repository |
| Headless Lightweight runner and deterministic workload scripts | This repository |
| CopperDisk, disk tests and package scripts | This repository; no music-player consumers were found |
| Lightweight execution plan, issue register and performance harnesses | This repository |
| Copper68k CPU interpreter | CopperMod; exact public package `1.4.1-boundary.1` here |
| Older CopperMod.Amiga used by Cust/AHX, music backends and player | CopperMod; not dependencies of this product |

The application builds the engine and CopperDisk from local projects. Neither
engine nor runner references a sibling CopperMod checkout. Original assembly and
namespace names are retained to avoid an unrelated API rename. The historical
documents are indexed in [the history archive](history/amiga/README.md).
`CopperMod.Amiga/` retains forwarding notes, not the old execution engine.
Current development guidance is in [architecture](engine/ARCHITECTURE.md),
[issues](engine/ISSUES.md) and [performance](engine/PERFORMANCE.md).

Source was imported from CopperMod commit
`0978025c4786cd924ef99fb82bd6508c50888494`. All **76 imported C# files** are
byte-identical to that commit's Git blobs. Existing application C# files were
not edited. Only ownership, project dependencies, package metadata, build/test
infrastructure and documentation change in this migration.

## Verification

- Release production solution: build passed, zero warnings/errors.
- Locked restore: production solution and isolated diagnostic tests passed.
- Engine timing tests: **382 passed**, zero failures/skips.
- CopperDisk tests: **74 passed**, zero failures/skips.
- Focused host tests including both native Lemmings replays: **66 passed**,
  zero failures/skips. Accepted CPU/cycle/pixel/audio fingerprints are unchanged.
- Release headless runner: ten synthetic ROM-loop frames completed, unsupported
  feature status `none`. This is a smoke check, not a performance measurement.
- Local package build: CopperDisk `2.1.1-boundary.2` and engine
  `0.1.0-preview.2` packed successfully with external package dependencies.
- Imported/new PowerShell scripts parse successfully.

Local evidence is under ignored `artifacts/`: `product-build.log`,
`engine-tests.log`, `disk-tests.log`, `product-native-tests.log`,
`product-native-results/`, `runner-smoke.log` and `product-pack-check.log`.
No ROMs, disks, generated binaries or private local artifacts are in this commit.
CI independently builds production and runs diagnostic tests in separate jobs.

The first migration CI run (`35141801928`) correctly rejected stale package
hashes copied from the developer's global cache. Copper68k and CopperPad cached
DLLs also differed from the public artifacts, so local verification was rerun
against a fresh public-only restore, not waived as a signing-only difference.
The corrected locks use public package hashes. `NuGet.Config` now isolates the
repository cache under ignored `artifacts/packages` to prevent recurrence.
At corrected commit `a5e0e22ffa0f9b9a22a6efb8eeaf050753399981`, both
[GitHub CI jobs passed](https://github.com/ilehtoranta/CopperScreen/actions/runs/35142346789).
The public-package local rerun also passed all 382 + 74 + 66 tests, including
the two native replays with zero skips. Local package creation was repeated
successfully after isolating the cache.

No formal FPS gate was rerun or reclassified. This source move does not change
the accepted build's hardware behavior or complete the unfinished performance
objective. Historical G6/G7 outcomes and disclosed hardware uncertainty remain
unchanged.

## CopperMod cleanup boundary

The corresponding CopperMod cleanup removes the migrated engine, runner, disk
and focused native-host test projects, removes emulator-only projects from its
active solution/CI, and leaves forwarding notes for the moved engine records.
Shared host-load policy/support stays there for historical Legacy gates.

The original working tree contains substantial unfinished Legacy/CopperStart,
old host/test and CPU work. Those changes are not imported, deleted or committed
by this migration. Old host/Legacy projects remain a deferred compatibility
workspace, not a second active CopperScreen product; remaining references to
the moved libraries use the already published previews. Further archival or
restoration needs its own scoped change.

The clean **pre-migration** CopperMod player build already fails in
`CopperMod.Cust/CustMachine.cs:488` with CS1729: `KickstartTrapTable` has no
17-argument constructor. This is independent of repository ownership and must
not be represented as a migration regression or silently fixed in this move.
Post-cleanup comparison is recorded in CopperMod's `COPPERSCREEN_MIGRATION.md`.

CopperMod cleanup was committed and pushed as
[`30615e8317a4069e879b585049ed97ffdfecd4fe`](https://github.com/ilehtoranta/CopperMod/commit/30615e8317a4069e879b585049ed97ffdfecd4fe)
only after the destination CI passed. Its staged tree exactly matched the
separately verified cleanup, excluding all unrelated working changes. Of 132
previously dirty tracked paths, 129 remained byte-identical; the other three
retained their prior edits while receiving only the ownership notice or
dependency adaptation. One pre-existing unstaged old-test reference to
Lightweight was adapted to the published package and remains unstaged with
that user's unfinished work. No historical host/CopperStart work was discarded.

## Follow-up: retire the old host workspace

The owner subsequently authorized removing the six old CopperScreen project
trees from CopperMod after archiving their working contents. The
[recovery archive](../archive/legacy-copperscreen-20260916/README.md) supersedes
the earlier statement that these project trees must remain there. Shared
CopperMod.Amiga/Cust/AHX and unrelated CopperStart/CPU work are not removed.
The previously uncommitted phosphor hold/resume regression test is carried into
the active focused host suite; production code is unchanged.
Validation: the migrated test passed; the complete focused host suite passed
65 tests, with its two optional native-media replays explicitly skipped. No
runtime change required a new gameplay or FPS acceptance run.

## Package/release boundary (unchanged)

Published `0.1.0-preview.1` / `2.1.1-boundary.1` packages remain immutable and
available to external or historical consumers. The next source versions above
are **unpublished development versions**; no NuGet or binary release is
authorized by this source migration. `scripts/pack-engine.ps1` creates isolated
local packages without publishing. A later release must use fresh versions.
