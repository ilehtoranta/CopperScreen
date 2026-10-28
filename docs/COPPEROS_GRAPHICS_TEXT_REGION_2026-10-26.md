# Reusing admitted text geometry — 2026-10-26

The graphics-text-region.35 development bundle follows
[descending bitmap DMA](COPPEROS_GRAPHICS_COPY_SHIFT_2026-10-26.md). It retains the
same layer-lock.9 HDF and reset bank. The
[producer report](../../CopperStart/Docs/NativeGraphicsTextRegion20261026.md)
records implementation, original controls and the timing limits below.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `255151b89bbfba7030945cd6511020c12d0c21ac7ba7d862f2d23a1dc9b6fc3f` |
| Unchanged HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Layered Text reuses its preflight geometry when a locked Layer has exactly one
ClipRect. Multi-clip lists keep their full traversal. Clipping, Smart backing,
physical DMA admission, ownership, waits, template lifetime and cursor behavior
remain. Flat text follows the same instructions; no new memory-type query is added.

Completed MC68000 cycles, without an outer Layer lock:

| Call | Kickstart 3.1 | .34 | .35 |
| --- | ---: | ---: | ---: |
| Text 8 | 10,136 | 19,966 | 18,360 |
| Text 64 | 32,258 | 43,118 | 41,504 |

Eight-character Text improves **8.04%**; all eight CPU/count/nesting combinations
save 1,496–1,620 cycles. The eight original Layer result controls match. All 34
flat medians and the 2,016 rectangle, 840 legacy-copy and 168 flat-text matrix
medians remain unchanged. These are guest-cycle diagnostics; original 020 timing
and host-throughput acceptance are unavailable.

All 174 graphics tests and 11 bundled native tests pass, zero skips. New clipping
coverage checks 270 draws across baseline/candidate/original against independent
pixels, guards and cursors. A fresh 13-test replay reproduces all Layer/flat tables
and identities. All 44 captures reproduce; 40 are unchanged. Consumer Release is
clean; host/migration has 82 passes and three unavailable historical optional
replays. Producer Release retains three existing Shell/DOS errors.

The historical redraw probe always runs four pressed fields plus four released
fields, imposing a **160 ms observation minimum**. The new
[completion probe](../scripts/probes/CopperOsGraphicsCompletion/README.md) records
exact drawing entry/return separately. **Overall desktop completion does not
improve:** the readiness-run key-to-return latency is 137.178→138.329 ms; after
settling to field 1,000 it is 137.053→138.564 ms. Scheduling offsets the reduced
Text cost. Both baseline/candidate profile/control pairs match CPU/RAM, pixels,
PCM and milestones. The old protocol and its previous records remain unchanged.

Cold readiness is 13.822 guest seconds; the historical redraw observation remains
eight PAL fields. GUI, Home, launcher, HDF, compiler, CPU/chipset and package pins
remain unchanged. Remaining Layer/ownership, small-copy, 65-word boundary and
application-yield costs are still open. Local verification is recorded in
`../CopperStart/artifacts/graphics-text-region/qualification.json`.
