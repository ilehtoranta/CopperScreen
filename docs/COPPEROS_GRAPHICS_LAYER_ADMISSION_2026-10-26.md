# Native Layer admission — 2026-10-26

The graphics-layer-admission.28 development qualification follows
[Layer result transport](COPPEROS_GRAPHICS_LAYER_RESULT_2026-10-26.md). It keeps
the exact .26/.27 ROM and bundles the layer-admission.8 desktop HDF.
The [producer report](../../CopperStart/Docs/NativeGraphicsLayerAdmission20261026.md)
records the source changes, original-ROM controls and local evidence.

| Artifact | SHA-256 |
| --- | --- |
| Unchanged combined ROM | `9fbb569a09a91c310d1abb39f3735e11cd655374278dff965430abc11d27edd5` |
| DOS1 HDF | `99bd9c60c27b474adb42a3a1bdc786fd88b00e74d73cf8d96257058d05f8772d` |
| L:CopperGUI | `d814cd8669b17aa4a9f43179bffccc80c7c9f61a5a3c26b0c6fb7407a010e0a5` |
| L:CopperHome | `feaf6a59b184bed6e8e15e047cff784f8a13b84292dac94ecc30d5e3b116aefe` |
| C:Desktop | `76fa20db1d23b0e70f193fd79a19a0fb357c890c7e8c242ce3e9a24e810b5c66` |

The native Layers bridge now evaluates the same initial root, binding, public
Layer and private descriptor predicates in one admission routine. Fixed header
address envelopes branch directly; the dynamic descriptor envelope uses scalar
unsigned arithmetic. This removes generic validation call frames while retaining
the checks on CopperStart-owned state. After a wait, the existing continuation
path still re-admits. Queue/grant, nesting, owner and teardown protocols remain.
No memory-type query is introduced. Two frozen method bodies change and three
helpers are added; all other 1,343 bodies and the original compiler are unchanged.

All 16 measured MC68000/020 layered drawing cases save **2,926–3,306 guest
cycles**, **5.98–16.68%** against .27. Completion costs below include final DMA
completion. Original controls are MC68000; no original 020 comparison is claimed.

| MC68000 call, no outer lock | Kickstart 3.1 | .27 | .28 |
| --- | ---: | ---: | ---: |
| RectFill 16×8 | 3,572 | 18,530 | 15,582 |
| RectFill 184×32 | 7,278 | 21,680 | 18,752 |
| Text 8 | 10,136 | 26,058 | 23,112 |
| Text 64 | 32,258 | 49,196 | 46,254 |

All eight original layered controls match the observed pixels, guards and
cursors. All 34 flat-call medians remain exact .27, with matching original
control results. Several small calls already meet or beat Kickstart; ownership,
bitmap-copy setup, text and layered drawing retain performance gaps.

Desktop profile/control agree on 700 fields / 99,471,422 cycles, register/RAM
state, framebuffer and PCM. Cold readiness is **13.8621 guest seconds**. Tab
redraw remains **160 ms / eight PAL fields**, with no meaningful redraw gain.
The pixel hash remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

All 151 graphics tests and 11 bundled native desktop/input/reset/graphics tests
pass, zero skips. Range coverage includes 100,400 host predicates and 1,424
compiled cases on each CPU. Native private-state tests exercise 18 corruptions
and three accepted descriptor sizes on each CPU. Host/migration coverage has
81 passes and three unavailable historical optional replays. The .5, .6 and .7
upgrade replays execute, including exact image output, backup, idempotence,
preservation of unrelated bytes, active-disk deferral and custom-file refusal.
The broader source suite retains nine known failures; the producer solution
retains three unrelated Shell/DOS errors. The consumer Release build is clean.

GUI and complete HDF reproduce independently byte for byte. The three HUNK file
lengths stay fixed; Home's text entry and resident identity are updated together.
The updater additionally recognizes the previous .7 components. CPU/chipset
code, package pins and the reset bank are unchanged. Full local evidence is in
`../CopperStart/artifacts/graphics-layer-admission/qualification.json`.
Media stays outside Git. Guest-cycle diagnostics are separate from host
throughput acceptance; remaining optimization work stays active.
