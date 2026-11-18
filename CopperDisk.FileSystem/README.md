# CopperDisk.FileSystem

UI-independent, transactional Amiga disk-image operations for DOS0–DOS5, classic
SFS and SFS2. Filesystem execution uses the embedded CopperStart guest ROM through
the pinned 0.5.0-dev.1 service and Copper68k 1.5.4 M68020 interpreter. There is no
Avalonia or Lightweight dependency. Consumers do not compile a ROM during restore.

`DiskImageSession.Open` discovers raw/RDB volumes. Use a `DiskVolume` to enumerate
entries or `OpenRead(entry, offset: ...)` for bounded streaming. Dispose streams
before mutations. Entry records and pages belong to the session generation;
reacquire volumes and entries after publication or refresh.

Imports and mutations clone the image, verify the original and publish a staged
replacement. Existing entries are never overwritten or merged. Links and dirty
SFS volumes are refused. Separate-source moves retain a completed destination if
source removal fails. Callers must obtain destructive confirmation before invoking
rename, delete, move or protection changes.

Image saves use sparse host working files where supported, with ordinary-file
fallback. Before publication, the guest revalidates frees observed during this
staged mutation and discards complete host allocation units. Deletes and source
removal during moves can therefore reclaim nonzero former file data. Unrelated
free data is retained; there is no whole-volume historical free-space scan.
Verified zero-only units are also reclaimed after guest writes. Logical image
lengths are preserved. ADF/HDF publications retain sparse storage. DMS working sectors are
sparse while the final archive uses its normal compression. This reduces host
allocation without changing Amiga filesystem allocation or logical image capacity.
Discard is offered only for a successfully probed sparse stage; ranges are mapped
and bounded to their selected partition. Cancellation, bad allocation metadata or
discard failure prevents publication. Classic SFS uses explicit permanent removal
for the manager's confirmed Delete, without changing the volume recycling policy.

`HostNameCodec` maps complete sibling snapshots to reversible Windows aliases.
Extraction uses these aliases recursively; imports restore canonical aliases by
default. Present `Preview` before import and offer explicit literal-name mode.
IPF volumes are read-only and every accessed sector must pass strict decoding.
Complete, unencrypted standard DD DMS archives support browsing and all existing
image edits through the same guest/transaction path. Saves preserve header
metadata, use SIMPLE/RLE with stored fallback, verify the encoded stage against
every edited sector and publish only after original-archive verification.
Archives with auxiliary/unsupported records, trailing data or unsupported flags
remain read-only. Corrupt, incomplete, encrypted, HD and non-Amiga archives are
rejected. No archive data is silently discarded; compression and size may change.

This package is a development version. Runtime packaging does not grant rights
to redistribute third-party disk images.
