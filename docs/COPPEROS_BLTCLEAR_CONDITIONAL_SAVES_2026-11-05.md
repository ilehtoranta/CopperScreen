# BltClear conditional register saves — 2026-11-05

CopperSharp68k now places saves only on paths that use the preserved register.
The ordinary C# BltClear DMA entry saves four bytes instead of eight; packed
sizes reuse the other already saved register. The C# routine is unchanged and
all other generated kernel units retain their bytes. No custom graphics
assembler is added.

| 68000 synchronous zero, completed cycles | .72 remeasured | .73 | Kickstart |
|---|---:|---:|---:|
| 16 bytes | 430 | 430 | 544 |
| 512 bytes | 1,734 | 1,714 | 1,636 |
| 832 bytes | 2,396 | 2,372 | 2,296 |
| 4,096 bytes | 9,030 | 9,004 | 8,916 |

525 of 566 clears improve, 17 tie, and 24 slow by 2–4 cycles within the unchanged
allowance. All 40 split return/completion cases improve by 8–38 cycles, retaining
DMA at asynchronous returns. Both matrices repeat byte-identically with three
warmups and 21 samples. The ordered probe puts DMA size writes 24–26 guest cycles
earlier. Original EC020 timings remain unavailable. Kickstart still leads the
medium and large cases above; these results are not host-throughput measurements.

1,875 compiler cases, 237 frozen graphics cases, the ordered probe, and 19
current-source cases pass. The engine suite passes 1,300 cases with seven
optional skips; nineteen optional graphics cases are unavailable. Builds pass.
The candidate's paired desktop replay preserves expected pixels and reaches
ready at 12.0792278880542 guest seconds. Boot speed is unchanged.

Standalone `.73` uses 521,576 of 524,288 bytes, 14 fewer than `.72`, with checksum
FFFFFFFF and the unchanged HDF. The active app bundle was separately migrated
to the 1 MiB full-filesystem image and remains selected. Eleven native app cases
pass for that active image; they are separate from `.73`'s frozen replay coverage.
This work supplies the generated production kernel and a qualified 512 KiB ROM,
without replacing that concurrent app-bundle selection.

Exact local evidence is under `artifacts/graphics-bltclear-conditional-save-20261105`
in both repositories. The standalone ROM and source/runtime archive are in the
producer's `rom-qualified` directory. The detailed producer report is
`CopperStart/Docs/NativeBltClearConditionalSaves20261105.md`, with measurement JSON.

ROM SHA-256: `155A764C9CE1F2B79F959A085F3840494B8956A88ECA2AA8A418D7C6000A8FB2`.
