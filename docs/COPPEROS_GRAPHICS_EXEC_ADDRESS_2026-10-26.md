# Graphics Exec/address setup — 2026-10-26

The graphics-exec-address.26 ROM follows
[graphics-layer-bridge.25](COPPEROS_GRAPHICS_LAYER_BRIDGE_2026-10-26.md), retaining
the layer-bridge.6 HDF. The [producer report](../../CopperStart/Docs/NativeGraphicsExecAddress20261026.md)
contains the original traces, implementation, exact inputs and measurements.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `9fbb569a09a91c310d1abb39f3735e11cd655374278dff965430abc11d27edd5` |
| Unchanged DOS1 HDF | `de07a5d313e42c90013ed230c45cd907179e0598819ef900e86e5be564ca33b6` |

Exec calls and reads use the Amiga global ExecBase at address 4 directly,
removing repeated public Library size/cache branches. Nearby code addresses
use one PC-relative LEA when its displacement fits. Private ownership, closure,
reservation, FIFO/grant and DMA checks remain. Standalone/resident sizes fall
25,694/29,601→24,304/28,211 bytes. All 44 compositions reproduce independently.

Original-controlled MC68000 completed median guest cycles:

| Call | Kickstart | .25 | .26 |
| --- | ---: | ---: | ---: |
| OwnBlitter uncontended | 62 | 356 | 320 |
| DisownBlitter uncontended | 64 | 302 | 282 |
| Text fixed8 8 | 7,912 | 9,894 | 9,800 |
| RectFill 16×8 | 2,368 | 3,144 | 3,092 |
| BltBitMap 16×8 | 2,416 | 4,798 | 4,702 |

All 34 original state/pixel/defined-result controls match. All 13 drawing cases
improve 52–96 cycles; all 16 Layer rows improve 52–144 cycles, including eight
original MC68000 controls. Other already-fast calls remain below Kickstart;
InitRastPort measures 1,010 versus 990 previously and 1,658 original. Large
remaining drawing and Layer dispatch gaps keep the overall objective active.

Profile and normal desktop execution agree on state, pixels, PCM, 702 fields
and 99,755,606 cycles. Cold readiness is 13.902138349 guest seconds and Tab
redraw 0.160246638 seconds / eight PAL fields. This retains .25's practical
desktop timing; no meaningful boot/redraw improvement is claimed. The unchanged
framebuffer hash is `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

All 148 producer graphics tests and ten pre-stage consumer pixel tests pass,
zero skips. Four new executable relocation/control-entry cases cover both CPUs.
The consumer Release build passes with zero warnings/errors. All 11 staged
native desktop/input/reset/graphics tests pass, zero skips. Host/migration
coverage has 79 passes, zero failures and three unavailable historical optional
replays, with the redraw.5 upgrade case executed. Complete results are in
`../CopperStart/artifacts/graphics-exec-address/qualification.json`.
The full producer solution retains its three existing Shell/DOS API errors.
CPU/chipset code, package pins, the exact reset bank and HDF payloads are unchanged.
Only the ROM contract/build hash changes; the existing .6 disk update remains.
These guest-cycle diagnostics are separate from host-throughput acceptance.
