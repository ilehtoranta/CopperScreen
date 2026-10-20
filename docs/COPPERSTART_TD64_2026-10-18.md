# CopperStart TD64 qualification — 2026-10-18

This historical report retains the earlier TD64 ROM and results. The subsequent
[FFS bitmap qualification](COPPERSTART_FFS_BITMAP_2026-10-18.md) uses a newer ROM
and passes the large-volume writes and forced upper allocation that remained
unfinished in this snapshot.

**TD64 high-offset reads and writes pass.** Partitions crossing or wholly above
4 GiB pass app and runner writes, flushing, restart and fresh protected readback.
A 4.125 GiB volume passes protected full-ROM boot and reads of files stored above
4 GiB. **In this snapshot, writes on volumes themselves larger than 4 GiB remained unqualified:**
bitmap traversal and allocation are slow. No NSD changes were made.

## Pinned artifact

* Qualification: `diskboot-2026-10-18-td64.1`.
* Raw ROM: 524,288 bytes.
* SHA-256: `199685f8aa9bebe7d1879f12e5c7d37a88b85ab7752fca48af30c9cedac97ad0`.
* Native core: 493,716 bytes; graphics: 30,099 bytes at `$FF8A00`.
* Machine: PAL OCS A500, 68000, 512 KiB Chip + 512 KiB slow RAM.

The [machine-readable evidence](COPPERSTART_TD64_2026-10-18.json) records test
names, input hashes, app/engine/runner binaries and source provenance. Producer
inputs, source snapshots and local ROMs remain under the ignored producer
`artifacts/td64-20261018/` directory. The accepted ROM is
`native-qualified/CopperStart-DiskBoot.rom`. Earlier overlapping compositions
were rejected and are excluded. Static analysis reports no managed allocation,
runtime helpers/features, external native targets or exception regions; the 11
arithmetic fault sites are retained.

The frozen producer retains RDB discovery, multiple FFS mounts, signed boot
priorities, DD/HD trackdisk and compact font data. It excludes unrelated
concurrent DOS/Exec provider edits. The ROM is a development artifact, not a
published package.

## Configuration and bounds

For example, this geometry places a 1,802,240-byte DOS1 partition above 4 GiB:

```text
--copperstart --rom <qualified.rom> --hdf-ffs-writable <existing.hdf> DH0:2:32:131076:131130
```

The native runner uses the same arguments without `--copperstart`. Use
`--hdf-ffs` for write protection. These options require an existing formatted
image and explicit inclusive cylinder geometry. Separate RDB configuration
remains available; the high-offset cases here use explicit partitions.

The old 4 GiB byte-extent restriction is removed from native mount selection and
host/app validation. Cylinder products are checked before multiplication against
unsigned 32-bit filesystem block counts and signed 64-bit host byte positions.
The backing image must contain the complete partition. These representation
bounds do not qualify every representable capacity. Individual-file size and
extended DOS seek APIs remain separate work.

## Accepted validation

| Coverage | Cases | Full-ROM boots |
| --- | ---: | ---: |
| App/runner writes, flushing, reset/replacement, process restart and protected controls | 6 | 21 |
| Protected 4.125 GiB volume boot and upper-file reads | 1 | 1 |
| HD drive geometry, 150 RPM, raw reads/writes and OFS/FFS/export regressions | 21 | 28 |
| DD DOS boot and native graphics | 6 | 6 |
| RDB priorities, flags, multiple mounts and both-partition write/readback | 13 | 14 |
| **Full-ROM total** | **47** | **70** |

Separately, 61 host checks pass: 16 filesystem geometry, 16 partition geometry,
16 app settings, 11 CLI configuration and two font/graphics composition checks.
Five compiled MC68000 cases pass, including TD64 writes and transfer splitting
at the 4 GiB boundary. There are no skips in the accepted groups. Both production
solutions build in Release; CopperStart reports two existing compiler nullable
warnings, and CopperScreen reports no warnings. Installed-package execution and
host throughput acceptance were not exercised.

The two small TD64 partitions are `DH0:2:32:131068:131122` (crossing) and
`DH0:2:32:131076:131130` (above). Tests restore profile and last-machine settings,
observe guest-flushed backing bytes before app host-save/disposal, and preserve
low-offset alias and edge sentinels. Runner tests use separate write, restart
and protected-read processes. Their receipts prove ACTION_FLUSH/CMD_UPDATE;
backing files are inspected after exit. Whole multi-gigabyte image snapshots are
not taken at each runner capture.

The large volume is `DH0:2:32:4:135171`: 8,650,752 blocks, 4.125 GiB. An independent
sparse formatter relocates startup and saved-file blocks above 4 GiB. A small
original guest HUNK calls DOS `AddBuffers("DH0:", 32)` before the unchanged
WriteProbe; native receipts verify success and the 32 extra buffers. The protected
replay drives complete app-session frames without host wall-clock pacing,
checks payload/metadata/bitmap accounting, and compares a streaming SHA-256 of
the complete image before and after boot. It is a read qualification.

## Reproduce

```powershell
./scripts/test-copperstart-harddisk-writes.ps1 `
  -RomPath <qualified-raw-ROM> `
  -HardDiskFixturePath <original-copperhdf-track.hdf> `
  -OutputDirectory <new-output-directory> `
  -IncludeTd64
```

This runs seven frontend cases across 22 boots. Without `-IncludeTd64`, the
original four-case/nine-boot suite remains. Optional `-RunnerPath` selects a built
runner. The producer fixture is hash-checked and never changed. Skips in the
selected qualification group fail the script.

## Unqualified large-volume writes

`-IncludeLargeVolumeWrites` adds the separate large-volume mutation/fresh-readback
case (eight frontend cases/24 boots if it succeeds). It is retained for the next
bitmap-performance change and has not passed this qualification.

The separate `Td64AllocationStressFillsLowerFourGiBAndWritesAboveIt` test is enabled
with `COPPERSCREEN_TD64_ALLOCATION_STRESS=1` and an explicit test-name filter. Real
filler files occupy all lower data blocks; its inspector walks their extension
chains and checks low-offset guards. It is excluded from the qualification script.

The initial stress run, without added buffers, was stopped after 600 seconds and
9,455,751,284 guest cycles in the first CreateDir mutation. The root had been
invalidated for allocation, with no native fault. A sparse-volume write run
reached its second mutation but was stopped while investigating cache thrashing.
Neither observation is a successful write replay. The current writer validates
all bitmap pages per mutation, repeatedly traverses their extension chains, and
searches used allocation keys individually. Those costs require further work
before large-volume writes can be described as qualified or practical.
