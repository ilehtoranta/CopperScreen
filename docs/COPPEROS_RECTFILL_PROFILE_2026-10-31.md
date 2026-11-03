# RectFill profile - 2026-10-31

This profiles the unchanged `.42` image qualified in
[Layers release](COPPEROS_LAYER_RELEASE_2026-10-30.md). No production graphics,
ROM, HDF or bundle metadata was changed. The test harness now compares flat and
layered calls with identical rectangle coordinates and captures each guest
instruction externally. This is a diagnostic guest-cycle profile, separate
from host-throughput acceptance.

## Controlled comparison

Protocol `rectfill-flat-layer-profile-v1`: PAL 8375/OCS Denise, 2 MiB Chip,
display DMA off, IPL 7, depth 2, 80-byte stride, 40-row bitmap, APen 2, Mask 3,
DrawMode 1. The rectangles are `(3,2)..(18,9)` and `(3,2)..(186,33)`.
The layered case uses one visible Simple ClipRect covering `(0,0)..(639,39)`,
no outer lock. Each result uses 3 warmups and 21 samples and includes final
WaitBlit completion. All pixel/cursor hashes agree across flat/layer and both
CPUs; the four MC68000 controls also agree with original Kickstart.

| MC68000, completed median cycles | CopperStart `.42` | Kickstart | Ratio |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,854 | 2,362 | 1.21x |
| RectFill 16x8, layered | 7,140 | 3,570 | 2.00x |
| RectFill 184x32, flat | 7,228 | 6,456 | 1.12x |
| RectFill 184x32, layered | 10,092 | 7,278 | 1.39x |

The 68EC020 native medians are 2,876/7,404 cycles for flat/layered 16x8, and
7,278/10,382 for flat/layered 184x32. Original-020 timing is not claimed.
These fresh runs alternate flat/layer cases. Small bus-phase differences from
previous `.42` measurements are retained rather than rewriting earlier evidence.

## Small rectangle: CPU path and Layers cost

The trace executes `RectangleWords.cs`, with no fill-DMA submission. The two
selected planes each use an eight-row, two-word masked CPU loop. Layers plus
its Exec calls account for **4,362 of 7,056 return cycles (61.8%)**; the flat
router is itself 46 cycles of that group. This is not a per-draw scheduling/yield
cost: the observed Exec boundaries are the existing Forbid/Permit transactions.

| Small layered call: traced return cycles | Cycles | Share |
| --- | ---: | ---: |
| Layer routing/clipping/frames | 2,092 | 29.6% |
| Layers kernel | 2,038 | 28.9% |
| Exec Forbid/Permit incl vectors | 232 | 3.3% |
| CPU word fill | 1,640 | 23.2% |
| Blitter ownership incl vectors | 496 | 7.0% |
| Raster entry/mode/frame | 462 | 6.5% |
| WaitBlit incl vector | 84 | 1.2% |
| Public RectFill vector | 12 | 0.2% |

The 1,640-cycle word helper breaks down as follows:

| Word helper | Cycles |
| --- | ---: |
| Coordinate/crossover/alignment and selected-plane preflight | 440 |
| Offset, edge masks and width dispatch | 202 |
| Plane selection/operation dispatch and helper return | 382 |
| Actual row loops, including address increments and DBRA | 616 |

Only 320 cycles of those row loops are the 16 longword read-modify-write
instructions; 128 advance the row pointers and 168 implement DBRA. The rest
of the word helper is setup and dispatch. The raster entry also saves/restores
another frame and performs coordinate/mode setup after Layers clipping.

With Layers removed, the completed small call is **20.8% slower** than original,
versus **100% slower** when layered. The extra flat-to-layer latency is 4,286
native cycles versus 1,208 original cycles. The difference of 3,078 cycles
accounts for **86.2% of the 3,570-cycle layered performance gap** in this
CPU-only case. This subtraction is a same-geometry controlled latency result,
not an assumption that every wrapper instruction costs the same in both paths.

## Large rectangle: blitter path

The 184x32 trace reaches `RectangleFillDma.cs` and submits both planes to the
blitter. It does not take the CPU word or byte-fill path. Its representative
flat MC68000 return trace is:

| Large flat call | Cycles |
| --- | ---: |
| Raster entry/mode/frame | 514 |
| DMA geometry and admission | 848 |
| Selected-plane preflight | 266 |
| Static custom-register setup | 318 |
| Plane dispatch/submission/return | 524 |
| WaitBlit, including its vector jumps | 2,334 |
| Blitter ownership, including vector jumps | 538 |
| Public RectFill vector and flat routing | 58 |
| **Return total** | **5,400** |

The completed median is 7,228 cycles: its final completion tail adds 1,828
cycles beyond the return median. WaitBlit categories include polling, call
cost and bus stalls; they are not pure DMA execution time. Blitter execution
also overlaps other CPU instructions, so it must not be added again as an
independent cost.

The layered large call has a 10,006-cycle return median and a 10,092-cycle
completed median. Much of the final-plane transfer runs during Layers release,
leaving an 86-cycle completion tail. Its net flat-to-layer completion increment
therefore understates the CPU wrapper cost. In its representative trace,
Layers kernel and wrapper cycles rise to 2,212 each while DMA is active;
these instruction-cycle counts include bus contention.

## Measured opportunities

For layered short fills the largest remaining target is the **2,092-cycle
routing/clipping/caller-frame portion**, alongside the 2,038-cycle owner kernel.
For flat short fills, **1,024 of 1,640 word-helper cycles are setup/preflight/
dispatch**, exceeding the 616-cycle row loop. Those are the next useful audit
areas; the profile does not establish that every check is removable or that
forcing blitter use would improve the short case. The large case already uses
the blitter and exposes 1,956 cycles of DMA setup/submission plus its waits.
No implementation changes were made from these observations.

## Reproduction and artifacts

The focused test is
`LayerCallBenchmarks.ProfileRectFillWithIdenticalFlatAndLayerCoordinates`.
Use the standard reference-ROM, benchmark-ROM/HDF, Layers-baseline-ROM and
Layers-output environment variables; the baseline variable may name `.42`
for this diagnostic. Run only that test in Release. Its test result passes
one fact covering eight native cases and four original controls.

Producer artifacts: `CopperStart/artifacts/rectfill-profile-20261031` contains
`rectfill-calls.json`, traces per geometry/path/CPU, `symbols.json`,
`breakdown.json`, the attribution script and a retained emitter-map tool.
The tool loads the frozen `.42` emitter, verifies its entire emitted code
byte-for-byte against the qualified ROM, locates the helper sequences and
reads their build-time labels. Every attributed native instruction trace sums
exactly to its observed return cycles, with no unclassified instructions.
The attribution script's row-loop subcategory is specific to the measured
two-word short case.

ROM remains one **524,288-byte** file, **520,462 used bytes**, **3,826 padding**.
SHA-256: `501D63931DFF0735B24B71FBD10F7DD2521146310C1B672383A7B967AE3DF76F`.
Original SHA-256:
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
ROM/media and local artifacts are not committed or published.
