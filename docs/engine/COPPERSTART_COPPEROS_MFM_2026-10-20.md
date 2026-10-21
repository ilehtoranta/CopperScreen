# CopperOS native MFM optimization qualification, 2026-10-20

The native MFM encoder is qualified with the existing bounded track cache,
synchronous writes, Close ordering and rollback. The ROM remains 512 KiB; the
PAL OCS MC68000 machine retains 512 KiB Chip + 512 KiB Slow, no Fast RAM and one
DD DF0. No encoder allocations, lookup tables or additional buffers are added.

The [implementation and reproduction guide](../../../CopperStart/Docs/NativeMfmEncoding.md)
describes the narrow projection. It incorporates the previously unqualified
live density-bound hoist and fills only the tail gap and sector preambles,
using even-aligned longword writes. The sector payload, checksum and clock-bit
passes are unchanged. The public transfer, cache, DMA and filesystem paths have
zero resolved-method changes relative to the qualified track-cache baseline.

The frozen Devices producer reproduces with zero resolved-IL differences before
the overlay. Exactly the four-argument EncodeTrack method changes. Other ROM
producers and graphics bytes match. The complete ROM checksum is FFFFFFFF;
code is 468,358 bytes plus its eight-byte header, leaving
23,154 bytes before graphics at 0xFF8000. The ROM SHA-256 is
`1211BF01AED8FC5CC2E979575DFA5779A4E578E2AFAEA720B852847E2398F7F9`. Only the development Desktop profile advances.

## Validation

The exact frozen-producer candidate passes 28 compiled disk/cache cases and one
encoder comparison fact containing 24 cases, with zero skips. Both encoders run
as MC68000 instructions: DD/HD, tracks 0/1/159, zero/one/pattern/random payloads.
Every output byte matches the frozen encoder, including sync, checksums, clock
carry, sector preambles and trailing padding. Input bytes, two-byte alignment,
guard regions and bytes after the encoded stream are checked. Existing disk
cases independently decode/check MFM and exercise partial writes, protection,
cache invalidation, media changes, failed DMA and allocation failure. All 28
also pass with the live compiler; the separate host clock-bit oracle passes.

| Sectors | Mean baseline encoder cycles | Mean optimized encoder cycles | Reduction |
| --- | ---: | ---: | ---: |
| 11 | 13,975,019 | 1,783,783 | 87.24% |
| 22 | 27,947,688 | 3,565,276 | 87.24% |

These are isolated fixture counts; the desktop observations below include the
rest of the save path. They do not measure spindle timing or host throughput.

The native reset/Exec DoIO replay and focused cache/media replay pass with zero
skips. All nine retained I/O facts pass: five ROM/media replays, one compiled
buffer case, two freestanding checks and one host glyph test. Editor, Save and
recovery each finish eleven lifetimes. Original-fixture cold reopens, short and
partial writes, failed Write/Close, failed replacement/restoration, protection,
full media, startup failure and restart remain covered by the retained tests.

The unchanged measurement protocol passes its 10,000-field preflight and full
paired journey. All 105,586 observed fields match the public ExecuteFrame
control in RAM, display/audio, registers and cycles; final ADF bytes match.
Both saved files reopen through native DOS and match independent FFS extraction
at 33 and 16,384 bytes. All eleven post-stop heap geometries restore. The
GUI/Home/Desktop/SaveProbe payloads match the immutable
[track-cache baseline](COPPERSTART_COPPEROS_TRACK_CACHE_2026-10-20.md).

## Comparable guest latency

| Interval | Track-cache baseline guest seconds | Optimized guest seconds |
| --- | ---: | ---: |
| First desktop launch | 155.723 | 155.723 |
| Warm desktop launches | 27.188–27.189 | 27.188–27.189 |
| Full Home paint | 11.968 | 11.968 |
| Home damage repaint | 10.831 | 10.831 |
| Text drawer navigation, 20 entries | 66.934–69.700 | 66.934–69.685 |
| Open 32-byte document | 11.866 | 11.866 |
| Open 16 KiB document | 116.909 | 116.926 |
| Replace edited 33-byte document | 310.406 | 214.138 |
| Replace edited 16 KiB document | 986.673 | 679.906 |

The same routine boundaries, workload, probe overhead and 7,093,790 Hz integer
clock are retained. Save includes source checks, physical writes, Close,
replacement and cleanup; it excludes Prepare and outside repaint. These are
individual modeled guest intervals, not physical hardware, installed response
percentiles or host FPS. Failed attempts are retained separately, not pooled.

## Heap comparison

| Observation | Baseline bytes | Optimized bytes | Difference |
| --- | ---: | ---: | ---: |
| Lowest free | 218,696 | 218,696 | +0 |
| Lowest largest block | 96,040 | 96,040 | +0 |
| Chip: lowest free | 124,040 | 124,040 | +0 |
| Chip: lowest largest block | 91,272 | 91,272 | +0 |
| Slow: lowest free | 12,840 | 12,840 | +0 |
| Slow: lowest largest block | 7,216 | 7,216 | +0 |

Managed capacity remains 1,011,648 bytes. Both
free counters are observed at every instruction with free-list validation at
transitions and allocator returns. Minima need not occur together. Four normal
lifetimes and seven startup failures per pass return the same post-stop heaps.
Broader queue pressure, fragmentation and 100-cycle sustained use remain open.

Both production solutions build with zero errors. Exact source, producer, ROM,
TRX, receipt, layout and exported-media hashes are in the
[machine-readable evidence](COPPERSTART_COPPEROS_MFM_2026-10-20.json), SHA-256 `25ED7FEF31FBB7A2DE4C65C7EC826D8BC1D008DCEF986925C30C9FB4D76558D1`.
Generated ROMs/media stay local. The projection check ties the live encoder to
the qualified candidate while retaining unrelated live decoder work outside
this comparison. Previous evidence records are unchanged.

Remaining priorities are native drawing and I/O latency, installed response,
sustained use, bounded Files mutation/paging and Shell/Demo ownership return.
Shipping admission and the app firmware pin remain separate work.
