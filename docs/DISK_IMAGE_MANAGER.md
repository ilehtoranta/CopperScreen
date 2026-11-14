# Disk Image Manager

Open **Disk Image Manager…** from the launcher or the **More** menu. Stop the
Amiga first. Save any unsaved floppy changes separately: the tool reads the saved
image on the host.

The left pane is the host folder. Choose a folder, enter a path and choose
**Go**, or double-click a directory to enter it. The right pane is the disk image:

1. Choose **Open ADF/HDF…** and select an existing sector image.
2. For an RDB hard disk, select a volume in **Image volumes**.
3. Double-click directories or press Enter to browse; **Parent directory** goes up.
4. Select one or more entries (Ctrl/Shift selection is supported), then choose
   **← Copy selected to host**. Directories include their entire tree, including
   empty directories and ordinary `.info` files.

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
staged roots are published. This is a reader, not a repair or recovery tool.

The packaged CopperFS cores perform filesystem detection, traversal, metadata
validation and file reading in the guest. In particular, OFS reading follows the
file pointer table; redundant next-block fields are not a separate traversal or
consistency check. FFS file data has no per-block checksum. The desktop rejects
ambiguous duplicate directory rows and unsafe host destinations.

## Host-to-image copying

Select host files or directories in the left pane, browse to the destination
**directory** in the right pane, then choose **Copy host selection to image →**.
The current image directory is the destination; selecting a file does not change
it. Multiple host selections and entire directory trees, including empty
folders and `.info` files, are supported. Imports preserve representable local
modification dates, with the filesystem's timestamp precision. Host permissions,
links, comments and extended metadata are not mapped.

Import supports **DOS0/DOS1, SFS and SFS2** through CopperFS's maintained native
writers. DOS2–DOS5 remain browse/extract-only. The button is disabled for these
volumes and for an image whose host file has the read-only attribute. Dirty Smart
volumes are refused at mount. Existing image entries are never overwritten or
merged; a conflict aborts the whole selection. Latin-1 names must fit the writer:
30 characters for OFS/FFS, 100 for classic SFS and 107 for SFS2. Names are never
silently truncated or renamed. Case collisions are refused for round trips.

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
destination when a selected name already exists. Names are preserved exactly;
names that cannot safely be represented on Windows, case collisions, and paths
through host symbolic links or junctions are rejected.

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
entry; they are not mapped to host permissions or exported as sidecar files.
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

The service embeds its ROM and versioned manifest and uses pinned Copper68k 1.5.4,
M68020 interpreter execution, flat ROM/RAM and bounded storage gateways. Writes require an explicit writable staged source and service opt-in.
The default per-command budget is 50 million instructions; cancellation is checked
every 256 interpreted instructions. There is no managed filesystem fallback or
host Exec/DOS shim. CPU selection and future JIT remain service concerns; this
integration uses its qualified defaults. Host extraction policy lives separately
in `CopperScreenDiskExtraction`.

## Development package

The app references `CopperStart.FileSystem.Service` **[0.2.0-dev.2]** through NuGet.
This development package is local, not published to a feed or checked into Git.
Obtain the producer's built package (CopperStart's
`build/Build-FileSystemService.ps1` creates it) and stage that artifact:

```powershell
./scripts/stage-filesystem-service.ps1 -PackagePath "path/to/CopperStart.FileSystem.Service.0.2.0-dev.2.nupkg"
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release --no-restore
```

The script verifies the pinned package SHA-256:
`366314B88B2E457CD657A7A2333A9F71B5E736A8B16D2B001850F8009A714AD1`.
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
