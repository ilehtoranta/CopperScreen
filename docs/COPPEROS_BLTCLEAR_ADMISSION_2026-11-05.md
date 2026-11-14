# BltClear admission — 2026-11-05

The `.71` A1200 bundle removes obsolete synchronous-size and CPU-type branches
from C# BltClear admission. A common 512-byte DMA threshold replaces them. Chip
RAM selection, Fast RAM support, OCS short-remainder fallback, ownership and
direct hardware fences remain. No additional compiler changes are needed.

| 68000 synchronous zero, completed guest cycles | .70 | .71 | Kickstart |
|---|---:|---:|---:|
| 16 bytes | 442 | 438 | 544 |
| 512 bytes | 2,250 | 1,764 | 1,636 |
| 640 bytes | 2,674 | 2,034 | 1,876 |
| 768 bytes | 2,404 | 2,298 | 2,196 |
| 1,024 bytes | 2,858 | 2,812 | 2,676 |

The 512-byte call improves 21.6%; 640 bytes improves 23.9%. Across 566 clears,
553 improve, 11 match and two EC020 cases slow by 4–6 cycles within the unchanged
eight-cycle allowance. All 40 split completions improve by 4–48 cycles. Every
asynchronous split return still has DMA active. Both v7/v2 matrices repeat
byte-identically, with three warmups and 21 samples. Original EC020 timings are
unavailable. This is guest execution evidence, not host FPS.

The forced-DMA-disabled 512-byte control pays 26 more cycles for classification;
the gain targets Chip RAM DMA. Sub-512-byte CPU clears and argument/save overhead
remain opportunities. Fast RAM correctness is covered, not a claimed speed gain.

237 frozen graphics cases and the ordered probe pass; 19 optional cases remain
unavailable. The CPU instruction-equality control moves from 638 to 384 bytes so
it stays below the new DMA cutoff. The initial failure is retained; timing gates
are unchanged. Paired A1200 replays match CPU/RAM/display/pixels. Ready remains
12.0792278880542 guest seconds; no boot-speed gain is claimed.

The standard ROM is 524,288 bytes with 521,614 used, 36 fewer than `.70`.
Checksum is FFFFFFFF and the HDF is unchanged. Frozen producer dependencies and
replay runtime exclude unrelated ongoing work. Exact evidence is retained under
producer/consumer `artifacts/graphics-bltclear-admission-20261105`. The detailed
producer report is `CopperStart/Docs/NativeBltClearAdmission20261105.md` with its
measurement JSON. ROM and media remain local artifacts.

ROM SHA-256: `F99AE210B943E81A6166A8C937B5DF9F93982027782BD938823839D167F8B0D7`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.

Nineteen current-source ownership/identity/split cases and all 11 embedded app
native tests pass. Default and CopperStart-enabled Release builds pass without
warnings. Embedded ROM/HDF/manifest hashes match `.71`; the producer Release
build passes with 13 existing warnings. A frozen rebuild reproduces the measured
ROM exactly.
