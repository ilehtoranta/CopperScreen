# Bitmap-copy setup and DMA submission — 2026-10-26

The graphics-copy-setup.30 development qualification follows
[native Layer locks](COPPEROS_GRAPHICS_LAYER_LOCK_2026-10-26.md). It bundles an
updated graphics bank with the exact reset bank and unchanged layer-lock.9 HDF.
The [producer report](../../CopperStart/Docs/NativeGraphicsCopySetup20261026.md)
records source changes, original controls and remaining gaps.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `cc4bf5268c1dba8fadc67b61098affc459a8791a94c7d4c469c358129762c29b` |
| Unchanged DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Bitmap copies avoid an unnecessary D0 save/result stack slot, reuse the live
word count, use WORD arithmetic where the existing bounds prove it safe, and
publish geometry readiness once per exit. Address products/extents remain
32-bit; alignment, geometry, Chip-RAM, alias and fallback checks remain.

An original-ROM control also found that eligible copies should submit their
final plane while BLTEN or DMA master is disabled. Kickstart leaves it pending
and the destination untouched until DMA is enabled. The previous gate instead
performed a CPU copy. That gate is removed, matching the observed original
behavior and avoiding its per-plane setup cost. Multi-plane serialization is
tested with DMA enabled externally after the first plane becomes pending.

Completed MC68000 guest-cycle medians:

| Call | Kickstart 3.1 | .29 | .30 |
| --- | ---: | ---: | ---: |
| BltBitMap 16×8 | 2,416 | 4,702 | 4,486 |
| BltBitMap 184×64 | 14,580 | 15,890 | 15,756 |
| BltBitMap 600×64 | 41,674 | 42,994 | 42,838 |

All 34 original result controls match; the other 31 medians are unchanged.
All 840 copy-matrix cases improve by 18–502 cycles against .29, preserving
pixels, guards, counts and CPU/DMA selection. There are 84 original one-row
controls. Large performance gaps remain; no original 020 timing is claimed.

All 156 graphics tests and 11 bundled native tests pass, zero skips. Thirteen
final focused cases include disabled-DMA submission, independent strides, late
fallback and BLTSIZE zero (64 words × 1,024 rows), including original controls.
All 44 factory/relocation captures and the combined ROM reproduce independently;
40 captures are unchanged. Standalone/resident sizes fall by 28 bytes.
Consumer Release is clean; producer Release retains its three existing Shell/DOS
errors. Host/migration has 82 passes and three unavailable historical optional
replays. Four prior-seed upgrades execute.

Desktop profile/control agree at 700 fields / 99,471,402 cycles with unchanged
pixels and PCM. Cold readiness remains **13.86 guest seconds**, and Tab redraw
**160 ms / eight PAL fields**. No desktop timing improvement is claimed.
CPU/chipset code, package pins and HDF payloads are unchanged. Local evidence is
`../CopperStart/artifacts/graphics-copy-setup/qualification.json`.
Media stays outside Git; guest-cycle diagnostics remain separate from host
throughput acceptance. Remaining original-controlled optimization continues.
