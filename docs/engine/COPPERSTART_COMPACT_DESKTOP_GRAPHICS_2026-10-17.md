# CopperStart compact desktop graphics — 2026-10-17

Producer implementation: compact native text, rectangles, bitmap clears and one
PAL display owner. The target remains a 512 KiB ROM and an OCS 68000 A500 with
512 KiB Chip + 512 KiB slow RAM, one DF0 and no Fast RAM or host OS services.
The CopperScreen app pair is not changed by this producer work.

## Current implementation and fit

The selected graphics set grows from 41 to 53 vectors. New entries are Text,
RectFill, SetRast, MakeVPort, MrgCop, LoadView, GetColorMap, FreeColorMap,
LoadRGB4, SetRGB4, FreeVPortCopLists and FreeCprList. Small loops replace the
historical expanded raster cases. The ROM's original font includes an explicit
default glyph; its bootstrap and emitted font layout change together.

The graphics block is **29,418 bytes** at `$FF8000` (ROM offset `$78000`). It
fits without moving the resident and leaves **3,330 bytes** before the existing
20-byte footer. The preceding compiled-root gap is a separate range; it is not
all contiguous free space after graphics. No ROM or media bytes enter Git.

The drawing scope is unlayered planar memory. Proportional monochrome text with
kerning/spacing is admitted in JAM1; the other draw modes use contiguous fixed
cells. Rectangles support all draw-mode combinations and plane masks with
masked byte loops. Patterns, algorithmic styles, color fonts, reverse-path fonts
and Layer-aware rendering remain unselected.

The display scope is one 320×256 or 640×256 noninterlaced PAL ViewPort at the
InitView origin, depth 1..4, one RasInfo and zero offsets. MakeVPort builds public
intermediate instructions; MrgCop creates a separate Chip RAM Copper stream;
LoadView selects the stream using the real custom registers and beam wait.
Palette changes patch intermediate/raw lists and active hardware. A display
must be unloaded before freeing/rebuilding it. LoadView(NULL) leaves no active
View; restoring the separate boot-console display still needs an owner.

## Verification

The CopperStart Release solution build passes. **41 emitted-MC68000 instruction
checks pass**, covering expected bitmap pixels, draw modes/masks, unaligned
edges, public WORD text counts, proportional/default glyphs, Copper construction,
palette patches, allocation failures, repeated resource frees and callee registers.
These use synthetic memory/Exec and are separate from native ROM/media evidence.

The exact combined-ROM/native replay passed **110/110** selected cases, with no
skips: **53 native ROM/media**, **41 emitted-instruction**, **15 filesystem
semantic**, and **one composition** check. The native display cases exercised
both resolutions at **depth two**, compared bitmap bits and all visible display
pixels, changed the active palette and repeated three complete display
lifetimes each. Their frame fingerprints stayed identical and Chip/total free
memory recovered exactly after each cleanup.

| Result | Value |
| --- | --- |
| ROM bytes | 524,288 |
| Compiled C# code bytes | 463,658 |
| Root/prefix-to-graphics gap | 27,854 bytes |
| ROM SHA-256 | `F0AB800A3890BD4E53FB9606EF34F6839AEE8104856816C252B024686E2B6216` |
| 640-wide cleanup: Chip / total free | 418,472 / 760,640 bytes, all three cycles |
| 320-wide cleanup: Chip / total free | 438,952 / 781,120 bytes, all three cycles |

The memory checkpoints still hold the command, its stack and its bitmap
workspace; they are not idle-desktop or full GUI budgets. The same ROM passed
the physical FFS write/rename/read and fresh protected reboot proof, with real
graphics Open/Close pins balanced. V40 utility, Layers and Intuition opens
remain unavailable in this boot path. This does not qualify an installed
desktop, Intuition window, other display depths, shipping OS pair or throughput.

Producer artifacts are under CopperStart's ignored
`artifacts/compact-desktop-20261017`: `native` retains the original fixed-point
root compile and its three passing native controls; `relink-v2` contains the
fresh final graphics composition/manifest; `qualified-v2` contains the passing
protocol receipt, frozen inputs, TRX and six full PNG captures. Initial attempts
and preliminary instruction results remain separate. The paired machine-readable
[record](COPPERSTART_COMPACT_DESKTOP_GRAPHICS_2026-10-17.json) pins these receipts.

The native protocol is CopperStart `build/Test-CompactDesktopGraphics.ps1`, with
details in `Docs/CompactDesktopGraphics.md`. It requires the exact input hashes,
both native display resolutions, three display lifetimes each, complete
908×313 captures, pixel comparisons, recovery of Chip/total free memory and the
same-ROM writable DOS/fresh protected reboot. Skips cannot pass it.
`build/Relink-GraphicsBootRom.ps1` can create a fresh graphics composition over
an exact frozen C# root, retaining the original receipt; every other input and
the resident address must match. Font/bootstrap contract changes need a new
root compile. Negative controls reject changed root/replay inputs.

## Concrete next window prerequisites

Native `LayersNativeGraphicsVectors` still calls five unselected vectors:

| Required vector | LVO | Next boundary |
| --- | ---: | --- |
| AllocBitMap | -918 | Real bitmap/plane allocation and failure ownership. |
| FreeBitMap | -924 | Matching resource lifetime. |
| GetBitMapAttr | -960 | Real public bitmap attributes. |
| BltBitMapRastPort | -606 | RastPort destination and actual Layer clipping. |
| EraseRect | -810 | Background erase and actual Layer clipping. |

Its InitRastPort and WaitBlit calls are selected already. Layers also calls
Utility CallHookPkt and currently requests graphics and utility V40. The separate
native Shell Utility owner identifies as V37 and is absent from the disk ROM
startup. Qualify the required hooks/version and install real providers, then
admit Layers and the chosen public Intuition screen/window path. The Intuition
ROM InvokeCapability fingerprint remains insufficient.

After those foundations, qualify pointer/sprites, native keyboard/mouse to GUI
events, reply ownership, refresh, close/reopen and restoration after a foreground
child exits. The first native-window probe must demonstrate actual pixels,
physical click/key delivery and stable resource lifetimes on this same budget.
The app ROM/OS pair and affected native media need their own admission afterward.
