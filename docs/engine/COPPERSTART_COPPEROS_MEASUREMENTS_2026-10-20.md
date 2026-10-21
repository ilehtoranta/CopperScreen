# CopperOS guest latency and heap qualification, 2026-10-20

The exact development pair from [native I/O qualification](COPPERSTART_COPPEROS_IO_2026-10-20.md)
now has a completed, isolated guest latency and heap study. One full paired native
fact passes with zero skips: four normal Desktop lifetimes and seven startup
failure cases per pass. A separate 10,000-field boot parity preflight passes.
All 134,317 completed fields of the full observed journey match the public
ExecuteFrame control in Chip/Slow RAM, framebuffer, audio, D/A registers, PC, SR
and guest cycles. Final exported disks match byte-for-byte.

The immediate priority is native disk I/O latency and drawing cost before adding
desktop features. The current journey fits the bounded RAM profile, but these
operation times do not establish acceptable interactive performance.

## Exact scope and boundaries

PAL OCS MC68000, 524,288 Chip + 524,288 Slow, zero Fast, one writable DD DF0.
The ROM remains 524,288 bytes, with 467,340 code bytes, its eight-byte header,
24,172-byte gap and unchanged graphics reservation at `0xFF8000`. ROM, GUI,
CopperHome and Desktop command bytes are unchanged from the I/O record.

The [versioned protocol](../../../CopperStart/Docs/NativeCopperOsMeasurements.md)
uses the emulator's integer guest cycles at 7,093,790 cycles/second. It includes
disk rotation, seeks, scheduling and IRQs and the retained SaveProbe hooks,
code, stack and workspaces. No fixture overhead is subtracted. These are modeled
guest intervals, not physical A500 measurements, host FPS or installed UI latency.

Startup runs from the phase marker immediately before RunCommand to the first
successful Desktop Wait. The first launch includes resident module loading and
excludes earlier ROM/probe boot. Navigation covers NavigateCore; open covers
OpenCore. Save covers Execute, including source checks, writes, Close,
replacement and cleanup; it excludes Request/Prepare and outside repainting.
Draw intervals cover complete routine calls, including damage clipping. Nested
calls are not added together. Samples below are individual observations/ranges,
not a percentile or statistical release budget.

## Observed guest latency

| Interval | Samples | Modeled guest seconds |
| --- | ---: | ---: |
| First desktop launch | 1 | 155.691 |
| Warm desktop launches | 3 | 27.187–27.189 |
| Full Home paint during first startup | 1 | 11.968 |
| Home damage repaint | 1 | 10.831 |
| Text drawer navigation, 20 entries | 2 | 66.912–69.616 |
| Open original 32-byte document | 1 | 11.864 |
| Open 16 KiB document | 1 | 116.802 |
| Replace edited 33-byte document | 1 | 364.483 |
| Replace edited 16 KiB document | 1 | 1105.269 |

The saves replace existing files. Both edited payloads reopen through native DOS
and match all bytes. Independent read-only FFS extraction also verifies 33 bytes
at `SYS:Text/Mixed` and 16,384 bytes at `SYS:Text/Full` on exported media. This
adds media inspection; it is not a new cold-boot persistence replay. The earlier
original-fixture cold-boot results remain in the I/O record.

## Whole-heap observations

The passive observer starts before the first resident load, watches both free
counters at every instruction, and validates sorted Classic free lists and byte
sums at counter transitions and allocator returns. It records
9,908 counter changes and 9,912 allocator
returns. All eleven post-stop heaps restore the same totals, largest blocks and
free-chunk geometry. Both passes match those heaps exactly.

| Observation | Bytes |
| --- | ---: |
| Managed heap capacity | 1,011,648 bytes (987.9 KiB) |
| Lowest total free | 218,696 bytes (213.6 KiB) |
| Lowest largest free block across banks | 96,040 bytes (93.8 KiB) |
| Lowest Chip free / largest block | 124,040 / 91,272 |
| Lowest Slow free / largest block | 12,840 / 7,216 |
| Peak managed bytes unavailable | 792,952 bytes (774.4 KiB) |
| Peak physical RAM unavailable to the heap | 829,880 bytes (810.4 KiB) |

The total-free minimum occurs in `save-33`, lifetime 1,
at guest cycle 4,600,762,898. The table's minima need not occur together.
Physical unavailability includes RAM reserved outside MemHeaders; it is not a
host working-set measurement. Loader buffers, stacks, normal queues and all
application allocations in this instrumented journey are included. Broader
queue-pressure peaks and long-run fragmentation remain separate work.

## Implementation and validation

Only test/measurement code and documentation changed. The diagnostic driver uses
the original CPU and hardware owner and preserves the separate outer frame and
inner scalar loops, including interrupt sampling. Production execution has no
added callback. The frozen emulator/producer hashes are required by replay.

Early attempts remain failed/stopped evidence: symbol overload handling, the
incorrect Chip-only ExecBase assumption, and ABI export/body confusion were
corrected. Attempt v4 completed its control but failed parity at field 5,015:
the observer sampled interrupt recognition twice between instructions, changing
the IPL pipeline. The corrected 10,000-field preflight and full v5 run use the
same hashed sources/inputs and pass. No failed measurement is pooled into these
results and no engine behavior was changed.

CopperStart Release builds with 24 existing dependency warnings and zero errors;
CopperScreen Release builds with zero warnings/errors. The unavailable-input
guard explicitly skips one fact (zero passes); that is not native coverage.
The mandatory full and preflight profiles each run their fact with zero skips.

## Next work

1. Profile and reduce repeated native track transfers. Investigate a bounded,
   trackdisk-owned decoded-track cache and retained motor/head state, reusing
   existing workspaces where possible. Preserve stateless takeover behavior,
   media-change/inhibit/write-failure invalidation, physical write/Close ordering
   and rollback; do not weaken chipset behavior or add host filesystem shims.
2. Profile rectangle and glyph work and use bounded planar spans where justified.
   Preserve Layer clipping, backfill, refresh and pixel goldens. Repeat this exact
   latency/heap study and the affected native functional replays after changes.
3. Then finish Files mutation/paging, a Shell/Demo child that fits 1 MiB and
   ownership restoration, 100-cycle/sustained tests and installed response checks.
   Shipping pair/workspace/update and the itch.io ZIP journey remain unqualified.

## Evidence

- [Machine-readable record](COPPERSTART_COPPEROS_MEASUREMENTS_2026-10-20.json),
  SHA-256 `E33ACD51E422506544A253162D1B5BD92340BAD98EFD38C15D9442F57F9F7BE9`.
- [Full receipt](../../../CopperStart/artifacts/copperos-measurements-20261020/qualified-v5/qualification.json)
  and [native TRX](../../../CopperStart/artifacts/copperos-measurements-20261020/qualified-v5/native/measurements.trx).
- [Preflight](../../../CopperStart/artifacts/copperos-measurements-20261020/preflight-v5/preflight.json)
  and [independent audit](../../../CopperStart/artifacts/copperos-measurements-20261020/audit-v5.json).
- [Reproduction script](../../../CopperStart/build/Test-NativeCopperOsMeasurements.ps1),
  [passive driver](../../../CopperStart/tests/CopperStart.Floppy.Native.Tests/NativeDesktopMeasurementDriver.cs),
  [native journey](../../../CopperStart/tests/CopperStart.Floppy.Native.Tests/NativeCopperOsMeasurementTests.cs)
  and [audit](../../../CopperStart/build/Audit-NativeCopperOsMeasurements.py).

The record binds 549 artifact hashes and 4 current source
hashes. Generated ROMs, HUNKs, media and build outputs remain under artifacts and
must not be committed. Source versions and shipping pins are unchanged.
