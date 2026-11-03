# Plain-bus CPU prefetch metadata cleanup — 2026-11-03

The unpublished Copper68k development package **1.5.5-prefetch-metadata-dev.1**
moves publication-context construction and value copies behind the instruction
fetch-window consumer boundary. The Lightweight plain bus uses the shared fetch
kernel with a reference to immutable empty storage, instead of creating and
copying an unused 152-byte stack value for each prefetch.

**Correctness PASS; native comparison VALID.** Six paired Lemmings runs show
**+6.9481% throughput**, with a two-sided 95% interval of **+4.7471% to +9.1952%**.
Mean rates are **307.44 FPS reference** and **328.79 FPS candidate**. The paired
frame-time change is **−6.4967%**; its one-sided 95% upper bound is **−4.9601%**,
within the native 1% retention limit. Full three-workload and release acceptance
are not established by this native-only comparison.

Target-pair refills retain the opcode fetch, IPL poll and extension fetch in
that order. Retirement keeps its existing empty-queue sample, pending request
ownership, issued phases, bus readiness, CPU tail and deferred tokens. Accepted
fetches still complete. The metadata-consuming path retains owned snapshots
across queue clearing, including predecessor and branch-transition metadata.
No instruction, interrupt sample, bus transfer or hardware phase is removed.

## Source and package boundary

The producer change is in the isolated CopperMod worktree
`../CopperMod-prefetch-metadata`, branch `codex/plain-bus-prefetch-metadata`, based
on published 1.5.4 commit `535e9ecfc543931b46f7c332d9593805382bd7cf`.
Only `Copper68k/M68kCore.cs` and the producer package-version/release-notes metadata
are modified. The current CopperMod main checkout is untouched. Its unrelated
later memory-BTST interrupt-timing change is not included in this comparison.

The producer builds and packs a new local development NuGet package. CopperScreen
then builds the production solution and diagnostic engine project with
`Copper68kBoundaryVersion=1.5.5-prefetch-metadata-dev.1` and the isolated additional
package source. Restored `project.assets.json` identifies Copper68k as a package;
the candidate DLL matches the DLL inside that `.nupkg` byte for byte.
Per-project private lock files keep the checked-in package locks unchanged.

**CopperScreen's default published pin remains 1.5.4.** No sibling source/project
reference, public feed publication or NuGet configuration change is introduced.
The development package and worktree remain available for review and follow-up;
this is not a new stable release.

The actual NuGet consumer build passes without warnings/errors. To isolate CPU
cost, timing uses its packaged CPU DLL with the already-qualified common engine
and runner from the preceding [hires cleanup](HIRES_MODE_SELECTION_2026-11-03.md).
The candidate dependency manifest truthfully names the development package.
Normalizing only CPU package/binary metadata leaves the manifests identical,
including CPU dependencies. Every other capsule file matches. Both runtime
assembly versions load through the same common engine's supported CPU API.

## Behavior and code checks

- **4,475 standard Copper68k cases pass**, with zero failures and 19 optional
  external-conformance cases unavailable. This excludes the separate synthetic
  discovery namespace. A focused 447-case MC68000/010/prefetch/interrupt run also
  passes; its two unavailable external cases overlap the standard suite.
- **1,285 engine cases pass**, with zero failures and seven optional native groups
  unavailable. Repeated inactive/masked interrupt sampling remains covered.
- Normal and scalar full **18,120-field Lemmings replays** each match all **160
  retained files**: CPU/device JSON, pixels, Chip/slow RAM and real stereo PCM.
  There are zero mismatches or unsupported features. These captures are separate
  from the unavailable optional tests.
- Selected generated-code inspection is collected during the normal capture,
  separately from throughput, with ordinary .NET 10.0.12 tiering/PGO defaults.

The capture runs use zero warmup and produce output `0x72FF28A9A6D931E7`;
their CPU/hardware hashes and final cycles remain `0x0BC2B7554DA315AB`,
`0x8F41668D6DF17C3C` and `2574756906`. Capture allocations/I/O and FPS are
excluded from throughput conclusions. The measured benchmark uses its separate
retained 14,520-warmup identity; no new golden is learned from this candidate.

In the inspected Tier1 code, `TopUpPrefetchOne(out)`, `TopUpPrefetchOneCore` and
the plain target-pair refill have **no 152-byte copies**. No metadata-only
`WithPublication`/`WithPredecessor` helper is compiled during the replay.
The shared retirement body still contains three 152-byte copy sites for actual
consumers: its branches into IG71 and IG127 require a non-null fetch-window bus;
IG74 is reached within that consuming path. The plain bus enters neither branch.
Queue metadata clearing retains its existing zero state. This is a scoped
consumer split, not a blanket change of publication-context arguments to `in`.
Code shape alone does not establish a throughput gain.

An initial broad prototype test launch suffered a logging setup failure and
left an extra process running. Both superseded prototype runs and their child
processes were stopped; they provide no passing-suite claim. The completed
scoped and standard final runs above use the final frozen build. No such process
is allowed to contaminate the subsequent timing series.

## Paired native measurement

[`measure-cpu-prefetch-metadata-v1.ps1`](../../scripts/measure-cpu-prefetch-metadata-v1.ps1)
is separately versioned from earlier engine/cache protocols. It retains six pairs
ordered R1/C1/C2/R2/R3/C3/C4/R4/R5/C5/C6/R6, the reviewed ROM/two disk archives
and portable input script, 14,520 warmup plus 3,600 measured complete fields,
PAL OCS/68000, 512 KiB Chip plus 512 KiB slow RAM, full 908×313 pixels,
conservative interpreter batching and real stereo PCM.

Topology and inputs are rechecked on the homogeneous Ryzen 5 5600X. CPU 2,
protected SMT pair 2–3, Normal priority, Balanced power, .NET 10.0.12 and
host-load policy v2 remain in effect, with native telemetry, preflight and
cooldowns. Builds, tests and code inspections finish before timing. Loaded-host
diagnostics are authorized; any violations remain INVALID / RERUN.

All twelve results match the same complete retained benchmark identity with
zero measured steady-state allocation and no unsupported features. Every frozen
file remains unchanged. Independent analysis rereads all raw stdout, recomputes
the paired statistics, verifies file hashes and reapplies policy v2 to all **729
telemetry intervals**: **zero violations and zero competing-job intervals**.
Peak selected-CPU other activity is 8.75%, sibling activity 46.84%, and package
interference 41.54%. The sibling/package peaks are transient; neither reaches
the sustained rejection period. No observation is trimmed or relabeled.

| Pair | Published 1.5.4 FPS | Development package FPS |
| --- | ---: | ---: |
| 1 | 304.98 | 332.94 |
| 2 | 304.43 | 331.85 |
| 3 | 311.15 | 329.12 |
| 4 | 311.65 | 323.56 |
| 5 | 302.43 | 326.66 |
| 6 | 310.00 | 328.63 |
| Arithmetic mean | 307.4400 | 328.7933 |

Paired changes use geometric ratios, not a ratio of these arithmetic means.
Frame-time ratios are reference FPS / candidate FPS. Student-t intervals on the
six paired log ratios use df=5: 2.0150483733330233 for the declared one-sided
95% frame-time bound and 2.570581835636305 for the separately reported two-sided
95% throughput interval. This series establishes a native speedup for the frozen
plain-bus candidate. It does not identify the cause of an older source-stack
regression or compare against rates collected in a different series.

Native-only results do not establish the full three-workload retention gate,
performance of metadata-consuming hosts or release qualification. Optional
hardware conformance and synthetic discovery coverage remain separate from the
completed behavior-preservation checks. The default pin remains published 1.5.4.

Raw evidence, the local development feed, package-integrity checks and producer
diff are under `artifacts/cpu-prefetch-metadata-20261103/`. The
[machine-readable record](CPU_PREFETCH_METADATA_2026-11-03.json) binds the final
results and source/input/binary hashes. The earlier
[bottleneck investigation](LEMMINGS_BOTTLENECKS_2026-11-03.md) and published
package evidence remain unchanged.
