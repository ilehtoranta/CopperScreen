# CopperStart installed-owner drawing and updates — 2026-10-18

Installed native Layers now draws and refreshes through its real CMLG owner
on a displayed **320/640 × 256, four-colour PAL screen**. This joins the earlier
[provider startup](COPPERSTART_PROVIDER_STARTUP_2026-10-18.md) and drawing paths
in one native machine. The ROM remains **512 KiB**, and every case uses **512 KiB
Chip + 512 KiB slow RAM, no Fast RAM**, MC68000 and one DF0.

The guest creates real LayerInfo and Simple/Smart Layers through public vectors.
All rendering, damage generation, allocation and update operations execute
native instructions. A guest observer counts calls while delegating to the real
CMLG lock bridge; the host only acknowledges capture handshakes and checks output.

The Simple cases cover/uncover a Layer to generate real damage, then prove:

- Oversized rectangles and edge-crossing text affect only the damage rectangle;
  the logical string cursor advances once for the complete string.
- EndUpdate(FALSE) restores the normal clip head and guard while preserving
  damage bytes. A later complete update clears owned damage and preserves the
  caller's Refresh flag. Empty updates leave pixels unchanged.
- Failure at scratch, ClipRect or descriptor allocation returns FALSE;
  EndUpdate(FALSE) restores clips/locks with unchanged damage/pixels and exact
  heap recovery. Eighteen such failures run across both widths and three cycles.

The Smart cases draw a rectangle and glyphs into concealed backing storage,
then delete the covering Layer. Native restoration reveals that drawing without
Simple redraw damage. Every mode runs three clean Layer lifetimes; final display
teardown restores its starting heap, and owner retirement balances provider pins.
The retained segment, permanent Utility service and probe stack stay until reset.

The combined v6 replay passes **344/344**, with no skips: 263 compiled instruction
cases, 64 native ROM/media cases, 15 filesystem semantics, one composition check
and one provider compilation. The separate refresh/raster/locking suite passes
**88/88**. Three stale semantic cases were replaced with valid two-pass update,
clipping and lock-lifetime sequences; core behavior is unchanged. The producer
Release solution build passes with zero warnings/errors.

There are **66 new full 908 × 313 captures** across twelve owned Layer lifetimes.
The preceding 24 graphics and two console captures remain controls. The ROM hash
is unchanged, as is the **223,116-byte provider HUNK** with 215,994 code/data bytes.
No ROM relayout was needed: 842 contiguous tail bytes and the separate 27,854-byte
earlier gap remain. This does not change CopperScreen's shipping firmware pin.

Minimum bytes free at live capture points:

| Mode | Chip free | Total free | Largest Chip | Largest slow |
| --- | ---: | ---: | ---: | ---: |
| Simple 320 | 222,672 | 520,624 | 189,904 | 262,000 |
| Simple 640 | 202,192 | 500,144 | 169,424 | 262,000 |
| Smart 320 | 222,496 | 521,608 | 189,728 | 281,984 |
| Smart 640 | 202,016 | 501,128 | 169,248 | 281,984 |

These observations include retained code, stack, display and current Layers;
they do not measure transient allocation peaks or include Intuition. Compare
lifetimes within a mode: Simple/Smart probe code sizes differ.

Exact inputs, per-stage hashes, failure rows and source/build provenance are in
the [machine-readable evidence](COPPERSTART_OWNED_LAYER_UPDATES_2026-10-18.json).
Producer artifacts are under `CopperStart/artifacts/owned-layer-update-20261018/qualified-v1`;
the producer workflow and API references are in `Docs/NativeOwnedLayerUpdates.md`.
Earlier attempts are retained. This is correctness evidence, not a throughput result.

The first native desktop window still needs:

1. Intuition startup, installed-owner query/custody, provider pins and module
   lifetime bound to this exact disk provider. Its current ROM capability entry
   is still a fingerprint path; prior private ROM fixtures do not admit it here.
2. Real screen/window creation and decoration at the 640 × 256 target, including
   allocation rollback, close/reopen and display restoration.
3. BeginRefresh/EndRefresh, Refresh-flag retirement, refresh/close event delivery
   and replies, then physical keyboard/mouse/pointer, focus and wait/wake ownership.
4. Native disjoint damage, foreign-clip two-pass updates, contended locks, staged
   backfill callbacks/stale continuations and pending-work teardown. Smart scroll,
   SuperBitMap and NeverBackfill remain separate qualifications.
5. The selected Intuition/GUI closure and full allocation-peak budget within the
   existing ROM and RAM limits. A complete native window has not yet been admitted.
