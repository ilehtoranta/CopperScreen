# Nonstandard OCS display and line-mode investigation — 2026-09-20

The display portion is implemented. Nonstandard blitter line widths are **not
complete** and remain explicitly rejected. This record does not certify complete
OCS emulation or transfer the previous keyboard/CIA performance exceptions.

## Display behavior

- HAM with zero to four enabled lores planes has no modify-control bits, so its
  visible codes select the palette. Hires HAM likewise has at most four planes.
- Lores BPU=7 makes Agnus fetch four planes at the ordinary lores cadence. Denise
  still decodes its six physical latches: manual/retained BPL5DAT and BPL6DAT can
  supply HAM control or other upper-plane data. This is not seven-plane hardware.
- Hires BPU=5–7 disables bitplane fetching and decoding; it does not clamp to four.
- Dual HAM uses the raw plane-5/6 control code and the selected dual-playfield
  palette index for direct colour or the modified component. Sprite composition
  uses both raw playfields and never feeds sprite colour into the HAM hold.
- Dual priority codes 5–7 blank the selected field's palette index to COLOR00.
  Its raw opacity still hides the field behind it. The other opaque field still
  participates in sprite masking. In dual HAM the blanked index also supplies
  component data; it does not remove the raw HAM control bits.

The existing 64-byte register-derived dual lookup handles invalid priority values;
the ordinary lores/hires pixel paths are unchanged. DMA plane-count decoding changes
only at BPLCON0 commits. Dual HAM stays in the existing special-render path. No
device timeline, callback, scheduler, per-pixel allocation or dependency was added.
The 454-pixel output profile continues to reject hires; native hires requires 908.

## Evidence and limits

The [Commodore HAM and display documentation](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_3.html)
defines ordinary operation. Nonstandard programming values need additional evidence.
The research pins vAmigaTS to `0489f55d22ef7560d924a304e30998aea6864264`:

- [BPLCON0 probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Denise/Registers/BPLCON0)
  `block2`, `block3`, `modes0`–`modes3` sweep plane counts, resolution and HAM/DPF.
- [BPLCON2 probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Denise/Registers/BPLCON2)
  `dualpf1`, `dualpf2`, `dualpf9`, `dualpf10` sweep priorities and scroll values.

These ten bootable ADF probes ran for 900 fields with local Kickstart 1.3, 908-wide
output and no input script. Their key bands, colour selection and blank regions
were compared with the published A500 photographs. The photographs are labelled
**A500_ECS**, not revision-specific OCS logic-analyzer captures. They support the
shared display behavior but do not independently certify OCS subpixel phases.
Reference TIFFs are emulator output with a different colour-transfer function;
they are supporting comparisons, not physical truth or exact RGB goldens.

WinUAE source was reviewed as supporting research at
[`e786c015b5b22c04b51573bf3d6fbc4c8792c8fe`](https://github.com/tonioni/WinUAE/tree/e786c015b5b22c04b51573bf3d6fbc4c8792c8fe).
No emulator renderer or scheduler was imported. In particular, its ordinary dual
priority special case alone was insufficient for the dual-HAM photographed bands;
the selected index must also be blanked in that combination.

Focused tests check palette/modify results, raw opacity and every sprite group,
delayed priority writes, BPU=7 DMA pointers versus retained data latches, and excess
hires plane counts. Existing timing tests retain their original authority. Exact
HAM hold reset at HBLANK/DIW, held colour across resolution/mode transitions, and
sprite/control phases still require physical evidence; see [ISSUES](ISSUES.md).

## Why line widths remain open

The [Commodore line-mode contract](https://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0128.html)
requires width two and does not specify the other encodings. The pinned
[line12/line13 probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Agnus/Blitter/line)
include source, ADFs and A500_ECS photographs. `line12` uses widths 1–8 across
octants; `line13` uses width one throughout.

Diagnostic runs explicitly bypassed the existing unsupported guard to inspect the
current approximation. They produced ordinary eight-octant lines. The published
`line13.tiff` emulator reference also shows ordinary lines, while its hardware
photograph shows a vertical striped result. The line12 photograph similarly has
the width-one stripe and nonordinary line details. Thus matching that emulator
reference would validate the wrong behavior. Legacy's available implementation
also advances ordinary four-phase pixels and does not supply a width oracle.

The guard has **not** been removed and no guessed width multiplier or image-specific
workaround was added. Needed next: a physical RAM/register/transfer capture for
widths 1, 3 and 0 (=64), with controlled initial C/D latches, masks, A/B/C enables
and single-dot state. It must distinguish C-read repetition, address/error steps,
first/last-word control and completion/IRQ timing. Published photographs alone do
not resolve those states. These two bypass runs are failed compatibility evidence,
not successful native validation or performance samples.

## Validation and performance

Production Release: zero warnings/errors. Engine diagnostics: **622 passed**;
host: **95 passed**, including all three enabled native checks; CopperDisk:
**74 passed**, zero skipped. The first 619-case run passed. After adding three
sprite cases, one existing CIA allocation test observed 2,752 bytes; the failure
log is retained. Its isolated rerun and complete 622-case rerun passed unchanged.

Reference: accepted `6651307`, engine SHA256
`D79290B6BD3ED08B5797DC89DC69F77A41E6D5B58935844DCEC85906BBE5890A`.
Display candidate engine SHA256:
`33BACBDCA3A8A427D09B405334364B890AB38991C2809A52BB2933942F69E64F`.

[Homogeneous retention v3](../../scripts/run-lightweight-homogeneous-retention-v3.ps1)
changes the pinned reference from v2 to the accepted keyboard/CIA build. Workloads,
fingerprints, runner/dependencies, placement, host-load policy, six balanced pairs
and the one-sided 95% upper frame-time bound ≤1% remain unchanged. Previous
protocols/evidence are preserved.

The frozen display candidate **passes all three workloads** against `6651307`.
All 36 samples had matching complete-workload fingerprints, zero measured
steady-state allocations and valid telemetry. No builds or tests ran concurrently.
Placement was logical CPU 2 on the Ryzen 5 5600X, with SMT sibling 3 protected,
Normal priority and host-load policy v2. The figures below are engine throughput;
they do not measure interactive desktop presentation.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Lores | 390.93 | 393.92 | -0.764% | +0.737% | PASS |
| Hires | 354.04 | 355.14 | -0.309% | +0.216% | PASS |
| Native Lemmings | 265.42 | 265.67 | -0.094% | +0.425% | PASS |

FPS columns are arithmetic means; acceptance uses the six paired log frame-time
ratios and the Student-t upper bound, not a ratio of those means. Raw samples,
telemetry, identities and the computed summary are in `retention-v3/` under the
local evidence root. The production DLL matches the frozen candidate hash, and
runtime/test source hashes remained unchanged through measurement. A final
`dualpf2` replay with that exact candidate also reproduced the corrected frame-900
capture byte for byte. No performance exception is needed for this display change.

Local evidence lives under ignored `artifacts/nonstandard-ocs-2026-09-20/`:
source hashes/patch, frozen reference/candidate, test logs, downloaded research,
media hashes and bounded native framebuffer/Chip-RAM captures. ROMs, probe media,
external source and generated captures are not committed.
