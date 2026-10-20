# RDB FFS persistence across an installed update — 2026-10-19

**PASS:** 15 installed-process phases across 17 full-ROM boots in the accepted
run, with no failures or skips. Both RDB images and all six saved settings files
remain byte-identical across the update and its readbacks. New writes and
flushing in the updated app pass with either partition selected as SYS:.
The Release solution builds with zero warnings and errors.

This continuation validates a signed MSIX update with the same package identity
and publisher, preserving the installed app's private RDB images and settings.
The [qualification receipt](COPPERSTART_RDB_UPDATE_2026-10-19.json) records exact
packages, deployment state, persisted-file hashes and native observations.

## Packages and deployment

The baseline is the previously qualified installed diagnostic package
`CopperScreen.Test.ef78ae9aea114bc5a18b56220a60c510`, version `1.1.45.0`,
SHA-256 `F17FBD0ABB7D3F7FFA3C9743C830D0E1B085D094ED174A919638A90380FD93B0`.
Its update uses the same identity, publisher and architecture, version `1.1.46.0`,
SHA-256 `0604916E9D0AAC5799895ECDAC62CC600191EB4506F82E49AA80763394807F42`.
Both are signed local diagnostic packages, with no Store release or certification.

The controller first installs the baseline into WindowsApps and runs native
writes, flushing and process restart with each RDB boot-priority layout. It
hashes both images and each group's profile, last machine and preferences:
eight persisted files in total. It retains a private-data snapshot before the
update. Windows then installs the newer package **without uninstalling or
re-registering the baseline and without rewriting its data on the host**.

The installed version and physical installation directory must change. The
package family and LocalState path must remain the same. All eight files must
be byte-identical immediately after deployment, before the updated app starts,
and after every updated-version readback and new-write check.

Deployment uses `Add-AppxPackage -ForceApplicationShutdown` after all observed
baseline processes have exited normally with code zero. This lets Windows
release remaining package activation resources. The attempt without that option
was rejected with `0x80073D02` (resources in use) and is retained separately;
it is not counted as a successful update. No other package is removed.

The controller also retains an earlier process-path observation failure. It
now waits briefly for a newly activated process's executable metadata while
retaining the original process handle. It still rejects a wrong executable
or an unsuccessful process exit.

## Native requirements

Both versions use the current `diskboot-2026-10-18-ffs-bitmap.1` ROM, 524288
bytes, SHA-256
`C77E51BBBAB2D0F5EDFC12B59094707279B2D1BC44E57B585857F1B56763C23B`.
The machine is PAL OCS A500, 68000, 512 KiB Chip plus 512 KiB slow RAM, no
floppy medium, RTC, Fast RAM or shared folders. The normal installed desktop
window, runtime, presentation and audio paths own execution. There is no host
DOS service or guest-memory patch.

The owned fixture and original MC68000 probe are unchanged from the
[installed-app qualification](COPPERSTART_RDB_INSTALLED_2026-10-19.md).
DH0 begins at byte 131072 and DH1 at byte 1933312; each FFS partition contains
3520 blocks of 512 bytes. The default RDB has DH0 priority -128 and DH1 priority
+5, selecting SYS identity 2. The reversed variant swaps those priorities,
selecting SYS identity 1 while both partitions remain mounted.

The native command creates `Data/Saved` on both volumes, writes 2049 bytes,
overwrites 32 bytes across the sector boundary at byte 509, flushes and closes
the stream, reopens, reads all bytes and checks EOF. It sends `ACTION_FLUSH`
to each filesystem. Later boots must take the existing-file readback branch.

| Stage | Processes | Reset-ROM boots | Required result |
| --- | ---: | ---: | --- |
| Baseline writes and restarts, both boot layouts | 4 | 5 | Two-volume writes and guest flush before host save; default reset; two separate process restarts |
| Updated app reads preserved data, both layouts | 4 | 4 | Remembered-machine restart plus fresh protected boot for each SYS selection |
| Updated app creates new results, both layouts | 7 | 8 | New writes/flushes, reset, process restarts, fresh protected boots and a write-protection control |
| Total | 15 | 17 | All intended processes exit zero and all native observations pass |

Updated-version new-write cases use separately named, package-private test
images. Their diagnostic scenario does not replace or mutate the baseline
images that crossed the update. This exercises actual native creation and
flushing in the updated binary while retaining the migrated images unchanged.
Fresh boots copy the pre-host-save exports; they do not reconstruct guest files.

RAM snapshots check SYS identity, both branch receipts, both complete payloads,
distinct handler ports and requests, successful flush replies and completed
`CMD_UPDATE` requests with zero error and actual length. The observer pauses
execution and captures the open backing file **before host save or disposal**.
Host save must advance no guest cycles and leave the captured bytes unchanged.
Reset must reach both new readback phases and completed device flushes.

Independent FFS inspection checks allocation, free space, metadata checksums,
payloads and unchanged unrelated blocks. The whole saved image must match
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`
for default priorities and
`10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617`
for reversed priorities. The protected pristine control must return error 214
and retain its pristine hash. Each process must be non-elevated and load the
app, engine, disk and CPU binaries from the expected installed version.

## Reproduction and limits

Build two uniquely owned, signed diagnostic packages with the same identity,
publisher and architecture and increasing versions. The baseline must contain
the earlier installed RDB observer; the update must also support the separate
`post-update` diagnostic scenario. Use an already trusted signing certificate;
the tooling does not change trust settings. Then run:

```powershell
./scripts/test-msix-rdb-update.ps1 `
  -BaselinePackage C:\Packages\CopperScreen-1.1.45.0-win-x64.msix `
  -UpdatePackage C:\Packages\CopperScreen-1.1.46.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -OutputDirectory artifacts/rdb-update-proof
```

The script rejects pre-existing registrations, untrusted packages, mismatched
identities/publishers/architectures, non-increasing versions and incorrect
inputs. It retains this run's private media, profiles and logs outside package
storage before removing only its owned registration. All media, ROMs, packages
and signing material remain outside source control.

Accepted evidence is retained under ignored
`artifacts/rdb-update-installed-20261019/installed-accepted/`, including the
before-update private-data copy and the final nine fixture/profile groups.
An independent raw RAM/media audit verifies all 17 native observations and
compares every one of the eight persisted files in both retained snapshots.
The owned test registration is removed. The earlier failed attempts remain in
`installed/` and `installed-final/` and are excluded from accepted counts.

The only added app code is diagnostic scenario selection, excluded from normal
production builds. This continuation has no new host-semantic or compiled
MC68000 cases; native update replays are separate from the earlier host and
producer qualifications. It does not establish arbitrary future schema
migrations, uninstall/reinstall recovery, Store certification, power-loss
durability or performance/timing acceptance.

The subsequent [backup and restore continuation](COPPERSTART_RDB_BACKUP_RESTORE_2026-10-19.md)
separately exercises an actual uninstall/reinstall with external whole-image
backups. It does not change the scope of the update-only evidence above.
