# CopperOS A1200 Layers optimization — 2026-10-30

Qualification `diskboot-2026-10-30-a1200-desktop-single-bank.40` follows the
[text-column checkpoint](COPPEROS_TEXT_COLUMNS_2026-10-30.md). It isolates
graphics/Layers work against the same frozen reset/compiler inputs and unchanged
`exec-preemption.10` HDF. Concurrent producer ROM migration is outside this image.

The Layers owner emits a position-independent ROM kernel for recognized
empty-queue transitions. It retains private root/descriptor admission, task
ownership, nesting, late waiter reads and public semaphore publication order.
Contention, continuations and unrecognized owner records use the published bridge.
Caller Forbid depth is preserved, including the near-overflow fallback case.
Drawing still runs with pre-emptive multitasking; no unconditional yield is added.

The one-time graphics initializer moves into the checked free ROM gap before
the fixed `$FF9C00` Resident. Both fragments execute from ROM. The file remains
**524,288 bytes**, including padding. Used code/data, vectors, command bridge and
footer total **520,506 bytes**, leaving **3,782 padding bytes**. Graphics uses
25,908 bytes: 24,980 contiguous Resident/code bytes and a 928-byte initializer
at `$FF9860`. The final code has 600 bytes before the checksum/footer.

| Artifact | SHA-256 |
| --- | --- |
| A1200 ROM | `C9D8747E186A2EAA1D82FEFEE61131094DCB179D5C0862A85C759502BB01EB57` |
| Unchanged HDF | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The unchanged installed-owned-layer protocol measures median MC68000 guest
cycles through completion: PAL 8375/OCS, 2 MiB Chip, display DMA off, IPL 7,
three warmups and 21 samples. All 16 rows across both CPUs and outer-lock
nesting match baseline state/pixels; MC68000 also matches original v40.63.

| No outer lock | Kickstart | `.39` | `.40` |
| --- | ---: | ---: | ---: |
| RectFill 16x8 | 3,570 | 12,964 | 8,676 |
| RectFill 184x32 | 7,278 | 16,150 | 11,508 |
| Text 8 | 10,136 | 18,494 | 14,208 |
| Text 64 | 32,258 | 40,946 | 36,644 |

This saves approximately 4,300 cycles per layered draw: 33.1% for the short
rectangle and 23.2% for Text 8. The calls still trail Kickstart. The baseline
instruction profile attributes 6,460 cycles of Text 8 to compiled Layers bridge
helpers; the recognized candidate path executes no GUI bridge instructions.

The final desktop profile and uninstrumented control agree on CPU, complete
Chip RAM, pixels, audio, cycles, frames and milestones. Pixels match `.39`:
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Guest measurement | `.39` | `.40` |
| --- | ---: | ---: |
| Cold ready | 14.583213769 s | 14.563181882 s |
| DrawChanges, Tab at field 1,000 | 41.252701 ms | 29.160717 ms |
| Key-down to DrawChanges return | 86.174527 ms | 74.082543 ms |

The eight-field input scenario is unchanged. These are guest-cycle diagnostics,
separate from host-throughput acceptance. The producer selection passes 198
tests and skips 2 optional crossover cases. Four previously documented extended
image identity failures are explicitly excluded, with their expectations unchanged.
Controls cover both CPUs, 210 lock transitions per CPU, waiter arrivals, nested
locks, published-owner fallback, caller Forbid depth and Simple/Smart clipping.
The bundled app passes 11 native graphics/text/desktop checks, including editor,
reset and cold reopen. Launcher/migration checks pass 69 cases, with 3 optional
workspace cases unavailable. Both Release solutions build; producer has 11
existing warnings, consumer has none.
ROM/media and local artifacts are not
committed or published. Exact producer image/inputs/emitter dependencies are in
`CopperStart/artifacts/graphics-layers-20261030/rom-qualified`; consumer captures
are under `artifacts/graphics-layers-20261030/qualified-{control,profile}`.
