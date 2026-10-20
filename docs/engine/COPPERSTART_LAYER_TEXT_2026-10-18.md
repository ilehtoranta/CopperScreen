# CopperStart native Layer text — 2026-10-18

The compact producer ROM now draws **Text through Classic Simple/Smart Layer
ClipRects**, clipping individual glyph pixels into the screen or concealed Smart
backing bitmap. It walks each string once and publishes the original cursor once,
including empty clip lists and fully hidden Simple output. The production Layers
provider has a typed A0/A1/D0-WORD Text call. Installed native Layers refresh and
the first Intuition window remain open.

## Result and scope

The complete ROM stays **524,288 bytes** on the **512 KiB Chip + 512 KiB slow
A500**, with 58 selected graphics vectors. Graphics grows by **466 bytes** to
**31,906 bytes** at unchanged `$FF8000`. **842 bytes** remain before the footer;
the **27,854-byte** pre-graphics gap is separate. ROM SHA-256:
`DE0C6C8BE7395788081EF26E9B50FCC01DB912FD2BFA8A7924A7230F4979133F`.

The exact frozen C# root/font/bootstrap and non-graphics composition inputs are
reused. The changed Layers provider assembly is compiled/tested separately and
is **not installed in this ROM**; the fit excludes its full GUI closure. The
shipping CopperScreen firmware pin is unchanged.

**328/328 checks pass, no skips:** 253 emitted/compiled instruction cases,
59 native ROM/media cases, 15 filesystem semantic cases and one composition
check. The new 61 text instruction cases cover modes/masks, proportional/default
glyphs, visible/hidden clipping, one cursor publication, WORD count/wrapping,
unsupported-font preflight and synthetic installed-owner pin/lock cleanup. Ten
compiled production-provider cases include visible, empty-partition and zero-count
Text calls. Its focused HUNK is 4,028 bytes with 2,308 code/data bytes and zero
managed allocation sites, runtime helpers or framework features.

Actual ROM probes build caller-owned Classic Smart Layer/ClipRects and a real
Exec semaphore. Fixed 8×8 JAM1 strings cross visible/backing boundaries, draw
fully concealed text and clip top/right/bottom edges; guest code checks cursor
positions and zero WORD count. Both 320×256×2 and 640×256×2 pass three complete
lifetimes, with every screen/backing bitmap bit and displayed pixel compared.
Four display profiles at both widths retain **24 full 908×313 captures**. The
same ROM passes flat drawing, backing APIs, rectangle/Hook paths and writable
DOS plus protected reboot.

Text-profile cleanup recovers Chip free **458,920** and total free **799,888**
bytes after every cycle at either width. The larger diagnostic command, stack
and 1,024-byte workspace are still held; these are cleanup checkpoints, not
peak/full-GUI RAM budgets. The Release producer solution build passes with two
existing nullable warnings at CopperSharp `M68kSpillAndCopy.cs:74`, no errors.

Proportional JAM1 and fixed-cell JAM2/complement/inverse remain supported with
default glyphs and plane masks. Algorithmic styles, color/reverse-path fonts
and proportional background composition remain outside the compact renderer.
Unsupported fonts are rejected before ownership/drawing; unsupported Super mode
unwinds ownership. The installed CMLG owner remains authoritative and pinned
through cursor publication. Its instruction fixture is synthetic; native ROM
coverage uses the caller-built Layer's public semaphore. No installed Layers
startup, refresh concurrency, native window, app admission or throughput result
is inferred from these probes.

The [machine-readable record](COPPERSTART_LAYER_TEXT_2026-10-18.json) binds the
frozen root, new emitter, complete managed input identities, source snapshots,
TRX, eight profile receipts, provider HUNK/map and 24 display captures. Ignored
producer evidence is under `CopperStart/artifacts/layer-text-20261018`:
`relink-v1`, `qualified-v1`, `source-inputs-final` and `solution-build.log`.
The v4 compact protocol requires every case and all four profiles at both widths.
The [rectangle baseline](COPPERSTART_LAYER_DRAWING_2026-10-18.md) is retained.

## Refresh handoff

1. **Install Utility V40 and Layers on the combined boot path.** Qualify real
   initialization, compatible provider pins, missing/incompatible/allocation
   rollback, CallHookPkt and contended lock wait/resume. Neither owner is
   installed in the frozen ROM. Measure the loaded closure before placement.
2. **Execute actual damage/update transactions.** `LayersRefreshCore` already
   publishes a damage-restricted head in BeginUpdate, restores normal ClipRects
   in EndUpdate and retains incomplete damage. Text now consumes the current
   head; it does not produce damage or complete refresh. Qualify restricted
   text/rectangle drawing, foreign clip regions, repeated BeginUpdate,
   complete/incomplete EndUpdate, abort and allocation rollback on native media.
3. **Qualify hide/reveal and callback ownership.** Cover Simple redraw damage,
   Smart backing restore, scroll, callback reentry/stale continuation rejection
   and teardown with pending work. Keep staged Layers refresh callbacks distinct
   from synchronous raw graphics erase Hooks. SuperBitMap and NeverBackfill
   remain separate implementation/admission work.
4. **Wire Intuition's refresh boundary.** Its request core retains the public
   Window/BOOL, but ROM InvokeCapability remains a fingerprint implementation.
   Real screen/window ownership must connect BeginRefresh/EndRefresh, refresh
   events/replies and the caller-owned Refresh flag after successful EndUpdate.
   Follow with pointer/input, close/reopen and display restoration.
5. **Keep the bounded target.** Measure loaded GUI code/data, loader/task stacks,
   bitmap peaks and largest Chip/slow blocks. With 842 contiguous tail bytes,
   evaluate qualified disk-loaded GUI components. The first real native window
   still needs its full resource/input/refresh lifetime journey on the 1 MiB A500.
