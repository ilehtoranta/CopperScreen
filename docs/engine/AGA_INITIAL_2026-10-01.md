# Initial PAL A1200 / AGA support

Implemented on top of CopperScreen `70936d4` (ECS-required media replay).
This is the first AGA milestone: an explicit PAL A1200 with Alice/Lisa,
68EC020, 2 MiB Chip RAM, native Kickstart 3.0 (39.106) and eight-plane RGB24
output. It does not establish full AGA game/demo compatibility. Input identities,
capture hashes and verification results are retained in
[the machine-readable record](AGA_INITIAL_2026-10-01.json).

## Implementation and source audit

- `Mos8374Alice` and `Lisa4203` are paired explicitly. The initial layout requires
  2 MiB Chip RAM, no slow/Fast RAM, and 68EC020. Existing OCS/ECS choices retain
  their CPU and memory maps. The desktop validates the A1200 ROM separately from
  A500 1.3/3.1; the profile is `a1200-aga-pal`.
- Alice DMA adds all eight pointers, BPU3 and FMODE 16/32/64-bit payloads.
  Accepted transfers retain their address and fetch mode until the next CCK
  samples RAM. Each terminal fetch applies its parity modulo once. FMODE and
  BPLCON0 have separate pending deadlines; a mode write cannot discard a future
  plane-count change.
- Lisa adds eight shifters and a 256-entry RGB24 palette, banked high/low-nibble
  writes, eight-plane single-playfield output, EHB and BPLCON4 palette XOR.
  High-nibble writes initialize both halves for RGB4 compatibility. Low-resolution
  sprites retain their existing path and common palette bank. Higher sprite
  resolutions, unequal sprite banks and wide sprites are explicitly unavailable.
- An aligned CPU Chip/ROM longword uses one accepted motherboard bus transfer.
  Custom/CIA/Gayle retain narrower accesses, and unaligned operands split through
  the existing bridge. One machine still owns the canonical clock and arbitration.
  This is the bounded `a1200-initial-v1` bridge, not certified physical A1200 timing.
- Empty Gayle IDE/PCMCIA decode and D1 identification allow native ROM startup.
  Reads advance the ID shift only on a real strobe; inspection does not. No ATA
  drive is emulated. Native HD boot and persistence use the existing CopperHDF
  guest device and gateway.

The primary register source is Commodore's
[Lisa specification](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Aaa_lisa_specification.pdf),
sections 1.1 and 2.1: eight planes, BPU3, palette BANK/LOCT, FMODE, reset values,
sprite resolution and chip identification. The LISAID high byte contains board
configuration, rather than floating data; this A1200 returns `$00F8`. PAL Alice
returns `$2300` in VPOSR identification bits. The A1200 board-ID value, widened
DMA alignment/replication and fetch ordering were cross-checked against
[WinUAE custom-chip source](https://github.com/tonioni/WinUAE/blob/master/custom.cpp).
That is supporting emulator research, not an independent hardware trace.

MIT Legacy `Bus/A1200PlatformIo.cs` was adapted from CopperMod commit
`8fb826eacf8e9dc32948b482c878079448bd6e61`. Its register/palette/bitplane paths
were inspected alongside the hardware specification. Lightweight retains its own
device calendar, accepted-transfer pipeline and renderer; no Legacy execution
fallback or sibling project reference is introduced.

## Shared CPU release

Copper68k **1.5.1** is a public mainline package from CopperMod commit
[`43a3a84`](https://github.com/ilehtoranta/CopperMod/commit/43a3a84f79657cb4c2b32bb58d8e510e3aec9c15).
Publication was explicitly authorized on 2026-10-01 and completed through the
[release workflow](https://github.com/ilehtoranta/CopperMod/actions/runs/36905930892).
All three direct consumers and the retained diagnostic probe locks select the
immutable public package. A fresh package directory restores the production
solution in locked mode from NuGet.org, without a local feed.

`CreateA1200Ec020(bus)` selects the existing A1200 timing profile through a new
public factory method. Default `Create(M68EC020, bus)` preserves the OCS profile.
Sixteen missing integer operand families reached during native ROM/DOS/graphics
startup were implemented in the shared executor. Forty-seven focused tests
discriminate widths, flags, signed PC/index addressing, bit selection and stack
aliasing. The full CPU suite passes 3,522 tests; six optional external corpus
cases are unavailable. The retained AHX consumer passes 18 tests. CPU semantics
use [Motorola M68000PRM](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf);
costs remain the existing approximate operand-shape policy.

## Native validation

The supplied 512 KiB A1200 Kickstart 3.0 (39.106) reaches its native insert-disk
screen. An original Hunk probe, installed into a disposable copy of the supplied
FFS/RDB image, then boots DOS, opens native graphics/Intuition libraries and calls
the V39 `SetChipRev` API. The minimal test startup does not run SetPatch; this
documented bootblock API lets the ROM detect and activate the fitted chipset.
Its absence initially left ChipRevBits0 at `$13` and correctly rejected depth eight.
The successful probe reports `$1F`, opens PAL `$21000` at 320×256×8, draws all
256 RGB24 colours, waits for blitting and writes its proof file through native DOS.
The original probe and independent bitmap checker are in
[scripts/probes/AgaDisplay](../../scripts/probes/AgaDisplay/README.md).

The completed raster is 1816×313. Final BPLCON0 is `$0210`, FMODE `$0003` and
BPLCON3 `$0C40`; native graphics.library therefore exercises all eight planes,
64-bit fetches, 24-bit palette writes and explicitly selected 140 ns sprites.
The bitmap checker verifies 180,224 interior pixels against the 256 expected
RGB values. The independent bounded FFS reader verifies the proof separately.
Proof-file success alone cannot substitute for pixel or register checks.

| Check | Result |
| --- | --- |
| Production Release build using public Copper68k 1.5.1 | Passed, zero warnings/errors |
| Fresh-cache, locked public restore | Passed |
| Engine diagnostics in separate outputs | 912 passed, zero skipped |
| Ordinary host suite | 149 passed; 6 optional native cases unavailable |
| Supplied native A1200 desktop boot/persistence/cold reopen | 1 test passed, two boots, zero skipped |
| CopperDisk suite | 74 passed, zero skipped |
| Native regression tooling negative controls | 21 passed; no ROM/media replay |
| AGA native normal/scalar replay | 3600 fields each; every captured file and final HDF identical |
| Independent RGB24 and FFS proof verification | Passed for both AGA replays |
| Existing native OCS and ECS HD controls | 2400 fields each; every capture and final HDF identical to the preceding ECS record |
| Stable eight-plane rendering allocations | Zero bytes across four warmed fields |

The 28 AGA diagnostic cases cover profile pairing, board/chip identity, reset
state, four fetch widths, eight-plane colour selection, palette banking, wide
alignment/replication, accepted-transfer retention, the aligned CPU long bus,
Gayle read strobes, unsupported modes and steady rendering allocation. Twelve
host cases cover profile save/load, presentation geometry, ROM identity and
rejection of mixed hardware/memory/CPU layouts. These synthetic tests are
separate from the native replay and desktop cold-boot test.

No formal host-throughput acceptance measurement was run. Short runner rates
remain diagnostics; historical placement/load protocols and evidence are unchanged.

## Remaining work

AGA HAM6/HAM8, dual playfields, enhanced sprites and extended collisions,
palette readback and scan doubling remain unavailable. Fine 35 ns positioning
and scrolling, manual wide BPLDAT writes, genlock keys and physical bus phases
need focused follow-up. The native probe covers stock PAL lores; native hires,
SuperHires and third-party AGA game/demo coverage remain open. The engine keeps
the PAL oscillator; no NTSC A1200 or A4000 board profile is validated. A1200
Kickstart 3.1, expansion Fast RAM and physical ATA/PCMCIA devices are not part
of this initial profile. Interactive Settings/desktop presentation was not tested;
automated desktop sessions, profile persistence and frame geometry were tested.

Next milestones should implement HAM8 and dual playfields, then widened sprites,
with discriminating guest probes before native AGA game/demo replays. Previously
recorded Alien Breed II AGA failures remain failure evidence until a new replay
actually verifies its loader and gameplay.
