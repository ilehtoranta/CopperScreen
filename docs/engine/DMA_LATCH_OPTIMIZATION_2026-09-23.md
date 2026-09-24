# OCS DMA bus latch: remove duplicate timing state

2026-09-23. **Correctness retained; valid performance comparison exceeds the 1%
upper-bound gate. No exception granted.** Optimization of the unaccepted
[Tower Assault correction](TOWER_ASSAULT_INVESTIGATION.md). The first candidate's
[valid performance result](TOWER_ASSAULT_PERFORMANCE_2026-09-23.json) is preserved:
its lores/hires/Lemmings upper bounds were +5.0003%, +1.3831%, +2.1427%.
The owner requested optimization; those bounds have not been accepted.
The owner also declined this candidate's new bounds below and requested continued
work under the 1% limit. The [fixed-size DMA word-access follow-up](DMA_WORD_ACCESS_2026-09-23.md)
keeps this latch design and removes redundant RAM-access work. This record remains
the evidence for the first single-latch candidate.

## Change and ordering

The first correction stored a word and a cycle after every physical DMA transfer.
All six DMA engines already record their completed output cycles for arbitration.
The optimized implementation retains only the shared 16-bit bus word. On the
uncommon absent-DENISEID read, a non-inlined helper obtains the latest completed
cycle from those six existing histories. It returns the retained word only in
the following CCK, otherwise `$FFFF`, exactly as the preceding bounded model did.

The transfer paths lose the extra cycle argument and timestamp store. Timing
queries execute only on readback; there is no new per-CCK work, allocation,
callback, CPU bookkeeping or scheduler. Untimed RAM setup/debug helpers do not
drive the latch. Actual read and write transfers update it at the same output
phase as before. The read uses the latest completed cycle, not merely the first
device whose history matches a preceding slot.

All six histories reset with their devices. Accepted DMA remains visible even
when its consumer is subsequently canceled. Internal blitter phases do not
update its bus-output history. Device histories survive ordinary frame and DMA
enable transitions. CPU grant/completion rules, chip-memory contents, device
event ordering and CPU package are unchanged.

This preserves the implemented digital model. It does not establish electrical
decay, refresh/dummy bus values, coincident physical phases or broader unreadable
register behavior; the original hardware-evidence limitations still apply.

## Generated code and correctness

Release JIT captures use the complete retained lores workload on the reference,
first correction and optimized candidate. Their fingerprints match and measured
allocations are zero. These instrumented captures establish code generation,
not throughput acceptance. The optimized DMA paths contain one cached-word
store; the redundant 64-bit timestamp store is absent.

| Tier-1 routine | Pre-correction bytes | First correction bytes | Optimized bytes |
|---|---:|---:|---:|
| Bitplane output | 422 | 436 | 429 |
| Copper word completion | 362 | 377 | 370 |
| Audio DMA advancement | 495 | 512 | 505 |
| Sprite output | 604 | 619 | 612 |
| Blitter output | 1158 | 1223 | 1195 |

Smaller generated code and a removed store do not quantify their share of the
earlier regression; the full comparison measures the resulting candidate.

The original sixteen identification regressions pass. Six additional cases
exercise real bitplane, Copper, sprite, blitter-read and blitter-write output,
plus internal blitter phases after an expired DMA sample. Backing-RAM changes
after transfer cannot alter the cached bus word. These are regression checks of
the existing model, not new physical hardware measurements. The first Copper
test setup placed the read across refresh stalls; starting after refresh makes
the intended immediately-following-CCK check explicit. The failed setup log is
preserved locally.

- Production Release build: 0 warnings, 0 errors.
- Isolated engine diagnostics: 691 passed, 0 skipped.
- CopperDisk: 74 passed, 0 skipped.
- Host: 95 passed, 0 skipped, with native ROM/media/input supplied.

The optimized 20,000-field Tower Assault replay is byte-identical to the prior
correction: all 335 state snapshots, 335 full field images and 335 chip-RAM dumps
match (1005 files). The previous scalar replay matched those same files. Boot,
disk-2 handling, level entry and scripted movement therefore retain the same
bounded evidence. Disk 3 and whole-game completion remain unverified.

## Frozen performance comparison

The reference remains the pre-correction engine at accepted source `cb946eb`,
SHA256 `1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683`.
Optimized engine SHA256:
`800010B3D8ACC20884F331DE99D89C86B5928A9391FF4676A42DA7FAFF43056C`.
Runner, Copper68k, CopperDisk and CopperFloat retain the common original binary
identities in the Tower Assault evidence. Only the engine differs.

[Comparison v2](../../scripts/run-lightweight-ocs-identification-comparison-v2.ps1)
changes the candidate binding and protocol name from v1. Its measurement body is
unchanged: six balanced pairs each for lores, hires and native Lemmings; verified
homogeneous topology, protected SMT sibling, Normal priority and host-load policy
v2; complete matching identities and zero measured allocations. The one-sided
95% upper frame-time regression bound must be ≤1% for every workload. No earlier
exception or invalid partial sample transfers.

The complete comparison is **VALID**, with all 36 fingerprints matching, zero
measured allocation and 1,465 valid telemetry intervals. No competing build/test
process was detected. The longest above-limit sibling/package streaks were
2.349/1.148 seconds, below the 10-second threshold. Independent recalculation
reproduces the protocol's paired statistics:

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
|---|---:|---:|---:|---:|---|
| Lores | 383.12 | 381.94 | +0.3011% | +2.2299% | Inconclusive |
| Hires | 344.76 | 336.65 | +2.5009% | +6.8299% | Acceptance required |
| Native Lemmings | 330.18 | 329.56 | +0.2067% | +2.6925% | Inconclusive |

Every upper bound exceeds the default 1% limit. **No performance exception is
granted.** The candidate removes redundant work, but this result does not
establish an overall throughput improvement or acceptance. The two candidates'
separate comparisons must not be treated as a paired measurement against each
other. In particular, the valid hires C5 sample at 307.11 FPS is retained despite
its large contribution to the spread; no timing sample was discarded or pooled
with another series.

The [result record](DMA_LATCH_OPTIMIZATION_2026-09-23.json) retains every sample,
fingerprint, independent statistic, validation total, binary/source identity and
local evidence hash. Protocol SHA256:
`DC922041FDAA2C62E41C82863A2D72433AFFDEE06240DCCAD019BF2802AD0F7F`.
All complete lores, hires, Lemmings and Tower Assault identities were verified
independently of the performance series, with zero measured allocations.
Native Tower Assault throughput from the first candidate is not relabeled as
an optimized-candidate measurement.

Local raw evidence is under ignored `artifacts/dma-latch-optimization/` and the
focused/build/engine logs under `artifacts/alien-breed2-investigation/`. No ROM,
game media, RAM dumps or binaries are committed. No commit, push or publication
is part of this optimization request.
