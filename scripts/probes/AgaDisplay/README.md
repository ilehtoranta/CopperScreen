# Native AGA display probe

`prepare_native.py` inserts an original 68000 Hunk executable into a new copy of
the supplied pristine FFS/RDB fixture. It never overwrites the source. No ROM,
OS, monitor-driver or NDK bytes are distributed.

Native Kickstart opens graphics/Intuition/DOS, activates the detected chipset
with the V39 `SetChipRev` bootblock API, opens PAL ModeID `$21000` at 320×256
and depth eight, sets 256 RGB24 colours and draws a 16×16 tile grid. It validates
the returned screen dimensions and depth, waits for blitting, and writes
`aga-screen-proof.txt`. The probe holds the screen for 6000 fields before closing.
Disk boot uses CopperHDF; this does not validate physical Gayle ATA emulation.

```powershell
python scripts/probes/AgaDisplay/prepare_native.py <pristine-rdb-ffs.hdf> <new-image.hdf> --ndk <supplied-NDK-FD-directory>
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom <A1200-KS3.0-39.106.rom-or-zip> --hdf <new-image.hdf> --cpu 68ec020 --agnus alice --denise lisa --chip-ram-kib 2048 --slow-ram-kib 0 --frames 3600 --boot-probe <new-capture-directory> --boot-probe-interval 300 --boot-probe-extended
python scripts/probes/AgaDisplay/verify_capture.py <new-capture-directory>/frame-003600.bmp
```

The independent bitmap reader checks 180,224 interior pixels across all 256
expected RGB24 colours. Independently read the native-written file with
`scripts/verify-lightweight-ofs-proof.py --filesystem ffs --offset-sectors 32
--name aga-screen-proof.txt --expected <the exact proof line including newline>`.
The proof is `Native AGA PAL 320x256 depth 8: all 256 RGB24 colours drawn\n`.
Use identical fresh images for normal and `--scalar-cpu` replays; compare all
captures and final disks. Keep the native evidence separate from throughput.

`aga-diagnostic.bin` records big-endian ModeNotAvailable (ULONG), screen pointer
(ULONG), returned width and height (UWORD each), bitmap depth (byte) and native
GfxBase ChipRevBits0 (byte). Zero dimensions/depth indicate an allocation
failure. Diagnostics cannot substitute for the successful proof and pixels.

For native desktop boot and cold-reopen coverage, supply `COPPERSCREEN_A1200_ROM`
and `COPPERSCREEN_AGA_PROBE_HDF` to `Kickstart30AgaTests`. The fixture and ROM
archive hashes are pinned in that test. Without both inputs the optional test
is unavailable coverage. See [the initial AGA record](../../../docs/engine/AGA_INITIAL_2026-10-01.md).
