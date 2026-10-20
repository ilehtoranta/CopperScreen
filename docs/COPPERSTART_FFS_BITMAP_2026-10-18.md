# CopperStart FFS bitmap qualification - 2026-10-18

Writes on 4.125 GiB DOS1/FFS volumes pass full-ROM execution, guest device
flushing and fresh protected boot readback. A second volume fills the lower
four GiB with ordinary files and verifies new allocations above that boundary.
No NSD changes were made; large offsets use TD64.

## Artifact and implementation

* Qualification: `diskboot-2026-10-18-ffs-bitmap.1`.
* ROM: 524,288 bytes, SHA-256 `c77e51bbbab2d0f5edfc12b59094707279b2d1bc44e57b585857f1b56763c23b`.
* Native core: 493,924 bytes; graphics: 30,099 bytes at `$FF8A00`.
* Machine: PAL OCS A500, 68000 interpreter, 512 KiB Chip + 512 KiB slow RAM.

Sequential bitmap scans retain the current root/extension pointer table in
caller-owned scratch, reading each table once. Allocation skips occupied words
32 blocks at a time and retains the bitmap page's first block to avoid software
division on cache hits. The allocation search also retains its pointer-table
cursor across pages. Checksums, metadata allocation validation, partial-word
bounds and failed-write invalidation remain enforced.

The producer uses a recorded, reconstructed Classic DOS snapshot based on the
previous qualified producer, including its original `AdvanceTicks`. Concurrent
DOS/Exec/SFS work in the live tree is outside this ROM snapshot. SFS journal
recovery follows the existing native SFS-write profile: it remains enabled with
SFS writes and is excluded from this Classic build. Both FFS core source files
are hash-checked against the current source. Static native analysis reports no
managed allocations, runtime helpers/features, external native targets or
exception regions. The exact inputs and source hashes are recorded in the
[machine-readable evidence](COPPERSTART_FFS_BITMAP_2026-10-18.json).

## Accepted validation

| Coverage | Cases passed | Reset-ROM boots |
| --- | ---: | ---: |
| Host filesystem semantics | 38 | - |
| Compiled MC68000 device/geometry checks | 5 | - |
| Full-ROM large-volume read/write/allocation stress | 3 | 5 |
| Full-ROM app/runner writes and protection, including high offsets | 6 | 21 |
| Full-ROM DD/HD OFS/FFS and RDB regressions | 22 | 27 |
| Full-ROM total | 31 | 53 |

All intended cases passed without skips. Both solution Release builds passed.
Host semantics, compiled instruction tests and full-ROM execution are distinct
coverage. The PowerShell entry point also passed a syntax check.

The two writable large fixtures contain 8,650,752 blocks of 512 bytes, with
explicit geometry `DH0:2:32:4:135171`. Original startup commands and existing
file data are above the volume-relative four-GiB boundary. The stress fixture
owns the lower four GiB with two normal files and their extension tables.
Independent image audits verify ownership, allocation counts, checksums,
payload, metadata, partition guards and the saved whole-image hash.

Guest commands exercise creation, a 2,049-byte write, a patch crossing a sector
boundary, stream flushes, rename, truncation, deletion, protection, comment and
date updates, reads and a final volume flush. The successful native receipt
includes the `CMD_UPDATE` result. The test audits the backing file before host
save or disposal, verifies that host save neither advances guest cycles nor
changes the image, and boots a new protected machine to read the saved file and
metadata. The protected readback leaves the whole-image hash unchanged.

Each large-volume guest requests 32 additional cache buffers through DOS
`AddBuffers`. The app session executes every CPU instruction and chipset frame;
host presentation pacing is omitted. These are correctness checks, not a
throughput acceptance benchmark. Full bitmap validation still occurs for each
mutation. Observed native phase timings on this host were:

| Volume | Write-phase cycles | Host elapsed | Fresh-read cycles | Host elapsed |
| --- | ---: | ---: | ---: | ---: |
| Ordinary free space | 6,455,125,470 | 365.61 s | 90,092,686 | 5.15 s |
| Lower four GiB occupied | 9,235,493,188 | 684.62 s | 90,092,686 | 4.10 s |

Timings exclude fixture construction and the independent disk audits. Elapsed
times depend on host load; the evidence records frame counts and receipts too.

## Reproduction and retained evidence

From CopperScreen, supply the pinned ROM, the original qualified
`copperhdf-track.hdf` fixture and a new output directory:

```powershell
./scripts/test-copperstart-harddisk-writes.ps1 `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -HardDiskFixturePath C:\Disks\copperhdf-track.hdf `
  -OutputDirectory artifacts/ffs-bitmap-replay `
  -IncludeAllocationStress
```

This selects nine frontend cases and 26 boots, including both large-volume
writes, high-offset app/runner checks and protected controls. The accepted run
executed those same cases as separate groups of three and six. The other
22 native regression cases use the frozen producer/replay harness recorded in
the evidence. Local source snapshots, ROM composition, static analysis, TRX
results and disk observations remain under ignored
`CopperStart/artifacts/ffs-bitmap-20261018/`. No ROMs or media are committed and
no package is released. Installed-package qualification is outside this run.

The earlier [TD64 report](COPPERSTART_TD64_2026-10-18.md) retains its original
pin and results. This continuation qualifies the previously unfinished
large-volume writes and forced upper allocation; it does not establish support
for every representable FFS volume size or extended individual-file sizes.
