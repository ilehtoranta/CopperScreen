# CopperOS native track cache qualification, 2026-10-20

The bounded native track cache completes its ROM, save/Close/rollback and repeated
latency/heap qualification. The candidate retains a 512 KiB ROM and the 1 MiB
PAL OCS MC68000 profile: 512 KiB Chip + 512 KiB Slow, no Fast, one DD DF0.
There is no extra DD allocation: the entry reuses the decoded workspace and eight
bytes in the existing 576-byte private device area.

The [implementation and reproduction guide](../../../CopperStart/Docs/NativeTrackCache.md)
describes unit/track/density matching, native disk-change checks and invalidation.
Writes remain synchronous through Paula DMA, including on a cache hit. Head
position is never cached. Failures discard decoded modifications; only completed
prefixes contribute to Actual. No writes move past Close or rollback boundaries.
Direct writers returning ownership use CMD_CLEAR.

The exact frozen baseline Devices source reproduces with zero resolved-IL
differences. Only 17 cache-related members change. The overlay preserves the
baseline codec and every other ROM producer. The ROM checksum is FFFFFFFF,
code is 468,376 bytes plus its eight-byte header, with
23,136 bytes before the unchanged graphics reservation at 0xFF8000.
The candidate ROM SHA-256 is `19582AB0F0E10C512F339310148F1FA6C3179E2D8A54C759F0A5CA1E8DFFBCC2`. Shipping firmware pins remain unchanged.

## Validation

All 28 compiled MC68000 cache/device checks pass against both the audited frozen
producer and the live compiler. Coverage includes DD/HD cached writes, complete
track preservation, takeover head movement, media replacement/removal, eviction,
invalidation, allocation failure, failed cached DMA and partial prefixes.
The pin/DMA fixture is separate from spindle timing and full native replay.

A focused native cache/media fact also passes with zero skips and no halted CPU.
It checks a repeated-read hit, protection, replacement while the cache is live,
eject/reinsert, unchanged destination on failed read, and Close's cleared request
fields through real emulated CIA/Paula. Its command-marker timing remains separate
from the desktop study below.

All nine retained I/O regression facts pass with zero skips: five ROM/media
replays, one compiled-buffer test, two freestanding checks and one glyph test.
Editor, Save and recovery each complete eleven lifetimes. Short/partial/failed
writes, failed Close cases, replacement/restore failures, protection, full media,
restart and original-fixture cold reopen checks remain required. Native cold
reads preserve the original 36-, 40- and 16,384-byte payload hashes.

The unchanged [measurement protocol](../../../CopperStart/Docs/NativeCopperOsMeasurements.md)
passes its 10,000-field preflight and full paired journey. All 125,714
observed fields match the public ExecuteFrame control in RAM, display, audio,
registers and cycles; final ADFs match. All eleven post-stop heap geometries
restore, with four normal lifetimes and seven startup failure cases per pass.
Both saved files reopen through native DOS; independent FFS extraction verifies
33 and 16,384 bytes. GUI, CopperHome, Desktop and SaveProbe bytes are identical
to the [baseline study](COPPERSTART_COPPEROS_MEASUREMENTS_2026-10-20.md).

## Comparable guest latency

| Interval | Baseline guest seconds | Cached guest seconds |
| --- | ---: | ---: |
| First desktop launch | 155.691 | 155.723 |
| Warm desktop launches | 27.187–27.189 | 27.188–27.189 |
| Full Home paint | 11.968 | 11.968 |
| Home damage repaint | 10.831 | 10.831 |
| Text drawer navigation, 20 entries | 66.912–69.616 | 66.934–69.700 |
| Open 32-byte document | 11.864 | 11.866 |
| Open 16 KiB document | 116.802 | 116.909 |
| Replace edited 33-byte document | 364.483 | 310.406 |
| Replace edited 16 KiB document | 1105.269 | 986.673 |

The cache reduces the 33-byte replacement by 14.8% and the 16 KiB replacement by
10.7%, without another DD allocation. Startup, navigation, opening and painting
show no material improvement; read intervals change by less than 0.12% in this
journey. The 16 KiB replacement still takes about 16.4 modeled minutes and Home
repaint still takes 10.8 seconds. The next work remains native I/O cost and planar
drawing before additional desktop features. Encoding and motor/seek waits are
useful targets for further profiling; the unrelated live codec changes excluded
from this ROM comparison are not qualified by this record.

These are individual modeled guest intervals, including scheduling, IRQs, disk
rotation/seeks and retained probe overhead. No overhead is subtracted. Routine
boundaries and workload are unchanged. Save includes source checks, physical
writes, Close, replacement and cleanup; it excludes Prepare and outside repaint.
These observations are not host FPS, physical hardware or installed-response
percentiles. The baseline record is unchanged; failed attempts are not pooled.

## Heap comparison

| Observation | Baseline bytes | Cached bytes | Difference |
| --- | ---: | ---: | ---: |
| Lowest total free | 218,696 | 218,696 | 0 |
| Lowest largest free block across banks | 96,040 | 96,040 | 0 |
| Lowest Chip free | 124,040 | 124,040 | 0 |
| Lowest Chip largest block | 91,272 | 91,272 | 0 |
| Lowest Slow free | 12,840 | 12,840 | 0 |
| Lowest Slow largest block | 7,216 | 7,216 | 0 |

Managed capacity is 1,011,648 bytes. The observer
watches both free counters at every instruction and validates free lists at
transitions and allocator returns. Minima need not occur together. Stable
post-stop geometry covers this journey; broader queue pressure, fragmentation
and 100-cycle sustained stress remain separate work.

Both production solutions build successfully; CopperStart retains 24 existing
warnings and CopperScreen has none. Exact input, source, TRX, receipt, layout and
media hashes are in the [machine-readable evidence](COPPERSTART_COPPEROS_TRACK_CACHE_2026-10-20.json), SHA-256
`285D572CFABD2AA8100C4639464774EC55E03BB968DA3BC6852216C88FB80E9B`. Generated ROMs/media/runtime outputs remain local artifacts.
The source projection audit ties the current cache functions to the qualified
producer while leaving unrelated live codec work outside this comparison.

The remaining priorities are drawing cost and native I/O costs that still appear
in the table, followed by installed responsiveness and sustained stress. Bounded
Files paging/mutation, Shell/Demo ownership return and release/workspace admission
remain open. No shipping or package publication is authorized by this record.
