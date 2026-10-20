# Automatic cleanup of interrupted hard-disk copies - 2026-10-20

**PASS:** the installed app automatically removes its recorded, abandoned export
and restore copies on the next launch. Four actual process interruptions cover
partial and flushed exports/restores. All four next-startup cleanups pass. Existing
disks, saved settings, backups, published restores and untracked lookalikes remain
byte-identical. Five full-ROM boots and the independent raw-memory/media audit
pass, including fresh-process readback of both FFS partitions after cleanup.

The [qualification receipt](COPPERSTART_HDF_BACKUP_CLEANUP_2026-10-20.json)
records exact inputs, package identity, source hashes and evidence. The production
Release build has zero warnings/errors. Thirty-three focused host tests and four
desktop tests pass with zero skips. Separate compiled MC68000 cases: zero.

## Ownership and cleanup

Each Windows backup/restore creates a fresh GUID journal under the machine's
HardDiskBackupOperations directory. Its exclusive open handle is the operation
lease. The header records the exact unpublished path; flushed append-only records
capture the Windows volume/file IDs of the temporary archive, staging directory
and each staging file before image bytes are copied. A successful publication
moves the object out of its temporary path and removes its journal.

Cleanup runs at normal app startup and before another export/restore. It requires
an unlocked, complete, valid journal and matching file identities at the recorded
temporary path. Deletion uses those same verified handles. Restore cleanup opens
and validates every present child before deleting any; it never recursively deletes
a directory. Native opens support extended Windows paths, including extended UNC
syntax. Restore's unpublished profile is rewritten in place so its recorded file
identity remains valid.

Active leases, replaced files/directories, directory links, unknown children,
corrupt/truncated records, missing identity proof and inaccessible destinations
are deferred. No age threshold or filename pattern alone authorizes deletion.
Untracked temporary files from older versions are preserved. Published backups
and restored directories are outside the recorded temporary-path contract. A
filesystem that reports unsupported file identity queries can still export;
interrupted copies without identity proof are left alone. The qualified storage
host is Windows/NTFS; automatic identity cleanup is disabled on other operating
systems, while current-operation exception cleanup remains in place.

## Validation

| Installed interruption | Barrier | Next launch |
| --- | --- | --- |
| Partial export | After 262144 image bytes copied | Remove temporary archive and journal |
| Flushed export | Archive flushed/closed, before publication | Remove complete temporary archive and journal |
| Partial restore | After 262144 image bytes copied | Remove owned staging files/directory and journal |
| Flushed restore | Images/profile flushed, before publication | Remove complete owned staging and journal |

All four use the actual settings buttons and Windows file pickers in a signed,
non-development MSIX. Diagnostic barriers block the background operation and the
controller kills only its recorded installed PID. It retains copies/hashes of
the abandoned artifacts and journals before launching the next process. Startup
counts are exactly 0, 1, 1, 1, 1 cleaned and zero deferred in the accepted run.
All five processes load binaries matching the immutable package layout.

The controller compares disk.hdf, profile.json, last-machine.json and
recent-session.json with their baseline after every interruption and final
recovery. Independent sentinels cover an untracked export lookalike, an untracked
restore directory and a published restore with its saved profile. All survive.
The input backup remains SHA-256
`04C02C602A060601C986A94E90540B08DDC1528A46B42744683B4BFC8F4CCCAC`.

Host regressions separately cover active leases, replacement of roots/children,
unknown contents, open child handles, a real directory junction, stale records
after publication, invalid/incomplete/missing identity records and paths exceeding
MAX_PATH. The desktop startup test constructs the real MainWindow, cleans an
abandoned export and preserves an active export, original disk/settings/history
and an existing backup. The focused suite also retains positive backup/restore,
integrity/rollback and RDB fixture-inspector checks.

Every accepted process boots the complete pinned standalone ROM with the existing
native HUNK, no floppy, no host DOS service and no guest-memory patch. Default
boot priorities select SYS identity 2. The independent audit reads raw chip/slow
RAM to verify both 2049-byte Data/Saved payloads and their patch at byte 509,
distinct partition handlers/requests, successful ACTION_FLUSH replies and
completed CMD_UPDATE requests with zero error/actual length. Every whole-media
capture equals the original image, SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.
The final installed process reads both partitions after the last cleanup and
exits normally. No new guest writes or boot-priority changes are claimed here;
their earlier qualifications retain their separate scope.

## Evidence and reproduction

Accepted evidence is ignored artifacts/hdf-backup-cleanup-20261020/installed-final/,
with inputs-final/, package-final/, source snapshots, build logs and host/desktop
TRX records alongside it. The package is version 1.1.56.0, identity
`CopperScreen.Test.f4d7f31a59984410a0834fac45e68b6e`, MSIX SHA-256
`3F5D5457A5C2C61022E304D2BE038133ED7EBE95DE262B10F052BF761E31D85A`.
It uses the current diskboot-2026-10-19-ffs-td64-frontend.1 ROM, SHA-256
`D21E5223150A8837739E08ACC65AF52F483B5789C077A32E071F824BC14B7A86`.
The firmware pin was not changed by this task. Production metadata contains
neither installed proof entry points nor the backup observer field; diagnostic
metadata contains both. Both owned diagnostic registrations were removed after
retaining evidence. No package was released.

```powershell
./scripts/test-msix-hdf-backup-cleanup.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.56.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -InputBackupDirectory artifacts/cleanup-inputs `
  -OutputDirectory artifacts/cleanup-proof

python scripts/qualification/audit-hdf-backup-cleanup.py `
  artifacts/cleanup-proof --inputs artifacts/cleanup-inputs `
  --package-layout C:\Packages\Layout
```

Supply the qualified existing backup as existing-backup.copperhdf in fresh owned
inputs. Use a fresh signed diagnostic identity and an already trusted certificate;
the tools never change trust. Read each report's ioCaseReady and select its path
through the actual settings button/picker. The controller interrupts four phases,
automatically launches subsequent phases and retains/removes only its owned data
and registration. The audit checks retained pre-cleanup copies, journal contents,
final absence of owned temporary artifacts/records, preserved hashes and raw
native readback independently of the app's pass flags.

The initial installed package 1.1.55.0 exposed a real long-path defect: .NET could
create restored files beyond MAX_PATH, while native cleanup opens returned path
not found. Cleanup safely deferred the restore. A later controller assertion and
sharing violation prevented that run's final retention/removal block completing.
Its private data and failed record were retained separately before removing the
owned registration. Extended-path opens and host regressions fix the product
defect; the controller now retries evidence-file sharing conflicts and saves its
record even if retention fails. The final qualification restarts all five phases
with fresh inputs and a new immutable package. Initial evidence is excluded from
the accepted counts. An early host run omitted the fixture environment variable;
the accepted 33-case run explicitly supplies it and has zero skips.

This extends the earlier [I/O interruption qualification](COPPERSTART_HDF_BACKUP_IO_2026-10-19.md).
Physical-volume exhaustion, power-loss durability, non-NTFS/remote filesystem
qualification, Store certification and timing/performance acceptance remain
outside this result. No ROMs, media, local artifacts or credentials are committed.
