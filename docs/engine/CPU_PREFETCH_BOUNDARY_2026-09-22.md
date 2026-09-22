# Copper68k prefetch helper boundary — 2026-09-22

Candidate: prevent `TopUpPrefetchAtRetirement` from being inlined into its callers.
This changes one `MethodImpl` attribute in Copper68k source commit
`ada86020ad7a9b298cd7f689c3731d8d779684d1`, from `AggressiveInlining` to `NoInlining`.
No CPU semantics, chipset timing, public API or emulated work changes.
The candidate is isolated; the production `1.4.1-trace.1` pin remains unchanged.

The [dispatch investigation](CPU_DISPATCH_LOCALITY_2026-09-22.md) records the
observed mechanism: native instruction-body initialization falls from 2,864 to
1,776 bytes, with 104 bytes in an additional helper entry on the studied path.
This is the reason for the experiment, not evidence of a particular speedup.

## Correctness validation

| Check | Result |
|---|---|
| Production Release solution build, isolated outputs | Pass, zero warnings/errors |
| Diagnostic engine Release build, separate outputs | Pass, zero warnings/errors |
| Copper68k complete Release suite against candidate | 1,499 passed; six optional external-corpus cases unavailable |
| Engine diagnostics against candidate | 669 passed on full rerun; initial allocation failure retained below |
| CopperDisk tests | 74 passed |
| Host tests including supplied native media | 95 passed, none skipped |
| Frozen runner native, lores and hires replays | Complete accepted fingerprints match; zero measured allocations |

The CPU tests were exported from the pinned CopperMod source into the ignored
candidate workspace. Their compiled CPU DLL hash exactly matches the frozen
benchmark candidate. No sibling project reference was added to CopperScreen.
Production/diagnostic projects were built in separate isolated output trees;
the candidate CPU was then placed in those trees and tests ran with `--no-build
--no-restore`. This tests the exact candidate assembly without changing package
locks or silently replacing the cached trace package. The production build itself
uses the accepted package pin.

The first engine run passed 668 tests and failed the fast variant of
`CausalReceiverProducesZerosThroughNoFluxAndRecoversClock`: its allocation probe
reported 4,064 bytes instead of zero. This receiver-only test does not execute
the modified CPU helper. Both cases passed in a focused candidate rerun, and
subsequent complete runs passed all 669 tests with both the accepted CPU and the
candidate. The original failure remains recorded; its precise transient cause
has not been established. No assertion was weakened and no engine/test source
was changed to obtain the passing rerun.

The initial host run passed 92 and skipped its three optional native cases.
After supplying the existing ROM/media/script paths, the complete host run
passed all 95 tests, including native gameplay and keyboard/reset handling.
The six optional external CPU-corpus cases remain unavailable; host replays do
not supply that coverage.

## Performance protocol

**Status: attempt 2 VALID and complete; candidate does not pass the 1% gate.**

| Workload | Reference mean FPS | Candidate mean FPS | Paired mean frame-time change | One-sided 95% upper regression bound | Disposition |
|---|---:|---:|---:|---:|---|
| Lores | 377.73 | 367.72 | +2.7629% | +6.5470% | ACCEPTANCE_REQUIRED |
| Hires | 341.25 | 338.63 | +0.8059% | +4.4964% | INCONCLUSIVE |
| Native Lemmings | 251.94 | 261.38 | -3.6202% | -1.5164% | PASS |

Positive frame-time changes mean slower execution. Displayed FPS means are
arithmetic means of six runs; the gate uses the six paired log frame-time ratios,
not the ratio of the displayed means. The native paired FPS gain is 3.7562%.
The requested general 5% improvement is not established.

All 36 complete workload fingerprints match their independently retained
identities and all measured allocation counts are zero. Statistics were
independently recomputed from the raw samples. All 1,595 telemetry intervals are
valid, with no concurrent tests/builds/profiling. The longest continuous threshold
exceedances were 1.133 seconds on the selected CPU, 7.818 seconds on its sibling
and 4.621 seconds across the package, each below the unchanged ten-second limit.
Short load spikes remain in the data; no samples were discarded.

The [complete JSON record](CPU_PREFETCH_BOUNDARY_2026-09-22.json) includes paired
FPS, fingerprints, exact bounds, test counts, assembly identities, telemetry
summaries and evidence hashes. Native's gain does not waive the two synthetic
workload limits. No exception has been granted and the candidate has not been
adopted. The recommended disposition is to keep the accepted production CPU and
continue investigating ways to remove initialization without this regression.

Attempt 1 stopped after 15 completed measurements (all twelve lores samples and
three hires samples), during a cooldown. Measured package interference remained
at or above 25% for 11.11163 seconds. The entire series is invalid; its provisional
lores statistics are not acceptance evidence. No timings are carried into the
fresh attempt 2. A subsequent five-second process-CPU observation found no
sustained heavy ordinary process in the accessible sample; it does not identify
the cause of the earlier interference or substitute for protocol telemetry.

[CPU prefetch comparison v1](../../scripts/run-lightweight-cpu-prefetch-comparison-v1.ps1)
copies the retained empty-shifter comparison's measurement body unchanged. Only
the experiment name/topology type and candidate binary bindings differ. The
reference is the accepted `9be9fee` frozen runner; the candidate uses exactly the
same runner, engine, disk and floating-point assemblies, with only Copper68k
replaced. Both native expected fingerprints are identical and remain pinned.

- Reference CPU SHA256: `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC`.
- Candidate CPU SHA256: `2710185867F6E2D4B219704F6B1670BA4F012B24686284CE458B33F87B1E7E23`.
- Shared engine SHA256: `8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.

The preflight verified current homogeneous topology, LP2 with protected sibling
LP3, Normal priority, live telemetry and rejection of missing telemetry, sustained
interference and corrupted native fingerprints. It is not an acceptance run.
The measurement uses six balanced pairs for each of lores, hires and native
Lemmings, the unchanged warmups/frame counts, ten-second cooldowns, complete
fingerprints, zero measured allocations and host-load policy v2. No JIT dump,
diagnostic instrumentation, builds or tests run during measurement.

The gate remains the **one-sided 95% upper frame-time regression bound ≤1% for
every workload**. Above the limit requires owner acceptance. Historical exceptions
do not transfer. Invalid attempts remain excluded; no sample may be selected or
pooled to rescue a result.

Local evidence is under ignored `artifacts/cpu-prefetch-boundary-2026-09-22/`.
The source experiment and disassembly remain under
`artifacts/cpu-dispatch-2026-09-22/isolation/`. No ROM/media, generated package or
build output is included in source control. No package publication is authorized
by this measurement.
