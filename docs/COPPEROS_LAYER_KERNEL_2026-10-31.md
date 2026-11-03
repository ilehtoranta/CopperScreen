# CopperOS Layers owner kernel - 2026-10-31

Qualification `diskboot-2026-10-31-a1200-desktop-single-bank.45` follows
[Layers routing](COPPEROS_LAYER_ROUTING_2026-10-31.md). It uses the unchanged
exec-preemption.10 desktop HDF and frozen `.44` reset/compiler and graphics
composition inputs. Separate working-source color-map/backfill changes and
whole-system ROM migration are outside this development qualification.

The native Layers owner now saves five registers instead of eight. It retains
incoming Layer/continuation in D5/D6, and results/issued handles in D2/D3/A2
across public Exec. Incoming held root/descriptor are retained before Forbid.
A4, already saved in the frame and preserved by Exec, switches A6 without an
additional stack push/pop for each exclusion call. The public/private ABI is
unchanged: D5-D6/A2-A6 survive and immediate Lock still returns its admitted
root/descriptor; shared/resumed acquisitions issue no handle. A nearby held
readmission trampoline gives valid conditional branches byte displacements.

All root/descriptor admission and lifetime checks, queue/task/owner/nest/grant
predicates, late waiter reads, public semaphore write order and Forbid/Permit
transactions remain. Callback release still discards its issued handle and
re-admits. No exclusion spans drawing. Raster code, CPU/blitter fences and
asynchronous final DMA behavior are unchanged.

| MC68000 completed median guest cycles | Kickstart | `.44` | `.45` |
| --- | ---: | ---: | ---: |
| RectFill 16x8, flat | 2,362 | 2,856 | 2,856 |
| RectFill 16x8, layered | 3,570 | 6,738 | 6,520 |
| RectFill 184x32, flat | 6,456 | 7,212 | 7,200 |
| RectFill 184x32, layered | 7,278 | 9,686 | 9,570 |

Small layered latency falls **3.2%**, while its kernel bucket falls **2,038 to
1,832 cycles (10.1%)**. The large return trace's kernel bucket falls 2,212 to
1,948 cycles; completed latency falls only 1.2% because CPU work overlaps DMA
and the final completion tail changes. Return-cycle reductions must not be
reported as completed-blit savings. Small layered RectFill still takes **1.83x**
original; parity is not claimed. Native 68EC020 layered completed medians fall
6,968 to 6,746 and 9,926 to 9,712. Original-020 timing is not claimed. Small flat
CPU timing is identical; small flat DMA differences include bus/instruction phase.

These matching-coordinate profiles retain `rectfill-flat-layer-profile-v1`:
PAL 8375/OCS, 2 MiB Chip, display DMA off, IPL 7, 3 warmups, 21 samples, single
visible Simple ClipRect with zero origin. This is guest-cycle diagnostic evidence,
separate from formal host-throughput acceptance. The original v40.63 ROM SHA
is `8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.
The label tool independently regenerates and verifies every kernel byte against
the ROM before attributing cycles; all native return attribution sums exactly.

Paired scalar-profile and normal desktop runs agree exactly in CPU, full Chip
RAM, pixels, audio, cycles, frames, display registers and milestones. Pixel SHA
remains `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
DrawChanges falls **26.395763 to 26.126513 ms**; key-down to its return falls
71.317589 to 71.048339 ms. Cold ready remains 14.543149995 seconds. The fixed
eight-field input sequence and absolute field 1,000 alignment remain unchanged.

| Image identity | Value |
| --- | --- |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used code/data, vectors, command bridge and footer | 520,446 bytes |
| Padding | 3,842 bytes |
| ROM SHA-256 | `0F86F2E88806F830B970BE398C5C028B291DCBCEC940BC16B96C07F3BE1B0E78` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Used bytes decrease by 12. The owner kernel is 530 bytes (previously 542).
Graphics totals 25,848 bytes, including the 928-byte initializer at `$FF9860`
and 24,920 contiguous bytes at `$FF9C00`. Footer headroom is 660 bytes, checksum
`$FFFFFFFF`. Raw compiler/reset SHA remains
`8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`.
The staged app image and build-time pin select these exact bytes. No executable
code is installed into RAM by this change.

The frozen selection passes **206 graphics tests**, with **2 optional crossover
benchmarks unavailable**. The **34 flat-call rows** match original; **16 owned
Layer rows** match baseline state/pixels and MC68000 also matches original.
The **168 text** and **20 idle/pending pipeline rows**, overlapping-memory,
private admission, nesting/owner, FIFO arrival/publication, callback/lifetime,
Forbid-depth and register controls pass. The four previously documented extended
image identity cases remain excluded with unchanged expected hashes. The three
separate color-map cases are outside this frozen image's scope, as described below.

Both Release solutions build (producer 13 existing warnings, consumer none,
zero errors). The app passes **11 native graphics/desktop** and **69 launcher/
update checks**, with three optional workspace cases unavailable. The separate
normal working-source image passes its **three identity** and **three color-map**
checks. No emulator implementation or CPU package change is included.


The comparison explicitly extracts and verifies `.44`'s actual old ROM kernel
for baseline private-ABI tests. A changed emitter must not silently replace
that baseline with the published slow bridge. A new patched-Exec regression
clobbers D0/D1/A0/A1 after both Forbid and Permit; input, result, root/descriptor
and caller-register preservation, held release and shared fallback all pass.

Frozen qualification uses `Freeze.targets` to select `.44` composition and
Layers platform source, plus matching frozen image identity expectations.
Normal working-source identity expectations separately include the pre-existing
color-map composition work; three identity and three color-map tests pass on
that distinct working image. The three color-map cases are excluded from the
`.45` frozen selection because that feature is not part of its image. An initial
broad selection that hit this scope mismatch was aborted and retained separately.
No feature-source changes were reverted. Four known extended-image identity
expectations remain unchanged and excluded as previously documented.

Exact image/map/size proof, frozen source overlays, emitter dependencies,
source/diagnostic hashes, old raw kernel, per-label profiles, original comparisons
and logs are retained in producer `artifacts/layers-kernel-20261031/rom-qualified`
and its parent. Desktop paired captures are in consumer
`artifacts/layers-kernel-20261031/qualified-{profile,control}`. ROMs/media and
local artifacts are not committed or published.
