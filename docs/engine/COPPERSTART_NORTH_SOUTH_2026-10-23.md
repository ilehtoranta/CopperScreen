# CopperStart North & South: inherited sprite priority — 2026-10-23

North & South's supplied CP release exposed an invisible mouse pointer under
CopperStart. The guest updated its mouse coordinates, sprite descriptor and
Copper sprite pointer, but CopperStart left BPLCON2 at zero. The native
graphics initializer now supplies `$0024`, matching the observed Kickstart 1.3
boot handoff. The rebuilt ROM renders the pointer in the language screen,
menu and strategic map, and recorded mouse input selects and moves an army.

## Correction and discrimination

This is a ROM initialization change in CopperStart commit
`0057fb9c38d82d38122ddf69a4aa960573beedd7`. It writes BPLCON2 once during native
graphics startup; subsequent guest writes remain authoritative. No emulator
hardware semantics, title-specific behavior, host OS shim or caller-data
validation changes are introduced.

The [Commodore hardware reference, System Control Hardware](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_1989.pdf)
describes the sprite/playfield priority fields: zero places an opaque
playfield ahead of the sprites, while priority four places it behind all four
sprite pairs. `$0024` sets both priority fields to four. The choice of that
startup value is supported separately by a native-1.3 boot probe, not inferred
from the register description alone.

An original guest boot block draws an opaque red plane and a white sprite,
without programming BPLCON2. Its corrected raster assertions pass with native
Kickstart 1.3, fail with the old CopperStart image (red where white is expected),
and pass with the new image. The same probe is committed as
`NativeBootSpritePriorityTests.BootCodeInheritsSpritePriorityAboveAnOpaquePlayfield`
in CopperStart. Eleven selected native graphics, Intuition and boot-memory
cases pass with the exact rebuilt image, without skips. The four reviewed
before/after game rasters at fields 19000, 23500, 27000 and 28000 differ only
inside the pointer's 22×16-pixel bounds: 194, 154, 194 and 182 pixels,
respectively. The rest of each raster is identical.

An initial oversized 175-count vertical mouse move wrapped the game's signed
8-bit mouse delta; smaller moves correct the input script. The input trace
and RAM inspection distinguish the game's direct hardware mouse scan from
Intuition's unrelated stationary public coordinates with no open screen.
Palette inspection finds the same 32 colors under both ROMs. Those hypotheses
do not justify changing Intuition mouse semantics or the palette. Earlier
diagnostic sprite assertions sampled the wrong horizontal raster position;
only the corrected native/before/after results establish the regression.

## ROM, runtime and media

The 524288-byte rebuilt ROM has SHA-256
`fefe13e86ad00e3609ad4e385b5e3752e2407e1b5a9b67affa904d2160d9e8e7`.
Its native graphics block grows from 28818 to 28826 bytes. The 476276-byte
compiled C# guest root and compiler inputs from producer
`cf7c9b54aefea723b018bd68e10e829e67684250` remain unchanged. The owning native
emitter is rebuilt in Release, composed with that retained compiled root, and
the ROM checksum renewed. Independent recomposition yields identical bytes.
The old linked ROM is retained locally with SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.

The engine and CopperDisk are freshly built from CopperScreen
`29bc8635fe33ae9e267846ade511e9b368f7158d`, including the preceding source merge.
The frozen functional harness is retained from the
[Nitro record](COPPERSTART_NITRO_2026-10-23.json). Copper68k remains the pinned
unpublished `1.5.5-nitro.1` package. Exact identities are in the
[machine-readable record](COPPERSTART_NORTH_SOUTH_2026-10-23.json).

The profile is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
The comparison uses native Kickstart 1.3 on the same engine and profile.
The archive is `North & South (1989)(Infogrames)(M5)[cr CP].zip`, containing one
901120-byte standard ADF, SHA-256
`f40ad6733f0b7103a69c765f0c9be5136d84053f07e48c05675e29bbc21095db`.
This is the supplied cracked release, not original-protection qualification.
No media conversion, added crack, trainer, saved state or guest-memory patch
is used.

## Gameplay and replay verification

| CopperStart field | Reviewed observation |
| ---: | --- |
| 19000 | Language screen; previously hidden white pointer is visible. |
| 23500 | English menu, human North/Captain Stark, computer South, 1861. |
| 27000–28000 | January 1861 strategic map; mouse selects Pennsylvania's army and displays its 6 infantry, 3 cavalry and 1 cannon. |
| 29000–31000 | Army moves west; ending the turn advances to February, with computer movement and supply animation. |
| 34000 | March 1861; a Southern army has approached the Northern position. |
| 36000 | Attack enters the river-and-bridge pitched battle with both armies, terrain and animated projectiles. |
| 36400–37000 | Right Shift changes the selected-unit icon from artillery to cavalry, then infantry. Space and cursor-key input exercise battle controls. |
| 39000 | Escape retreats to the March map; the attacking Northern army survives. |
| 40000 | Surviving army selected: 6 infantry, 2 cavalry, 0 cannon, compared with 6/3/1 before battle. |

Native Kickstart 1.3 separately reaches the same menu and January map, moves
the same army, advances through February/March and enters the same battle
landscape. Right Shift selects cavalry. Its battle ends in defeat by field
27000 and returns to the map; battle positions and outcomes are not asserted
equal between ROMs. Early joystick/Left Shift trials remain in both workloads
without being counted as successful player artillery or unit selection.

The [publisher's instruction guide](https://device.report/m/7d309c94f415a97b95d134eff4acd4894c226d542198b3f4f21a98fe3b1936aa)
documents the menu, adjacent-territory movement, end-turn date band and battle
controls. CopperStart's blank loading interval is much longer than native
1.3's: the ROM performs successive 512-byte reads with motor restarts and
re-homing. Progressing reads and eventual startup distinguish this from a hang.
The two ROMs use separately recorded boot/menu timing and physical input files.
Normal and scalar CopperStart runs use identical inputs.

All three independent final reset replays exit successfully at field 40000,
without a reported unsupported CPU/video operation. Each retains 2005 files
across 401 captured fields. The CopperStart normal/scalar capture manifests
are byte-identical. Every final filename, size and SHA-256 matches its
corresponding exploratory manifest, including the separately timed native-1.3
control. Nonzero PCM occurs in 115 captured CopperStart fields and 139 native
fields; this establishes sample presence, not listening or audio fidelity.

The complete [CopperStart workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/north-south-copperstart-1861-v1.json)
contains 71 events; the separately timed [native-1.3 workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/north-south-kickstart13-1861-v1.json)
contains 76. Final input and exit-control files are written before process
creation. Captures retain both RAM banks, full raster, state and field PCM at
field 1 and every 100 fields through 40000. Work began October 23; final reset
verification completed October 24.

The production Release solution builds with zero warnings/errors. The separate
engine suite passes 1258 tests; seven optional native cases are unavailable,
not successful replays. All 85 disk tests pass. Those suites qualify the newly
built runtime separately from the native ROM/media evidence. The native
graphics emitter builds cleanly; its 11 focused full-ROM cases are separate
from these host/engine suites.

This extends the [earlier Copper restart investigation](NORTH_SOUTH_INVESTIGATION.md),
whose menu/display and historical performance conclusions retain their
original scope. ROMs, media, captures and build outputs remain local.
Historical workloads, performance fingerprints and acceptance records are
unchanged. This is functional compatibility evidence, not a throughput
measurement or physical hardware timing certification.

Full campaign completion, battle victory, deliberate enemy destruction,
artillery aiming, fort/train sequences, other years, disasters, two-player
control, save/load and pristine protected media remain unqualified. A visible
projectile alone does not identify which side fired it. The native-1.3 control
runs on the same engine and is not a physical hardware oracle.
