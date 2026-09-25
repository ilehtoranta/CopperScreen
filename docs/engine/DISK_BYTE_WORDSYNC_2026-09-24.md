# Alien Breed SE '92: CPU disk-byte WORDSYNC alignment

2026-09-24. The production engine correction removes the retained black boot
of Alien Breed Special Edition '92. The exact supplied boot IPF reaches the title
and disk-two prompt, and its matching second disk reaches the main menu.
This is bounded boot/menu compatibility, not full gameplay or OCS certification.

## Correction and evidence boundary

The [preceding investigation](THREE_NATIVE_INVESTIGATIONS_2026-09-24.md#alien-breed-se-92-sync-polling-and-cpu-byte-alignment)
found the `$8911` sync in the recovered stream. The CPU polls WORDEQUAL and then
reads two DSKBYTR bytes, expecting `$2A91`. The receiver aligned DMA words but
left the CPU byte counter at its previous phase. A diagnostic spindle shift
could make the poll succeed, but the resulting `$44AA` bytes still failed the
guest check. Aligning the byte counter alone removed the black boot.

[LightweightDiskSerial.Receive](../../CopperMod.Amiga.Lightweight/LightweightDiskSerial.cs)
now resets the byte counter when received input matches DSKSYNC and
ADKCON.WORDSYNC is set. This works without DMA. A complete byte already published
at the matching bit retains its data/ready latch; subsequent bytes complete eight
received bits after the match. Continuing equality aligns on each arriving bit,
while the sync interrupt remains edge triggered. DSKSYNC register writes still
update the live comparator and interrupt, but do not clock/reset the byte counter.
WORDSYNC-disabled input retains its free-running byte phase.

The [Commodore HRM, chapter 8](https://bastya.net/AmigaDevDocs/hard_8.html)
documents byte-ready/read-clear status, transient WORDEQUAL and resynchronization
on received sync words. It does not fully specify CPU byte phase outside DMA.
That extension, and the distinction between input and register-only comparisons,
are supported by the native counterfactual and the pinned
[WinUAE disk controller](https://github.com/tonioni/WinUAE/blob/2b64e9cff1ca5ce4ec2f1808042cad7d72dc24da/disk.cpp#L4096):
`wordsync_detected` resets the read bit offset independently of active DMA;
`DSKSYNC` register writes do not call it. The input path aligns on each matching
arrival, independently of the interrupt edge latch. No external implementation
was copied. Exact physical propagation, coincident CPU reads/register writes,
reset seed and combined MSBSYNC/WORDSYNC behavior remain unverified. These tests
and game progress are not measurements of physical Paula timing.

The change adds one conditional reset in the existing receive path, with no new
state, allocation, logging, scheduler or title-specific handling. CPU/package,
recovered bit timing, disk geometry, spindle phase and DMA/FIFO handling are
unchanged. Unlike the diagnostic experiment, register-only matches cannot
realign bytes, and continuing equality is distinct from the interrupt edge.

## Verification

The Release solution builds with **zero warnings/errors**. All **716 engine
diagnostic cases pass**, with no skips, using the separate diagnostic output path.
Eleven new cases in [LightweightDiskSerialTests](../../CopperMod.Amiga.Lightweight.Tests/LightweightDiskSerialTests.cs)
cover all eight input offsets, the following `$2A91` CPU bytes without DMA,
ready/read-clear behavior, disabled WORDSYNC, a matching register write and
continuing equality. Before the fix, eight fail and three controls pass; after
the fix all eleven pass. Existing allocation and disk-DMA checks also pass in
the complete engine run. Host/disk suites were not rerun for this serial-only
change; actual native runs provide the boot evidence below.

Native profile: PAL OCS A500, 68000, Kickstart 1.3, 512 KiB chip + 512 KiB slow
RAM, DF0 read-only, full 908-pixel output. ROM SHA256:
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.
The exact archive/entry hashes are retained in the
[original media manifest](NATIVE_CORPUS_2026-09-21_BATCH2_MEDIA.json) and the
[new machine-readable record](DISK_BYTE_WORDSYNC_2026-09-24.json).

| Replay | Observed result |
| --- | --- |
| Frozen pre-fix boot | Retained 8,000-field black screen, PC `$07F6D6` |
| Production fix, normal execution | Advertisement at 2,000 fields, SE '92 title at 4,000, disk-two prompt at 8,000 |
| Production fix, scalar execution | All 32 image/state/chip/slow-memory files match normal execution at eight checkpoints |
| Comparison with diagnostic alignment-only experiment | The same 32 files also match the successful experiment exactly |
| Production runner, matching disk two | Disk-two main menu by 9,000 fields; menu/credits sequence still running at 16,000, audio active, no unsupported feature |
| Retained native Lemmings gameplay workload | Complete CPU, hardware and output fingerprints unchanged; zero measured allocation |
| Retained lores and hires full-pipeline workloads | Complete CPU, hardware and output fingerprints unchanged; zero measured allocation |

The disk-two run uses the retained
[input script](../../CopperScreen.Lightweight.Tests/Workloads/alien-breed-se92-wordsync.json).
It ejects at field 8,000, inserts at 8,100 and presses fire at 8,200. The extracted
`media/alien-breed-se92-disk2.ipf` is the exact second entry from
`Team17/AlienBreedSpecialEdition92_615.zip`, SHA256
`8F27F047E15788DE9D0FD6B6691D7FBB2DCEE401095A4B34DBD1BD4FC572A3EF`.
The differently hashed pre-existing `media/alien-breed-disk2.ipf` was not used.
No game media was changed or committed. This script reaches menu/credits;
the supplied inputs do not establish a started level or gameplay completion.

Local captures, frozen binaries and logs are under
`artifacts/alien-wordsync-fix/`. Native capture/media-swap allocation and FPS are
diagnostic outputs, not acceptance measurements. The prior investigation and
failed counterfactuals retain their original results.

## Host throughput

The separately bound [disk-byte WORDSYNC comparison v1](../../scripts/run-lightweight-disk-byte-wordsync-comparison-v1.ps1)
compares pre-fix engine `4333ABB681731F8D50A80B7670E9880986212CEE9DEC41B5CF884FAA7D57B435`
with production correction `AC21CD70A32365755652B1BEBAACBA423C3150FDD89185E2775EB4F6A23F72CF`.
Both use the same runner, disk library, stable Copper68k 1.4.1 assembly and pinned
.NET 10.0.12 runtime. Only the engine DLL and its symbols differ. This is a new
comparison; previous package-specific performance exceptions do not apply.

The retained full lores, hires and native Lemmings identities, six balanced pairs,
Normal priority, homogeneous-host topology checks, CPU/SMT placement, host-load
policy v2 and default 1% one-sided 95% upper-bound limit remain intact.
The first series is **INVALID / RERUN**: package interference exceeded 25% for
10.32031 consecutive seconds during lores R2. Three completed lores identities
match with zero measured allocation, but none of those timing samples contributes
to acceptance. No complete paired result or confidence bound exists, and no
exception is granted. A fresh, complete comparison remains outstanding; the
source correction and its correctness evidence do not depend on that timing run.
