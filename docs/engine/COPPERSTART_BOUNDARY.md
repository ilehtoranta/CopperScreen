# CopperStart and CopperScreen boot ownership

The full Amiga system replacement belongs to CopperStart. CopperScreen needs a
bounded disk bootstrap for titles that retire the OS, in addition to its normal
native Kickstart option. [Minimal disk boot](MINIMAL_DISK_BOOT.md) implements that
boundary without depending on a sibling checkout or installing host traps.

## Current product scope — 2026-10-05

Legacy boot is not required. The [earlier restoration plan](../../CopperScreen/COPPERSTART_RESTORATION.md)
is superseded. Native Kickstart and Minimal disk boot both use Lightweight;
restoring `AmigaBoot`, DOS/Workbench host services, task scheduling or their old
gateway graph is not a CopperScreen requirement.

The source ownership move is complete. The old host's compiler errors describe
an unused consumer, not unfinished CopperScreen work. Further full-OS
consolidation belongs to CopperStart. Minimal firmware additions should address
a demonstrated boot-block/loader need within its bounded contract and preserve
normal guest instruction, DMA and hardware execution.

## Source ownership and retained component evidence

### Standalone replacement-ROM follow-up — 2026-10-23

The [Nitro replay](COPPERSTART_NITRO_2026-10-23.md) adds supplied 166-track
extended-ADF boot, human-driver name/car selection and first-race acceleration
and steering. CopperDisk now preserves additional cylinders and admits empty
raw tracks; the shared Copper68k development package corrects memory BTST's IPL
sample phase. Both CopperStart and native Kickstart 1.3 reach gameplay. The ROM
is unchanged. Race completion, later tracks and multiplayer remain unqualified.

The [Lemmings replay](COPPERSTART_LEMMINGS_2026-10-23.md) adds supplied Skid
Row two-ADF boot, disk swapping, mouse-controlled Fun 1 gameplay, P pause/resume,
live digger assignment and terrain removal, 100% rescue completion and
progression to the Fun 2 briefing. Native Kickstart 1.3 uses the same physical
inputs. No ROM or engine correction was established. Later gameplay and
pristine original media remain unqualified; historical performance inputs
are unchanged.

The [Lotus III replay](COPPERSTART_LOTUS3_2026-10-23.md) adds supplied
FLT / Crack Inc two-ADF boot, disk swapping and player-controlled one-player
Arcade racing with automatic gears, steering, braking, F10 pause/resume,
a Turbo Zone and timeout through scores back to setup. Native Kickstart
1.3 needs one extra Fire pair before aligned setup; later input is shared.
No new ROM or engine correction was established. Original-media behavior,
later stages and successful race completion remain unqualified.

The [Lotus Esprit Turbo Challenge replay](COPPERSTART_LOTUS1_2026-10-23.md)
adds supplied Angels `[b doscopy]` ADF boot, options, soundtrack selection,
normal and alternative driving controls, automatic gears, braking and Escape
back to the presentation before restarting. No ROM or engine correction was
established. Original-media behavior, pause and lap/race completion remain
unqualified.

The [Major Motion replay](COPPERSTART_MAJOR_MOTION_2026-10-23.md) adds supplied
single-ADF DOS boot, one-player driving/fire, pause/resume, mission abort and
restart, and sound toggling against a clean Kickstart 1.3 reference. No ROM or
engine correction was established. Slow CopperStart loading, a separate
Kickstart 3.1 graphics failure and incomplete mission coverage remain explicit.

The [Miami Chase replay](COPPERSTART_MIAMI_CHASE_2026-10-23.md) adds supplied
mixed-release ADF disk swapping, one-player street gameplay, driving/fire,
police-stop dialogue, radar, pause/resume and return to the main menu.
No new ROM or engine correction was established. Pristine original-media
protection behavior and mission completion remain unqualified.

The [Tower Assault replay](COPPERSTART_TOWER_ASSAULT_2026-10-23.md) adds
original SPS 279 disk swapping, crash-site traversal, movement/fire,
pause/resume, PDC messages and entry into Civilian Zone 1 with indoor gameplay.
No new ROM or engine correction was established. Zone completion, later
progression and the unverified map/statistics functions remain explicit limits.

The [Alien Breed SE92 replay](COPPERSTART_ALIEN_BREED_SE92_2026-10-23.md)
adds original SPS 615 disk swapping, Deck One movement/fire, pickups, doors,
the Intex mission-objective round trip and storage-area enemy contact.
No additional ROM or engine correction was established. The same record
identifies supplied Alien Breed II SPS 44 as AGA media, unavailable for the
bounded A500 profile; it is not counted as a successful replay.

The [Overdrive replay](COPPERSTART_OVERDRIVE_2026-10-23.md) adds original
two-IPF Arcade boot, driving, pause/resume, race abort and both physical disk
swaps back to the main menu. No additional ROM or engine change was needed.
This covers the opening race controls, without a completed lap or race claim.

The [F17 Challenge replay](COPPERSTART_F17_2026-10-23.md) uses the current
standalone CopperStart ROM and the engine's
[sprite/audio DMA correction](WORMS_SPRITE_DMA_2026-10-22.md). Original IPFs
reach Disk 2 loading and the South Africa race, with acceleration, steering,
collision response, braking, pause/resume, HUD toggling and race exit checked.
No additional ROM or engine change was needed. This is bounded opening-race
compatibility, without a lap or championship completion claim.

### Standalone replacement-ROM compatibility — 2026-10-15

The separate CopperStart repository now provides a native disk-boot ROM with
bounded DOS startup and a console. Its prebuilt October 13 image was tested on
the unchanged Lightweight engine. [The first compatibility record](COPPERSTART_COMPATIBILITY_2026-10-15.json)
identifies the exact Lotus Turbo Challenge 2 disk, configuration, native Kickstart
control and initial blocker: a fixed-address loader read targets unused space in
CopperStart's bootstrap stack reservation and returns `IOERR_BADADDRESS` because
trackdisk requires its destination to belong to an Exec memory range. Removing
that allocator-range check in CopperStart fixes the read without moving the live
stack. The freshly compiled ROM passes 23 native disk-boot tests; both 14,000-field
Lotus replays reach the opening race and respond to acceleration input, with all
355 batch/scalar capture files identical. Full-game completion remains unverified.
The [single-title recorder](../../scripts/probes/CopperStart/replay.py)
preserves commands, hashes and captures; it never infers compatibility from runner
completion. This external ROM replay does not enable the old CopperStart host
integration in the standard application.

The [second compatibility record](COPPERSTART_ARTE_2026-10-16.json) covers Arte
by Sanity on the PAL OCS A500 with 512 KiB Chip and 512 KiB slow RAM. It exposed
a caller-built request length check, missing A501 discovery, reset disk-pin/MFM
state, and a split Chip MemHeader that made the demo report insufficient memory.
CopperStart fixes these in the firmware. An original boot-block control now
reads the same DDRB, ADKCON and physical Chip extent as native Kickstart 1.3.
All 83 focused/native tests pass. Both 40,000-field Arte replays reach credits,
later 3D scenes and the earlier intro sequence, with all 505 batch/scalar capture
files identical. The Lotus 2 regression preserves all 71 captured images and
its opening-race acceleration response. This remains bounded compatibility
evidence for the recorded variants and machine, not general OS qualification.

The [third compatibility record](COPPERSTART_DESERT_DREAM_2026-10-16.json) covers
Desert Dream by Kefrens on the same 512 KiB Chip + 512 KiB slow PAL A500.
Its loader reads ExecBase's physical memory limits directly; CopperStart's
unset `MaxExtMem` produces a false hardware/1 MB rejection. Firmware now reports
the detected extended-memory end and uses the native `$FFFF` static ExecBase
checksum convention. All 85 focused/native tests pass. Both 72,000-field demo
replays load disk II and reach the closing credits, with all 365 batch/scalar
capture files identical. Lotus 2 retains its opening-race response and all 71
images; Arte retains all 101 images and its later scenes/intro loop. The record
discloses Arte's small sampled-PCM difference and the distinct no-slow-RAM
allocator reservations. Secret parts and audio fidelity remain unverified.

The sibling repositories were inspected on 2026-10-04. CopperStart already owned
portable Exec, Devices, DOS compatibility, Utility, Expansion, Icon,
non-graphics Intuition/Workbench state and runtime orchestration. At that
inspection, its ROM bootstrap initialized Exec memory/allocator state and
diagnostics, then idled; it did not yet supply a disk boot path.

The complete former `CopperMod.Amiga.Emulator/CopperStart` subtree and
`AmigaBoot.cs` have now moved into
`CopperStart/host/CopperStart.CopperMod`: 123 files, 9,688,547 bytes. All moved
files were verified against their original bytes at the mechanical move, and the
old CopperMod source paths are removed. Subsequent consolidation has changed the
memory/pool bridge and added adapters; the shared import currently includes 133
C# sources. CopperMod imports the sources through
`CopperStart.CopperMod.projitems` into its existing emulator assembly. The shared
import preserves namespaces, internal access and hardware scheduling; it adds
no dependency to CopperScreen. Hardware, media/filesystem adapters and raster
providers remain with the CopperMod consumer.

The original minimal disk firmware remains in CopperScreen. The larger host
service graph stays outside CopperStart's portable/ROM project graph.

## Retained replacement-OS source areas

| Former CopperMod source area | CopperStart ownership / retained scope |
| --- | --- |
| `CopperMod.Amiga.Emulator/CopperStart/Exec` | Now under `host/CopperStart.CopperMod/CopperStart/Exec`; memory/pool calls use the portable cores with guest-owned allocation descriptors. Other services remain to consolidate. |
| `CopperStart/Devices`, `Dos`, `Utility`, `Expansion` beneath the emulator | Now under the host integration; audio/keyboard/input and trackdisk TD64 bridges are tested. Configured DOS handlers, queued/public packet calls and Process metadata are tested as separate components. The full portable routing/lifecycle graph remains unqualified. Host file/media access remains adapter code. |
| Missing Icon host bridge | Now implemented under `host/CopperStart.CopperMod/CopperStart/Icon`, using portable string/tool-type/provider logic. Other Icon vectors remain unsupported. |
| `CopperStart/Intuition`, `Workbench` beneath the emulator | Now under the host integration; consolidate non-graphics semantics while preserving SDK/value-platform contracts. |
| `CopperStart/Graphics`, `Layers` beneath the emulator | Now under the host integration; follow CopperStart's current graphics/portable Layers boundaries. |
| `CopperStart/Runtime` and `AmigaBoot.cs` | Retained under the host integration. Legacy boot restoration is outside the current product task. |
| Host callback dispatch and firmware gateway installation | Adapter responsibilities; no direct port to the Lightweight bus or clock. |
| Original boot-block invocation and bounded boot services | CopperScreen firmware, tested independently of CopperStart. |

The bulk source ownership move is complete. The first consolidation slice adds
the Exec memory/pool bridge and audio/keyboard/input platform adapters. The
follow-up adds Icon, trackdisk TD64 and validated DOS mount configuration. The
message/lifetime follow-up consolidates guest queues and adds deferred task retirement.
The configured filesystem follow-up implements SDK packet decoding, volume-local
resources, bounded reads/seeks and staged writes: 159 host tests and 66 focused
portable DOS ABI/packet/lock/Close tests pass. The host harness includes generated
native bitmap ABI calls; its configured DOS cases are host/provider checks, not
native DOS command replays. The earlier 47 message/task/DOS lifecycle,
37 portable memory and 11 trackdisk/Icon/Expansion tests passed in their slices.
Subsequent DOS queue, public packet-call and Process-metadata slices bring the
separate host component suite to 224 passing tests with zero failures/skips.
The unused full host still has 74 boot/service interface errors. Its DOS context,
task creation, switch certification and library callbacks remain unqualified;
they are retained limitations of that consumer, not CopperScreen work items.
Formerly current task storage remains pinned without execution-release proof.
Its full ROM/media tests remain unavailable coverage.

CopperStart records the historical move verification in
`Docs/CopperModHostSourceMove.md` and current slice results/limits in
`Docs/CopperModExecMemoryConsolidation.md` and
`Docs/CopperModIoAdapterConsolidation.md` and
`Docs/CopperModMessageLifetimeConsolidation.md` and
`Docs/CopperModConfiguredDosConsolidation.md`,
`Docs/CopperModDosQueueConsolidation.md`, `Docs/CopperModDosCallConsolidation.md`,
`Docs/CopperModDosProcessPacketBinding.md` and the current scope in
`Docs/CopperModHostScope.md`, with an executable ownership check in
`build/Test-CopperModHostSourceOwnership.ps1`.

The consuming emulator project uses development version `1.1.1-dev.1`; no
package was published. Relocating source does not qualify a generated ROM.

CopperScreen continues to reference its local Lightweight/CopperDisk projects
and the pinned public Copper68k package. CopperStart's compiler/SDK and portable
OS project graph are not added to the standard application build.
