# Lightweight A500 architecture

Lightweight is CopperScreen's active engine. Legacy and CopperStart are unavailable
in the standard application. The old G6/G7 cutover and completed Lightweight H-stage
procedures are [historical records](../history/amiga/README.md), not prerequisites
for continued development.

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

## Clock and state invariants

Keyboard startup/recovery, physical key state, A500 reset, CIA serial/CNT modes
and timer-port/pin interfaces are specified in [KEYBOARD_CIA.md](KEYBOARD_CIA.md).
Idle CIA timers retain arithmetic advancement; active pin edges share the existing
interface deadline. No MCU execution loop or additional per-CCK polling is used.

The PAL model has two CPU cycles per color clock (CCK), 454 CPU cycles per line,
and 312/313-line fields. Reset selects the long field; field selection and LACE
behavior use the same beam state. CPU accesses may stop on either CPU-cycle phase.
The clock completes the partial CCK before continuing steady CCK advancement.

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

The supported product profile has 512 KiB Chip RAM and 512 KiB slow RAM at
`$C00000`, native 256 KiB Kickstart 1.3 and reset overlay vectors. Slow RAM is not
true fast RAM. Low-level API capabilities do not expand the supported host profile.

CPU chip-memory addresses below `$200000` alias the fitted 512 KiB through
`$07FFFF`, including instruction fetches and Agnus bus contention. This follows
the default A500 JP2 wiring; it adds no memory capacity. See the
[Super Cars II investigation](SUPER_CARS_II_INVESTIGATION.md) for the hardware
basis, regression tests and the resulting Kickstart memory-probe timing change.

## Floppy drives

`FloppyDriveCount` connects DF0 through DF3 in order (1–4; default 1). Each drive
owns its standard-ADF or preserved IPF tracks, cylinder, motor latch, readiness/change state and
nominal 300 RPM spindle phase. CIA-B PRB bits 3–6 select the drives; side, direction,
step and motor are shared. Selected status inputs combine as active-low lines.
External empty DD drives identify as `DRT_AMIGA` on READY with the motor off;
disconnected drives do not pull any input low. References:
[Commodore Hardware Reference Manual, disk interface](https://bastya.net/AmigaDevDocs/hard_8.html)
and [Commodore disk.resource constants](https://d0.se/include/resources/disk.i).

Unselected running drives keep rotating. Selection/head changes preserve phase;
mounting/ejecting one drive does not reset another. Reset stops all motors and
receiver/DMA state while retaining mounted media and cylinders. One shared Paula
receiver owns partial bytes, sync and slow-recovery state; one DMA FIFO owns
accepted transfers. Disk arrivals still execute before the disk DMA phase.
After the initial sync gate opens, ADKCON.WORDSYNC toggles preserve partial and
buffered words; only subsequent enabled input matches realign a running read.
Toggles while still waiting for the first sync remain explicitly unsupported.
See [live WORDSYNC evidence and limits](DISK_LIVE_WORDSYNC.md).
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
title-specific behavior. Native Workbench launches disk software; optional
[CopperStart restoration](../../CopperScreen/COPPERSTART_RESTORATION.md) is separate.

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
