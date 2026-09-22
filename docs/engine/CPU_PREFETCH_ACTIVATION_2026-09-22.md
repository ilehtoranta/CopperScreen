# Copper68k locality package activation — 2026-09-22

CopperScreen's production engine, runner and desktop now use the optimized
**Copper68k `1.4.1-locality.1`** package. The owner explicitly authorized package
publication and the pin update after the source/evidence commits.

## Distribution and restore

The existing package was published unchanged as a
[CopperMod GitHub development prerelease](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-1.4.1-locality.1)
targeting source commit `b59985f682e342b9b830771c09f617bb547e4116`.
This is not a NuGet.org publication. Older package versions and release assets
were not overwritten.

- Package SHA256: `4005DB85A1E7F288376AC65678BD28B29B30EFF811C25EC31EB6BA4533DFA8CE`.
- CPU DLL SHA256: `982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F`.
- Package size: 315,868 bytes.

An anonymous download from the public asset URL matches the original package.
The existing bootstrap now reads the
[locality manifest](copper68k-locality-development-2026-09-22.json) and checks its
SHA256 before adding it to the ignored local feed. All five affected lockfiles
were regenerated; only Copper68k's version, hash and project dependency bounds
changed. Production and isolated diagnostic locked restores pass. No sibling
project reference, ROM, media or generated package was added to source control.

The package was built before its source commit and retains the recorded earlier
repository revision and unpublished build-time release notes. Publication did
not rebuild or modify those bytes.

## Executable comparison and correctness

The [read-only assembly comparison helper](../../scripts/probes/AssemblyExecutionComparison/README.md)
compared actual metadata and method bodies rather than inferring binary equality
from source text. It is outside the production solution.

| Comparison | Result |
|---|---|
| Benchmarked CPU DLL → packaged CPU DLL | 28,186 records; no executable differences; all 4,357 methods and 4,250 bodies match |
| Frozen benchmark engine → rebuilt production engine | 7,238 records; no executable differences; all 1,222 methods, 1,206 bodies and four static initializer blobs match |
| Negative control: old CPU → optimized benchmark CPU | Exactly three changed inlining flags detected; identical method IL alone does not hide those changes |
| Frozen benchmark runner → current production runner | Different pre-existing runner versions; not treated as equivalent |

The matching CPU/engine comparisons differ only in build/debug identity: MVID,
PE timestamp, PDB identity/path/checksum and informational source revision.
Checks cover IL and headers, local signatures, exception regions, implementation
flags, layouts, constants, reference token mappings, strings and resources.

The frozen runner identifies revision `08c5174`; the current runner has later
multi-drive/IPF/HDF/export and partial-audio checksum handling. These changes
predate this pin update. Current production replays were therefore checked
separately against the complete accepted workload fingerprints.

| Activation check | Result |
|---|---|
| Production Release solution | Pass, zero warnings/errors |
| Engine diagnostics in separate outputs | 669 passed, none skipped |
| CopperDisk tests | 74 passed, none skipped |
| Host tests with supplied native media | 95 passed, none skipped |
| Current production lores, hires and native Lemmings runner replays | Complete accepted fingerprints match; zero measured steady-state allocations |
| Desktop, runner, host-test and engine-test CPU DLLs | All match the published package DLL hash |

The prior exact-package CPU suite passed 1,499 cases; its six optional external
corpus cases remain unavailable. The activation suites above had no failures.

## Performance provenance

The original [six-pair performance result](CPU_PREFETCH_LOCALITY_2026-09-22.md)
remains attached to its exact frozen binaries and runner: all three workloads
passed the 1% upper-bound gate, with 26.47% paired native Lemmings FPS improvement.
This packaging activation adds no new executable CPU/engine change and does not
relabel that series as a fresh benchmark of the packaged DLL or current runner.
Equal executable inputs do not prove identical JIT placement or throughput.

The activation replays are correctness checks, not additional performance
acceptance samples. Their FPS is not used for a new gain/regression claim.
Background Blender/Python CPU activity was observed during a subsequent bounded
host check, so those diagnostic timings must not be compared with the quiet-host
formal series.

The [machine-readable activation record](CPU_PREFETCH_ACTIVATION_2026-09-22.json)
contains release IDs, assembly/test hashes, lockfile checks and complete replay
identities. Raw logs and comparator outputs remain under ignored
`artifacts/cpu-prefetch-activation-2026-09-22/`. Hosted CI is pending at this local
validation stage and will run when the integration commit is pushed. The earlier
trace-pin CI result remains historical.
