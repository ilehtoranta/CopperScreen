# BltClear admission and setup — 2026-11-06

Qualified standalone `.75` against remeasured `.74` and local original Kickstart
3.1. Guest routine bodies remain C# compiled by CopperSharp68k. No custom graphics
assembler, generated-byte patch, emulator change or package release was added.

## Changes and profile

The C# admission path selects ECS/OCS once before ownership. ECS no longer retains a
chipset temporary or repeats its capability test after setup. The bounded OCS
helper retains its dimensions and CPU crossover. Each private DMA body now
saves one register (4 bytes), versus two (8 bytes) previously. The public DMA
entry still saves four bytes. Selecting before the private helper also avoids
an extra save/restore on OCS. This duplicates a small setup sequence to remove
a live value and a branch, adding 70 linked ROM bytes overall.

CopperSharp68k selects CMPA.L directly for a single-use address comparison,
omits word-read normalization when all emitted consumers use only the word,
and selects BTST for zero-save leaf predicates. This removes the address-copy,
DMACONR extension and completion-mask temporaries. Generic compiler tests cover
unsigned address boundaries, both CPU models, register preservation, and both
enabled and disabled late peepholes. Memory-read width and order are retained.

Ordered diagnostic size-write starts move from 476/476/478 to 442 cycles for
512/832/1024-byte clears and from 664 to 630 for the 4098-byte bulk submission.
The ordered 512-byte completed call plus public WaitBlit drops from 1720 to
1670 cycles, versus Kickstart's 1642. Raw instruction traces accompany the JSON.
These single ordered samples
are distinct from the warmed median protocol below; DMA phase affects completion.
Observed and unobserved diagnostic runs agree.

Ownership still uses the atomic word operation and uncontended fallthrough.
Required Agnus reads, direct private waits, Fast RAM CPU fallback, public ABI,
pattern semantics, synchronous completion and remainder-first asynchronous
splits remain intact. OCS/ECS tests cover limits, guards and CPU/DMA crossover.

## Completed guest cycles

Unchanged v7 protocol: PAL 8375/ECS blitter, OCS Denise, 2 MiB Chip RAM, display
DMA disabled, IPL7, 3 warmups and 21 samples. Both matrices repeated byte-for-byte.

| 68000 synchronous zero clear, bytes | .74 remeasured | .75 | Kickstart |
|---|---:|---:|---:|
| 0 | 178 | 178 | 526 |
| 16 | 418 | 418 | 544 |
| 256 | 1,296 | 1,296 | 1,154 |
| 512 | 1,696 | 1,672 | 1,636 |
| 640 | 1,960 | 1,942 | 1,876 |
| 768 | 2,228 | 2,186 | 2,196 |
| 832 | 2,348 | 2,330 | 2,296 |
| 1,024 | 2,738 | 2,720 | 2,676 |
| 4,096 | 9,010 | 8,966 | 8,916 |

317 of 566 cases improve, 241 tie and 8 regress
by at most 4 cycles. All
remaining regressions, including the small clears, are recorded in the JSON;
they were not hidden by the crossover acceptance window. Zero-count cases tie.
Split completion deltas range from -52 to -16 cycles.
Asynchronous returns save [48, 58] cycles and
retain active final DMA. The 144 historical numerical
crossover limits also pass; their unavailable older ROM was not freshly replayed.
Original Kickstart EC020 timing is unavailable. No host throughput claim is made.

Rejected trials are retained separately: passing the custom base as an argument
increased register pressure; moving the zero-count guard increased empty-call
cost by 22–24 cycles and introduced other regressions. Dispatching OCS through
the ECS helper caused redundant preservation and regressed OCS, so admission
now selects the helper directly. None of those trial layouts was retained.
The separate OCS/ECS crossover results and deltas against recorded `.74` are
also included in the JSON; they are not Kickstart timing comparisons. All 164
cases in each of the four CPU/chipset combinations improve: OCS by 8–58 cycles
on 68000 and 12–54 on EC020, ECS by 14–52 and 12–54 respectively.

## Validation and ROM

1,895 compiler tests, 237 frozen graphics tests, the ordered probe, both repeated
matrices and 19 current-source tests pass. Of 20 optional graphics skips, one is
the separately run ordered probe; 19 remain unavailable. The engine passes
1,300 tests with seven optional skips. Producer and consumer Release builds pass.
Generator --verify reproduces the generated units. Only BltClear and BlitterClose
units differ from `.74`; BlitterClose shrinks from 46 to 42 bytes.

The ROM is exactly 524,288 bytes, uses 521,622 bytes and has 2,666 padding bytes;
checksum FFFFFFFF. The paired candidate desktop replay preserves the expected
pixels and ready time (12.0792278880542 guest seconds). This is behavior
coverage, not evidence of faster boot.

ROM SHA-256: `8AFD9A088112EAF2791FE7310C9C0457FEE0F11A8C1F1780CF7860CDF23510C5`.

The active app's concurrent 1 MiB full-filesystem selection remains unchanged.
Production generated kernels are updated; this 512 KiB qualification uses the
frozen standalone emitter/reset dependencies inherited from `.74`. Current-source
identity tests separately cover the current emitter. No ROM or media is committed.
The producer Release build has six warnings (existing nullable/package-version
diagnostics); the consumer Release build has none. Neither has build errors.

Local reproducible evidence, exact sources, generating binaries and SHA-256
manifest: `artifacts/graphics-bltclear-overhead-20261106/rom-qualified`.
Full results: [NativeBltClearOverhead20261106.json](../../CopperStart/Docs/Measurements/NativeBltClearOverhead20261106.json).

Earlier BltClear experiments compared CPU/DMA crossover, ownership and split
setup. The [register-home checkpoint](COPPEROS_BLTCLEAR_REGISTER_HOMES_2026-11-05.md)
records the `.72` results. The [ClearDma setup record](https://github.com/ilehtoranta/CopperScreen/blob/de78b91be4f6aa1b033ed81ecf33656912cb1414/docs/COPPEROS_CLEAR_DMA_SETUP_2026-11-06.md)
records `.74`, the remeasured reference for this `.75` qualification. These
records report guest cycles. The desktop replay retained the same ready time.
