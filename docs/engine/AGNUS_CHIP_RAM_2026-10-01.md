# Explicit PAL Agnus and configurable Chip RAM

This slice adds selectable Agnus and Chip RAM to the engine, runner, Memory
settings and saved profiles. The default remains 8371 / 512 KiB Chip / 512 KiB
slow. Copper68k remains the public `1.5.0` package. The source baseline is
`eaf00f7aca0e971a12433c50140dab1fa8d37063`; source identities, pinned inputs and
results are in the [JSON record](AGNUS_CHIP_RAM_2026-10-01.json).

## Implemented layouts

| PAL Agnus | Engine enum / profile value | Chip KiB | Slow KiB |
| --- | --- | --- | --- |
| 8371 | `Mos8371` / `8371` | 512 | 0 or 512 |
| 8372A | `Mos8372A` / `8372a` | 512 | 0 or 512 |
| 8372A | `Mos8372A` / `8372a` | 1024 | 0 |
| 8375, Commodore part 318069-10 | `Mos8375Pal2M` / `8375-318069-10` | 1024 or 2048 | 0 |

These are declared A500 memory layouts with OCS Denise and fixed PAL timing,
not a complete A500+ or ECS display profile. The 8375 choice identifies a
specific part; RAMesses' hardware designer lists 318069-10 among supported PAL
2 MiB parts and warns that other 8375 variants have 1 MiB limits.
[RAMesses hardware compatibility](https://github.com/LinuxJedi/RAMesses).
The 8372A vendor identifies it as a replacement for 8370/8371 with a 1 MiB
limit. [Individual Computers](https://wiki.icomp.de/wiki/Agnus).

Larger Chip layouts replace the trapdoor slow bank. Separate optional Zorro II
Fast RAM remains CPU-only. Engine construction and host availability reject
unsupported bank/model combinations. These rules deliberately do not enable
every physical motherboard or third-party expansion layout.

## Addressing and execution

The default CPU mirrors and 19-bit DMA addressing retain their previous behavior.
The 1 MiB 8372A layout decodes physical A19 and aliases A20; 8375 decodes the
full 2 MiB window. Its partially fitted 1 MiB layout has an unfitted upper bank,
which reads `$FFFF` and ignores writes instead of aliasing the lower bank.
For 8372A with 512 KiB Chip plus 512 KiB slow, CPU JP2 mapping still selects the
slow bank at `$C00000`; chipset DMA A19 reaches that same bank at `$080000`.
The default JP2 basis is the [Commodore A500 schematic, sheet 2](https://amiga.net.au/files/Tech_Amiga/Commodore_Amiga_500_Technical_Manual.pdf),
also recorded in the [original mirror correction](SUPER_CARS_II_INVESTIGATION.md).

ECS pointer storage uses a 21-bit word address independently of physical bank
decoding. Copper, blitter, bitplanes, sprites, audio and disk all retain the
extra bits through increments and register writeback. Device masks are fixed
at construction; DMA bank accesses remain bounded word loads/stores at the
existing accepted output phase. Clock ordering and contention policy are unchanged.

PAL ECS identification is returned through VPOSR, including light-pen latches.
BLTCON0L updates the minterm; BLTSIZV/H provide extended dimensions, with H as
the start strobe and zero maximum encodings. Copper CDANG permits low register
writes on ECS Agnus. These expectations follow
[Commodore HRM appendix C](https://bastya.net/AmigaDevDocs/hard_c.html).
The old BLTSIZE path and OCS register restrictions are retained.

ECS display sequencing is pending. BEAMCON0 modes other than fixed PAL `$0020`,
DIWHIGH writes (including zero, which can change window interpretation) and
active blitter DOFF report unsupported. OCS Denise identification, shifters,
palette, raster dimensions and PAL clock geometry remain in use.

## Host and profile behavior

Settings > Memory selects Agnus and filters Chip RAM sizes to the selected
model. Larger Chip RAM automatically removes slow RAM. Model and memory changes
require restarting the machine. Saved profiles persist explicit part choices;
old `"Agnus": "ocs"` files still load as 8371. Generic `ecs` and AGA values
remain round-trippable but unavailable, because an unspecified revision does
not select a validated machine. Configuration/session/reset persistence is
tested; interactive Avalonia controls have not been manually exercised.

The runner accepts `--agnus 8371|8372a|8375-318069-10`,
`--chip-ram-kib 512|1024|2048` and `--slow-ram-kib 0|512`.
Omitted slow RAM defaults to 512 KiB at 512 KiB Chip and zero for larger Chip.
Omitted Agnus/Chip retain 8371/512. A nondefault memory configuration prints its
model and bank sizes; default diagnostic output is unchanged.

## Verification

- Production Release solution build: zero warnings/errors.
- Diagnostic engine suite: **835 passed, zero skipped**. New cases distinguish
  upper Chip banks, unfitted banks, slow-bank DMA wiring, PAL ID and reset,
  pointer width/wrap, Copper upper-memory fetch/CDANG, extended blit sizes and
  zero encodings, BLTCON0L, unsupported modes, and disk/audio/sprite/bitplane DMA.
  The disk check retains the established output phase.
- Default host suite: **134 passed, five optional native tests skipped**.
  Those skips are unavailable coverage, not replay passes.
- Supplied native KS 3.1 hard-disk host matrix: **seven passed, zero skipped**.
  Each performs two independent 2,400-field cold boots with DF0 empty, then
  disposes/reopens the persisted FFS/RDB hardfile.

| Agnus | Chip KiB | Slow KiB | Fast KiB | KS 3.1 boot/write/reopen |
| --- | --- | --- | --- | --- |
| 8371 | 512 | 512 | 0 | PASS |
| 8371 | 512 | 0 | 0 | PASS |
| 8371 | 512 | 512 | 2048 | PASS |
| 8372A | 512 | 512 | 0 | PASS |
| 8372A | 1024 | 0 | 0 | PASS |
| 8375 (318069-10) | 1024 | 0 | 0 | PASS |
| 8375 (318069-10) | 2048 | 0 | 0 | PASS |

The ROM's own memory probes set ExecBase.MaxLocMem and the MEMF_CHIP memory
header upper bound to exactly 1,048,576 or 2,097,152 bytes for the larger layouts,
on both cold boots. This is native memory discovery, not a guest patch. The
structure interpretation follows the original
[execbase.i](https://d0.se/include/exec/execbase.i) and
[memory.i](https://d0.se/include/exec/memory.i) definitions. The reviewed full-beam
Workbench desktop pixels retain SHA-256
`6d9d59261ac424a3461a96ef8e518128b5cfbbcea11e5cd69bbc61798f7a30d1`.
The guest writes the 47-byte FFS proof on the first boot and reads it before
writing the 44-byte reopen marker on the second. A separate bounded filesystem
reader verifies both after disposal.

The current default 8371 / 512 Chip / 512 slow / 2 MiB Fast run matches all
**35** captures from the prior source baseline at fields 1/600/1200/1800/2400,
including CPU/register JSON, pixels, PCM and all RAM. Its persisted hardfile
also matches SHA-256
`b507421f40568dcc7443287bc36abd27cee69072d48c93a1fc78ed730887432e`.
The 8375 / 2 MiB Chip / no slow or Fast run matches all **25** normal/scalar
captures and the entire persisted hardfile. Independent Python proof inspection
passes for both, without using CopperDisk's reader.

Native KS 1.3 OFS controls run 2,400 fields with 8372A / 1 MiB Chip and
8375 / 2 MiB Chip, each with no slow RAM and 2 MiB Fast. Both execute the
unmodified ROM, show the native Avail/proof completion output and persist the
39-byte proof. Independent OFS verification passes; both HDFs match the prior
control's `3770c46f...bd312828283dc374` identity. No floppy is mounted.

## Reproduction and limits

Prepare the pinned disposable fixture using the
[KS 3.1 hard-disk procedure](KICKSTART_31_HD_2026-09-30.md#reproduction).
With its unmodified ROM and media paths:

```powershell
$env:COPPERSCREEN_KICKSTART31_ROM = "path/to/kickstart-3.1-a500.rom"
$env:COPPERSCREEN_WORKBENCH31_HDF = "path/to/pristine-ks31.hdf"
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --filter FullyQualifiedName~SuppliedNativeWorkbench31HardDisk

dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom "path/to/kickstart-3.1-a500.rom" --hdf "path/to/disposable.hdf" --agnus 8375-318069-10 --chip-ram-kib 2048 --frames 2400 --boot-probe artifacts/agnus-2m --boot-probe-interval 600 --boot-probe-extended
```

The runner HDF must start from a pristine copy; add `--scalar-cpu` on a separate
copy for parity. All recorded native cases use 68000. Accelerator combinations,
full ECS Denise/display modes, programmable timing, DOFF and broad game
compatibility remain unverified. The 8372A internal A20 pointer rollover uses
the generic ECS register specification; revision-specific hardware behavior
has not been independently measured. `$FFFF` for unfitted banks is a bounded
digital convention, not measured electrical bus retention. Existing PAL
sequencers are retained; this is not exhaustive ECS Agnus timing conformance.

Local diagnostic captures remain ignored under
`artifacts/agnus-chip-ram-2026-09-30/`; no ROM/media or build artifacts are
committed. Runner throughput is diagnostic. Historical performance protocols,
golden fingerprints and prior records remain unchanged; this record makes no
new performance-acceptance claim.
