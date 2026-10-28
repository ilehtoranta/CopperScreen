# Original Kickstart graphics comparison and bundle — 2026-10-25

The graphics-benchmark.8 bundle adopts direct native attribute leaves, shared
pen-cache generation and minimal text DMA padding. Original-ROM controls correct
InitBitMap's initial flags and preserve unrelated RastPort flags in pen setters.
The [producer comparison](../../CopperStart/Docs/NativeGraphicsBenchmark20261025.md)
contains the 29-case table, versioned procedure, controls and limits.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `9b6c0a04ff1f9541be1dc95091988e26701fa37abc87c389dc7a198dbeb8c1dc` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |
| Local original V40.63 control ROM | `8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbaea447a8000c5ee` |

The 26,293-byte graphics resident replaces graphics-audit.7. The exec.6 reset
bank and redraw.5 HDF/payloads remain byte-identical. No CPU/emulator scheduling,
chipset behavior, package pin or host-OS shim changes. The firmware contract and
build hash gate select the new image. App-owned workspaces acquire that ROM on
launch while retaining HDF, geometry, write protection and documents. No disk
migration is needed.

## Measured result

The call benchmark boots both real kernels on identical 68000 PAL ECS machines
and uses their installed vectors and real Exec. It records return and completed
draw costs separately, with three warmups/21 samples. All 29 final cases match
selected state/pixels/results; 128 attribute and 45 in-bounds text controls pass.
Five flat-text overflow cases remain outside the qualified comparison. This is
targeted parity, not complete graphics.library equivalence or physical timing.

Getters improve from **422 to 56 cycles**, matching original Kickstart. Move
improves **494→100** versus original 96; SetAPen **2086→722** versus 556;
InitBitMap **524→128** versus 130; SetFont **598→360** versus 362. Eight-character
text improves **41578→37074** completed cycles, still slower than original 7910.
OwnBlitter and drawing setup remain expensive: the real Exec Disable/Enable
pair costs 6614 cycles versus 158 in the original. No Exec change is included.

Protocol-v4 A1200 Alice/Lisa/68EC020 desktop replay and normal ExecuteFrame
control agree at **777 fields / 110,413,258 cycles**, including CPU registers,
Chip RAM, framebuffer and PCM. Boot takes **15.284332 guest seconds**, formerly
15.304365. Tab selection redraw takes **0.280445 seconds / 14 PAL fields**,
formerly 0.300476 / 15 fields. Selected-tile pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics instructions remain explicitly unattributed in the desktop observer;
the installed-call benchmark supplies their timing evidence separately.

## Verification and evidence

- **24 producer tests pass**, zero skips; the reproducible comparison also
  passes from fresh dependency builds. The predecessor deliberately fails the
  new InitBitMap state comparison, with failure evidence retained.
- **11 embedded native tests pass**, zero skips, covering ECS/AGA drawing
  oracles, text/ROM fonts/clipping, desktop/Editor/input, reset and writable reopen.
- `dotnet build CopperScreen.slnx -c Release` passes with zero warnings/errors.
  The full producer solution retains three unrelated Shell/DOS API build errors.
- Source ownership evaluation checks 198 integration/graphics imports exactly
  once. Extended/provider graphics compositions retain the preceding audit's
  byte identities. Local build receipts and source/assembly hashes accompany
  `../CopperStart/artifacts/graphics-benchmark`; binaries remain ignored.

These are modeled guest-cycle and compatibility results. Host throughput,
historical performance protocols and their input/topology/load policies remain
separate; no release acceptance or publication is claimed.
