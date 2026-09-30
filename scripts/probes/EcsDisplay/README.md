# Native ECS display probe

`prepare_native.py` creates a new disposable FFS/RDB fixture from the supplied
Workbench 3.1 hardfile used by the native HD tests. It inserts an original 68000
Hunk executable and replaces that copy's startup script. Native Intuition opens
a screen, graphics.library draws four panels, and DOS writes a proof only after
the returned screen dimensions match. No licensed bytes are distributed.

```powershell
python scripts/probes/EcsDisplay/prepare_native.py <pristine-rdb-ffs.hdf> <new-image.hdf> --mode super --ndk <supplied-NDK-FD-directory>
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom <A500-KS3.1.rom> --hdf <new-image.hdf> --agnus 8375-318069-10 --chip-ram-kib 2048 --denise 8373 --frames 2400 --boot-probe <new-capture-directory> --boot-probe-interval 600 --boot-probe-extended
```

Modes `super` / `super-lace` request stock PAL progressive/interlaced SuperHires;
`ntsc-super` requests stock NTSC SuperHires through OpenScreenTagList.
`productivity` requests the stock VGAPRODUCT_KEY `$39024`. Non-default monitor
launchers must be installed in the supplied image. Driver absence is
unavailable native coverage, not a passing VGA replay.

`programmed-ntsc-super` and `programmed-productivity` are separate hardware probes: native OS services first
allocate/draw a PAL virtual screen and write its proof; the guest then takes
chipset ownership and supplies its own Copper list. The first programs NTSC
counters for 1280x200 output; the second programs a 114-CCK/525-line raster
for 640x480 output. They verify display hardware execution, not stock NTSC/VGA
monitor launchers.
Captured register metadata and pixels must be checked as well as the proof file.
The probe holds this mode; stop the disposable replay after the desired captures.

Run normal and `--scalar-cpu` copies from identical fresh inputs and compare all
captures and the final disk. Independently read the proof with
`scripts/verify-lightweight-ofs-proof.py --filesystem ffs --offset-sectors 32
--name ecs-screen-proof.txt --expected <mode-specific-line>`. Native captures,
hardfiles, ROMs and NDK inputs remain local artifacts. These correctness probes
are not throughput acceptance measurements.

`--diagnostics` also writes `ecs-mode-diagnostic.bin`: big-endian ModeNotAvailable
result (ULONG), OpenScreenTagList result (ULONG), and returned width/height
(UWORD each, zero when no screen opens). It never turns a failed screen request
into a passing proof.
