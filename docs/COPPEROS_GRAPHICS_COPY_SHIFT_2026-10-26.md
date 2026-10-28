# Descending bitmap-copy DMA — 2026-10-26

The graphics-copy-shift.34 development qualification follows
[bitmap-copy registers](COPPEROS_GRAPHICS_COPY_REGISTERS_2026-10-26.md).
It bundles the shifted-mask graphics bank with the exact reset bank and unchanged
layer-lock.9 HDF. The
[producer report](../../CopperStart/Docs/NativeGraphicsCopyShift20261026.md)
records the hardware-manual method, correctness correction and measurements.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `663fbaba3e3de952b80b09850acf758ef3b38366f31233791304cb0da0a08458` |
| Unchanged DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Descending rectangles fitting 64 source words use shifted A masks and B source
DMA with C/D destination, removing the per-row CPU edge. Other minterms such
as XOR also gain this path. Constant planes retain masking/direction with B
disabled. Existing physical bounds and alias admission retain CPU fallback.
The 65-source-word legacy carry path remains separate.

Original controls also found that the tested disabled-DMA final planes should
stay pending with untouched destination bytes. Previous copy wrote its CPU edge
early, and XOR completed on the CPU. Four native cases failed before the change;
the original control passed. The new path matches pending and later completion.

Completed MC68000 cycles for descending 640×64 copies:

| Planes | Kickstart 3.1 | .33 | .34 |
| --- | ---: | ---: | ---: |
| 1 | 22,990 | 32,236 | 24,440 |
| 2 | 44,588 | 61,744 | 46,082 |
| 4 | 87,722 | 120,764 | 89,316 |
| 8 | 174,080 | 238,804 | 175,856 |

The eight-plane case uses **26.36% fewer cycles**, now 1.02% above Kickstart.
Every measured multi-row descending shape improves. Expanded copy-v2 has
1,435 improvements, 131 ties and 114 regressions of at most 58 cycles / 0.529%,
with all 840 original result controls matching. Legacy copy-v1 has 769 gains,
33 ties and 38 regressions of at most ten cycles. Rectangle, text and Layer
timings remain unchanged. These are guest-cycle diagnostics, not host-throughput
acceptance; original 020 timing is unavailable.

All **173 graphics tests and 11 bundled native tests pass**, zero skips. The
independent 41-case replay passes. New tests cover all 120 descending alignments,
whole-buffer guards and disabled-DMA behavior; existing constants, all minterms,
fallback/resume and 1,024-row cases remain. The retained 65-word path has an
after-WaitBlit original control, but is outside the new pending-state qualification.
All 44 captures reproduce; 40 are unchanged and affected images grow by 166 bytes.

Consumer Release is clean. Host/migration has 82 passes and three unavailable
historical optional replays; four prior-seed upgrades execute. Producer Release
retains three existing Shell/DOS API errors. Cold readiness remains **13.84 guest
seconds** and redraw **160 ms / eight PAL fields**, with matching profile/control
CPU/RAM, pixels and PCM. No visible desktop timing gain is claimed. CPU/chipset,
compiler, package and HDF payloads are unchanged. Local evidence is
`../CopperStart/artifacts/graphics-copy-shift/qualification.json`.
Other Kickstart performance gaps remain active work.
