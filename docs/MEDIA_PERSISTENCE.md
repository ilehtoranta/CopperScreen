# Media protection, saves and recovery

P1-06 keeps guest execution on Lightweight. CopperStart and CopperOS use only
the admitted standalone ROM/system pair; this work adds no Legacy services or
host DOS path. Qualification results are in the
[2026-10-17 record](MEDIA_PERSISTENCE_VALIDATION_2026-10-17.json).

## Source qualification, 2026-10-17

| Check | Result |
| --- | --- |
| Locked restore and production solution Release build | Passed; zero build warnings/errors |
| Host suite | 263 passed; 12 optional entries unavailable in the default run |
| Desktop workflows, separate process with qualified pair | 11 passed, including recovery confirmation/cancellation and preservation |
| Focused engine hardfile/shared-folder storage diagnostics, isolated outputs | 41 passed, including open-file flush, reopen and unchanged guest cycle counts |
| Disk library | 74 passed |
| Native CopperOS storage replay | Three tests passed, seven boots; no skips |

The three CopperOS cases skipped in the default host run were exercised by the
separate native replay. Other optional native ROM/game/hardfile cases remain
unavailable coverage. Process interruption and malformed-backup cases passed.
Native restart, changed-install-directory readback and imported recovery backup
all retained the reviewed saved-image hash. Each run has its own recorded binary
hashes; final shipping-package qualification remains separate.

The subsequent [signed MSIX continuation](MEDIA_MSIX_VALIDATION_2026-10-17.md)
passes six installed native boots, media protection and backup/recovery checks,
including external backup restore after uninstall/reinstall. It records its own
version pair, binary hashes and updated diagnostic controls separately.

## Write contract

| Storage | Default and explicit permission | Persistence and export |
| --- | --- | --- |
| Standard 880 KiB ADF, including a selected ZIP entry | Newly imported media are write protected. Enable writes after insertion completes. Saved profiles retain their explicit write setting. | Guest writes change the in-memory disk. **Save ADF** publishes a complete `.adf`; replacing an existing destination requires confirmation. Loading a ZIP never changes the archive. |
| ADZ, DMS, extended ADF, IPF and SCP, including ZIP entries | Permanently read-only. | Save ADF is unavailable and attempts fail explicitly. No lossy conversion or writeback to these formats. SCP still uses the decoded first revolution. See [format limits](engine/DISK_FORMATS.md). |
| CopperHDF | Newly selected files and profile entries with no `ReadOnly` field are read-only. An explicit saved `ReadOnly:false` remains writable. Desktop `--hdf` / `--hardfile` now mean read-only; `--hdf-writable` / `--hardfile-writable` explicitly enable writes. | Writable mode changes the backing hardfile directly. The session owner flushes it on save/Stop/Restart/close. **Save ADF** does not export a hardfile. Make an offline copy before granting writes. |
| Shared folder | Newly selected folders are read-only; changing the path resets that choice. `--host-dir-rw` or explicitly clearing Read-only grants direct writes. | Guest file operations change host files and metadata directly. The owner flushes open writable streams before shutdown/restart. There is no volume snapshot/rollback/export. Back up the folder separately. See [folder limits](engine/HOST_DIRECTORIES.md). |
| CopperOS workspace | The hash-bound input seed is read-only. Creation copies it into a new, user-owned workspace; only that owned copy is writable. | Completed native writes are checkpointed, at most every 50 emulated fields. Stop/Restart/close drain bounded disk activity and save. A complete staged file is flushed before replacing `System.adf`; `Previous.adf` retains the preceding checkpoint. **Export workspace** produces a versioned `.copperos` backup. |

The hardfile flag change applies to the desktop app. The headless engine runner
has its own diagnostic command-line contract. CopperHDF and shared folders
require native Kickstart; they remain outside the bounded CopperStart/CopperOS
configuration. Loading media never formats or repairs it automatically.

A newly inserted disk cannot inherit the previous disk's writable setting,
including across the delayed swap. Save refuses another mounted disk's path,
managed workspace directories and application installation paths. Workspace
exports always require a new output file. ADF save never silently redirects to
the source when the chosen destination fails. Save ADF keeps the mounted source
path unchanged; select the exported disk explicitly for a later launch.

## Failure and interruption behavior

- Ordinary dirty ADFs remain in memory after a failed save or reset. Eject,
  replacement, Stop and Restart require saving or an explicit discard. The close
  dialog offers keeping the session open; discard is a user decision.
- A workspace save clears dirty state only after successful publication.
  Permission loss, missing storage or a failed staging write retain the last
  complete primary and its previous checkpoint. Retry after restoring access,
  or export the live workspace to a new backup on accessible storage. Export
  does not acknowledge the unsaved primary.
- Writable hardfiles and shared folders are **direct writes**. Keeping the
  session open after a flush error permits retry. Closing despite the error may
  lose buffered changes; it cannot undo changes already written. Their guest
  filesystems are not transactional. On an unclean stop, preserve an offline
  copy and use the native filesystem's validation/repair tools before further
  writes. A damaged volume is not automatically reset or replaced.
- Forced process termination before snapshot publication retains the complete
  old primary; termination after publication retains the complete new primary
  and the old primary as `Previous.adf`. Unpublished `.snapshot-*.tmp` files and
  `.creating-*` directories are ignored on reopen, never promoted as saves.
  Copy them elsewhere before manual investigation or deletion.

The process tests terminate a separate writer during partial staging, after
`Flush(true)` and after atomic publication. A separate injected I/O error models
disk exhaustion during staging. These establish host process-interruption
behavior, not physical power-loss durability, controller-cache guarantees,
network-filesystem atomicity or arbitrary guest filesystem consistency.
CopperOS admission checks image size, DOS1 root and allocation metadata; they
are not a full scan/repair of every guest file. The latest uncheckpointed guest
changes can be lost after a crash. Keep independent backups.

## Back up, import and recover CopperOS

While running, choose **More → Export workspace** to capture a validated,
quiescent image even if the primary location is unwritable. While stopped, select
the CopperOS boot method and a saved workspace, then expand **Back up or recover a workspace** and choose **Export saved
workspace** or **Export previous checkpoint**. Use a new filename outside app
data. Previous-checkpoint export remains possible when the primary is missing
or damaged. An older seed identity may be exported for a compatible app; export
does not grant permission to boot it.

To restore a portable backup, choose a matching CopperStart ROM, enter a **new**
workspace name under **Create a new workspace**, and choose **Import workspace
backup**. Import validates the version, seed pair/hash, image hash and filesystem
metadata before creating an owned workspace. It never replaces an existing
workspace and does not require the original seed file. Unsupported pairs need a
compatible app version; keep the backup intact.

To roll back the selected workspace, stop all sessions using it and choose
**Recover previous checkpoint**. Confirm that the older checkpoint may lose
recent files. Recovery takes an exclusive lease, validates the previous image,
preserves an existing current image as `Before-recovery-<id>.adf` and atomically
restores `System.adf`. `Previous.adf` remains intact. A missing/invalid previous
checkpoint produces an error and leaves current files untouched. Recovery never
recreates a seed or silently migrates the workspace.

Format 1 `.copperos` is a ZIP containing exactly `System.adf` (901120 bytes) and
`export.json` (at most 4096 bytes): `Format`, `SeedPairId`, `SeedSha256`,
`ImageSha256` and `Bytes`. Input archives are limited to 4 MiB. Import reads only
those entries into bounded buffers; it does not extract archive paths. SHA-256
detects accidental mismatch; the manifest is not a signature or proof of trust.
Treat imported operating systems as user-supplied content.

Same-family MSIX updates retain the established user-data root. Backups are
portable across user roots because import creates a new local identity. Export
before uninstalling or changing package identity: Windows may remove app data.
The previous [signed installed update evidence](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
remains evidence for its exact binaries. The
[P1-06 signed diagnostic pair](MEDIA_MSIX_VALIDATION_2026-10-17.md) now passes
installed update, export/import, recovery and reinstall restoration. Final
shipping-package and native file-picker walkthroughs remain release gates.
The [fresh WACK continuation](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md) reports
overall PASS on that exact signed `1.1.24.0` update, with all 13 required checks
passing and one optional blocked-executable FAIL retained. Earlier records remain
unchanged; this does not qualify a different final shipping package.

## Reproduce

```powershell
dotnet restore CopperScreen.slnx --locked-mode -m:1
dotnet build CopperScreen.slnx -c Release --no-restore -m:1
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests --filter "FullyQualifiedName~CopperHdfControllerTests|FullyQualifiedName~LightweightHostPacketTests|FullyQualifiedName~LightweightHostDirectoryTests"
```

Host tests include `MediaPersistenceTests` and the separately built
`CopperScreen.Storage.Probe`. The probe links production publication code with
diagnostic-only barriers; production compiles out their call sites. It is not a
package dependency or an app command-line feature. Desktop tests run in their
own process to preserve dispatcher isolation.

For the seven native CopperOS boots, set `COPPERSCREEN_COPPEROS_STORAGE` to the
frozen proof directory and `COPPERSCREEN_COPPERSTART_ROM` to its exact qualified
ROM, then run the host project with `--filter FullyQualifiedName~CopperOsStorageTests`.
For the desktop tests set `COPPERSCREEN_LAUNCH_ROM` and `COPPERSCREEN_LAUNCH_SEED`.
No producer ROM rebuild is needed. Missing native assets mean unavailable
coverage. These checks make no throughput or wider OS compatibility claim.
