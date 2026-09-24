# Fixed-size DMA word access

2026-09-23. **VALID comparison; default performance gate not met. No exception granted.**
The owner declined the [single-latch candidate's performance bounds](DMA_LATCH_OPTIMIZATION_2026-09-23.md)
and requested continued investigation under the same 1% limit. That complete,
valid result is preserved. This follow-up keeps the one-word bus latch and the
existing device completion histories, then simplifies the underlying RAM access.

## Evidence and change

Hires JIT captures show unchanged generated sizes for device dispatch, video
stepping and the central clock. The captured sprite-step inlining differs, but
these captures do not quantify its performance contribution. The synthetic
display workload has negligible steady CPU execution; the rare DENISEID read
is not established as the cause of its timing spread.

The generated DMA read, however, repeatedly loads the array length to calculate
an address mask and then checks the two-byte span range. The write path performs
masking and two separately checked byte stores. These checks are redundant under
the current machine's enforced memory invariant:

- Construction rejects every Chip RAM size except 512 KiB.
- The configuration's size is init-only; the allocated array is private and readonly.
- DMA ignores A0 and wraps into that physical RAM. Thus the word offset is
  `address & 0x7FFFE`, always even and at most 524,286.
- A two-byte access ends at most at index 524,287, inside the 524,288-byte array.

The read and write helpers now use `MemoryMarshal.GetArrayDataReference` with
`Unsafe.ReadUnaligned<ushort>` / `Unsafe.WriteUnaligned`, plus explicit host-endian
conversion. The mask remains on every access. This removes redundant per-word
range checks without permitting any address to escape the array. If supported
memory sizes change later, this invariant and the constructor guard must be
reviewed together. CPU address decoding and CPU memory access methods are unchanged.

Both DMA bytes become visible at the same previously accepted output phase.
There was no callback or other observer between the former byte stores on the
single owner thread. The new word store does not change emulated bus duration,
grant ordering, memory visibility, or CPU cycles. No device is skipped or batched.
The bus latch still records the completed word; the preceding-CCK/idle readback
model and its physical hardware uncertainties are unchanged.

A retained intermediate trial used only the constant mask with ordinary span/
array access. It passed 698 engine cases and retained the complete lores identity,
but still emitted range checks. Its local binary and code-generation evidence
remain available; it has no formal performance acceptance result.

## Validation and generated code

Seven DMA boundary cases cover aligned/odd addresses, both RAM ends, mirrors and
`uint.MaxValue`, checking CPU-visible byte order and untouched neighboring words.
Seven construction cases reject unsupported sizes before reaching direct DMA
access. The earlier 22 bus-readback cases remain, including all graphics DMA
sources, byte/long access, accepted cancellation, stale words and resets.

The complete lores code-generation run on .NET 10.0.11 retains its prior
CPU/hardware/output fingerprint, zero allocation and real PCM. Captured Tier-1
sizes on that runtime are:

| Routine | Single-latch candidate bytes | Direct-word candidate bytes |
|---|---:|---:|
| DMA read with bus latch | 74 | 30 |
| DMA write with bus latch | 84 | 29 |
| Bitplane output | 429 | 388 |
| Copper completion | 370 | 322 |
| Audio DMA advancement | 505 | 461 |
| Sprite output | 612 | 569 |
| Blitter output | 1195 | 865 |

Generated size establishes removed work, not an FPS result.

Production Release builds with zero warnings or errors. All 705 isolated engine,
74 CopperDisk and 95 native-enabled host tests pass, with no skipped cases.
The complete hires, native Lemmings and Tower Assault identities match their
previously validated identities, with zero measured steady-state allocation.
The lores code-generation run also retains its complete identity; its instrumented
timing is not performance acceptance evidence.

The bounded Tower Assault replay completes 20,000 fields. All 1,005 capture files
match the original correction byte for byte: 335 states, 335 bitmaps and 335 Chip
RAM dumps. That original replay also matched the scalar CPU control. The new
comparison is retained locally as `native-direct-equivalence.json`. Capturing
these diagnostics allocates output buffers; those capture totals are separate
from the zero-allocation steady-state runner checks. Native coverage remains
bounded by the same scripted replay, not whole-game completion or physical bus
edge certification.

## Frozen comparison

Reference engine SHA256 remains
`1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`,
from the pre-correction engine at accepted source `cb946eb`.
Direct-word candidate engine SHA256:
`34ED3311D4182666D30E63B53BF489CF5123A1BE62AC5C210AD2BACF67CD2C01`.
Common runner, Copper68k, CopperDisk and CopperFloat remain the same frozen
binaries as the earlier comparisons. CPU pin and machine profile are unchanged.

[Comparison v3](../../scripts/run-lightweight-ocs-identification-comparison-v3.ps1)
changes only the candidate binding and protocol identifier. Its first attempt
stopped after 26 completed samples when a Visual Studio update removed .NET
10.0.11 while the series was running. Starting the next process failed because
the required framework was absent. This whole attempt is **INVALID / RERUN**;
none of its samples contributes to acceptance. Its logs and samples remain
unchanged under `performance-direct-attempt-1` in the local evidence directory.

The update installs SDK 10.0.401 and runtime 10.0.12. The frozen application
binaries remain unchanged, compiled with SDK 10.0.400. Separately versioned
[comparison v4](../../scripts/run-lightweight-ocs-identification-comparison-v4.ps1)
selects `--fx-version 10.0.12` for both builds and checks SHA256 identities of
the host, resolver, execution engine, JIT and core library before and after the
series. Before timing, all 874 existing tests pass again on the replacement
runtime without rebuilding the frozen application. All four complete workload
identities retain zero measured allocations. A fresh 20,000-field Tower Assault
capture again matches all 1,005 original state/image/RAM files byte for byte,
recorded locally as `native-direct-runtime12-equivalence.json`.
Earlier runtime measurements and generated-code captures are not relabeled.

The measurement rules and expected identities are unchanged: six balanced pairs per retained workload,
Normal priority, verified homogeneous topology/SMT sibling protection, host-load
policy v2, matching complete fingerprints and zero measured allocations. Every
one-sided 95% upper frame-time regression bound must be ≤1%.

The complete v4 series is **VALID**. All 36 complete fingerprints match, measured
allocations are zero and runtime identities remain unchanged. Independent Python
recalculation agrees with the protocol statistics. The
[complete result record](DMA_WORD_ACCESS_2026-09-23.json) retains every sample,
input/build/source/protocol identity and evidence hash.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
|---|---:|---:|---:|---:|---|
| Lores | 376.14 | 377.28 | -0.3585% | +3.2687% | INCONCLUSIVE |
| Hires | 340.28 | 346.82 | -1.8964% | -0.0605% | PASS |
| Native Lemmings | 322.81 | 321.34 | +0.5053% | +4.9319% | INCONCLUSIVE |

All 1,459 telemetry intervals are present across 1,604.3733 seconds; maximum
interval is 1.2018 seconds. Longest continuous interference at or above 25% is
1.1498 seconds selected, 2.3339 seconds sibling and 2.3498 seconds package, each
below the unchanged ten-second rejection threshold. No competing build/test
workload is detected. Timing variation is retained; the slow native C5 sample
is valid and is not dropped.

Hires passes, but lores and native upper bounds exceed 1%. Their means are within
1%; the series does not establish that their regression is bounded by 1% with
the required confidence. The owner requested continued investigation with the
same limit. No exception, overall speedup or acceptance is inferred. Earlier
series, including the interrupted .NET 10.0.11 attempt, are not pooled or relabeled.
Local evidence is retained under ignored `artifacts/dma-latch-optimization/`.
No ROM, game media, RAM dumps, binary artifacts, commit, push or publication are
included in the requested source change.
