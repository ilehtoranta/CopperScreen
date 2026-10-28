# Copper68k 1.5.4 release and consumer update

Publication was authorized on 2026-10-22. The release workflow successfully
uploaded the package and symbols on 2026-10-23 (Europe/Helsinki). Published
[Copper68k 1.5.4](https://www.nuget.org/packages/Copper68k/1.5.4) replaces the
private qualification feed. Public-package consumer checks are complete; the
[machine-readable record](COPPER68K_1_5_4_RELEASE_2026-10-22.json) retains identities
and results.

## Source and publishing boundary

[CopperMod PR #25](https://github.com/ilehtoranta/CopperMod/pull/25) prepares
stable `1.5.4` at commit `535e9ecfc543931b46f7c332d9593805382bd7cf`, based on
merged mainline `ca49fe8d5ccf063ff96502f3dac2c39a3b43cba5`. It changes package
metadata, documentation and explicit release-tag workflow gates. CPU execution
source is unchanged from that mainline. The earlier dirty sibling checkout is
preserved; release preparation uses an isolated worktree.

The source PR passed 5,417 CPU tests with 109 optional/deferred checks unavailable,
the required 86,663,530-case architectural gate and all 18 AHX tests. It was merged
as `f4d461bb2dc9e16babf9ca63dcf474d0f5a8ceb0`. The tagged publishing workflow
independently reruns its required gates before upload.

This retains the qualified integer implementation, generic EORI postincrement,
context-copy ownership correction and the already merged memory BTST final
prefetch correction. The latter completes required fetches after waited reads
and publishes Z before that fetch. Existing published packages and historical
development packages are unchanged.

The `copper68k-v1.5.4` tag runs
[trusted publishing](https://github.com/ilehtoranta/CopperMod/actions/runs/39845506952).
The existing OIDC policy/workflow filename is retained. CPU tests, required
architectural reports, AHX tests and package-content checks run before NuGet
login and package/symbol upload. Only explicit unchanged-content retry tags are
accepted; changed package content requires a new version.

## Local review-package checks

- EORI/BTST-focused CPU selection: 86 passed, two unrelated optional cases
  unavailable. AHX: 18 passed, zero skipped.
- Release `.nupkg`/`.snupkg` contents pass the established packing validator.
  Public API baseline validation against published `1.5.3` passes.
- Current CopperScreen production Release build: zero warnings/errors.
- Separate engine diagnostics: 1,272 passed, seven optional native groups
  unavailable. Host: 360 passed, 42 optional checks unavailable. Disk: 93 passed.
- Four explicit native Kickstart 1.3 CPU cases pass: 68010 batch/scalar and
  68040 interpreter/JIT, including positive compiled-instruction evidence.
- Detroit completes all 34,000 fields in normal/scalar modes without an
  unsupported stop. All 1,027 capture files match. All 28 retained qualified
  milestone files and the final cycle/CPU/hardware/output fingerprints are
  unchanged. Inspected screens show January's report and the return to February
  management with the expected 55,061-pound balance. This is bounded first-month
  gameplay; saves and later gameplay remain unverified.
- Desktop: 65 passed, three failed, three optional checks unavailable. Both
  `FocusLossAndReleaseCommandClearHeldJoystickWithoutAKeyUp` cases and
  `ResizingMovesTheSameControlsAndKeepsUtilitiesBelowDisks(scale: 2)` time out.
  The identical three failures reproduce with `1.5.4-qualified-dev.1` in a
  frozen copy of the same consumer source (two other selected cases pass).
  They are retained baseline failures, not counted as successful validation.

The initial Detroit replay attempts used the obsolete local media directory and
failed before guest execution. Those logs remain separate from the subsequent
replay, which verifies the original ZIP hashes at the relocated `C:/Data` paths.
Native ROMs, media, captures, package files, frozen comparison source and logs
remain ignored under `artifacts/copper68k-release-20261022/`.

## Public package and scope

Both PR and publishing workflows pass 5,417 CPU tests (109 optional/deferred
checks unavailable), the required 86,663,530 cases in 783 reporting batches and
all 18 AHX tests. The approved-coverage gate passes; `roadmapComplete=false`
continues to distinguish that scope from exhaustive hardware qualification.

Hosted CPU execution took longer than the preceding CI run. An additional local
duplicate was started to investigate that delay, then manually stopped after
both complete hosted gates and exact public-package consumer checks passed. Its
partial output and aborted test-host result are retained and are not counted as
passing coverage. Neither hosted gate nor its required case set was reduced.

The downloaded public package has a valid NuGet.org repository signature. Every
unsigned payload entry matches the retained GitHub publishing artifact. A fresh
restore with an empty package cache and NuGet.org as its only source succeeds;
its metadata identifies that public source and package content hash. Standard
production restore, locked restore and Release build also pass (zero warnings or
errors) with the private feed removed and defaults pinned to `1.5.4`.

The public CPU DLL SHA-256 is
`ff3a73b2dd2a62329be1fcdad1eab8ca3a62d95f73bcfc03a4b87bd25955837a`.
It matches the production runner and fresh public cache. It differs from the
local review build (`d2f58fdb48ba2e2fc5a0edbbe800c808691adaaec6962a0c33467ff2450f9095`);
review-package gameplay is therefore not relabeled as exact public-binary evidence.
Independent public-binary consumer checks use their own logs and captures.

The public-binary build passes 1,272 engine tests (seven optional native groups
unavailable), 360 host tests (42 optional checks unavailable), all 93 disk tests
and all four explicitly enabled native CPU cases. Desktop checks record 66
passes, two input-focus timeouts and three optional skips. Both failing
`FocusLossAndReleaseCommandClearHeldJoystickWithoutAKeyUp` identities also fail
with the old candidate; there are no new desktop failure identities. The layout
case that timed out in the review/baseline runs passes in the public run.

Both exact public-binary Detroit runs complete 34,000 fields with exit code zero
and no unsupported stop. All 1,027 normal/scalar capture files match. They also
match all 1,027 local-review captures and retain the qualified candidate's final
cycle/CPU/hardware/output fingerprints and all 28 inspected milestone identities.
The public package therefore has direct first-month gameplay evidence; it is
not inferred from the review build. Saves and later gameplay remain unverified.

The required known integer semantic scope is retained. Five unresolved
qualification areas remain deferred and visible; they are never counted as
passing. Physical CPU timing, FPU, enabled-MMU, general OS compatibility and
performance acceptance are not claimed. Earlier native evidence remains bound
to its actual package and input identities.
