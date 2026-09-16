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
documents retain their `CopperMod.Amiga/` path; that directory here contains
records, not the old execution engine.

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

## Package/release boundary

Published `0.1.0-preview.1` / `2.1.1-boundary.1` packages remain immutable and
available to external or historical consumers. The next source versions above
are **unpublished development versions**; no NuGet or binary release is
authorized by this source migration. `scripts/pack-engine.ps1` creates isolated
local packages without publishing. A later release must use fresh versions.
