# CopperMod.Amiga.Lightweight

Source and development now belong to the CopperScreen repository. The desktop
app and headless runner build this project directly; Copper68k remains a shared
external package. The original package ID and namespace are retained for compatibility.

Independent PAL OCS A500 engine: Copper68k accurate 68000, 512 KiB Chip RAM,
512 KiB slow RAM, native 256 KiB Kickstart 1.3 (v34), one to four standard
880 KiB ADF/read-only IPF drives (including selected ZIP entries through the host/runner),
and optional file-backed CopperHDF units.
ROMs and game media are not distributed with this package.

The fitted 512 KiB chip RAM is CPU-mirrored through the low 2 MiB address window,
with the same Agnus bus contention. See the [memory-map correction](../docs/engine/SUPER_CARS_II_INVESTIGATION.md)
for the default A500 wiring and the changed native boot identity.

Lightweight is the application's active/default engine. Legacy and CopperStart
are unavailable in the standard build. Supported input is mouse, keyboard
startup/recovery and physical keys, and digital-controller input; output is a full
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
ERSY without an external HSYNC source now holds the beam while CPU/device time
continues. `BeamSyncRunning` exposes that state. During lost sync, output delivery
is bounded and retains the last complete image without generating a hardware
VSYNC. PCM consumers need capacity for 1,024 stereo samples and must use the
actual returned length. External genlock and beam-counter writes beyond LOF
remain open; see the [beam/synchronization record](../docs/engine/BEAM_SYNC.md).
Request `FramebufferWidth = 908` to retain OCS hires output. The library default
is 454, which is lowres-only; it is not the complete native display contract.

OCS dual playfield supports odd/even-plane separation, independent scrolling,
transparent color zero, PF2 palette selection, BPLCON2 playfield/sprite priorities,
lores/hires and interlaced fields. It reuses the existing DMA and shifter pipeline.
Dual-playfield HAM and priority codes 5–7 are modeled, including selected-field
colour blanking without changing raw opacity. Low-plane HAM uses palette codes;
BPU=7 fetches four lores planes while retaining the six physical display latches.
Hires BPU above four disables bitplanes. Nonstandard line-mode widths remain
rejected; see the [evidence and limits](../docs/engine/NONSTANDARD_OCS.md). CLXCON/CLXDAT
implement playfield and sprite collisions, including CPU read-clear strobes.
See the [display contract](../docs/engine/ARCHITECTURE.md#dual-playfield-output).

Paula UART exposes owner-thread `SetSerialReceivePin` and `SerialTransmitHigh`,
with buffering, SERPER timing, TBE/RBF interrupts, overrun and break control.
`SetPaddlePosition` supplies ideal scan-period charge times; `TriggerLightPen`
captures the current beam. Physical pin phases and optional host transports are
separate from these digital models.

`SetKeyState(key, down)` handles physical key repeat, Caps Lock and the A500
Ctrl–Amiga–Amiga reset chord. `SubmitKey` remains a raw-byte replay interface.
ROM cold boot includes keyboard synchronization and FD/held-key/FE startup;
lost acknowledgments recover with F9 and retransmission. CIA serial output,
CNT timer modes and PB6/PB7 timer outputs are implemented. Owner-thread
`SetCiaBSerialPins`, `SetParallelDataPins` and `SetParallelAcknowledgePin` supply
external digital levels. Physical MCU/reset and CIA pipeline accuracy remain
bounded; see [keyboard/CIA contracts and evidence](../docs/engine/KEYBOARD_CIA.md).

Set `FloppyDriveCount = 1..4` at construction (default 1). Use
`MountAdf(drive, bytes)`, `EjectAdf(drive)`, `IsDriveMounted(drive)` and
`GetDriveState(drive)` with zero-based indices. Existing unindexed APIs refer to
DF0. Disconnected or out-of-range indices are rejected. Each drive has separate
media, head position, motor/ready/change state and spindle phase; all share one
Paula receiver and DMA controller. Simultaneous selected read streams are reported
unsupported; ordinary drive switching and multi-select control/status are supported.
See the [disk contract and verification boundaries](../docs/engine/ARCHITECTURE.md#floppy-drives).

DSKBYTR DMA status follows the active transfer, including sync wait and write
serialization. Zero-length WORDSYNC reads complete at the qualifying match without
RAM transfer. See [disk control edges and limits](../docs/engine/DISK_CONTROL_EDGES.md).
Running reads preserve partial and buffered words across WORDSYNC enable changes;
changes during the initial sync wait remain unsupported. See
[live WORDSYNC evidence](../docs/engine/DISK_LIVE_WORDSYNC.md).

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
Protection compatibility is **incomplete**. The trace-exception defect is repaired
in the pinned development CPU package; Operation Thunderbolt reaches gameplay,
but its second-disk handling remains unverified. See the
[storage record](../docs/engine/STORAGE.md) and [CPU pin](../docs/engine/CPU_TRACE.md).

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
and optimization candidates have separately recorded owner acceptance of unresolved
measurement gates, not measured performance passes. OCS completion remains in progress. This does not certify all OCS
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
