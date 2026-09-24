# Fixed-size OCS palette storage

2026-09-23. **Valid comparison; the 1% gate is not met. Palette edit discarded.**
This continues the [DMA word-access investigation](DMA_WORD_ACCESS_2026-09-23.md),
whose valid comparison passes hires but leaves lores and native Lemmings
inconclusive. Those results remain unchanged; no exception has been granted.

## Target and implementation

Generated production code loads the palette array reference and length before
looking up visible pixels. Even the constant color-zero lookup checks the array
length. This palette always has 64 entries: 32 programmed colors and their
existing half-bright values. The type previously exposed an arbitrary array
length to the compiler.

The measured `LightweightVideo` candidate owns a private `[InlineArray(64)]` value. Reset fills its
span with opaque black. Existing register updates and pixel lookups retain their
indexes and values. Variable indexes retain checked bounds of 64; constant color
zero becomes a direct load. Rendering order, physical register/reload phases,
shifting, sprite composition, HAM hold updates and framebuffer stores are the
same operations at the same emulated cycles. This changes the host representation
of a fixed lookup table, without skipping hardware work.

The DMA bus still retains its single word. Reusing the bitplane data array was
examined but not implemented: the next input can replace `_pendingPlane` during
a free CPU output slot, before the CPU samples the preceding DMA word. The
separate `LastPlane` diagnostic field is updated only in diagnostic builds.
Using either as an unconditional Release bus-history selector would be incorrect;
adding a Release selector would add another per-transfer store.

## Generated-code evidence

After the Visual Studio update, an unchanged source control was built with SDK
10.0.401 before making the palette edit. Its engine SHA256 is
`E0BE58D0A63313F2A89F573128ABBBD5F6880934CC5B1F7DD9AE1A4BA34F0DBE`.
Both control and palette candidate are captured on explicitly selected runtime
10.0.12, CPU 2 / Normal priority, using the complete lores and hires workloads.
All four instrumented runs retain the expected complete fingerprints and zero
allocation. Instrumented timing is not acceptance evidence.

The visible-pixel path replaces a palette object load and a length load with an
address calculation and immediate `64` bounds. The color-zero path removes its
array-length check. Captured Tier-1 sizes are:

| Method / workload | Unchanged SDK 10.0.401 control | Palette candidate |
|---|---:|---:|
| Device dispatch, lores | 3598 | 3585 |
| Video step, lores | 2167 | 2144 |
| Lores pair | 936 | 924 |
| Device dispatch, hires | 3536 | 3522 |
| Video step, hires | 2086 | 2072 |
| Hires CCK | 2294 | 2313 |
| Hires visible pair | 921 | 929 |

Some method sizes grow with generated layout/inlining. Removed memory loads and
checks are the optimization evidence; total code size does not prove a speedup.
Earlier SDK 10.0.400 captures are retained separately, rather than attributed to
this source change.

## Verification and frozen comparison

Production Release builds with zero warnings or errors. All 705 isolated engine,
74 CopperDisk and 95 native-enabled host tests pass, with no skipped cases. Existing
tests cover physical palette-write timing, HAM, sprites, dual playfields and
display boundaries. The retained lores workload exercises six-plane display/EHB.
The frozen runner retains complete lores, hires, Lemmings and Tower Assault
identities, with zero measured steady-state allocation. The 20,000-field Tower
Assault replay retains all 1005 byte-identical capture files: 335 states, 335
bitmaps and 335 Chip RAM dumps. This is bounded native equivalence, not whole-game
completion or physical open-bus certification.

Candidate engine SHA256:
`2F9E2C78E0B53ADBE1349B857D047A28892EC934F277B2BB27F7E0A34DD607EA`.
The accepted-source reference remains
`1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`.
Both directories retain the earlier identical runner, Copper68k, CopperDisk and
CopperFloat binaries; the candidate engine was built with SDK 10.0.401. The
unchanged SDK 10.0.401 source control above is diagnostic evidence, not a new
accepted reference.

[Comparison v5](../../scripts/run-lightweight-ocs-identification-comparison-v5.ps1)
changes the engine binding and protocol identifier from v4. Its measurement body
is identical: six balanced pairs, the same workload/input identities, explicitly
pinned .NET 10.0.12, CPU 2 with sibling 3 protected, Normal priority and host-load
policy v2. Every one-sided 95% upper frame-time regression bound must be at most
1%. All valid samples are retained; earlier comparisons are not pooled.

## Complete performance result

The first v5 series completed VALID on September 23. All 36 complete workload
identities match the independently verified inputs, measured allocations are zero
and host-load policy v2 passes. Independent calculation reproduces the protocol
summary; all valid samples are retained and no preceding result is pooled.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Result |
|---|---:|---:|---:|---:|---|
| Lores | 382.3467 | 383.2500 | -0.2509% | +1.4213% | Inconclusive |
| Hires | 344.7700 | 352.9033 | -2.3057% | -1.3773% | Pass |
| Native Lemmings | 322.8767 | 318.8133 | +1.3041% | +3.7981% | Above limit |

Telemetry contains 1456 intervals spanning 1600.63 seconds; the longest interval
is 1.188 seconds. The longest continuous selected/sibling/package load at or
above 25% is 0.130 / 2.284 / 1.149 seconds, below the unchanged ten-second rejection
threshold. No competing build, test or runner was detected.

The owner requires continued investigation with the 1% limit retained. This
candidate establishes a hires gain but not an acceptable overall result. The
palette edit is discarded; the native correction and direct DMA word-access work
remain under investigation. Removed host instructions alone are not sufficient
grounds to retain an optimization. The earlier SDK 10.0.401 array control and all
candidate source, binaries and observations remain available for diagnosis.

See the [machine-readable result](VIDEO_PALETTE_2026-09-23.json) for every sample,
independently recomputed statistics, source/build/protocol hashes and evidence
checksums. The working-tree HEAD during measurement was `123ec93`, whose only
addition to `cb946eb` is an unrelated website verification file; engine and
dependency source was unchanged. Local evidence is retained under ignored
`artifacts/video-palette-optimization/`. No media or local binary/capture artifacts
are included in the source change.
