# CopperStart native Layer rectangles — 2026-10-18

The compact producer graphics ROM now supports **clipped RectFill and EraseRect
on Classic Simple/Smart Layers**, including scroll translation, visible pixels,
concealed Smart backing pixels, zero/no-backfill and raw native backfill Hooks.
The production Layers provider now has typed rectangle/erase calls and uses a
temporary unlayered RP for already mapped partition backfill. Layered Text,
installed native Layers refresh and the first Intuition window remain open.

## Result and limits

The complete ROM remains **524,288 bytes** on the **512 KiB Chip + 512 KiB slow
A500**, with 58 selected graphics vectors. Graphics grows by **1,122 bytes** to
**31,440 bytes**, at the unchanged `$FF8000` resident address. **1,308 bytes**
remain before the footer; the **27,854-byte** pre-graphics gap is separate.

ROM SHA-256: `71DB88FBAA33E38F8C5AD25844E7231AEF2EF257BBE211A4708A73B029D152EA`.
The exact frozen C# disk root/font/bootstrap is reused. The separately changed
Layers provider assembly is not installed in that ROM; the fit does not include
an entire GUI closure. The shipping app pair is unchanged.

**262/262 checks pass, no skips:** 189 emitted/compiled instruction cases,
57 native ROM/media cases, 15 filesystem semantic cases and one composition
case. The 52 new instruction cases cover clipping, modes/masks, hidden storage,
lock/pin ownership, Hooks, unsupported cleanup and compiled typed provider calls
with synthetic Exec. The native 320×256×2 and 640×256×2 probes build Classic
Smart Layer/ClipRect records in guest RAM and initialize a real Exec semaphore.
They compare all display/backing bitmap pixels and visible display pixels,
execute three native raw Hook callbacks per lifetime, and pass three complete
lifetimes per width. Full 908×313 captures are retained.

Each Layer lifetime recovers Chip free **458,920** and total free **800,160**
bytes for both widths, with the command/stack/1,024-byte workspace still held.
These are cleanup checkpoints, not a full GUI or peak-memory budget. The same
ROM passes the earlier flat/backing-store paths and native writable DOS plus
protected reboot. The Release producer solution build passes with two existing
nullable warnings in CopperSharp compiler `M68kSpillAndCopy.cs:74`, no errors.

An installed Layers owner stays pinned through drawing and retains its CMLG
private lock bridge. The absent-owner path uses the public Exec semaphore of
the caller-built Layer; it creates no Layers service or successful provider pin.
Installed-owner locking currently has synthetic-owner instruction coverage,
not positive installed-Layers ROM coverage. Raw Hook RP bitmap changes are
restored. There is no engine timing change, host OS implementation, native
window, concurrency/refresh qualification, app admission or throughput result.

The [machine-readable record](COPPERSTART_LAYER_DRAWING_2026-10-18.json) binds
the frozen root, emitter, managed inputs, source snapshots, TRX, six native
display-profile receipts, compiled provider probe and eighteen full PNGs.
Artifacts stay under ignored `CopperStart/artifacts/layer-drawing-20261018`:
`relink-v2` and `qualified-v2`; earlier failed/preliminary attempts are retained.
The v3 `build/Test-CompactDesktopGraphics.ps1` requires all three display
profiles at both widths, three balanced lifetimes each, and same-ROM persistence.
The [five-API baseline](COPPERSTART_LAYERS_GRAPHICS_2026-10-18.md) remains separate.

## Concrete next work

1. **Layered Text:** add its typed A0/A1/D0-WORD entry, clip glyph/template ink
   through the visible/backing partition, and update the original cursor once
   per string, including fully hidden/empty output. Replaying the current whole
   Text routine per ClipRect would both overdraw and advance repeatedly.
   Preserve baseline, default glyph, kerning/spacing, plane masks and the current
   proportional-JAM1/fixed-cell drawing-mode limits; verify pixels and cursor
   independently across edges and scroll offsets.
2. **Native refresh:** start real Utility/Layers providers, then exercise
   CreateLayer, damage ownership, BeginUpdate/EndUpdate, restricted update clips,
   hide/reveal and Smart backing restore. The renderer consumes the current
   ClipRects; it does not produce refresh partitions or complete refresh events.
   Qualify native callbacks, contended lock wait/resume and failed-operation
   rollback. SuperBitMap and MorphOS NeverBackfill remain unsupported here.
3. **First window:** replace Intuition's fingerprint capability path with the
   admitted public screen/window owner, then qualify pointer/sprites, physical
   input, GUI event replies, focus/close and display/console restoration after
   child exit. Measure loaded GUI code, stacks, simultaneous bitmaps and largest
   Chip/slow blocks before placement; only 1,308 contiguous ROM tail bytes
   remain. Keep disk-loaded GUI services inside the same 1 MiB budget.

Producer `Docs/NativeLayerRectangles.md` contains the full API/locking contract,
primary autodoc references, reproduction and the Text/refresh audit. No package,
ROM pin or OS image was published by this producer-only result.
