# CopperOS A1200 GUI and drawing optimization — 2026-10-25

This retained predecessor is superseded by the
[blitter and repaint-batching qualification](COPPEROS_DESKTOP_BLITTER_2026-10-25.md).

The development bundle reduces cold desktop readiness from **35.316 to 26.963
modeled guest seconds**, 1,763 to 1,346 PAL fields: **23.7% less boot time**.
The completed Home framebuffer is byte-identical. Relative to the original
54.867-second desktop bundle, the two optimization passes reduce startup by
50.9%. The earlier [memory/filesystem record](COPPEROS_DESKTOP_BOOT_SPEED_2026-10-25.md)
retains its original results and identities.

These are diagnostic guest-work measurements on the existing PAL Alice/Lisa,
68EC020 interpreter, 2 MiB Chip RAM, no slow/Fast RAM profile. They are not host
throughput acceptance, physical A1200 timings or installed/Store qualification.
The CPU package, hardware scheduling, bus contention, display geometry, native
GUI/Home programs and HDF are unchanged.

## Profile and changes

The [bounded boot probe](../scripts/probes/CopperOsBoot/README.md) now optionally
maps the hand-emitted graphics bank. Protocol v2 records that map's hash and
native bitmap-copy argument shapes. No observer or counter enters production
execution. Public `ExecuteFrame` controls match profiled field/cycle counts,
CPU registers, Chip RAM, framebuffer and PCM exactly for the candidate. The
baseline v2 run also matches the retained v1 baseline state and cycles.

| Milestone | Previous guest seconds | Current guest seconds |
| --- | ---: | ---: |
| GUI HUNK loaded | 9.64 | 9.64 |
| Desktop HUNK loaded | 11.64 | 11.64 |
| Intuition installed | 12.28 | 12.28 |
| Screen created | 15.50 | 14.42 |
| Window created | 22.04 | 17.69 |
| First ready desktop | 35.32 | 26.96 |

Module discovery runs every 20 fields; public readiness is sampled every field.
The normal app-session test observes 1,364 fields / 27.323 guest seconds at its
20-field readiness checkpoints. The screen, window, Home paint, native task
scheduling and input worker initialization are included.

The profile attributed about five seconds to the rectangle byte loop, with
branches for edge masks and drawing mode on every byte. It also exposed a large
shifted bitmap copy, `(10,12) -> (0,0), 620×232`, between 80/78-byte strides.
Optimizing only byte-aligned copies would miss that GUI initialization cost.

Changes in CopperStart's portable native graphics emitters:

- `NativeGraphicsRasterBodies.CompactRaster.cs`: RectFill chooses set, clear or
  complement once per plane; only the first/last bytes need masks. Interiors
  use even-aligned longword groups and exact byte tails. Single-byte rectangles,
  inclusive bounds, masked planes, pens and drawing modes retain their behavior.
  SetRast uses the same wide-store principle. Transparent JAM1 source-zero text
  pixels return before saving registers and traversing Layer ClipRects.
- `NativeGraphicsRasterBodies.PlanarBlit.cs`: byte-aligned source copies use
  bulk moves in the established forward/backward overlap direction. Matching
  address parity permits longwords; opposite parity uses byte copies. Shifted
  source copies with disjoint row spans stream a sliding source pair, handling
  edge masks separately. Aliases, negative adjusted source positions, constant
  planes, other minterms and unsupported fast-path shapes retain the general
  path. OwnBlitter/WaitBlit/DisownBlitter and existing DMA eligibility remain.

## Drawing work measurements

An emitted-instruction diagnostic runs on 68EC020 with synthetic RAM/Exec and
no chipset DMA. These are CPU-only operation costs, not end-to-end GUI latency
or real Chip RAM timing. Rectangle bounds are `(4,14)..(615,227)`, two planes;
SetRast clears/fills 640×256, two planes. Copies use two planes, 232 rows and
624 aligned or 620 shifted pixels, with the actual shifted source x=10.

| Operation | Previous CPU cycles | Current CPU cycles | Ratio |
| --- | ---: | ---: | ---: |
| Rectangle fill | 1,211,622 | 81,087 | 14.94× |
| SetRast | 410,449 | 52,093 | 7.88× |
| Aligned bitmap copy | 3,995,330 | 168,258 | 23.75× |
| Shifted bitmap copy | 4,537,966 | 845,454 | 5.37× |

## Frozen build and bundle

The original graphics emitter source set was recovered from portable PDB hashes,
retained snapshots and Git. Its rebuilt E60000 graphics image and combined ROM
reproduce fast.1 byte-for-byte before changes. The emitter's diagnostic label
export is build-time only and also preserves exact emitted baseline bytes.
Only the two files above are overlaid in the candidate; unrelated live producer
changes are excluded. The reset bank is retained exactly from fast.1. Graphics
grow from 27,841 to 28,747 bytes.

| Artifact | SHA-256 |
| --- | --- |
| Previous combined ROM / reproduced baseline | `028a12aff709cdca19e29000f6ae13f5174fbab49bc75b192c24100232fcf780` |
| Current combined ROM | `ea47ed531629375bcbf812eabc6648811b9825ff4c918e4ef737aa090d452423` |
| Unchanged desktop HDF | `e113a115bc2518014b79526f00e07c8bccacac3dc2537f30734115f4085f82ce` |
| Completed Home framebuffer, both versions | `792f7c9b344170df46ef8a8c119246783c07fc8ac2c1d30f3aae126370edf785` |

The [firmware contract](../CopperScreen/Firmware/CopperStartA1200.json) and
production build gate pin this new development qualification. Existing personal
HDFs and write protection remain intact; the app extracts the new hash-named ROM
cache on opening/reselecting its owned desktop profile. No ROM/HDF is committed
and no package is published.

Local evidence: `artifacts/copperos-gui-speed/{frozen-graphics,candidate-graphics,
baseline,stream-candidate,baseline-profile,fill-profile,drawing-profile,
copy-shapes,final-profile,final-control,graphics-tests,ui-validation}` and the two
`*-drawing-cost.json` files. Original inputs, maps, source reconstruction and
composition tools are retained there. Initial intermediate candidates are not
the final bundle.

## Validation and limits

- The focused frozen-emitter selection passes **261 tests, zero skips**, including
  an operation-cost diagnostic. Native 68000 and 68EC020 cases verify wide fill
  alignment, masks, every drawing mode, single-byte rectangles, tails, guards,
  saved registers, copy overlap in both directions and 800 deterministic shifted
  copy shapes per CPU against an independent pixel oracle. Existing tests cover
  Layer clipping/backing, bitmap-to-RastPort minterms, text modes and display setup.
- The wider selection exposes two pre-existing text-admission expectation failures
  (`unsupported: 3/4`, spacing and late glyph-width mismatch) on both the unchanged
  baseline and candidate: the frozen implementation already supports these via
  its general compositor. That five-case theory is excluded from the focused
  count, not reported as passing or skipped coverage. No production behavior is
  changed to satisfy those stale expectations. An initial cost run also exceeded
  the old two-million-instruction fixture budget; the completed diagnostic uses
  an explicit five-million bound for both versions.
- The actual embedded desktop boot, raw-keyboard Editor, reset and writable HDF
  cold reopen replay passes **one test, zero skips**. The cold-start bound tightens
  from 1,900 to 1,450 fields. The independent UI-runtime desktop boot passes
  **one test, zero skips** at its 1,500-field checkpoint. Host launch/upgrade checks
  pass **53 tests**; three optional older workspace/recovery replays are unavailable
  and skipped, not successful native coverage.
- `dotnet build CopperScreen.slnx -c Release` passes with zero warnings/errors.
  The broader current-source CopperStart solution build encounters three unrelated
  Shell/DOS API errors: `LoadSegmentNative`, `UnloadSegmentNative` and
  `UnloadDosProcessSegment`. The frozen candidate emitter and exact embedded
  consumer build/replays above succeed; the wider solution is not reported green.

Remaining boot work includes loading the native programs, GUI private-state
initialization, allocator/library calls, clipping and text composition. The full
desktop still performs its normal initialization and painting.
