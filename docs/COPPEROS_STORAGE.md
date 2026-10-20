# CopperOS native storage and workspaces

P1-04 continuation. Lightweight remains the sole hardware/time owner; Legacy
and host OS shims remain unavailable. This is development work, not Store
shipping admission. The earlier inspection and read-only boot proof are frozen
historical records.

The [P1-06 signed installed continuation](MEDIA_MSIX_VALIDATION_2026-10-17.md)
qualifies media protection, backup/import, previous-checkpoint recovery and
external backup restoration after uninstall/reinstall on its recorded diagnostic
pair. The [fresh P1-06 WACK continuation](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md)
reports overall PASS on its exact signed update, with all required checks passing
and the optional blocked-executable finding retained. Final shipping-package and
interactive file-picker gates remain separate.

## Native storage

CopperStart supplies an original, serial DOS1 DD FFS mutation implementation.
Its DOS handler supports writable/update opens, file writes, truncation on output
open, directory creation, rename and deletion. File growth uses standard file
headers, extension pointer tables and the volume allocation bitmap. Existing
OFS/DOS2–5 read traversal remains available; writes are limited to DOS1 DD.
SetFileSize, comments, protection/date editing, formatting, links and arbitrary
filesystem variants are not newly qualified by this work.

Public DOS `Write` uses the native provider's packet-sized write capability,
matching the existing range-read boundary. Providers without it retain their
byte fallback. This avoids a filesystem transaction and physical seek/spin-up
for every byte. Zero-length writes remain valid without a source buffer.

Relative file operations resolve the owning process's current-directory lock
directly. Routing does not exchange or clear that directory and does not depend
on a missing inherited default filesystem-task field. The native fixture also
changes into a new directory, writes/reads/deletes a relative child, restores the
original directory and removes the empty directory.

The native trackdisk driver reads and verifies the target track, replaces its
changed sector bytes, generates original standard AmigaDOS MFM and writes the
track through CIA/Paula disk DMA. It preserves the remaining sector payloads and restores
the controller state. Clock generation uses fixed adjacent-bit operations, tested
against the bit-by-bit MFM rule; it avoids repeated variable shifts on 68000. No host filesystem callback services DOS requests, and no
engine or CPU scheduling changes are part of this implementation.

Metadata mutations clear the standard root bitmap-valid flag before writing.
They publish a valid root only after data, pointers and bitmap have been written.
Read-only media and allocation exhaustion fail before mutation. A later device
failure leaves the root invalid, preventing an incomplete host checkpoint.
This is not a journaled guest filesystem or an arbitrary power-loss guarantee.

## User-owned workspace

| Content | Location and policy |
| --- | --- |
| Qualified seed | Explicit read-only input, hash-bound by `CopperScreen/Firmware/CopperOS.json`. Never written by the app. |
| Workspace | `<user-data>/CopperOS/Workspaces/<id>/System.adf` |
| Identity | Sibling `workspace.json`, format 1: workspace ID, user-data root and original seed pair/hash. |
| Previous checkpoint | Sibling `Previous.adf`; retained during atomic replacement. |
| Ownership lock | Sibling `workspace.lock`, exclusively held for the session lifetime. |

For an MSIX installation, `<user-data>` is explicitly
`%LOCALAPPDATA%/Packages/<package-family>/LocalState/CopperScreen/`. Portable and
development launches retain `%LOCALAPPDATA%/CopperScreen/`. The package family,
not its versioned installation directory, owns the installed data root. See the
[profile and settings storage contract](PROFILE_STORAGE.md).

Creation stages the complete seed and identity in a sibling temporary directory
and publishes the directory atomically. Opening an existing workspace preserves
its original identity and disk even when a new package ships a different seed.
A workspace is never automatically reset, overwritten or migrated. The running
ROM contract must explicitly admit its original seed pair and hash, either as
the current seed or in `CompatibleWorkspaceSeeds`. A newer seed alone does not
authorize booting an older workspace; unsupported pairs stop with export guidance. Unsupported
identity/metadata and inaccessible storage produce errors; there is no fallback
to package files. Export a complete ADF before explicit repair or migration.

The normal app session mounts the owned copy writable and checkpoints completed
native mutations, at most once every 50 emulated fields. The host checks disk DMA, motor activity
and native root/bitmap validity before exporting the image. It flushes a complete
temporary image and atomically replaces `System.adf`, keeping `Previous.adf`.
The dirty state clears only after successful replacement. Failed saves retain
the previous complete disk and report that changes were not saved.

Closing drains bounded native disk activity on the existing session owner before
saving. Runtime shutdown waits for that owner to stop before disposing the
machine or releasing its storage lease. If saving fails, the user can keep the session open, export a complete
versioned `.copperos` backup elsewhere, or explicitly discard unsaved memory and retain the last complete
checkpoint. Recovery export does not depend on the primary save succeeding and
does not clear the primary workspace's dirty state. After the primary location becomes writable again, saving to that managed path
retries the primary checkpoint and clears dirty state only on success. P1-06 adds
backup import into a new workspace and confirmed recovery from `Previous.adf`,
preserving the current disk separately. See the
[media protection, backup format and recovery guide](MEDIA_PERSISTENCE.md).
The diagnostic raw-ADF export remains available; manual repair must preserve
the original identity and use an image from its admitted pair. Nothing is
automatically reset or migrated.

A managed
workspace cannot be swapped/ejected during its session, and unowned CopperStart
media remain read-only. Personal profiles/last-machine settings retain the
absolute workspace path and writable flag across app/package directory changes.

These paths follow the existing per-user/MSIX data contract. Same-family package
updates are designed to preserve the workspace; actual signed installed update
acceptance is recorded separately below; uninstall/reinstall persistence is a separate
Windows storage policy, not guaranteed by this feature. Export user work before
uninstalling or changing package identity.

## Launch and reproduction

After qualifying the exact development ROM/seed pair:

```powershell
# First launch: copies the admitted seed; subsequent launches preserve the copy.
CopperScreen.exe --copperos '<qualified System.adf>' --copperos-workspace personal --rom '<qualified standalone ROM>'
# Resume an existing workspace without reading the seed.
CopperScreen.exe --copperos-workspace personal --rom '<qualified standalone ROM>'
```

The producer implementation lives in CopperStart (`AmigaFfsWriteCore`,
`NativeAmigaFileSystem`, `NativeFloppyReader`, `NativeTrackDiskDevice`). Build the
original `C:Storage` guest command with CopperOS
`tools/ImageBuilder/build_storage_proof.ps1`, using a hash-bound base boot proof
and frozen compiler inputs. Compose its image with `build_system_image.py`.
The ROM build recipe is `CopperStart/build/Build-StorageProofRom.ps1`. It verifies
the frozen base inputs, builds only the disk/DOS components, preserves the
qualified root/Exec/compiler, records source hashes and applies native static
gates. Concurrent producer graphics work is excluded from that artifact.
The command uses public DOS calls to create/write/read settings and a data file,
rename/delete scratch content, create/remove a directory and verify files after
reboot. It does not use host services or write diagnostics into guest memory.

Run `scripts/test-copperos-storage.ps1 -Rom <ROM> -ProofDirectory <proof>` for
the complete source workflow. Source replay uses `COPPERSCREEN_COPPEROS_STORAGE` (proof directory) and
`COPPERSCREEN_COPPERSTART_ROM` (exact ROM), filtered to `CopperOsStorageTests`.
The command has a bounded 25,000-field allowance after boot for its expanded
mutation workflow; this is a diagnostic timeout, not a performance result.
Its three positive boots cover creation, app restart and a changed installation
directory. Separate native read-only and full-disk boots must report the expected
DOS error, return to the prompt and retain the complete image hash. A host-save
failure case exports the still-unsaved native data elsewhere and reopens that
export through native DOS.
The final case is a package-directory simulation, not installed MSIX evidence.
Missing assets skip the optional test and mean unavailable coverage.

## Installed update pipeline

Diagnostic MSIX builds accept `-CopperOsSeed` and `-CopperStartRom`, only with
`-InstalledTestBuild`. Both hashes must match embedded qualification metadata;
assets are copied into the diagnostic package only. Production packaging rejects
these proof-asset parameters. No ROM or system image is committed to the repo.

The installed probe runs the same native workflow from the package's normal app
session. The pipeline launches baseline creation, relaunches that baseline to
check process-restart persistence, installs the newer version and verifies the
same workspace and guest data. It checks the unchanged complete image hash, then
retains separate transcripts and runtime results for all phases. Profile/settings
checks remain separate. An absent guest pair produces `NativeCopperOsStorage:
UNAVAILABLE`, never a native storage PASS.

The dedicated workflow requires `COPPERSCREEN_COPPEROS_SEED` and
`COPPERSCREEN_COPPERSTART_ROM` artifact paths. Signed installation requires an
already trusted publisher certificate. The pipeline does not provision trust,
enable Developer Mode or turn developer registration into signed-install evidence.

## Qualification

The exact source pair is qualified for this bounded continuation on 2026-10-16.
See the [versioned storage descriptor](../packaging/guest/copperos-storage.v1.json)
and [machine-readable evidence](COPPEROS_STORAGE_2026-10-16.json).

| Artifact | Identity |
| --- | --- |
| CopperStart | `diskboot-2026-10-16-dev.8`, 524,288 bytes |
| ROM SHA-256 | `2b3d6d31663a9bbc5baa68d0d8549b5079e81b8d075ce79e95b2975c01515db9` |
| CopperOS storage seed | `0.2.0-dev.1`, 901,120 bytes |
| Seed pair | `a500-copperos-cli-boot-proof-dev.1.31e326e7505f` |
| Seed SHA-256 | `40e8d29c6a29b419fe117b62c61bd65828b1484b01086897404cda791b60cd88` |
| Saved/reopened/exported image SHA-256 | `e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0` |

All three native persistence tests passed with no skips, covering seven boots.
The first complete mutation workflow was captured at field 21,360; readback
checkpoints were captured at field 8,960. Native console input invokes the original
`C:Storage` command through the standard Lightweight session. Its settings fixture
is `S:User-Settings` (`volume=37` plus LF); its data fixture is `S:Persistent-Data`
(`CopperOS data survives restart` plus LF). These establish native file/settings
fixture persistence; complete CopperOS preferences/UI workflows are separate.

Native read-only and full-volume boots returned their expected DOS errors and
preserved the entire image hash. The full-volume control deliberately reserves
all bitmap blocks. The denied-host-save case retained the original checkpoint,
kept primary dirty state after export, and reopened the exported disk through
native DOS. Independent saved-disk readback verifies all original files plus the
two new files, absence of scratch/temporary directories, disjoint block ownership
and exact bitmap allocation: 934 allocated blocks and 824 free, excluding the
two reserved boot blocks. The seed remains unchanged.

Other checks: production Release build; 240 host tests passed (12 optional native
cases skipped in that command); 73 focused DOS tests; 12 filesystem/MFM tests;
six image-builder tests; 15 native console/DOS boot tests; original read-only
Shell/Alias/Demo/error regression; 12 app lifecycle/Lotus checks; and 22 MSIX
tooling controls. Dedicated native results provide their own coverage; the host
suite's skips do not count as replays. The inspected Lotus loading/menu/opening
race images match the preceding reviewed captures; PCM matches the scalar
control. This is not full-game, listening/fidelity or performance acceptance.

Diagnostic MSIX versions `1.1.7.0` and `1.1.8.0` were built with the exact guest
pair and validated package layouts. Signed install readiness reports
**UNAVAILABLE**, zero deployed phases: packages require a trusted signing
publisher. No certificate trust, Developer Mode or registration was changed.
The changed-install-directory source replay is a simulation, not installed MSIX
update acceptance. That 2026-10-16 result remains frozen. The subsequent
[2026-10-17 signed installed validation](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
passed on diagnostic versions `1.1.9.0` → `1.1.10.0`: native creation, process
restart, actual WindowsApps package update and clean uninstall/reinstall. Profiles,
settings and the complete native workspace image remain unchanged across restart
and update. The continuation also corrects the installed data root to explicit
package-family LocalState after retaining an initial failed reinstall attempt.

The [WACK/DPI continuation](MSIX_WACK_VALIDATION_2026-10-17.md) also passes a real
signed `1.1.9.0` → `1.1.12.0` update with three native boots and unchanged workspace,
profile/settings and preset bytes. All four desktop phases exit with code zero.
WACK reports overall PASS for that diagnostic package and the separate normal
package shape; their optional executable-reference findings remain recorded.

Earlier timed-out/per-byte candidates and the native relative-directory failure
remain failed evidence under local artifacts. The final ROM uses packet-sized
writes and owned current-directory routing; no earlier failure is relabeled.

Wider Shell/resource budgets, additional filesystem APIs, commercial rights,
clean bundled onboarding and Store acquisition/update acceptance remain separate
P1-04/release gates. Host format/workspace tests are not native execution evidence.

The producer working tree also contains an unrelated alias-registry edit made
after the ROM's managed inputs were frozen. The evidence records its frozen and
current hashes explicitly. It is excluded from this ROM; the qualified binary
inputs, not an arbitrary rebuild of the evolving producer tree, define this
result. A rebuilt/changed artifact requires a new qualification identity.
