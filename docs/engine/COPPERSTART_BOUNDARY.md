# CopperStart and CopperScreen boot ownership

The full Amiga system replacement belongs to CopperStart. CopperScreen needs a
bounded disk bootstrap for titles that retire the OS, in addition to its normal
native Kickstart option. [Minimal disk boot](MINIMAL_DISK_BOOT.md) implements that
boundary without depending on a sibling checkout or installing host traps.

The sibling repositories were inspected on 2026-10-04. CopperStart already owns
portable Exec, Devices, DOS compatibility, Utility, Expansion, Icon,
non-graphics Intuition/Workbench state and runtime orchestration. Its current
ROM bootstrap initializes Exec memory/allocator state and diagnostics, then
idles; it does not yet supply the application's disk boot path.

The complete former `CopperMod.Amiga.Emulator/CopperStart` subtree and
`AmigaBoot.cs` have now moved into
`CopperStart/host/CopperStart.CopperMod`: 123 files, 9,688,547 bytes. All moved
files were verified against their original bytes at the mechanical move, and the
old CopperMod source paths are removed. Subsequent consolidation has changed the
memory/pool bridge and added adapters; the shared import currently includes 131
C# sources. CopperMod imports the sources through
`CopperStart.CopperMod.projitems` into its existing emulator assembly. The shared
import preserves namespaces, internal access and hardware scheduling; it adds
no dependency to CopperScreen. Hardware, media/filesystem adapters and raster
providers remain with the CopperMod consumer.

The original minimal disk firmware remains in CopperScreen. The larger host
service graph stays outside CopperStart's portable/ROM project graph.

## Ownership and remaining consolidation

| Former CopperMod source area | Current CopperStart ownership / remaining boundary |
| --- | --- |
| `CopperMod.Amiga.Emulator/CopperStart/Exec` | Now under `host/CopperStart.CopperMod/CopperStart/Exec`; memory/pool calls use the portable cores with guest-owned allocation descriptors. Other services remain to consolidate. |
| `CopperStart/Devices`, `Dos`, `Utility`, `Expansion` beneath the emulator | Now under the host integration; audio/keyboard/input and trackdisk TD64 bridges are tested. Configured DOS mounts, filesystem provider and a packet subset are implemented and tested; connection to portable DOS routing, packet delivery and lifecycle is pending. Host file/media access remains adapter code. |
| Missing Icon host bridge | Now implemented under `host/CopperStart.CopperMod/CopperStart/Icon`, using portable string/tool-type/provider logic. Other Icon vectors remain unsupported. |
| `CopperStart/Intuition`, `Workbench` beneath the emulator | Now under the host integration; consolidate non-graphics semantics while preserving SDK/value-platform contracts. |
| `CopperStart/Graphics`, `Layers` beneath the emulator | Now under the host integration; follow CopperStart's current graphics/portable Layers boundaries. |
| `CopperStart/Runtime` and `AmigaBoot.cs` | Now under the host integration; separate portable OS initialization from outer host lifetime, scheduler and hardware adapters in subsequent implementation work. |
| Host callback dispatch and firmware gateway installation | Adapter responsibilities; no direct port to the Lightweight bus or clock. |
| Original boot-block invocation and bounded boot services | CopperScreen firmware, tested independently of CopperStart. |

The bulk source ownership move is complete. The first consolidation slice adds
the Exec memory/pool bridge and audio/keyboard/input platform adapters. The
follow-up adds Icon, trackdisk TD64 and validated DOS mount configuration. The
next slice consolidates guest message queues and adds deferred task retirement.
The configured filesystem follow-up implements SDK packet decoding, volume-local
resources, bounded reads/seeks and staged writes: 159 host tests and 66 focused
portable DOS ABI/packet/lock/Close tests pass. The host harness includes generated
native bitmap ABI calls; its configured DOS cases are host/provider checks, not
native DOS command replays. The earlier 47 message/task/DOS lifecycle,
37 portable memory and 11 trackdisk/Icon/Expansion tests passed in their slices.
The full old host still fails to compile with 79 boot/service interface
diagnostics. DOS service/context wiring, queued packet delivery and lifecycle,
task creation/context-owner binding,
native switch-completion certification, library callbacks and other interfaces
still need implementation. Formerly current task storage remains pinned without
that execution proof. Its full ROM/media tests remain unavailable coverage.

CopperStart records the historical move verification in
`Docs/CopperModHostSourceMove.md` and current slice results/limits in
`Docs/CopperModExecMemoryConsolidation.md` and
`Docs/CopperModIoAdapterConsolidation.md` and
`Docs/CopperModMessageLifetimeConsolidation.md` and
`Docs/CopperModConfiguredDosConsolidation.md`, with an executable ownership check in
`build/Test-CopperModHostSourceOwnership.ps1`.

The consuming emulator project uses development version `1.1.1-dev.1`; no
package was published. Relocating source does not qualify a generated ROM.

CopperScreen continues to reference its local Lightweight/CopperDisk projects
and the pinned public Copper68k package. CopperStart's compiler/SDK and portable
OS project graph are not added to the standard application build.
