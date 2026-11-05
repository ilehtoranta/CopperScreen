# Full filesystem qualification v2 — 2026-11-05

The A1200 Full ROM pair now supports native OFS/DOS0, FFS/DOS1, classic SFS
and SFS2 cold boot, reads, writes and quick formatting. OFS/FFS DD and HD DF0
writes and formatting are qualified through MFM export and a fresh protected
boot. This supersedes the [first Full qualification](COPPERSTART_FULL_FILESYSTEMS_2026-11-05.md).

Consumer identity: `diskboot-2026-11-05-a1200-full-filesystems.2`. The producer
base is CopperStart `395753337557b72b2c093adb01776fe4556bc712`, plus the retained
frozen source graph and changes. This is a development qualification; no package
or ROM was published.

| Artifact | SHA-256 |
| --- | --- |
| 512 KiB F80000 reset ROM | `415E9F11BA08E07D944500B2B310E60282C2D87F74A7E2D387E57A81610C224C` |
| 512 KiB E00000 extension | `DDC4759B5EA353AEDFC52BAFE87B499B9DBFB18331A3EF3A67B384AA452FE2D0` |
| 1 MiB extension-first pair | `269C41EE42F89859769BE7F171F7D18C0528BB5C1C38EFBE25ECF1F16A380962` |
| Unchanged CopperOS HDF seed | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The pair uses private ABI `CSEE`/15. Its extension contains the common filesystem
packet task, both SFS engines and native DOS client service. Immutable metadata
identifies the matching reset-bank graphics resident. Both banks operate on one
mount state, and system routine bodies execute from ROM. No host OS callbacks
or chipset/CPU execution changes were introduced. Compiled reset code/data is
482,274 bytes, with 28,254 graphics bytes; extension code/data is 376,950 bytes.
Both compatibility reports have zero managed allocation sites.

## Native guest qualification

All **38** cases pass without skips on PAL Alice/Lisa EC020 with 2 MiB Chip RAM,
no slow/Fast RAM and matching DD/HD drive types. Fixtures are generated original
media; no licensed ROM or game images are used.

| Coverage | Cases | Result |
| --- | ---: | --- |
| OFS/FFS/SFS/SFS2 explicit and RDB boot, write/flush, protected cold reopen and first-boot protection | 12 | Passed |
| Missing or foreign extension rejection | 2 | Passed |
| Blank and existing RDB partition formats, cross-format transitions, active-lock rejection, new volume-name lookup, writes and protected reopen | 12 | Passed |
| OFS/FFS DD/HD writes, track cache and extra buffers, MFM export and cold protected reboot | 8 | Passed |
| OFS/FFS DD/HD blank-floppy format, initial protection, writes, MFM export and cold protected reboot | 4 | Passed |

The floppy checks decode all 11 DD or 22 HD sectors on dirty tracks and inspect
payload, file metadata, block checksums, allocation bits, OFS forward links and
reclaimed temporary blocks. HD write fixtures force data allocation above block
1800. Protected replays preserve media byte-for-byte.

Formatting is performed through `Format()`/`ACTION_FORMAT`; classic SFS also
uses the tag-based packet used by CopperOS `Format SFS`, including inhibit and
uninhibit. OFS/FFS bitmap extension chains and SFS/SFS2 root/admin/object/node/
extent metadata are initialized with caller-owned scratch. Device errors and
protection propagate. The handler invalidates caches, recreates writer resources
and publishes the new DOS volume entry. Four persisted SFS/SFS2 format outputs
also pass the separate CopperSFS.Check tree/ownership/bitmap checker. Its
NORECYCLED sentinel and recycled-file counter checks have positive and corruption
regressions.

## Host and app evidence

- CopperFS: 201 existing OFS/FFS, 954 SFS/SFS2, 18 handler semantic and 19 formatter
  checks pass (1,192 distinct host cases). Three optional original/external-media
  tests are unavailable coverage. Portable formatter checks include block sizes,
  bitmap extension chains, I/O failure, protection, case sensitivity and recycling.
- CopperDisk: 26 partition geometry/contract checks pass, including SFS2.
- CopperScreen: 34 distinct selected app checks pass; two optional supplied-ROM/game
  cases are unavailable. Native desktop launch, interaction, reset/reopen and
  graphics/text DMA remain covered. Desktop readiness is unchanged at 684 fields
  (13.701810738688346 guest seconds); redraw is eight fields. The existing
  850-field readiness and 17-field redraw bounds were retained.
- Production Release solution builds pass for CopperScreen, CopperStart and
  CopperFS. CopperStart uses the existing local CopperSharp SDK override for the
  frozen graph. Existing producer/compiler warnings are retained. Standalone
  CopperFFS, classic CopperSFS and SFS2 targets rebuild separately.

The app admits one OFS/FFS DD/HD DF0 disk and/or one existing hard disk image on
unit 0 with explicit or RDB OFS/FFS/SFS/SFS2 partitions. A1200 DF0 protection can
be changed without a CopperOS workspace; Save ADF persists its in-memory writes.
The normal seed/workspace ownership and disabled-build payload rules remain.

## Reproduction and limits

From CopperStart:

```powershell
./build/Build-DiskBootRom.ps1 -A1200 -FileSystemProfile Full -ArtifactRoot artifacts/full-filesystems
$env:COPPERSTART_FULL_FS_ARTIFACT_ROOT = (Resolve-Path artifacts/full-filesystems).Path
dotnet test tests/CopperStart.Floppy.Native.Tests/CopperStart.Floppy.Native.Tests.csproj `
  -c Release -p:CopperStartNativeSfs2=true -p:CopperStartFullFileSystemExtension=true `
  -p:CopperStartGraphicsExtension=true -p:CopperStartSingleBankGraphics=true `
  --filter 'FullyQualifiedName~A1200FullFileSystemsColdBoot|FullyQualifiedName~FullFileSystemBootRejects|FullyQualifiedName~A1200FullRomFormats|FullyQualifiedName~A1200FullRomOfsFfsDdHd'
```

Frozen sources, compiler runtime, input hashes, native maps/reports, media,
checker output and final TRX files are retained locally under
`artifacts/full-filesystems-v2-20261105/`; the qualified pair is `rom-full-v5`.
The final floppy cases were partitioned into four independent test processes
with unchanged ROMs, fixtures, assertions and frame bounds. Earlier narrow-buffer,
short-bound and intermediate-source diagnostics are retained separately and are
not successful qualification evidence.

Quick format initializes filesystem metadata; it does not install a boot program,
rewrite the RDB, scan the surface or erase every data sector. Insert target floppy
media before boot; Full-profile disk-change registration is not added. DOS2-DOS5
remain read-only, DOS6/DOS7 are outside this profile, and SFS2 formatting disables
recycling. Existing 48-bit SFS2 core tests do not establish native large-volume
or original-handler interoperability coverage. The optional original-handler
replays remain unavailable. No host FPS or broader throughput result is claimed.
