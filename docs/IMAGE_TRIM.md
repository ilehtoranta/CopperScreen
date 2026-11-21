# Image-manager TRIM, 2026-11-16

CopperScreen and CopperExplorer share CopperDisk.FileSystem `0.1.0-dev.5`, using
the new CopperStart.FileSystem.Service `0.5.0-dev.1` local package. CopperDisk
remains `3.0.1-dev.5`; Copper68k remains `1.5.4`, M68020 interpreter. No package
has been published and existing package versions have not been replaced.

## Execution and storage boundary

The ABI 4 guest records a conservative interval containing allocation frees in
constant mount state. A later `Trim()` command revalidates persisted allocation
with the maintained CopperFS cores and submits only currently free runs. A range
may include allocated metadata or reallocated data; those blocks remain intact.
Each command considers at most 4,096 blocks, retaining mount ownership until its
discard callbacks finish. Overlapping windows preserve alignment opportunities
at command boundaries. There is no retained bitmap-validation cache.

The optional `IDiscardBlockSource` contract reports explicit limits in volume
blocks and accepts synchronous bounded byte discards relative to that volume.
The host validates capabilities, block bounds, lengths, alignment, overflow and
the absence of a guest buffer pointer before invoking it. Read-only sessions and
ordinary writable sources cannot use the discard gateway. Filesystem parsing,
free-space validation and range selection remain in the guest. The 50-million
instruction budget and cancellation polling every 256 instructions are retained.

The shared backend advertises discard only for a successfully probed sparse
working clone. It drains observed frees after the staged operation and before a
fresh guest verification mount and publication. Partition-relative alignment maps
to absolute 64 KiB host allocation units; partial boundary units stay allocated.
Both partitions of an internal move use this path within one clone. Separate-image
or host moves retain destination-first publication and report failed source
removal as the existing partial outcome.

Failed/cancelled TRIM invalidates the service session and prevents publishing
that stage. Protection, active-reader exclusion, original verification, no
overwrite/merge, generation invalidation and cleanup remain in the shared
transaction. Unsupported sparse storage retains ordinary save behavior.

Raw ADF/HDF images retain their logical capacity. DMS trims its decoded working
image before the usual verified recompression; final archive size may shrink.
Historical free space outside the tracked interval is retained. Submitted-block counts
are not a measurement of host bytes reclaimed.

Classic SFS normally recycles files when configured to do so. The portable core
now exposes explicit `RemovePermanent`; service Delete uses it to implement the
manager's permanent-delete contract. Native ordinary Remove still recycles, the
volume flag is unchanged and unrelated recycled files are retained. The portable
FFS TRIM core now admits DOS2–DOS5 as well as DOS0/DOS1. On real Amiga the native
trackdisk.device exclusion remains in place; no native SCSI transport or OS shim
is included in the standalone service ROM.

## Local qualification

- All 79 service tests pass on the actual compiled ROM, including all eight
  formats, fragmented/extension data, multiple trim windows, reallocation,
  persisted bitmap checks independent of the guest scanner, nonzero discard
  readback, corrupt allocation, barrier and partial-discard failures,
  cancellation, open handles, malformed transport ranges and read-only rejection.
- An isolated local NuGet consumer restores the service and pinned CPU without
  source references, browses/extracts all eight formats and performs write,
  delete, TRIM and surviving-file verification on all eight.
- The new shared-backend tests pass 18 cases: raw ADF/HDF and DMS for all eight
  formats, recursive trees, logical capacity, remaining metadata/content and two
  unaligned RDB partition ranges with exact preservation outside the selection.
- CopperExplorer's 16 new packaged-broker cases confirm destructive approval,
  host allocation/archive reduction and surviving-file bytes. Its complete
  managed suite passes 78 tests.
- CopperScreen's desktop suite passes 458 tests; 31 optional ROM/workspace cases
  are unavailable. The new ROM uses 346,982 bytes in the minimum supported
  524,288-byte allocation, with 622 linked symbols. OS, chipset, formatting,
  native handlers, SCSI and automatic-maintenance roots are excluded.
- CopperFS's eight solution test assemblies pass 2,511 cases, with nine optional
  CPX-payload/external-media cases unavailable. All 167 CopperDisk storage tests
  pass. CopperScreen and CopperExplorer Release solution builds pass with no
  warnings or errors.
- Rebuilt standalone 68000 FFS/SFS/SFS2 handlers remain allocation-free. Two
  CopperStart native Kickstart SFS floppy replays pass with the new handler,
  including recovery/remount and concurrent-task access. This is separate from
  the 68020 standalone service-ROM tests and is not full system-ROM qualification.
  Three additional native Kickstart hardfile TRIM replays pass with the rebuilt
  FFS/SFS/SFS2 handlers.

CopperStart's full Release solution build was attempted separately. Unrelated
merge markers in `src/CopperStart.Layers/CopperSharpNativeLayersPlatform.cs`
(including lines 612 and 772) prevent it; a concurrently held DOS output also
reported a sharing violation. Those files were left untouched. Focused service,
package and filesystem replay builds pass. This is not a successful whole-ROM
build qualification.

The service package SHA-256 is
`248F5BE9D717C0CD7343FF9F9CE7383E5B7A2139279E84CDE4DF2B5547F36D87`.
Raw receipts, generated media, maps and local packages are ignored artifacts:
`CopperStart/artifacts/filesystem-service-trim`,
`CopperScreen/artifacts/copperexplorer/trim-*`,
`CopperExplorer/artifacts/trim`, and `CopperFS/artifacts/image-service-trim`.
The x64 broker has also been published locally as a self-contained, untrimmed
build in `CopperExplorer/artifacts/trim/broker-x64`.

Signing, ARM64, CI/private-feed publication and installed Explorer interaction
qualification are outside this slice. Physical Amiga devices, external media and
an actually unsupported Windows sparse filesystem remain unavailable coverage.
No JIT or throughput improvement is claimed.
