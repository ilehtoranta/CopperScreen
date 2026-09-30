# Native Kickstart 3.1 hard-disk boot — 2026-09-30

Native A500 Kickstart 3.1 v40.63 now cold-boots a supplied Workbench 3.1 FFS/RDB
hardfile with **DF0 empty**, on the default 68000 with **0 or 2 MiB Fast RAM**.
The guest writes a proof file; a fresh process/session reads it from the backing
file and writes a separate reopen marker. Both boots reach the reviewed
Workbench desktop. This required correcting an invalid DOS lock in CopperHDF's
boot metadata. Copper68k remains the public pinned **1.5.0** package.

The [machine-readable record](KICKSTART_31_HD_2026-09-30.json) binds source,
licensed input and fixture hashes, native results and local artifact paths.
Base revision is `942e00609e7e35b04c6449189e5020e3999c4012`, after the
[desktop/floppy boot slice](KICKSTART_31_2026-09-30.md). No ROM, Workbench disk or
hardfile is committed.

## Failure and proper fix

Before the correction, native DOS raised Guru **80000003** before CopperHDF's
first device Open/Read request. The scalar trace located the first address error
at field **154**, cycle **21923244**, source PC `$F81852`, opcode `$2B48`.
The same failure occurred with an OFS RDB fixture under 3.1.

CopperHDF initialized `DeviceNode.dn_Lock` to `$FFFFFFFF`. DOS treats a nonzero
lock as a BPTR to a `FileLock`; its `fl_Task` field is a message-port pointer.
These are native DOS structures, not a driver-specific sentinel.
See [AmigaDOS data structures](https://wiki.amigaos.net/wiki/AmigaDOS_Data_Structures).

In the captured execution, converting that invalid BPTR produced `$FFFFFFFC`.
Reading `fl_Task` at offset 12 wrapped to address 8, the bus-error vector,
whose value was `$F80ACE`. DOS used that ROM address as a message port. `PutMsg`
then read ROM instruction bytes `$615C615A` as a soft-interrupt pointer and
corrupted the Exec soft-interrupt list at `$C00CC2`. The later address error was
a correct CPU response to invalid metadata.

An isolated diagnostic probe ran each supported ROM's own
`expansion.library/MakeDosNode` through ordinary guest instructions using its
[documented input packet](https://developer.amigaos3.net/autodocs/expansion.library/MakeDosNode.html).
Both v34 and v40 returned **zero `dn_Lock`**. The correction removes CopperHDF's
`-1` initializer so the cleared node retains zero. Explicit loaded-filesystem
patch flags still apply normally. Other node defaults, CPU instructions,
chipset ordering and the device gateway path are unchanged. The controller
regression failed before this correction and passes afterward.

## Native evidence

The disposable fixture derives from the supplied Workbench 3.1 rev 40.42 DOS1
FFS ADF. It adds a checksum-valid RDB with 32 reserved sectors and one bootable
DH0 partition (cylinders 1–55, 32 blocks/cylinder). It replaces Startup-Sequence
with a bounded script that assigns ENV/T, writes/reads the proof and runs LoadWB.
The generator creates a new file only; unused old startup data blocks remain
allocated. This is a test fixture, not a full OS installer. The guest follows
the native [hard-disk boot contract](https://wiki.amigaos.net/wiki/SCSI_Device).

| Check | Result |
| --- | --- |
| Production Release build | Pass, zero warnings/errors |
| Engine diagnostics, isolated outputs | 802 passed, zero skipped |
| Default host suite | 124 passed; five optional entries unavailable |
| Supplied native HD desktop-session tests | Two passed, zero skipped; each runs two cold boots at 0 or 2 MiB Fast RAM |
| Runner cold boot and reopen | Four runs, 2,400 fields each, no floppy mounted; proof and reopen files independently verified |
| Normal versus scalar, 2 MiB cold boot | All 35 captures match byte for byte; final backing HDF SHA-256 also matches |
| Native KS 1.3 OFS compatibility control | 2,400 fields, 2 MiB Fast RAM, DF0 empty; native 39-byte proof independently verified |
| Independent FFS verifier controls | Rejects absent proof, changed data with valid headers, and bad header checksum |

The first boot writes **47 bytes**, `CopperHDF Kickstart 3.1 native FFS persistence`
plus LF. After disposal, a fresh boot reads that file and writes **44 bytes**,
`CopperHDF Kickstart 3.1 FFS reopen verified` plus LF. The reopen marker is absent
after the first boot and present after the second. The independent Python reader
uses no CopperDisk code and checks root/header checksums, directory chains,
parent/type, size and single-block FFS contents. The optional host tests use a
separate bounded reader and copy the supplied pristine hardfile before writing.

All four final runner captures and both phases of both native host cases match
the reviewed desktop pixel SHA-256
`6d9d59261ac424a3461a96ef8e518128b5cfbbcea11e5cd69bbc61798f7a30d1`
(908 × 313 full-beam, little-endian ARGB). Raw logs, traces, frozen phase files
and screenshots remain under ignored `artifacts/kickstart31-hd-2026-09-30/`.
The final fixture/runs are under `v2/`; earlier failures and the first fixture's
ENV requester remain separate diagnostic evidence.

## Reproduction

Use the unmodified ROM and Workbench bytes identified in the JSON record. The
output hardfile must not exist. Native tests copy it to disposable storage.

```powershell
python scripts/probes/Kickstart31/prepare_hdf.py "path/to/Workbench31.adf" "path/to/pristine-ks31.hdf"
$env:COPPERSCREEN_KICKSTART31_ROM = "path/to/kickstart-3.1-a500.rom"
$env:COPPERSCREEN_WORKBENCH31_HDF = "path/to/pristine-ks31.hdf"
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --filter FullyQualifiedName~SuppliedNativeWorkbench31HardDisk
```

For runner evidence, copy the pristine image to a disposable HDF and execute
this command twice on the same file, allowing each process to exit normally:

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom "path/to/kickstart-3.1-a500.rom" --hdf "path/to/disposable.hdf" --fast-ram-kib 2048 --frames 2400
python scripts/verify-lightweight-ofs-proof.py "path/to/disposable.hdf" --filesystem ffs --offset-sectors 32 --name ks31-hd-proof.txt --expected "CopperHDF Kickstart 3.1 native FFS persistence`n"
python scripts/verify-lightweight-ofs-proof.py "path/to/disposable.hdf" --filesystem ffs --offset-sectors 32 --name ks31-hd-reopen.txt --expected "CopperHDF Kickstart 3.1 FFS reopen verified`n"
```

The reopen check belongs after the second run. Repeat from a pristine copy with
`--fast-ram-kib 0`. For captured scalar comparison use separate pristine copies,
`--boot-probe <directory> --boot-probe-interval 600 --boot-probe-extended`, and add
`--scalar-cpu` to one run. Missing native inputs are unavailable coverage;
different bytes fail the pinned identity checks.

## Limits

This is bounded PAL OCS A500 / 68000 boot and file-persistence evidence with
512 KiB Chip plus 512 KiB slow RAM. It does not establish a six-disk Workbench
installation, broad native application compatibility, interactive desktop
controls, arbitrary FFS files/extensions, additional handlers, accelerator/3.1
combinations, ECS/AGA or 68060 OS/FPU support. The proof reader intentionally
accepts only single-block root FFS files. The runner's throughput observations
are diagnostic; historical performance protocols and acceptance remain separate.
