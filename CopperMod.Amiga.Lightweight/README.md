# CopperMod.Amiga.Lightweight

Source and development now belong to the CopperScreen repository. The desktop
app and headless runner build this project directly; Copper68k remains a shared
external package. The original package ID and namespace are retained for compatibility.

Independent PAL OCS A500 engine: Copper68k accurate 68000, 512 KiB Chip RAM,
512 KiB slow RAM, native 256 KiB Kickstart 1.3 (v34), one to four standard
880 KiB ADF/read-only IPF drives (including selected ZIP entries through the host/runner),
and optional file-backed CopperHDF units.
ROMs and game media are not distributed with this package.

Lightweight is the application's active/default engine. Legacy and CopperStart
are unavailable in the standard build. Supported input is mouse, synchronized
keyboard transport and implemented digital-controller input; output is a full
raster and 48 kHz stereo PCM. ECS/AGA, other CPU/ROM profiles, RTC, RTG, physical
IDE/SCSI controllers, preserved-track writes and save-state
compatibility are outside current product scope. Unsupported settings fail visibly.

Native Lemmings and bounded Hired Guns sessions have recorded acceptance; this is
not complete game compatibility or exhaustive hardware conformance. Residual
interlace instability and other limitations remain in the issue register.

Use the public `LightweightA500Machine` API to load a ROM, mount/eject floppy
bytes, submit input, reset and execute frames. Read `Framebuffer` and
`AudioSamples` from their reusable buffers before executing the next frame.
The engine is single-owner; marshal UI input onto its execution thread.
Host presentation, pacing and audio-device delivery remain outside this library.
Request `FramebufferWidth = 908` to retain OCS hires output. The library default
is 454, which is lowres-only; it is not the complete native display contract.

OCS dual playfield supports odd/even-plane separation, independent scrolling,
transparent color zero, PF2 palette selection, BPLCON2 playfield/sprite priorities,
lores/hires and interlaced fields. It reuses the existing DMA and shifter pipeline.
Dual-playfield HAM and priority codes 5–7 remain unsupported. CLXCON/CLXDAT now
implement playfield and sprite collisions, including CPU read-clear strobes.
See the [display contract](../docs/engine/ARCHITECTURE.md#dual-playfield-output).

Paula UART exposes owner-thread `SetSerialReceivePin` and `SerialTransmitHigh`,
with buffering, SERPER timing, TBE/RBF interrupts, overrun and break control.
`SetPaddlePosition` supplies ideal scan-period charge times; `TriggerLightPen`
captures the current beam. Physical pin phases and optional host transports are
separate from these digital models.

Set `FloppyDriveCount = 1..4` at construction (default 1). Use
`MountAdf(drive, bytes)`, `EjectAdf(drive)`, `IsDriveMounted(drive)` and
`GetDriveState(drive)` with zero-based indices. Existing unindexed APIs refer to
DF0. Disconnected or out-of-range indices are rejected. Each drive has separate
media, head position, motor/ready/change state and spindle phase; all share one
Paula receiver and DMA controller. Simultaneous selected read streams are reported
unsupported; ordinary drive switching and multi-select control/status are supported.
See the [disk contract and verification boundaries](../docs/engine/ARCHITECTURE.md#floppy-drives).

Drives start write protected. For ADF, `SetDriveWriteProtected(drive, false)` enables
guest memory-to-disk DMA into the owned encoded tracks. `ExportAdf(drive)` copies
standard sectors and rejects damaged/custom tracks that cannot be represented in
an ordinary ADF. Export and persistence are host operations, outside emulated
execution. The desktop Floppy settings expose Read-only, Save ADF and Discard and
eject. Changes stay in memory until saved; mounting from a ZIP never modifies the
archive automatically. Native Workbench format/write/read and a fresh-machine
save/reopen check have run on disposable media.

`MountIpf(drive, bytes)` decodes preserved tracks at mount time;
`LightweightIpfImage.Prepare` and `MountIpf(drive, prepared)` separate preparation
from attachment. Exact bit lengths, index orientation, stored gaps, density and
weak regions feed the causal integer receiver. IPF cannot be made writable or
exported as ADF. `GetDriveFormat` and `CanWriteDrive` expose media capabilities.
Protection compatibility is **incomplete**, including a reproduced trace-exception
failure in the pinned CPU dependency. See the [storage record](../docs/engine/STORAGE.md).

Set `Hardfiles` in `LightweightA500Configuration` before constructing the machine.
`CopperDisk.AmigaHardfileConfiguration` exposes unit/path/protection/creation size,
Auto/RDB/Partition mode and partition metadata. Configuration changes require
restart. CopperHDF preserves `copperhdf.device` and Legacy's virtual Zorro II board
identity; native OFS partition and RDB cold boots without DF0 have been exercised.
Writable HDFs update their backing files, unlike explicit-save ADFs.

The [OCS completion record](../docs/engine/OCS_COMPLETION.md) tracks current
validation, the 1% performance requirement and remaining register/physical-timing
boundaries. The measured build has performance acceptance with two explicit
exceptions; those exceptions apply only to that earlier build. The storage
candidate has its own 1% performance gate. OCS completion remains in progress. This does not certify all OCS
register combinations or external synchronization modes.

The host does not need friend access, a Legacy Bus/Scheduler or CopperStart.
Internal CPU timing hooks are shared only between the engine and Copper68k.
Unsupported behavior is reported, not delegated to another engine.

The preview package is experimental. Compatibility is narrower than a complete
Amiga emulator; documented hardware uncertainties are not closed by package
validation. Only Release builds without diagnostics may be packed.

## Development documentation

- [Product completion roadmap: ECS/AGA, CPUs, drives and storage](../docs/engine/ROADMAP.md)
- [Architecture and reference inventory](../docs/engine/ARCHITECTURE.md)
- [Current issues and regression index](../docs/engine/ISSUES.md)
- [Performance measurement guide](../docs/engine/PERFORMANCE.md)
- [Build and isolated diagnostic tests](../README.md#tests-and-headless-execution)
- [Portable native replay](../docs/NATIVE_VALIDATION.md)
- [Historical implementation and acceptance records](../docs/history/amiga/README.md)
