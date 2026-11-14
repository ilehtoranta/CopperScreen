# BltClear Chip RAM dispatch — 2026-11-04

The `.66` A1200 bundle keeps BltClear in C# and replaces ExecBase/MaxLocMem
classification with the supported hardware Chip RAM address aperture.
Chip RAM falls through to the DMA-enable check; Fast/slow RAM retains CPU
support. Existing crossover thresholds, ownership, direct hardware waits
and completion behavior remain. No compiler or emulator changes are included.

The separate ordered 832-byte probe reduces public dispatch difference
**294→276 guest cycles** (about 6%) and preparation before DMA **744→724**.
Completion is unchanged. Kickstart remains faster overall.

Two identical 572-row v7 runs compare `.65`, `.66` and Kickstart 40.63, with
three warmups, 21 samples, display DMA off, IPL 7, and public BltClear plus
WaitBlit. Of 566 clears, **273 improve, 274 match, 19 are 2–8 cycles slower**;
getter controls match and crossover limits pass. These are guest-cycle
measurements, not host FPS. Original EC020 comparison is unavailable.

| Bytes | .65 | .66 | Kickstart 40.63 |
|---:|---:|---:|---:|
| 16 | 442 | 442 | 544 |
| 512 | 2,250 | 2,250 | 1,636 |
| 704 | 2,444 | 2,422 | 1,976 |
| 768 | 2,562 | 2,544 | 2,196 |
| 832 | 2,624 | 2,632 | 2,296 |
| 1,024 | 3,042 | 3,022 | 2,676 |
| 4,096 | 9,290 | 9,270 | 8,916 |
| 4,098 | 9,512 | 9,494 | 8,936 |

The 832-byte case is eight cycles slower in this benchmark despite shorter
dispatch; do not treat the dispatch saving as a universal completed-clear win.
The C# unit is 1,072 bytes, six fewer than `.65`. The 524,288-byte ROM uses
522,238 bytes, has 2,050 padding bytes and a 74-byte footer gap; checksum FFFFFFFF.

241 frozen graphics cases and the ordered profile pass. Nineteen optional
cases remain unavailable. Eleven app-native tests and three working-source
identity tests pass. Paired A1200 desktop/Tab replays match CPU/state, RAM and
pixels; desktop-ready is unchanged at 12.0792278880542 guest seconds.
Default/enabled production builds and embedded-ROM/HDF/manifest checks pass.
New tests cover the Chip/Fast boundary and large
CPU clears while unrelated DMA remains busy.

Qualification: `diskboot-2026-11-04-a1200-desktop-single-bank.66`.
ROM SHA-256: `2655A3566D93E5CF63F86CB3F3C764F0BAC346952C93D5864717BBEB6CF321B6`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
Producer report: `CopperStart/Docs/NativeBltClearChipDispatch20261104.md`, with
machine-readable data in `Docs/Measurements/NativeBltClearChipDispatch20261104.json`.
Raw evidence is under `artifacts/graphics-bltclear-chip-dispatch-20261104` in
both repositories. Exact frozen source/runtime files are in the producer's
`rom-qualified` directory. Other producer work is excluded from the bundle.
