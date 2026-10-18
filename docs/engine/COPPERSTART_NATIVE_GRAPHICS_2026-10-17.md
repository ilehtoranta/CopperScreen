# CopperStart standalone native graphics — 2026-10-16–17

CopperStart already owned the native graphics bodies and image builders. The
remaining shared graphics image layout has moved out of CopperMod, whose
retained firmware imports the owned source. An independent CopperStart build
tool now composes the native resident into the standalone disk ROM. Guest Exec
installs it through AUTOINIT; all graphics execution is ordinary guest 68000
code. Lightweight, the pinned CPU package and chipset ordering are unchanged.

The historical full emitter produces 1,553,212 code bytes. The bounded A500 ROM
instead selects 41 vectors in a compact native block, including constructors,
pens, fonts, raster allocation, timing, blitter ownership and BltBitMap.
Unsupported vectors fault at distinct mapped addresses. This is partial
graphics.library coverage, with an explicit inventory in the ROM manifest.

North & South exposed allocation pressure, missing interrupts and two native ABI assumptions:

- A `$177D8`-byte Chip/Clear allocation returns zero at field 10685, caller
  `$3B052`. The later I/O into `$1FC` and image plane at `$3A` are offsets from
  that null result. Bootstrap now prefers fitted A501 RAM for its private arena
  and releases the unused low-Chip arena reservation. Its live supervisor stack
  and actual private allocations remain protected from double allocation.
  A subsequent `$18945`-byte allocation still failed with the 128 KiB supervisor
  reservation. Reducing that reservation to 32 KiB lets both allocation gates
  succeed. The traced live stack span was 638 bytes; this is bounded startup
  evidence rather than a complete stack-depth proof.
- The title then registered VERTB and EXTER servers, but the CPU autovectors and
  INTENA remained zero. Native Exec now emits the seven autovectors, dispatches
  Paula handlers and priority server chains, preserves interrupted registers,
  and implements source/master enables. Handler D1 contains the full enabled
  pending mask; server continuation follows Z independently of D0. There is no
  arbitrary chain-length cutoff. Private bootstrap records follow the expanded
  runtime without overlap.
- The downstream corruption replaces address four with picture data. Native graphics now retains
  the AUTOINIT Exec pointer at GfxBase's documented `$1A4` field. Font, raster
  allocation, timing and blitter calls use that saved context.
- The corruption also clears vector `$80`. Exec used TRAP #0 merely to capture task-call SR, so
  OwnBlitter reached PC zero at field 12897. The MC68000 task-call path now reads
  SR directly before changing flags. Actual supervisor operations retain their
  exception path. The native graphics regression clears that vector around
  OwnBlitter/DisownBlitter while address four is also replaced; it fails on the
  preceding ROM.

The observed first BltBitMap uses a 288 by 107 rectangle, four planes, shifted
edges and stale BitMap flags. Its image beginning at `$3A` exposed the earlier
allocation failure; it is not evidence of a valid allocated source. The compact
implementation handles all 16 binary minterms, masks and ordinary overlap;
suitable Chip rectangles use the real OCS blitter, with a CPU path for other
shapes. It uses the documented argument widths without clipping, allocator
ownership requirements or arbitrary work limits.
[BltBitMap autodoc](https://developer.amigaos3.net/autodocs/graphics.library/BltBitMap.html).

After GO, a `$1106D`-byte Chip/Clear HUNK also fails. Releasing the lower
24 KiB of the initial 32 KiB supervisor reservation leaves 91 KiB free, but the
largest chunk is still only about 50 KiB. The native Kickstart control exposed
another default: ANY/PUBLIC allocations prefer fitted A501 RAM. CopperStart's
bootstrap banks had equal priority and ordinary allocations consumed Chip
first. The standalone native finalizer now raises CPU-only banks to priority
one and re-enqueues them, preserving explicit nonzero priorities and caller
CHIP requirements. The native probe returns `$C07488`/`$C074A8` for ANY/PUBLIC
and `$A078` for CHIP; the Kickstart control returns `$C04AD0`/`$C04AF0` and
`$59E8`. This follows the
[AllocMem default preference](https://developer.amigaos3.net/autodocs/exec.library/AllocMem.html).
The finalizer publishes the actual 8 KiB supervisor bounds and renews Exec's
private checksum. The initial 32 KiB remain available during bootstrap.

Arte exposed a separate A501 reservation problem: its private loader pool
needs a contiguous 470,000-byte block after its Chip request. The initial
128 KiB private arena left only about 370 KiB in that pool. Packing the native
records and initial task stack into 28 KiB, without the unused 4 KiB gap,
restores the needed space. The Slow MemHeader begins at `$C07000`.

Standalone WaitTOF polls the real beam wrap without a timeout. The retained
full-library graphics VBlank task owner remains unqualified. An intermediate
attempt to enable that path caused native boot regressions and was rejected.
It is excluded from the final source.

Source changes are published on `codex/copperstart-native-graphics-20261017`:
[CopperStart 8b90a09](https://github.com/ilehtoranta/CopperStart/commit/8b90a09096506b262756c98fd10e19e19fefde52)
and [CopperMod 4d59ef7](https://github.com/ilehtoranta/CopperMod/commit/4d59ef7b12c399da825fd8948eae1b3586486c43).
The CopperStart topic branch keeps this qualified runtime separate from the
independently advanced remote A500 runtime.

## Qualification

The qualified ROM is 524,288 bytes, with 448,506 compiled C# bytes and a
26,252-byte native graphics/bootstrap block at `$FF0000`. SHA-256:
`AA7BB8AC694DE977429E4B3F4C436B2B17627E6A56AD2BA708A101256695AAB5`.

| Coverage | Passed, no skips |
| --- | ---: |
| Graphics pixel oracle, initializer and native ABI components | 178 |
| Focused Exec vectors, task frames, bootstrap and interrupts | 82 |
| Fresh standalone polled-reader ROM | 21 |
| Exact composed disk ROM, including real VBlank and Chip-pressure probes | 34 |
| DOS, startup scripts, console, disk and graphics regression | 53 |
| Retained CopperMod AHX layout consumer | 18 |

The nine bootstrap cases are a subset of the 82 Exec cases; the native ROM and
regression suites also overlap. These counts must not simply be added. The
pressure and interrupt probes fail on their preceding ROMs. Test RAM inspection
now covers A501 caller storage without advancing emulated time.

Both production Release solutions build successfully. The final CopperStart
build has no warnings or errors; an earlier build reported two existing
CopperSharp nullable warnings.

| Media | Bounded result |
| --- | --- |
| North & South, Captain Paranoia variant, 26,000 fields per CPU mode | Recorded English and GO clicks reach the opening message and animated strategy map. All 330 capture files and final CPU/hardware/output/cycle fingerprints match. Combat and a complete game are unverified. |
| Arte by Sanity, 40,000 fields per CPU mode | Effects, credits, later 3D scenes and the later intro sequence run. All 505 capture files and final CPU/hardware/output/cycle fingerprints match. |

The native Kickstart control responds to equivalent mouse actions after its
shorter startup. Attract-mode visuals alone are not counted as input response.
The first IRQ ROM and the later stack-only image still fail after GO; their
corrupted low-memory writes are downstream allocation failures. They are not
qualified game results.

[The evidence record](COPPERSTART_NATIVE_GRAPHICS_2026-10-17.json) records exact
ROM, producer, media, input, runner, TRX and capture-manifest hashes. The original
producer snapshot includes concurrent console/DOS edits excluded from the
graphics commit. A later source overlay and changed Exec producer identify the
final rebuild; other managed producer binaries remain frozen. This is recorded
working-tree evidence, not a claim that the commit alone reproduces those
concurrent inputs.

ROMs, licensed media, build products and captures stay outside Git. These PAL
OCS 68000, 512 KiB Chip plus 512 KiB A501 replays are bounded compatibility
checks. They establish neither full AmigaOS support nor throughput acceptance.
Audio fidelity and listening are unassessed.
