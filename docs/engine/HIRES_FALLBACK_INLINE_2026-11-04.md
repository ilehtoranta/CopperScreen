# Hires fallback inlining fix — 2026-11-04

`LightweightSprites.ComposeHiresColorIndexes` now has an
`AggressiveInlining` hint. This restores both active fallback operations inline
in the reproduced hires DMA renderer after the visible-CCK transparent guard.
It retains the transparent path and
four-pixel output implementation. No executable C# method body, register phase,
sprite/comparator operation, collision match, CPU/interrupt sample, DMA transfer,
pixel or PCM sample is changed. No cache or production diagnostic is added.

The [prior diagnosis](HIRES_TRANSPARENT_DIAGNOSIS_2026-11-04.md) found a 41.86%
fallback share in the full diagnostic hires workload and two surviving compositor
calls in the regressed candidate. The
[prior valid benchmark](HIRES_TRANSPARENT_BENCHMARK_2026-11-04.md) measured a
−2.5702% hires change against the original pre-optimization reference. Those
records remain unchanged and their samples are excluded from this series.

## Validation

- The production Release solution builds with zero warnings/errors, using its
  current published CPU pin and owned CopperDisk source.
- **1,300 diagnostic engine cases pass**, zero fail; **seven optional native
  groups remain unavailable**. The interrupt-sampling and collision read-clear
  cases remain included. Diagnostics use separate outputs.
- Normal and scalar **18,120-field Lemmings captures** each match all **160
  retained files**: state, pixels, Chip/slow RAM and real PCM. Fingerprints and
  golden files are unchanged. Capture FPS and allocation totals are diagnostic.
- Ordinary optimized .NET 10.0.12 hires DMA code confirms **zero out-of-line single
  compositor call sites** in `RenderHiresCck`; both fallbacks inline again. The
  observed renderer is **2,525 bytes**, versus 1,217 in the regressed inspection
  and 2,221 in the original reference. Code size is not an FPS result.

The source change is limited to the hint and one explanatory comment; removing
those lines reproduces the saved pre-edit source. The body and transparent
guard remain intact.

## Controlled measurement build

The comparison uses the **same original reference** as the prior clean series,
engine DLL SHA-256
`217B17AE2148F79EB24C2798E94666AD492B768B71780D33812DECB07950E164`.
It evaluates the fixed complete candidate against that baseline; it is not a
direct fixed-versus-regressed speedup measurement. Rates from separate series
are not subtracted or pooled to estimate the hint's isolated FPS contribution.

Both capsules share the frozen runner, CopperDisk binary and unpublished
Copper68k **1.5.5-prefetch-metadata-dev.1** package, CPU SHA-256
`20B4E1002E8F7B2994EB139E04905BE1B5AC2970C360C4E2B1F682CA14029133`.
Only their engine DLL differs. The actual production solution is built normally.
For the controlled engine DLL, a private artifact project copies the same 49
production C# files byte for byte, retains the pinned Copper68k NuGet reference,
and compiles against the frozen CopperDisk assembly. This prevents unrelated
storage build changes from entering the comparison. No repository project
reference or package pin changes, no sibling CopperMod source reference, and no
publication occur.

## Timing protocol and status

The versioned protocol retains six alternating pairs per lores DMA, hires DMA
and native Lemmings workload. Native runs retain 14,520 warmup plus 3,600 measured
fields, reviewed ROM/two disks/script, PAL OCS/68000, 512 KiB Chip plus 512 KiB
slow RAM, full 908×313 output, conservative interpreter batching and real stereo
PCM. Synthetic runs retain 600 warmup plus 7,200 measured fields and the same
expected identities. Topology, CPU 2/protected SMT pair 2–3, Normal priority,
Balanced power, .NET 10.0.12, native host-load policy v2 and cooldowns are checked.

Two early attempts stop before any complete sample because real build/test jobs
are active. They remain **INVALID / RERUN**. The new
[v2 measurement script](../../scripts/measure-hires-fallback-inline-v2.ps1)
additionally recognizes the active CopperSharp compiler CLI as a competing build.
It retains the prior idle-node classifier and every load threshold. The
[v1 script](../../scripts/measure-hires-fallback-inline-v1.ps1) and its records
remain unchanged. No older data is relabeled.

The bounded idle check expires while native storage tests remain active. A
loaded-host diagnostic then records three complete matching samples plus an
interrupted next run. Its preflight contains interference, so the attempt stays
**INVALID / RERUN**. Once those competing tests exit, it is stopped and a fresh
strict full comparison starts. No sample is transferred between attempts.

That strict restart also encounters active MSBuild work after two complete lores
samples and stops as **INVALID / RERUN**. A final bounded full series runs in
explicit loaded-host diagnostic mode, as previously authorized. It will retain
all samples and violations; affected timings cannot establish throughput or
retention, even if individual workload identities match.

## Paired rerun disposition

**Timing INVALID / RERUN and incomplete. FPS recovery is not verified.**
The final diagnostic series completes **22 of 36 planned runs**: four lores
pairs, four hires pairs and three Lemmings pairs. Every complete result matches
its retained identity with zero measured steady-state allocations and no
unsupported operation. The next `lemmings-C4` run is interrupted and retained.

The host reaches approximately 97% package interference during observation;
the latest completed hires reference run drops to **50.13 FPS**. The series is
stopped because escalating saturation makes further timing uninformative. This
is a stopped invalid series, not a six-pair acceptance measurement. All complete
samples, the partial next run and all violations remain preserved; no subset is
promoted to a performance result.

Independent analysis rereads all **22 complete identities**, verifies the frozen
files and reapplies host-load policy v2 and the build-node classifier to **844
telemetry intervals**. It reproduces **565 violation observations** and **496
competing-job intervals**, with idle nodes excluded in 303 intervals. Peak
selected-CPU other activity, sibling activity and package interference each
reach **100%**. The failure is not merely idle process presence.

Raw mean rates are stored as descriptive data in the machine-readable evidence.
No paired FPS change, confidence interval, valid retention decision or release
claim is reported for this incomplete, contaminated series. A quiet full rerun
is still needed to determine whether restoring inlining recovers throughput.
The inlining and behavior checks above remain successful and separate results.

Raw builds, tests, captures, code inspection, source snapshots and frozen
capsules are under `artifacts/hires-fallback-inline-20261104/`.
The [machine-readable evidence](HIRES_FALLBACK_INLINE_2026-11-04.json) records the
source change, validation, attempts, complete raw results, stop reason and hashes.
