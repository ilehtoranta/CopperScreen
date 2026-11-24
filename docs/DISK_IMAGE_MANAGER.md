# Disk Image Manager

Open **Disk Image Manager…** from the launcher or the **More** menu. Stop the
Amiga first. Save any unsaved floppy changes separately: the tool reads the saved
image on the host.

Both panes can browse a **Host** folder or an **Image**. Choose the type in the
pane header and use **…** to choose its location. Each image has its own volume
selector. The highlighted pane is the source. **Copy →** or **← Copy** copies its
selection into the other pane's current directory. Clicking a centre command
retains the source pane. **Open image…** opens an image in the active pane.

1. Activate a pane, choose **Open image…** and select an ADF, HDF, VHD, IPF or DMS image.
2. For an RDB hard disk, select a volume in **Image volumes**.
3. Double-click directories or press Enter to browse; **Parent** goes up in the
   active pane. Choose **…** in the host header to pick a folder, or enter a path
   in its bottom address bar and press Enter.
4. Choose a location in the other pane, select source entries, then choose **Copy**. Directories include their entire tree, including
   empty directories and ordinary `.info` files.

IPF and VHD images are read-only sources in Disk Image Manager. Complete,
unencrypted standard DD DMS archives support imports, New directory, Rename,
Delete, Move and Protect on recognizable
filesystems. DMS saves recompress and verify the entire disk before staged
replacement; compression methods and archive size may change. Header metadata is
preserved. Archives with auxiliary records (such as banners/FILEID), trailing data
or unsupported flags remain read-only. Encrypted, incomplete, corrupt, HD and
non-Amiga archives are rejected.

On Windows filesystems supporting sparse storage, saves keep zero-filled image
regions unallocated on the host. ADF/HDF images retain their full logical capacity;
only **Size on disk** can shrink. DMS uses sparse working files and saves its usual
compressed archive. Deletes and source removal during moves also reclaim eligible
nonzero former file data: CopperFS rechecks freed blocks in the guest before the
staged image is published. Other file data and image capacity stay intact.
Historical unused contents outside the tracked interval are retained. Unsupported
host filesystems use ordinary files. Cancellation or TRIM failure prevents saving
the staged changes. Classic SFS Delete permanently removes the selected entries;
it does not change the volume's recycling setting or purge other recycled files.

Click selects one entry, Ctrl toggles and Shift selects a range. **All**, **Invert**
and **Clear** change the active selection. Click **Name** or **Size** to sort;
directories stay first and sizes are bytes. **Info** shows the selected entry's
metadata; **Help** explains operation limits and shortcuts. Full paths and status
messages are available in tooltips. Selected file bytes exclude directory contents.

| Shortcut | Action |
| --- | --- |
| F6 | Switch active pane |
| F5 | Copy active selection |
| F3 | View one selected file as text |
| Shift+F3 | View one selected file as hex |
| Shift+F5 | Move active selection, with confirmation |
| F7 | New directory in the active image |
| F2 | Rename the selected image entry |
| Delete | Delete selected image entries, with confirmation |
| Alt+Up | Parent directory |
| Ctrl+R | Refresh active listing |
| Ctrl+A | Select all entries |
| Escape | Cancel a running operation |

Commands do not intercept address-bar editing. While busy, **Cancel** replaces
Copy and conflicting controls are disabled.

## Text and hex viewers

Select one ordinary file in either pane, then choose **Text** or **Hex**. F3 opens
Text; Shift+F3 opens Hex. Enter or double-click also opens a selected file as text;
directories still open in the pane. Viewing works with host files and all supported
image filesystems, including read-only images. Directories, links and multiple
selections are excluded.

The read-only viewer has a mode selector, first/previous/next/last-page buttons,
hexadecimal byte offset and Go. Prefixing the offset with `0x` is optional.
Ctrl+PageUp/PageDown changes pages; F3/Shift+F3 switches modes within the viewer.
Contents are selectable, so Ctrl+A/C can copy displayed text. Escape or Close
closes the viewer and cancels a pending read.

Files are read on demand in 4 KiB pages, with only a few boundary bytes retained
to display split UTF-8 characters and CRLF correctly. Large files are not loaded
in full or silently truncated. The status shows byte range and total file length;
offsets above 4 GiB are supported. Lines remain unwrapped, with horizontal scrolling.
Text initially selects UTF-8 when the first page is valid UTF-8 (including its
optional BOM), otherwise Latin-1. The encoding selector permits switching between
UTF-8 and Amiga Latin-1. Other encodings, such as UTF-16, are not decoded in this
version. Nonprinting control/format characters appear as dots; file contents are
never interpreted as terminal escapes, markup or commands. Hex displays sixteen
bytes per row, byte offsets and printable ASCII; other bytes appear as dots.

Image pages come from CopperFS guest streams, with each page's stream closed
before another operation. The modal viewer prevents concurrent pane edits. Reads
use the manager's existing cancellation, instruction-budget and fault recovery.
A later-page error clears displayed contents, disables further navigation and
keeps the reason visible in both viewer and manager. Host file length/date changes
are detected when opening a page; close and reopen the viewer after editing the
file externally. Viewing never saves or changes the source.

## Image-to-image copying and image edits

ADF ↔ ADF, ADF ↔ HDF, HDF ↔ HDF, host ↔ image and host ↔ host are supported.
The two images may use different supported filesystems and block sizes. Image
copies stream directly between independent guest sessions; files do not pass
through a temporary host directory. This preserves Amiga names such as `CON`
that Windows cannot represent. Dates, comments and protection bits are preserved
within destination-format limits. An unrepresentable name or metadata value
aborts the whole selection; nothing is silently truncated.

The same ADF/HDF can be opened in both panes to copy between directories or RDB
partitions. Volume choices, paths and selections are independent. Both views share
one protected image handle; copying reads the unchanged original while writing an
unpublished clone. A successful save refreshes both panes and preserves their
volume, path and surviving selections. Renaming a directory updates an open view
below it; deleting it returns that view to its nearest surviving ancestor.

Copying into the same directory, or copying a directory into itself or its
descendants, is refused. Existing destination entries are never overwritten or
merged. Closing or changing one pane leaves the other image view usable.

**New dir**, **Rename** and **Delete** operate on the active image. Rename acts
on one selected entry, including case-only renames. Delete asks for confirmation
and removes selected directory trees, including empty directories. Guest
protection flags are respected. Links cannot be renamed, deleted or followed.
These three commands are disabled for host folders and read-only images.

**Move →** / **← Move** transfers the active selection and removes the source.
The confirmation names both locations; Cancel leaves them unchanged. Move works
with host folders, separate images, directories in one image and different RDB
partitions in one image. Read-only images can be copy sources but cannot be move
sources. Same-directory, self/descendant and destination-conflict guards apply.

Same-volume OFS/FFS, classic SFS and SFS2 moves use guest rename. File contents
are neither copied nor removed, and no room for a second copy is needed inside
the volume. Normal filesystem metadata-space requirements still apply. Image
saves retain the unpublished host clone, clean remount and atomic replacement;
host staging space is still required. No guest fault is retried or saved.
Moves between partitions in one image copy then remove the
source in the same clone; failure or cancellation leaves the whole image unchanged.

Separate images and host locations cannot be saved as one transaction. The
destination copy completes first; source removal begins only after that succeeds.
If removal fails or is cancelled, the destination is retained and the status
reports the incomplete move. Image source deletion is staged as a whole; host
source deletion can finish partially. Host trees are checked against a pre-copy
snapshot (including file hashes); read handles protect existing source files
during the copy. Windows removal verifies each file through the same handle it
marks for deletion. Links and read-only entries are refused, and only examined
files and empty directories are removed. Check remaining source
entries before retrying; existing destinations are never replaced automatically.

**Protect** opens a confirmation editor for selected image entries. Readable,
Writable, Executable and Deletable are permissions (the underlying RWED bits are
inverted); Archive, Pure, Script and Hold set the remaining Amiga flags. Mixed
checkboxes leave that flag unchanged. Apply preserves other protection bits and
changes selected entries only, with no implicit recursion. Cancel saves nothing.
The edit uses the same staged save and respects guest errors.

Drag selected entries onto the other pane's file list to copy to its current
directory. Hold Shift while dropping to request a confirmed move. Dropping on a
directory row still uses the pane's **current** directory. Native host file/folder
drops into either pane copy only, even with Shift; CopperScreen never asks the
external drag source to delete them. Busy, read-only destination, unavailable
volume, stale selection and unsafe same-directory drops are refused. Image entries
are internal drags, not Windows virtual files; dragging them out to another app
is not supported. Use a Host pane to extract them.

Every image edit uses the same unpublished whole-image clone, clean guest remount,
hash check and atomic replacement as imports. A failed or cancelled edit leaves
the original unchanged. After saving, the pane reopens its previous volume and
directory. There is no separate Save command or persistent backup.

The reference DiskMaster/FileMaster commands suggest these further additions:

| Command | Possible next addition |
| --- | --- |
| Copy As | Prompt for a destination name, using the existing staged transfer |
| Comment | Edit the comment already displayed by Info; the guest API supports it |
| DiskInfo / DiskSize | Volume information; free/used space needs a guest query rather than a host filesystem parser |
| Find file | Cancellable directory search through guest enumeration |

Media playback, executable launching and raw disk/file editing are separate
features, beyond the filesystem manager operations implemented here.

Browsing opens images read-only with a handle that excludes concurrent writers. The
modal manager prevents another machine start through its owner while it is open.
Filesystem operations run in a separate CopperStart guest on Copper68k's 68020
interpreter, outside Lightweight and the main machine's execution path. No
Kickstart, desktop CopperStart ROM, CopperOS image or chipset emulation is required.

## Supported media

- Standard sector ADF, including DD and HD.
- Raw single-volume HDF/IMG with 512-byte filesystem blocks.
- Mountable partitions discovered by CopperDisk's RDB parser, with validated
  geometry and power-of-two filesystem blocks from 512 to 65,536 bytes.
- Classic OFS/FFS DOS0–DOS5, including international and directory-cache variants.
- Clean classic SFS (`SFS\0`) and SFS2 (`SFS\2`). Dirty volumes are refused;
  journal recovery of supplied dirty volumes is unavailable.

Unsupported or damaged volumes show an explanation. Custom game filesystems,
encoded/extended ADF, compressed/archive images, PFS and DOS6/DOS7 long-name FFS
are unavailable. Links are displayed as metadata, but cannot be traversed or
selected for copying. A link inside a copied directory stops extraction before its
staged roots are published. The manager does not repair or recover filesystems.

The packaged CopperFS cores perform filesystem detection, traversal, metadata
validation and file reading in the guest. In particular, OFS reading follows the
file pointer table; redundant next-block fields are not a separate traversal or
consistency check. FFS file data has no per-block checksum. The desktop rejects
ambiguous duplicate directory rows and unsafe host destinations.

## Host-to-image copying

Select files or directories in a **Host** pane, browse to the destination
**directory** in an **Image** pane, activate the host pane, then choose **Copy**
toward the image.
The current image directory is the destination; selecting a file does not change
it. Multiple host selections and entire directory trees, including empty
folders and `.info` files, are supported. Imports preserve representable local
modification dates, with the filesystem's timestamp precision. Host permissions,
links, comments and extended metadata are not mapped.

Import supports **DOS0–DOS5, SFS and SFS2** through CopperFS's maintained native
writers. DOS2–DOS5 use international Latin-1 hashing; DOS4/DOS5 also maintain
directory caches. Imports and image edits are disabled when the destination image
file is read-only; such images can still be copy sources. Dirty Smart
volumes are refused at mount. Existing image entries are never overwritten or
merged; a conflict aborts the whole selection. Latin-1 names must fit the writer:
30 characters for OFS/FFS, 100 for classic SFS and 107 for SFS2. Names are never
silently truncated. The guest enforces the destination filesystem's case comparison
rules; conflicts are refused without overwrite or merge.

### Reversible Windows names

Image views retain original Amiga names. Export preserves compatible Windows
names and encodes invalid/reserved names, trailing dots/spaces, reserved-prefix
names and every member of a case-insensitive sibling collision group. The alias
is `.copperimage-v1-` followed by uppercase hexadecimal Latin-1 name bytes. A
readable extension is retained only when it is ASCII alphanumeric and at most
16 characters. Each path component is mapped separately. Mapping uses the full
source directory even when only part of it is selected, so aliases remain stable.

Import previews encoded names and defaults to restoring original names. Choose
**Literal names** explicitly to import the host spellings instead. Malformed
encodings cannot be restored; traversal components and destination collisions
are rejected. No sidecars are required. Image-to-image transfers bypass Windows
aliases and preserve supported Amiga metadata. Shared-folder mappings are separate.

The image operation implementation is shared with CopperExplorer through the
UI-independent `CopperDisk.FileSystem` library. IPF sector decoding is opt-in and
strict: unreadable, corrupt, conflicting or weak-dependent accessed sectors fail
instead of yielding placeholder bytes. IPF sources are always read-only.

The manager copies the entire image to a temporary sibling, then creates
folders and writes file contents through the packaged 68020 guest. Writes are
confined to the selected volume range, preserving RDB metadata and other
partitions. After a clean guest remount and flush, it reacquires writer ownership,
checks the original image's SHA-256 and atomically replaces it. The listing is
refreshed in the original destination directory. Imported files are immediately
saved in the image; there is no separate Save action.

Any conflict, invalid name, disk-full condition, host I/O error or cancellation
before publication discards the temporary image. The original stays unchanged.
The operation needs free space for a whole image copy and may fail if another
program still has the image open. Host symbolic links and junctions are refused,
as are selections containing the destination image itself. A late cancellation
after publication does not undo the saved import. There is no persistent backup;
keep your own backup when editing valuable media.

## Host extraction

Existing files and directories are never overwritten or merged. Choose a different
destination when a selected name already exists. Compatible names are preserved;
other names use the reversible aliases described above. Destinations through
host symbolic links or junctions are rejected.

Copying uses disposable sequential guest streams with a reusable 64 KiB transfer
buffer. Trees are staged in a temporary child of the destination, then moved into
place after all selected contents have been copied. Cancellation or a read error
removes the staging directory. Destination moves never overwrite; if an external
process causes an error during publication, already published roots remain.
Final publication finishes without cancelling between roots. Closing the window
requests cancellation and waits for the operation before releasing the image.

Cancellation, a guest CPU fault or the service's per-command instruction budget
invalidates the active session. The manager releases it, clears the listing and
asks you to reselect the volume. Reselecting creates a fresh mount. Ordinary
filesystem errors display their structured service message. An unavailable mount
can be retried by reopening the image after repairing it elsewhere.

File contents, directory structure and representable local modification dates are
exported. Amiga protection bits and comments can be inspected by selecting one
entry and choosing **Info**; they are not mapped to host permissions or exported as sidecar files.
No timezone is inferred from Amiga dates. `.info` icons are ordinary files.

## Implementation

`CopperDisk.AmigaSectorImage` owns storage, discovers RDB extents and exposes
bounded reads and explicit geometry. Raw volumes use 512-byte blocks; their boot
identifier supplies only mount traits, including SFS's reserved-block default.
`CopperScreenDiskImage` adapts these views to the packaged
`CopperStart.FileSystem.Service` API. Only the selected volume owns a CPU/session;
opening an image does not eagerly instantiate a runtime for every partition.
Switching volumes disposes the previous session. Entries from a prior mount cannot
be used in a new session. The window serializes background operations.

The service embeds its ROM and versioned manifest and uses pinned Copper68k 1.5.10,
M68020 interpreter execution, flat ROM/RAM and bounded storage gateways. Writes require an explicit writable staged source and service opt-in.
The default per-command budget is 50 million instructions; cancellation is checked
every 256 interpreted instructions. There is no managed filesystem fallback or
host Exec/DOS shim. CPU selection and future JIT remain service concerns; this
integration uses its qualified defaults. Host extraction policy lives separately
in `CopperScreenDiskExtraction`.

## Development package

The app builds `CopperDisk.FileSystem` from source and uses the pinned
`CopperStart.FileSystem.Service` **[0.5.0-dev.6]** from NuGet.org. A clean
checkout needs no private feed credentials:

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
```

If you have the original qualified archive, you can also stage it in the local feed:

```powershell
./scripts/stage-filesystem-service.ps1 -PackagePath "path/to/CopperStart.FileSystem.Service.0.5.0-dev.6.nupkg"
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
```

The script verifies the pinned package SHA-256:
`449B1A13AC7CE682C5D6954DAE750DB61A2C79C621F9DEFF2529E7BD70635EF0`.
It copies the package to ignored `artifacts/filesystem-service-feed`, an additional
restore source when present. Lock files also pin the NuGet content hash.
Consumers need the managed package and its pinned CPU dependency; CopperStart,
CopperFS and CopperSharp checkouts and native compilation are unnecessary during
CopperScreen restore, build and execution. Default app builds include this service;
the separate full CopperStart boot feature remains explicitly opt-in.

## Validation

Generated-media package-consumer tests exercise the actual embedded ROM for
DOS0–DOS5, SFS and SFS2: nested/empty directories, empty files, fragmented files,
OFS/FFS extensions, directory batches, metadata, repeated streaming reads, RDB
volume ranges and larger filesystem blocks. They also check dirty Smart volumes,
corrupt metadata, links, source immutability, writer exclusion, cancellation,
staging cleanup and stale entries after remounting.

Headless desktop tests cover navigation, extraction, switching FFS/Smart RDB
partitions, dirty-volume explanations, cancellation/reselection and layouts at
minimum, default and scaled sizes. These tests require the packaged ROM; there is
no optional missing-ROM skip. Generated fixtures contain no external ROM or game
media. External real-world SFS/SFS2 media compatibility and throughput are separate,
unmeasured coverage; these tests establish no JIT or performance improvement.

Read-only integration checkpoint (0.1.0-dev.1), 2026-11-14: locked restore, Release production build and
publish passed. CopperDisk passed 136 tests; desktop passed 204 with 31 optional
boot/bundle skips; separate engine diagnostics passed 1,323 with seven optional
native-media skips. The 70 focused manager/service tests passed without skips.
A fresh-cache consumer compiled the app adapter against the package and the built
CopperDisk library, then listed and extracted all eight filesystem variants with
exact fixture-byte comparisons. Its cache contained only the service, Copper68k
and CopperFloat packages. Captures and test receipts are local ignored artifacts
under `artifacts/disk-image-service` (engine builds use `artifacts/diagnostic-tests`).


Host-to-image validation on 2026-11-14 (0.2.0-dev.2): the compiled service passed
34 tests, including all four native writers, asynchronous host file streams,
read-only gateway enforcement, handle/range checks and actual guest remounts.
The linked ROM uses 234,426 bytes in the compiler's minimum 262,144-byte allocation;
linked symbols exclude OS, desktop, chipset, format and trim roots. The ABI is 2.
Read-only sessions still reject write and flush gateways even with writable storage.

The full desktop suite passed 222 tests with 31 optional boot/bundle skips;
CopperDisk passed 136; separate engine diagnostics passed 1,323 with seven optional
native-media skips. Production Release build and locked restore passed without
warnings. The full CopperStart solution built successfully with 11 warnings in
existing dependency and desktop/OS projects; the service build itself was clean.

Imports were checked with raw ADF/HDF and RDB ranges, 512/1024-byte blocks, nested
and empty directories, empty files, multi-batch data, dates, icons, conflicts,
invalid names, disk-full failures, cancellation after staging, publication sharing
failures, source immutability on failure, retry and UI refresh in the destination
directory. Other RDB partitions and metadata remained byte-identical. A fresh
package cache containing only the service, Copper68k and CopperFloat passed
import/extraction/conflict checks through the app adapter for DOS0, DOS1, SFS and
SFS2, without filesystem source checkouts. Captures, generated blank fixtures and
receipts are ignored under `artifacts/disk-image-import`. External real-media write
compatibility and throughput remain unmeasured.

DOS2–DOS5 integration checkpoint on 2026-11-14 (0.3.0-dev.1): CopperFS now
writes and quick-formats all classic variants. International hash folding is
shared with the reader, including the non-folding Latin-1 0xFF boundary. DOS4/DOS5
create and maintain directory caches for entry creation, file growth/truncation,
metadata updates, moves and deletion. Independent on-disk tests compare cache
records and allocation bits against canonical headers; the hash-chain reader is
not used as the cache oracle. The implementation and layout references are in
CopperFS's `docs/classic-variant-writes.md`.

The final CopperFFS managed suite passed 311 tests. The actual compiled 68020
service ROM passed 42 tests; the app's 105 focused image-manager tests passed
without skips. Full desktop validation passed 239 with 31 optional boot/bundle
skips; separate engine diagnostics passed 1,324 with seven optional media skips.
Production Release build, locked restore and publish passed. A new isolated
package consumer imported, extracted and checked atomic conflict handling for
all eight formats using a fresh cache with no CopperFS/CopperStart source
references. DOS2–DOS5 checks include raw images and RDB ranges with larger
filesystem blocks, international names, cached-volume corruption, disk-full
failures and cancellation without publishing source changes.

The ROM uses 241,510 bytes in 262,144 allocated bytes (ABI 2, minimum 68020,
interpreter execution). The rebuilt standalone 68000 CopperFFS handler is 74,392
bytes. External real-media/AmigaOS directory-cache interoperability and performance
remain separate, unavailable coverage; no JIT or throughput improvement is claimed.
Receipts, captures and generated fixtures are ignored under `artifacts/dos2-dos5`.

CopperStart's final focused native replay run passed 41 tests on the 68000
interpreter (overwrite, geometry and link instruction coverage). The overwrite
fixture now explicitly supplies its optional native platform operations and
handles odd/short copy buffers; a null adapter call was diagnosed without changing
Copper68k or weakening instruction semantics. The full CopperStart solution build
passed with 11 existing dependency/desktop warnings; CopperFS and CopperScreen
solution builds passed without warnings.

The broad `dotnet test CopperFS.slnx -c Release` run is not a clean suite pass:
MSBuild could not replace `CopperCPX.InstructionTests.dll` because another
`testhost` process held it (MSB3027/MSB3021). Completed projects in that run passed
1 prototype-instruction, 77 prototype, 444 CPX-core, 311 CopperFFS-core,
18 native-interface and 32 storage-harness tests. This limitation is separate
from the isolated, passing filesystem/service/app checks above. External licensed
media skips remain unavailable coverage.
The broad run was stopped after the CPX instruction-test output lock had already
failed its build step; its remaining SFS tests had not completed. This is
incomplete solution-wide coverage, not a passing full suite. No other test run
was stopped. The focused, isolated checks recorded above had completed.

## Interchangeable panes and editing checkpoint — 2026-11-14

The manager now supports independent Host/Image locations in either pane,
direct image-to-image transfers, and staged New dir/Rename/recursive Delete on
images. Folder icons have a fixed gutter, checked at both window sizes and 200%
scaling. The reference-command audit is listed above under image edits.

The new service package is `0.4.0-dev.3`, ABI 3, pinned to Copper68k 1.5.4 and
the 68020 interpreter. Its immutable ROM uses **274,318 bytes in 524,288 allocated
bytes**, the next power-of-two allocation required by the linked image, with
513 symbols and no OS/desktop/chipset/format/trim roots. RAM remains 1 MiB per
session. Two image panes use independent serialized guest sessions. The package
is local development output; no feed publication or older-version replacement
was performed.

Validation receipts are ignored under `artifacts/two-images` in CopperScreen,
`artifacts/filesystem-service`, `artifacts/manager-native*` and
`artifacts/manager-solution` in CopperStart, and `artifacts/manager-validation`
in CopperFS:

- Production CopperScreen Release solution build: zero warnings/errors.
- Full desktop suite: **269 passed, 31 optional boot/bundle cases skipped**.
  Eight additional protected-delete rollback cases passed, one per filesystem.
- Standalone service: **50 compiled-ROM tests passed**, covering all supported
  formats, Rename/Delete/metadata/protection, case-only rename and retained read,
  transport, cancellation and instruction-budget checks. A fresh isolated package
  consumer restored the final package and browsed/extracted all eight formats.
- Direct image-copy fixtures cover ADF/HDF and filesystem changes, nested/empty
  directories, empty and multi-batch files, dates/protection/comments, source
  immutability, invalid Windows names, guest-specific name comparison, conflicts,
  read-only storage, cancellation and cleanup. Image edits remount cleanly.
- The full CopperFS solution build passed with one existing compiler nullable
  warning. Its entire test run completed: **2,315 passed and 8 optional cases
  skipped**, including 338 CopperFFS core tests, 1,339 CopperSFS core tests and
  66 CPX instruction tests. This completed isolated run is separate from the older
  incomplete output-lock run recorded above.
- Full CopperStart solution build: zero errors, 13 existing dependency/compiler/
  desktop warnings.
- Three full-system FFS boot replays failed before guest execution: the Full
  filesystem-profile ROM required **603,636 code bytes**, exceeding its 524,260-byte
  code allowance (`C68K0007`). These are unavailable full-system replay coverage,
  not successful replays or failures of the separate filesystem-service ROM.
- A separate Classic-profile follow-up (`CopperStartNativeSfsWrites=false`) built
  successfully but was stopped after more than 17 minutes in compiler optimization,
  before guest execution. A captured stack identifies
  `M68kPeepholeOptimizer.TryForwardGeneratedRegisterCopies` inside ROM compilation.
  Only that verified testhost was stopped. This is incomplete additional replay
  coverage, not a pass; its log and stack are retained under CopperStart artifacts.
  Resolving full-system ROM size/optimizer issues is separate from this service.
- Locked package restore passed. Captures at 1060×720 and 760×620, including 200%
  scaling, cover populated, empty, unavailable and busy states and two loaded
  images. File views retain their compact height and folder icons have clear gaps.

The maintained CopperFFS rename core now updates stored casing and DOS4/DOS5
directory caches for case-only renames; exact no-op renames retain their prior
behavior. Image-only operations delegate name comparison to CopperFS, including
the distinction between ASCII and international Latin-1 folding. Host transfers
retain their existing Windows collision policy. Same-image dual-pane access,
overwrite/merge, links and journal recovery remain unavailable.

## Shared-image panes checkpoint — 2026-11-15

Both panes can now share one ADF/HDF, including different RDB partitions. The
manager retains one protected storage owner per open image and one guest session
per mounted volume; pane paths and selections remain independent. Sessions for
volumes no longer shown are released without closing a volume used by the other
pane. Changing one pane to Host releases only that pane's image reference.

Copies read the original image while the destination guest writes its unpublished
clone. All readers close only after the copy and clean remount finish, immediately
before the hash-checked atomic publication. Both panes then rebind to fresh volume
records, restore surviving selections and keep their volume/directory. Directory
renames remap an open descendant path; deletion falls back to a surviving parent.
Same-directory and self/descendant copies are refused before staging. Failed or
cancelled copies retain the original bytes and remove staging files.

Focused validation passed 136 checks, including same-volume trees and metadata in
DOS0–DOS5/SFS/SFS2, RDB transfers in both directions with unchanged source partitions
and RDB bytes, cancellation/conflicts, reader lifetime and shared-pane UI edits.
The production Release solution build passed with zero warnings/errors. The full
desktop suite passed **297 tests**, with 31 optional boot/bundle cases skipped.
Receipts and the same-image capture are ignored under `artifacts/same-image`. The pinned
guest package, ROM ABI, CPU backend and filesystem cores are unchanged by this slice.

## Move, Protect and drag-and-drop checkpoint — 2026-11-15

Move routes the same highlighted source as Copy and requires confirmation for
buttons, Shift+F5 and Shift-drop. Same-image partition moves publish one clone.
Separate-storage moves retain a completed destination if source removal cannot
be confirmed; that result remains visible after both panes refresh. Host moves
hold source read handles during copying and verify files again before removal.
Protect applies masked HSPARWED edits through the guest, preserving mixed and
unexposed bits without recursively changing directory contents.

Validation for this slice:

- Final Disk Image Manager/storage/service regression run: **208 passed, zero
  skipped**. Coverage includes DOS0–DOS5, classic SFS and SFS2 moves and protection,
  both RDB directions, same-image rollback, protected/locked/changed sources,
  cancellation, source/destination conflicts, modal confirmations, mixed flags,
  pointer-initiated dragging, routed copy/move drops and external host-file drops.
- Full desktop run: **341 passed, 31 optional boot/bundle cases skipped**. This
  broad run preceded the final host read-lock and status-retention refinements;
  those refinements and the additional partial-move UI regression are covered by
  the final 208-check run above. A separate five-check host run also passed.
- Final `dotnet build CopperScreen.slnx -c Release`: zero warnings and errors.
- Populated, empty, unavailable and busy captures at 1060×720 and 760×620,
  including 200% scaling, retain aligned file views occupying at least 75% of
  client height. Reviewed captures also include Move and the Protect dialog.
  Host drop events were injected through Avalonia.Headless; Explorer shell
  interoperability was not manually exercised. Image virtual-file export to
  external applications is not implemented.

The initial direct classic SFS directory reparent test faulted in the pinned
guest at PC `$00FA70EE`, reading `$00100004`. The original image stayed unchanged.
Move therefore uses qualified guest copy/delete commands in one clone for
classic SFS, with the free-space and descendant-protection limits described
above. This does not fix or retry the guest's reparent command. Its failing
receipt is retained alongside the successful final regression receipts.

Logs and TRX files are ignored under `artifacts/move*.log` and `artifacts/move/`;
captures are under `artifacts/move/captures/`. No CopperFS, guest ROM, package or
emulator sources changed, and no new external-media replay coverage is claimed.

## Classic SFS native reparenting checkpoint — 2026-11-15

The preceding checkpoint's classic SFS copy/delete fallback is superseded by
local package `0.4.0-dev.4`. All supported same-volume image moves now call native
Rename, including classic SFS. Existing staging, confirmation, conflict,
cancellation and shared-pane refresh safeguards remain in place. Cross-volume
and cross-image transfers retain their copy/remove behavior.

The fault was in the standalone guest platform adapter, not CopperFS's rename
implementation or Copper68k. Classic SFS directory reparenting describes destination
ancestors through ObjectView, which reads optional `SfsMountStorage`. The stateless
adapter inherited that property's default interface body. Compiled interface
dispatch attempted to interpret the value-type platform as an object and scanned
outside guest RAM at `$00100004` (old PC `$00FA70EE`). The adapter now explicitly
returns `APTR.Null`, as intended for this service without a mount cache. The linked
ROM resolves this call to `ServicePlatform::get_SfsMountStorage`; the interface
getter is no longer linked. No filesystem-core, compiler or CPU source was changed.

The new direct directory-rename regression failed on the old ROM for classic SFS
and passed for the other seven formats. With the fix, the full service suite
passed **58 compiled-ROM checks**, including all eight directory-reparent cases,
node identity, preserved metadata, empty/nested directories, protected descendants,
descendant rejection, fresh mounts, streaming, corruption and transport guards.

Two separate nearly-full-volume tests passed for classic SFS and SFS2. Each fills
a 2 MiB image until copying the 100,013-byte payload tree fails with status 221
(disk full), verifies the failed copy left the original unchanged, then moves the
tree through native Rename and verifies its bytes, dates, comment and protection
after fresh mounts. No duplicate payload space inside the volume is required.
Normal metadata allocation and host space for the staged image still apply.

The immutable local development package was restored into an isolated consumer,
which browsed/extracted all eight formats without sibling references. CopperScreen
consumes that same package through the updated pin and NuGet lock hashes. Nothing
was published. Package SHA-256 is recorded in Development package above; ROM
SHA-256 is `210BFD2170BDD7AAB0E147A61DF77CECFB20C2B86E6B1921737C7DB2D59F8D5B`.
The ROM uses **302,362 bytes**, allocates **524,288 bytes**, and links **557 symbols**,
with excluded OS/chipset components absent. ABI 3 and the pinned Copper68k 1.5.4
M68020 interpreter are unchanged; no throughput or JIT improvement is claimed.

The full desktop suite passed **344 tests**, with 31 optional boot/bundle skips.
The CopperScreen Release solution build passed with zero warnings/errors; the
CopperStart Release solution build passed with 11 existing warnings. No new
full-system ROM or external-media replay coverage is claimed. Baseline failure,
fixed service, nearly-full-volume and desktop TRX receipts are ignored under
`artifacts/sfs-reparent/`; build/consumer logs are `artifacts/sfs-reparent-*.log`.

## Text and hex viewer checkpoint — 2026-11-15

Text and Hex commands view one selected file from either host or image pane.
Image pages use the existing guest OpenRead offset API; no filesystem-core, guest ROM,
package or emulator changes were needed. Each request reads at most 4,101 bytes,
including boundary context, and releases its stream before returning. The modal
viewer serializes reads through the manager's operation/fault-recovery path.

The 25 focused checks passed for host and actual guest reads in all eight
formats, source immutability, UTF-8/CRLF page boundaries, Latin-1, exact hex/ASCII,
empty/read-only files, 64-bit offsets, changed/short/corrupt reads, selection and
shortcut routing, and closing either window during a pending page read. Captures
under `artifacts/viewers/captures/` were reviewed for Text and Hex, including a
760×620 viewer at 200% scaling with an offset above 4 GiB.

The viewer slice was also applied to an isolated checkout of the committed
manager baseline: its Release solution build passed with zero warnings/errors,
and its full desktop suite passed **369 tests**, with 31 optional boot/bundle
cases skipped. The shared checkout's Release build also passed; its broad run
had **368 passes, one failure and 31 skips** because the concurrent lossless
filename-encoding refactor changes the old case-collision extraction expectation.
That unrelated behavior/test was left untouched. Viewer receipts and isolated
validation logs are ignored under `artifacts/viewers/` and `artifacts/viewer-*.log`.
