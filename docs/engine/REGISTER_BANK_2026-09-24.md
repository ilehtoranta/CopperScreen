# Fixed-size custom-register bank

2026-09-24. **Valid comparison; the 1% gate is not met. Inline storage discarded.**
This continues the [DMA word-access investigation](DMA_WORD_ACCESS_2026-09-23.md).
The [palette trial](VIDEO_PALETTE_2026-09-23.md) missed the performance gate and
its source edit was removed before this candidate was built. Its complete valid
result remains unchanged.

## Target and implementation

The bitplane DMA output path updates two custom-register words for every
completed pointer increment. Generated production code previously loaded the
register array and its length and compared both indexes with that length. The
storage has always contained exactly 256 words.

The measured `LightweightRegisters` candidate owns a private `[InlineArray(256)]` value. The general
read/write guard retains the same 512-byte window, odd-offset aliasing and
out-of-window behavior. Reset clears the bank's span and initializes the same
registers. Special readback and set/clear behavior are unchanged. Internal
variable indexes retain bounds checks where the compiler cannot prove the range.

Bitplane and sprite DMA setters calculate a word index directly. Their existing
unsigned channel guard limits the channel to 0–7. The resulting high/low indexes
are 112–127 for bitplanes and 144–159 for sprites, all inside the bank. This removes
the intermediate byte offset, ushort narrowing and two shifts without changing
which words are written. Pointer masks, store order and physical completion
cycles remain unchanged.

The preceding single DMA word latch and fixed-size Chip RAM access are retained.
There is no new device deadline, per-CCK polling, bus transfer, CPU-package change
or approximation of the hardware sequence.

## Generated-code evidence

The unchanged direct-word source control and register candidate use SDK
10.0.401 and explicitly selected runtime 10.0.12. Complete lores and hires
instrumented replays run sequentially at CPU 2 / Normal priority. All four retain
the established identities and zero measured allocation; instrumented FPS is not
acceptance evidence.

| Tier-1 method | Unchanged control, bytes | Register candidate, bytes |
|---|---:|---:|
| Bitplane completed output, lores and hires | 388 | 360 |
| Register read, lores and hires | 121 | 94 |
| Device dispatch, lores | 3608 | 3608 |
| Device dispatch, hires | 3536 | 3536 |

The completed-output capture shows removal of the register array/length loads,
two bounds comparisons and associated failure branches. Register reads no longer
load an arbitrary array length. The device-loop size is unchanged. These are
removed operations; a throughput gain still requires measurement.

## Verification and frozen comparison

Production Release builds with zero warnings or errors. All 705 isolated engine,
74 CopperDisk and 95 native-enabled host tests pass, with no skipped cases.
The frozen runner retains the complete lores, hires, Lemmings and Tower Assault
identities, with zero measured steady-state allocation. The 20,000-field Tower
Assault replay matches all 1005 original capture files byte for byte: 335 states,
335 bitmaps and 335 Chip RAM dumps. This retains bounded disk-two loading and
opening-level evidence; whole-game completion and physical open-bus edges remain
uncertified.

Candidate engine SHA256:
`969DAEDA957D5E429BB9DC9CF4F1CDA0E8954B16051FA9E4659351DD9F2B894F`.
The accepted-source reference remains
`1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`.
Runner, Copper68k, CopperDisk and CopperFloat binaries remain identical across
both directories. The candidate is built with SDK 10.0.401; the reference was
built with SDK 10.0.400. Both execute on pinned .NET 10.0.12.

[Comparison v6](../../scripts/run-lightweight-ocs-identification-comparison-v6.ps1)
retains the complete v4 measurement body, changing only the candidate binding and
protocol identifier: six balanced pairs, unchanged workload/input identities,
CPU 2 with sibling 3 protected, Normal priority and host-load policy v2. Every
one-sided 95% upper frame-time regression bound must be at most 1%. All valid
samples are retained; preceding candidates and runtimes are not pooled.

## Complete performance result

The first v6 series completed VALID. All 36 full workload identities match,
measured allocations are zero and host-load policy v2 passes. An independent
calculation reproduces the protocol's complete statistics; no samples are removed
or pooled with preceding candidates.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Result |
|---|---:|---:|---:|---:|---|
| Lores | 378.6467 | 372.8250 | +1.5456% | +4.4445% | Above limit |
| Hires | 344.5567 | 350.2017 | -1.6053% | -0.5406% | Pass |
| Native Lemmings | 323.8767 | 323.3083 | +0.1864% | +2.4795% | Inconclusive |

Telemetry contains 1461 intervals spanning 1600.08 seconds, with a longest interval
of 1.191 seconds. The longest continuous selected/sibling/package load at or above
25% is 0.099 / 3.376 / 2.269 seconds. No competing build, test or runner is detected.

Only hires meets the 1% gate. The owner requires continued investigation with that
limit retained, so no exception is assumed and inline register storage is discarded.
The removed instructions do not establish an acceptable overall improvement.
Further investigation retains the original arrays while exposing their fixed
extents to the compiler, separating constant bounds from the changed object layout.

See the [complete machine-readable result](REGISTER_BANK_2026-09-24.json) for all
samples, independently computed statistics and source/build/protocol/evidence
hashes. Local source, binary, test, code-generation and native evidence is under
ignored `artifacts/register-bank-optimization/`. No media or local executable/capture
artifacts are included in the source change.
