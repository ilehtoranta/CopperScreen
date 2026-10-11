# Initial-wait WORDSYNC control — 2026-10-11

This bounded correction implements the remaining unsupported initial-sync-wait
case in LWA-DISK-014. It establishes a digital model for live ADKCON.WORDSYNC
changes; it does not certify physical Paula timing or demonstrate a new game
compatibility benefit. The preceding [running-read correction](DISK_LIVE_WORDSYNC.md)
and [CPU byte-alignment correction](DISK_BYTE_WORDSYNC_2026-09-24.md) retain their
own dated evidence and performance decisions.

## Contract and evidence

The [Commodore HRM, chapter 8, DSKSYNC](https://bastya.net/AmigaDevDocs/hard_8.html)
specifies synchronized read starts, alignment on received matches, and a sync
interrupt independent of WORDSYNC. It does not explicitly resolve clearing
WORDSYNC after the start gate has been established. The following distinction
uses the pinned [WinUAE controller](https://github.com/tonioni/WinUAE/blob/e786c015b5b22c04b51573bf3d6fbc4c8792c8fe/disk.cpp)
as supporting implementation evidence:

- `DISK_start` seeds `dma_enable` from WORDSYNC. `DISK_update_adkcon` is empty;
  register writes do not recompute that gate.
- `wordsync_detected` opens the established gate independently of the live
  WORDSYNC bit. Its separate word-phase reset checks live WORDSYNC.
- `disk_doupdate_read` advances the input word phase while waiting, including
  when DMA cannot transfer, and `doreaddma` requires the gate to have opened.

No external implementation was imported. This supports a bounded interpretation,
not a physical hardware oracle or complete equivalence to WinUAE.

In Lightweight, the second enabled DSKLEN strobe latches the initial wait when
WORDSYNC is set. Clearing and re-enabling WORDSYNC do not release/rearm that wait,
clock input, reset a partial word or generate a disk interrupt. A received
DSKSYNC match opens the gate only with both DMACON enables set, preserving the
existing model's DMA qualification. The matching input is not queued.

The read word counter remains seeded at zero by the second DSKLEN strobe, as in
the existing Lightweight start model (LWA-DISK-004). It now advances modulo sixteen
on each received bit while waiting. Enabled WORDSYNC resets it on received matches,
including matches during a DMA pause. A match with WORDSYNC cleared opens an
eligible gate without resetting phase: the first stored word can contain the
tail of that sync and the beginning of following data. A match after re-enabling
WORDSYNC aligns the following sixteen input bits. The live enable continues to
control subsequent matches after the gate opens.

Zero-length reads still complete at the qualifying input match, with no pointer
movement or RAM transfer. A paused match cannot complete them; a later enabled
input match is required. Register-only DSKSYNC comparisons retain their existing
sync interrupt behavior and do not open the DMA gate. Shared serial shift/byte
state and published byte data/ready latches are unchanged by ADKCON toggles.

Cancellation drops the new read's gate, phase and FIFO. An already accepted RAM
word from a preceding cancelled transfer retains its address/data/output cycle;
it cannot decrement or complete a rearmed read. Reset revokes pending hardware
using the existing reset contract. Write DMA is unaffected.

The code adds no state, allocation, logging, scheduler or polling. Only the
initial-wait input branch advances the existing counter; established reads retain
their input path. The obsolete unsupported-only ADKCON handler and its call are
removed. Disk arrivals, address acceptance, RAM output and IRQ scheduling retain
their existing canonical ordering.

## Validation

Focused tests in [LightweightDiskInitialSyncTests](../../CopperMod.Amiga.Lightweight.Tests/LightweightDiskInitialSyncTests.cs)
cover all sixteen word phases with WORDSYNC cleared or re-enabled during the
first sync, actual serial input and resulting stored words, CPU/Copper writes,
encoded ADF input and partial CPU bytes on DF0–DF3, register-only equality,
zero-length sync/block interrupt ordering, both DMACON pause bits, enabled and
disabled matches during a pause, accepted output across cancel/rearm, and
cancel/reset phase revocation. The previous retained unsupported assertions in
[LightweightDiskLiveSyncTests](../../CopperMod.Amiga.Lightweight.Tests/LightweightDiskLiveSyncTests.cs)
now assert the latched gate and input-driven completion. These exercise the
declared digital model, not measured DMAL or physical interrupt propagation.

The Release production solution builds with zero warnings/errors. The final
focused set contains 72 cases: 59 fail and 13 pass on the pre-change source;
21 failures are phase/transfer assertions and 38 are the unsupported guard.
All 72 pass on the candidate. The complete candidate engine diagnostic suite
passes 1,241 cases and skips seven optional native checks whose environment
inputs were not configured. Those skips provide no native coverage. Diagnostic
outputs use the separate `artifacts/diagnostic-tests` path.

Local evidence is under `artifacts/wordsync-first-wait-2026-10-11/`, including
the complete pre-change focused run (`before-complete/`) and candidate engine
run (`engine-candidate/`). The aborted preliminary phase test (`final-tests-before/`)
and stale-baseline full diagnostic run (`engine/`) are retained and excluded from
candidate validation. Final tests initialize destination RAM explicitly and
assert a finite deadline before advancing to a scheduled transfer.

The [durable validation record](DISK_INITIAL_WORDSYNC_2026-10-11.json) pins the
Release engines, test-result hashes, native contracts, retained identities and
verification scripts. The frozen reference is source commit
`b64fc62f86039d357a055dff61d1fd1f7832444f`. Its engine SHA256 is
`8D533D78145628140C4B92CE7F786A05251FAE9F0A702B345C76818C568197D7`;
the candidate is `0978A510E212C936096AB0E7F2E1E57735D8F5B25FB9C8BB86222C857F5F0C6C`.

Both engines pass the unchanged reviewed [native regression baseline](../../CopperScreen.Lightweight.Tests/Workloads/native-regression-baseline-v1.json)
for these subsets of the pinned PAL OCS A500/68000, Kickstart 1.3, 512 KiB Chip
plus 512 KiB slow RAM profile:

| Replay | Fields per mode | Captured files per mode | Result |
| --- | ---: | ---: | --- |
| Alien Breed SE '92 | 16,000 | 85 | Reference/candidate and normal/scalar match; reviewed baseline PASS |
| Lemmings | 18,120 | 100 | Reference/candidate and normal/scalar match; reviewed baseline PASS |

All 370 candidate captured files (state, RAM, image and PCM) match the reference
across both execution modes, with matching complete final CPU/hardware/output
identities and no unsupported feature. The ROM/media contracts and frozen binary
hashes are checked before and after execution. The runner DLL/PDB hashes differ
between the builds; an explicit assembly audit finds all 216 runner method
signatures/bodies, locals, stack limits and exception regions identical. Other
runner dependencies match byte-for-byte. This is not an identical-runner-binary
performance comparison. Raw native reports remain under `native-reference/` and
`native-candidate/`; `verify_retention.py` rehashes every capture and binary and
compares the complete inventories, summaries and input contracts without
changing the reviewed baseline.

## Remaining limits

Physical WORDSYNC write/input collisions, actual start-phase seed, DMACON match
qualification, sync-to-DMAL propagation, combined MSBSYNC/WORDSYNC and silicon
revision differences remain unverified. The source cross-check does not resolve
those edges. No new native title is shown to require initial-wait toggles.
Native retention establishes regression coverage only.

No host-throughput acceptance is inferred from builds, tests or native capture
timings. Earlier WORDSYNC performance exceptions do not transfer to this change;
the current [performance guide](PERFORMANCE.md) owns applicable measurement
requirements and protocol availability.
