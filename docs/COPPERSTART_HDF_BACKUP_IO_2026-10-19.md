# Hard-disk backup/restore I/O failures and interruption - 2026-10-19

Subsequent implementation and validation of recorded temporary-file cleanup is
documented in the [2026-10-20 cleanup qualification](COPPERSTART_HDF_BACKUP_CLEANUP_2026-10-20.md).
The results and limitations below describe the earlier package unchanged.

**PASS:** five graceful failures and four forced process terminations through
the installed app's actual settings buttons and Windows file dialogs. The
existing RDB disk, saved settings and existing backup remain byte-identical.
Ordinary failures also preserve unsaved form edits and remove their new copies.
Fresh-process full-ROM readback of both FFS partitions and the independent
retained-file/raw-native-capture audit pass.

Disk-full coverage injects Windows ERROR_DISK_FULL after 256 KiB has actually
been copied; it does not fill a physical volume. Access denial and process
termination are real Windows operations. Forced termination can leave unpublished
temporary files. Automatic orphan cleanup is not implemented or claimed: those
files never become configured disks or replace a backup, and the preserved
machine boots while they remain.

The [qualification receipt](COPPERSTART_HDF_BACKUP_IO_2026-10-19.json) records
exact inputs, source hashes and evidence. Nineteen focused host tests and three
desktop tests pass with zero skips. The production Release build has zero
warnings/errors. Separate compiled MC68000 cases: zero. Native coverage is seven
full-ROM boots in six accepted installed-process phases.

## Restore fix

A desktop regression reproduced a production bug: when last-machine.json could
not be replaced, restore still selected its new working disks and could show
success. RememberCurrentSession reported the error without aborting, after the
settings form had committed the new draft.

Restore now persists the selected machine as one atomic settings record before
updating the form. Volume/mute and recent-floppy history remain unchanged. If
persistence fails, the error reaches the visible failure handler and the backend
removes only its newly created restore directory. Existing images, published
restores and settings remain intact. The backend regression checks rollback beside
an already published restore. Desktop coverage checks blocked settings-file
replacement, committed settings, original files, cleanup and successful restore
persistence.

Export still writes a fresh temporary archive, flushes it, and moves it without
replacement. Restore still validates the manifest/hashes/lengths, copies to a
fresh GUID staging directory and flushes files before publication. Test barriers
and error injection are compiled only into diagnostic builds; production excludes
them. No firmware, filesystem, device or engine behavior changes are required.

## Installed cases

| Case | Failure point | Required result |
| --- | --- | --- |
| Export disk full | After 262144 image bytes copied | Visible 80070070; temporary archive removed |
| Restore disk full | After 262144 image bytes copied | Visible 80070070; staging removed |
| Export unwritable destination | OS denies file creation | Visible 80070005; no export |
| Restore unwritable destination | OS denies staging-directory creation | Visible 80070005; no restore |
| Restore unwritable settings | OS denies atomic last-machine.json replacement | Visible 80070005; new copies rolled back; form unchanged |
| Interrupted export, partial | Kill after 262144 copied bytes | Temporary file stays unpublished |
| Interrupted export, flushed | Kill after flush/close, before destination move | Complete archive retains temporary name |
| Interrupted restore, partial | Kill after 262144 copied bytes | Partial staging stays unpublished |
| Interrupted restore, flushed | Kill after image/profile flush, before directory move | Complete staging stays unpublished |

All nine pass. Folder-denial cases modify only owned fixture DACLs at the backend
opening barrier after the picker returns. Finally imports and verifies the exact
original access rules. The settings case holds a real read lease that prevents
replacement, after all restored files are flushed. Error codes, reached barriers,
copied-byte counts and visible error screenshots are retained.

Every graceful case compares the complete editable draft and committed settings,
plus the relative-path set/hashes of every non-log machine file. It requires no
published restore, no retained staging and an empty export destination. Every
interruption records its barrier before the controller kills that uniquely owned
PID. The controller then hashes protected files and inventories temporary copies.

## Readback and independent audit

The owned RDB fixture contains two FFS partitions with existing 2049-byte
Data/Saved files. Default priorities select SYS identity 2. All accepted boots
use the full pinned standalone ROM and existing native HUNK, with no floppy,
host DOS service or guest-memory patch. The graceful-error phase boots before
the failures and again in a new runtime afterward. Each crash phase boots before
its operation. A final installed process boots the original remembered machine
after all four terminations.

The audit checks both payloads and the patch at byte 509, SYS identity, distinct
handler ports/requests, successful ACTION_FLUSH replies and completed CMD_UPDATE
requests with zero error/actual length. Every observed whole HDF equals the
original, SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.
All four protected files (disk.hdf, profile.json, last-machine.json and
recent-session.json) retain baseline hashes after recovery.

The audit also checks all interruption receipts and temporary-file hashes,
complete ZIP CRCs/image bytes at the export flush barrier, partial/complete
restore images and the exact unpublished-directory set. Loaded binary hashes
must match the signed package layout. The valid input backup remains SHA-256
`04C02C602A060601C986A94E90540B08DDC1528A46B42744683B4BFC8F4CCCAC`.

## Reproduction and evidence

Place a qualified original app-exported default backup in a fresh owned input
directory as existing-backup.copperhdf. Build a fresh signed diagnostic identity
using an already trusted certificate. These tools never change certificate trust.

```powershell
./scripts/test-msix-hdf-backup-io.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.54.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -InputBackupDirectory artifacts/io-proof-inputs `
  -OutputDirectory artifacts/io-proof
```

Read ioCaseReady in each phase report, click the corresponding settings button
and choose its path in the native picker. The controller terminates the expected
processes at their barriers, retains private evidence and removes only its owned
registration. Audit independently:

```powershell
python scripts/qualification/audit-hdf-backup-io.py `
  artifacts/io-proof --inputs artifacts/io-proof-inputs `
  --package-layout C:\Packages\Layout
```

Accepted evidence: ignored artifacts/hdf-backup-io-20261019/installed-complete/,
with inputs-final/, package-final/, source snapshots/hashes, build logs and host/
desktop TRX files alongside it. Identity:
`CopperScreen.Test.8d5788917fba4b63b1f2473277245c25`, version 1.1.54.0,
MSIX SHA-256 `79B7B413BD09A020AA945BA1B63A29D3BEDEF11CD5EC053722EE20417FA3218A`.
Firmware remains diskboot-2026-10-19-ffs-overwrites.1, SHA-256
`4D59A7AEB53C87709C137500BEE50FC5E10DD96E833B779ABD1C3D4A86C6E46D`.
The diagnostic publish has 12 CA1416 warnings about its Windows-only ACL callback;
the production Release build is clean. Owned registrations were removed.

Earlier evidence is retained separately. The original desktop regression failed
because restore swallowed the error. Its first post-fix assertion expected exactly
IOException; Windows returned UnauthorizedAccessException. The corrected test
accepts either filesystem error and verifies preservation. The first installed
fixture failed to restore its original DACL because an unmodified ACL object
does not mark sections for writing. Only its owned folders were repaired; the
final fixture explicitly restores and verifies the original SDDL.

The final package completed all five graceful cases and both export terminations,
but the controller's progress write collided with a status reader while starting
the next process. It retained evidence and uninstalled its owned package. Progress/
record writes now use atomic publication with bounded retries. The narrow
ResumeAfterExportEvidence option checked that exact failure and package, reinstalled
the same immutable identity, restored only its retained private copy and resumed
the two restore checks and final readback. The first three accepted reports and
snapshots were copied unchanged into installed-complete and audited with the
final three. Six accepted phases/seven observed boots exclude the extra aborted
activation and earlier diagnostic attempts. The original backup was not replaced.

This covers the tested exception/process-interruption behavior. Physical-volume
exhaustion, power-loss durability, automatic orphan cleanup, Store certification
and timing/performance acceptance remain outside this result. The earlier
[corrupt/truncated qualification](COPPERSTART_HDF_BACKUP_FAILURES_2026-10-19.md)
and positive-flow qualifications retain their separate scope. No ROMs, media,
local artifacts, credentials or package releases are committed.
