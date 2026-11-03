# CopperOS blitter preparation overlap - 2026-10-31

Qualification `diskboot-2026-10-31-a1200-desktop-single-bank.43` follows
[Layers release](COPPEROS_LAYER_RELEASE_2026-10-30.md) and the
[RectFill profile](COPPEROS_RECTFILL_PROFILE_2026-10-31.md).
The same frozen reset/compiler inputs and exec-preemption.10 HDF isolate this
graphics change from concurrent producer system-ROM migration.

RectFill and shared SetRast now prepare coordinates, pens, masks, addresses and
DMA geometry while the previous asynchronous blit may still be active.
OwnBlitter remains held. WaitBlit fences precede CPU destination reads/writes
and the first custom-register write; next-plane addresses are prepared ahead
of their inter-plane wait. The final DMA plane remains asynchronous. CPU fallback,
no-effect modes, masks and register preservation remain covered. The existing
Layers implementation is unchanged by this step.

| MC68000, prior blit pending | `.42` | `.43` | Saved cycles |
| --- | ---: | ---: | ---: |
| RectFill 1x1 | 34,170 | 33,872 | 298 |
| RectFill 16x8 (word CPU) | 35,434 | 34,598 | 836 |
| RectFill 184x32 (DMA) | 39,850 | 38,378 | 1,472 |
| RectFill 16x8 (odd-plane byte fallback) | 40,690 | 38,524 | 2,166 |
| SetRast 1024x128 | 101,424 | 100,370 | 1,054 |

These are completed median guest cycles for the next call after a deliberately
pending real transfer, from protocol `raster-pending-blit-overlap-v1`: PAL
8375/OCS, 2 MiB Chip, display DMA off, IPL 7, depth 2, 3 warmups, 21 samples.
Both implementations use installed ROMs and independent expected pixels.
The pending transfer writes overlapping memory with a distinct stride/modulo;
external observation rejects register changes while it is active. All 20
idle/busy rows match pixels. Two CPU theories also cover 84 cases across draw
modes, zero masks and pixel/word/DMA/byte/SetRast paths. These diagnostics are
separate from host-throughput acceptance.

Idle word and byte timings are effectively unchanged. With no earlier transfer
to overlap, CPU preparation still has to execute. The desktop replay is pixel
identical: DrawChanges improves from **27.240164 to 26.867443 ms**, and input
to return from **72.161990 to 71.789269 ms**. Cold ready remains 14.543149995 s.
The fixed eight-field input scenario at field 1,000 is unchanged. Profile/control
CPU, full Chip RAM, pixels, audio, cycles, frames, registers and milestones agree.
Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Bundle identity | Value |
| --- | --- |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used code/data, vectors, command bridge, footer | 520,478 bytes |
| Padding | 3,810 bytes |
| ROM SHA-256 | `030939E846669A4256A85979A3639B62F0D00BB4AE7D6679880923A78AA850CA` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

The size increase is 16 bytes. Graphics uses 25,880 bytes, including its
928-byte initializer in the checked preceding gap. Footer headroom is 628 bytes;
checksum is `$FFFFFFFF`. The app's bundle and build-time hash pin select this
exact image.

The graphics selection passes **204 tests**, with **2 optional crossover
benchmarks unavailable**. Four previously documented extended-image identity
failures remain explicitly excluded, with their expected hashes unchanged.
All 34 flat-call rows match original; all 16 owned-layer rows match baseline,
and MC68000 also matches original. The 168-row text controls and the matching
flat/layer RectFill profile pass. The original installed-owned-layer protocol
remains unchanged (PAL 8375/OCS, 2 MiB Chip, display DMA off, IPL 7, 3 warmups,
21 samples); original-020 timing is not claimed. Lock, waiter, callback and
register-preservation controls continue to pass.

Both Release solutions build (producer 13 warnings, consumer none). The app
passes 11 native graphics/text/desktop checks and 69 launcher/migration checks;
3 optional workspace cases remain unavailable. Exact producer image, source,
inputs, pipeline results and original comparisons are retained in
`CopperStart/artifacts/graphics-overlap-20261031/rom-qualified`. Consumer captures
are `artifacts/graphics-overlap-20261031/qualified-{profile,control}`.
ROM/media and local artifacts are not committed or published.
