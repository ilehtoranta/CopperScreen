# Hard-disk backup/restore failure handling - 2026-10-19

**PASS:** nine failure attempts through the installed app's actual settings
buttons and Windows file dialogs. The existing two-partition RDB disk, saved
configuration, committed settings, unsaved form edits and input backups remain
unchanged. No failed restore leaves staging files or publishes working disks.
A fresh runtime boots the full ROM and reads both FFS partitions successfully.
The independent retained-file and raw native-capture audit passes.

Eighteen focused host checks pass with zero failures or skips. The production
Release build has zero warnings and errors. No production backup/restore or
native implementation change was necessary. The added code is diagnostic
observation, regression coverage and reproduction tools. Exact hashes and
evidence paths are in the [qualification receipt](COPPERSTART_HDF_BACKUP_FAILURES_2026-10-19.json).

## Installed app cases

The signed diagnostic MSIX runs non-elevated from WindowsApps with the normal
settings controls, runtime and backing storage. Computer Use presses the actual
Export/Restore buttons and completes native file pickers. The observer waits
for input; it does not call backup/restore on the user's behalf.

| Attempt | Expected visible failure | Result |
| --- | --- | --- |
| Export while paused | Stop the Amiga before backup/restore | PASS |
| Restore while paused | Stop the Amiga before backup/restore | PASS |
| Export while running | Stop the Amiga before backup/restore | PASS |
| Restore while running | Stop the Amiga before backup/restore | PASS |
| Restore with one changed disk-image byte | Disk image integrity check | PASS |
| Restore with one changed configuration byte | Configuration integrity check | PASS |
| Restore archive missing its final 32 bytes | End of Central Directory missing | PASS |
| Restore a half-length image in a valid archive | Image length does not match backup | PASS |
| Export to an existing valid backup | Choose a new filename; existing backups preserved | PASS |

The image/configuration corruption and short-image fixtures have valid ZIP
CRCs and retain the original manifest. They exercise the backup's own hash and
length checks. The raw truncated archive separately exercises ZIP parsing.
The existing backup is a byte-identical copy of the earlier qualified
app-exported default-priority backup; the original is never modified.

Before every attempt, the observer serializes the entire editable settings
draft and committed configuration. An uncommitted description edit must survive
each failure. Active cases also require the same runtime reference and pause
state. Stopped cases must remain stopped. Each error must be visible in the
settings footer; rendered error screenshots are retained.

Every non-log file under the owned machine directory is checked by complete
relative-path set and SHA-256 after every attempt: disk.hdf, profile.json,
last-machine.json and recent-session.json. Unexpected new files fail the check.
All input backups are hashed too. HardDiskRestores must be absent or empty,
including hidden staging directories. Before/after copies and retained final
package-private files are independently compared after process exit.

## Full-ROM readback and separate host coverage

One installed process performs two independent normal runtime starts: before
the failures and after the final attempt. The latter uses a new runtime with
fresh RAM and the same existing disk/settings. Both starts execute the pinned
standalone ROM and existing native RdbProbe HUNK, without a floppy, host DOS
service or guest-memory patch.

Both observations must read the complete 2049-byte Data/Saved payload and EOF
from DH0 and DH1, including the 32-byte patch at offset 509. SYS identity 2
confirms the default RDB boot-priority selection. Raw RAM checks require
distinct handler ports and I/O requests, successful ACTION_FLUSH replies and
completed CMD_UPDATE requests with zero error/actual length. The complete
live HDF, fresh-readback HDF and post-close disk all retain SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.

The focused host tests are separate coverage. Three added truncation cases
preserve an already published restore and saved settings when the archive,
first image or second image is truncated. The second-image case requires
cleanup after a complete first disk has already been staged. Together with
the existing backup and installed-inspector tests, all 18 pass with the owned
RDB fixture supplied. No separate compiled MC68000 test cases were run; the
full-ROM replays execute the unchanged native probe.

## Reproduction and retained evidence

Use a fresh signed diagnostic identity and an already trusted local certificate.
The tools never change certificate trust. Prepare invalid copies from the
qualified original app-exported backup:

```powershell
python scripts/qualification/prepare-hdf-backup-failures.py `
  artifacts/rdb-backup-app-20261019/installed/export/default.copperhdf `
  artifacts/failure-proof-inputs

./scripts/test-msix-hdf-backup-failures.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.52.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -InputBackupDirectory artifacts/failure-proof-inputs `
  -OutputDirectory artifacts/failure-proof
```

Read failureCaseReady in failures/failures.json for the next operation/path.
Click the corresponding settings button; active-session cases have no picker.
After normal Stop, use the four damaged backups in order and select the existing
backup as the last export destination. The observer then performs the fresh
runtime boot/readback. The controller retains private evidence and removes
only its uniquely owned package registration.

```powershell
python scripts/qualification/audit-hdf-backup-failures.py `
  artifacts/failure-proof --inputs artifacts/failure-proof-inputs `
  --package-layout C:\Packages\Layout
```

Accepted evidence is under ignored
`artifacts/hdf-backup-failures-20261019/installed-final/`, with inputs in
`inputs-accepted/` and the immutable diagnostic package in `package-final/`.
The identity is `CopperScreen.Test.0278af0ff27743489112adff143a275d`, version
1.1.52.0, package SHA-256
`15814B0C2B838882E1CD9A70EFCF95EFDFA2FC21EE32E9B4BC9065DF1F470C61`.
The run uses firmware `diskboot-2026-10-19-ffs-overwrites.1`, SHA-256
`4D59A7AEB53C87709C137500BEE50FC5E10DD96E833B779ABD1C3D4A86C6E46D`.
The firmware pin was advanced by concurrent work; this qualification does not
change it. The package's compiled firmware contract accepts that exact ROM.

Source snapshots/hashes, production build output, host TRX results, input
preparation metadata, package metadata, nine error screenshots, both raw RAM/HDF
observations and completion-audit.json are retained. The production assembly
excludes installed observers; the diagnostic assembly includes them. The
controller's legacy ResetRomBoots field counts the initial and fresh runtime
starts here; no in-place reset is used.

Two earlier installed attempts are retained as excluded diagnostic runs: the
first incorrectly compared changing logs, and the second compared an implicit
empty-floppy protection default against the normal settings form's explicit
default after restart. All disk/settings files survived the former; all nine
failure attempts and native readback passed in the latter, whose only saved
change was that normal default materialization. The final fixture makes those
four defaults explicit before its byte-preservation baseline and excludes only
logs. Both earlier owned package registrations were removed. An initial fixture
preparation attempt also failed from reused ZIP metadata; copying entry metadata
fixed it, and only inputs-accepted is used. An initial final-build host run
without the optional fixture skipped three tests; the accepted fixture-enabled
rerun passes all 18 with no skips.

This covers bounded backup validation and app failure handling. It does not
establish arbitrary damaged-filesystem repair, out-of-space or power-loss
durability, Store certification or performance/timing acceptance. The earlier
[positive backup/restore](COPPERSTART_RDB_APP_BACKUP_2026-10-19.md) and
[restored-write](COPPERSTART_RDB_RESTORED_WRITES_2026-10-19.md) qualifications
retain their separate scope. No ROMs, media or packages are committed or released.

The [I/O failure and interruption continuation](COPPERSTART_HDF_BACKUP_IO_2026-10-19.md)
adds injected disk-full errors, real unwritable destinations/settings, forced
termination and fresh readback. It also fixes restore committing its form when
the saved-machine write failed. Physical-volume exhaustion remains unqualified.
