# CopperStart Full filesystem ROM pair — 2026-11-05

This records the retained first qualification. The current [SFS2, formatting and
floppy-write qualification](COPPERSTART_FULL_FILESYSTEMS_V2_2026-11-05.md) supersedes its product limits.

The development A1200 Full profile pairs a 512 KiB reset ROM at `$F80000`
with a 512 KiB filesystem extension at `$E00000`. CopperScreen admits the
combined 1 MiB file, extension first, as one SHA-256-pinned firmware artifact.
The CopperOS desktop seed and existing user disks retain their identities.

OFS (`DOS0`), FFS (`DOS1`) and classic SFS (`SFS0`) boot directly from either
an explicit partition or RDB discovery. Reads, writes, create/delete, rename,
directory operations, metadata and flushing use guest DOS packets and the
existing block device. DOS2-DOS5 retain the core's read-only support. SFS2,
DOS6/DOS7, formatting and complete AmigaOS filesystem/API compatibility are
outside this qualification.

The reset bank retains mount preparation. The extension supplies the common
filesystem packet task, SFS engine and native DOS client service. There is one
filesystem state per mount; moving executable code does not add a second
owner, copy the state or install host OS callbacks. Routine bodies execute
from ROM. The Full bridge uses `CSED` (private ABI 14); its task pointer occupies
the reserved longword at extension size minus 24. Both signature and executable
range are checked before DOS mount preparation. The existing standalone SFS
handler and older extension profiles retain their packaging ABI.

The first Full A1200 compilation overflowed the reset bank before graphics.
Moving the shared packet task to the extension makes the reset code fit:
482,120 compiled bytes, plus 28,252 graphics bytes in the fixed upper layout.
The extension has 300,656 compiled bytes. These are footprint results, not
throughput measurements. The Full reset exports the graphics library name for the composer. The extension also records the matching reset bank graphics resident address; boot reads that immutable address once.

Build from CopperStart:

```powershell
./build/Build-DiskBootRom.ps1 -A1200 -FileSystemProfile Full -ArtifactRoot artifacts/full-filesystems
```

The output includes `CopperStart-DiskBoot.rom`, `CopperStart-SfsExtension.rom`,
the combined `CopperStart-A1200.rom` and `disk-boot-manifest.json`. Keep each
pair together. A handler stored only in `L:` would require an accessible
bootstrap filesystem before the initial SFS volume could be mounted.

| Artifact | SHA-256 |
| --- | --- |
| Reset ROM | `6FFA0F3ED0D08C9C0C215E0EB91093FC0EB3B844ACAF4D4DB40DF69560998DEB` |
| Extension ROM | `E77C241EBFFCA242A3D27EBC641772F563264D89CCA5922FAD4148FDB336DE37` |
| Combined image | `90CC75EDACFB094476186AFFAC6125A78BB0FFE37A2C94D35776D0F31578B0F4` |

Exact frozen inputs, source hashes, native maps, compatibility reports, logs and
TRX files are retained locally under `artifacts/full-filesystems-20261105/`.
The source snapshot preserves existing uncommitted producer/compiler/engine
work. OFS/FFS consume the maintained CopperFS writers; the SFS core remains the
producer's pinned source dependency. No package or firmware release is published.

Validation on the frozen source graph:

- Eleven native A1200 storage cases pass: OFS/FFS/SFS explicit and RDB boot,
  create/write/overwrite/rename/delete/metadata/flush, fresh protected readback,
  first-boot write protection, and missing/foreign extension rejection.
- Thirty selected consumer checks pass, including profile round trips, native
  graphics/text DMA, desktop interaction, reset and fresh process readback.
  Two optional supplied-ROM/game cases are unavailable coverage.
- CopperFS host suites pass 201 OFS/FFS, 18 handler semantic and 954 SFS/SFS2
  cases. Three optional external cases are unavailable. Both standalone native
  handler targets rebuild; their default profiles do not enable the Full bridge.
- The initial Full desktop took 944 fields and failed the unchanged 850-field
  gate. An external trace identified repeated SFS root probes after empty DF0's
  DOS read failed. The Full mount path now stops on device read failure while
  retaining declared SFS secondary-root recovery. No trackdisk commands or
  chipset execution were changed. Final desktop readiness is 684 fields
  (13.701810738688346 guest seconds); redraw is eight fields. The original
  ROM control reaches readiness in 603 fields in the bounded external probe.
  No host FPS or wider performance acceptance is claimed.

The A1200 product contract still requires empty floppy drives. A separate DD
write/export probe did not finish within its 5,000-field bound with either the
original qualified ROM or the Full candidate. Its failed logs and source remain
under the local task directory; it is not successful floppy coverage or
part of this HDF qualification. The broader DD/HD combination remains unqualified.

The discarded read-dispatch optimization and frozen-graphics experiment did not
improve desktop readiness. Their artifacts and failures are retained; neither is
part of the final source change. The final pair uses the current frozen source
snapshot's normal graphics emitter.

The production CopperScreen Release and CopperStart-enabled Release builds both
pass with zero warnings/errors. CopperFS Release passes with one existing
compiler warning. CopperStart Release passes with nine existing warnings, using
the authoritative local SDK projects and the public preview package metadata
override for excluded package assets. The same 30 consumer cases and 25 partition
cases pass from the working trees. The runner accepts the paired image in a
one-field loader smoke; that smoke is not a native replay or throughput result.
