# Sparse HDF storage (development)

HDF remains a raw image of its full virtual capacity. Sparse storage changes host
allocation, not guest geometry or the image format. New images and restored
backups request sparse storage. Existing and imported images default to disabled.
The nullable `SparseStorage` profile field preserves an explicit choice; an absent
field enables only creation of a missing image, never an existing image.

While stopped, select **Configure Amiga → Hard disks → Enable sparse storage**.
This prepares the file and saves the disk preference with the machine settings.
Enable guest writes to permit discard. Reclamation is an explicit guest operation:

```text
CopperTrim DH0: CHECK
CopperTrim DH0: DRYRUN
CopperTrim DH0:
```

Install the matching development handler and command. CopperTrim supports our
DOS0/OFS, DOS1/FFS, classic SFS and SFS2 handlers through the same version-1 packet. The old
CopperSFSTrim name remains a compatibility alias. Old handlers do not gain TRIM
from the host setting alone. The currently qualified desktop reset ROM is not
replaced by this development handler build, and no volume is automatically trimmed.

OFS/FFS requires a valid allocation bitmap. The handler holds the mount, durably
flushes pending writes, and validates all bitmap pages and extension pointers
before discarding anything. Unvalidated volumes, checksum damage, missing or
aliased bitmap pages, malformed extension chains, and metadata marked free are
rejected. TRIM does not repair a filesystem. Failed requests can have discarded
part of their range; completed counters exclude that request and retry scans afresh.

OFS uses the CopperFFS handler for read access and manual TRIM. Its normal OFS
writer remains separate: stop/unmount it before using the maintenance handler,
and unmount the maintenance handler before returning to normal writes. The FFS
writer stays disabled for OFS. Only one handler may own a partition at a time.

The command counts filesystem blocks submitted for discard, not host bytes
reclaimed. Hard-disk settings display virtual bytes and host allocated bytes
separately. Allocation is unavailable when the host query cannot be trusted or
completed. Host allocation can lag buffered writes; durable measurements require
a completed storage flush. Host space can run out before the virtual disk is full.

## Backing storage

`CopperDisk.AmigaSparseFile` provides maintenance-only operations. Normal hardfile
reads and writes keep their direct FileStream path. A disposable sibling of the
resolved backing file probes capability without changing live image bytes.
Unsupported hosts/filesystems retain ordinary HDF operation and advertise no UNMAP.
Writable, opted-in images advertise discard only after successful preparation.

Windows uses `FSCTL_SET_SPARSE` and `FSCTL_SET_ZERO_DATA`; Linux uses
`fallocate(PUNCH_HOLE | KEEP_SIZE)`; macOS uses `fcntl(F_PUNCHHOLE)` with separate
ARM64 variadic ABI marshalling. Requests round **inward** to 64 KiB units. Boundary
fragments remain allocated, and small requests may reclaim nothing. Logical file
length is unchanged. The controller retains its bounds, synchronous ordering,
partial-failure behavior and absence of a guaranteed zero-read promise.

Windows allocation comes from `FileCompressionInfo`; Linux/macOS x64 and ARM64
use `st_blocks`. Unsupported ABIs return unavailable. APFS snapshots and other
host sharing can retain physical space independently of an image's allocation.

References: [Windows sparse operations](https://learn.microsoft.com/en-us/windows/win32/fileio/sparse-file-operations),
[Linux fallocate](https://man7.org/linux/man-pages/man2/fallocate.2.html),
[Apple fcntl definitions](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/fcntl.h),
[Apple ARM64 calling convention](https://developer.apple.com/documentation/xcode/writing-arm64-code-for-apple-platforms).

## Backups

`.copperhdf` keeps manifest version 1, full logical lengths and SHA-256 verification.
Image ZIP entries are compressed. Restore hashes every logical byte, skips zero
output chunks in a newly created sparse file, and explicitly sets the full length,
including trailing holes. Old uncompressed entries remain readable. Publication,
staging journals and failure cleanup retain their existing behavior. Unsupported
destinations restore ordinary files and report the unavailable sparse capability.

## Validation

Host storage checks are in `CopperDisk.Tests/SparseHardfileTests.cs`; real UNMAP
above 4 GiB is exercised by the separate engine diagnostic suite. Desktop tests
cover both ZIP encodings, logical hashes, sparse tails, old profiles, persistence,
corruption and cleanup. `HardDiskSparseRestoreFailureTests` additionally uses the
existing `CopperScreenInstalledTests=true` diagnostic build to exercise disk-full
injection and actual child-process termination during restore.

The CopperSFS repository owns version-4 scanning and recovery tests. CopperStart's
`build/Test-SparseHdf.ps1` builds a development handler/command/extension set and
runs compiled guest write/delete/TRIM/rewrite/cold-reboot tests. It records hashes
and a host-specific receipt. No packages, firmware or media are published by it.

CopperStart owns the OFS/FFS scanner in `AmigaFfsTrim.cs`. Build its standalone handler
with `build/Build-SfsHandler.ps1 -Ffs` and the shared commands with
`build/Build-Trim.ps1`. `Test-SparseHdf.ps1` now qualifies OFS and FFS as well as SFS/SFS2,
including poisoned partial discard, fresh retry, rewrite and cold reboot.

The OFS development-v3 run on 2026-11-04 passed 139 managed filesystem tests and
all 12 native cases with no skips. OFS cases cover production TRIM, a no-TRIM
control and destructive partial-discard retry. Four cold boots switch exclusively
between the ROM OFS writer and the CopperFFS maintenance handler. A live file and
the rewritten data survive; independent checks validate OFS headers and bitmaps.
After rewrite and reboot, the OFS production image retained 16 MiB of logical
capacity while host allocation fell from 16 MiB to 512 KiB. The control retained
16 MiB of allocation. Measurements and the receipt are under CopperStart's
`artifacts/ofs-qualification/`. Both extension variants built successfully.

Windows/NTFS has been exercised on this development host. Linux/ext4 and
macOS/APFS implementation is present but runtime qualification is unavailable
here (no Linux environment or Mac). Run the same storage and native suites on
each host before claiming that platform qualified. Full extension-ROM boot
qualification is separate from the compiled standalone handler replay.

### Windows development results, 2026-11-04

| Check | Result |
| --- | --- |
| CopperScreen production Release solution | Passed, no warnings |
| CopperStart Release solution | Passed, existing warnings |
| CopperDisk | 108 passed |
| Desktop storage/profile/session tests with installed-test hooks | 105 passed, 13 optional tests unavailable; child-only entry skipped in parent run |
| Engine diagnostic suite (separate outputs) | 1300 passed, 7 optional tests unavailable |
| Final focused CopperHDF discard checks | 11 passed |
| CopperSFS suite | 837 passed, 1 external-media test unavailable |
| Final classic/SFS2 partial-request counter checks | 3 passed |
| Compiled native handler/command tests | 6 passed, none skipped |

The native qualification receipt and binary SHA-256 values are local artifacts at
`CopperStart/artifacts/sparse-hdf/qualification.json`; its accompanying TRX records
the six cases. This is correctness evidence, not a throughput measurement.
The receipt used CopperSFS `3cb300be936712f16dd2a0a83423fa1fc687e430`.
The subsequent pin `cd606a9` adds only assertions that completed-range counters
exclude partially failed requests; production code and native binaries are unchanged.

The subsequent FFS/CopperTrim development-v2 run on 2026-11-04 passed nine native
cases with no skips, 112 managed filesystem tests (including 27 FFS TRIM cases),
and the 11 focused CopperHDF discard diagnostics. Both Release solutions built.
The matching handler/command/extension hashes and native TRX are under
`CopperStart-sparse-publish/artifacts/ffs-trim/`; source integration is on that
worktree's `codex/ffs-trim` branch with CopperSFS pin `ee11ac9`. The original
CopperStart checkout's unrelated local edits were preserved. This development
qualification does not replace the currently pinned desktop reset ROM.
