# Graphics fast-call checkpoint — 2026-10-25

The graphics-fastcalls.9 development bundle integrates the original-controlled
native Disable/Enable vectors and the current graphics optimizations. The
[producer record](../../CopperStart/Docs/NativeGraphicsFastCalls20261025.md)
contains the full 34-case comparison, controls and remaining work. The objective
to reduce slower calls remains active; this checkpoint is not full performance
or API parity with Kickstart.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `8a5c9ae0e477773acccaa2fab010f832c8e98f960450bf9fda819c3a00e38752` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

Only the two existing native Exec nesting instruction sequences enter the
frozen exec.6 reset source snapshot. Other concurrent Exec list/copy/signal work
is excluded. The compiler reports **500,968 reset code bytes**, formerly 500,958.
The graphics resident is **27,397 bytes**, with **23,490 standalone code bytes**.
HDF/GUI/Home payloads remain redraw.5. The firmware contract and build hash gate
select this ROM; app-owned workspaces keep their disk, geometry and documents.

The graphic changes retain private ownership/lifetime checks and FIFO handoff,
reduce save frames and duplicate glyph classification, pack text with real B
shifting where clipping permits, use nibble pen caches and native scalar leaves,
and select DMA for smaller fills. Wider original controls correct InitArea's
unchanged coordinate bytes and WORD wrap, SetMaxPen(0), and negative cell
advances in TextLength. CPU/chipset execution and package pins are unchanged.

## Result and verification

Protocol-v4 desktop profiler and normal ExecuteFrame control agree at
**767 fields / 108,992,243 cycles**, including CPU registers, RAM, pixels and PCM.
Cold readiness improves **15.284332 → 15.124078 guest seconds**. Tab repaint
improves **0.280445 → 0.240380 seconds**, from 14 to 12 PAL fields. Selected-tile
pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain explicitly unattributed in the desktop observer; their
call costs come from the separate installed-library matrix.

Against graphics-benchmark.8, uncontended OwnBlitter improves **7584→768 cycles**,
eight-character text **37074→16466**, 64-character text **108274→42638**, small
fill **19968→5164**, and small bitmap copy **21562→7930**. The corresponding
original costs are 62, 7912, 30480, 2270 and 2416: these gaps remain work.
WaitBlit now matches the original; several scalar/pen calls are faster.

- **32 producer tests pass**, zero skips. The 34-case matrix plus scalar,
  text, pen and installed IRQ controls match their selected original results.
- **11 bundled native tests pass**, zero skips, covering both CPU/chipset
  models, ROM fonts, long strings, masks, Simple/Smart clips and full desktop,
  Editor/input, reset and writable cold reopen. Ten isolated pixel oracles also
  pass against the exact pre-staging candidate.
- CopperScreen production Release build passes with zero warnings/errors.
  Source ownership checks 199 integration/graphics imports exactly once.
- Fresh reproduction passes all three installed comparison/control tests, and
  seven image checks pass against its independently rebuilt emitter. Its
  compiler dependency retains one existing nullable warning. The producer
  solution build still fails the three existing Shell/DOS API references
  (`LoadSegmentNative`, `UnloadSegmentNative`, `UnloadDosProcessSegment`).

Local evidence is under `../CopperStart/artifacts/graphics-kickstart-gap`,
producer/consumer TRXs and the staged ignored bundle. Binaries are not committed
or published. This is guest-cycle/compatibility evidence, not host-throughput
acceptance or a replacement for historical placement/load requirements.
