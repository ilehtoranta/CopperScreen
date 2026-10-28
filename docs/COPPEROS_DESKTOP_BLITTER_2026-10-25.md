# CopperOS blitter and repaint batching — 2026-10-25

The previous development bundle still used CPU rendering for RectFill and
SetRast. Its bitmap blitter path rejected left-shifted copies, including the
desktop's `(10,12) -> (0,0), 620×232` backing-store copy. The earlier
[CPU drawing optimization](COPPEROS_DESKTOP_GUI_SPEED_2026-10-25.md) did not
establish blitter acceleration for those operations.

The current bundle adds real guest blitter programming and removes Home's
eight-row rectangle splitting. On the unchanged PAL Alice/Lisa, 68EC020,
2 MiB Chip RAM profile:

| Actual native workload | Previous guest seconds | Current guest seconds |
| --- | ---: | ---: |
| Cold desktop readiness | 26.963 | 20.773 |
| Tab selection: repaint two Home tiles | 3.486 | 1.102 |

Boot is **23.0% faster** and the interactive redraw takes **68.4% less time**.
Completed tab-selected Home pixels match exactly. These are modeled guest-work
measurements, not host throughput acceptance or physical A1200 measurements.
The CPU package, clock, hardware scheduler, contention and display geometry are
unchanged. Text composition remains CPU-rendered, and small/ineligible raster
operations retain CPU fallbacks.

## Why enabling the blitter alone was insufficient

The baseline observer sees only two BLTSIZE launches, the two planes of the
initial screen copy. The fill-only candidate adds real rectangle/plane DMA but
only reduces boot from 26.963 to 26.743 seconds. Its Tab repaint still takes
3.466 seconds. Native Layer validation, TypeOfMem boundaries and associated
critical-section/vector calls dominate much of that redraw.

Home was issuing a separate RectFill, pen update and Schedule for each eight-row
band. That repeatedly acquires the Layer and validates the same storage. One
bounded rectangle call retains clipping, ownership and DMA completion, with a
cooperative yield between rectangles. This removes the repeated work and visible
banding without changing drawing bounds or input semantics. Input IRQs remain
enabled; no extra CPU speed or unthrottled boot mode is used.

## Native changes

In CopperStart's graphics emitter:

- `NativeGraphicsRasterBodies.CompactFillDma.cs` preflights all selected planes
  before any DMA. Word-aligned Chip RAM, enabled master/blitter DMA and classic
  size/modulo limits select the hardware path. RectFill uses a disabled constant
  A channel with first/last-word masks and C/D channels for set, clear or XOR.
  Whole-word set/clear can use D alone. SetRast reuses that path. Completion is
  awaited before releasing ownership. CPU paths retain odd addresses, disabled
  DMA, small shapes and other ineligible geometry.
- `NativeGraphicsRasterBodies.CompactRaster.cs` calls those helpers after its
  existing drawing-mode/pen decisions. The helper's register-preserving return
  flags choose completed DMA or the existing CPU fallback.
- `NativeGraphicsRasterBodies.PlanarBlitDma.cs` supports non-overlapping
  left-shifted source copies in descending mode. It composes the right edge on
  the CPU, uses that preserved word as a masked first DMA word to seed the B
  shifter, then transfers the remaining words through B/C/D DMA. Single-word
  edges combine both masks. Other minterms/shapes retain the prior fallback.

These sequences follow the Commodore hardware manual's
[channel rules](https://www.amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node011B.html),
[shifts/masks](https://www.amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node011F.html)
and [arbitrary-copy direction rules](https://www.amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0121.html).
No host graphics shim or instantaneous memory operation substitutes for DMA.

In CopperOS, `HomeView.Rect` issues one inclusive rectangle and yields afterward.
The frozen Home source/compiler rebuild first reproduces the previous 41,500-byte
HUNK exactly. Only Rect changes in the candidate. Its 41,456-byte HUNK is padded
with 44 trailing zero bytes to retain the existing filesystem allocation envelope;
normal native LoadSeg/reopen replays verify that image. `C:Desktop` retains its
652-byte bootstrap and updates the fixed-length resident key for the new Home
identity. CopperGUI and all filesystem/ROM reset code remain unchanged.

## Identities and safe personal-disk update

| Artifact | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `bcf530e943a6ae083ae222b034c708b6be8f7f7d29c6dc079bbd2c9543770c80` |
| 8 MiB DOS1 HDF | `11f825cd5ec50786b9779449c1eefd1a2088a9bab502c2be326db94cbc968236` |
| Padded L:CopperHome | `527e2229fb112f400227d89f39a53d482dc2cfc0677e3ed000b3e974fbc0236a` |
| C:Desktop | `b1a3b48f376e20251290008a974c64c98034468f3de62639c02d4bbc04681f58` |
| Unchanged L:CopperGUI | `5900735263a90ac53626e6103fd6b232329667ae03d78105bc74d88e2bc085fd` |

The graphics resident grows from 28,747 to 29,587 bytes; the reset bank is
byte-identical to drawing.2. Contracts and build gates pin the new development
bundle; no package is published and no ROM/HDF enters Git.

For an app-owned workspace marked with the previous desktop seed, the host
recognizes the exact old C:Desktop and L:CopperHome payload hashes. It replaces
only their same-length data bytes on a temporary copy, preserving every other
disk byte, including file tables, allocation bitmap, dates and user documents.
Both recognized components must match before any disk write. File.Replace
atomically installs the result and retains the entire old image as
`CopperOS.before-drawing-<id>.hdf` beside the personal disk. The native image,
geometry and write-protection setting remain at their existing path.

A write-protected configuration, active writable session, custom system component
or malformed target metadata keeps its image. A cold writable launch can retry a
locked/read-only workspace. Explicit custom profiles and the old Shell workspace
are not migrated. Concurrent first use is serialized inside the app; firmware
cache creation uses non-overwriting moves and atomic repair rather than deleting
a valid destination during a race.

The actual personal workspace upgraded from the original `e113a115…` HDF to the
qualified `11f825cd…` image; its saved full backup hashes to the exact original.
A separate migration replay modifies a real ReadMe document and an unused byte,
then requires every byte outside the two system payloads to survive. The backup
is exact, reapplying is idempotent and an active writable disk defers the update.

## Validation and evidence

The external [probe](../scripts/probes/CopperOsBoot/README.md) records actual
BLTSIZE instruction launches and custom-register values. Protocol v3 adds a raw
Tab key and waits for the selected tile state, completed drawing state, empty
message queue and native task/input-worker waits. Its normal ExecuteFrame control
matches exact final fields/cycles, CPU registers, Chip RAM, framebuffer and PCM.
The final profile/control both finish at 1,092 fields / 155,175,409 cycles after
the Tab redraw. No production observer/counter is added. Unmapped trampoline work
remains explicitly unattributed rather than being called raster time.

- Native guest DMA tests execute the actual embedded graphics vectors against
  Lightweight on **68000/ECS and 68EC020/AGA**, with an independent pixel oracle,
  masks, all rectangle modes, single-word edges, SetRast, both copy-shift directions,
  80/78-byte strides, multi-row modulos, guard bytes and callee registers. They
  require real busy/completion activity, not just writes to blitter registers.
- The selected embedded desktop boot, raw-keyboard Editor, reset, writable cold
  reopen and DMA checks pass **7 tests, zero skips**. The boot bound is 1,150
  fields; UI-runtime readiness is checked at 1,200 fields.
- Host FFS update and launch/upgrade selection passes **64 tests**; three optional
  older checkpoint/workspace replays are skipped and remain unavailable coverage.
- The frozen emitter's CPU fallback selection passes **261 tests, zero skips**.
  The previously documented stale five-case text-admission theory remains excluded;
  it is not reported as passing coverage.
- The production Release solution builds with zero warnings/errors. Concurrent
  CopperPad preview.2 work requires its existing local feed/cache; their package
  SHA-512 hashes match the current lock files exactly. The locked build uses
  `RestoreAdditionalProjectSources=artifacts/hid-investigation/packages-release`
  and `RestorePackagesPath=artifacts/hid-investigation/cache-release` with absolute
  paths. No dependency version or lock hash is weakened for this graphics task.

Local evidence is under `artifacts/copperos-blitter-speed`: the frozen emitter,
native Home rebuilds/manifests, `copy-candidate-v2`, `desktop`, `reproduced-hdf`,
`baseline-tab-frozen`, `batched-profile`, `batched-control`, native/host test
outputs, `upgrade-evidence` and before/after personal-disk receipts. Initial
one-word edge and test-fixture setup failures are superseded by the passing
oracle runs. `baseline-tab-profile` is INVALID: a shared staged HDF was changed
during that run. The valid baseline uses the immutable original HDF and records
its `e113a115…` hash. No failed or unavailable coverage is relabeled.
