# Offline hard disk setup

Settings > Storage > Hard disks & folders can create a formatted RDB image or
add a formatted partition to the free cylinders of an existing RDB image.
The Amiga must be stopped. Existing disks are edited by saving a **new copy**
and attaching it; the source is never overwritten. Removing, resizing and
reformatting existing partitions are outside this editor's scope.

Choose **Create disk image…** for a new image. The filesystem picker defaults to
FFS and also offers Unformatted, SFS, SFS2 and CopperNova (CNFS). CNFS remains
experimental. Unformatted hides
the partition options and adds a blank image that is created on the next Amiga
start; format it inside the Amiga before use. Formatted images are created and
verified immediately. Choose **Add partition and format…** on an existing disk
configured with RDB layout. Enter a new destination filename, disk capacity,
partition size, filesystem and volume label. Disk and partition sizes accept
fractional **MiB** or **GiB** values. **Use available space** fills one contiguous
free area; turn it off to set a partition size and leave space for more partitions.
Automatic placement chooses the first free area large enough for that size.
The preview shows actual aligned partition size and remaining free space.

Cylinder boundaries are calculated internally. **Advanced geometry** shows the
device geometry and offers **Use explicit cylinder range** for an exact override;
that override replaces the normal partition-size input and must fit one free area.
Existing images retain their geometry. New images use 2 heads, 32 sectors per
track and 512-byte device sectors (32 KiB cylinders), reserve cylinder 0 for RDB
metadata, and require at least 8 MiB. Disk capacity is rounded down to complete
cylinders; partition sizes are rounded down to complete cylinders and filesystem
blocks. The preview exposes the resulting sizes before creation.

**Filesystem block size** offers **512 bytes, 1 KiB, 2 KiB and 4 KiB** for all four
formatters, defaulting to 512 bytes. This is independent of cylinder size and the
512-byte device sector size. RDB metadata records the selected sectors-per-block
value, and formatting and verification use that same block size. The guest's
filesystem handler must support it; host formatting checks do not establish
native guest compatibility for every block-size/ROM combination.

| Choice | Partition DOS type | Guest requirement |
| --- | --- | --- |
| FFS | DOS1 / `$444F5301` | FFS handler or compatible ROM |
| SFS | `$53465300` | Classic SFS handler or compatible Full ROM |
| SFS2 | `$53465302` | SFS2 handler or compatible Full ROM |
| CopperNova (CNFS) | CPX4 / `$43505834` (unchanged) | Matching experimental CNFS handler |

Formatting installs filesystem metadata, not an operating system, boot files or
an RDB filesystem handler. A bootable flag alone does not make a disk bootable.
The qualified CopperStart ROM restrictions still apply; selecting a filesystem
here does not add that filesystem to the ROM. CopperNova (CNFS), previously
called CPX, is the new CopperFS filesystem. The rename leaves its on-disk format
and CPX4 DOS type unchanged. Its development format and guest compatibility may
change. Creating it requires an explicit experimental-volume checkbox.
The current UI accepts 1–30 Latin-1 label characters without NUL, colon or slash.
New disks start read-only. A partitioned copy retains the source disk's saved
write-protection setting.

## Bundled dependency

Formatting is included in the normal desktop UI and the standard Release build;
there is no feature flag or CopperFS checkout requirement for the app or its
build. The host-only `CopperScreen.FileSystems` project bundles an exact source
snapshot of the maintained CopperFFS, CopperSFS and CopperCPX cores. It does not
reimplement their on-disk formatters. Its published CopperSharp host SDK package
is pinned in the normal NuGet locks; there is no compiler checkout requirement.

The [dependency notes](../CopperScreen.FileSystems/README.md) explain how to
update the snapshot from CopperFS. Its manifest records the source repository,
base commit, working-tree status and every imported file's SHA-256. Build checks
enforce those hashes. CPX's uncommitted development inputs are identified by
their exact bytes rather than represented as a released upstream version.
The source license and provenance manifest accompany the app's build/publish
outputs. No package or ROM publication is part of this integration. Native guest
qualification remains separate from authoring an image on the host.

## Preservation and limits

The editor accepts consistent, checksum-valid 512-byte RDB layouts whose
partition environment vectors use 512-byte device sectors, whole filesystem
blocks and agree with the disk
geometry. It includes disabled partitions in overlap checks, checks metadata
chains and the RDB high-water mark, and requires an unused metadata block.
Unsupported, overlapping, cyclic or damaged layouts fail closed. The editor is
bounded to 128 partitions, 1,024 referenced metadata blocks and a 65,536-block
RDB reserved area. Other layouts can still be mounted through existing APIs;
these bounds apply to authoring only.

The source stays open against writes/replacement during copying. The new
partition must fit entirely in free whole cylinders. Copying preserves all
source bytes outside the new partition and the necessary RDB header/new PART
updates, including existing partition metadata, boot priorities and handlers.
Zero ranges are kept sparse when supported. Copies can take substantial time
and host space. Cancellation or any formatting/verification failure removes the
temporary file and leaves the source and destination unchanged. Completed files
are flushed and published under a new name without replacement; settings switch
to the copy only after publication. Process termination can leave an unpublished
`.disk-setup-*.tmp` file beside the destination; setup does not resume it.

All formatter I/O is bounded to the new partition. FFS/SFS volumes are checked
through CopperFS's readers and CNFS through a fresh read-only mount before the
new image is published. These are host checks, not native guest replay or
boot-compatibility qualification. No recurring emulator execution path changes.

## Validation

```powershell
dotnet build CopperScreen.slnx -c Release
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --filter FullyQualifiedName~AmigaRdbSetupTests
dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release --filter "FullyQualifiedName~HardDiskSetupTests|FullyQualifiedName~DiskSetupCreatesAFormattedImage|FullyQualifiedName~DiskSetupRequiresAStoppedMachine"
```

The normal desktop tests cover all four formatters, preserving existing partition bytes
in a copied disk, overlap/label rejection, cancellation, destination preservation,
and the visible new-disk/add-partition buttons and dialogs including CNFS acknowledgement. They use
synthetic temporary images, with no ROM or media committed.
