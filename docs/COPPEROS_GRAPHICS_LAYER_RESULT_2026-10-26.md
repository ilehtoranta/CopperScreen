# Layer result transport — 2026-10-26

The graphics-layer-result.27 qualification follows
[graphics-exec-address.26](COPPEROS_GRAPHICS_EXEC_ADDRESS_2026-10-26.md). It keeps
the exact ROM and pairs it with the layer-result.7 HDF. The
[producer report](../../CopperStart/Docs/NativeGraphicsLayerResult20261026.md)
records source scope, original controls, profiles and unbundled experiments.

| Artifact | SHA-256 |
| --- | --- |
| Unchanged combined ROM | `9fbb569a09a91c310d1abb39f3735e11cd655374278dff965430abc11d27edd5` |
| DOS1 HDF | `e730a9f4db97d450b4288de20fb1d21658226530141293987b2a02c00e432ecc` |
| L:CopperGUI | `1fb6b5a30565567e388681e082dd65d8e48d45e3354b5755c52316eefd9636b7` |
| L:CopperHome | `2ef15dc76976973b37f7131d419d44961899c2ff3d5e4d2e469cafcd39ade88e` |
| C:Desktop | `b97ceb0d6b118330425c5a35ce827378c51b2a8bebcc6e8afe27615c72eb6d2c` |

Two GUI Layers bodies change: dispatch writes Declined at declining exits
instead of pre-filling a result replaced on success; the native bridge reads
its local scalar result fields directly. Every exit still publishes all result
fields. Root/descriptor admission, owner/nest, queue/grant and wait/resume checks
remain. All other frozen bodies and the original compiler remain unchanged.
GUI native size falls 219,732→219,428 bytes. The original three file lengths
stay fixed; Home's text entry and resident identity are updated together.

All 16 measured MC68000/020 Layer cases save **386–424 guest cycles**,
**0.74–2.11%**, matching pixels, guards and cursors across eight original
MC68000 controls. For example, layered MC68000 RectFill 16×8 falls
18,930→18,530 cycles, compared with Kickstart's 3,572. Large gaps remain active.
All 34 flat-call medians remain exact .26 and match original control results.

Desktop profile/control agree exactly at 701 fields / 99,613,503 cycles, with
unchanged framebuffer and PCM. Cold readiness is 13.8821 guest seconds;
Tab repaint remains approximately 160 ms / eight PAL fields. This change does
not produce a meaningful redraw timing gain. The unchanged pixel hash is
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

All 149 graphics tests, 12 focused source lock/continuation cases and 11 staged
native desktop/input/reset/graphics cases pass, zero skips. Host/migration
coverage has 80 passes, zero failures and three unavailable historical optional
replays; both .5/.6 upgrade cases execute. The broader source Layer suite retains
the same nine failures alongside 637 passes, and the producer solution retains
three existing Shell/DOS API errors. The consumer Release build is clean.

GUI and complete HDF reproduce independently. The existing atomic updater now
also recognizes layer-bridge.6 files, backs up the disk and retains unrelated
bytes; active disks defer and custom components remain intact. CPU/chipset code,
package pins and the reset bank are unchanged. Full evidence is recorded in
`../CopperStart/artifacts/graphics-layer-result/qualification.json`. No media
enters Git; guest-cycle diagnostics remain separate from host acceptance.
