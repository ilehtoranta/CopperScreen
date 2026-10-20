# Personal profiles, presets and settings

Implementation record: 2026-10-16, first slice of
[productization task P1-01](PRODUCTIZATION_ROADMAP.md#p1-01--separate-application-files-and-user-data).
This changes host-side configuration storage; it does not change emulated hardware
or qualify Microsoft Store installation/update behavior.

Storage correction, 2026-10-17: packaged apps explicitly select
`%LOCALAPPDATA%/Packages/<package-family>/LocalState/CopperScreen/` through the
current Windows package family. Portable/development builds retain
`%LOCALAPPDATA%/CopperScreen/`. The table below uses the portable root; substitute
the package-owned root for an MSIX installation. Explicit test/user directory
overrides retain their existing behavior. Profiles, preferences, workspaces and
logs share this root.

The first signed diagnostic run showed that relying on AppData virtualization
could leave new files in an existing shared portable folder after uninstall.
The app now selects LocalState directly. Same-family updates retain that root;
uninstall/reset can remove it. Export valuable guest work to a user-selected
location before uninstalling or changing package identity. Existing portable
data is preserved separately and is not silently imported into an MSIX family.
See Microsoft's [package identity API](https://learn.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefamilyname)
and [app-data lifetime](https://learn.microsoft.com/en-us/windows/apps/develop/data/store-and-retrieve-app-data).

## Storage contract

| Content | Windows location | Behavior |
| --- | --- | --- |
| Bundled presets | Application `Presets/` directory; source in `CopperScreen/Presets/` | Read-only defaults copied into build/publish output. Saving never changes these files. |
| Personal profiles | `%LOCALAPPDATA%/CopperScreen/Profiles/` | Save creates or updates the named personal profile. Save a copy chooses an unused name. |
| Last machine | `%LOCALAPPDATA%/CopperScreen/last-machine.json` | Remembers the committed machine for launch without arguments. |
| Host preferences | `%LOCALAPPDATA%/CopperScreen/recent-session.json` | Remembers volume, mute and bounded recent-disk history. The previous location and format remain compatible. |
| Migration receipts | `%LOCALAPPDATA%/CopperScreen/Migrations/` | Records successful profile imports for each legacy installation directory. |

Settings > Profiles identifies bundled presets and personal profiles separately
and shows the personal profile folder. Both remain selectable when they share an
ID. Saving a bundled preset creates a personal profile with that ID. Loading by
ID, including the default profile and command-line `--profile`, prefers a personal
profile; an explicit preset path selects the original. Save a copy avoids names
used by either catalog and does not replace an existing file.

Profile file names cannot contain folder paths, traversal, Windows device names
or reserved file-name characters. Save operations validate before replacing a
profile, write a uniquely named temporary file in the destination directory,
flush it and rename it into place. Incomplete temporary files are not catalog
entries. Permission failures leave prior files intact and are reported to the
caller/UI; no fallback writes into the application directory.

Known ROM, disk/archive-entry, hardfile, shared-folder and custom sound-pack paths
are resolved using the existing launch path rules before saving a personal
profile or remembered machine. This retains their original location when the
application directory changes. It does not move or redistribute the media.

## Existing portable profiles

At desktop startup, the application looks for the nearest legacy `Profiles/`
directory using the existing installation/development-directory search pattern.
Migration is a copy operation and never removes or edits the originals.

- Files identical to the current bundled preset text are left as bundled defaults.
  Older or customized preset files are conservatively imported as personal profiles.
- Valid custom profiles are imported into the user folder. Unknown JSON properties
  are preserved, while known relative content paths are resolved before copying.
- An existing personal profile is never overwritten. Different content with the
  same ID is kept separately with a deterministic `-imported-<source hash>` ID and
  an `(imported)` display name. Retrying does not replace an already imported copy.
- A receipt is written only after the directory finishes without errors. Invalid
  files or unavailable folders produce a message; repair the source/permissions
  and restart to retry. Successfully imported files are retained during retries.
- Once a directory has a receipt, later changes in that legacy folder are not
  imported automatically. Open an explicit JSON profile and save it to import a
  later change. Keep the original portable folder until its imports are reviewed.

Migration cannot discover a portable installation that is no longer present or
outside that search path. Existing portable profiles should be migrated before
removing the old installation. A future Store migration workflow must account
for installing to a different location.

## Verification and remaining release coverage

The new `CopperScreenProfileStorageTests` exercise personal overrides and explicit
preset selection, copy collisions, two independent user roots, simulated package
directory replacement, relative paths, legacy conflicts/retries, malformed files,
interrupted writes and unavailable destinations. Windows ACL tests deny writes
to the application directory and to the user directory, verifying successful
user-data persistence in the former and recoverable failure in the latter.
Temporary paths include spaces and non-ASCII characters. Existing profile-save
fixtures use isolated user roots rather than writing to the developer's account.

Release validation on 2026-10-16 uses isolated production outputs under
`artifacts/productization-p1-01/` because the ordinary Release build encountered
runner assemblies held open by existing emulator processes. Those processes were
not interrupted. The isolated solution build succeeds with zero warnings/errors.
Engine diagnostics retain their separate `artifacts/diagnostic-tests/` outputs.
Local logs are ignored build artifacts under `artifacts/productization-p1-01-*.log`.

| Check | Result |
| --- | --- |
| Release solution build with isolated outputs | Passed; zero warnings/errors. |
| Host tests, including 25 profile-storage cases | 220 passed; 6 optional native-media cases skipped. |
| Disk tests | 74 passed; no skips. |
| Separate engine diagnostic tests | 1,241 passed; 7 optional native-media cases skipped. |
| Relocated preset content | All 23 retain their tracked content after Git line-ending normalization. |

Skipped optional cases are unavailable native coverage, not successful replays.

The subsequent [2026-10-17 signed MSIX validation](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
passes installed restart/update persistence for profiles, preferences and native
CopperOS workspaces, plus clean uninstall/reinstall after the explicit LocalState
correction. Separate Windows account checks, clean-machine acceptance and
installation-relocation migration remain open. No native gameplay or throughput
acceptance is claimed by the host storage tests.

The later [WACK/DPI continuation](MSIX_WACK_VALIDATION_2026-10-17.md) repeats actual
signed restart/update and clean reinstall checks on diagnostic `1.1.12.0`, with
unchanged profile/settings bytes and verified zero process exit codes. The earlier
five-second exit-timeout failure remains retained evidence; the installed harness
now records its explicit 30-second exit allowance and observed exits.

## CopperOS workspaces

CopperOS keeps its user-owned `System.adf`, format-1 identity, previous checkpoint
and exclusive lock under `CopperOS/Workspaces/<id>` in the same per-user data root.
Profiles and recent settings retain its absolute path and writable flag. Package
seeds stay immutable, and an update never substitutes a fresh seed for an existing
workspace. Exact pair admission, native checkpointing and export/recovery are
defined in [CopperOS storage](COPPEROS_STORAGE.md).
