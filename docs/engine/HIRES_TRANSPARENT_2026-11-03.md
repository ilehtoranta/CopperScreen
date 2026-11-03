# Transparent hires sprite span — 2026-11-03

This investigation follows the [CPU prefetch metadata cleanup](CPU_PREFETCH_METADATA_2026-11-03.md).
It tests one engine cost with that unpublished development CPU fixed in both
roles. The existing default Copper68k pin remains published **1.5.4**.

## Change and preservation argument

The ordinary visible hires CCK previously entered the sprite compositor twice,
once for each lores sprite position underlying four hires pixels. When existing
canonical state proves that neither position has a shifting sprite or comparator
start, a combined guard now advances `_lastOutputX` directly to the second
position and renders all four playfield pixels. It adds no cached or mirrored
hardware state.

The guard requires the same output line, increasing x, zero dirty state, zero
shifting mask and the next start strictly after **both** positions. Line setup,
register writes, pending playfield collision matching, active shifters and either
comparator position retain the original fallback. A pending collision match
requires work even when no sprite is visible. CLXDAT read-clear re-arms that
pending state; it cannot be treated as an empty span.

Both bitplane-pair shifts still run before the guard, including reload handling.
Single/dual playfield palette selection preserves the existing lookup semantics.
All four colors are stored together using the existing four-pixel CCK buffer
contract, as in `FillHiresCck`. Borders and partial-window CCKs keep their existing
paths. CPU execution, DMA, integer clock, interrupt sampling, PCM and package
interfaces are unchanged. This preserves implemented behavior; replay agreement
does not establish new hardware conformance.

## Behavior and generated code

The production solution builds successfully with both the common development
CPU and the default published **1.5.4** package, with zero warnings/errors.
**1,287 engine cases pass**, zero
fail, with **seven optional native groups unavailable**. Two new cases cover
CLXDAT read-clear inside a hires window with transparent sprites, in single and
dual playfield modes. Removing the dirty-state guard makes both cases fail;
the retained production implementation passes. An initial four-plane test fixture
delayed the CPU read until a scalar window edge and did not discriminate that
mutation. The final one-plane fixture asserts that the read occurs inside the
window, with available CPU bus slots.

Normal and scalar full **18,120-field Lemmings captures** each match all **160
retained files**: CPU/device state, pixels, Chip/slow RAM and real stereo PCM.
No expected fingerprint is changed. These tests and captures use the fixed
development CPU. Diagnostic capture allocations, I/O and FPS are excluded from
throughput evidence. The fresh default-package build verifies the maintained
NuGet consumer boundary; its throughput was not measured.

Ordinary optimized .NET 10.0.12 Tier1 code is inspected separately from timing.
The new guard inlines into `RenderHiresCck`, with a four-color vector store on
the transparent branch. The inspected renderer is 1,210 bytes versus 1,524 in
the reference, with 36 versus 47 conditional branch sites across the entire
method. These counts describe the inspected PGO bodies, including fallbacks;
they are not executed-instruction counts or an FPS result. Active-sprite
fallbacks remain. A scalar-store prototype is retained separately.

## Paired measurement

**Timing INVALID / RERUN; no speedup, slowdown or retention result established.**
The tested source is retained as an **unmeasured performance candidate**.
The [machine-readable evidence](HIRES_TRANSPARENT_2026-11-03.json) includes file
hashes, raw result identities, descriptive rates and independent telemetry checks.

The versioned protocol plans six alternating pairs for each of lores DMA, hires
DMA and native Lemmings. Both capsules use Copper68k
**1.5.5-prefetch-metadata-dev.1**, and only their engine DLL differs. Lemmings
retains the reviewed ROM, two disks and portable script, **14,520 warmup plus
3,600 measured fields**, PAL OCS/68000, 512 KiB Chip plus 512 KiB slow RAM,
full 908×313 output, conservative interpreter batching and real stereo PCM.
Each synthetic run uses **600 warmup plus 7,200 measured fields** with the
retained lores/hires identities. No workload or golden is learned from this build.

Topology is rechecked on the homogeneous Ryzen 5 5600X: CPU 2, protected SMT
pair 2–3, Normal priority, Balanced power and .NET 10.0.12. Builds, tests and JIT
inspection finish before timing. Preflight and ten-second cooldowns retain
native policy-v2 telemetry.

After two earlier failed starts, the authorized loaded-host diagnostic completes
**two pairs per workload, 12 complete runs**. Every complete result matches its
expected identity with **zero measured steady-state allocation**. The series is
stopped after those two rounds because real build activity and sustained host
interference persist. The next partial lores run, all complete results and all
violations remain recorded. The planned six-pair comparison is **incomplete**;
no subset is promoted to acceptance and no confidence/retention bound is claimed.

The following are **descriptive INVALID data only**, not measured gains/losses:

| Workload | Reference mean FPS | Candidate mean FPS | Paired change, two pairs |
| --- | ---: | ---: | ---: |
| Lores DMA | 282.220 | 282.420 | −0.9396% |
| Hires DMA | 281.365 | 280.010 | −0.4499% |
| Lemmings | 276.875 | 322.315 | +16.4317% |

Paired figures use geometric ratios rather than a ratio of arithmetic means.
Changing host load prevents attributing the apparent native improvement to this
candidate. It must not be added to the earlier CPU-package gain. Default-1.5.4
throughput, valid native gain, full three-workload retention and release
qualification all remain unestablished.

Independent analysis rereads all 12 raw identities, rechecks frozen files and
reapplies policy v2 and the idle-node classifier to **457 telemetry intervals**.
There are **209 violation observations** and **120 competing-job intervals**.
Peaks are **87.38% selected-CPU other activity, 100% sibling activity and 96.41%
package interference**. These are genuine invalid timings even after excluding
provably idle build nodes in 454 intervals. All frozen files remain unchanged.

## Earlier attempt and evidence handling

A preceding bitplane-control experiment is **shelved** and its source restored.
The reference JIT already inlined the helpers, so the earlier call-removal
interpretation was incorrect. Its partial timing series is **INVALID / RERUN**:
the historical host checker classified idle reusable Visual Studio MSBuild nodes
by presence. Those records and historical helper are unchanged; no bitplane gain
is claimed or pooled with this comparison.

The separately versioned [sprite protocol](../../scripts/measure-hires-transparent-v2.ps1)
retains native host-load policy v2 counters, 25% selected/sibling/package limits
with the existing ten-second sustained rule, Normal priority and topology checks.
It records raw job presence and before/after native MSBuild CPU counters.
Only an unchanged reusable Visual Studio child with complete counters and zero
CPU delta is classified as idle. New identities, changing CPU counters, missing
telemetry, non-reusable nodes and other build/test/benchmark jobs remain rejected.
Positive and negative classifier controls run before measurement. This does not
reclassify any older timing evidence.

The first sprite series is also **INVALID / RERUN**: both Visual Studio nodes
consumed CPU during preflight. Its complete first lores sample and interrupted
next sample remain preserved, with no causal performance conclusion. A second
attempt catches another active build and stops automatically. The third,
explicit loaded-host attempt above uses fresh evidence paths and is stopped at
the stated bound. No samples are trimmed or moved between attempts, and no
older invalid record is relabeled.

Raw build/test records, mutation results, native captures, disassembly, frozen
reference/candidate capsules and measurement records are retained locally under
`artifacts/hires-transparent-20261103/`. The setup note records an early retry
after a line-ending hash mismatch. Temporary checked-in lockfile changes from
that retry were restored; private restore locks isolate the final builds.
