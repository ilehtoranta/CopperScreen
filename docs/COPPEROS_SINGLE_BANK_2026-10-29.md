# CopperOS A1200 single-bank ROM — 2026-10-29

Qualification `diskboot-2026-10-29-a1200-desktop-single-bank.37` replaces the
previous combined 1 MiB ROM with one standard **524,288-byte** ROM, including
padding. Reset code and graphics share `$F80000–$FFFFFF`; no E0 bank or
companion ROM is loaded. The machine remains PAL Alice/Lisa, 68EC020 interpreter,
2 MiB Chip RAM, no slow/Fast RAM, and the existing native FFS HDF on unit 0.

| Artifact | SHA-256 |
| --- | --- |
| CopperStart A1200 ROM | `ADD5F92850CE3E32C5F8283734A319D445300FE11F0B15F111B19A7D5ECF3680` |
| Unchanged CopperOS HDF | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Used ROM code/data, reset vectors, native command bridge and footer total
520,058 bytes, leaving 4,230 padding bytes. The root is 494,518 bytes and the
graphics resident 25,460 bytes at `$FF9C00`. The checksum is `$FFFFFFFF`; the
native compatibility validator reports zero managed allocation sites. Exact
producer DLL/map/emitter hashes are retained with the local qualified image.

This development snapshot restores the public TextLength vector, accelerates
pen setters and adjacent-word rectangle updates, shares the console glyph
source, and includes the original-controlled Chip Copper/VBlank initializer
and viewport palette fixes. No emulator CPU/chipset code or package pin changes.
The existing HDF and `exec-preemption.10` app-file migration remain unchanged.

The installed public call matrix matches original Kickstart v40.63 state/pixels
in all 34 rows. SetAPen/SetBPen take 490 guest cycles versus the original's 556;
SetABPenDrMd takes 508 versus 608. Smaller rectangle updates improve but still
trail the original for several shapes. The final producer selection passes
182 tests and skips 8 unavailable optional cases. Four pre-existing extended
image hash failures reproduce with unmodified HEAD and are excluded explicitly.

The exact file boots the actual desktop and completes Tab selection. Profile
and uninstrumented control agree on CPU, memory, framebuffer, audio, cycles and
frames, with input aligned to field 1,000. The framebuffer matches `.36`:
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
The native task-ready condition alone accepted an earlier blank-screen trial;
the final result also checks rendered pixels and retains PNG captures.

The embedded bundle passes 10 native graphics cases plus the app's desktop,
editor, reset and cold-reopen replay. Launcher/migration tests pass 69 cases,
with 8 optional frozen-media cases unavailable. Both Release solutions build;
the producer retains 11 existing warnings. The app recognizes the ROM's size
before splitting historical combined banks, and loads no companion bank for
this A1200 image.

| Guest measurement | Previous `.36` | Single-bank `.37` |
| --- | ---: | ---: |
| Cold desktop ready | 12.159355436 s | 14.583213769 s |
| DrawChanges | 41.567625 ms | 41.633880 ms |
| Key-down to DrawChanges return | 85.659711 ms | 86.555706 ms |

The eight-field/160 ms input floor remains. These are guest measurements,
separate from host throughput. The cold-boot regression remains open; current
profiles identify increased allocator boundary/span work and filesystem
checksums among its costs. This checkpoint meets the ROM-size requirement and
improves specific calls; it does not close the broader performance goal.

Local producer evidence is in `CopperStart/artifacts/graphics-512-20261029`;
consumer captures are in `artifacts/graphics-512/qualified-control` and
`qualified-profile`. ROM/media and local artifacts are not committed or released.
