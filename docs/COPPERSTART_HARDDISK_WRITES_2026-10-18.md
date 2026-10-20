# FFS hard-disk writes through the app and runner

**PASS:** four full-ROM cases across nine machine boots, zero failures and zero
skips. Eleven host command-line configuration checks also pass. Release solution
build: zero warnings and zero errors. The [qualification receipt](COPPERSTART_HARDDISK_WRITES_2026-10-18.json)
records the test names, exact app/runner binary hashes and retained artifacts.

Both front ends reproduce the exact qualified saved image SHA-256
`74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046`
from pristine image SHA-256
`D2D23EAB4C75E5BAAADF99F5D59E0A36F7454FCDDDCA512153D96A8CD2C49F6E`.
All restart/readback paths leave that saved image byte-identical, and both
protected-image controls reject the first mutation with error 214 without
changing their pristine images. Guest flush is observed before host save or
disposal; the app's explicit host save advances no guest cycles.

This continuation exercises the pinned `diskboot-2026-10-18-hdf.1` ROM through
the normal application runtime and actual command-line runner process. It uses
PAL OCS, 68000 interpreter, 512 KiB Chip + 512 KiB A501 slow RAM, no Fast RAM,
RTC or floppy medium. DH0 is one explicit DOS1 partition: 512-byte blocks,
two heads, 32 blocks per track, inclusive cylinders 4–58, byte offset 131072,
length 1802240. The ROM is 524288 bytes, SHA-256
`FC6511086BB86A33A04887FBF2EF1D7F82A91631853E39DB08529AD0C0314AC0`.

The original producer fixture `copperhdf-track.hdf` is hash checked against
`74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046`.
The test creates a pristine private copy by unlinking `Data/Saved`, freeing and
clearing its six allocated blocks, and updating the directory and bitmap
checksums. The qualified original MC68000 WriteProbe HUNK remains unchanged.
Neither the supplied fixture nor ROM is modified or committed. No host DOS
implementation or guest-memory patch creates the test results.

## Checks

The native command creates a directory and file, writes 2049 bytes, overwrites
32 bytes starting at byte 509 across a sector boundary, renames the file, removes
temporary directories/files, truncates a temporary file, and sets file
protection, comment and date. It checks content, metadata and EOF through native
DOS calls. `Flush(fh)` drains the DOS stream before close; `DeviceProc` plus
`DoPkt(ACTION_FLUSH)` asks the filesystem to complete `CMD_UPDATE`.

Host observation checks the final device command is 4, its error and actual
length are zero, and the native flush reply succeeded. It independently decodes
the live FFS backing image, checks all 2049 patched bytes, metadata, allocated
blocks, free-space accounting, directory/root/bitmap/file checksums, deleted
entries, and unchanged bytes outside the partition. Diagnostic RAM inspection
uses snapshots of Chip/A501 RAM and does not perform guest bus reads.

The application case restores writable settings through a saved profile and
the last machine. The normal runtime owns execution and consumes presentation
frames. After the guest flush, it pauses and inspects the backing file **before
host save or disposal**. It then exercises `SavePersistentStorageAsync`, verifies
that host save advances no guest cycles, resets the machine, replaces the paused
session using the standard shared backing lease, and finally starts a new
protected machine from a copy of the saved bytes. Every readback must preserve
the complete backing image. Reset retains RAM, so the test requires the new
readback branch and its completed device flush rather than accepting the old
completion word.

The runner case launches the real DLL with `--hdf-ffs-writable`, the exact ROM
and explicit geometry. It uses a 2000-field write run and 1000-field readbacks.
New optional
`--boot-probe-storage` snapshots copy each configured HDF while the machine
still owns its open backing file. Observation deliberately does **not** flush
the source. The in-process snapshot must already contain the correct saved
filesystem and agree with the image after exit. A second writable process must
take the native readback branch, followed by a fresh protected process from a
copy of the saved bytes. RAM receipts, device flush completion and full disk
contents are checked for all three processes.

Two additional native cases boot pristine protected images through the app and
runner. The first mutation must return AmigaDOS `ERROR_DISK_WRITE_PROTECTED`
(214), leaving the entire backing file unchanged.

## Reproduction

```powershell
./scripts/test-copperstart-harddisk-writes.ps1 `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -HardDiskFixturePath C:\Fixtures\copperhdf-track.hdf `
  -OutputDirectory artifacts/hdf-write-proof
```

The script builds isolated app/runner outputs, runs all four intended native
cases and rejects skips. An optional `-RunnerPath <runner.dll>` selects an
already built runner. Test inputs are explicit environment variables when
running directly: `COPPERSCREEN_COPPERSTART_ROM`,
`COPPERSCREEN_COPPERSTART_HDF`, `COPPERSCREEN_LIGHTWEIGHT_RUNNER` and optional
`COPPERSCREEN_HDF_WRITE_OUTPUT`. Without the relevant inputs the optional
native cases report unavailable coverage.

These are source app/runtime and command-line compatibility checks, separate
from [producer filesystem qualification](COPPERSTART_HARDDISK_2026-10-18.md),
host semantic tests, newly compiled MC68000 tests, installed-package validation
and performance acceptance. These compatibility diagnostics do not measure
host throughput or certify cycle timing.
