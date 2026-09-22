# Paula disk control edges — 2026-09-20

This bounded follow-up starts from accepted display commit `a10b22c`. It corrects
DMA status and zero-length WORDSYNC read completion in the PAL OCS/68000 model.
It does not complete the disk-controller timing audit.

The later [live WORDSYNC correction](DISK_LIVE_WORDSYNC.md) supports toggles after
the initial sync gate has opened. The first-sync-wait case remains unsupported;
the original validation and performance evidence below is preserved.

## Control changes

- DSKBYTR.DMAON now uses the active transfer latch and both DMACON enables.
  The first DSKLEN enable write only arms the interlock. The second starts the
  transfer. Waiting for WORDSYNC reports enabled; pausing either DMACON enable
  reports disabled without cancelling the transfer. Completion, cancellation and
  reset clear DMAON. DSKWRITE still reflects DSKLEN's direction bit.
- During writes, fetching the final RAM word does not clear DMAON: buffered bits
  still have to pass through the serializer. Its existing completion event clears
  the latch. Read completion remains attached to the accepted RAM output.
- A zero-length read with WORDSYNC waits for a qualifying input match before
  raising DSKBLK, without moving the disk pointer or transferring a RAM word.
  Cancellation/reset can revoke that wait. The unsynchronized zero-length
  second-strobe behavior is unchanged.

These changes reuse existing transfer/sync state. No fields, allocation, per-CCK
polling or new scheduler were added. Status work occurs only on DSKBYTR reads;
the zero-length branch runs only when a waiting transfer receives a match. Media
encoding, bit recovery, spindle phase and the ordinary FIFO transfer path are
unchanged.

## Evidence and limits

The [Commodore HRM, chapter 8](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_8.html)
defines the two-write enable interlock, enabled-DMA status and WORDSYNC's start
gate. Interpreting DMAON as the transfer latch, rather than the last written
enable bit, is also supported by the pinned
[WinUAE disk controller](https://github.com/tonioni/WinUAE/blob/e786c015b5b22c04b51573bf3d6fbc4c8792c8fe/disk.cpp):
DSKBYTR uses active DMA state, and zero length completes when the sync gate opens.
This is specification/model evidence with an independent implementation cross-check,
not new physical Paula measurements. No external controller code was imported.

Eight discriminating control cases failed on the accepted build and pass after
the changes. Four additional cases drive the real encoded-ADF receiver through
the first sync on DF0–DF3, observing DSKSYN/DSKBLK and unchanged RAM/pointer.
The existing accepted-address, cancellation/rearm, partial-byte, write protection,
four-drive and write/export tests remain intact.

The pinned vAmigaTS
[read probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Paula/Drive/read)
were inspected as possible physical references. A 600-field reference-build
`read2` replay does not match the published A500_ECS photograph's complete raster
phases. Its source also reads address `$8400` into ADKCON, rather than writing
immediate `#$8400`; the comment alone does not establish WORDSYNC setup. This
probe is not claimed as passing hardware validation for these corrections.

The existing rule requiring enabled DMA for a waiting sync match is retained;
exact qualification around DMACON changes is still unverified. Also open: FIFO
request sampling, last-read-word loss, completion/IRQ propagation, initial
non-WORDSYNC alignment, active DSKLEN rewrites, live WORDSYNC changes and FIFO
overrun. The latter three still report unsupported. See [ISSUES](ISSUES.md).

## Validation and performance

Production Release build: zero warnings/errors. Engine diagnostics: **634 passed**;
host: **95 passed**, including all three enabled native checks; CopperDisk:
**74 passed**. No tests skipped.

The frozen performance runner predates writable/export CLI options. An initial
Workbench run therefore left DF1 protected and produced no export; its attempted
reopen failed with a missing file. Those logs are retained as failed harness
coverage. The corrected native check uses a separately frozen production runner,
SHA256 `179EFEBFBD3604BED593A78EE415B8E324F7BA2B339C4190FA72A723D528154A`,
with reference/candidate engine overlays. The performance runner remains unchanged.

Both builds completed the same Workbench 1.3 script: full DF1 format/verify,
`proof.txt` write/read, explicit ADF export at 14,000 fields, then readback on a
fresh machine at 3,500 fields. Candidate captures visibly show `WRITE TEST FINISHED`
and `REOPEN TEST FINISHED`. Complete CPU/hardware/output fingerprints match between
these two builds in each run:

| Run | Cycle | CPU | Hardware | Picture/PCM |
| --- | ---: | --- | --- | --- |
| Format/write/export | 1989295738 | `2FA2FF5564A3DC89` | `38DFA45B1786A97A` | `80AE84C1B2A4A0BD` |
| Fresh reopen | 497224738 | `D4ECD9D164B37A40` | `E59FCAFC9D2F71EA` | `07E60B00CF30D4C0` |

Both exported ADFs have SHA256
`57E7D9499AF5D4471AD03FAC5F5866E58F8BE710B44F10B623CBEF773BC6A6D4`.
These are new same-build-pair native records; historical pre-keyboard/beam
fingerprints are preserved in [NATIVE_VALIDATION](../NATIVE_VALIDATION.md).
Boot-probe capture allocations and incidental FPS are not acceptance measurements.

Reference engine SHA256:
`33BACBDCA3A8A427D09B405334364B890AB38991C2809A52BB2933942F69E64F`.
Candidate engine SHA256:
`501A3DBD82A29DEA310E308D077BB93965CB31D9F4E8E166C7050CA5166DFA02`.

[Homogeneous retention v4](../../scripts/run-lightweight-homogeneous-retention-v4.ps1)
pins accepted `a10b22c`, retaining v3's runner, dependencies, workload fingerprints,
six balanced pairs, host-load policy v2 and one-sided 95% upper frame-time bound
of at most 1%. Earlier approvals do not transfer. **Hires/native pass; the user
accepted the specific lores +1.606% upper-bound exception on 2026-09-20 and
authorized commit/push.** This approval covers only the identified candidate;
the default 1% bound remains in force for subsequent changes.

The first series (`retention-v4/`) is **INVALID / RERUN**: during lores R4,
package CPU activity excluding the runner remained at or above 25% for
10.978 seconds. Its partial samples are retained and are not acceptance evidence.
The retry uses a fresh directory and the same frozen inputs and policy.

The second series (`retention-v4-retry1/`) is also **INVALID / RERUN**: during
hires R2, package interference remained at or above 25% for 10.430 seconds.
The completed lores portion had a paired mean frame-time change of -0.403% and
upper bound +1.158%, but it belongs to this invalid series and is **not** an
acceptance result or a valid basis for a performance exception. Neither series
produced a final summary. No build/test ran concurrently with either attempt.
The production/candidate hashes and frozen source inputs remained unchanged.
Both invalid directories are preserved. Correctness results above do not
establish the 1% budget.

### Valid complete comparison

After the user made the machine available for measurement, the third series
(`retention-v4-retry2/`, 2026-09-20 14:12–14:40 UTC) completed all 36 samples with
valid telemetry, matching complete-workload fingerprints and zero measured
steady-state allocations. The same frozen Release binaries ran on logical CPU 2
of the Ryzen 5 5600X at Normal priority, protecting SMT sibling 3 under host-load
policy v2. No builds, tests or competing emulator runs accompanied measurement.
Source hashes and the production/candidate DLL identity remained unchanged.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 395.28 | 394.31 | +0.246% | +1.606% | INCONCLUSIVE — scoped exception accepted |
| Hires | 355.99 | 356.59 | -0.170% | +0.527% | PASS |
| Native Lemmings | 265.89 | 265.57 | +0.124% | +0.448% | PASS |

FPS columns are arithmetic means. The unchanged gate uses six paired log
frame-time ratios with the one-sided Student-t bound. Lores is not a demonstrated
regression above 1%, but its uncertainty exceeds the user's bound. The user
accepted the scoped exception after reviewing the complete result; the measured
INCONCLUSIVE classification is preserved. These figures measure engine
throughput, not desktop presentation. Raw samples, telemetry, identities and
`summary.json`/`report.json` remain in the third series directory.

Ignored local evidence: `artifacts/disk-edges-2026-09-20/`, including frozen
assemblies, source/input hashes, failing/passing test logs, probe research and
native captures. ROMs, disk media and external sources are not committed.
