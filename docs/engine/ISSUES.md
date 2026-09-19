# Lightweight A500 issues and limitations

Updated 2026-09-19 for the accepted optimization candidate and current OCS gap audit.
Storage acceptance remains incomplete; see [STORAGE.md](STORAGE.md).
The [OCS completion record](OCS_COMPLETION.md) retains earlier evidence and scoped
performance exceptions. Scope is the [supported PAL OCS A500 profile](../../CopperMod.Amiga.Lightweight/README.md).
This is a focused follow-up register, not an exhaustive emulator conformance audit.
For the broader ECS/AGA, CPU, storage and host transition, see the
[product completion roadmap](ROADMAP.md), which separates missing implementation
from verification and integration work.

Native Lemmings gameplay, live digger assignment, disk replacement and a separate
same-build boot-path reset check were accepted in the bounded 2026-09-15 session.
Hired Guns menu/initial training replays and live music, sound effects and graphics
have recorded evidence and bounded acceptance. Full game completion, every disk
transition and every timing edge are not certified. The [acceptance record](../history/amiga/LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md)
preserves scope and the one-off performance waiver. These checks are not pending
merely because an earlier paragraph in the [historical register](../history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md)
said so.

## Status and next work

Confirmed bugs contradict a supported contract; unverified behavior records an
assumption requiring evidence; unsupported features are implementation gaps.
Missing hardware proof is not itself a demonstrated bug or a blanket blocker.
Performance is assessed separately using the [measurement guide](PERFORMANCE.md).

| ID | Subsystem / subject | Category and current status | Next action or trigger |
| --- | --- | --- | --- |
| LWA-HOST-002 | Interlace steadiness | Blackout repaired; residual symptom OPEN / deferred | On a repeatable report, distinguish host cadence, field sequencing and inherent flicker. |
| LWA-DISK-001 | Request sampling / slots | Unverified; OPEN | Capture FIFO arrivals around eligible slots. |
| LWA-DISK-002 | Final word / completion IRQ | Unverified; OPEN | Tie RAM visibility, countdown and interrupt to a hardware-backed timeline. |
| LWA-DISK-003 | Cancel / rearm | Unverified; OPEN | Check accepted transfers across cancellation/disable/rearm. |
| LWA-DISK-004 | Non-WORDSYNC alignment | Unverified; OPEN | Vary start strobe across all bit positions. |
| LWA-DISK-005 | Partial words / drive transitions | Unverified; OPEN | Investigate a transition-related read failure. |
| LWA-DISK-006 | DMAON readback | Unverified; OPEN | Establish readback after start, completion and pause. |
| LWA-DISK-007 | Sync / byte status / reset | Unverified; OPEN | Obtain reset and coincident-write expectations. |
| LWA-DISK-008 | Ideal-ADF mechanics | Bounded model; OPEN | Add an effect only when supported content demonstrably needs it. |
| LWA-DISK-010 | Receiver-window phase | Approximation; OPEN | Measure FAST changes, window completion and readback phases. |
| LWA-DISK-013 | Preserved IPF receiver / native compatibility | Implemented bounded model; OPEN | Full Contact completes a disk-two fight replay; Beast responds to outdoor game controls after a correctly timed swap. Thunderbolt now passes protection and enters gameplay; disk-two handling remains unverified. Verify density/weak/no-flux against independent hardware traces; bounded native success is not receiver certification. |
| LWA-CPU-001 | 68000 trace exception in pinned Copper68k | Fixed in development pin | `1.4.1-trace.1` passes scalar/batched vector-9 integration and Thunderbolt protection. Source fix belongs to CopperMod; package is local and unpublished. See [CPU_TRACE.md](CPU_TRACE.md) for restore requirements, architecture and coverage. |
| LWA-AUDIO-004 | CPU-fed word after DMAOFF | Fixed bounded transition | Queued AUDxDAT now survives the last DMA word and raises its manual-playback IRQ. Four discriminating channel/byte-phase cases and Thunderbolt gameplay pass. Exact physical interrupt phase remains within the audio uncertainties above. |
| LWA-HDF-001 | CopperHDF integration | Implemented; bounded native OFS proof | Retain partition/RDB cold-boot/write/flush/reopen evidence; other filesystem handlers need supplied media. Physical IDE/SCSI and host directories are separate features. |
| LWA-VIDEO-002 | Hires transitions | Unverified; OPEN | Investigate supported raster/scroll/mode-transition failures. |
| LWA-VIDEO-005 | HAM edges | Mode implemented; edges OPEN | Establish hold reset, mid-line mode and sprite interaction phases. |
| LWA-VIDEO-006 | Sprite sequencing edges | Early-blank corruption repaired; edges OPEN | Verify comparator reuse, manual control and stolen-slot boundaries. |
| LWA-VIDEO-008 | Dual-playfield edges | Ordinary mode implemented; undocumented modes/physical phases OPEN | Broaden native gameplay; establish priority codes 5–7 and mode-write boundary expectations. |
| LWA-COPPER-001 | WAIT wake-up under bitplane DMA | Confirmed early colour write; repaired 2026-09-18 | WAIT wake-up now yields to higher-priority DMA. Six-plane Beast border starts at x288 instead of x272; see the [native record](../NATIVE_VALIDATION.md#copper-wait-wake-up-correction-2026-09-18). Other undocumented control-state edges remain unverified. |
| LWA-CIA-001 | Board TOD pulse phase | Unverified; OPEN | Obtain board pulse-to-counter/IRQ evidence. |
| LWA-CIA-002 | TOD comparator / reset | Unverified; OPEN | Establish write-trigger and latch/reset behavior. |
| LWA-CIA-003 | CIA-to-Paula synchronization | Lost held IRQ repaired; sub-CCK edges OPEN | Obtain acknowledgement/INTREQR/IPL phase evidence. |
| LWA-INPUT-001 | Keyboard startup / resync | Unsupported; OPEN | Implement when supported boot/recovery needs the missing protocol. |
| LWA-INPUT-002 | Keyboard / CIA serial pins | Unverified edges and unsupported modes; OPEN | Isolate a pin/IRQ discrepancy or required clocked serial mode. |
| LWA-INPUT-003 | Controller pins / analog input | Digital controls and ideal paddle counters implemented; physical edges OPEN | Establish pin/RC timing; see LWA-INPUT-005. |

Prioritize a demonstrated failure in the supported profile first. Residual
interlace is a disclosed non-blocking follow-up. Hardware research should start
from the smallest discriminating case, not from reopening all completed stages.
Benchmark portability remains separate tooling work in the performance guide.

## Remaining PAL OCS completion work — 2026-09-19

Ordinary dual playfield, collisions, Paula UART, DF0–DF3 and standard-ADF writes
are implemented. The following source-backed gaps prevent an unrestricted OCS
completeness claim; undocumented combinations are separate from ordinary mode support.

| Area | Missing implementation |
| --- | --- |
| Beam and synchronization | VHPOSW and VPOSW beam repositioning beyond LOF; external synchronization/genlock and consistent DMA/display rescheduling. See LWA-VIDEO-009. |
| Nonstandard display and blitter modes | Dual HAM, HAM outside five/six-plane lores, hires BPU above four, BPU=7, dual-playfield priority codes 5–7, and line-mode BLTSIZE widths other than two. These remain explicitly rejected. |
| Disk-controller edge behavior | Active DSKLEN reprogramming without cancellation, WORDSYNC changes during DMA, FIFO overrun behavior and simultaneous selected read streams. Preserved-track writes and fully physical write splices/precompensation are absent. |
| A500 board I/O | Keyboard power-up/resynchronization/retransmission/reset chord; general CIA clocked serial output and external CNT timer modes. General parallel-port integration remains separate work. |
| Undocumented bus data | The 227-CCK wrap-dummy bus-data case remains explicitly unsupported. |

Implemented behavior still needs independent physical verification at display
mode/reload, HAM, sprite/collision, Copper/bus, disk FIFO/sync/completion, CIA TOD/IRQ,
Paula audio/UART interrupt and analog input boundaries. The IPF receiver is a
bounded model, not a silicon-certified separator. Residual interlace instability
still needs a reproducible host-versus-engine diagnosis. These are verification
gaps or disclosed approximations, not proof that each feature is broken.

Host paddle/light-pen mapping, optional serial/parallel transports, interactive
storage-dialog checks and Thunderbolt disk-two replay remain integration/native
coverage work. They should not be counted as missing collision, UART, four-drive
or ADF-write implementations. See [storage](STORAGE.md) for the exact evidence.

Recommended implementation order is beam/synchronization, keyboard/CIA recovery
and pin modes, then the explicitly rejected disk and nonstandard register cases,
prioritizing any demonstrated native failure. Each slice needs focused hardware
expectations and its own unchanged-work performance comparison. The latest scoped
performance acceptance does not close these gaps. NTSC is also missing for a
broader OCS profile; ECS/AGA, newer CPUs and expansion/storage devices are separate
capabilities beyond the agreed PAL OCS/68000 scope.

## Disk assumptions

### LWA-DISK-001 — Request sampling and slot ordering

A ready FIFO word uses the next eligible address input at internal H7/H9/HB,
with RAM output one CCK later at H8/HA/HC. These coordinates do not establish
hardware request-sampling delay or ordering for FIFO occupancy 1–3. The impact is
contention and memory visibility relative to CPU/Copper/blitter/display. Close
with arrival-before/on/after-boundary expectations and focused phase checks.

### LWA-DISK-002 — Final word, length and completion interrupt

The read model decrements length at RAM output and latches DSKBLK on final output,
with provisional immediate CPU visibility. Serial/FIFO/RAM attribution, actual
IRQ delay and the documented last-read-word quirk need bounded evidence. Revisit
final-word corruption or completion polling; close with a short-transfer timeline
including the final serial bit, accepted bus phase, RAM data, count and IRQ.

### LWA-DISK-003 — Cancellation and rearming

Cancellation drops unaccepted words but preserves an accepted address/data pair
through RAM output. The old write must not decrement or interrupt a newly armed
transfer; reset clears pending work. Actual DSKLEN cancel and DMACON disable phases
remain unverified. Retain accepted-write isolation regressions and add hardware-backed
cancel/disable/rearm cases when a supported loader or LWA-DISK-002 evidence requires it.

### LWA-DISK-004 — Initial alignment without WORDSYNC

The second enabled DSKLEN strobe starts a fresh 16-bit counter when WORDSYNC is
disabled. Hardware may retain a free-running receiver phase instead. Establish
first-word data/timing across all 16 strobe positions when unsynchronized reads
matter. A continuous-DMA benchmark only exercises the current model.

### LWA-DISK-005 — Partial words and drive transitions

DMA disable preserves alignment while dropping incoming enqueue operations;
drive deselection freezes receiver input while the medium keeps rotating.
Selection/head changes do not restart the spindle. Retention of bit/byte/word/sync
phase through disable, selection, head and seek transitions remains unverified.
Close transition-related failures with phase-specific expectations preserving the
shared clock and already accepted RAM effects.

### LWA-DISK-006 — DMA-active readback

DSKBYTR enable/direction bits derive from raw DSKLEN/DMACON, separately from internal
activity. DMAON after completion, first start strobe, cancellation and pause needs
external evidence. Revisit software polling; test externally justified readback,
not merely the raw-bit formula.

### LWA-DISK-007 — Sync, byte status and reset

The live shift register is compared on incoming bits and DSKSYNC writes. A new
match requests a sync interrupt with provisional four-CCK CPU visibility;
DSKSYNC resets to `$4489`. Comparator propagation, reset seed, WORDSYNC byte phase
and IRQ visibility remain unverified. Revisit sync/status loader failures with
reset/coincident-write evidence; retain documented following-word WORDSYNC coverage.

### LWA-DISK-008 — Ideal-ADF mechanics

Standard encoded ADF tracks use nominal 300 RPM and a new motor/media run starting
at track bit zero after readiness. Insertion phase, coast-down, seek settling and
speed variation are not fully modeled. ADF sectors are not physical flux captures.
Add effects only for demonstrated supported-workload needs with bounded evidence;
broader protection/flux support remains outside scope.

### LWA-DISK-010 — Recovery-window phase

The implemented slow receiver consumes two/three nominal source cells sequentially
and publishes at window completion. A pending window survives FAST writes and
line/field wraps; deselection freezes receiver state while rotation continues.
A new selected spindle/media run discards an incomplete window; hardware reset also clears
partial bytes. FAST input without a pending window uses the one-cell path.

This is an experimental ideal-ADF approximation, not a general PLL. Pulse aperture,
phase/frequency history, FAST-write phases, motor/media gaps and general GCR remain
unverified or unsupported. MSBSYNC byte alignment now has an ideal-bit-stream
implementation and focused coverage, without physical recovery timing proof.
HRM cell rates, the Commodore receiver patent and pinned
WinUAE comparisons are retained with the [original evidence](../history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).
They do not certify the chosen transition rule. Close with successive-phase writes
around pulse/window boundaries and recovered-bit, DSKBYTR, sync/IRQ and DMA evidence.

### LWA-DISK-011 — Multiple-drive read-line and native verification

DF0–DF3 standard ADF read support is implemented. CIA selects/control fan-out,
combined active-low status, external DD identification, independent spindle phases
and the single Paula receiver/DMA path have deterministic regression coverage.
Simultaneous selected running mounted sources are explicitly unsupported; physical
pulse overlap, drive-switch recovery phase and index pulse overlap are unverified.
Native Workbench 1.3 has detected/read media in all four drives and handled external
drive replacements/ejection; see the [native validation record](../NATIVE_VALIDATION.md#four-drive-verification-2026-09-17).
Broad native multi-disk loader compatibility and physical switching phases remain
unverified. Passing synthetic controller tests are separate from that native evidence.
The newer writable-ADF implementation and its validation boundaries are LWA-DISK-012.

## Display, interrupt and input edges

### LWA-HOST-002 — Residual interlace instability

Host phosphor decay now stops at one expected field boundary when input pressure
delays presentation, retaining the Background-priority responsiveness repair.
Occasional interlace instability remains accepted as a non-blocking caveat; its
cause is unclassified. Capture a repeatable case distinguishing host cadence,
incorrect field sequencing and normal interlace flicker. Do not assume it is
unfixable or reopen the accepted live session as an exhaustive compatibility check.

### LWA-VIDEO-002 — Hires reload and mode transitions

Normal hires uses 4,2,3,1 fetch cadence, 40 words per plane, lowres scroll units
modulo eight, delayed register/data stages and eight-CCK DDF stop control. Scroll
reloads resolve per physical hires pixel; sprites advance at lowres rate.
Writes adjacent to reloads, resolution/BPU/scroll changes during terminal fetch,
modulo selection and broader cracktro correctness remain unverified. Close with
hardware-backed phase cases; coarse/single-cycle internal equality is not an oracle.

### LWA-VIDEO-005 — HAM transition edges

PAL OCS five/six-plane lores HAM, including interlaced fields, is implemented.
The output stage holds a playfield color, resets to COLOR00 outside the window
and overlays sprites without modifying that hold. Exact reset versus HBLANK/DIW,
off-window fetch/scroll, mid-line HAM toggles without direct-color pixels and
undocumented sprite priority/transparency need evidence. Hires HAM, fewer-plane
HAM, BPU=7 and dual-playfield HAM remain unsupported; native artwork/menu success
does not verify these transitions.

### LWA-VIDEO-006 — Sprite comparator and control edges

Premature PAL vertical-blank sprite requests are repaired; direct blank-end
scheduling preserves the legal line-25 start and accepted completions. Exact
VSTART/VSTOP equality/reuse, starts on the reset line, manual POS/CTL interaction
and partial control pairs under stolen slots remain unverified. Obtain physical
expectations before expanding or certifying the later range-based sequencer.

### LWA-VIDEO-008 — Dual-playfield verification boundary

Ordinary OCS lores/hires dual playfield is implemented, including interlaced fields,
independent scrolling and sprite masking by both opaque playfields. Priority codes
0–4 follow the Commodore HRM; codes 5–7 and dual HAM explicitly report unsupported.
Regression tests preserve existing effective-register and shifter phases without
claiming new physical timing proof. A 3,600-field native Shadow of the Beast PNA
crackintro replay exercises BPLCON0=$4600, separate modulo values and audio. It is
intro coverage, not gameplay or game-completion acceptance. See the
[implementation contract](ARCHITECTURE.md#dual-playfield-output).

### LWA-CIA-001 — TOD pulse qualification

CIA-B receives a pulse per completed PAL line and CIA-A per completed field;
enabled alarms use the existing eight-CPU-cycle CIA-to-Paula visibility model.
Board sync/TICK phase, interlace pulses, E-clock qualification and reported
14–16 E-clock debounce need physical evidence. Close with pin-to-counter-to-ICR/IPL
traces including boundary crossing/reset when sub-line timing affects a workload.

### LWA-CIA-002 — TOD comparator, writes and reset

The model has a binary 24-bit counter and separate write-only alarm. High-byte
read latches until low-byte read; high-byte counter write stops, low-byte write
starts, and middle-byte write preserves running state. Alarm writes do not change
running state. Reset stops/clears; comparison occurs on an accepted pulse.
Write-trigger comparison, intermediate carries/glitches, power-on behavior and
writes during latched reads remain unverified. Close with evidence-backed register,
pulse, latch and IRQ sequences; Legacy's always-running counter is not an oracle.

### LWA-CIA-003 — Held interrupt synchronization

The lost held-IRQ/music defect is repaired: each CIA retains its asserted line
until ICR read/reset, and Paula acknowledgement cannot lose a still-held request.
Recording and rebuilt live music/sound effects were accepted. Sub-CCK synchronization,
transient INTREQR and IPL around acknowledgement remain unverified. Retain the
held-source regressions; use physical phase evidence to refine propagation.

### LWA-INPUT-001 — Keyboard startup and recovery

Synchronized raw-key transport includes KCLK phases, serial receive, ten-code
type-ahead and host data-line acknowledgement; SDR/ICR reads do not acknowledge.
Startup/resync pulses, FD/FE power-up stream, F9 retransmission, MCU scanning,
power-up held keys and reset chord are missing. A 143 ms timeout parks/reports
unsupported; changing direction during active receive is also unsupported.
Implement when supported startup/recovery requires it, using protocol-backed
interrupted-byte/held-key/reset cases and native replay. A synchronized fixture
does not establish cold-keyboard boot behavior.

### LWA-INPUT-002 — Keyboard and CIA serial pins

The chosen model has three 142-CPU-cycle phases per bit (~20 microseconds), starts
data at the next CCK, latches receive/ICR on the eighth rising KCLK edge and uses
the eight-cycle CIA/Paula IRQ path. Host low/high pulses of at least eight CPU
cycles acknowledge; fixtures use at least 604 cycles. These are model choices,
not measured oscillator/pin propagation. General timer-clocked serial output and
keyboard CNT timer modes remain unsupported. Revisit a required mode or observed
pin/IRQ mismatch; isolate the first differing phase before changing execution.

### LWA-INPUT-003 — Controller pins and analog behavior

SubmitInput applies relative mouse counters at the current cycle with 8-bit wrap;
joystick uses JOYDAT XOR encoding and stored digital fire/POTGOR levels. JOYTEST
preserves the low two counter bits; modeled low outputs/buttons pull lines low.
Ideal paddle charge-time counters and light-pen capture now have explicit owner-thread
APIs and focused tests (LWA-INPUT-005). Electrical quadrature/sampling, mixed pin
changes, physical RC/comparator timing, adapters and reset details remain unverified
or missing. Host paddle/light-pen mapping is still absent. Add required behavior with
explicit host-event/pin/counter evidence, without per-cycle polling or generic events.

## Resolved defects and implemented gaps

These IDs remain regression references. Full reproductions, build identities and
historical test results are retained in the [original issue register](../history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).
No historical test count is presented as a newly run suite.

| ID | Resolution to preserve | Coverage / remaining boundary |
| --- | --- | --- |
| LWA-EXEC-001 | Advance canonical hardware time through accepted interrupt-entry retirement. | `LightweightInterruptProgressTests`; native stall repaired, not full Hired Guns certification. |
| LWA-HOST-001 | Coalesced presentation runs below Input priority, preventing dispatcher starvation. | Host dispatcher regression and bounded live acceptance; new hangs need host stacks. |
| LWA-HOST-002 | Bound phosphor decay and resume with fresh fields. | Host decay/hold/resume checks; residual symptom remains above. |
| LWA-CIA-003 | Preserve asserted CIA lines across Paula acknowledgement. | `LightweightCiaInterruptLineTests`; exact propagation remains above. |
| LWA-VIDEO-001 | Implement OCS hires fetches and full-width output. | `LightweightHiresTests`; transition uncertainty is LWA-VIDEO-002. |
| LWA-VIDEO-003 | Copper WAIT/SKIP always compares vertical bit 7. | `LightweightCopperTests`; repaired repeated panel/short terrain. |
| LWA-VIDEO-004 | COPJMP reads trigger the shared hardware strobe. | `LightweightCopperReadStrobeTests`; repaired blank output after native progress. |
| LWA-VIDEO-005 | Implement OCS HAM5/HAM6 output. | `LightweightHamTests`; unverified transitions remain above. |
| LWA-VIDEO-006 | Suppress illegal early-blank sprite fetches and schedule first legal input directly. | `LightweightSpriteDmaTests`; broader comparator edges remain above. |
| LWA-VIDEO-007 | Decode OCS DIWSTOP V8 correctly and use host beam-coordinate viewports. | Bitplane and host viewport regressions; historical cracktro ID alias described below. |
| LWA-VIDEO-008 | Implement ordinary OCS dual playfield. | `LightweightDualPlayfieldTests`; undocumented modes and physical phases remain above. |
| LWA-DISK-009 | Execute slow-mode ideal-ADF reception with a bounded recovery window. | `LightweightDiskSerialTests` and recorded native replay; rate-switch phases remain LWA-DISK-010. |
| LWA-INPUT-004 | SDR is a holding buffer; reject actual clocked output, not an unclocked handshake write. | `LightweightKeyboardHandshakeTests` and native Return press/release replay. Actual clocked output remains unsupported. |
| LWA-PERF-001 | Keep the cycle-preserving CPU-wait simplification that resolved the input-active retention shortfall. | Historical same-work evidence; not an open hardware defect or a current-host speed claim. |

The historical register used LWA-VIDEO-003 twice. From this consolidation onward,
LWA-VIDEO-003 means the terrain/Copper comparison defect; **LWA-VIDEO-007** means
the cracktro DMA/host-origin repair formerly also called LWA-VIDEO-003. Historical
records retain their original spelling; use the title to disambiguate old citations.

For LWA-INPUT-004, native Enter/gameplay functionality is repaired and the later
bounded live session accepted. The fault-banner fixture was separately verified
in windowed/fullscreen modes. Earlier failed native-window captures do not prove
native fault-banner coverage; that narrower UI check remains unverified, without
reopening accepted gameplay.

Also retain the zero-length second DSKLEN-strobe regression: it is not cancellation.
The H6b2 serial-only retention shortfall was resolved in its historical comparison;
it is not an outstanding performance defect.

## Explicit scope gaps

Dual-playfield HAM and priority codes 5–7 remain explicitly unsupported.
Nonstandard line-mode BLTSIZE widths other than two are also rejected. The
2026-09-17 collision/UART gaps now have implementations and focused/native
coverage. Their original OCS candidate and later candidates have separate scoped
performance decisions in [PERFORMANCE.md](PERFORMANCE.md); physical verification
remains distinct from that acceptance.

Active DSKLEN reprogramming without cancellation, WORDSYNC changes during DMA and
FIFO overrun currently report unsupported. Standard-ADF write DMA and MSBSYNC
byte alignment are implemented in the ideal bit-cell model. Preserved-track/flux
formats, analog precompensation and general GCR media admission remain outside
the standard-ADF scope. Slow-mode input retains the approximation above.

### New OCS completion follow-ups

- **LWA-COLLISION-001:** CLXCON/CLXDAT implemented. The 2026-09-18 retry found
  `sprcoll8=$8400`, `sprcoll8d=$840A`, while
  pinned A500 OCS photographs show $8401/$840B. Earlier $8401 coverage preceded
  the BPU-disable guard and does not establish final retention. Border probes
  remain $8020/$8000 as expected. Retaining comparison through the current DIW
  after a midline BPU disable repairs the mismatch: all four native probes now
  match together, with four added focused cases. Exact pixel-phase timing remains
  unverified; see the identified build and evidence in OCS_COMPLETION.md.
- **LWA-SERIAL-001:** Paula transmit/receive, buffering, status, interrupts and
  break implemented. Twelve deterministic tests passed; three native UART probes
  ran. The TBE probe image does not establish a cycle-exact match to the hardware
  photograph. CCK start/receive qualification, period-write, interrupt and held-break
  edge cases need physical timing evidence.
- **LWA-DISK-012:** Guest writes, protection, cancel/completion, strict standard-ADF
  export and desktop save implemented. Native Workbench full format/verify,
  file write/read and separate-machine reopen ran on disposable media. The initial
  native build predates later optimizations. Both the identified retry engine and
  the final measured optimized engine repeat format/write/export and fresh-machine
  reopen fingerprints and visible completion markers; see the active record.
  FIFO request phases and write-splice/clock drift remain bounded models.
- **LWA-INPUT-005:** Ideal paddle charge-time counters and light-pen trigger API
  implemented. Four focused tests passed. Eight-line POT discharge, PAL line-25
  release and no-trigger blank capture are model choices; exact analog/silicon
  boundary timing and host peripheral mapping remain unverified.
- **LWA-VIDEO-009:** The broader register audit found VHPOSW and VPOSW beam
  repositioning beyond LOF, plus external synchronization/genlock, unimplemented.
  They must not be counted as completed OCS hardware merely because the register
  store accepts their values. Canonical beam/DMA rescheduling needs its own
  discriminating tests and implementation before an unrestricted completeness claim.

Keyboard MCU startup/recovery and general CIA serial/CNT modes remain the board
I/O gaps recorded above. The new OCS interfaces do not implicitly close them.

CopperStart/Legacy restoration is a deferred host feature, not a timing defect or
a dependency of Lightweight. Follow the [optional adapter boundary](../../CopperScreen/COPPERSTART_RESTORATION.md).
Other chipset/CPU profiles, RTC, RTG, physical IDE/SCSI controllers and save-state
compatibility are outside current product scope.

The storage change also repairs TOD high-byte reads while CRB.ALARM is selected:
they no longer leave a stale persistent TOD latch. Operation Thunderbolt passes
its PAL count check after this correction. The independent CPU trace defect and
subsequently exposed audio transition are now repaired in the local development
build. The latch regression and evidence are recorded in [STORAGE.md](STORAGE.md);
this does not close physical TOD/comparator uncertainties above.

For each revisit record the build/profile, minimal reproducer, observed/expected
cycle and state, evidence source, resolution and affected checks. Keep IDs stable
and distinguish repaired symptoms from remaining uncertainty. Implementation and
focused tests are in [the engine](../../CopperMod.Amiga.Lightweight) and
[diagnostic test project](../../CopperMod.Amiga.Lightweight.Tests); host regressions
are in [CopperScreen.Lightweight.Tests](../../CopperScreen.Lightweight.Tests).
