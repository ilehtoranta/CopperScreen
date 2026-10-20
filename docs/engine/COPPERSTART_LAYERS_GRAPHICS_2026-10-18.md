# CopperStart Layers graphics prerequisites — 2026-10-18

The producer now implements the five remaining graphics provider vectors used
by Layers: **BltBitMapRastPort (-606), EraseRect (-810), AllocBitMap (-918),
FreeBitMap (-924) and GetBitMapAttr (-960)**. The selection grows from 53 to
**58**. These are native backing-store operations; Layer-aware drawing and a
native Intuition window remain outstanding. The shipping app pair is unchanged.

## Fit and native result

The ROM remains **524,288 bytes**, with the same compiled C# root/font/bootstrap
and graphics resident at `$FF8000`. Graphics grows by **900 bytes** to **30,318**,
leaving **2,430 bytes** before the 20-byte footer. The separate pre-graphics gap
remains **27,854 bytes**. It is not contiguous tail capacity.

ROM SHA-256: `7D9D2780BCA664FE1974EE426A5680F1CBD28714472646982C64B57F0C83C2E5`.

**208/208 selected checks passed, with no skips:** **137 emitted-instruction**,
**55 native ROM/media**, **15 filesystem semantic**, and **one composition**
case. The new native probes run all five vectors at 320×256×2 and 640×256×2 on
the bounded **512 KiB Chip + 512 KiB slow A500**. They exercise allocation
exhaustion, CLEAR, public attributes, a MINPLANES friend, masked bitmap copies,
zero-pen erase and complete display/bitmap teardown. Every bitmap bit and
visible display pixel matches; both widths pass three lifetimes with identical
frame fingerprints. Full 908×313 captures are retained.

After each new backing-store lifetime, Chip free is **459,432** and total free
is **801,224** bytes for both widths. The command, stack and 512-byte workspace
remain allocated at that checkpoint; this is not peak usage or a complete GUI
memory budget. The same exact ROM passes the earlier text/rectangle displays
and native writable-DOS/protected-reboot persistence proof.

The final Release producer solution build passes with zero warnings/errors.
The initial isolated build
reports two existing nullable warnings in CopperSharp compiler
`M68kSpillAndCopy.cs:74`; there are no build errors. This is correctness evidence,
not throughput, an installed desktop or app-pair admission.

The [machine-readable record](COPPERSTART_LAYERS_GRAPHICS_2026-10-18.json) binds
the ROM, fresh manifest, original frozen root, managed inputs, TRX, receipts,
captures and source snapshots. Local producer artifacts are under ignored
`CopperStart/artifacts/layers-graphics-20261018`: `relink-v1` and `qualified-v1`.
Older [53-vector evidence](COPPERSTART_COMPACT_DESKTOP_GRAPHICS_2026-10-17.md)
remains unchanged. The v2 `build/Test-CompactDesktopGraphics.ps1` protocol
requires both original display paths and both five-API backing-store paths.

## What still blocks the window

1. **Layer raster interception and refresh.** Compact drawing still traps on
   RP.Layer; `CopperSharpNativeLayersPlatform.ExecuteLayeredRaster` returns
   false. Connect typed graphics calls to the published Layers partition,
   coordinate translation, visible/backing ClipRects, lock/continuation and
   damage/update ownership. The native Layers backfill provider can supply a
   layered RP to EraseRect, so -810 being present alone does not complete that
   path. Qualify clipped zero-fill/Hook handling, overlapping layers and uncover
   refresh without discarding topology or faking success.
2. **Real provider startup and placement.** The exact frozen ROM lacks Utility,
   Layers and Intuition. Working Shell Utility source now installs V40; this is
   separate from the qualified ROM. Layers still requires V40 graphics/utility
   pins and CallHookPkt. Integrate actual startup and callback/rollback tests.
   Measure reachable native GUI closures before deciding ROM versus disk;
   account for code/data, temporary bitmaps and loader/task stacks, including
   the separate Shell loader's 65,536-byte worker-stack request.
3. **Native Intuition screen/window admission.** Its ROM InvokeCapability still
   returns an XOR fingerprint. Wire the actual selected public open/draw/
   refresh/close paths with allocation rollback and balanced provider pins.
4. **Pointer, input and display ownership.** Deliver physical click/key events
   to GUI messages with wait/wake, replies, focus and close. Establish one
   sprite/pointer owner and restore the desktop/console after child exit;
   LoadView(NULL) currently restores neither console nor an Intuition screen.
5. **First-window qualification.** Open, draw across edges, obscure/uncover,
   click/type, close/reopen and exercise low memory. Record free and largest
   Chip/slow blocks at boot, provider startup, active window and cleanup, then
   run the desktop plan's repeated lifetime gate on this same 512 KiB/1 MiB
   profile. These graphics probes do not satisfy that gate.

The producer's `Docs/LayersGraphicsPrerequisites.md` records precise API bounds,
autodoc references, reproduction and the source audit. Bitmap allocation is
native OCS planar; INTERLEAVED is ignored on OCS, optional MINPLANES works, and
queries accept borrowed headers. Copies/erases currently require unlayered
rectangles that fit their bitmap. Erase clears to zero independently of the
caller's pens, mode and pattern. No host OS implementation or engine timing
change was added.
