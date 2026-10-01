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

## HAM and dual playfields

Both preparation and pixel verification accept `--mode ham6`, `--mode ham8`
or `--mode programmed-dual`. Omission selects the original RGB24 probe; its
Hunk and pristine image hashes are unchanged.

HAM6/HAM8 open stock PAL HAM ModeID `$21800` with depth six/eight. They draw
256 encoded tiles, exercising direct selection and B/R/G modification. V39
rotates HAM8 bitmap pointers, so the probe translates raw Lisa codes to the
OS's logical pens before drawing. The independent checker verifies the raw
code semantics, including the two held low component bits in HAM8, at 180,224
interior pixels. Each tile begins at x=`516 + column*80`, y=`60 + row*15`.

`programmed-dual` opens and draws a stock depth-eight RGB24 screen, then writes
its proof before taking chipset ownership. Its original Copper list restarts
the native bitmap pointers every field, sets DPF, PF2OF=32 and palette XOR=128,
and switches PF2PRI at line 180. Two grids each exercise all 256 combinations
of four-plane PF1/PF2 values. The first begins at guest y=16, the second y=136;
tiles are 20 by 7 guest pixels. Verification checks 98,304 interior pixels and
raw-zero transparency before XOR. This is a native guest hardware probe,
not a stock graphics.library dual-playfield screen or third-party game replay.

Each proof line is `Native AGA <mode> PAL 320x256 depth <depth>: encoded pixels drawn\n`.
Use depth 6 for HAM6 and 8 otherwise. `aga-screen-proof.txt` and the diagnostic
file keep their original names. Validate native DOS I/O separately from pixels.
See [implementation and retained evidence](../../../docs/engine/AGA_HAM_DUAL_2026-10-01.md).

Synthetic pixel-checker controls run without supplied ROM or media:
`python -m unittest discover -s scripts/tests -p test_aga_probe.py -v`.

`aga-diagnostic.bin` records big-endian ModeNotAvailable (ULONG), screen pointer
(ULONG), returned width and height (UWORD each), bitmap depth (byte) and native
GfxBase ChipRevBits0 (byte). Zero dimensions/depth indicate an allocation
failure. Diagnostics cannot substitute for the successful proof and pixels.

For native desktop boot and cold-reopen coverage, supply `COPPERSCREEN_A1200_ROM`
and `COPPERSCREEN_AGA_PROBE_HDF` to `Kickstart30AgaTests`. The fixture and ROM
archive hashes are pinned in that test. Without both inputs the optional test
is unavailable coverage. See [the initial AGA record](../../../docs/engine/AGA_INITIAL_2026-10-01.md).
