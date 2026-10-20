# FFS short-overwrite qualification — 2026-10-19

Development ROM `diskboot-2026-10-19-ffs-overwrites.1`, SHA-256
`4d59a7aeb53c87709c137500bee50fc5e10dd96e833b779abd1c3d4a86c6e46d`, is based on the previously qualified Classic DOS snapshot
plus the FFS overwrite changes. The native core is 494,058 bytes;
graphics remains 30,099 bytes at `$FF8A00`. This is not a package release.
There are 14 bytes between the native core plus its link allowance and the
fixed graphics boundary; future native features will need further size work.

## Behavior and limits

Existing-file overwrites validate the root, the linked pointer-table path and
the data pointers and allocation pages touched by the requested range. They
stop at the final affected block. They no longer inspect every file data pointer
or count the whole volume's bitmap before an overwrite. Extension tables still
need traversal to reach a distant file position; this is not constant-time
random access. Growth and freeing retain full-chain and allocation validation.
The three-block scratch requirement, root invalidation on failed writes, file
protection and device protection remain. TD64 is unchanged. NSD remains excluded.

## Accepted checks

- 52 host OFS/FFS and geometry cases, including corruption preflight, partial
  failure, allocation preservation and 512/1024/4096-byte blocks.
- 6 compiled MC68000 scenarios against both frozen firmware dependencies and
  current project dependencies (12 cases), including an odd-address payload,
  extension-table boundaries, damaged metadata and a partial device failure.
- 2 full-ROM large-file cases / 4 reset-ROM boots on a 4.125 GiB FFS partition:
  a direct 32-byte overwrite crossing 2 GiB and a buffered 15-byte overwrite
  ending at the maximum FFS file length, `0xFFFFFFFF`.
- 33 full-ROM DD/HD, FFS growth/reclamation, explicit HDF and RDB regression cases.
- 6 app/runner full-ROM persistence cases and 10 consumer loader/settings cases.

All accepted cases passed without skips. Large-file checks require DOS flushing,
successful guest `CMD_UPDATE` before host disposal, independent inspection of
the on-disk payload/metadata/allocation, and a fresh protected boot whose whole
image hash remains unchanged. App/runner cases also exercise restart and session
replacement. These are separate host, compiled-code and full-ROM coverage groups.

## Work reduction

A two-byte overwrite near the beginning of a 20,000-block file on an
8,650,752-block volume now takes 26 block transfers and 1,714 longword reads
at 512-byte block size, compared with 5,632 and 1,303,160 in the previous
implementation. The three traversal regressions fail against the old assembly
and pass against the new one; those deliberate baseline failures are not
included in the accepted pass counts.

The unchanged large-file full-ROM workload produced these write-phase totals:

| File length | Previous guest cycles | New guest cycles | Reduction |
| --- | ---: | ---: | ---: |
| `0x80000040` | 45,995,859,582 | 8,930,826,500 | 80.6% |
| `0xFFFFFFFF` | 98,858,656,194 | 17,612,264,000 | 82.2% |

These are guest-cycle comparisons for the entire write phase, including boot,
checks and flushing, not isolated filesystem timings or a host-throughput claim.
The host was shared; replay process priority changes are recorded in the JSON.

## Provenance and reproduction

The [machine-readable record](COPPERSTART_FFS_OVERWRITES_2026-10-19.json) records
ROM, producer, fixture and assembly hashes, test receipts and cycle counts.
Per-case TRX outcomes retain every regression result; some legacy DD/HD
receipt filenames are shared, so their JSON files retain the last density run.
Local evidence is under `CopperStart/artifacts/ffs-range-validation-20261019`.
The final candidate is `native-v4/CopperStart-DiskBoot.rom` with its matching
`disk-boot-test-manifest.json`. Use the existing
`CopperStart/scripts/test-large-ffs-files.ps1` harness to repeat the two large-file
cases. The ROM and media stay local and must not be committed.

The native candidate excludes the concurrent SFS/Exec refactor. Its FFS writer
body matches current source; the shared filesystem interfaces remain in their
original assembly in this frozen snapshot. The large-file engine and regression
engine are separately frozen, as in the previous qualification. App/runner
validation uses the new consumer build and embedded firmware pin.

Initial oversized candidates and the regression launch rejected for a stale
support-assembly identity are failed setup attempts, not accepted guest runs.
The CopperScreen Release solution build passed. The current CopperStart
Release solution build is blocked by seven unrelated compilation errors in
`MorphOsCompatibilityResidentTests` (missing MorphOS resident types/flags).
Focused DOS/filesystem builds and tests passed. This build failure is not
reported as successful qualification coverage.
