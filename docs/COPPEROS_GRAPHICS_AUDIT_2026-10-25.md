# CopperOS graphics audit qualification — 2026-10-25

The graphics-audit.7 bundle adopts the audited current-source graphics resident.
The [producer audit](../../CopperStart/Docs/NativeGraphicsAudit20261025.md) records
the implementation, original-ROM controls, measured costs and remaining scope.
WaitBlit loses unnecessary frames, ownership transitions stop recertifying public
headers, wider copies use DMA, and BltBitMap permits final asynchronous DMA.

The exact exec.6 reset bank and redraw.5 HDF remain unchanged. The prior graphics
bank was separately frozen; the reorganized current-source bank is independently
replayed before adoption. No CPU/chipset timing or engine execution changes.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `6233a69f723beea59f24596743dbdca45fcc54ec55c7b9160b88e091bd00dab3` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The resident is 29,653 bytes. Source/assembly identities and reset-bank equality
are in `../CopperStart/artifacts/graphics-audit/build-receipt.json`. The personal
HDF remains the current redraw.5 image. App-owned workspaces acquire the new
hash-addressed ROM on launch while retaining disk, geometry, write protection
and documents. No further system-file migration or disk backup is needed.

## Verification and latency

- **19 producer tests pass**, no skips, including the local original V40.63
  ROM's real graphics/ownership vectors. The control agrees on all 16 minterms,
  constant planes, bitmap results, registers and pending final DMA. This is
  targeted call parity, not complete graphics.library parity.
- **11 embedded native tests pass**, no skips: ECS/AGA DMA pixel oracles,
  modes/alignment, Simple/Smart text, ROM-font staging, desktop/Editor input,
  reset and writable cold reopen. Pixel tests explicitly call public WaitBlit
  before inspecting asynchronous results.
- Protocol v4 diagnostic/control match at **779 fields / 110,697,462 cycles**,
  including CPU registers, Chip RAM, pixels and PCM. Graphics instruction
  symbols are unavailable and remain unattributed; kernel costs come from the
  separate bounded observer.
- Selected-tile pixels retain SHA-256
  `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
  Boot is **15.304365 guest seconds**, compared with 15.324399 for exec.6.
  Tab redraw remains **0.300476 seconds / 15 PAL fields**; no further UI redraw
  improvement is claimed by this pass.
- Production Release solution build passes with zero warnings/errors. The host
  selection records 68 passes, three unavailable optional historical replays,
  and one pause-notice race in a synthetic-ROM UI test that does not use CopperOS.
  All four notice cases pass on isolated rerun. The initial failure is retained.
- The producer solution retains its three existing Shell/DOS API build errors.
  Source tests, native controls, full replays and host UI checks are separate
  evidence. Guest-cycle results are not physical timings or throughput acceptance.

Evidence is local under `../CopperStart/artifacts/graphics-audit`, producer
Graphics.Tests TRXs and consumer TestResults. ROM/HDF assets remain ignored and
are not published.
