# ClearDma setup optimization — 2026-11-06

CopperSharp68k now removes ClearDma's D2 temporary and save, keeps the chipset
byte unexpanded for bit tests, preserves required hardware reads without unused
values, and emits shorter constant loads and grouped register saves. The bodies
remain C#; direct waits, ownership, Fast RAM support and DMA ordering are retained.

| 68000 synchronous zero clear, bytes | .73 remeasured | .74 | Kickstart |
|---|---:|---:|---:|
| 16 | 430 | 418 | 544 |
| 256 | 1,302 | 1,296 | 1,154 |
| 512 | 1,712 | 1,690 | 1,636 |
| 640 | 1,982 | 1,956 | 1,876 |
| 768 | 2,250 | 2,228 | 2,196 |
| 832 | 2,372 | 2,348 | 2,296 |
| 1,024 | 2,760 | 2,738 | 2,676 |
| 4,096 | 9,004 | 8,988 | 8,916 |

501 of 566 clears improve, 21 tie, and 44 take 2–8 more cycles. Of 40 split
cases, 34 improve and six take 4–8 more completed cycles. All asynchronous
returns retain active DMA and return 14–24 cycles earlier. Both matrices repeat
byte-identically within the unchanged limits. Original EC020 Kickstart timings
are unavailable. These are guest-cycle results, not host throughput measurements.

ClearDma saves eight bytes instead of twelve. The ROM uses 521,552 of 524,288
bytes, 24 fewer than `.73`. The paired candidate replay preserves the expected
desktop at 12.0792278880542 guest seconds; no boot-speed improvement is claimed.
1,887 compiler, 237 frozen graphics and 19 current-source cases pass, alongside
the probe/repeat checks. The engine passes 1,300 with seven optional skips;
nineteen optional graphics cases remain unavailable. Release builds pass.

The active 1 MiB full-filesystem app selection remains unchanged; its eleven
native app tests are separate coverage. The new standalone 512 KiB ROM is in
`CopperStart/artifacts/graphics-cleardma-saves-20261105/rom-qualified/`.
Detailed evidence: `CopperStart/Docs/NativeClearDmaSetup20261106.md`, with JSON,
source/runtime archives and hash manifest. Local consumer replay/build records
are under `artifacts/graphics-cleardma-saves-20261105`.

ROM SHA-256: `2AFA07F8AD8EFE142D3CFF60B8FA671384859412536F54F2D88649168F8495EE`.
