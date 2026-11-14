# BltClear incoming register homes — 2026-11-05

The `.72` bundle regenerates unchanged C# BltClear using CopperSharp's improved
incoming-register preferences and legal fixed-home coalescing. Count and flags
stay in D0/D1 through admission, avoiding redundant argument moves. There is no
custom assembler routine or patched body. Preservation and clobber checks remain.

| 68000 synchronous zero, completed cycles | .71 remeasured | .72 | Kickstart |
|---|---:|---:|---:|
| 16 bytes | 438 | 430 | 544 |
| 512 bytes | 1,760 | 1,736 | 1,636 |
| 640 bytes | 2,032 | 2,006 | 1,876 |
| 1,024 bytes | 2,810 | 2,790 | 2,676 |

565 of 566 clears improve; one patterned 706-byte case slows by four cycles,
within the unchanged allowance. All 40 split completions improve by 14–64 cycles.
Every async split return still has DMA running. Both v7/v2 matrices repeat
byte-identically with three warmups and 21 samples. Original EC020 timings remain
unavailable. The 512-byte size write moves from cycle 540 to 516; the 4,098-byte
bulk write moves from 740 to 702. This is guest-cycle evidence, not host FPS.

Public/private save frames remain. A C# split tail-call experiment delayed DMA
and failed timing gates, so its change was discarded and evidence retained. The
final C# split schedule, ownership, direct fences and Fast RAM support are intact.

1,867 compiler cases, 237 frozen graphics cases, the ordered probe, 19 current-source
cases and 11 embedded native app cases pass. Nineteen optional graphics cases are
unavailable. Producer Release passes with 13 existing warnings; default/enabled
consumer Release builds have none. Regeneration and embedded hashes are verified.
Paired A1200 replays match CPU/RAM/display/pixels; ready remains 12.0792278880542
guest seconds. No boot-speed gain is claimed.

The ROM uses 521,590 of 524,288 bytes, 24 fewer than `.71`; checksum FFFFFFFF.
The HDF is unchanged. Frozen producer/replay dependencies exclude unrelated work.
Exact local evidence is under `artifacts/graphics-bltclear-register-homes-20261105`
in both repositories. The detailed producer report is
`CopperStart/Docs/NativeBltClearRegisterHomes20261105.md` with measurement JSON.

ROM SHA-256: `2A3C1689D34A54E89ECA9C4A0C7F1B6268FFA9BC7A1F978E439E4AFE21992AD6`.
