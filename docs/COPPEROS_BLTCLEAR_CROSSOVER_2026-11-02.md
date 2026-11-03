# CopperOS BltClear crossover qualification — 2026-11-02

Development bundle `diskboot-2026-11-02-a1200-desktop-single-bank.58` fixes the
measured synchronous crossover regressions in `.57`. The implementation remains
C#, compiled into ROM by CopperSharp68k. It uses 64-byte CPU blocks, synchronous
DMA cutoffs of 704 bytes on 68000 and 770 bytes on 68020, shorter DMA setup and
fencing, direct OCS/ECS size paths and a CPU store for an OCS final-word tail.
Asynchronous DMA remains eligible from 512 bytes. Fast RAM and disabled DMA
retain CPU handling; OCS partial rows below 1 KiB retain their CPU fallback.

The compiler removes repeated constant-address reloads and folds dead temporary
byte loads plus bit tests into memory bit tests, preserving access width/count
and observable flags/registers. No hand-written assembly, emulator, scheduler or
CPU timing change is introduced.

Median completed public BltClear plus public WaitBlit cycles, synchronous calls:

| CPU / bytes | Pre-regression `.56` | Previous `.57` | New `.58` | Kickstart 40.63 |
| --- | ---: | ---: | ---: | ---: |
| 68000 / 640 | 2,916 | 2,930 | 2,738 | 1,876 |
| 68000 / 768 | 3,186 | 3,386 | 3,180 | 2,196 |
| 68000 / 770, patterned | 3,264 | 3,596 | 3,172 | 2,226 |
| 68000 / 832 | 3,306 | 3,616 | 3,232 | 2,296 |
| EC020 / 768 | 3,266 | 3,254 | 3,144 | unavailable |
| EC020 / 832 | 3,392 | 3,472 | 3,318 | unavailable |

The 572-row `compiled-raster-v7-crossover-phase-envelope` comparison uses three
warmups and 21 samples, output/guard/register checks and a DMA-disabled CPU
control. No measured BltClear row is slower than `.57`; none exceeds `.56` by
more than the eight-cycle PAL bus-phase envelope. A CPU-only control also checks
identical per-PC instruction counts. Separate OCS/ECS selection grids cover both
CPUs. These are guest-cycle diagnostics, not host-throughput acceptance or a
boot-speed claim. Kickstart still has lower fixed overhead; its EC020 fixture
remains unavailable. Full rows and protocol details are in producer
`Docs/Measurements/NativeBltClearCrossover20261102.json` and
`Docs/NativeBltClearCrossover20261102.md`.
The sole increase against `.56` is six cycles in the patterned 702-byte 68000 case.

Ownership, completion flags, Agnus fencing, patterns, packed sizes and supported
OCS/ECS limits are preserved in the native checks. The standard ROM is exactly
**524,288 bytes**, with **522,254 used**, 2,034 padding bytes, 108 bytes before the
footer and checksum FFFFFFFF. The generated clear is **1,122 bytes**, only
**246 bytes** larger than `.57` (0.047% of the ROM). All routines execute from ROM;
there is no extension bank or executable RAM copy.

Paired A1200 desktop replays match CPU state, full Chip RAM, pixels, audio,
display registers, milestones and scripted Tab input. Readiness remains
12.0792278880542 emulated seconds. Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

ROM SHA-256: `E162450EB1AF3424E91A6A013238F947FAE8D9516A887B26DBABF78AAD5072E0`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The manifest and project hash gate select `.58`. Qualification uses the frozen
`.57` composition and `.49` reset input, excluding concurrent producer changes.
Exact source/runtime receipts, profiles, tests and size proof remain in producer
`artifacts/graphics-bltclear-crossover-20261101/`; paired captures remain under
that path in this consumer. ROMs, media and artifacts remain local and
uncommitted. No package is published.

Validation passes 1,040 selected compiler cases, 229 frozen graphics cases,
three separate working-source image checks,
11 app native desktop/DMA checks and 69 launcher/update checks. Nineteen optional
graphics and eight optional host cases remain unavailable; historical expanded
image and unrelated ColorMap32 exclusions are unchanged. Two final 572-row runs
agree exactly. Both production solutions build (nine existing producer warnings,
no consumer warnings or errors). The built ROM/HDF/manifest resources were read
back and verified.
