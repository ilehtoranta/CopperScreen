# CopperOS Layers routing - 2026-10-31

Qualification `diskboot-2026-10-31-a1200-desktop-single-bank.44` follows
[blitter overlap](COPPEROS_OVERLAP_2026-10-31.md). It retains the frozen reset
inputs and unchanged exec-preemption.10 desktop HDF; concurrent producer
system-ROM migration remains outside this development qualification.

Layers drawing now uses direct owner-operation calls, a smaller public register
frame, sign-extending grouped ClipRect bounds reads and a register-held bitmap.
It avoids redundant visible-storage scratch writes and adding zero origins.
Private admission, lock lifetimes, queue transitions, semaphore publication,
Forbid/Permit and callback re-admission remain unchanged. No exclusion spans
drawing, and all CPU/blitter completion fences remain.

New original-ROM controls also found and corrected default Layer backfill:
EraseRect must clear all bitmap planes independently of the caller's RastPort
mask. Its private copy now selects all planes; caller state and custom hooks
retain their original mask. Checks cover signed bounds, Simple/Smart storage,
multiple clips, and alternating hidden/visible backfill message coordinates.

| MC68000 completed median guest cycles | Kickstart | `.43` | `.44` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,856 | 2,856 |
| RectFill 16x8, layered | 3,570 | 7,144 | 6,738 |
| RectFill 184x32, flat | 6,456 | 7,200 | 7,212 |
| RectFill 184x32, layered | 7,278 | 10,070 | 9,686 |

These matching-coordinate profiles retain the single visible Simple ClipRect
and zero origin. Small layered latency drops **5.7%**; large drops **3.8%**.
The small trace's routing/clipping/frame bucket falls **2,098 to 1,684 cycles
(19.7%)**, with unchanged owner-kernel code. Layer-added small-call overhead
falls 4,288 to 3,882 completed cycles. Native 68EC020 layered medians fall
7,404 to 6,968 and 10,334 to 9,926. Original-020 timing is not claimed.
Idle flat CPU timing is unchanged; the 12-cycle MC68000 flat DMA difference
includes instruction/bus phase after code relocation. No raster algorithm is
changed by this step. Zero-origin savings do not apply to nonzero translations.
Small layered RectFill remains **1.89x** original; parity is not claimed.
The separate owned-Layer protocol also reduces MC68000 Text 8 from 13,496 to
13,282 cycles (original 10,136), and Text 64 from 35,934 to 35,738 (original
32,258). These are separate call sequences from the matching-coordinate profile.

The fixed-field desktop replay retains pixel SHA
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
DrawChanges falls **26.867443 to 26.395763 ms**; key-down to its return falls
71.789269 to 71.317589 ms. Cold ready remains 14.543149995 seconds. The historical
eight-field input scenario at absolute field 1,000 is unchanged. Paired scalar
profile and normal execution agree exactly in CPU, full Chip RAM, pixels,
audio, cycles, frames, display registers and milestones. The final image was
also visually inspected; no pixel change accompanies the timing reduction.

| Bundle identity | Value |
| --- | --- |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used code/data, vectors, command bridge and footer | 520,458 bytes |
| Padding | 3,830 bytes |
| ROM SHA-256 | `FB170B81928775CD9B462AD46871ACC9D2A7D603CCD9F5254F5B35A24EACFC12` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Used bytes decrease by **20** from `.43`. Graphics totals 25,860 bytes,
including the 928-byte initializer at `$FF9860` and 24,932 contiguous bytes at
`$FF9C00`. Footer headroom is 648 bytes and checksum `$FFFFFFFF`. System routine
bodies remain in ROM. Raw compiler/reset SHA remains
`8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`.
The staged app image, embedded resource and build-time pin select these bytes.

The selected graphics run passes **205 tests**, with **2 optional crossover
benchmarks unavailable**. Four previously documented extended-image identity
failures remain explicitly excluded; their expected hashes are unchanged.
All **34 flat-call rows** match original; all **16 owned-Layer rows** match
baseline state/pixels and MC68000 also matches original. The **168 text rows**,
**20 idle/pending pipeline rows**, overlapping-memory regressions, lock/nesting,
waiter-arrival, private admission, callback and public register controls pass.
The new default-backfill regression fails against `.43` at mask 2 and passes
against `.44` and original, independently of the earlier trial's old oracle.

Both Release solutions build: producer has 13 existing warnings and no errors;
consumer has no warnings/errors. The app passes **11 native graphics/text/desktop
checks** and **69 launcher/update checks**, with **3 optional workspace cases
unavailable**. No emulator implementation or CPU package changes are included.



The exact image, map, size proof, frozen reset inputs, graphics/unchanged Layers
source, emitter dependencies and hashes are retained in producer
`artifacts/layers-profile-20261031/rom-qualified`. Instruction profiles and
original comparisons are retained beside it. App paired captures are in
`artifacts/layers-routing-20261031/qualified-{profile,control}`. These are guest
cycle diagnostics, separate from formal host-throughput acceptance. Original
68EC020 timing is not claimed. No ROM/media or artifacts are committed or published.
