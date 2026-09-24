# Fixed bounds with original array storage

2026-09-24. **VALID comparison; overall 1% gate not met.**
The [inline palette](VIDEO_PALETTE_2026-09-23.md) and
[inline register-bank](REGISTER_BANK_2026-09-24.md) trials remove generated
instructions but fail the overall performance gate. Their inline-storage edits
are discarded and their complete results remain unchanged.

## Target and invariant

This candidate exposes the same constant bounds while preserving the original
arrays and instance-field layouts. Private register and palette arrays are
allocated from `RegisterWordCount` (256) and `PaletteColorCount` (64). They remain
readonly references and are never replaced or exposed. Private span accessors
use those same constants and managed array-data references. Consequently each
view covers exactly its backing array, and its indexer still checks the range.
There is no raw pointer or per-access allocation.

The compiler can remove checks where it proves an index is in range, and uses a
constant bound elsewhere. Paired reads/writes reuse a local view. The first
fixed-view build reconstructed views twice in some pairs; generated code exposed
extra array loads/null checks, so the local-view revision was made before any
performance comparison. That preliminary build's source, binaries, passing tests
and four matching workload identities remain local diagnostic evidence.

Register readback, set/clear semantics, odd-offset aliasing, out-of-window behavior
and reset are unchanged. Bitplane/sprite pointer updates retain the
[proved word indexes](REGISTER_BANK_2026-09-24.md#target-and-implementation).
Palette values, HAM state, composition, framebuffer writes and all hardware
input/output phases retain their previous order. A local view caches an array
reference and extent, not color values across emulated hardware updates.

The single DMA word latch and fixed-size Chip RAM access remain from the
[word-access candidate](DMA_WORD_ACCESS_2026-09-23.md). This adds no device
deadline, hardware polling or CPU-package change. The bounded undriven-bus model's
physical uncertainties remain open.

## Verification

Both fixed-view revisions build in production Release with zero warnings or
errors. The first revision passes all 705 engine, 74 disk and 95 native-enabled
host tests, plus four frozen-runner identities with zero measured allocation.
The final local-view revision also passes all 874 tests with no skips. Generated
code captures and all four complete uninstrumented workload identities pass with
zero measured steady-state allocations. The 20,000-field Tower Assault replay
retains all 1005 byte-identical capture files: 335 states, 335 bitmaps and 335
Chip RAM dumps. This preserves the bounded disk-two/opening-level proof; it does
not certify whole-game completion or physical open-bus behavior.

The final capture removes both register-pointer bounds comparisons and uses one
array reference per paired update. Palette pairs likewise reuse one checked view,
and constant color-zero accesses require no length comparison. The generated
null checks and remaining variable-index checks are retained.

| Tier-1 method | Unchanged control, bytes | Final candidate, bytes |
|---|---:|---:|
| Bitplane completed output, lores and hires | 388 | 363 |
| Register read, lores and hires | 121 | 98 |
| Device dispatch, lores | 3608 | 3595 |
| Lores pair | 936 | 929 |
| Device dispatch, hires | 3536 | 3526 |
| Hires CCK | 2318 | 2250 |
| Hires visible pair | 932 | 919 |

These are sequential captures of the full workloads on runtime 10.0.12, CPU 2 /
Normal, against the same unchanged SDK 10.0.401 control used for the preceding
trials. Captured methods can vary with dynamic PGO between runs; the table uses
this investigation's actual control capture. Code sizes and instrumented FPS
are not performance acceptance evidence.

Final candidate engine SHA256:
`B50BF3D9F2582F0ED6E021AD1640B30C1BA507AE80EE3B7091A29A98B475E734`.
The preliminary engine is
`38F5AFD4CED41D79D09D5E9938993E5DF65BE11565ABB7340BEA6474A560894B`.
Both use SDK 10.0.401 and the unchanged frozen runner, Copper68k, CopperDisk and
CopperFloat dependencies. The accepted-source reference remains
`1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`,
built with SDK 10.0.400. All current executions explicitly use .NET 10.0.12.

## Performance protocol

[Comparison v7](../../scripts/run-lightweight-ocs-identification-comparison-v7.ps1)
retains the v4 measurement body with the final candidate binding and new protocol
identifier. It uses six balanced pairs per workload, unchanged complete workload
identities, CPU 2 with sibling 3 protected, Normal priority, pinned .NET 10.0.12
and host-load policy v2. Every one-sided 95% upper frame-time regression bound
must be at most 1%. Earlier candidates, runtimes and samples are not pooled.

The complete comparison is VALID: all 36 complete identities match, measured
steady-state allocations are zero and host-load policy v2 telemetry passes.
The independently calculated [result record](FIXED_BOUNDS_2026-09-24.json)
agrees with the protocol summary.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
|---|---:|---:|---:|---:|---|
| Lores | 394.17 | 396.49 | -0.5602% | +2.2979% | Inconclusive |
| Hires | 357.48 | 360.19 | -0.7459% | +0.4647% | Pass |
| Native Lemmings | 324.68 | 337.97 | -4.0274% | -0.0878% | Pass |

All valid samples remain included, including the slow native reference R3
(293.14 FPS). No sample is removed based on its speed. There are 1,445 valid
telemetry intervals over 1,574.158 seconds; the largest interval is 1.822 seconds.
The longest continuous intervals at or above 25% selected/sibling/package load
are 0 / 1.129 / 2.346 seconds, below the ten-second rejection threshold. There
are no competing build/test/runner workloads.

Lores does not establish the required upper bound. **No exception is granted.**
Investigation continues with the unchanged 1% requirement; this candidate's
complete evidence is retained separately from subsequent candidates.

Local evidence is under ignored
`artifacts/fixed-bounds-optimization/`; its `candidate` / `source` folders identify
the preliminary view implementation, and `cached-candidate` / `cached-source`
identify the final local-view implementation. The initial v7 draft binding was
replaced before any preflight or measurement; no recorded protocol is changed.
No media or local binary/capture artifacts are included in the source change.
