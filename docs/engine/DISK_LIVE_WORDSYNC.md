# Running-read WORDSYNC control — 2026-09-21

This bounded correction starts from performance-accepted `c92b16b` (gallery-only
HEAD `3e9517a`). It removes the Superfrog, F17 Challenge and Overdrive boot stops
identified in [native batch 2](NATIVE_CORPUS_2026-09-21_BATCH2.md). Changes before
the first sync match remain explicitly unsupported; this is not completion of
the Paula disk timing audit.

## Behavior and evidence

Once a read has passed its initial sync gate, changing ADKCON.WORDSYNC preserves
the partial word, queued FIFO words, accepted RAM transfer, pointer and remaining
count. Setting WORDSYNC does not rearm the initial gate. Clearing it disables
alignment on later matches. Setting it allows later matching input to realign
the receiver through the existing `ReceiveBit` path. A register write itself
does not supply matching input. Cancellation and reset retain their existing
semantics; write DMA is unaffected.

The [Commodore HRM, chapter 8, DSKSYNC](https://bastya.net/AmigaDevDocs/hard_8.html)
describes input-driven alignment during an enabled synchronized read. Preserving
an established transfer across an enable write is a model interpretation of that
contract, cross-checked against the pinned
[WinUAE controller](https://github.com/tonioni/WinUAE/blob/e786c015b5b22c04b51573bf3d6fbc4c8792c8fe/disk.cpp):
`DISK_start` initializes the start gate, `wordsync_detected` opens it and applies
the live alignment enable, and `DISK_update_adkcon` does not reset these states.
No external implementation was imported. Neither this cross-check nor native
game progress is a physical Paula timing oracle. No new hardware measurement of
a coincident ADKCON/input edge is available.

The previous handler reported unsupported and then reset both gate and partial
word on every live toggle. The correction narrows that guard to the unresolved
first-sync wait and removes those resets. It adds no fields, allocations,
per-bit work, polling or scheduler. Media decoding, recovered bit timing, spindle
phase, FIFO scheduling and DMA completion timing are unchanged.

## Correctness checks

[Fourteen focused cases](../../CopperMod.Amiga.Lightweight.Tests/LightweightDiskLiveSyncTests.cs)
cover both toggle directions, partial words, future aligned/unaligned matches,
queued and accepted words, DMACON pause/resume, cancel/reset, real encoded ADF
input on DF0–DF3 and the retained first-sync guard. On the accepted reference,
eleven behavioral cases fail; two further failures reflect the guard's more
precise diagnostic text. The reset case already passes. All fourteen pass on the
candidate. FIFO cycle assertions check the existing bounded model, not measured
DMAL propagation phases.

Production Release build: zero warnings/errors. Engine diagnostics: **669 passed**.
CopperDisk: **74 passed**. Host: **95 passed**, including all three enabled native
checks; no skips. Lemmings retains its accepted native output identity.

## Native checks

Profile: PAL OCS, 68000, Kickstart 1.3, 512 KiB chip + 512 KiB expansion RAM,
read-only media, conservative CPU batching. Exact archive/entry hashes are in the
[batch-2 manifest](NATIVE_CORPUS_2026-09-21_BATCH2_MEDIA.json). Kickstart SHA256 is
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.

All three previously stopped at fields 269/238/265. Each now completes an
8,000-field no-input replay with no unsupported feature. Superfrog reaches title,
credits and attract gameplay; F17 reaches its disk-two prompt; Overdrive reaches
title, credits and bonus instructions. Follow-up input replays use:

- [Superfrog](../../CopperScreen.Lightweight.Tests/Workloads/corpus-superfrog.json)
- [F17 Challenge](../../CopperScreen.Lightweight.Tests/Workloads/corpus-f17.json)
- [Overdrive](../../CopperScreen.Lightweight.Tests/Workloads/corpus-overdrive.json)

The F17 swap uses the exact `F17Challenge_Disk2.ipf` entry from the supplied ZIP,
extracted into ignored local `media/corpus-f17-disk2.ipf`. Source archives remain
unchanged. Captures distinguish loading, menu, attract and controlled gameplay;
bounded progress does not certify every level, protection path or disk swap.

The 18,000-field F17 replay reaches track selection, race briefing and the race
after the disk-two swap. The 18,000-field Superfrog script changes the intro
sequence and reaches later attract gameplay; it does not establish controlled
play or use of the second/story disks. Both complete without unsupported features.

Overdrive's initial 18,000-field input run reaches the disk-two request. Its final
24,000-field run swaps the exact `Overdrive_Disk2.ipf` entry from the supplied ZIP
via ignored `media/corpus-overdrive-disk2.ipf`, then reaches the interactive race
selection/statistics screen. Fire advances the selection from race 1 to races 2
and 3. This proves second-disk loading and menu input, not a driven race. Both
runs complete without unsupported features.

Local evidence: `artifacts/wordsync-investigation/`, including exact commands,
script snapshots, frame/state/RAM captures every 60 fields, test logs and frozen
runner directories. Capture allocations and incidental FPS are diagnostic only.

## Frozen performance candidate

Reference engine SHA256:
`DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365`.
Candidate engine SHA256:
`8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`.

[Live-WORDSYNC comparison v1](../../scripts/run-lightweight-live-wordsync-comparison-v1.ps1)
pins these engines and the existing frozen runner/dependencies. It preserves
blitter-modulo v1's six balanced pairs, unchanged lores/hires/Lemmings workloads,
Normal priority, verified homogeneous-core placement, host-load policy v2 and
one-sided 95% upper frame-time limit of 1%. Both builds must produce the accepted
post-blitter native identity, including output `7D31C3889FF73760`; all complete
workload identities must match. Previous performance exceptions do not transfer.

The initial validation-only attempt correctly rejected concurrent native replays
(`validate-v1/`). It collected no timing samples. The fresh validation-only retry
after all replays completed verifies topology, live telemetry and rejection
probes. Both formal attempts were then invalidated by measured background load:

| Evidence directory | Point reached | Invalidation |
| --- | --- | --- |
| `comparison-v1/` | First lores reference finished; following cooldown | Package interference >=25% for 10.3610974 seconds |
| `comparison-v1-retry1/` | First lores reference in progress | Protected sibling interference >=25% for 10.5844222 seconds |

Both are **INVALID / RERUN**, retained without pooling or dropping samples. Neither
provides a complete paired workload result, regression estimate or acceptance decision.
A post-run CPU-delta check found active OldWorld CPU use; the invalidations are
based on measured load, not the mere presence of that process. At that point the
candidate remained **performance verification pending** until the machine was
available for a fresh full run. On 2026-09-21 the owner explicitly chose to leave performance
verification pending. This is not performance acceptance. No earlier exception
applies and no new exception was requested.

The owner resumed measurement on 2026-09-22. A fresh validation-only preflight
passed, with unchanged protocol SHA256
`F182F38CBCA22ED847646CD0FA191169C300019B90FB167616FFEA13DAADF637`, engines,
dependencies, inputs, CPU 2 placement and protected sibling 3. The fresh formal
series `comparison-2026-09-22/` completed four lores samples before sustained
selected-core interference >=25% for 11.0241629 seconds invalidated R3. Peak
recorded package interference in that interval was 90.474%. A subsequent
five-second CPU-delta check observed 10.25 CPU-seconds in OldWorld. This series
is also **INVALID / RERUN**; its partial timings are not acceptance evidence.

### Complete comparison — 2026-09-22

After the owner confirmed that the competing game was stopped, the unchanged
protocol completed a fresh full series in `comparison-2026-09-22-retry1/`.
All **36 samples** are valid, in the prescribed six-pair order per workload.
All complete workload fingerprints match across reference and candidate,
including native CPU/hardware/picture/PCM state; all measured allocations are
zero. The 1,591 telemetry samples contain no missing interval, competing workload
or sustained threshold breach. An independent calculation reproduces every
summary statistic. Frozen binary and protocol hashes remain unchanged.

| Workload | Mean reference FPS | Mean candidate FPS | Paired frame-time change | One-sided 95% upper bound | Default gate |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 371.34 | 368.30 | +0.8098% | +3.9888% | INCONCLUSIVE; exceeds 1% bound |
| Hires | 346.37 | 346.49 | -0.0279% | +1.0749% | INCONCLUSIVE; exceeds 1% bound |
| Native Lemmings | 255.54 | 259.83 | -1.6501% | +1.6400% | INCONCLUSIVE; exceeds 1% bound |

FPS columns are arithmetic means of the six runs. Frame-time changes and bounds
use paired log ratios with the protocol's one-sided Student t calculation.
Negative frame-time changes mean faster execution. None of the mean changes
exceeds 1%, but **none of the three workloads passes the owner's required upper
bound**. On 2026-09-22 the owner accepted all three specific upper-bound exceptions
for candidate `8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5`
and authorized commit/push. Numerical dispositions remain INCONCLUSIVE; this
acceptance does not transfer to future changes, which retain the 1% default.
No samples were discarded and no series were pooled. The invalid
attempts above remain excluded from acceptance.

The [durable measurement record](DISK_LIVE_WORDSYNC_PERFORMANCE_2026-09-22.json)
includes every pair's FPS, full workload identities, precise statistics, telemetry
audit and SHA256 hashes of raw evidence, frozen assemblies and protocol. The
frozen script's inherited final console sentence calls native a changed-state
comparison; its actual pinned expectations and all twelve native samples are
identical across roles. No changed-state allowance was used in this comparison.

The separate wide-output Lemmings diagnostic preflight retains cycle
`2063189662`, CPU `249E90223C45891F`, hardware `F4951BBFA89CA99A`, output
`7D31C3889FF73760`, real PCM and zero measured allocations. It ran alongside a
native correctness replay and is **not** a throughput acceptance measurement.
