# CopperStart combined ROM and desktop prerequisites

2026-10-17: the combined writable-DOS/native-graphics producer ROM passes the
bounded native prerequisite checks. A first native desktop window is still
unavailable. The [hash-bound record](COPPERSTART_DESKTOP_PREREQUISITES_2026-10-17.json)
identifies the exact ROM, source snapshots, binary inputs, fixtures and receipts.

## Combined native artifact

The current standalone builder already composes both services from the selected
source. This work adds a repeatable qualification entry point and a native
coexistence/persistence probe, rather than joining two opaque ROM images.

| Property | Measured artifact |
| --- | --- |
| ROM | 524,288 bytes |
| SHA256 | `9E143A28EB8DEAE4E821DE43D49BDB0FB47BD415EA9710A2CC4BA28F4AF484C8` |
| Compiled guest code | 463,658 bytes, FixedPoint optimization |
| Native graphics block | 26,252 bytes, resident at `$FF8000`, 41 selected vectors |
| Linker gap before graphics | 27,854 bytes after compiled code and its eight-byte prefix allowance |
| Space after graphics | 6,496 bytes before the ROM footer |
| Native target | PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip + 512 KiB slow, one DF0 |

The free ROM ranges are separate. In particular, adding more graphics bodies at
the current resident address has only 6,496 bytes of tail space. Measure and
choose layout deliberately before expanding the drawing/display selection.

CopperStart base commit is `8b90a09096506b262756c98fd10e19e19fefde52` with the
recorded working-tree inputs. Native compilation uses frozen managed assemblies;
the corrected fixture replay checks the original producer, SDK, compiler,
Lightweight, CPU and graphics-emitter hashes before loading the ROM. Source
copies, binary inputs and intermediate/final ROMs remain under ignored artifacts.

## Verification

The final run passes **67 selected cases**, with zero failures or skips:

- **51 native execution cases** cover boot, DOS, console/input, graphics and the
  new two-boot storage/graphics journey.
- **15 filesystem model cases** check directory traversal and FFS mutation/error
  semantics independently of emulated CPU execution.
- **One build-time layout case** checks overlap rejection and the Kickstart
  checksum using the current resident boundary.

The new original native disk command keeps graphics open while creating,
writing, closing, renaming and reading `Data/Desktop Saved`. It returns to the
boot CLI before inspection. Readback lives in fixture-owned diagnostic RAM, so
inspection does not depend on an unloaded command HUNK. The first phase finishes
at 4,741 fields; those physical MFM writes are exported and mounted in a fresh
machine with write protection enabled. That second boot reads the same 25 bytes
at 936 fields, leaves the image unchanged and performs no writes. Graphics opens
and closes balance in both phases. Field endpoints describe this correctness
fixture, not startup or throughput acceptance.

The first completed run retained three fixture failures: the old graphics
address assumption, an expectation that CMD_WRITE was unsupported, and readback
from command storage after unloading. The corrected replay uses the same ROM.
CMD_INVALID now checks NoCommand; protected CMD_WRITE checks the SDK's actual
TrackDiskError.WriteProtected. No guest write implementation was changed to
satisfy an obsolete read-only expectation.

The CopperScreen production solution builds with zero warnings/errors. The final
CopperStart solution build also passes, with two existing nullable warnings in
the sibling compiler. The final fixture build has zero warnings/errors. Build
logs are retained separately from native execution evidence.

## First window findings

Both fresh native boots make real V40 OpenLibrary calls:

| Provider | Native result |
| --- | --- |
| graphics.library | Opens successfully; version 40; open count returns to zero |
| utility.library | Unavailable in this boot path |
| layers.library | Unavailable in this boot path |
| intuition.library | Unavailable in this boot path |

The emitted graphics inventory selects InitView and InitVPort, but does not
select **Text, RectFill, MakeVPort, MrgCop, LoadView, LoadRGB4 or SetRGB4**, nor the
classic sprite acquisition/movement entries. Font metrics and the separate boot
console therefore do not establish desktop drawing or a native Intuition screen.

Producer source inspection also confirms:

- The native Utility bridge belongs to the separate full Shell startup path and
  identifies as V37, while the current Layers provider contract requests V40.
  Qualify a real compatible provider; changing a version field is insufficient.
- Intuition public admission remains closed. The ROM-facing parity adapter's
  InvokeCapability computes a fingerprint; it is not a window implementation.
  Real provider/owner startup and screen/window dispatch still need integration.
- The console's input path and private input/IDCMP foundations do not establish
  the real device-to-GUI event, focus, reply and close lifetimes.

After the first native command returns, the boot-CLI footprint is:

| Bank | Free bytes | Largest free block |
| --- | ---: | ---: |
| Chip | 459,944 | 427,176 |
| Slow | 360,216 | 359,456 |

The fresh read-only boot has the same Chip values and 360,104 free slow bytes,
with the same largest slow block. These are two different command paths in fresh
machines, not a repeated-lifecycle leak measurement. They exclude the full
CopperOS Shell and a GUI. Disk loading a large library still needs enough
contiguous guest RAM for each HUNK and its live dependencies.

## Next implementation order

1. Choose a compact drawing/display graph and its ROM or disk placement using
   the measured headroom. Implement and natively verify text, rectangles,
   viewport/Copper-list construction and palette installation.
2. Complete real compatible Utility/graphics provider lifetimes and the native
   Layers/Intuition owner startup. Preserve their existing admission prerequisites.
3. Implement the selected real OpenScreen/OpenWindow path, pointer ownership and
   device/IRQ event delivery with proper wait/wake, focus and message replies.
4. Run the original first-window probe: draw a label and rectangle, respond to a
   click and typed character, refresh, close and reopen. Capture full hires output
   and resource state, then extend it to Demo/Shell launch and return.

The producer guide is `CopperStart/Docs/DesktopPrerequisites.md`; its executable
entry point is `CopperStart/build/Test-DesktopPrerequisites.ps1`. From that
repository, use a fresh output directory:

```powershell
./build/Test-DesktopPrerequisites.ps1 -OutputDirectory artifacts/desktop-prerequisites-new
```

The broader [desktop implementation plan](../COPPEROS_DESKTOP_PLAN.md) retains
the remaining CopperOS, workspace and consumer-package tasks. This record does
not qualify the full CopperOS Shell/pair, native games, an installed package,
audio fidelity or host throughput. CopperScreen's existing qualified app ROM pin
is unchanged; this is a producer development artifact, not shipping activation.
