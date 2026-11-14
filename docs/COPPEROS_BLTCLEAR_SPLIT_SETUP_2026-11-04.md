# BltClear setup and synchronous splits — 2026-11-04

The `.70` A1200 bundle shortens C# BltClear setup and removes the measured
synchronous split regressions. Separate ECS/OCS helpers avoid repeated chipset
selection. Both save frames are restored before the final completion fence;
ECS prepares its next pointer while the remainder DMA runs. Private hardware
waits use the existing A6 directly. Ownership and Fast RAM behavior remain.

CopperSharp now publishes explicit internal call argument effects to peephole
liveness and folds proven zero-extended masked-word comparisons. The original
MMIO read and its width/order remain. There are no handwritten graphics routines
or emulator timing changes in this qualification.

| 68000 synchronous split, completion cycles | Pre-split .68 | .69 | .70 | Kickstart |
|---|---:|---:|---:|---:|
| 4,098 bytes | 9,382 | 9,450 | 9,308 | 8,936 |
| 4,100 bytes | 9,566 | 9,592 | 9,428 | 8,936 |
| 8,194 bytes | 17,726 | 17,792 | 17,652 | 17,256 |
| 131,074 bytes | 267,884 | 267,960 | 267,808 | 267,506 |
| 131,200 bytes | 268,192 | 268,308 | 268,140 | 267,796 |

All 20 synchronous split cases beat `.68` by 44–138 cycles and `.69` by 134–192.
Asynchronous completion improves by 72–152 cycles versus `.69`, with DMA still
active at every measured return. A 4,098-byte asynchronous zero clear returns in
956 cycles instead of 1,040; Kickstart takes 480. Kickstart still has lower setup
cost. These changes improve CPU overhead and overlap, not DMA bandwidth.

The general v7 matrix contains 566 clears: 269 improve, 283 match and 14 regress
by at most eight cycles. Its existing timing limits pass. The explicitly versioned
v2 split matrix adds a fresh `.68` reference and synchronous regression gate.
Both matrices repeat byte-identically. Three warmups, 21 samples, display DMA off,
IPL 7 and the retained PAL configuration are unchanged. Original EC020 timing
remains unavailable. The missing `.56` ROM is covered only by a separate check
against retained historical v7 samples, not a new replay. No host FPS claim.

981 selected compiler cases, 237 frozen graphics cases and 19 current-source
identity/ownership/split cases pass. The ordered probe passes; 19 optional graphics
cases remain unavailable. Paired A1200 replays match CPU state, full Chip RAM and
pixels. Ready remains 12.0792278880542 guest seconds; no boot-speed gain is claimed.

The standard 524,288-byte ROM uses 521,650 bytes, only 18 more than `.69`, with
checksum FFFFFFFF. The HDF is unchanged. Exact inputs, rejected trial results,
repeated measurements and source provenance are retained in producer/consumer
`artifacts/graphics-bltclear-split-setup-20261104`. The detailed producer report is
`CopperStart/Docs/NativeBltClearSplitSetup20261104.md` with its measurement JSON.
Unrelated producer and emulator development is excluded from the qualified ROM
and frozen replay. ROMs and media remain local artifacts.

ROM SHA-256: `5D4F076A8EABC26E6E73F8C8A4CAF729E3B291F5D64756AE3D9CCE50EAFD3863`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.

Default and CopperStart-enabled Release builds pass without warnings. Embedded
ROM/HDF/manifest hashes are verified, and all 11 app native boot/drawing tests
pass against the embedded `.70` ROM.
