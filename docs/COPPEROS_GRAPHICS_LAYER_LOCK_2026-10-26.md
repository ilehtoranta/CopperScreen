# Native empty-queue Layer locks — 2026-10-26

The graphics-layer-lock.29 development qualification follows
[native Layer admission](COPPEROS_GRAPHICS_LAYER_ADMISSION_2026-10-26.md). It
retains the exact ROM and bundles the layer-lock.9 HDF. The
[producer report](../../CopperStart/Docs/NativeGraphicsLayerLock20261026.md)
records implementation, original controls and remaining gaps.

| Artifact | SHA-256 |
| --- | --- |
| Unchanged combined ROM | `9fbb569a09a91c310d1abb39f3735e11cd655374278dff965430abc11d27edd5` |
| DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |
| L:CopperGUI | `84fe4e97b5c6cdffa1867eee6849194f67fdc2b837871f9bf06216df660bb0d4` |
| L:CopperHome | `a8678989641df2ffea9a5cb10ab8ddceaa4dc23a3c034b73cdb27efbba27e157` |
| C:Desktop | `042a8ab4871452b6bbc3f10ed6a93f38ceb6a85faf33170c9388a369dc11e33e` |

After unchanged private root/descriptor admission, the native bridge handles
empty-queue lock/unlock/attempt transitions directly. It retains task, owner,
nesting, grant-token and public semaphore semantics. Queued/malformed states,
high-bit nesting counts and continuations retain the shared state machine.
Final release rechecks for a newly arrived waiter; public synchronization uses
the current wait count. The public semaphore envelope follows from its complete
containment in the already-admitted Layer. No memory-type query is added.
One frozen body changes and one helper is added; the other 1,347 bodies and the
original compiler remain unchanged.

All 16 measured layered cases save **2,624–3,376 guest cycles**, **5.73–20.03%**
against .28. Completed medians for MC68000 without an outer lock:

| Call | Kickstart 3.1 | .28 | .29 |
| --- | ---: | ---: | ---: |
| RectFill 16×8 | 3,572 | 15,582 | 12,520 |
| RectFill 184×32 | 7,278 | 18,752 | 15,698 |
| Text 8 | 10,136 | 23,112 | 20,048 |
| Text 64 | 32,258 | 46,254 | 43,186 |

Eight original MC68000 controls retain matching observed pixels, guards and
cursors; no original 020 timing comparison is claimed. An independent run
reproduces all 16 candidate rows. All 34 flat-call medians remain exact .28 and
retain their original result matches. Significant call-cost gaps remain active.

Desktop profile/control agree on 700 fields / 99,471,402 cycles, CPU/RAM state,
pixels and PCM. Cold readiness is **13.8621 guest seconds**; Tab redraw remains
**160 ms / eight PAL fields**, with no meaningful desktop timing gain. Pixels
retain `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

All 153 graphics tests and 11 bundled native desktop/input/reset/graphics tests
pass, zero skips. New coverage includes 210 differential lock transitions per
CPU and four software queue-arrival cases per CPU/implementation. Nineteen
focused source tests pass. The broader source suite retains nine known failures
alongside 638 passes; its 647 outcomes are unchanged. Consumer Release is clean;
the producer solution retains three unrelated Shell/DOS API errors.
Host/migration coverage has 82 passes and three unavailable historical optional
replays. The .5/.6/.7/.8 upgrade replays all execute.

GUI and full HDF reproduce independently byte for byte. The three HUNK file
lengths stay fixed, with Home's text entry/resident identity updated together.
The updater also recognizes .8 components and preserves backup, deferral and
customization behavior. CPU/chipset code, package pins and the reset bank are
unchanged. Full local evidence is in
`../CopperStart/artifacts/graphics-layer-lock/qualification.json`.
Media remains outside Git; these diagnostics are separate from host throughput
acceptance. Remaining original-controlled optimization work continues.
