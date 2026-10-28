# Bitmap-copy registers, masks and row control — 2026-10-26

The graphics-copy-registers.33 development qualification follows
[ownership publication](COPPEROS_GRAPHICS_OWNERSHIP_PUBLISH_2026-10-26.md).
It bundles the final register-cache/DBRA graphics bank with the exact reset bank
and unchanged layer-lock.9 HDF. The
[producer report](../../CopperStart/Docs/NativeGraphicsCopyRegisters20261026.md)
records profiling, implementation, rejected experiments and remaining gaps.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `af5147fd3564e48cb6a7a5dded1be0841287dfc576474b4be9a502226d1663d1` |
| Unchanged DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Bitmap bases and the programmed-mode key stay in registers across DMA planes.
CPU fallback restores the bases and invalidates the mode cache. Descending
edges retain that key above a bounded DBRA row counter, reducing loop overhead.
Adjacent private mask WORDs share an ascending MOVE.L with the same ordered
blitter register writes. Equivalent stride-bit tests and existing MOVEQ flags
remove redundant setup work. Ownership, public layouts, hardware bounds,
overlap, asynchronous submission and fallback behavior remain covered.

Completed MC68000 guest-cycle medians:

| BltBitMap | Kickstart 3.1 | .32 | .33 |
| --- | ---: | ---: | ---: |
| 16×8 | 2,416 | 4,380 | 4,232 |
| 184×64 | 14,580 | 15,690 | 15,562 |
| 600×64 | 41,674 | 42,766 | 42,674 |

The small case uses **3.38% fewer cycles**. All 34 original result comparisons
match; independent/full runs reproduce the timing table exactly. All 840 legacy
copy shapes improve by 8–1,238 cycles. In the separate expanded matrix, 1,677
of 1,680 improve; three regress by at most 78 cycles / 0.354%. All 840 expanded
original MC68000 controls match. Rectangle/text matrix medians remain unchanged.
These are guest-cycle diagnostics, separate from host-throughput acceptance.

All **165 final graphics tests and 11 bundled native tests pass**, zero skips.
The independent 28-case replay also passes. Coverage includes independent stride
boundaries, minterms, constants, masks, shifts, late CPU fallback/resume and a
new original-controlled 1,024-row descending case. All 44 image captures reproduce;
40 are unchanged and affected images shrink by six bytes. An earlier stale
incremental DLL failed image identities; fresh isolated final builds resolve it
and retain the failed evidence separately.

Consumer Release is clean. Host/migration has 82 passes and three unavailable
historical optional replays; four prior-seed upgrades execute. Producer Release
retains three existing Shell/DOS API errors. Cold readiness remains **13.84 guest
seconds** and redraw **160 ms / eight PAL fields**, with identical profile/control
CPU/RAM, pixels and PCM. No visible desktop timing gain is claimed. CPU/chipset,
compiler, package and HDF payloads are unchanged. Local evidence is
`../CopperStart/artifacts/graphics-copy-registers/qualification.json`.
Remaining Kickstart performance gaps stay active work.
