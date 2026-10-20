# Hard-disk backup and restore in app settings - 2026-10-19

**PASS:** the real installed settings flow completed two exports and two
restores across uninstall/reinstall. Eight installed processes and nine full-ROM
boots passed, including both boot-priority selections, two-volume file readback
and restart from remembered new paths. Both HDFs are byte-identical to their
exports, and all exported configuration fields are preserved except disk paths.
Fourteen focused host cases and four desktop cases pass without skips. The final
Release build has zero warnings and errors. The temporary package is removed.
The [qualification receipt](COPPERSTART_RDB_APP_BACKUP_2026-10-19.json) records
exact inputs, hashes, deployment observations and the coverage boundary.

## App behavior

Settings > Storage > Hard disks now provides **Export hard-disk backup…** and
**Restore hard-disk backup…**, using the normal Windows Save/Open dialogs.
Stop the Amiga first. The normal Stop transition saves persistent storage and
releases the runtime. Export takes read leases on every source image, so an
active writable session in another process also prevents a backup.

A `.copperhdf` backup is a versioned ZIP containing `manifest.json`, the full
`profile.json` and `disks/unit-N.hdf` for every configured hard disk. Images
are streamed intact, including RDB/PART blocks, all partitions and unused space.
SHA-256 hashes cover the profile and every image; the manifest records image
lengths and units. No filesystem is rebuilt or formatted. Export flushes a
complete temporary file before publishing without replacement. Keep the backup
outside app data so Windows uninstall does not remove it.

Restore validates the version, entry set, image lengths and hashes, plus the
profile's disk-unit mapping. It writes into a fresh staging directory, then
publishes a new `HardDiskRestores/<id>` directory only after all checks pass.
It preserves configuration fields, including layout, explicit geometry and
write protection, and rebases each disk path to its new working copy. The app
selects and remembers that machine. The original images, other restores and
the backup remain untouched. A failed restore removes only its unpublished
staging directory. Restore does not start the guest automatically.

Firmware, floppy images, sound packs and shared-folder contents are not copied;
the profile retains their paths. Check those dependencies before starting.
Global recent-media history and master volume preferences are not part of the
machine profile. This feature is distinct from CopperOS workspace/ADF export.

## Installed protocol

A uniquely owned signed diagnostic package is installed into WindowsApps.
Normal desktop startup, presentation, audio and runtime own execution. The
native observer only reads snapshots and issues ordinary host commands; no
host DOS shim or guest-memory patches are used.

Native writes create `Data/Saved` on both FFS volumes, write 2049 bytes, overwrite
32 bytes across offset 509, flush and close, reopen, read the full payload and
check EOF. ACTION_FLUSH must complete independently on both filesystems, with
distinct handler ports and completed CMD_UPDATE requests with zero error and
actual length. Live backing bytes are captured before host save or disposal.
Host save must advance no guest cycles and leave the bytes unchanged.

The fixture has two FFS partitions, each 3520 blocks of 512 bytes: DH0 starts
at 131072 and DH1 at 1933312. Default priorities DH0 -128 / DH1 +5 select SYS
identity 2. The reversed image selects identity 1 while mounting both volumes.
The original probe and fixture remain unchanged.

| Stage | Processes | Full-ROM boots | Required result |
| --- | ---: | ---: | --- |
| App exports before uninstall | 4 | 5 | Native two-volume writes/flushes, default reset, both process restarts and two settings exports |
| App restores after reinstall | 4 | 4 | Two settings restores into new paths, full-ROM two-volume readback and two further process restarts |
| Accepted total | 8 | 9 | Both boot-priority selections and every guest payload/flush receipt pass |

The observer stops the session normally and opens Storage settings. Computer
Use clicks the actual export button and completes the native Save dialog for
each variant. The controller retains the original private files for comparison,
then uninstalls the package. Windows must remove its registration and private
test data without host deletion. Both external backups must survive unchanged.

The same signed package is reinstalled, with private test data still absent.
The controller does not copy any backed-up files into app data. Computer Use
clicks the actual restore button and selects each exported file in the native
Open dialog. The app creates new working disks and remembers their paths.
The native observer then starts the restored machine and captures full-ROM
readback; subsequent installed processes use remembered settings with no launch
arguments. The expected SYS selection must survive restoration.

## Reproduction and limits

Build a new signed local diagnostic package supporting the `app-backup`
installed scenario, using an already trusted certificate. The controller does
not change certificate trust or target other installed packages:

```powershell
./scripts/test-msix-rdb-app-backup.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.48.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -OutputDirectory artifacts/rdb-app-backup-proof
```

While each export/restore phase waits, use the settings button and native file
dialog. Save exports as `export/default.copperhdf` and `export/priority.copperhdf`
in the evidence directory; after reinstall, restore those exact files.
The retained progress/report files identify the expected phase. Then audit:

```powershell
python scripts/qualification/audit-msix-rdb-app-backup.py `
  artifacts/rdb-app-backup-proof --package-layout C:\Packages\Layout
```

Qualification is bounded to these two-partition RDB images on the same Windows
user with external firmware retained. It does not establish Store acquisition,
arbitrary corrupt-media recovery, power-loss durability or timing/performance
acceptance. Large images use streaming and ZIP64 support, but this replay does
not qualify multi-gigabyte backup throughput. No ROMs, media, packages or signing
material are committed or released.

## Exact inputs and retained evidence

The installed package is `CopperScreen.Test.bf75fb2633b74fc3899eeeb5df6e6d9b`,
version `1.1.48.0`, SHA-256
`3EB5EC866A0DA62F771F33C8FB29613F07D2D8992FC9BB3F337414E6714F342E`.
Both installations use that same signed package. Firmware is the current
`diskboot-2026-10-19-dos-large-files.1` pin, SHA-256
`4054184E5CBCD93511DA0C8C86B963955E670C959343DDDF31F2C324EA03A963`.
Default saved HDF SHA-256 is
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`;
reversed priorities produce
`10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617`.

Accepted evidence is retained under ignored
`artifacts/rdb-backup-app-20261019/installed/`: both exported `.copperhdf`
files, a before-uninstall private-data copy, final restored working copies,
process reports, native RAM/HDF/window captures and `completion-audit.json`.
The independent audit verifies the complete ZIP image bytes and manifest hashes,
all exported/restored profile fields, rebased paths and every raw native receipt.
The existing Settings reader makes empty floppy slots' default protection
explicit in the exported profile; the audit checks that exact normalization.
No floppy media is introduced. Backups remain unchanged after all readbacks.

Host/desktop cases are separate from full-ROM replays. No separate compiled
MC68000 test cases were run; the replays execute the existing qualified HUNK.
The first desktop harness searched an unopened settings page; opening Storage
corrected the harness, and all four final cases pass. After the installed run,
two UI exception filters were extended to report invalid numeric partition
fields without escaping the asynchronous button handler. A final Release build
and desktop regression cover that error-only change; version 1.1.48.0 is retained
unchanged as the installed positive-flow evidence. The backup implementation
and successful export/restore paths are unchanged.

The [restored-write continuation](COPPERSTART_RDB_RESTORED_WRITES_2026-10-19.md)
adds discriminating new writes on the restored working images, then re-export,
restart and fresh settings restore/readback for both boot priorities.

The [failure-handling continuation](COPPERSTART_HDF_BACKUP_FAILURES_2026-10-19.md)
validates corrupt/truncated backups, active-session rejection and export to an
existing backup, with byte preservation and fresh full-ROM readback.
