# RDB FFS backup and restore across reinstall — 2026-10-19

**PASS:** 15 installed-process phases across 17 full-ROM boots, with no
failures or skips. Both boot-priority selections pass native writes, flushing,
process restart and fresh protected readback. Both exported disks and all six
settings files survive outside package storage and restore byte-for-byte after
Windows removes the original private data. The owned test installation is removed.
The [qualification receipt](COPPERSTART_RDB_BACKUP_RESTORE_2026-10-19.json)
records exact inputs, package hashes, coverage and deployment observations.

This procedure validates a manual whole-image backup, with the app closed,
and restoration after Windows removes the installed app and its private test
storage. CopperScreen had no dedicated HDF backup/export button at the time of this manual qualification.
Workspace/ADF export is a separate feature and is not used for this proof.

## Backup and restore procedure

1. Flush and close guest files, then close CopperScreen normally. Wait for its
   process to exit. Do not copy a running writable HDF as a consistent backup.
2. Copy the **entire HDF**, including its RDB, partition chain and both FFS
   partitions, to a folder outside the app's package data. Also copy the saved
   machine profile and any remembered-machine/preferences files you want to
   retain. Record hashes and verify the copies before uninstalling.
3. Uninstall the app. Package-private files are disposable; the external
   backup must remain available. Keep separately supplied firmware available.
4. Reinstall the app. Restore the HDF and profiles from the external backup.
   This qualification restores the same paths, under the same Windows user
   and package family. If moving files, select the restored image and firmware
   in app settings and save the profile with the new paths. Choose RDB layout;
   the image supplies partition geometry and boot priorities.
5. First open a protected copy and check both volumes and the expected boot
   volume. Enable writes explicitly on a working copy when needed. Preserve
   the backup itself.

Copying only an FFS partition does not preserve the disk's RDB or other
partitions. Copying only a profile does not back up disk contents.

## Installed package

The local diagnostic identity is
`CopperScreen.Test.5cc970f248af48fda36699d9ca2fd8af`, version `1.1.47.0`.
The signed MSIX SHA-256 is
`49A70F602627997D37655C9BA180BB3EB6A6774BFBF57CDC25222EA423ED5341`.
Both installations use that exact package and the same package family.
Neither is a development registration. Certificate trust is unchanged.

The app-code addition only allows the `post-restore` diagnostic scenario,
which selects separate test directories. It is excluded from normal production
assemblies. No production filesystem, device, emulator scheduling or ROM
behavior changes are part of this continuation.

## Installed validation protocol

The uniquely owned signed diagnostic MSIX is installed normally into
WindowsApps. Normal app startup, desktop runtime, audio and presentation own
the emulator. The observer reads snapshots and issues ordinary host commands;
it neither services guest DOS calls nor patches guest memory.

The owned fixture has two 3520-block FFS partitions: DH0 at byte 131072 and
DH1 at byte 1933312. With DH0 priority -128 and DH1 +5, SYS identity is 2.
The reversed RDB selects DH0 (identity 1) while mounting both partitions.
The current qualified ROM is `diskboot-2026-10-18-ffs-bitmap.1`, SHA-256
`C77E51BBBAB2D0F5EDFC12B59094707279B2D1BC44E57B585857F1B56763C23B`.

| Stage | Installed processes | Full-ROM boots | Requirements |
| --- | ---: | ---: | --- |
| Before uninstall | 4 | 5 | Both boot selections, two-volume native creation/writes/flushes, reset and process restarts |
| Restored readback | 4 | 4 | Restored remembered settings, two process restarts and two fresh protected boots from the exported images |
| Reinstalled writes | 7 | 8 | New two-volume writes/flushes, reset, restarts, fresh protected readback and protected-write control |
| Accepted total | 15 | 17 | Every process exits zero; all native receipts and backing images pass |

After all initial processes close, the controller exports two complete HDF
images and six settings files to `external-backup`, outside package storage.
All eight files must match their originals. It then calls Windows uninstall,
requires removal of the registration, and observes disappearance of private
test data **without deleting it itself**. The backup must survive unchanged.
The same signed package is reinstalled; private test files must still be absent
before restore and before any app activation. The controller restores from the
external backup and requires all eight hashes to match before launching.

Restored readback uses the actual restored profiles and disks. Fresh protected
machines copy the exported HDF bytes, without reconstructing saved guest files.
New-write tests use separate `post-restore` images, preserving the restored
disks and backup unchanged. They prove creation and flushing in the reinstalled
app; they do not claim that the existing-file probe rewrites restored files.

The unchanged MC68000 probe creates `Data/Saved` on both volumes, writes 2049
bytes, overwrites 32 bytes across byte 509, flushes, closes, reopens and reads
the entire payload plus EOF. It also sends ACTION_FLUSH to each filesystem.
Later boots must follow the existing-file branch on both volumes. RAM captures
must show the selected SYS identity, distinct handler ports and requests,
correct full payloads, successful packet replies and completed CMD_UPDATE
requests with zero error and actual length. The protected pristine control
must fail with DOS error 214 and leave its disk unchanged.

The app pauses and captures backing bytes before host save or disposal. Host
save must advance no guest cycles or change bytes. Reset must reach both new
readback receipts and complete both flushes. Independent disk inspection checks
FFS checksums, allocation, free counts, payloads and unchanged unrelated blocks.
Raw RAM/media captures are independently audited after the controller finishes.

## Reproduction and scope

Build a new uniquely owned signed diagnostic package supporting the
`post-restore` scenario. Use an already trusted certificate; these tools do not
change trust settings. No ROM, media or signing material is packaged here.

```powershell
./scripts/test-msix-rdb-reinstall.ps1 `
  -PackagePath C:\Packages\CopperScreen-1.1.47.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -OutputDirectory artifacts/rdb-reinstall-proof

python ./scripts/qualification/audit-msix-rdb-reinstall.py `
  artifacts/rdb-reinstall-proof --package-layout C:\Packages\Layout
```

The controller rejects a pre-existing registration of its target identity,
untrusted/non-diagnostic packages and incorrect inputs. It retains the backup,
RAM/media captures, profiles and deployment receipts before removing only its
owned test registration. The test does not establish a Store release,
cross-user/path migration, a backup wizard, interrupted-copy recovery,
power-loss durability or emulator performance/timing acceptance. Host semantic
tests, compiled MC68000 cases and full-ROM replays are reported separately.

## Accepted evidence and separate checks

The accepted run is retained under ignored
`artifacts/rdb-reinstall-installed-20261019/installed-accepted/`. It contains the
surviving external backup, eight-file hash manifest, all native RAM/media
captures, process/deployment records and final nine private test groups.
`completion-audit.json` confirms all 17 raw observations, both complete guest
payloads and device flush requests, all four fresh protected boots, and equality
of all eight exported/restored files before and after readback. Every intended
installed process exits zero. No failed attempts are included in these counts.

The Release solution builds with zero warnings and errors. Seven focused host
inspector/profile cases pass with no failures or skips; these are separate from
the native replays. No separate compiled MC68000 test cases were run in this
continuation: the full-ROM replays execute the existing qualified native HUNK.
The normal production assembly excludes the installed observer, while the signed
diagnostic assembly includes it. Source snapshots, hashes, build output and host
TRX results remain under `artifacts/rdb-reinstall-installed-20261019/`.

The subsequent [app settings qualification](COPPERSTART_RDB_APP_BACKUP_2026-10-19.md)
adds and validates dedicated export/restore controls and native file dialogs.
