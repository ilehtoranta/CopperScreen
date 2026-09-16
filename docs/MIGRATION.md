# Repository extraction — 2026-09-16

The package boundary was committed and pushed in CopperMod as
`713ad6c1bc1bc31efc038996c50faccec31208d6`. CopperScreen's subtree was split at that
commit into `f776f7915522b1ad360dffc4e697f8d47a9d3790`, preserving 90 application
commits. The standalone layout then moved those files under `CopperScreen/` and
imported the focused test sources from the same source commit.

The application has unconditional exact package references. There is no
source-project fallback. The only project reference is from its focused test
project to the app. CPU/device execution code has not changed for this move.
Historical excluded Legacy implementation files are retained references, not
dependencies of the active host.

Dependency versions: Copper68k `1.4.1-boundary.1`, CopperDisk `2.1.1-boundary.1`,
CopperMod.Amiga.Lightweight `0.1.0-preview.1`. All are locally verified previews;
public-feed publication is pending. Packages were rebuilt from the clean source
commit, not from the original dirty working tree. NuGet package locks are included.

The original repository's app checkout remains intact during migration. Do not
delete it or rewrite existing history as part of activating this repository.
The original worktree also contains unrelated unfinished changes; none were
included in the boundary commit or this export.

The detailed [pre-extraction boundary record](../CopperScreen/PACKAGE_BOUNDARY.md)
is historical evidence: its monorepo paths and commands describe that source
repository. Use this repository's README and solution for standalone builds.

Still pending: package-feed selection/publication, public-feed-only clean restore,
automatic CI activation and any new binary release. Repository creation is not
itself a release of emulator binaries or a new performance acceptance.

## Standalone verification

- All **66 focused tests passed with zero skips**, including both native Lemmings
  replays using the standalone workload's portable disk-2 path. Accepted cycle,
  CPU and pixel/audio fingerprints remain unchanged. Log: `artifacts/tests-native.log`.
- Every application C# source file is byte-identical to the committed boundary
  snapshot. Only project dependencies, layout and build/test infrastructure
  changed for extraction.
- The local self-contained `win-x64` package built successfully. ZIP SHA-256:
  `55097A8C10AD3DD24B1DF0998A1EA9AD5219E05F2EDAA2C14A275389326CB43F`.
  This archive remains local; it is not a published binary release or live UI
  acceptance. The inherited optional Git-metadata command warning remains visible.

Exact library package SHA-256 values from the committed-source local feed:

| Package | SHA-256 |
| --- | --- |
| Copper68k 1.4.1-boundary.1 | `B07AB6585EDCB76FAB3E1A595AC1A96720E4D396B6F1D9897D2CAEDBFB6A1A9B` |
| CopperDisk 2.1.1-boundary.1 | `9161DE96FAE4F9DCDA611955964ED08556C9EAD461DD34B2765DE97BC2F8C2D4` |
| CopperMod.Amiga.Lightweight 0.1.0-preview.1 | `083C7F3C16EBA69343EB7B98C6D670F8A472B7B51829BCE939AAEA2D30DE5F10` |

Publish these exact verified artifacts if retaining these versions. Repacking
different bytes under the same version would invalidate the committed package
content locks. The source feed is retained at the original checkout's
`.codex-tmp/copperscreen-release-packages-20260916/feed`.
