# Lightweight A500 architecture

Lightweight is CopperScreen's active engine. Legacy and CopperStart are unavailable
in the standard application. The old G6/G7 cutover and completed Lightweight H-stage
procedures are [historical records](../history/amiga/README.md), not prerequisites
for continued development.

The host also offers [Minimal disk boot](MINIMAL_DISK_BOOT.md), an original guest
bootstrap on the same machine/CPU path. It does not enable CopperStart, add host
OS traps or change chipset execution ownership.

Start with the [engine API and supported profile](../../CopperMod.Amiga.Lightweight/README.md).
Use the [issue register](ISSUES.md) for limitations, [performance guide](PERFORMANCE.md)
for measurements, and [root README](../../README.md) for builds and tests.

## Ownership and execution boundary

This repository owns the application, Lightweight engine, CopperDisk, runner,
workloads and tests. Copper68k is a pinned external NuGet dependency maintained in
CopperMod. Preserve its package boundary; do not reference a sibling checkout.
See [product ownership](../PRODUCT_OWNERSHIP.md).

`LightweightA500Machine` owns the memory map, register semantics, devices and one
canonical integer CPU-cycle clock. Copper68k reaches it through the direct CPU/bus
boundary. Compact device state shares that clock; host UI, pacing and audio-device
delivery are outside emulated hardware ownership. There is no Legacy scheduler,
requester graph or parallel device timeline to synchronize.

The default 68000 reaches the machine directly. Experimental 68EC020/68020/68030/68040 use
[LightweightAcceleratorBus](../../CopperMod.Amiga.Lightweight/LightweightAcceleratorBus.cs)
under the versioned `ocs-accelerator-v1` policy. Copper68k converts two native CPU
clocks for 020/030 or four for 040 to one motherboard clock; the adapter must not convert that clock again.
Aligned long operands use two 16-bit transfers; odd long operands use byte, word,
byte transfers. Each transfer retains at least four motherboard clocks including
its address/data phases, with existing contention or CIA synchronization allowed
to extend it. All device progress remains owned by the machine.

The initial PAL A1200 uses an explicit `CreateA1200Ec020` package profile and
`a1200-initial-v1` host bridge. An aligned Chip/ROM longword occupies one accepted
motherboard transfer; custom registers stay 16-bit and CIA/Gayle byte-wide.
Unaligned operands split through the existing bus adapter. Alice bitplane DMA
retains its accepted address and FMODE until the output phase samples RAM,
then transfers 16/32/64 bits to Lisa. FMODE does not cancel a separately pending
BPLCON0 update. CPU/cache and motherboard timing remain bounded approximations.
See [initial AGA implementation and native evidence](AGA_INITIAL_2026-10-01.md).
Lisa's [HAM and dual-playfield compositor](AGA_HAM_DUAL_2026-10-01.md) uses the
same accepted-data shifters. HAM8 replaces a component's high six bits and holds
its low two; HAM6 expands a modified nibble while retaining RGB24 direct colours.
Sprites overlay the result without entering the hold. A 256-entry dual-playfield
table maps raw odd/even-plane codes to selected palette address and the masking
threshold of both opaque fields. BPLCON2 priority and BPLCON3 PF2OF writes update
that table at the existing control deadline. BPLCON4 XOR follows playfield selection.
Lisa [sprite composition](AGA_SPRITES_2026-10-01.md) samples every 35 ns raster
position independently of the playfield resolution. Comparator-latched data,
width and sample step produce 16/32/64-bit sprites at 140/70/35 ns; palette banks
remain live controls. Attached pairs always select the odd bank. Alice captures
sprite addresses and FMODE at the existing address-input phase, samples RAM at
output, advances by the selected aligned stride and delivers the payload through
the existing Lisa input deadline. CLXCON2 expands the raw collision lookup to
eight planes; a CLXCON write resets the extension at that same input phase.
Border sprites respect ECSENA and border blanking. No additional device timeline
or steady Release allocation is added. Active resolution/fetch transitions and
physical sub-CCK phases remain unverified; the retained raster phase is +1 lores pixel.

Lisa [palette readback and Alice scan doubling](AGA_READBACK_SCAN2_2026-10-02.md)
share these owners and deadlines. RDRAM reads the BANK/LOCT-selected palette half
and suppresses colour writes, including Copper writes. BSCAN2 applies BPL1MOD
to every plane when DIWSTRT and the live vertical beam have matching parity;
otherwise every plane uses BPL2MOD. A terminal fetch group crossing horizontal
sync uses the beam at its modulo phase. SSCAN2 suppresses alternate active data
slots only for sprites with SH10 set, retaining the full Lisa payload. Control
reloads still run; the global bit removes SH10 from Lisa's horizontal comparison.
Accepted transfers remain accepted across subsequent control changes. Focused
phase/row-wrap checks preserve this bounded ordering contract, without certifying
physical transition timing. No raster frequency or output clock is changed.

EC020 masks each transfer to 24 bits. A 68020/030/040 address above `$FFFFFF` returns
all-one data or ignores a write while advancing time; it never aliases a device.
This is an explicit unmapped-address policy, not accelerator autoconfiguration or
a physical bus-error model. Cache code reads use side-effect-free RAM/ROM peeks;
they do not strobe CIA/custom registers. Cache, exception and instruction timing
remain bounded package policies, not accelerator-card certification. See
[CPU options](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md) for coverage and limits.
The 020/030/040 instruction cache includes Chip RAM when enabled; guest code writes
remain stale in that cache until guest invalidation. See the independent probe
and [native cache correction](LOTUS_III_020_CACHE_2026-09-26.md).
040 uses CACR bits 15/31 and CINV/CPUSH; cache geometry and write-back behavior
remain approximate. The diagnostic-only 060 has an eight-clock policy, distinct
integer exceptions and a single supervisor stack. It requires 060-aware OS/FPU
task frames; see [implementation and limitations](CPU_OPTIONS_030_040_060_2026-09-27.md).

Absent OCS DENISEID readback retains one completed DMA word. Actual DMA
transfers update it at output; untimed memory inspection does not. Readback
consults the devices' existing completion histories, with no duplicate global
timestamp update; see the [latch optimization](DMA_LATCH_OPTIMIZATION_2026-09-23.md). The
preceding-CCK/idle policy is a bounded digital model, with physical uncertainties
listed in the [Tower Assault record](TOWER_ASSAULT_INVESTIGATION.md).

DMA RAM helpers use direct big-endian word access after masking to an even offset
and checking the fitted bank. Validated layouts guarantee both bytes are present;
unfitted banks return `$FFFF` and ignore writes. ECS 512 KiB Chip/512 KiB slow
wiring permits DMA A19 to select the slow bank. No observer runs between word
bytes and the accepted output phase is unchanged. The original fixed-size proof
is historical [word-access evidence](DMA_WORD_ACCESS_2026-09-23.md); current bank,
boundary and upper-address coverage is in the [Agnus record](AGNUS_CHIP_RAM_2026-10-01.md).
CPU memory decoding is separate. Device pointer masks are fixed at construction,
without per-transfer model selection or added execution diagnostics.

Optional Zorro II Fast RAM is CPU-only storage in the `$200000–$9FFFFF` expansion
window. One Autoconfig chain exposes the memory board before CopperHDF; native
Kickstart assigns its address and links it into Exec's memory list. The virtual
board decodes 64 KiB assignments that fit its complete bank in that window,
including Kickstart 1.3's 4 MiB assignment at `$200000`. It remains a 16-bit
expansion, including on the 020 bridge. Fast RAM adds no Agnus wait; CPU transfer
clocks and the single machine clock continue normally. Code peeks and CopperHDF
contiguous guest buffers include the configured bank. DMA helpers are unchanged.
Reset removes assignments, preserves bytes and reopens Autoconfig. No Zorro III,
32-bit accelerator-local memory or physical board timing is claimed. See the
[Fast RAM validation](FAST_RAM_2026-09-27.md).

Private register and palette arrays retain their original storage and expose
checked spans with the same constant sizes used at allocation. Their readonly
references must never be replaced or exposed; this is the proof that permits
constant-bound access. Paired accesses reuse the view, not hardware values across
device updates. See the [fixed-bounds record](FIXED_BOUNDS_2026-09-24.md).

The current optimization candidate scopes `SkipLocalsInit` to device dispatch,
video step and lores-pair rendering. These methods and their inlined paths assign
scalar locals before use and contain no uninitialized stack storage. Changes to
those paths must preserve that obligation. Device execution order is unchanged;
the [scoped-local record](SCOPED_LOCALS_2026-09-24.md) separates generated-code
evidence, correctness and the passing performance comparison.

| Area | Implementation entry point | Contract |
| --- | --- | --- |
| Machine and registers | [LightweightA500Machine](../../CopperMod.Amiga.Lightweight/LightweightA500Machine.cs), [LightweightRegisters](../../CopperMod.Amiga.Lightweight/LightweightRegisters.cs) | CPU and Copper use shared register storage and side effects, including read strobes. Debug peeks must not become hardware accesses. |
| Time and CPU bus | [LightweightClock](../../CopperMod.Amiga.Lightweight/LightweightClock.cs), [LightweightCpuBoundary](../../CopperMod.Amiga.Lightweight/LightweightCpuBoundary.cs), [LightweightBusArbiter](../../CopperMod.Amiga.Lightweight/LightweightBusArbiter.cs) | CPU waits, device grants, memory visibility and interrupt retirement use the same physical timeline. |
| Copper and blitter | [LightweightCopper](../../CopperMod.Amiga.Lightweight/LightweightCopper.cs), [LightweightBlitter](../../CopperMod.Amiga.Lightweight/LightweightBlitter.cs) | Preserve accepted input/output phases, contention, register semantics and completion ordering. |
| Display and sprites | [LightweightBitplanes](../../CopperMod.Amiga.Lightweight/LightweightBitplanes.cs), [LightweightVideo](../../CopperMod.Amiga.Lightweight/LightweightVideo.cs), [LightweightSpriteDma](../../CopperMod.Amiga.Lightweight/LightweightSpriteDma.cs), [LightweightSprites](../../CopperMod.Amiga.Lightweight/LightweightSprites.cs) | Direct OCS raster generation retains fetches, delayed shifter/composition state and full output. |
| Audio | [LightweightPaulaAudio](../../CopperMod.Amiga.Lightweight/LightweightPaulaAudio.cs), [PCM output](../../CopperMod.Amiga.Lightweight/LightweightPaulaAudio.Output.cs) | Manual/DMA channels and modulation produce real 48 kHz stereo PCM from canonical-clock state. |
| Disk | [LightweightFloppyDrive](../../CopperMod.Amiga.Lightweight/LightweightFloppyDrive.cs), [LightweightDiskSerial](../../CopperMod.Amiga.Lightweight/LightweightDiskSerial.cs), [LightweightDiskDma](../../CopperMod.Amiga.Lightweight/LightweightDiskDma.cs) | Standard ADF encoding occurs at mount; rotation, receiver state and RAM transfers remain distinct within one clock. |
| CIA and input | [LightweightCia](../../CopperMod.Amiga.Lightweight/LightweightCia.cs), [LightweightKeyboard](../../CopperMod.Amiga.Lightweight/LightweightKeyboard.cs), [LightweightControllers](../../CopperMod.Amiga.Lightweight/LightweightControllers.cs) | Batched timers, TOD, held IRQ, serial/CNT/port pins, keyboard startup/recovery and physical keys retain their documented model boundaries. |

Area blits publish enabled-channel pointers including the final row modulo before
completion. A later `BLTSIZE` may continue without pointer reload; see the
[hardware basis and regression evidence](BLITTER_FINAL_MODULO.md).
CPU/Copper writes to area-blitter pointer halves also update the running pointers;
already accepted bus outputs keep their captured addresses and values. Live
control/modulo/data writes, line-mode reprogramming and active BLTSIZE restart
remain unverified. See the [Desert Dream correction and limits](DESERT_DREAM_LIVE_POINTERS_2026-09-25.md).

## Clock and state invariants

Keyboard startup/recovery, physical key state, A500 reset, CIA serial/CNT modes
and timer-port/pin interfaces are specified in [KEYBOARD_CIA.md](KEYBOARD_CIA.md).
Idle CIA timers retain arithmetic advancement; active pin edges share the existing
interface deadline. No MCU execution loop or additional per-CCK polling is used.

The PAL model has two CPU cycles per color clock (CCK), 454 CPU cycles per line,
and 312/313-line fields. Reset selects the long field; field selection and LACE
behavior use the same beam state. CPU accesses may stop on either CPU-cycle phase.
The clock completes the partial CCK before continuing steady CCK advancement.

The CPU uses Copper68k's existing `IM68000BusCycleTiming` contract. An Agnus
transfer completes after one CCK, while the next CPU address phase remains
separated by the 68000's four-clock bus cycle. Data readiness and next-bus
availability are distinct; longword accesses retain their two word grants.
See the [Lotus III bus-timing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md)
for the failure-before regression and remaining prefetch-order limitations.

ERSY without an external HSYNC holds H0 and V at the line boundary; canonical
CPU/device time continues. Accepted outputs finish, future Agnus DMA/control
steps pause, and fixed-slot requests resume from an integer beam-phase offset.
Bounded host output during lost sync is distinct from actual VSYNC/TOD/VERTB.
See [BEAM_SYNC.md](BEAM_SYNC.md) for output/reset contracts, hardware evidence
and the still-open genlock/beam-write scope.

Preserve the ordering in `CompleteColorClock`, `TickDevices` and register dispatch.
Accepted address/data operations survive line/field boundaries and relevant DMA
disable transitions, completing with their accepted address and physical phase.
Device-specific uncertainty about real hardware is recorded in the issue register;
it does not justify changing implemented ordering during unrelated work.

Copper WAIT requires a free memory slot to wake after its comparison succeeds;
higher-priority DMA can defer that transition even though no instruction word is
read during wake-up. The following MOVE retains its ordinary two-word bus path.
This is distinct from palette presentation delay; see [the correction record](../NATIVE_VALIDATION.md#copper-wait-wake-up-correction-2026-09-18).

A frame restart requested while Copper DMA is disabled remains pending. Its
retained dummy output loads the then-current COP1LC before instruction fetches
resume; writing a new list while DMA is off must not execute the old list first.
An ordinary mid-field DMA pause retains execution position. See the
[North & South investigation](NORTH_SOUTH_INVESTIGATION.md) for the discriminating
hardware-photo probes and the bounded implementation scope.

Advance hardware through CPU instruction and accepted interrupt-entry retirement,
including internal cycles after the final bus access. A CPU already beyond the
field target must not leave hardware unable to complete that field (LWA-EXEC-001).
Preserve refresh/contention and every CPU bus operation when simplifying waits.

Paula's software-written `INTREQ` bit 14 is retained, read back and cleared
independently of the ordinary request bits. It participates in level-6 priority
while `INTENA` bit 14 enables interrupts. Enabling the master gate alone does
not synthesize a request. CPU and Copper register writes use the existing
one-CCK request/enable visibility scheduler, including acknowledgement; physical
propagation is not newly certified. This undocumented behavior follows the
independent [WinUAE controller](https://github.com/tonioni/WinUAE/blob/master/custom.cpp)
and [Minimig controller](https://github.com/retrofun/MinimigAGA-MiST-TC64/blob/master/rtl/minimig/paula_intcontroller.v).

The default product profile has PAL 8371 Agnus, OCS Denise, 512 KiB Chip RAM and
512 KiB slow RAM at `$C00000`. Explicit 8372A supports 512 KiB or 1 MiB Chip;
8375 part 318069-10 supports 1 or 2 MiB. These larger Chip layouts replace the
trapdoor slow bank. The 512 KiB layouts permit 0 or 512 KiB slow RAM. Native
Kickstart 1.3 and A500 3.1 execute with reset overlay vectors. Slow RAM is not
true Fast RAM; optional Zorro II Fast RAM remains separate.

CPU chip-memory addresses below `$200000` alias the fitted 512 KiB through
`$07FFFF`, including instruction fetches and Agnus bus contention. This follows
the default A500 JP2 wiring; it adds no memory capacity. See the
[Super Cars II investigation](SUPER_CARS_II_INVESTIGATION.md) for the hardware
basis, regression tests and the resulting Kickstart memory-probe timing change.

For 1 MiB 8372A, CPU/DMA physical A20 aliases; 8375 decodes the full 2 MiB.
With only 1 MiB fitted to 8375, the upper bank is unfitted and does not alias.
ECS pointer registers retain a 21-bit even address independently of physical
bank decoding. The 8372A internal A20 rollover behavior and electrical unfitted-bank
readback are not revision-specific hardware measurements. Native KS 3.1 memory
discovery, upper-memory DMA and unchanged default captures are recorded in
[the Agnus evidence](AGNUS_CHIP_RAM_2026-10-01.md). This slice retains the existing
OCS display sequencers when ECS controls are inactive. The subsequent
[ECS display implementation](ECS_DISPLAY_2026-10-01.md) adds explicit 8373
Denise, two-plane SuperHires and programmable raster geometry on the same
canonical clock. ECS register/data inputs retain the existing CCK delay, and
accepted DMA addresses survive later timing changes. Disk, sprite and audio
request calendars follow the current line period; Copper opportunities follow
the nominal counter transition. Programmable periods are HTOTAL+1 and VTOTAL+1
(with the additional long-field line in interlace). NTSC display counters
alternate 227/228 CCK lines unless LOLDIS is set; the fitted PAL oscillator stays
unchanged. Output remains bounded during exceptionally long/stopped fields.

Completed ECS field buffers carry their own dimensions. The host duplicates or
weaves vertical samples, changes buffer storage at mode boundaries, and stores
width/height/presentation geometry with each frame lease. Previously leased
arrays are immutable until released. Host pacing follows the current output
cadence; the standard 114-CCK/525-line Productivity presentation uses square
native pixels after compensating for progressive vertical duplication.

## Floppy drives

`FloppyDriveCount` connects DF0 through DF3 in order (1–4; default 1). Each drive
owns its standard-ADF or preserved IPF tracks, cylinder, motor latch, readiness/change state and
nominal 300 RPM spindle phase. CIA-B PRB bits 3–6 select the drives; side, direction,
step and motor are shared. Selected status inputs combine as active-low lines.
External empty DD drives identify as `DRT_AMIGA` on READY with the motor off;
disconnected drives do not pull any input low. References:
[Commodore Hardware Reference Manual, disk interface](https://bastya.net/AmigaDevDocs/hard_8.html)
and [Commodore disk.resource constants](https://d0.se/include/resources/disk.i).

Further inward STEP pulses at the bounded drive limit leave the head in place
and let the guest continue; side changes and outward homing still work. The
existing maxima remain cylinder 79 for ADF/empty drives and 83 for IPF. These
are model limits, not universal physical-drive measurements. See the
[Desert Strike end-stop comparison](DESERT_STRIKE_END_STOP_2026-09-24.md).

Unselected running drives keep rotating. Selection/head changes preserve phase;
mounting/ejecting one drive does not reset another. Reset stops all motors and
receiver/DMA state while retaining mounted media and cylinders. One shared Paula
receiver owns partial bytes, sync and slow-recovery state; one DMA FIFO owns
accepted transfers. Disk arrivals still execute before the disk DMA phase.
After the initial sync gate opens, ADKCON.WORDSYNC toggles preserve partial and
buffered words; only subsequent enabled input matches realign a running read.
Toggles while still waiting for the first sync remain explicitly unsupported.
See [live WORDSYNC evidence and limits](DISK_LIVE_WORDSYNC.md).
Received matches with WORDSYNC enabled also realign the CPU's DSKBYTR byte
counter independently of DMA. Register-only comparisons preserve that counter;
already published byte data/ready state survives alignment. See
[CPU byte alignment evidence and limits](DISK_BYTE_WORDSYNC_2026-09-24.md).
The next disk event is recomputed at disk/control/media events, with no additional
per-CCK polling or allocation. Tracks are encoded/decoded only at mount time.
IPF geometry has an exact bit count and optional cumulative integer cell deadlines;
head/seek changes map elapsed spindle time into the new track. Index is independent
of FAST/slow receiver mode. A separate causal receiver turns preserved transitions
into recovered cells. The standard ADF path retains its compact equivalent model.
See the [storage contract](STORAGE.md) for weak/no-flux, density and validation limits.

Multiple selection fans out controls and combines status. More than one selected,
running, mounted read source reports unsupported: this ideal-ADF implementation
does not model overlapping physical read pulses. Standard-ADF write DMA is now
implemented; HD media and silicon-exact analog recovery remain outside scope. See LWA-DISK-011
and LWA-DISK-012 in the [issue register](ISSUES.md).

In the desktop app, select **Settings > Floppy Drives > Connected** and restart to change
drive count. Each connected drive has its own image field and live status button.
Clearing an image field and applying settings ejects that disk; ZIP disk sets can
be assigned across connected drives. Swaps keep an independent 25-field empty
interval per drive. Pausing holds these intervals; reset mounts pending media.

[Multiple-drive tests](../../CopperMod.Amiga.Lightweight.Tests/LightweightMultipleDriveTests.cs)
cover all four select lines with distinct-sector DMA decoding, external DD IDs,
independent mechanics/rotation, shared receiver state, invalid indices and zero
execution allocations. Host tests cover four-drive startup, independent swaps,
reset, persistence and runtime command routing. These are deterministic model
checks. Separate [native evidence](../NATIVE_VALIDATION.md#four-drive-verification-2026-09-17)
covers Workbench detection, a DF1 directory read and independent external-drive
media changes; broad multi-drive game-loader verification remains open.

## Dual-playfield output

OCS BPLCON0 DPF splits planes 1/3/5 into PF1 and 2/4/6 into PF2. Each
playfield keeps the existing odd/even BPLCON1 scroll and DMA modulo path. PF1
uses colors 1–7, PF2 uses 9–15; each zero value is transparent, with COLOR00
behind both. Hires has two planes per playfield. BPLCON2 PF2PRI selects the
front playfield; legal PF1P/PF2P codes 0–4 independently mask sprite pairs.
An opaque hidden playfield still masks sprites, as illustrated by the
[Commodore HRM priority example](https://amigadev.grimore.org/Hardware_Manual_guide/node0159.html).
Palette grouping follows the [HRM dual-playfield chapter](https://amigadev.grimore.org/Hardware_Manual_guide/node007a.html).

Composition uses one 64-byte register-derived lookup table per machine, rebuilt
when relevant BPLCON2 bits change. No new fetches, schedules or per-frame allocations
are introduced. Hires samples/advances each sprite once per lores pixel while
applying separate masks to its two hires subpixels. Existing delayed control writes,
display windows, blanking and shifter state continue to apply across mode changes.
The ordinary lores path shares its pre-existing special-mode flag check with HAM.

The six bitplane shifters occupy 96 bits in pixel order (a 64-bit word and a
32-bit word), so ordinary output reads the next six-bit pixel directly. Plane
bits are deposited at the existing reload phase, using BMI2 when available and
a scalar fallback otherwise. Odd/even reload checks remain independent and occur
after the current pixel; scroll, plane-enable and mode changes retain their
existing phase semantics. Hidden pixels still advance the shifters. This changes
state representation, not DMA fetches, device ordering or the number of pixels
processed. The full engine suite passes with hardware intrinsics disabled as well
as enabled; host throughput is measured separately on the documented x64 host.
An inline due-time check avoids calling the input-application method when neither
pending input is due. When due, the same method applies control before data after
rendering the prior physical CCK, including the existing frame-boundary calls.
It adds no schedule cache or batching.

The pinned Legacy `Display.Bitplanes.cs` implementation listed in the
[reuse audit](ROADMAP.md#legacy-implementation-is-the-migration-starting-point)
was reviewed for grouping, palette and priority semantics; its renderer/scheduler
was not imported. The [dual-playfield regressions](../../CopperMod.Amiga.Lightweight.Tests/LightweightDualPlayfieldTests.cs)
exercise color combinations, scroll, attached/unattached sprite masks, hidden
playfields, clipping, mid-line changes, DMA invariance and interlaced allocation-free
output. These retain the current pipeline phase contract; they do not independently
certify undocumented physical transition timing. Dual HAM uses the selected
playfield index for palette/component data and raw planes 5/6 for HAM control.
Priority codes 5–7 select COLOR00 for the winning field without changing raw
opacity or exposing the field behind it. The same table handles these values at
register-write time; ordinary pixel execution is unchanged. See the
[nonstandard-mode evidence and limits](NONSTANDARD_OCS.md). CLXCON/CLXDAT collisions use the same raw
bitplane pixels and sprite shifters, before display priority hides either source.
CLXDAT bits are sticky until a CPU read; debug peeks do not clear them, and bit 15
reads high. Odd sprite inclusion follows CLXCON. Single-playfield PF1/sprite
matching includes the OCS even-plane condition observed in the pinned vAmigaTS
`sprcoll8`/`sprcoll8d` probes. Accumulation follows the existing visible-window
pipeline; a midline BPU disable retains comparison through that line's remaining
display window, then stops it before later lines. This bounded transition model
preserves sprcoll8/8d and the narrower-window border probes together; exact
pixel-phase timing remains unverified.

The pending playfield comparison shares the sprite pixel-work mask. Once its
sticky bit is set, the ordinary lores path needs no separate collision branch;
a read-clear or mode change restores the comparison at the next pixel. Every
visible pixel needed for an unlatched event is still evaluated. No host opt-out,
sampling or game-specific path is used.

Active bitplane sequencing publishes the next physical CCK directly: neither an
accepted pending output nor a delayed BPLCON0 stage can precede that next CCK.
Inactive sequencing still computes its next control deadline. Hires composition
uses the existing work mask to prepare pending playfield comparisons. A lone
eligible sprite can stop comparing after both of its playfield bits are latched;
read-clear re-enables that work, and overlapping groups still run the full test.

## CopperHDF

The optional virtual Zorro II board preserves the pinned Legacy CopperHDF identity
and `copperhdf.device` protocol. CopperDisk owns file ranges, RDB discovery and
filesystem metadata. Lightweight owns Autoconfig, diagnostic relocation, guest
device/boot registration and bounded synchronous gateways on the machine owner.
There is no scheduler import, recursive CPU dispatch, per-CCK HDF polling or runtime
Legacy dependency. Non-quick completion returns through a guest ReplyMsg thunk;
Exec calls run as ordinary CPU instructions. Guest RAM and whole transfer ranges
are validated before transfer; a 512-byte stack buffer tracks completed bytes.
Writable file handles flush on Update/Flush and disposal. Quiesced restart candidates
can share a backing handle until construction succeeds, so failed construction
leaves the current mount usable. Full interface and verification: [STORAGE.md](STORAGE.md).

## Newly implemented OCS interfaces

Paula serial uses canonical CCK deadlines with no host callbacks or allocations.
Its next edge shares the CIA/keyboard interface deadline, keeping an idle UART
out of the hot device tick. Transmit words have an automatic start and a
software-supplied stop bit; the holding register and shift register are separate.
Receive samples eight/nine data bits and a stop bit, with INTREQ RBF acknowledgement
and overrun. Break forces TXD low. Half-bit qualification and interrupt propagation
are bounded model choices, not measured UART pin timings.

Disk writes use the existing three-word FIFO and fixed Agnus address/output slots
in reverse: accept the address, then read RAM at output. Paula emits a cell every
7 CCKs in FAST or 14 CCKs in slow mode. Serial completion, including the documented
loss of the last three bits, owns DSKBLK. Cancellation retains an accepted bus
transaction without allowing it to feed a new transfer. Write protection affects
the medium, not DMA completion. All selected writable drives receive the write
stream. MSBSYNC aligns each byte on a set MSB in the ideal receiver.

Each mounted ADF has eagerly encoded tracks. Writes splice consecutive cells into
this grid without allocations or sector-level shortcuts. This is an ideal-ADF
model: analog precompensation, PLL behavior and the few-cell difference between
nominal 300 RPM and Paula's crystal-derived write clock are not flux simulation.
Host export uses CopperDisk's checksum-checked decoder and requires all 11 sectors
on every changed track. Invalid/custom tracks are retained in memory and export
fails rather than silently restoring old sector data. Save uses a temporary file
and replacement after successful export; no media file is written per frame.

Paddle counters evaluate elapsed horizontal periods on reads/input changes, with
an eight-line discharge model and independent X/Y thresholds. They do not add
per-line callbacks. The light-pen latch captures the current beam and releases at
PAL blank end (line 25). No-trigger blank latching, exact analog comparator phases
and chip-test beam counter writes remain explicit verification/implementation
boundaries in the completion record.

## Host and output contract

Marshal input to the machine's execution owner. Read or copy the reusable
`Framebuffer` and `AudioSamples` before executing the next frame; do not retain
them as immutable snapshots. Use width 908 for full hires output. The library's
454-wide option is explicitly lowres-only, not a cheaper complete native workload.

The framebuffer uses beam coordinates and includes blanking. Presentation owns
viewports, scaling and interlace composition. Do not crop or skip emulated pixels
to hide a defect or improve benchmark FPS. Host input delivery and audio queues
have their own lifecycle; pause/reset/fault handling must not replay stale audio.

Unsupported hardware or execution must be reported visibly. Do not silently
switch engines, downgrade a configured machine, bypass a stop or insert a
title-specific behavior. Native Workbench launches disk software. Legacy boot
restoration is outside the current product task; see the [retired restoration
plan](../../CopperScreen/COPPERSTART_RESTORATION.md).

## Maintenance and evidence

Fix demonstrated contradictions with a small discriminating regression check
when practical. Test expectations need provenance: implementation equality,
Legacy agreement and game compatibility are not independent hardware oracles.
Unverified edges may remain open during bounded development; a passing game does
not close them. Run checks appropriate to the changed path; keep diagnostic engine
outputs separate from production as documented in the root README.

Keep release execution lightweight: no added per-cycle logging, diagnostic
callbacks, allocations, generic event machinery or speculative schedule caches.
Profile actual work before optimizing; preserve CPU, DMA, pixels and PCM.
Documentation-only edits require link and instruction checks, not emulator suites.

## Hardware reference inventory

These are sources cited by the retained investigations, not newly verified sources
in this consolidation. The old `CopperMod.Amiga/References` directory and its local
files are absent from this checkout. Acquire only task-relevant references; do not
assume that an old drive path exists or that an archived citation proves availability.

| Reference | Relevant material | Availability and authority |
| --- | --- | --- |
| Commodore, Amiga Hardware Reference Manual, 2nd/3rd editions | Copper comparison, bitplane fetches/HAM, sprites, disk/ADKCON and appendix F CIA | Web citations remain in the [staged record](../history/amiga/LIGHTWEIGHT_A500_ENGINE_PLAN.md); the previously named local 2nd-edition PDF is absent. Record edition/section when using it. Primary documentation, not proof of every undocumented phase. |
| MOS/Commodore 8520 specification | Timers, serial pins, TOD and interrupts | Citation retained in the staged record's H1 evidence. No local copy inventoried. Primary specification; board propagation still needs evidence. |
| Commodore US4780844A receiver patent | Recovery windows and phase/frequency correction | Citation retained under LWA-DISK-010 in the [historical issue register](../history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md). Does not certify the reduced ideal-ADF model. |
| UndocumentedChipsetFeatures.md / linked hardware research | Undocumented registers and timing observations | Previously referenced local file absent. Recover provenance and applicable chip revision before relying on a claim. |
| Pinned WinUAE source and FPGA comparisons | Disk, CIA, display and sprite implementation comparisons | URLs/commits retained with the original investigations. Supporting evidence, not a physical A500 oracle. |

Raw traces, media and frozen binaries referenced by old paths have not been
recovered by this documentation change. See the [archive availability notes](../history/amiga/README.md).
