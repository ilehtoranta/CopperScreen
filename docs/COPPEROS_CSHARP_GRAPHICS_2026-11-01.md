# Production C# graphics migration — 2026-11-01

Development ROM `.54` replaces the production `BltClear`, `GetAPen`, `GetBPen`
and `GetDrMd` implementations with ordinary C# compiled by CopperSharp68k.
The generated kernels execute directly from ROM. Their former handwritten
opcode emitters are removed; native adapters and RAM post-increment stores are
compiler-generated. Further RectFill, text and ownership migration remains open.

The producer's [qualification report](../../CopperStart/Docs/NativeGraphicsCSharpProduction20261101.md)
contains the implementation, reproducible generator, full measurements and
limitations. [BltClear.cs](../../CopperStart/native/CopperStart.Graphics.Kernels/BltClear.cs)
is the actual C# drawing implementation.

The installed `compiled-raster-v1` diagnostic compares `.53`, `.54` and Kickstart
v40.63 with display DMA off, IPL 7, three warmups and 21 samples. Clear measurements
include public BltClear plus WaitBlit for all implementations. All 62 clear cases
improve; the six getter cases retain identical timing and eight-byte bodies.

| 68000 guest cycles | Previous `.53` | C# `.54` | Kickstart |
| --- | ---: | ---: | ---: |
| Each pen/mode getter | 56 | 56 | 56 |
| Clear 16 bytes | 788 | 518 | 508 |
| Clear 64 bytes | 1,748 | 1,286 | 510 |
| Clear 4 KiB | 82,386 | 65,798 | 8,796 |

The 4 KiB clear improves 20.1% on 68000 and 35.0% on EC020. It remains a CPU path;
Kickstart's blitter clear is substantially faster for large regions. No original
020 comparison or host-throughput acceptance result is claimed.

The ROM remains **524,288 bytes**, with **521,224 used**, 3,064 padding bytes and
**16 contiguous bytes before the footer**. The graphics composition grows by six
bytes; checksum `FFFFFFFF`. The reset/compiler image and desktop HDF are unchanged.
Concurrent producer graphics and system-ROM work remains outside this frozen
qualification, as documented in the manifest.

ROM SHA-256: `75A298F54B06A3A5C647F3D106FF32E7FA23C6AE0B66D7F939FA7E549E64C4EB`.
HDF SHA-256: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.

Normal and scalar A1200 replays agree on readiness, milestones, CPU state, full
Chip RAM, pixels, audio, display registers, PNG and final frame/cycle. Ready time
remains **12.0792278880542 guest seconds**. Tab interaction ends at field 1,008 /
cycle 143,238,816. Pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
No boot-time improvement or DrawChanges measurement is claimed.

Both production solutions build. The producer passes 464 compiler checks,
224 graphics checks plus three refreshed image-identity checks, and three
working-source identity checks. Five optional graphics cases remain unavailable.
The app passes 11 native desktop/DMA and 69 launcher/update checks; eight optional
host cases are unavailable. The actual built app's embedded ROM, HDF and manifest
are inspected and hashed. RectFill's eight measured medians are unchanged.

Evidence is retained under producer and consumer
`artifacts/graphics-csharp-production-20261101/`. ROM/media and artifacts remain
uncommitted; no package is published.
