# Lightweight A500 issues and limitations

Updated 2026-09-19 for accepted `67c13f6`, the current OCS gap audit and the
ERSY optimization follow-up, accepted on 2026-09-19 with a scoped hires
performance exception recorded in [PERFORMANCE.md](PERFORMANCE.md).
Storage acceptance remains incomplete; see [STORAGE.md](STORAGE.md).
The [experimental CD32 profile](CD32.md) has synthetic board, media and input
coverage. The CD32 qualification matrix tracks the open WinUAE parity work.
The 2026-11-24 native replay uses the verified Amiga Forever 40.60 ROM pair
and [Retroguru's Sqrxz disc](https://www.sqrxz.de/sqrxz/), ISO SHA-256
`F3ECFD7F9D8840EE80F59E8EEE3DEE757E3F6E8498B6CD2C9480A3AFE030C988`.
The stock PAL and NTSC profiles reach the Sqrxz title after the Akiko EEPROM
strobe correction. The [Sqrxz boot timing investigation](CD32.md#sqrxz-boot-timing)
records the earlier guest reboot and the remaining ROM execution timing gap.
An earlier NTSC replay reproduced a host spin when the
bounded output deadline expired mid-rasterline. The clock now waits for the
line boundary that retires that output. The corrected PAL and NTSC replays
each completed 1,900 fields.
The [Alien 3 NTSC intro audio comparison](CD32.md#ntsc-intro-audio) traces
the Paula ring starvation to EC020 instruction and bus timing. The
CopperScreen Release build with published Copper68k 1.5.13 restores the
affected passage in a stock NTSC replay against WinUAE 6.0.3. The follow-up
traces the short gaps to NTSC write/playback drift in the guest's PCM ring.
Published Copper68k 1.5.15 removes two multi-field gaps and reduces the
longest later gap to two fields. WinUAE's area-averaged reference accounts
for most of the quiet-cue peak difference. Later gaps, small stereo
differences and full decoder timing parity remain open.
The [OCS completion record](OCS_COMPLETION.md) retains earlier evidence and scoped
performance exceptions. Scope is the [supported PAL A500 profiles with OCS/ECS Denise](../../CopperMod.Amiga.Lightweight/README.md).
This is a focused follow-up register, not an exhaustive emulator conformance audit.
The retired [minimal floppy firmware](MINIMAL_DISK_BOOT.md) had a separately
bounded service contract. Bundled builds now default to the
[standalone CopperStart Mini ROM](../COPPERSTART_APP.md#copperstart-mini-bundle).
The 2026-11-03 model extension verifies guest boot/services on standard PAL A500+
and A1200, including a 3,000-field Desert Dream startup on both. Arte's A1200
`disk FIFO overrun timing` stop was traced to Copper SKIP bypassing illegal-MOVE
protection. Checking protection before suppressing the write fixes the unwanted
ERSY write and the downstream overrun; the original disk now completes 3,000
fields with animated output and audio on A1200. The subsequent
[full Arte replay](MINIMAL_DISK_BOOT.md#arte-full-a1200-replay--2026-11-03) verifies
the ending credits and automatic restart on A1200 and A500+, with 40,000 fields
per profile and no captured CPU exception or fault; no further fix was needed.
See the
[Copper SKIP follow-up](MINIMAL_DISK_BOOT.md#arte-a1200-copper-skip-correction--2026-11-03)
for evidence and limits. It is separate from the historical A500 exception below.
The [Arte minimal-boot investigation](ARTE_MINIMAL_BOOT_CRASH_2026-10-08.md)
reproduces SOS exception 3 after 22,008 completed fields; native Kickstart 1.3
progresses through 40,000 fields on the same engine/media. The firmware's nonzero
RAM location zero led an empty guest list into ROM code; initialization now clears
that sentinel, with a discriminating regression and all 18 minimal-boot tests
passing. Corrected minimal boot also completes 40,000 diagnostic fields with no
captured address exception or unsupported-feature stop. The separately reported
Avalonia/CLR termination remains open.
For the broader ECS/AGA, CPU, storage and host transition, see the
[product completion roadmap](ROADMAP.md), which separates missing implementation
from verification and integration work.

Native A500 Kickstart 3.1 (v40.63) is available as of 2026-09-30, with bounded
68000 Workbench 3.1 floppy boot and desktop-session parity at 0 and 2 MiB Fast RAM.
See [the floppy boot record](KICKSTART_31_2026-09-30.md) and the subsequent
[native FFS hard-disk boot/write/reopen correction](KICKSTART_31_HD_2026-09-30.md).
The [Agnus/Chip RAM follow-up](AGNUS_CHIP_RAM_2026-10-01.md) adds explicit PAL
8371, 8372A and 8375 (318069-10) layouts. The subsequent
[ECS display record](ECS_DISPLAY_2026-10-01.md) adds explicit 8373 Denise,
SuperHires, DIWHIGH, programmable timing and DOFF. The [initial AGA profile](AGA_INITIAL_2026-10-01.md)
adds native PAL A1200 boot and eight-plane RGB24 output. The
[HAM/dual-playfield follow-up](AGA_HAM_DUAL_2026-10-01.md) implements HAM6/HAM8
and two four-plane playfields in all three resolutions, with native stock HAM
screens and a programmed DPF probe. The [sprite follow-up](AGA_SPRITES_2026-10-01.md)
adds 16/32/64-bit DMA, 140/70/35 ns sprite output, fine positions, palette banks
and eight-plane collisions, with original native probes. The [palette/scan-doubling
follow-up](AGA_READBACK_SCAN2_2026-10-02.md) adds RDRAM, BSCAN2 and SSCAN2 with
original native guest readbacks and pixel checks. CPU/Copper page-mode sprite bus residue, active sprite
resolution/fetch transitions, combined HAM/DPF, nonstandard HAM/DPF modes, physical genlock,
A2024 external scan conversion and the 68060 OS/FPU validation remain open.
Palette readback electrical bus residue, active scan-control/position rewrites
and physical scan-doubled monitor timing also need independent hardware evidence.
Stock NTSC/VGA-driver coverage needs a supplied Storage/monitor-driver fixture.
The [ECS-required media replay](ECS_REQUIRED_MEDIA_2026-10-01.md) covers Final
Fight: Enhanced — Final Edition's opening level, 2 MiB Chip RAM and ECS border
blanking with matching normal/scalar captures and OCS Denise/1 MiB controls.
Third-party SuperHires/programmable-raster coverage remains open.
The [supplied-game follow-up](AGA_GAMEPLAY_2026-10-02.md) verifies bounded native
UFO: Enemy Unknown strategy gameplay on the stock PAL A1200 profile: new game,
difficulty selection, base placement/naming, advancing Geoscape and interception
menu. Normal/scalar runs match all 139 captures through 22,000 fields. Alien
Breed 3D II renders the opening level and responds to movement, turning and
firing through 72,000 fields, with all 871 captures matching. Continuous visual
stability remains open under LWA-AGA-001. The shared Copper68k 1.5.2 candidate implements full indexed effective
addresses for admitted forms and further operand gaps reached by these games.
These fixes are now included in the Copper68k 1.5.3 release. The
[original startup failures](AGA_GAMES_2026-10-02.md) remain historical evidence.

Native Lemmings gameplay, live digger assignment, disk replacement and a separate
same-build boot-path reset check were accepted in the bounded 2026-09-15 session.
Hired Guns menu/initial training replays and live music, sound effects and graphics
have recorded evidence and bounded acceptance. Full game completion, every disk
transition and every timing edge are not certified. The [acceptance record](https://github.com/ilehtoranta/CopperScreen/blob/f83c9092efa9e75d930cb602415a2b8da1637db5/docs/history/amiga/LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md)
preserves scope and the one-off performance waiver. These checks are not pending
merely because an earlier paragraph in the [historical register](https://github.com/ilehtoranta/CopperScreen/blob/f83c9092efa9e75d930cb602415a2b8da1637db5/docs/history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md)
said so.

The [CopperStart/native Kickstart 1.3 Lemmings follow-up](COPPERSTART_LEMMINGS_2026-10-23.md)
adds supplied Skid Row ADF boot and disk swapping, P pause/resume, live digger
assignment and terrain removal, Fun 1 completion with 100% rescued, and the
Fun 2 briefing. Normal/scalar captures match and each final reset replay
matches its discovery run. No ROM or engine correction was established;
later levels and pristine original media remain unqualified. Historical
performance workloads and acceptance records are unchanged.

## Status and next work

The [Nitro follow-up](COPPERSTART_NITRO_2026-10-23.md) corrects admission of its
166-track extended ADF and zero-bit raw entries in CopperDisk, then fixes an
independently traced memory-BTST IPL sample phase in shared Copper68k. Both
CopperStart and native Kickstart 1.3 reach human-driver selection and bounded
first-race acceleration/steering. The ROM and engine hardware source remain
unchanged. Race completion, later tracks, multiplayer and other releases remain
unqualified; historical performance protocols are unchanged.

Confirmed bugs contradict a supported contract; unverified behavior records an
assumption requiring evidence; unsupported features are implementation gaps.
Missing hardware proof is not itself a demonstrated bug or a blanket blocker.
Performance is assessed separately using the [measurement guide](PERFORMANCE.md).

The Major Motion graphics issue is LWA-NATIVE-015 after branch integration;
its branch-local LWA-NATIVE-011 label collided with Nitro's existing mainline ID.

| ID | Subsystem / subject | Category and current status | Next action or trigger |
| --- | --- | --- | --- |
| LWA-HOST-005 | CopperOS capture review | CLOSED — review false positive; diagnostic validation hardened | The [pixel audit and correction](../COPPEROS_CAPTURE_VALIDATION_2026-10-20.md) proves that signed 43 start/midpoint/end and signed 31 midpoint PNGs are byte-identical, each with 2,962 visible proof-text pixels; hashes match the original hour record. The earlier blank classification was an incorrect visual review. Original dated reports and images remain unchanged. Separate signed native probes compare guest framebuffer/Shell memory, session pixels, presenter and window captures. Diagnostic captures now record hashes and reject absent fixed-proof text, with positive/negative controls. No renderer or engine defect is established by the original images; physical display/pacing remains a separate open gate. |
| LWA-HOST-004 | Installed audio producer / display-off | Warmup and blocked UI publication corrected in bounded controls; release continuity OPEN | [Low-intrusion timeline and signed 43 controls](../AUDIO_WARMUP_REFILL_2026-10-19.md) support optimized first-use JIT and keeping rendered-field PCM production independent of UI dispatch/presentation waits, including command publication. A measured WASAPI catch-up burst requires ten reserve blocks instead of eight (about 40 ms additional queued PAL audio); latency budgets remain OPEN. Both native paths and both processes have zero cumulative missing values, including 120-second native reopen follow-ups, with native save/export/readback. The original 37 CopperOS 748-value episode has no retrospective owner trace; its exact cause is unassigned. First-incident retention preserves future recurrence context; the exact 42 warmup cause remains unassigned where rolling coverage was lost. Earlier [display recovery](../AUDIO_DISPLAY_RECOVERY_2026-10-18.md) and [EventPipe observations](../AUDIO_REFILL_TRACE_2026-10-18.md) remain unchanged. [Signed 42-to-43 install/update persistence](../AUDIO_MSIX_VALIDATION_2026-10-19.md) now passes for the unchanged 43 hash, including recovery/reinstall. [Fresh exact-hash 43 WACK](../AUDIO_MSIX_WACK_VALIDATION_2026-10-19.md) reports overall PASS with all 13 required checks passing; the optional blocked-executable scan retains its 24 messages. Final production/Store-identity WACK and dependency review remain open. [Exact-hash installed hours](../INSTALLED_SESSIONS_2026-10-20.md) now complete both 3,600-second sessions and fresh-process save/readback with zero cumulative missing samples in all four timelines. Physical display/S3/PnP remain open; the separate [capture audit correction](../COPPEROS_CAPTURE_VALIDATION_2026-10-20.md) closes the mistaken blank-image finding. Sampled memory is descriptive. Loaded-host timing is INVALID / RERUN for formal performance acceptance; release budgets remain OPEN. |
| LWA-NATIVE-014 | YEET 1.0.4 / CopperStart | Main integrated; first Rune Trial board, rune push and reset qualified | Fresh 512 KiB ROM; 53,000-field interaction replay plus a 49,000-field normal/scalar pair matching all 1,230 files. Direct Copper takeover, boot Workbench reuse and CPU-only workspace memory fixed. Later rounds and levels remain unverified. See [gameplay evidence](COPPERSTART_YEET_GAMEPLAY_2026-10-23.md). |
| LWA-NATIVE-013 | F17 Challenge / CopperStart memory detection | False one-megabyte rejection corrected in native boot metadata; bounded driving verified | Publish the detected A501 physical range without freeing the private arena. Fitted/absent native probes pass; each 26,000-field F17 CPU-mode pair matches all 1,305 captures. Lemmings retains 100% rescue and Apidya its opening stage. Full races, save paths and other layouts remain unverified. See [source fix and fresh-ROM evidence](COPPERSTART_F17_2026-10-17.md). |
| LWA-NATIVE-012 | Lemmings / CopperStart intro exit | Missing empty-display Intuition service implemented in firmware; first level completed | Both ROMs rescue 100% in level one; each 24,000-field CPU-mode pair matches all 605 captures. Nitro retains all 71 earlier screenshots. General Intuition services and full-game coverage remain unavailable. See [fresh-ROM correction and evidence](COPPERSTART_LEMMINGS_2026-10-17.md). |
| LWA-NATIVE-011 | Nitro / media, boot and race-start wait | Media admission, CIA startup, IPL sampling and BTST prefetch corrected; bounded racing verified | Both ROMs pass the former wait and render the first race; each 14,000-field batch/scalar pair matches all 355 files. A neutral-input control separates the player's driving response. Full races/game completion and physical timing remain unverified. See [race correction and validation](NITRO_RACE_START_2026-10-17.md) and the retained [boot-fix record](COPPERSTART_NITRO_2026-10-17.md). |
| LWA-HOST-003 | Shared folders | Native DOS folder mounts and optional boot implemented | KS1.3/3.1 folder and exchange tests, 040 JIT, metadata and confinement checks are in [the shared-folder record](HOST_DIRECTORIES.md). Hot mounting, notifications, links, ExAll, 64-bit files and equivalent non-Windows concurrent-replacement qualification remain outside this version. |
| LWA-HOST-002 | Interlace steadiness | Blackout repaired; residual symptom OPEN / deferred | On a repeatable report, distinguish host cadence, field sequencing and inherent flicker. |
| LWA-NATIVE-001 | North & South CP release / Copper restart with DMA off | Corrected; combined candidate accepted 2026-09-21; bounded CopperStart play added | Defer the DMA-disabled frame restart's pointer load until its retained bus output. Untouched CP menus/borders and four hardware-photo probe patterns now agree. Earlier release-blame inference withdrawn; see [investigation](NORTH_SOUTH_INVESTIGATION.md). Combined performance acceptance is recorded with Super Cars II. The [CopperStart continuation](COPPERSTART_NORTH_SOUTH_2026-10-23.md) separately initializes native BPLCON2=$0024 to restore its hidden pointer, then verifies 1861 army movement, battle/unit controls and retreat. Campaign completion and fort/train sequences remain unqualified. |
| LWA-NATIVE-002 | Super Cars II / CPU chip-RAM mirrors | Boot Guru corrected; bounded driving verified; accepted 2026-09-21 | Reaches race 1, responds to driving input and completes one lap. Valid six-pair upper bounds: lores +2.2310% (scoped owner exception), hires +0.2671%, native +0.6240% (both pass). See [investigation](SUPER_CARS_II_INVESTIGATION.md). Disk 2/full race remain unverified. |
| LWA-NATIVE-003 | Inside the Machine v1.0.1 / effect corruption | Final-row area-blitter modulo corrected; scoped performance exception accepted | Chained C2P blits continued from pointers missing the final modulo. Six new regressions and the 18,000-field replay verify the correction and closing logo. See [investigation](BLITTER_FINAL_MODULO.md); no full hardware-frame certification. |
| LWA-NATIVE-004 | Miami Chase / overlapping menu text | Visual symptom repaired by the same blitter correction; scoped performance exception accepted | Unchanged disk-2/menu replay now clears text between screens. The [CopperStart/Kickstart 3.1 follow-up](COPPERSTART_MIAMI_CHASE_2026-10-23.md) reaches opening gameplay, radar, pause/resume and return to menu with the supplied mixed-release ADFs; mission completion and pristine original media remain unqualified. See [investigation](BLITTER_FINAL_MODULO.md). |
| LWA-NATIVE-005 | Lotus III / disk-2 prompt | CPU bus-spacing correction; bounded 68000 player racing verified; scoped historical performance exception accepted | The existing Copper68k timing contract prevents adjacent-CCK CPU transfers. Untouched disks and input pass the earlier prompt; all 72 historical normal/scalar files through 18,000 fields match. See [correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md) and [original diagnosis](LOTUS_III_INVESTIGATION_2026-09-25.md). The [CopperStart/native 1.3 follow-up](COPPERSTART_LOTUS3_2026-10-23.md) adds supplied cracked ADFs, disk swapping, one-player Arcade driving, automatic gears, steering/braking, F10 pause/resume and timeout through scores back to setup; 1165 normal/scalar files match through 23,200 fields. Native 1.3 needs one additional Fire pair before setup; later input is shared. No new correction or completed-stage claim. Historical [performance verification](LOTUS_III_PERFORMANCE_2026-09-25.md) is unchanged; the owner accepted its exact lores/hires bounds on 2026-09-25. Statistical dispositions and the default 1% limit remain unchanged. |
| LWA-NATIVE-006 | Alien Breed Special Edition '92 / black boot | CPU byte alignment corrected; bounded disk loading and opening play verified | Received WORDSYNC aligns DSKBYTR bytes without DMA. The [original correction](DISK_BYTE_WORDSYNC_2026-09-24.md) retains its 716-test and boot/menu evidence; [combined throughput](NATIVE_CORRECTIONS_VALIDATION_2026-09-25.md) remains valid with the lores gate unmet. The [SE92 continuation](COPPERSTART_ALIEN_BREED_SE92_2026-10-18.md) adds 22,000-field native/CopperStart opening movement/firing, complete normal/scalar parity and independent idle controls using a fresh Overdrive-corrected ROM. Later levels and physical coincident edges remain unverified. The [original failure](NATIVE_CORPUS_2026-09-21_BATCH2.md) is retained. |
| LWA-NATIVE-007 | Desert Dream / late execution failure | Live area-pointer correction; main and hidden endings verified | Pointer writes reach the running area blit, preventing the wrapped-zero clear of vector $78. Normal/scalar 72,000-field main replays match all 288 files; the 30,000-field hidden sequence matches all 120 files through final greetings. See [correction](DESERT_DREAM_LIVE_POINTERS_2026-09-25.md) and [follow-up validation](NATIVE_CORRECTIONS_VALIDATION_2026-09-25.md). Active restart and exact hardware timing remain unverified; combined throughput is valid with the lores gate unmet. |
| LWA-NATIVE-016 | Desert Dream / gaps in the opening spaceship | Corrected in the pinned CopperStart Mini ROM and verified in the local Windows x64 bundle | The previous Mini ROM handed off with `BLTAFWM` and `BLTALWM` at `$0000`, reproducing the fault on PAL OCS with a 68000, including scalar execution. The fixed ROM sets both masks to `$FFFF` before enabling blitter DMA. A 3,800-field cold boot of the original Disk A through the extracted Windows x64 bundle matched the verified standalone ROM replay pixel-for-pixel at fields 3,000, 3,075, 3,150 and 3,225. |
| LWA-BLITTER-001 | Active blitter reprogramming | Area pointer, modulo, minterm, source-latch, shift and channel-enable corrections verified in bounded cases. Remaining edges OPEN | CPU/Copper pointer-half writes reach an active area blit. [Pointer writeback](CHALLENGING_GAMES.md#pointer-writeback-during-an-accepted-area-transfer) uses the accepted area DMA address. [Modulo](CHALLENGING_GAMES.md#active-area-blitter-modulo-writes), [minterm](CHALLENGING_GAMES.md#live-area-minterms-and-fill-carry), [data](CHALLENGING_GAMES.md#manual-area-data-writes), [shift](CHALLENGING_GAMES.md#live-area-shifts-and-fill-carry), [A enable](CHALLENGING_GAMES.md#live-a-source-enable) and [B, C and D enables](CHALLENGING_GAMES.md#live-b-c-and-d-channel-changes) cover later transfers after register writes. Specific Copper pointer conflicts, remaining source-only schedules, active BLTCON1 mode changes and BLTSIZE writes while transfers are still pending need discriminating hardware evidence. See also [the area pointer tests](DESERT_DREAM_LIVE_POINTERS_2026-09-25.md). |
| LWA-BLITTER-002 | Alice area blitter completion and final D | Destination pipeline and arbitration covered by focused probes | Source reads lead destination writes, BUSY and the final D drain are separate, and the CPU request observing Alice's interrupt yields to the final write. Absolute Copper start timing, live in-game reprogramming and physical signal measurements remain OPEN. See [Zool 2 investigation](CHALLENGING_GAMES.md#area-blitter-pipeline). |
| LWA-DISK-014 | Live WORDSYNC changes / IPF boot blockers | Running reads and initial wait implemented; physical edges OPEN | Established reads preserve partial/FIFO/accepted words on toggles. Initial-wait toggles retain the latched start gate and input word phase; only live-enabled received matches realign it. See [initial-wait semantics and validation](DISK_INITIAL_WORDSYNC.md). Superfrog, F17 Challenge and Overdrive passed their former boot stops in the earlier [running-read correction](DISK_LIVE_WORDSYNC.md); no additional compatibility gain or physical coincident-edge verification is claimed. |
| LWA-NATIVE-008 | Desert Strike / disk-2 drive-range stop | End-stop correction implemented; bounded coastal flight and weapon use verified | WinUAE reproduces the stale guest track cache but continues after over-seeking. Lightweight clamps at its existing drive limit without stopping execution or patching guest memory. See [the original correction](DESERT_STRIKE_END_STOP_2026-09-24.md) and [October 19 takeoff/flight/landing](COPPERSTART_DESERT_STRIKE_2026-10-19.md). The [October 23 follow-up](COPPERSTART_DESERT_STRIKE_2026-10-23.md) uses the later rebuilt ROM and frozen Nitro-qualified runtime for coastal flight, turning, chain-gun/Hydra use and map/status/mission interaction through 38000 fields. No new correction; enemy destruction, rescue and objective/campaign completion remain unverified. The [original failure](NATIVE_CORPUS_2026-09-21_BATCH2.md) remains historical evidence. |
| LWA-NATIVE-009 | Alien Breed II / black loader state | Reclassified: supplied AGA edition, outside current profile | All three supplied SPS 44 disks match the AGA catalog by size/CRC/SHA1. Loader clearing overlaps its mirrored code in 512 KiB chip RAM. This is not evidence of an OCS engine defect; OCS SPS 278 media is unavailable locally. See [investigation](ALIEN_BREED_II_INVESTIGATION.md); original failure captures remain preserved. |
| LWA-NATIVE-010 | Alien Breed: Tower Assault / boot Guru | Absent OCS DENISEID corrected; optimized candidate accepted 2026-09-24 | Zero register-storage readback selected the AGA stack outside fitted RAM. Bounded undriven-bus readback reaches disk-2 loading and the opening level; 335 state/image/RAM checkpoints match scalar execution. The [scoped-local candidate](SCOPED_LOCALS_2026-09-24.md) retains all 1005 captures and passes every retained workload under the unchanged 1% upper-bound limit, without an exception. Earlier comparison results remain separate evidence; physical bus uncertainties remain open. The [CopperStart continuation](COPPERSTART_TOWER_ASSAULT_2026-10-18.md) independently verifies the original disks through opening movement/firing and Game Over on a freshly compiled Overdrive-corrected ROM, with complete normal/scalar parity and idle controls; no new engine correction is needed. See [investigation](TOWER_ASSAULT_INVESTIGATION.md); original [corpus failure](NATIVE_CORPUS_2026-09-23.md) is retained. |
| LWA-NATIVE-015 | Major Motion / Kickstart 3.1 graphics | OPEN; clean CopperStart/Kickstart 1.3 gameplay verified | The same supplied ADF has corrupt title/attract graphics with Kickstart 3.1 through a 36000-field no-input diagnostic. Cause is not assigned to game, ROM or engine. CopperStart and Kickstart 1.3 separately pass bounded driving/fire, pause, abort/restart and sound-toggle replays; CopperStart loading is much slower. See [evidence and limits](COPPERSTART_MAJOR_MOTION_2026-10-23.md). |
| LWA-BUS-001 | OCS undriven custom-register reads | DENISEID bounded digital model; physical edges OPEN | Previous-CCK DMA data / idle pull-ups implemented for absent `$DFF07C`. Electrical decay, refresh/dummy cycles, coincident edges and other write-only register read side effects are not certified. See [evidence boundary](TOWER_ASSAULT_INVESTIGATION.md#hardware-evidence-and-model-boundary). |
| LWA-RUNNER-001 | Scripted ZIP-entry media paths on Windows | Corrected 2026-09-26 | Resolve the host archive path separately while preserving `#/` and the entry. Four discriminating tests and the six-title [native regression suite](NATIVE_REGRESSION.md) verify direct ZIP-entry swaps. The [original reproduction](NATIVE_CORPUS_2026-09-21.md#runner-scripted-zip-entry-paths-on-windows) remains historical evidence. |
| LWA-DISK-001 | Request sampling / slots | Unverified; OPEN | Capture FIFO arrivals around eligible slots. |
| LWA-DISK-002 | Final word / completion IRQ | Unverified; OPEN | Tie RAM visibility, countdown and interrupt to a hardware-backed timeline. |
| LWA-DISK-003 | Cancel / rearm | Unverified; OPEN | Check accepted transfers across cancellation/disable/rearm. |
| LWA-DISK-004 | Non-WORDSYNC alignment | Unverified; OPEN | Vary start strobe across all bit positions. |
| LWA-DISK-005 | Partial words / drive transitions | Unverified; OPEN | Investigate a transition-related read failure. |
| LWA-DISK-006 | DMAON readback | Active-latch correction implemented; physical phases OPEN | First-strobe/completion status and zero-length sync gating have focused coverage; see [disk control edges](DISK_CONTROL_EDGES.md). |
| LWA-DISK-007 | Sync / byte status / reset | Unverified; OPEN | Obtain reset and coincident-write expectations. |
| LWA-DISK-008 | Ideal-ADF mechanics | Bounded model; OPEN | Add an effect only when supported content demonstrably needs it. |
| LWA-DISK-010 | Receiver-window phase | Approximation; OPEN | Measure FAST changes, window completion and readback phases. |
| LWA-DISK-013 | Preserved IPF receiver / native compatibility | Implemented bounded model; OPEN | Full Contact completes a disk-two fight replay; Beast responds to outdoor game controls after a correctly timed swap. Thunderbolt now passes protection and enters gameplay; disk-two handling remains unverified. Verify density/weak/no-flux against independent hardware traces; bounded native success is not receiver certification. |
| LWA-CPU-001 | 68000 trace exception in pinned Copper68k | Fixed in stable pin | Stable Copper68k `1.4.1` retains the trace correction and validated locality changes. `1.4.1-trace.1` passed scalar/batched vector-9 integration and Thunderbolt protection. Source belongs to CopperMod; the current package is on NuGet.org. See [CPU_TRACE.md](CPU_TRACE.md) for package history, architecture and coverage. |
| LWA-CPU-002 | CPU bus-transfer spacing / prefetch order | A1200 immediate MOVE corrected upstream; package integration and broader ordering OPEN | Lightweight supplies the existing CPU bus timing interface. The shared core now fetches aligned instruction longwords for the A1200 and advances the queue before immediate MOVE stores. The [ZooL 2 EC020 follow-up](CHALLENGING_GAMES.md#ec020-fetch-order-around-the-pointer-write) verifies the order independently of custom DMA and records the game replays. The CopperScreen package pin predates this change. Earlier CLR/MOVE prefetch and JSR target-fetch/stack-write differences remain open. See [the four-clock bus spacing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md). |
| LWA-CPU-003 | Additional CPU models / pinned dependency readiness | Experimental 68EC020/68020 prerelease published; broad conformance OPEN | Copper68k `1.4.2-ocs020.52` corrects Chip RAM instruction caching and extended MOVE source decoding, with tested continuation forms. Both models complete the original 18,000-field Lotus III script through a driving demo and menus; 95 captured files match across 68020 normal/scalar and EC020 normal. The 68000 replay retains all 95 baseline files. See the [correction, release and limits](LOTUS_III_020_CACHE_2026-09-26.md), retained [lost-edge investigation](LOTUS_III_020_INVESTIGATION_2026-09-26.md) and [initial implementation](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md). Player-controlled racing, full compatibility and physical timing remain unverified. Experimental 030/040 are now available with the cache corrections and native checks in [the newer CPU record](CPU_OPTIONS_030_040_060_2026-09-27.md). 68010 and 040 JIT are now integrated through the Copper68k 1.5.3 release; 060 desktop OS validation remains open. See [current CPU support](CPU_SUPPORT.md). |
| LWA-CPU-004 | 68060 guest FPU and OS task frames | VERIFIED for the native Kickstart 3.1 fixture | SetPatch and MuLib 46.4 enable the FPU and preserve task state across waits and preemption. Copper68k 1.5.7 fixes the double address update on the tested unimplemented `FCOS.S` postincrement and predecrement forms. See the [A1200 FPU replay](CPU_SUPPORT.md#a1200-fpu-replay-for-copper68k-157) and [earlier native frame evidence](CPU_OPTIONS_030_040_060_2026-09-27.md). |
| LWA-CPU-008 | MMU/fault continuation and physical CPU conformance | OPEN: translation implemented; recovery incomplete | 040/060 three-level paging, 4/8 KiB pages, permissions, timed U/M updates, PFLUSH/PTEST/PLPA and translated fault stacks are implemented. Complete 010 internal format-8 restart, 040 format-7 writeback/continuation, partial operand recovery, 030 PMMU, external 68881/68882 and physical cache/pipeline timing remain open. General-purpose virtual-memory OS operation is not qualified. See [scope and tests](CPU_SUPPORT.md). |
| LWA-CPU-010 | Detroit AGA intro exit | Fixed in published Copper68k `1.5.4` | Mainline's general logical handler supports the reached `$0A18` postincrement EORI form. The qualified candidate and stable review package each repeat 34,000 fields in normal/scalar modes with all 1,027 captures and final fingerprints matching, completing January 1908 into February with the report/back-button interaction inspected. No guest/media patch or unsupported bypass was used. Saves, later gameplay and physical CPU timing remain unverified. See the [stable release and exact-package checks](COPPER68K_1_5_4_RELEASE_2026-10-22.md), the historical [candidate qualification](COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.md), [dedicated correction](DETROIT_AGA_FIX_2026-10-08.md) and [original failure](DETROIT_AGA_2026-10-08.md). |
| LWA-CPU-009 | JIT breadth and native performance qualification | OPEN: optional 040 JIT integrated | Compiled execution, immutable snapshots, CPU/DMA/hardfile/map invalidation and interrupt/device boundaries are integrated and checked; native Kickstart loads original guest code that executes compiled instructions. MMU/TT uses the interpreter. Broad native gameplay parity, host-throughput acceptance and JIT on other CPU models remain open. See [scope and tests](CPU_SUPPORT.md). |
| LWA-CPU-CAS2 | 68040 failed CAS2.L | Physical read order open | With distinct operand addresses A and B, Copper68k reads A, reads B, then writes the saved value at A. The [programmer’s manual, p. 4-68](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf) identifies destination 1 as the writeback target. The [68040 user’s manual, §7.4.5](https://www.nxp.com/docs/en/reference-manual/MC68040UM.pdf) identifies the second operand read. A physical address/RW trace with distinct operands would distinguish reversed reads from a different writeback target. |
| LWA-CPU-005 | 68040 / Lotus III disk-two prompt | Uncached instruction-fetch defects corrected; original replay passes; full 040 timing OPEN | Aligned longword fetches and a half-line holding register replace per-word fetches; integer fallback uses the same frontend. The original field-6,500 press is consumed and the unchanged replay reaches a driving demo; all 95 normal/scalar captures match. See [correction and limits](LOTUS_III_040_FETCH_2026-09-27.md). The [earlier failed replay and phase variant](LOTUS_III_040_INVESTIGATION_2026-09-27.md) remain historical evidence. Speculative prefetch, pipeline overlap and physical timing remain unverified. |
| LWA-CPU-006 | 4th Dimension / Kick Off 3 native A1200 startup | Fixed in Copper68k 1.5.3 | CopperMod PR #19 implements the initial CMP/MOVE blockers and further reached MOVE, EOR, ADDI, OR, byte ADDQ and byte immediate ASL forms. 4th Dimension reaches effects with audio; both 12,000-field modes match all 151 captures. Kick Off 3 loads disk 2 and accepts joystick selection into Practice options; both 15,000-field modes match all 367 captures. Football gameplay and continuous visual stability remain unverified. The Copper68k 1.5.3 release retains these corrections. See [correction and identities](AGA_ADDITIONAL_MEDIA_FIXES_2026-10-03.md); the [eight original failures](AGA_ADDITIONAL_MEDIA_2026-10-03.json) remain historical evidence. No guest/media patch or unsupported continuation was used. |
| LWA-CPU-007 | Brian the Lion native installation / level entry | Reached blockers fixed in Copper68k 1.5.3 | CopperMod PR #19 implements sixteen reached operand families and corrects byte/word absolute MOVE decoding. The supplied native unpacker installs the hard-disk rip; both corrected 15,000-field runs reach the jungle level with all 367 captures matching. A control confirms input-dependent guest position state, but visible movement, sound and later gameplay remain unverified. The Copper68k 1.5.3 release retains these CPU fixes. See [identities, original failures and limits](BRIAN_THE_LION_2026-10-03.md). |
| LWA-CPU-011 | Stock AGA 68EC020 interrupt recognition | VERTB polling verified against WinUAE | Prepare the pending interrupt after the pin is stable and enter after the next instruction boundary. The synthetic poll and original Alien Breed 3D fade and first room pass. Physical 14 MHz sampling and mask-transition cases remain open. See [the game and probe evidence](CHALLENGING_GAMES.md#alien-breed-3d). |
| LWA-AUDIO-004 | CPU-fed word after DMAOFF | Fixed bounded transition | Queued AUDxDAT now survives the last DMA word and raises its manual-playback IRQ. Four discriminating channel/byte-phase cases and Thunderbolt gameplay pass. Exact physical interrupt phase remains within the audio uncertainties above. |
| LWA-MEM-001 | True Fast RAM | Implemented; bounded native validation | Optional 512 KiB–8 MiB Zorro II RAM, guest Autoconfig, CPU-only mapping, settings and runner support. Published CPU `.54` includes `.53`'s PC-indexed LEA and the subsequent [020/EC020 native allocation and OFS proof](COPPERHDF_020_2026-09-27.md). See [initial evidence and limits](FAST_RAM_2026-09-27.md). Virtual Zorro III RAM up to 1 GiB is now available with full 32-bit CPUs on A500 and A1200; see [CPU/memory upgrade verification](CPU_MEMORY_UPGRADES.md). Physical accelerator-local topology remains unmodeled. |
| LWA-MEM-002 | Explicit Agnus / configurable Chip RAM | Implemented; native boot and bounded DMA validation | PAL 8371 / 512 KiB, 8372A / 512 KiB or 1 MiB, and 8375 part 318069-10 / 1 or 2 MiB; larger Chip layouts use no slow bank. Native KS 3.1 discovers correct memory and boots/writes/reopens seven configurations. KS 1.3 OFS controls and unchanged default captures pass; see [evidence](AGNUS_CHIP_RAM_2026-10-01.md). ECS display and DOFF are implemented in the [subsequent record](ECS_DISPLAY_2026-10-01.md). Electrical unfitted-bank readback and revision-specific 8372A internal A20 rollover remain unverified. Generic unspecified ECS profiles remain unavailable. |
| LWA-ECS-001 | ECS display | Implemented; bounded native and diagnostic coverage | Explicit 8373, SuperHires palette/sprites/DMA, DIWHIGH, programmable blank/sync/beam, DOFF and safe host resizing. Native stock PAL SuperHires (progressive/interlaced), guest-programmed NTSC SuperHires, HD boot/write/reopen and programmed 640x480 have evidence. Stock NTSC/VGA monitor launchers are absent from supplied media; analog genlock, A2024 and broad silicon-edge coverage remain outside this record. See [ECS evidence](ECS_DISPLAY_2026-10-01.md). |
| LWA-AGA-001 | Alien Breed 3D II completed-raster intermittence | Observed; cause and continuous visual stability OPEN | The 2 MiB opening level responds to movement, turning and firing. Some completed rasters omit most of the viewport or HUD in both modes; field 70,000 has the scene/HUD, while 69,500 and 72,000 are incomplete. Retain the original captures and establish guest/Copper/window/IRQ ordering against an independent reference before changing timing. See [bounded gameplay evidence](AGA_GAMEPLAY_2026-10-02.md). |
| LWA-AGA-002 | Kick Off 3 completed-raster intermittence | Historical cause OPEN; current bounded Practice replay stable | The [October 3 replay](AGA_ADDITIONAL_MEDIA_FIXES_2026-10-03.md) showed Practice at field 9,500 and mostly blank output at 12,000. Its original input/captures are unavailable. The [October 8 investigation](KICK_OFF_3_BLANK_FIELDS_2026-10-08.md), using current source and Copper68k 1.5.3, confirms guest palette blanking for 11 transition fields followed by visible Practice through field 15,000; all 907 capture files match the production scalar runner. WinUAE also renders Practice, without field alignment. No engine timing change was made; changed input/build prevent attribution of the historical observation to a specific fix. Football gameplay remains unverified. |
| LWA-AGA-003 | Brian the Lion visible player / gameplay | Scene rendered; player visibility OPEN | The corrected native hard-disk rip renders the jungle and advancing timer, but sampled rasters do not establish a visible player or correct locomotion. Input changes the guest position state; sampled PCM remains zero. Obtain an independent reference for player rendering, direction/displacement and sound before declaring gameplay verified. No CD32-specific device blocker has been demonstrated. See [replay and control](BRIAN_THE_LION_2026-10-03.md). |
| LWA-HDF-001 | CopperHDF integration | Implemented; bounded native OFS and FFS proof | Native KS 3.1 FFS/RDB boot/write/dispose/reopen passes at 0 and 2 MiB Fast RAM after correcting the initial DOS lock; see [evidence and limits](KICKSTART_31_HD_2026-09-30.md). Retain KS 1.3 OFS partition/RDB evidence; other filesystem handlers need supplied media. Physical IDE/SCSI and host directories are separate features. The 020 indexed-JSR blocker at `$FC4E28` and subsequent reached operand gaps are [resolved in published CPU `.54`](COPPERHDF_020_2026-09-27.md): 020/EC020 native boot and OFS I/O pass with and without Fast RAM. |
| LWA-VIDEO-002 | Hires transitions | Unverified; OPEN | Investigate supported raster/scroll/mode-transition failures. |
| LWA-VIDEO-005 | HAM edges | Modes implemented; physical edges OPEN | See [narrow Lisa HAM](#narrow-lisa-ham). Establish hold reset, mid-line mode and sprite interaction phases. |
| LWA-VIDEO-006 | Sprite sequencing edges | Early-blank and direct-position DMA faults repaired; edges OPEN | CPU/Copper POS/CTL writes update Agnus as well as Denise; Superfrog's character is visible. Verify remaining comparator, manual-data and stolen-slot boundaries. |
| LWA-VIDEO-008 | Dual-playfield edges | OCS/ECS dual HAM and priority codes 5–7 modeled. Lisa priority codes 5–7 have pixel tests and priority 7 has native game coverage. Physical phases OPEN. | See [nonstandard OCS modes](NONSTANDARD_OCS.md) and [Lisa priority verification](#lisa-dual-playfield-priorities-57). |
| LWA-COPPER-001 | WAIT wake-up under bitplane DMA | Confirmed early colour write; repaired 2026-09-18 | WAIT wake-up now yields to higher-priority DMA. Six-plane Beast border starts at x288 instead of x272; see the [native record](../NATIVE_VALIDATION.md#copper-wait-wake-up-correction-2026-09-18). Other undocumented control-state edges remain unverified. |
| LWA-CIA-001 | Board TOD pulse phase | Unverified; OPEN | Obtain board pulse-to-counter/IRQ evidence. |
| LWA-CIA-002 | TOD comparator / reset | Unverified; OPEN | Establish write-trigger and latch/reset behavior. |
| LWA-CIA-003 | CIA-to-Paula synchronization | Lost held IRQ repaired; sub-CCK edges OPEN | Obtain acknowledgement/INTREQR/IPL phase evidence. |
| LWA-IRQ-001 | Paula software request bit 14 | Implemented; bounded propagation | Software INTREQ bit 14 latches, reads back, acknowledges and asserts IPL6 under the existing master gate and one-CCK visibility model. Enabling the master alone creates no request; ordinary hardware source scheduling is unchanged. Four discriminating tests cover the latch, gate, delayed Copper set/clear and stopped-CPU wakeup. Brian's title input handler now runs. Physical propagation remains unverified; see [evidence](BRIAN_THE_LION_2026-10-03.md). |
| LWA-INPUT-001 | Keyboard startup / resync | Digital protocol implemented; MCU/electrical detail OPEN | See [keyboard/CIA record](KEYBOARD_CIA.md); verify physical scan/reset timing. |
| LWA-INPUT-002 | Keyboard / CIA serial pins | Serial/CNT/port modes implemented; physical edges OPEN | Verify CIA pipeline and pin/IRQ phases against hardware. |
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
| Beam and synchronization | VHPOSW and VPOSW beam repositioning beyond LOF; external genlock source/pulse qualification and physical display synchronization. ERSY absent-source hold/recovery is implemented as a bounded first slice; see LWA-VIDEO-009. |
| Nonstandard display and blitter modes | HAM/BPU combinations and priority codes 5–7 have a bounded display implementation. Line-mode BLTSIZE widths other than two remain explicitly rejected: hardware photographs contradict the available emulator reference. See [the investigation](NONSTANDARD_OCS.md). |
| Disk-controller edge behavior | Active DSKLEN reprogramming without cancellation, WORDSYNC changes while waiting for first sync, FIFO overrun behavior and simultaneous selected read streams. Preserved-track writes and fully physical write splices/precompensation are absent. |
| A500 board I/O | Digital keyboard startup/recovery, physical key state/reset chord, CIA serial/CNT and timer-port modes are implemented. MCU scan/debounce, reset electrical timing and CIA silicon pipelines remain unverified. Host parallel transports remain separate. |
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

Next hardware work is remaining beam/synchronization and CIA pin/pipeline evidence,
then the explicitly rejected disk and nonstandard register cases,
prioritizing any demonstrated native failure. Each slice needs focused hardware
expectations and its own unchanged-work performance comparison. The latest scoped
performance acceptance does not close these gaps. NTSC is also missing for a
broader motherboard profile; implemented ECS display on a PAL oscillator, AGA, newer CPUs and expansion/storage devices are separate
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

DSKBYTR.DMAON now follows the active transfer latch gated by both DMACON enables;
the first DSKLEN strobe does not enable it, and completion clears it. Waiting for
sync is active; write prefetch completion is not serializer completion. Direction
still reflects raw DSKLEN. Specification/model tests cover start, pause, cancel,
reset and read/write completion; exact physical readback phases remain open.
Zero-length synchronized reads now wait for a qualifying match before DSKBLK.
See [the evidence and limits](DISK_CONTROL_EDGES.md).

### LWA-DISK-007 — Sync, byte status and reset

The live shift register is compared on incoming bits and DSKSYNC writes. A new
match requests a sync interrupt with provisional four-CCK CPU visibility;
DSKSYNC resets to `$4489`. Received WORDSYNC now aligns the CPU byte counter even
without DMA; register-only comparisons retain its phase. The
[Alien Breed correction](DISK_BYTE_WORDSYNC_2026-09-24.md) has focused phase/readback
and native boot/menu coverage. Comparator propagation, reset seed, coincident
read/write phase, combined MSBSYNC/WORDSYNC and IRQ visibility remain unverified.
Retain documented following-word WORDSYNC coverage.

### LWA-DISK-008 — Ideal-ADF mechanics

Standard encoded ADF tracks use nominal 300 RPM for DD or 150 RPM for HD and a new motor/media run starting
at track bit zero after readiness. Insertion phase, coast-down, seek settling and
speed variation are not fully modeled. ADF sectors are not physical flux captures.
Inward over-seeks now clamp without stopping guest execution. Existing model
limits remain 79 for standard sector/empty drives and 83 for preserved IPF/extended ADF/SCP tracks; actual mechanical travel and
media-independent bounds remain unverified. See the
[Desert Strike comparison](DESERT_STRIKE_END_STOP_2026-09-24.md).
Add effects only for demonstrated supported-workload needs with bounded evidence;
broader protection/flux support remains outside scope.

HD sector ADF/ADZ, media-dependent serial drive IDs and 150 RPM index timing
are implemented with bounded CopperStart full-ROM read/write/export/reopen
coverage; see [HD support and qualification](HD_FLOPPY_2026-10-18.md). HD
preserved/flux formats and physical-drive timing remain unqualified.

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
WinUAE comparisons are retained with the [original evidence](https://github.com/ilehtoranta/CopperScreen/blob/f83c9092efa9e75d930cb602415a2b8da1637db5/docs/history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).
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
undocumented sprite priority/transparency need evidence. Low-plane/hires HAM,
BPU=7 and dual HAM now have bounded decoding and native probe coverage; see
[the evidence record](NONSTANDARD_OCS.md). Native artwork/menu success does not
verify these transitions, including held colour across resolution changes.

#### Narrow Lisa HAM

The supplied Roots 2.0 AGA disk stopped the native runner at field 22,402
on the PAL A1200 Kickstart 3.0 profile. Its Copper list wrote
`BPLCON0=$4A00` during the late effect, then restored `$6A00` before the
field-end capture. The four-plane interval keeps the HAM bit set.

With one to four active planes, Lisa applies the HAM6 decoder with both
unfetched control bits clear before the palette XOR. The five focused cases
cover direct selection, XOR-controlled modification, raw component data and
the held colour. The [WinUAE HAM decoder](https://github.com/tonioni/WinUAE/blob/ee784914c8d61bb503d137e358b22fcc4758a294/drawing.cpp#L4444)
supports the model. Five- and seven-plane HAM remain explicit limits.

The corrected replay reached the face morph, credits and end screen, then
continued through field 28,500 with nonzero PCM and no unsupported-mode stop.
All 135 shared state, raster and Chip RAM captures before field 22,402 matched
the baseline byte for byte.

### LWA-VIDEO-006 — Sprite comparator and control edges

Premature PAL vertical-blank sprite requests are repaired; direct blank-end
scheduling preserves the legal line-25 start and accepted completions. Exact
VSTART/VSTOP equality/reuse, starts on the reset line, manual POS/CTL interaction
and partial control pairs under stolen slots remain unverified. Obtain physical
expectations before expanding or certifying the later range-based sequencer.

A CPX Kickstart 3.1 replay exposed a same-cycle collision between a pending
sprite DMA DATAA input and a Copper sprite-register output. The engine now
retires the existing Denise input after the current pixel step and before the
new bus output, preserving both values. A focused DMA/Copper test covers the
two successive inputs; this does not qualify the remaining physical sprite
comparator or stolen-slot edges.

The [Superfrog character follow-up](SUPERFROG_CHARACTER_2026-10-20.md) resolves
the earlier visibility uncertainty. CPU/Copper POS/CTL writes now update
Agnus's vertical controls and reprogram an exhausted channel, while preserving
the mandatory field reload and a pending control pair. The character is
visible during movement, jumping/landing, life loss and checkpoint respawn
under both CopperStart and Kickstart 1.3. Five new cases are included in the
1250-pass engine suite; seven optional native skips remain unavailable.
All 670 final normal/scalar capture files match. This repairs direct-position
DMA programming, not all manual-data or physical comparator/slot edges.

The [Worms DMA follow-up](WORMS_SPRITE_DMA_2026-10-22.md) corrects the fixed
sprite and audio slot phase against HRM Figure 6-9 and removes a conservative
DDF/channel cap in favor of actual physical slot ownership. Worms' stale
sprite-6 data/stop words caused the reported blue bars; the audio correction
also preserves Superfrog's character rendering. Five discriminating new cases
are included in the 1255-pass engine suite, with seven optional native skips
still unavailable. This bounds ordinary audio/sprite/early-DDF interaction;
the comparator, partial-control and undocumented edges above remain open.

### LWA-VIDEO-008 — Dual-playfield verification boundary

Ordinary OCS lores/hires dual playfield is implemented, including interlaced fields,
independent scrolling and sprite masking by both opaque playfields. Priority codes
0–4 follow the Commodore HRM; codes 5–7 and dual HAM now have bounded decoding
and native probe coverage in [NONSTANDARD_OCS.md](NONSTANDARD_OCS.md).
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

Digital cold synchronization, FD/held-key/FE startup, F9 retransmission, physical
key state, Caps Lock, overflow and A500 reset-chord handling are implemented.
A timeout now recovers instead of parking unsupported. The independently powered
keyboard survives CPU RESET. Native ROM boot/reset and the exact model boundaries
are recorded in [KEYBOARD_CIA.md](KEYBOARD_CIA.md). MCU scanning/debounce, self-test
failure and electrical reset-pulse duration remain unverified/unmodeled.

### LWA-INPUT-002 — Keyboard and CIA serial pins

The 142-cycle keyboard phases and eight-cycle CIA/Paula IRQ model remain. General
CIA serial output, holding buffer, CNT input/gated timers, PB6/PB7 outputs and
FLAG/PC digital interfaces now have focused tests. These are specification/model
checks, not measured pin propagation. The existing timer load/underflow pipeline,
ICR/sub-CCK synchronization and board electrical phases remain open; isolate the
first differing phase before changing execution. See [KEYBOARD_CIA.md](KEYBOARD_CIA.md).

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
historical test results are retained in the [original issue register](https://github.com/ilehtoranta/CopperScreen/blob/f83c9092efa9e75d930cb602415a2b8da1637db5/docs/history/amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md).
No historical test count is presented as a newly run suite.

The 2026-11-24 *Amiga Master* replay found an OCS display-fetch timing defect.
Agnus must release the DDF start limit with bitplane DMA enabled even while
`BPLCON0` selects zero planes. The demo selects three planes between that edge
and `DDFSTRT`. CopperScreen now fetches on the same raster line, and the logo
matches WinUAE 6.0.3 pixel for pixel at the aligned checkpoint. The regression
`SettingPlaneCountBeforeDdfStartFetchesOnTheCurrentRasterline` checks the first
accepted fetch. WinUAE screenshot timing can repeat an earlier framebuffer
during motion. At the checked scroller, the guest variables, Copper list and
bitplane data matched at aligned fields in both emulators.

The full PAL A500 replay used WinUAE 6.0.3 uncompressed video and aligned the
fields at guest interrupt-vector installation. The common viewport was
724 × 287 pixels. With the DDF correction in place, 17,805 of the first
17,819 fields were pixel-identical and the sampled guest state matched on all
17,819. The rotating object exposed a bitplane-memory difference at identical
geometry. The later scrolling state was 45 fields ahead in Lightweight.

Three bounded OCS line-blitter probes reproduced the causes of specific
differences. Single-dot mode still fetches C when it suppresses a repeated
D write on the same row. The final signed error reaches BLTCON1, so a second
plane can reuse the control word with the correct starting sign. With the B
channel disabled, the first C output is five CCKs after BLTSIZE and the first D
output is two CCKs later. Both outputs were one CCK late in the previous engine.
The focused regressions cover the suppressed write, repeated line, and startup
timing.

The corrected Release replay compared 31,310 fields through the return to
Kickstart. The first six affected 3D frames became pixel-identical. Of the
30,461 fields with both demos active, 18,561 matched at the same elapsed field.
The late scroll lead decreased from 45 to 36 fields. Aligning the late section
by that measured offset yields 11,422 exact images out of 11,425. The current
workspace build with Copper68k 1.5.12 reproduced the full framebuffer and guest
state captures from the controlled 1.5.10 build byte for byte.

The remaining phase difference is open. A larger timing difference begins
around a descending fill spanning the start of display DMA. Four matched
72-row, six-word area-fill probes with four display planes reached their final
D transfers at identical intervals from BLTSIZE. The bounded DBRA and
multiply loops also did not isolate an instruction defect. The full trace,
probe outputs and per-field pixel counts are retained under
`artifacts/the-company/full-parity/`. Release built with zero warnings and
errors. Engine diagnostics passed 1,459 tests with seven optional skips.

A follow-up verified that display DMA also stalls the OCS line-blitter
startup and internal phases. An idle phase and a suppressed single-dot write
still leave the physical bus free. With three display planes, a bounded line
finished 250 CPU clocks early before the correction. All six stopped-CPU line
probes then matched WinUAE's transfer addresses and timing from BLTSIZE.
`ThreeDisplayPlanesDelayInternalLinePhases` preserves the observed timing.

The follow-up Release replay completed 33,000 fields with Copper68k 1.5.12.
The comparison covered 31,310 fields through the return to Kickstart. Both
demos were active in 30,458 fields and 18,543 matched at the same elapsed
field. The late lead changed from 36 to 39 fields. At that measured offset,
11,416 of 11,419 late fields matched. Release built with zero warnings and
errors. Engine diagnostics passed 1,497 tests with seven optional skips.

The remaining drift builds during the rotating object. Masking the CIA music
interrupt in both emulators left the drift in place. A bounded replay of the
demo's rotation, projection and face calculations found the three measured
intervals together took 558 fewer CPU clocks in Lightweight with three display
planes. The two bitplane DMA streams used the same addresses and slot spacing.
The CPU traces exposed bus waits being absorbed into instruction-cycle floors
and a different divide prefetch phase. Three focused CPU tests fail on 1.5.12
and pass on the development build. A local Copper68k candidate preserving the
multiply and shift waits and issuing the divide prefetch after its internal
cycles reduced the isolated gap to 204 CPU clocks and the late demo lead to 19
fields. An additional candidate moved the long register clear and indexed
MOVEM bus phases. It reduced the isolated gap to 128 CPU clocks and left a
25-field lead in the full replay. Both development captures reached Kickstart.
The later candidate passed 335 focused Copper68k tests and the CopperScreen
engine diagnostic suite. The additional demo timing differences are open.
The production build at that stage used Copper68k 1.5.12. Captures and comparisons
are in `artifacts/the-company/drift-investigation/`.

The next investigation started from the 19-field development build. At the
first missed update the emulators wrote the same rotation value only 24 CPU
clocks apart. Lightweight started the second fill 662 clocks early and
cleared the frame-ready word before the Copper interrupt. WinUAE cleared the
word after servicing the interrupt and waited another field.

Development CPU corrections preserve the internal clocks that follow the
required prefetch for long register ADD, SUB and CLR. Indexed memory-to-register
MOVEM now charges two internal clocks after the index extension is ready.
The isolated three-plane calculation was eight CPU clocks early after those
changes, compared with 204 on the starting build. In the live trace the first
frame-ready clear moved 336 clocks later, reaching one CCK before the Copper
interrupt. The full native replay's late lead fell to eight fields. The
development package passed the eight focused CPU regressions and the engine
suite with 1,497 passes and seven optional skips. The trace and per-field
evidence are in `artifacts/the-company/drift-investigation/from-dev2/`.

Preserving post-prefetch execution time for immediate and address arithmetic
reduced the late lead to five fields. The following build also preserved the
register BTST tail and the indexed BCHG read, prefetch and write sequence.
It finished four fields behind the reference. Both bit-operation regressions
failed before their fixes. The combined build passed 346 focused CPU tests and
1,497 engine tests, with seven optional engine skips. A fresh complete WinUAE
run matched the archived capture's Copper interrupt flag in 2,210 sampled
fields and returned to Kickstart at the same elapsed field.

Separate rotation replays attributed the change in direction to register BTST
timing. The indexed BCHG change alone left the five-field lead. The combined
build matched 650 of 887 rotation updates at the same elapsed field, with the
first persistent lag at elapsed field 18,640. The full comparison included
31,310 fields, with 19,788 exact RGB images among 30,497 jointly active fields.
The verified changes are in local development CPU packages.

The first late-field trace used the frozen replay engine and a diagnostic-only
CPU package. The same rotation value was written within four CPU clocks of the
WinUAE trace. Both emulators observed the first area fill ready 7,028 CPU clocks
after BLTSIZE. Lightweight started the third fill about 14 CPU clocks later
than WinUAE. Lightweight's Copper handler set the flag before the guest
cleared it. In WinUAE, the guest cleared the flag before the handler set it.
This accounts for the first extra field in the combined build. The remaining
bus-timing difference before that interrupt is open.

The follow-up started from that same four-field build. During line drawing,
a wait on the first fall-through fetch of a non-taken MC68000 word branch
could let the branch retire before its second fetch. WinUAE performs both
fetches within the branch. Copper68k now completes the second fetch before
retirement, with the IPL sample at the earlier bus phase. The regression
covers scalar, kind-table and packed dispatch. A run that enabled the
correction only after the original rotation write still started the third
fill 14 CPU clocks late and missed the same Copper interrupt deadline.

The accepted blitter transfers agree in order, channel and address for all
16 line blits and three fills in that pass. In particular, the second fill
started 26 CPU clocks late but its final D transfer landed at the same
aligned raster slot. The guest's successful DMACONR poll still landed 12
clocks late, and contention added two more clocks before the third BLTSIZE
write. The traces move the remaining investigation upstream of that write,
to the CPU polling phase and its interaction with DMA grants.

The same trace matched all 9,000 sampled low-resolution OCS bitplane reads
by address. They occupied slots four CPU clocks earlier in Lightweight,
relative to the common Copper interrupt-request edge. A diagnostic shift of
the fetch phase aligned all 9,000 slots. It changed the timing of the first
two fills. The last poll and third fill reconverged to the original timing
with either the display change alone or both timing changes enabled for
that pass. Applying the fetch shift from reset also reduced exact RGB agreement
to 201 of the first 2,508 active fields and still returned at elapsed
field 30,501. A paired correction to the Denise raster cursor restored
2,505 exact images in that prefix. The paired full replay returned at
field 30,501 and matched 19,499 of the 30,497 jointly active images.
The experiments leave qualification of the display path open.

With the word-branch correction enabled from reset on the original display
path, the frozen engine returned to Kickstart at elapsed field 30,497,
matching a fresh WinUAE 6.0.3 run. The pixel comparison against the archived
lossless WinUAE capture covered 31,312 elapsed fields. It found
30,221 exact RGB images in 30,497 jointly
active demo fields. Most of the 276 differences are in the rotating
object sequence. Several differing images exactly matched a nearby
WinUAE field. The original four-field build returned at field 30,501.
The same CPU package with the later working-tree engine returned at
field 30,495. These results leave the local fill deadline and full
frame-by-frame parity open. The captures and comparisons are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/`.

The next follow-up captured CPU requests as well as grants in an instrumented
build of the WinUAE 6.0.3 source. The stock checkpoint that reproduces the
late third fill and the checkpoint from the fresh boot produced different
in-field histories for the same rotation value. In the original checkpoint
pass, all 379 matched `BTST` iterations used the same request spacing on both
emulators. Each emulator's successful `DMACONR` read was granted on its first
requested slot. The 12-clock difference was already present at request time.

Upstream, the trace showed missing internal clocks following a contended final
prefetch in `BCHG #3,D7`, `EXG D1,D3`, and long memory-to-register `ADD`.
Copper68k now retires these instructions with their internal tails after the
final refill. Eleven timing regressions cover those instruction families,
including `SUB` and the other immediate register bit changes, with a modeled
DMA wait. In the corrected trace, all matched intervals from the relevant
final refill to the next request agree for `BCHG`, `EXG`, and `ADD`. The
bit-change correction alone left the original poll slot unchanged. Combining
the CPU corrections and aligned display fetch in that pass moved the
successful request to 16 clocks late, with six more clocks waiting for the
grant. A diagnostic change to the upstream `MOVE` prefetch order moved the
initial rotation-value write four clocks earlier. Both runs reached the
same third fill slot. The original deadline remains open.

Full replays from reset with the CPU corrections returned at elapsed field
30,501, four fields behind a fresh WinUAE run. The bit-change-only replay
matched 19,623 of 30,497 jointly active images against the lossless WinUAE
capture. Correcting all three CPU instruction families matched 19,790. The
combined CPU and display diagnostic build matched 19,853 and returned at the
same field as the CPU-only build. The same-finish baseline above remains the
closest of these full replays. The request traces, fixture and replays are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/poll-followup/`.

The next trace used the same-finish CPU and engine again. It captured each
physical CPU word grant and the DMA owner of a denied request. The first
split with a shared request and preceding grant was at line 0, CCK 13. WinUAE
let the blitter write `$015D16` there and granted the fetch of `$031A02` at
CCK 15. Lightweight had written the same word at CCK 4 and granted the
fetch at CCK 13. The direct WinUAE RGA trace shows that CCK 4 belongs to the
Copper's restart dummy read at the previous list PC. The OCS Copper now takes
that output slot before fetching COP1LC with DMA already enabled. A regression
checks both the bus ownership of the dummy and the first MOVE's output time.
The corrected trace places the write to `$015D16` at CCK 13 and the CPU grant
at CCK 15 in both emulators.
The original CPU traces rejoined at line 4, CCK 18 after executing the same
159 loop-extension fetches since their shared grant at line 0, CCK 11.

The next ten splits before line 111 with a shared request were audio grants.
The first denied the `$031A02` fetch at line 28, CCK 17 in WinUAE. Lightweight
had assigned the channel 0 request at line 27, CCK 7 to that same line's slot.
WinUAE samples the Paula requests at CCK 4 on these aligned coordinates. The
OCS audio scheduler now defers requests raised after that sample to the next
line, while keeping a request raised on the sample clock eligible. Five timing
regressions check CPU contention at the sample boundary and after a late
request. In a controlled trace, the fix aligned all four channels' 541 audio
reservations across 267 lines with WinUAE. With both corrections, the next
observed grant split after a shared request and preceding grant is at line
210, CCK 144. The successful poll moved from eight to six clocks late in
this pass. The traces and full replays are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/grant-followup/`.

A fresh WinUAE 6.0.3 replay finished at elapsed field 30,497. Compared
field by field against the lossless WinUAE capture, the isolated Copper
restart fix finished at 30,498 with 8,072 of 30,497 active images exact.
The isolated Paula fix finished at 30,501 with 19,609 exact images. Combining
both fixes finished at 30,502 with 7,653 exact images. The same-finish baseline
matched 30,221 at elapsed field 30,497. A diagnostic one-field shift of the
Copper-only replay matched 30,218 of the 30,496 overlapping active images,
which accounts for most of its loss of exact same-field matches. The
individual DMA ordering fixes are covered by the controlled trace and
hardware-phase tests. Full replay qualification remains open.

The Copper-only follow-up traced the one-field difference to the first
guest interrupt. The demo installs vector `$6C` and starts a Copper list
whose first WAIT is `$6001`. The Copper-only build installs the vector at
line 272, after the eight-bit vertical comparator has wrapped. Its first
sampled field still has the initial loop count of 180. The pre-restart
control installs the vector at line 241 and has already decremented the
count to 179 when the capture sees the vector. The disk load and subsequent
depacker reach the launch at different beam positions.

WinUAE 6.0.3 can reproduce either startup under the same machine settings.
With `rndseed=1`, the vector is installed on line 233 and the initial
sampled count is 179. With `rndseed=2`, it is installed on line 298 and the
sampled count is 180, as in the Copper-only build. Leaving the seed open
lets the CPU reset delay and initial floppy position vary between runs.
Record the seed, timing profile and first-IRQ witness for comparisons that
use vector installation as their elapsed-field origin. Pinning the original
capture seed (`1795547020`) reproduced its vector-entry field and all 370
checked RGB images, qualifying the new recorder's field indexing.

The fresh Copper-only capture reproduced the previous capture byte for byte.
The full seed-2 WinUAE replay and Copper-only replay both finished at elapsed
field 30,498 and matched 30,219 of 30,498 active RGB images at identical
elapsed field indices. The strict comparison to the original WinUAE capture
still matches 8,072 of 30,497. Comparing the two WinUAE captures at those
same indices yields 7,986 exact images. The seed controls the startup
field difference. The remaining 279 differing fields leave pixel parity
open. The follow-up captures are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/field-followup/`.

The pixel-parity follow-up kept the frozen Copper-only engine and the same
seed-2 reference. The first sustained rotation lead follows the guest's
frame-ready clear between elapsed field samples 17,842 and 17,843. Both
emulators wrote the same angle `$3B21` at line 107, CCK 86. At the first
indexed `MOVE.W` to an absolute-long destination, Lightweight requested the
source refill one CCK earlier. WinUAE requested the next slot and waited
through a bitplane transfer. Lightweight also fetched the successor opcode
before the destination write. The MC68000 timed path now accounts for the
two index clocks before the source refill and fetches both successor words
after writeback. A contended-fetch regression covers all three interpreter
dispatch modes.

The corrected trace matched the requests and grants for all six indexed
angle-table reads in this rotation. The first line BLTSIZE moved from 72
CPU clocks early to 12 clocks late. The third fill still started 72 clocks
early and the guest still cleared the ready flag before the Copper handler.
The full local development replay recovered elapsed field 18,147 and
introduced no new mismatches. Both emulators finished at elapsed field
30,498, with 30,220 of 30,498 active RGB images exact at the original field
indices. A separate replay using a rebuilt engine with the working-tree line
blitter changes matched 27,568 images at the same finish field. It changed
the earlier display sequence and was dropped as a parity candidate.

A field-local absolute-long MOVE source refill correction aligned the operand
requests in the angle routine. A separate field-local Paula DMAL change
aligned all 144 CPU requests and grants across lines 158 through 161. The
third fill began at the same slot in both experiments as in the indexed MOVE
build. Applying the absolute-long source change from reset moved the vector
entry one raw field earlier and matched 7,921 of 30,497 jointly active images.
Reapplying the independently tested MC68000 register bit-change, EXG and long
memory arithmetic tails only during the affected field aligned the guest's
frame-ready clear with the WinUAE interrupt order. The first changed request
followed the contended final refill of `EXG D2,D3` at `$031EFA`. The guest
sample at elapsed field 17,843 then matched the WinUAE ready flag, angle and
buffer selection. The next divergence in those guest samples was at elapsed
field 18,359, where the third fill was 74 CPU clocks early. The timing window
was an instrumentation control.

Enabling those three corrections from reset with indexed MOVE reduced the
rotation ready-flag disagreements from 80 to six across 813 samples, then
returned four fields late and matched 19,789 of 30,498 jointly active RGB
images. Enabling just the EXG correction with indexed MOVE returned at
elapsed field 30,498 and matched 30,451 of 30,498 active RGB images without
shifting the captures. This recovered 231 additional fields with no new
mismatches. The remaining 47 differences include a second missed rotation
deadline after elapsed field 18,358. Adding either the bit-change or the
long memory arithmetic correction to that build made the demo finish four
fields late. The respective comparisons matched 19,790 and 19,852 active
images. A replay that computed the viewport RGB hashes directly reproduced
all 31,311 comparison rows of the lossless EXG capture.

The indexed MOVE correction is in CopperMod. These replays used local
development CPU packages. Request traces and per-field comparisons are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/pixel-followup/`.

The next trace starts from indexed MOVE plus EXG and keeps the seed-2
WinUAE reference. At angle `$289A`, Lightweight starts the third fill
42 CPU clocks early, at line 254 CCK 222 versus line 255 CCK 16.
The guest clears the ready word before servicing the Copper interrupt.
The first different ready-word sample is elapsed field 18,359.

Both CPUs receive the first `MOVE.W (A7)+,Dn` stack operand at line 182
CCK 8 after a refresh wait. WinUAE refills `$031F5E` at CCK 10 and then
reads the next stack operand at CCK 12. The Lightweight optimized CPU
path read the next operand ahead of the previous refill. Copper68k now
completes the required prefetch before the next instruction can issue
its operand. A unit regression with a waited stack read fails on both
old optimized dispatch paths and passes on scalar, kind-table and packed
dispatch with the correction. In the field-local trace, the prefetch and
operand grants match WinUAE. The third fill remains early.

The next traced split follows `BCHG #3,D7` at `$031ED4`. WinUAE keeps
the two internal clocks after the refill at line 184 CCK 150. Its next
request waits for bitplane DMA, while the starting build requests the
earlier free slot. Enabling that timing correction from reset closes
field 18,359, then finishes four fields late and matches 19,790 of 30,498
active images. Applying the stack-prefetch correction from reset changes
the raw demo entry from field 1,692 to 1,691, finishes three fields late,
and matches 7,679 active images. These results leave full parity open.

With the original startup held fixed, the stack refill, register BCHG
tail and long memory ADD/SUB tail together restore the interrupt order
at this deadline. The ready flag and all ten RGB images from elapsed
field 18,358 through 18,367 match WinUAE. The third fill starts after
the handler in this bounded run. The field-local timing windows are
diagnostic controls. Removing the windows changes the raw demo entry
to field 1,691. The full combined replay finishes one elapsed field
early and matches 7,925 of 30,497 jointly active images against the
fresh seed-2 reference.

A fresh WinUAE replay produced all 31,385 reference viewport hashes
byte for byte. The fresh full indexed-MOVE plus EXG replay reproduced
all 31,311 comparison rows, matching 30,451 of 30,498 active images and
finishing at the same elapsed field without shifting the captures.
The artifacts are under
`artifacts/the-company/drift-investigation/from-dev2/fill-deadline/deadline-18359/`.

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
| LWA-VIDEO-010 | Wrap the Copper horizontal comparison at the nominal line boundary. | Four `LightweightCopperLineWrapTests` cases and paired native/CopperStart Superfrog replays repair the repeated bottom strip; see [investigation](SUPERFROG_VIEWPORT_2026-10-16.md). No crop or firmware workaround; full-game/audio fidelity and physical coincident-edge qualification remain unverified. |
| LWA-DISK-009 | Execute slow-mode ideal-ADF reception with a bounded recovery window. | `LightweightDiskSerialTests` and recorded native replay; rate-switch phases remain LWA-DISK-010. |
| LWA-INPUT-004 | SDR is a holding buffer; reject actual clocked output, not an unclocked handshake write. | `LightweightKeyboardHandshakeTests` and native Return press/release replay. Clocked output is now implemented; see KEYBOARD_CIA.md. |
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

Dual-playfield HAM and priority codes 5–7 now have bounded decoding coverage.
Nonstandard line-mode BLTSIZE widths other than two are also rejected. The
2026-09-17 collision/UART gaps now have implementations and focused/native
coverage. Their original OCS candidate and later candidates have separate scoped
performance decisions in [PERFORMANCE.md](PERFORMANCE.md); physical verification
remains distinct from that acceptance.

Active DSKLEN reprogramming without cancellation and FIFO overrun currently report
unsupported. Initial-wait WORDSYNC changes have a [bounded implementation](DISK_INITIAL_WORDSYNC.md);
physical start-gate/alignment edges remain unverified. Standard-ADF write DMA and MSBSYNC
byte alignment are implemented in the ideal bit-cell model. Preserved-track/flux
formats, analog precompensation and general GCR media admission remain outside
the standard-ADF scope. Slow-mode input retains the approximation above.

### Lisa dual-playfield priorities 5–7

Alfred Chicken AGA stopped the native runner when it reported
`BPLCON2=$003F` as unsupported in eight-plane dual-playfield mode. The desktop
session also treats that report as fatal. On the supplied HLM disk pair, the
baseline `c245c89` stopped at field 5,607 after swapping Disk 2 into DF0.
The stock PAL A1200 profile used Kickstart 3.0 revision 39.106, 68EC020
and 2 MiB of Chip RAM.

Lisa's existing lookup places all sprite groups ahead of a playfield at
priorities 4–7 and preserves its palette colour and opacity. The
[WinUAE AGA decoder](https://github.com/tonioni/WinUAE/blob/ee784914c8d61bb503d137e358b22fcc4758a294/drawing.cpp#L4522)
and [sprite mask](https://github.com/tonioni/WinUAE/blob/ee784914c8d61bb503d137e358b22fcc4758a294/drawing.cpp#L3239)
support this interpretation. The OCS/ECS colour substitution is specific to
those chipsets. Three pixel tests verify both field orders, sprite overlay,
individual field colours and transparency. The previous guard failed all three
after the pixel checks passed.

The corrected runner matched 114 pre-stop state, image and Chip RAM captures
through field 5,550. It completed 7,300 fields and reached interactive Stage 1
with visible joystick response and nonzero PCM. This replay covers the game
handoff and opening stage. Physical Lisa priority transitions remain open.

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
- **LWA-VIDEO-009:** ERSY with no external HSYNC now holds H0 and the current V,
  preserves CPU/device time, and resumes DMA at the new beam phase. See
  [the bounded implementation and hardware-probe record](BEAM_SYNC.md).
  VHPOSW/VPOSW counter repositioning beyond LOF, external pulse input/qualification,
  and physical Denise/monitor behavior during lost sync remain open. ERSY1/2
  pre-hold samples still expose a CPU/interrupt/register phase discrepancy versus
  A500 photographs. Hardware certification and the new performance gate remain
  separate; this is not an unrestricted beam/synchronization completeness claim.

Keyboard/CIA digital functionality is recorded in [KEYBOARD_CIA.md](KEYBOARD_CIA.md).
MCU and CIA physical/pipeline accuracy boundaries remain open above.

Legacy boot restoration is outside the current product task, not a timing defect
or a dependency of Lightweight. The [earlier restoration plan](../../CopperScreen/COPPERSTART_RESTORATION.md)
is superseded; full replacement-OS work belongs to CopperStart.
Other chipset/CPU profiles, RTG, physical IDE/SCSI controllers and save-state
compatibility are outside current product scope.

**LWA-RTC-001:** Optional Legacy A501 RTC registers are ported into Lightweight,
with desktop configuration and runner `--rtc`. Host local time plus a guest-set
session offset is retained through resets and enabled restart candidates.
Battery-file persistence, physical HOLD/STOP phases, TEST/adjust behavior and other silicon
models remain unsupported; see [RTC scope and validation](RTC.md).

The storage change also repairs TOD high-byte reads while CRB.ALARM is selected:
they no longer leave a stale persistent TOD latch. Operation Thunderbolt passes
its PAL count check after this correction. The independent CPU trace defect and
subsequently exposed audio transition are repaired in the current mainline build
using public Copper68k 1.5.0. The latch regression and evidence are recorded in [STORAGE.md](STORAGE.md);
this does not close physical TOD/comparator uncertainties above.

For each revisit record the build/profile, minimal reproducer, observed/expected
cycle and state, evidence source, resolution and affected checks. Keep IDs stable
and distinguish repaired symptoms from remaining uncertainty. Implementation and
focused tests are in [the engine](../../CopperMod.Amiga.Lightweight) and
[diagnostic test project](../../CopperMod.Amiga.Lightweight.Tests); host regressions
are in [CopperScreen.Lightweight.Tests](../../CopperScreen.Lightweight.Tests).

## Hardwired PAL OCS replay

Use the two-disk The Party 1991 release with a PAL A500, 512 KiB Chip RAM,
512 KiB slow RAM, Kickstart 1.3, and DF0 at normal speed. Eject after
field 4,600, insert disk two after field 4,660, and hold the right mouse
button after fields 4,750 through 4,764. The reference is WinUAE 6.0.3
with CPU, memory and blitter cycle-exact enabled and seed `1795535325`.

| Input | SHA-256 |
| --- | --- |
| Disk 1 | `C4129BC68A506D3BEB0804BF3D9FE9E41DB91E04E575C41AF6F0ADF84BDB3435` |
| Disk 2 | `E197F632618EF9EFD6BDBFDCBA91A2F051F70A151C20D8D614A0213CE92F0066` |
| Kickstart 1.3 | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |

This change covers PAL beam readback, Copper reload and strobe phases,
Paula audio requests, floppy serial and DMA boundaries, the OCS blitter
restart sequence, and border output through raster 311. Focused synthetic
cases exercise those boundaries. The published Copper68k 1.5.11 pin still
passes the engine diagnostic suite with 1,527 passes and seven skips.

The integrated candidate used a local package built from the [Copper68k
source correction](https://github.com/ilehtoranta/CopperMod/commit/77eaf8fbdc2ee309463d32fa524493b0ebec7bbb).
It completed 39,000 fields and reached the ending loop. The renderer bus
operation order matched all 2,820 traced blocks. The renderer scene lasted
628 fields versus 629 in the reference. All 78 active captures of the ending
matched pixel for pixel at a common 13-field offset with warp disabled. The
second music cue started 19 fields later under the retained drive-ready
model. After the closing audio transition, the audio offset was about 14
fields. The shorter drive-ready experiment altered later scene timing and
remains outside the change.

The native Lemmings adapter replay reaches active gameplay on both clean
`2b47027` and the integrated candidate. Both miss the historical terminal
cycle assertion. All eight sampled gameplay frames from field 13,000 onward
match, both replays consume all 19 scripted inputs, and both produce PCM in
all 3,600 measured fields. The audio and terminal fingerprints differ. The
historical values remain useful for their original configuration and are
retained. The full Hardwired replay uses the combined source candidate and
the development CPU package, whose release and package pin are separate.

## Storage benchmark follow-up, 2026-11-07

- **LWA-HDF-002 — repaired synthetic read-ahead stream resumption:** after a
  metadata detour, a hit in retained payload could leave background prefetch on
  the abandoned metadata stream. A cached successor could also stop resumption.
  The shared timed-drive model now redirects out-of-stream hits, skips retained
  successors and protects them while filling free space. A failing-before
  regression observes the next fetched sector; the focused allocation check
  passes with zero managed bytes. All 1,323 Lightweight diagnostics pass, with
  seven optional skips. The canonical clock, profile parameters and bus timing
  are unchanged. See [the model contract](TIMED_STORAGE.md) and the
  [CopperFS comparison](../../../CopperFS/docs/kickstart-ffs-hdd-cached-followup-2026-11-07.md).
  This qualifies a synthetic-model correction, not physical HDD/Gayle accuracy.
- **LWA-HDF-003 — OPEN: unchanged-control timing variation:** the CopperFS IDE
  rerun records one original-FFS control workload (Kickstart 3.1, 2 MiB Chip plus
  8 MiB Fast, HDD, 512 B) 1.108% different from the earlier run, with matching
  ROM/handler/probe/payload/timing identities and the same engine/harness hashes.
  The three trials agree within each run; other original controls differ by at
  most about 0.04%. The cause is not established. Preserve the records in
  `CopperFS/artifacts/ffs-ide-regression/qualified-analysis/audit.json` and
  reproduce with fixed RTC input and a hashed, fixed-timestamp boot ADF before
  attributing this difference. Small performance changes of this scale remain
  inconclusive; it is not evidence for changing CPU or device timing.
