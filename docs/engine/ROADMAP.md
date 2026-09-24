# Lightweight product completion roadmap

Source audit: 2026-09-17; OCS/storage status reconciled 2026-09-19. Lightweight is already the active/default execution
engine, but currently supports a narrow native PAL OCS A500 configuration. Completing
the product transition requires porting/integrating capabilities, not merely enabling
settings. Legacy ECS/AGA implementations exist, but are not wired into this independent
engine; they should be audited for reuse before new implementation work. This roadmap proposes an implementation
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
| ECS | Not ported/integrated into Lightweight; Legacy has ECS addressing and display/register paths. Active host explicitly rejects ECS. | Audit and port applicable Agnus/Denise semantics and tests into the single-owner engine; cover larger Chip RAM, extended windows, timing/SuperHires and big blits, then native verification. |
| AGA | Not ported/integrated into Lightweight; Legacy has AGA register, palette and bitplane paths. Active output/device paths remain OCS. | Audit and port applicable Alice/Lisa semantics and tests, including fetch/palette/bitplane/sprite/HAM8 behavior, then native AGA validation. |
| NTSC | Host rejects non-OCS-PAL; engine clock geometry and Paula frequency are PAL-specific. | Machine timing configuration across beam, CIA/TOD, disk, audio and host pacing. Changing a display label is insufficient. |
| DF0–DF3 | Implemented: configurable 1–4 drives, independent media/mechanics/spindle phases, shared CIA status and Paula receiver/DMA, external DD identification, host controls/status and indexed scripted swaps. Native Workbench four-drive detection/media changes exercised. | Broaden native loader verification; physical overlapping read sources remain explicitly unsupported (LWA-DISK-011). |
| Writable floppy | Guest write DMA, protection, strict ADF export and desktop Save ADF implemented; native format/write/read/reopen exercised. | Interactive desktop save/close checks and physical FIFO/splice timing remain unverified; native and automated save/reopen evidence is retained. |
| More floppy formats | Standard ADF and read-only IPF are integrated through DF0–DF3, ZIP selection and scripted swaps. IPF preserves raw tracks and uses a causal receiver; protected-game compatibility remains incomplete. | The CPU trace blocker is repaired; verify Thunderbolt second-disk handling and receiver physics independently. Bounded Full Contact/Beast gameplay and disk swaps are recorded. Extended ADF/SCP execution remains separate. See [storage](STORAGE.md). |
| 68010 | Pinned Copper68k exposes M68010; Lightweight hardcodes M68000 and host CPU choices lack 68010. | Model selection, exceptions/VBR/reset/interrupt and bus-clock integration, configuration/UI, targeted CPU integration and native replay. |
| 68EC020 / 68020 / 68030 / 68040 | Pinned Copper68k exposes interpreters; host metadata contains several later models but rejects them for Lightweight. | Per-model addressing, instruction/bus timing, interrupt boundaries and memory access. Separately scope cache/MMU/FPU behavior rather than inferring completeness from the model name. |
| 68040 JIT / 68060 | Package documents opt-in 68040 JIT requiring a JIT-capable bus; Lightweight implements IM68kBus only. No M68060 model is exposed in the inspected package API. | JIT bus/snapshot/invalidation integration and parity are separate; 68060 would require CPU-package work as well as engine integration. |
| RAM and expansion | Constructor enforces exactly 512 KiB Chip + 512 KiB slow; host rejects true Fast RAM. Addressing contains unconditional 24-bit masks and OCS pointer limits. | Supported Chip/slow/Fast RAM maps, chipset-specific DMA addressing, CPU-specific address width and expansion discovery where required. |
| Other Kickstarts / Workbench | Host explicitly validates native KS 1.3; low-level ROM loading alone does not establish another machine profile. | ROM mapping/version validation, reset/overlay and native boot/application replay for each supported ROM/profile. Native ROM executes OS services; broad Workbench support does not require reimplementing those services in CopperStart. |
| Hard disk / HDF | CopperHDF virtual Zorro II interface ported from pinned Legacy, with file-backed persistence, RDB/partition metadata, host settings and native OFS cold boot without DF0. | Complete storage acceptance; additional filesystems need supplied handlers. Physical IDE/SCSI and host directories are outside this milestone. See [interface/evidence](STORAGE.md). |
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
host requires OCS PAL. “Missing” in this inventory means missing from Lightweight,
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
3. **Add configurable machine resources.** Implement declared Chip/slow/Fast RAM
   layouts and required expansion discovery, newer ROM profiles and NTSC timing.
   Integrate 68010 as the smaller CPU-model step, while retaining 68000 regressions.
4. **Deliver an ECS profile.** Treat Agnus and Denise revisions explicitly; add
   ECS registers/modes and verify native ECS use. Some memory/timing work in step 3
   naturally belongs to this implementation. An A500+-class profile and an A600-class
   profile should not be conflated merely because both use ECS.
5. **Deliver an AGA profile.** Integrate 68EC020/68020 CPU behavior and a declared
   memory map, then Alice/Lisa display/DMA changes and an A1200-class ROM/storage
   profile. Preserve OCS/ECS behavior with inactive-feature checks and native replays.
6. **Expand later CPUs and optional facilities.** Add 68030/68040 integration with
   explicit cache/MMU/FPU scope, then JIT if justified. Schedule RTG, full save states,
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
new pinned package when required. The current production pin is stable Copper68k
[`1.4.1`](CPU_TRACE.md), distributed through NuGet.org. It retains the 68000 trace
correction and validated prefetch-locality optimization, with the additional
release optimizations recorded in CopperMod.
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
