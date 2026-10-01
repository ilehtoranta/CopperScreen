# Lightweight product completion roadmap

Source audit: 2026-09-17; OCS/storage status reconciled 2026-09-19; Agnus/RAM and ECS display follow-up 2026-10-01. Lightweight is already the active/default execution
engine, with bounded native PAL A500 configurations and OCS/ECS Denise. Completing
the product transition requires porting/integrating capabilities, not merely enabling
settings. Legacy ECS semantics have been audited and ported into this independent
engine; Legacy AGA implementations remain available for the same reuse audit. This roadmap proposes an implementation
order; it is not a claim of completed support or a new historical cutover gate.

Keep the [supported profile](../../CopperMod.Amiga.Lightweight/README.md),
[architecture](ARCHITECTURE.md), [issue register](ISSUES.md) and
[performance guide](PERFORMANCE.md) authoritative for current behavior. The source
review here found additional gaps beyond the previously focused issue register.
The original inventory was a source review. The DF0–DF3 row has since been updated
for the implemented four-drive read support; verification boundaries remain explicit.

## Capability inventory

| Capability | Current evidence | Work needed |
| --- | --- | --- |
| OCS PAL / 68000 / native KS 1.3 | Implemented for 512 KiB Chip + 512 KiB slow RAM, one to four ADF/read-only IPF drives, ADF writes and optional CopperHDF. Bounded native acceptance recorded. | Broaden native compatibility coverage and close demonstrated failures; retain disclosed timing uncertainties. |
| OCS dual playfield | Implemented: lores/hires separation, scrolling, transparency, playfield/sprite priority, dual HAM and priority codes 5–7. See [nonstandard-mode evidence](NONSTANDARD_OCS.md). | Broaden native gameplay and physical transition coverage; see LWA-VIDEO-008. |
| Collision detection | Implemented CLXCON/CLXDAT accumulation, grouping and CPU read-clear; focused tests and native probes exercised. | Retain the focused/native evidence and scoped performance acceptance; verify remaining physical phases. See OCS_COMPLETION.md. |
| Remaining OCS edges | Beam-counter repositioning and external synchronization/genlock are missing. Line-mode BLTSIZE widths other than two are explicitly unsupported. Sprite/HAM/hires/CIA/disk timing edges remain documented. | Complete missing register behavior and final candidate validation; distinguish those gaps from unverified physical phases. |
| ECS | Explicit PAL Agnus memory layouts and 8373 Denise, SuperHires palette/sprites/DMA, DIWHIGH, programmable timing/blank/sync, HHPOSW, DOFF and safe host resizing implemented. Native PAL progressive/interlaced SuperHires, programmed NTSC/640x480 and HD boot/write/reopen pass; see [ECS evidence](ECS_DISPLAY_2026-10-01.md). | Broaden stock monitor-driver/native application and physical-edge coverage. Supplied media lacks NTSC/VGA launchers. Analog genlock and A2024 remain separate; generic unspecified Agnus remains rejected. |
| AGA | Explicit PAL A1200 profile, Alice/Lisa, 68EC020 and 2 MiB Chip RAM. Native KS3.0/CopperHDF boot, eight-plane 16/32/64-bit DMA and RGB24 output; [initial evidence](AGA_INITIAL_2026-10-01.md). HAM6/HAM8 and two four-plane playfields have [focused and native probe coverage](AGA_HAM_DUAL_2026-10-01.md). [Enhanced sprites and collisions](AGA_SPRITES_2026-10-01.md) add widths, resolutions, fine positions, palette banks and planes 7/8. [Palette readback and scan doubling](AGA_READBACK_SCAN2_2026-10-02.md) add RDRAM, BSCAN2 and SSCAN2. | Verify active transitions, electrical/manual page-mode bus residue, physical phases, nonstandard combinations, stock scan-doubled monitor drivers and representative native AGA games/demos. |
| NTSC | ECS BEAMCON0 can select alternating 227/228-CCK NTSC display counters, with DMA/TOD calendars and host cadence following them. The fitted oscillator and desktop motherboard remain PAL. | A separately declared NTSC oscillator/board profile and stock NTSC monitor-driver coverage remain open. Display-counter selection does not change physical CPU/CIA/audio clocks. |
| DF0–DF3 | Implemented: configurable 1–4 drives, independent media/mechanics/spindle phases, shared CIA status and Paula receiver/DMA, external DD identification, host controls/status and indexed scripted swaps. Native Workbench four-drive detection/media changes exercised. | Broaden native loader verification; physical overlapping read sources remain explicitly unsupported (LWA-DISK-011). |
| Writable floppy | Guest write DMA, protection, strict ADF export and desktop Save ADF implemented; native format/write/read/reopen exercised. | Interactive desktop save/close checks and physical FIFO/splice timing remain unverified; native and automated save/reopen evidence is retained. |
| More floppy formats | Standard ADF and read-only IPF are integrated through DF0–DF3, ZIP selection and scripted swaps. IPF preserves raw tracks and uses a causal receiver; protected-game compatibility remains incomplete. | The CPU trace blocker is repaired; verify Thunderbolt second-disk handling and receiver physics independently. Bounded Full Contact/Beast gameplay and disk swaps are recorded. Extended ADF/SCP execution remains separate. See [storage](STORAGE.md). |
| 68010 | API exists, but the [2026-09-26 probes](CPU_OPTIONS_2026-09-26.md) expose lost bus timing and an incomplete address-error frame. The model remains unavailable. | Correct the shared package, then model selection, exceptions/VBR/reset/interrupt and bus-clock integration, configuration/UI and native replay. |
| 68EC020 / 68020 | Experimental OCS choices implemented through Copper68k; `.54` published [CopperHDF boot and native Fast RAM I/O](COPPERHDF_020_2026-09-27.md), with the current `.55` integration preserving these results. Chip RAM instruction caching and extended MOVE decoding are corrected. See [correction, release and validation](LOTUS_III_020_CACHE_2026-09-26.md) and [initial implementation](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md). | Expand instruction/hardware conformance and game compatibility beyond the bounded Lotus III replay. |
| 68030 / 68040 | Experimental desktop/runner choices implemented with declared clocks and cache-control corrections; native Fast RAM/HDF I/O passes. See [validation](CPU_OPTIONS_030_040_060_2026-09-27.md). | The [040 fetch correction](LOTUS_III_040_FETCH_2026-09-27.md) passes the original Lotus script through a driving demo without shifting input. Broaden conformance; speculative prefetch, pipeline overlap and full hardware timing remain open. |
| 68040 JIT | Package requires a JIT-capable bus; Lightweight does not implement it. | JIT bus/snapshot/invalidation integration and parity are separate work. |
| 68060 | Distinct integer-focused diagnostic core implemented; desktop unavailable because native Kickstart 1.3 task/FPU frames are incompatible. | Add and validate 060-aware OS support, FPU arithmetic, MMU and remaining instruction/exception conformance. See [exact blocker](CPU_OPTIONS_030_040_060_2026-09-27.md). |
| RAM and expansion | Explicit PAL Agnus with 512 KiB/1 MiB/2 MiB Chip layouts and optional slow RAM at 512 KiB, plus optional 512 KiB–8 MiB CPU-only Zorro II Fast RAM. Settings, profiles, runner, native memory discovery and persistence are implemented; see [Agnus](AGNUS_CHIP_RAM_2026-10-01.md) and [Fast RAM](FAST_RAM_2026-09-27.md) evidence. | Broader motherboard/Chip/slow layouts, Zorro III or 32-bit accelerator-local memory and their model-specific timing remain separate work. |
| Other Kickstarts / Workbench | Native KS 1.3 and A500 KS 3.1 (512 KiB, v40.63) are available in the desktop. Default 68000 Workbench 3.1 floppy boot passes with 0 and 2 MiB Fast RAM; see [native boot evidence](KICKSTART_31_2026-09-30.md). | Broaden native application, storage and CPU/ROM combinations. Validate each additional ROM/machine profile separately. Native ROM executes OS services. |
| Hard disk / HDF | CopperHDF virtual Zorro II interface ported from pinned Legacy, with file-backed persistence, RDB/partition metadata, host settings, native KS 1.3 OFS cold boot and [KS 3.1 FFS RDB boot/write/reopen](KICKSTART_31_HD_2026-09-30.md) without DF0. | Broaden filesystem and installed-system coverage; other filesystems need supplied handlers. Physical IDE/SCSI and host directories are outside this milestone. See [interface/evidence](STORAGE.md). |
| Keyboard and controllers | Keyboard startup/recovery, physical keys/Caps Lock/A500 reset and CIA serial/CNT/port modes are implemented; see [the evidence and remaining accuracy boundaries](KEYBOARD_CIA.md). Ideal paddle counters and a light-pen latch are available. Mouse is allowed on the first port only. | Verify MCU/electrical and CIA pipeline timing; configurable port routing and broader native controls coverage remain. CD32/paddles/light pen/adapters are separate peripheral scopes. |
| Paula serial / parallel ports | Paula UART transmit/receive, status, interrupts, break and pin API implemented. General parallel transport and CIA pin modes remain separate gaps. | Complete UART physical phase verification; add optional host transports separately. |
| RTC | Explicitly rejected. | Select the RTC model per machine, implement registers/time policy and persistence, then native reads/set-time/reset checks. |
| Save states | Explicitly listed unsupported; no full machine state format. | Versioned CPU/device/pending-transfer/media state and deterministic restore across active DMA/audio/input. Host output queues need reset after restore. |
| RTG | Explicitly rejected; old composition code is excluded from compilation. | A separately scoped guest-visible graphics device/driver and host presentation integration. Not implied by AGA support. |
| Host integration | Native display/audio/input are active; CopperBench/CopperStart and clipboard gateways report unavailable. | Restore required user workflows through explicit supported integration. Do not make Lightweight depend on the Legacy execution engine. |
| Performance portability | Historical harnesses remain unchanged; a separate homogeneous-retention-v1 harness verifies current-host topology and retains host-load policy v2. | Use frozen same-work comparisons and the [measurement guide](PERFORMANCE.md); no inherited historical FPS claim. |

Settings entries, enums, raw register storage and excluded Legacy code are not
implementation evidence. `CopperScreen.csproj` excludes `CopperScreenEmulator.cs`,
`CopperBenchViewModel.cs` and the old presentation helper. CopperStart's archived
OS-service matrix must not be read as Lightweight hardware coverage.

## Legacy implementation is the migration starting point

Follow-up source verification on 2026-09-17 inspected the original CopperMod at
the recorded cleanup commit `30615e8317a4069e879b585049ed97ffdfecd4fe`:

- [ChipDmaAddressing.cs](https://github.com/ilehtoranta/CopperMod/blob/30615e8317a4069e879b585049ed97ffdfecd4fe/CopperMod.Amiga/CustomChips/Agnus/ChipDmaAddressing.cs):
  ECS-aware DMA masks and physical Chip RAM size checks.
- [Display.Bitplanes.cs](https://github.com/ilehtoranta/CopperMod/blob/30615e8317a4069e879b585049ed97ffdfecd4fe/CopperMod.Amiga/CustomChips/Denise/Display.Bitplanes.cs):
  ECS SuperHires selection and AGA-specific plane-count/playfield-color handling.
- [Display.Registers.cs](https://github.com/ilehtoranta/CopperMod/blob/30615e8317a4069e879b585049ed97ffdfecd4fe/CopperMod.Amiga/CustomChips/Denise/Display.Registers.cs):
  ECS/AGA BPLCON3, AGA BPLCON4/FMODE, DIWHIGH and banked high/low-nibble AGA palette writes.

These are real implementations, not just settings enums. Their existence does not
establish that they were ported into Lightweight or that every edge was verified.
The active engine has no dependency on those Legacy device/renderer paths and the
host requires a PAL motherboard and OCS/ECS Denise, with explicit Agnus memory layouts. “Missing” in this inventory means missing from Lightweight,
not absent from the project's engineering history.

Before ECS/AGA development, inventory Legacy implementations and their tests,
separating reusable semantics, known limitations and coupling to the old scheduler.
Port the useful behavior and discriminating tests while retaining Lightweight's
single-owner execution model; do not import the slow Legacy scheduler wholesale
or rewrite verified register behavior unnecessarily. Apply the same reuse audit
to other previously implemented features such as drives and hard disks.

## Recommended delivery sequence

1. **Complete the PAL OCS work.** Finish validation and optimization of collisions,
   UART, writable ADF and paddle/light-pen interfaces. Implement the remaining
   beam-counter/synchronization gaps. Extend native game/Workbench/input coverage
   and meet the owner's 1% performance ceiling; see [the active record](OCS_COMPLETION.md).
2. **Extend storage.** Finish final-candidate writable-ADF save/reopen and host checks.
   Finish IPF protection/native compatibility and the separate 1% performance
   gate for the implemented CopperHDF/ADF/IPF candidate. Avoid tying ordinary
   storage usability to completion of AGA.
3. **Add configurable machine resources.** Zorro II Fast RAM and native A500
   Kickstart 3.1 and explicit PAL Agnus/Chip layouts are implemented. Extend
   remaining motherboard layouts, additional ROM profiles and NTSC timing.
   Integrate 68010 as the smaller CPU-model step, while retaining 68000 regressions.
4. **Broaden the delivered ECS display.** Explicit Agnus/Denise selection, ECS
   registers/modes and bounded native use are implemented. Add supplied stock
   monitor-driver and native application coverage, then additional board layouts.
   An A500+-class profile and an A600-class profile should not be conflated merely
   because both use ECS.
5. **Deliver an AGA profile.** Integrate 68EC020/68020 CPU behavior and a declared
   memory map, then Alice/Lisa display/DMA changes and an A1200-class ROM/storage
   profile. Preserve OCS/ECS behavior with inactive-feature checks and native replays.
6. **Expand later CPUs and optional facilities.** Broaden the experimental 030/040
   integration, add 060-aware OS/FPU support, then JIT if justified. Schedule RTG, full save states,
   preserved-track media and optional host/peripheral services according to usage.

This is a priority proposal, not a rigid dependency chain. For example, a carefully
bounded save-state effort or 68010 integration can be scheduled earlier. Performance
measurement portability should proceed before making new formal speed claims,
without blocking correctness work on an old-machine gate.

## Important design boundaries

**Four drives:** keep one machine owner and one physical disk controller path.
Each drive needs its own cylinder/head, motor/ready/change/protect state and spindle
position. Shared select, step, side and motor lines must address the selected drives
correctly, including defined multiple-selection behavior. Four UI disk slots backed
by one drive object would not be four-drive support.

**Hard disks:** the selected A500 interface is virtual Zorro II CopperHDF,
preserving `copperhdf.device`, RDB/partition discovery and filesystem ownership.
The [storage contract](STORAGE.md) defines its native boot and persistence checks.
A600/A1200 IDE and a host-directory filesystem are separate interfaces.

**CPUs:** the original `Copper68k 1.4.1-boundary.1` API audit exposed M68000,
M68010, M68EC020, M68020, M68030 and M68040. This audit verifies API availability,
not complete core correctness. CPU fixes stay in CopperMod and arrive through a
new pinned package when required. The current public mainline pin is
[`1.5.0`](COPPER68K_MAINLINE_1_5_0.md), incorporating the validated
[040 fetch correction](LOTUS_III_040_FETCH_2026-09-27.md) and preceding
[020/HDF work](COPPERHDF_020_2026-09-27.md). Advanced CPU profiles remain
experimental and the 060 remains diagnostic-only.
The release retains its 68000 trace and prefetch-locality implementation.
Later CPU clock rates and bus widths must map
to the engine's single canonical timebase; do not preserve a 68000-specific
CPU-cycle/CCK assumption merely by switching the factory argument.

**ECS and AGA:** these are register and execution changes, not verification labels.
Commodore describes ECS additions including programmable scan timing, SuperHires,
extended windows/sprite behavior and larger blits. See the
[Commodore HRM ECS appendix](https://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node00A1.html).
For AGA, the Commodore Lisa specification describes eight bitplanes, expanded RGB
palette, wider fetch/shift paths, enhanced sprites and HAM8; Alice memory/DMA behavior
must be covered alongside Lisa output. See the
[Lisa specification, sections 1.1 and 2.1](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Aaa_lisa_specification.pdf).
These primary sources guide implementation scope, not a claim that one source
settles all hardware phases. CPU model expectations should use the
[Motorola programmer's reference](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf)
and the applicable processor user's manual.

## Definition of delivered support

For each declared machine profile, complete the engine behavior, public/session
configuration, UI/load/save round trip, runner and relevant native workloads.
Record separately what was implemented, what passed discriminating tests, what
was exercised natively and what remains unverified. Missing media is unavailable
coverage. A new flag or a boot screenshot is insufficient by itself.

Storage needs save/reopen/readback and cold-boot evidence, CPUs need model-specific
exception/interrupt/bus cases, and display needs register/fetch/composition cases
plus representative native modes. Re-run affected existing profiles to catch
regressions. Measure throughput separately with the same complete work; retain
the single-owner, allocation-light release architecture. No G6 revival, Legacy
fallback or blanket requirement to solve every undocumented edge is introduced.

## Source-review entry points

- [Host validation and per-drive operations](../../CopperScreen/CopperScreenLightweightSession.cs)
- [Fixed CPU, RAM, device ownership and register dispatch](../../CopperMod.Amiga.Lightweight/LightweightA500Machine.cs)
- [OCS mode rejection](../../CopperMod.Amiga.Lightweight/LightweightVideo.cs)
- [DF0–DF3 control and standard-ADF model](../../CopperMod.Amiga.Lightweight/LightweightFloppyDrive.cs)
- [Disk write/reprogramming restrictions](../../CopperMod.Amiga.Lightweight/LightweightDiskDma.cs)
- [PAL clock](../../CopperMod.Amiga.Lightweight/LightweightClock.cs)
- [CPU/bus boundary](../../CopperMod.Amiga.Lightweight/LightweightCpuBoundary.cs)
- [Broader CopperDisk format support](../../CopperDisk/README.md)

The engine's static `UnsupportedFeatures` list has been reconciled with this work,
but is not an exhaustive capability matrix. Unimplemented register behavior may
fall through to raw storage rather than explicitly fault, as the beam-counter
audit illustrates. Use the scope, issue and verification records together.
