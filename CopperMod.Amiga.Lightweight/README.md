# CopperMod.Amiga.Lightweight

Source and development now belong to the CopperScreen repository. The desktop
app and headless runner build this project directly; Copper68k remains a shared
external package. The original package ID and namespace are retained for compatibility.

The CPU dependency is the public mainline Copper68k `1.5.0` release. See
[package and consumer verification](../docs/engine/COPPER68K_MAINLINE_1_5_0.md).

Independent PAL A500 engine with OCS 8362 or ECS 8373 Denise: Copper68k accurate 68000,
configurable Agnus and Chip RAM, native 256 KiB Kickstart 1.3 (v34) or 512 KiB A500 Kickstart 3.1
(v40.63), one to four standard
880 KiB ADF/read-only IPF drives (including selected ZIP entries through the host/runner),
and optional file-backed CopperHDF units.
ROMs and game media are not distributed with this package.

The desktop validates both ROM shapes and provides the `lightweight-a500-kickstart31`
profile. The existing ROM window maps 3.1 at `$F80000` and exposes reset vectors
through the low-memory overlay. Native ROM code executes Exec and DOS services.
Workbench 3.1 floppy boot is verified on the default 68000 with 0 and 2 MiB Fast RAM;
see [boot evidence and limits](../docs/engine/KICKSTART_31_2026-09-30.md).

`LightweightA500Configuration.FastRamBytes` adds optional CPU-only Zorro II RAM:
0 (default), 512 KiB, 1, 2, 4 or 8 MiB. Native Kickstart Autoconfig assigns the
bank within `$200000–$9FFFFF`, ahead of CopperHDF in the expansion chain.
`FastRam` exposes read-only bytes and `FastRamBase` is null until configured.
CPU accesses bypass Agnus contention; chipset DMA remains in Chip RAM. Both
reset paths remove the mapping while preserving RAM contents. Size changes
require a new machine. See [implementation, package and native evidence](../docs/engine/FAST_RAM_2026-09-27.md).

`LightweightA500Configuration.CpuModel` accepts experimental `M68EC020`,
`M68020`, `M68030` and `M68040`. The 020/030 use two native clocks per motherboard
clock; 040 uses four and approximate fixed instruction timing. All use the fitted
16-bit OCS bridge; only EC020 wraps addresses at 24 bits. The default 68000 retains
its original bus path. CPU changes require a new machine.

The 040's uncached fetches now use aligned longwords and a half-line holding
register, including the integer fallback path. This corrects the original Lotus
III prompt replay; speculative prefetch, pipeline overlap and full 040 timing
remain approximate. See [the fetch correction and validation](../docs/engine/LOTUS_III_040_FETCH_2026-09-27.md).

`M68060` is accepted for diagnostic callers using an eight-clock integer policy.
It cannot boot the supported Kickstart 1.3 profile: native task frames lack the
060's 12-byte FPU state layout. FPU arithmetic and enabled MMU operation remain
unavailable. It is explicitly unavailable in the desktop. See [030/040/060
implementation and evidence](../docs/engine/CPU_OPTIONS_030_040_060_2026-09-27.md),
the [initial bridge](../docs/engine/CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md), and
[020 decoder/cache validation](../docs/engine/LOTUS_III_020_CACHE_2026-09-26.md).

`LightweightA500Configuration.AgnusModel`, `ChipRamBytes` and `SlowRamBytes`
select one of the implemented PAL memory layouts:

| Agnus model | Chip RAM | Slow RAM |
| --- | --- | --- |
| `Mos8371` (default) | 512 KiB | 0 or 512 KiB (default) |
| `Mos8372A` | 512 KiB | 0 or 512 KiB |
| `Mos8372A` | 1 MiB | 0 |
| `Mos8375Pal2M` (8375, part 318069-10) | 1 or 2 MiB | 0 |

Construction rejects other combinations. Model/size changes require a new machine.
ECS Agnus choices include widened DMA addressing, PAL chip identification,
extended blitter sizes, Copper danger access and DOFF output suppression.
`DeniseModel` selects `Mos8362` (default) or `Mos8373`. ECS display adds SuperHires,
DIWHIGH, border/genlock controls and programmable beam, blank and sync registers.
The desktop and runner preserve all SuperHires samples at an initial width of
1816 pixels. `--denise 8373` selects it in the runner; Settings > Memory provides
the separate Denise choice. Framebuffer dimensions can change after a guest mode
switch; query width/height for each completed field. Buffers are reused at a
stable geometry and reallocated only when its size changes. The host retains
dimensions and geometry with each leased frame and follows the guest cadence.
PAL is the board/oscillator selection; guest BEAMCON0 can select NTSC display
counter geometry without changing the oscillator. Native stock PAL SuperHires
(progressive/interlaced), guest-programmed NTSC SuperHires and a programmed
640x480 raster have coverage. Stock NTSC/VGA monitor launchers are absent from
the supplied media; see
[ECS implementation and verification boundaries](../docs/engine/ECS_DISPLAY_2026-10-01.md).
The [Final Fight: Enhanced native replay](../docs/engine/ECS_REQUIRED_MEDIA_2026-10-01.md)
adds bounded opening-level coverage for 2 MiB Chip RAM and ECS border blanking,
with OCS Denise and 1 MiB controls; it does not exercise SuperHires or programmable timing.
The 8375 model names a specific 2 MiB part because other 8375 variants differ.
See [implementation, native memory discovery and verification limits](../docs/engine/AGNUS_CHIP_RAM_2026-10-01.md).

The default 512 KiB layout remains CPU-mirrored through the low 2 MiB address window,
with the same Agnus bus contention. See the [memory-map correction](../docs/engine/SUPER_CARS_II_INVESTIGATION.md)
for the default A500 wiring and the changed native boot identity. Unfitted upper
banks in the 1 MiB 8375 layout read `$FFFF` and ignore writes; exact electrical
open-bus behavior is unverified.

Original Denise has no chip-ID register. Its `$DFF07C` read uses the bounded
preceding-DMA/idle bus model documented in the
[Tower Assault investigation](../docs/engine/TOWER_ASSAULT_INVESTIGATION.md).
Electrical edge cases and broader write-only readback remain unverified.

Lightweight is the application's active/default engine. Legacy and CopperStart
are unavailable in the standard build. Supported input is mouse, keyboard
startup/recovery and physical keys, and digital-controller input; output is a full
raster and 48 kHz stereo PCM. AGA, A2024 external scan conversion, additional CPU/ROM profiles, RTC, RTG, physical
IDE/SCSI controllers, preserved-track writes and save-state
compatibility are outside current product scope. Unsupported settings fail visibly.

Native Lemmings and bounded Hired Guns sessions have recorded acceptance; this is
not complete game compatibility or exhaustive hardware conformance. Residual
interlace instability and other limitations remain in the issue register.

Use the public `LightweightA500Machine` API to load a ROM, mount/eject floppy
bytes, submit input, reset and execute frames. Read `Framebuffer` and
`AudioSamples` from their reusable buffers before executing the next frame.
The engine is single-owner; marshal UI input onto its execution thread.
ECS digital genlock keys are retained in framebuffer alpha; `SetExternalBlank`
supplies a digital blank input. Physical genlock synchronization and analog
monitor behavior remain outside the model. Programmable horizontal/vertical
sync assertions are exposed for diagnostic callers.
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
Received WORDSYNC aligns DSKBYTR byte reads even without DMA. The
[CPU byte alignment correction](../docs/engine/DISK_BYTE_WORDSYNC_2026-09-24.md)
passes Alien Breed SE '92's black boot and reaches its disk-two menu; exact
physical coincident-edge timing remains unverified.

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
in the pinned CPU package; Operation Thunderbolt reaches gameplay,
but its second-disk handling remains unverified. See the
[storage record](../docs/engine/STORAGE.md) and [CPU pin](../docs/engine/CPU_TRACE.md).

Set `Hardfiles` in `LightweightA500Configuration` before constructing the machine.
`CopperDisk.AmigaHardfileConfiguration` exposes unit/path/protection/creation size,
Auto/RDB/Partition mode and partition metadata. Configuration changes require
restart. CopperHDF preserves `copperhdf.device` and Legacy's virtual Zorro II board
identity; native Kickstart 1.3 OFS partition/RDB cold boots and
[Kickstart 3.1 FFS RDB boot/write/reopen](../docs/engine/KICKSTART_31_HD_2026-09-30.md)
without DF0 have been exercised on the default 68000.
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
