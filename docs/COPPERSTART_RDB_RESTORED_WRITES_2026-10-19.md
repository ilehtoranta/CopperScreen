# Writes and flushing on restored RDB FFS disks - 2026-10-19

**PASS:** six installed-process phases across seven full-ROM boots, with no
failures or skips. Both restored FFS partitions receive new native writes and
complete their flushes with either boot-priority selection. Re-export, process
restart and fresh settings restore/readback all pass. Original Saved files and
unrelated blocks remain unchanged. Fifteen focused host checks pass, and the
Release build has zero warnings and errors. The owned test package is removed.
The [qualification receipt](COPPERSTART_RDB_RESTORED_WRITES_2026-10-19.json)
records exact inputs, package hashes, coverage and retained evidence.

This continuation tests new native writes on working images created by the
app's Restore hard-disk backup button. It uses both RDB boot-priority layouts,
re-exports the written disks through settings, restarts the same working disks,
and restores each re-export into another new machine for fresh-boot readback.

## Discriminating write fixture

The earlier [settings qualification](COPPERSTART_RDB_APP_BACKUP_2026-10-19.md)
used a native probe that reads `Data/Saved` when it already exists. Repeating
that probe on its restored images would not establish new writes.

The new owned inputs derive from those exact app-exported backups. Preparation
changes only the two filename strings in the native RdbProbe file in each
partition: `DH0:Data/Saved` and `DH1:Data/Saved` become `Data/After` paths of the
same length. Exactly 16 image bytes change across both partitions. No native
instructions, file length, FFS allocation or metadata change. Both original
Saved files remain present with their full 2049-byte payloads. Backup manifest
image hashes are updated; the original backups are never modified. The complete
prepared bytes and hashes are retained. An independent C# inspector derives the
same input image before any guest execution.

After the app restores that input, the complete pre-boot image must equal the
prepared image. The host does not modify the restored disk. The full-ROM guest
must create `Data/After` on both volumes, write 2049 bytes, seek to byte 509,
write another 32 bytes across the sector boundary, flush, close, reopen, read
the full payload and verify EOF. It must also send ACTION_FLUSH to each handler.
The two native phase-1 receipts distinguish creation from the phase-2 readback
branch. Six newly allocated blocks per partition and the actual disk payloads
provide independent evidence of writes.

All original Saved header/data blocks and every unrelated partition block must
remain byte-identical. FFS root, directory, file and bitmap checksums, six-block
free-space deltas, allocated bits and unique file blocks are checked. RDB and
unpartitioned regions must remain unchanged.

## App protocol

A new signed diagnostic MSIX runs normally from WindowsApps, non-elevated,
using the app desktop, audio, presentation and runtime. Computer Use clicks the
actual settings buttons and completes the Windows Open/Save dialogs. There is
no host DOS service or guest-memory patch. The controller never copies files
into a restored working image.

For each priority layout:

1. Restore the prepared input using Settings > Storage > Hard disks.
2. Capture the complete restored image before starting. Boot the current pinned
   full ROM and require new writes plus complete guest flushes on both volumes.
3. Pause and capture live backing bytes before host save or disposal. Host save
   must advance no guest cycles or change the bytes. The default layout also
   resets and must reach both new readback receipts and complete both flushes.
4. Stop normally, then re-export through the settings button and native Save
   dialog. The embedded HDF must equal the live, guest-flushed image.
5. Start another installed process using remembered settings, with no launch
   arguments. It must use the same restored working image and read After on
   both partitions through the phase-2 branch.
6. In a fresh machine, use the settings button and native Open dialog to restore
   the re-export. It must create a different image path. Capture before boot,
   then perform a fresh full-ROM phase-2 readback of both files and both flushes.

DH0 priority -128 / DH1 +5 selects SYS identity 2; the reversed layout selects
identity 1. Both partitions stay mounted. Each partition has 3520 blocks of
512 bytes, starting at 131072 and 1933312. The expected data is the existing
2049-byte pattern with the 32-byte patch at 509.

| Stage | Processes | Full-ROM boots |
| --- | ---: | ---: |
| Restore, new writes, reset and re-export, both priorities | 2 | 3 |
| Process restart on the same working disks | 2 | 2 |
| Fresh settings restore of each re-export and readback | 2 | 2 |
| Accepted total | 6 | 7 |

Raw snapshots check both payloads, SYS identity, distinct handler ports and
requests, successful packet replies and completed CMD_UPDATE requests with zero
error and actual length. Fresh readbacks require the entire image to match the
re-export. Exported machine settings must remain unchanged except rebased disk
paths. Prepared input backups remain unchanged throughout.

## Reproduction

Keep the exact prior app-exported backups available outside package data.
Prepare owned new inputs, leaving those source backups untouched:

```powershell
python scripts/qualification/prepare-rdb-restored-writes.py `
  artifacts/rdb-backup-app-20261019/installed/export artifacts/restored-proof-inputs

./scripts/test-msix-rdb-restored-writes.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.49.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -InputBackupDirectory artifacts/restored-proof-inputs `
  -OutputDirectory artifacts/restored-proof
```

Use a new uniquely owned signed diagnostic identity supporting the
`restored-writes` scenario and an already trusted certificate. The tools never
change trust. During the two restore-write phases, select the prepared input.
Re-export to `reexport/default.copperhdf` or `reexport/priority.copperhdf` under
the evidence directory. During fresh-restore phases, select that re-export.
Then inspect retained evidence independently:

```powershell
python scripts/qualification/audit-msix-rdb-restored-writes.py `
  artifacts/restored-proof --inputs artifacts/restored-proof-inputs `
  --package-layout C:\Packages\Layout
```

This is installed-app storage correctness for bounded owned RDB/FFS images.
The earlier uninstall/reinstall result remains separate; this continuation does
not repeat uninstall/reinstall between phases. It does not establish arbitrary
media recovery, power-loss durability, Store certification or timing/performance
acceptance. No ROMs, media or packages are committed or released.

## Exact inputs and accepted evidence

The signed local diagnostic identity is
`CopperScreen.Test.e0e295f7ae2545d7b1eea604bfe87e58`, version `1.1.49.0`,
MSIX SHA-256
`B1587590A7298CEA5FB2FE2015E8B29BAC90921F206060097CA4E143FFB046A9`.
The ROM remains `diskboot-2026-10-19-dos-large-files.1`, SHA-256
`4054184E5CBCD93511DA0C8C86B963955E670C959343DDDF31F2C324EA03A963`.
The preparation record identifies the exact earlier app-exported backups,
their image hashes, all 16 changed bytes' scope, and the prepared input hashes.

Accepted evidence is retained under ignored
`artifacts/rdb-restored-writes-20261019/installed/`. It includes every pre-boot
restored image, native RAM/HDF/window capture, both re-exported backups and all
four restored profile/image groups. `completion-audit.json` independently checks
both original Saved files, the new After payloads and allocation on each volume,
all seven raw native observations, packet and device flush completion, exact
re-export bytes and both rebased fresh restores. Every intended process exits
zero. Input backups remain unchanged. Source snapshots, hashes, build output,
input preparation metadata and host TRX results are retained alongside the run.

Host checks and full-ROM replays are separate coverage. No separate compiled
MC68000 cases were run: the existing native instruction stream is unchanged.
Only diagnostic scenario/inspection support, a discriminating fixture test,
qualification scripts and documentation are added. The normal production
assembly excludes the installed observer; the signed diagnostic assembly includes
it. No native ROM, device, filesystem, scheduler or production backup behavior
changes are needed for this continuation.
