# Bitmap-copy DMA channels — 2026-10-26

The graphics-copy-channels.31 development qualification follows
[copy setup/submission](COPPEROS_GRAPHICS_COPY_SETUP_2026-10-26.md). It bundles
the new graphics bank with the exact reset bank and layer-lock.9 desktop HDF.
The [producer report](../../CopperStart/Docs/NativeGraphicsCopyChannels20261026.md)
records implementation, controls, reproducibility and remaining gaps.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `2209514a3db8cc00a0d9703110a1e8a3c76b20292160001516289fb8eadd594b` |
| Unchanged DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Full-word, unshifted source copies of at least 96 words per plane skip the
blitter's unnecessary destination read. Memory planes use B/D channels;
constant planes use D only. A small ROM table calculates the threshold for
each admitted width. Existing masks, minterms, geometry, memory bounds, overlap,
fallback, ownership and asynchronous completion behavior remain covered.

Completed MC68000 guest cycles for aligned 640×64 copies:

| Planes | Kickstart 3.1 | .30 | .31 |
| --- | ---: | ---: | ---: |
| 1 | 22,590 | 23,866 | 18,738 |
| 2 | 43,668 | 44,914 | 34,564 |
| 4 | 85,720 | 87,004 | 66,226 |
| 8 | 169,880 | 171,158 | 129,542 |

The eight-plane case uses **24.31% fewer cycles than .30 and 23.74% fewer than
Kickstart** in this diagnostic. The expanded v2 matrix covers 1,680 shapes on
MC68000/020 and 840 original MC68000 controls. Of these, 1,495 improve, 154 tie
and 31 regress by at most 74 cycles / 0.379%. Every selected channel-elimination
case improves. Pixels, guards and defined results match original controls;
an independent build reproduces the complete matrix. No original 020 timings
or host-throughput acceptance are claimed.

The retained 840-shape v1 protocol is separate: 731 improve, 89 tie and 20 regress
by at most 16 cycles against .30. All 34 flat original controls still match.
The small 16×8 copy falls from 4,486 to 4,466 cycles but remains slower than
Kickstart's 2,416. The other flat copies improve slightly; all 16 Layer medians
and result controls are unchanged. Ownership, text and Layer/Exec gaps remain.

All 161 graphics tests and 11 bundled native tests pass, zero skips. Coverage
includes every width's threshold boundaries on both CPUs, independent pixel
oracles, all minterms, masks, shifts, constant planes, CPU fallback/resume and
pending blits with DMA disabled. Forty of 44 emitted image captures are unchanged;
all captures and the combined ROM reproduce independently. Affected images grow
by 180 bytes; no CPU, chipset, compiler or package change is involved.

Consumer Release is clean. Host/migration has 82 passes and three unavailable
historical optional replays; four prior-seed upgrades execute. Producer Release
retains three existing Shell/DOS API errors. The HDF and its GUI/Home payloads
are unchanged.

Desktop profile/control agree on CPU/RAM, pixels, PCM and milestones at 699
fields / 99,329,300 cycles. Cold readiness measures **13.84 guest seconds**, one
PAL field below .30; this does not isolate a causal desktop gain. Tab redraw
remains **160 ms / eight PAL fields** with the same golden pixels. Local evidence
is `../CopperStart/artifacts/graphics-copy-channels/qualification.json`.
ROM/media stay outside Git; no package is published.
