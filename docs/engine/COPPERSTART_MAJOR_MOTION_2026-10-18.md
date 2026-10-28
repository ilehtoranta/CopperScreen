# Major Motion: native DOS loading and opening play

2026-10-18. Major Motion (1988, Microdeal) reaches its title, starts a one-player
game and responds to driving input on a freshly compiled CopperStart ROM
containing the Overdrive fix and the compatibility corrections below. Native
Kickstart 1.3 provides the independent original-media control. The
[evidence record](COPPERSTART_MAJOR_MOTION_2026-10-18.json) binds the exact ROM,
media, execution binaries, commands, inputs, native probes and reviewed images.

The machine is a PAL OCS A500: 68000, 8371 Agnus, 8362 Denise, 512 KiB Chip RAM,
512 KiB A501 RAM, no additional Fast RAM and one DF0 drive. The supplied OFS ADF
is used unchanged. Its startup sequence invokes `major` directly.

Four general compatibility corrections were required:

- Relative DOS names select the current directory's filesystem handler, even
  when the process's default filesystem field is empty or stale. Reading that
  directory internally must not exchange it with null through CurrentDir.
  Current main already contains these underlying repairs; its narrow additional
  correction gives leading-colon paths a zero base lock for the selected volume's
  root instead of the current subdirectory lock.
- A sequential read retains each validated file/extension metadata table instead
  of fetching the header again for every data sector. Repeated metadata reads
  defeated the native track cache and caused prolonged mechanical seeks. The
  OFS/FFS regression observes one file-header request instead of 88/84, checks
  every byte across an extension, and preserves existing error/prefix behavior.
- LoadSeg forwards HUNK Chip/Fast requirements to Exec. Previously all ordinary
  LoadSeg hunks used an unrestricted allocation, so the Overdrive-corrected A501
  preference also placed Chip-marked display data outside DMA-accessible memory.
  An original two-hunk guest command reproduces this on the old image, passes
  on Kickstart 1.3 and verifies Chip placement and payload on the corrected ROM.
- Native boot leaves vertical blank enabled through the already installed Exec
  interrupt dispatcher. Major Motion installs its own level-3 handler and waits
  for the next interrupt without enabling the source itself. With the old image,
  INTENA is `$4000` and the guest remains at its handoff loop. An original boot block
  reproduces that wait on the old ROM and receives the interrupt under native
  Kickstart 1.3 and the corrected image. No title-specific exception is used.

The newly compiled baseline containing the preceding Overdrive work is
`5CA306B3A7ECE26A366ADC90E487FC384276A5463C22C3CE6B98AA4A118764D0`.
It fails the relative-path command and Major Motion startup. The directory-only
intermediate passes the new DOS cases but remains busy reading the executable.
The subsequent directory/read image loads the assets but hangs at the level-3
handoff; a diagnostic interrupt enable reveals the separate hunk-placement
problem. These unsuccessful images remain in the local record and are not
reported as gameplay passes. The VBlank-only compile was interrupted after
finding the memory issue; it produced no qualified image.

The corrected ROM was freshly compiled at `2026-10-18T16:40:20.555176+00:00`.
Its 512 KiB image is SHA-256 `9F938923B7E58DF756D6EB6E7A6E50C7A5BC901EFA8FB6F7AB3ABA5D2277290C`, with 451,192 code bytes
and 26,098 graphics bytes at `$FF8000`. The root has zero managed
allocation sites and zero framework features. The qualified source commit is
[`297c3c7`](https://github.com/ilehtoranta/CopperStart/commit/297c3c7e66c239ee10cb1c42da5eea67f355e8db).
The current-main changes are in
[CopperStart PR #10](https://github.com/ilehtoranta/CopperStart/pull/10).

This remains the retained bounded native composition: one rebuilt DOS DLL,
the NativeDiskBoot VBlank change, four unchanged root sources and ten unchanged
component DLLs. Compiler/SDK, compile options, graphics composer and all four
execution binaries remain frozen. The pinned CPU package is
`1.5.4-nitro-dev.2`. No engine/CPU implementation, ROM/media byte patch or host
OS shim is introduced. The image retains the F17, Lemmings, Alien Breed,
Supervisor/caller-trap-zero and Overdrive allocation fixes.

Native control uses the existing [corpus input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-major-motion.json)
with key 1 at 4,900 and driving input from 6,000. Its
[idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-major-motion-idle-v1.json)
retains game start and omits the driving events. CopperStart's separate [positive input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-major-motion-driving-v1.json)
uses the same sequence shifted 8,100 fields: key 1 at 13,000 and driving from 14,100.
Its [idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-major-motion-delayed-idle-v1.json)
retains that same start and omits driving. Each control shares all loading and
start inputs with its own positive run.

The native 10,000-field CPU-mode pair matches all 505 capture files at 101
checkpoints. CopperStart's 18,000-field pair matches all 905 files at 181
checkpoints. Complete images, CPU/device state, Chip/A501 RAM and sampled PCM
match within each pair, as do CPU/hardware/output/cycle fingerprints. Raw-file
equality is mandatory for those pairs. Native/CopperStart CPU or screenshot
identity is not a gate; their start and input schedules differ.

The native input control has 300 identical files before the changed input;
its first differing image is field 6,100, with 40 later different images.
CopperStart has 705 identical prefix files and first differs at 14,200, with
39 later different images. Full-viewport review confirms player-controlled
driving and scoring against the idle controls. The CopperStart run reaches
the high-score screen by field 15,500 with 35 points after losing its lives;
the native run reaches its high-score screen with 455 points. The different
trajectories are independently checked against each ROM's own idle control.
The reviewed captures and individual observations are retained in the JSON.
This establishes opening play only; later stages and full-game completion are
unverified. Captures are reviewed at the full 908×313 viewport without cropping
or scaling; labelled JPEG viewing copies are supplementary to the raw captures.

Fourteen exact-ROM guest cases pass with zero skips: six memory/allocation,
Supervisor/trap-zero and interrupt-chain cases; two graphics/caller-storage
cases; four relative DOS OFS/FFS cases; and the new boot-VBlank and Chip-hunk
cases. The independently generated native Kickstart 1.3 oracles pass for both
new cases and the two OFS relative-DOS configurations. The native 1.3 control could not boot
this FFS fixture; that trial is unavailable oracle coverage. Host tests and
original-media replays remain separate coverage. The focused suites pass 70 cases on the qualified
branch and 75 on current main, with no skips. All nine filesystem cases also
pass against both final source builds.

The same corrected image also completes the retained 46,000-field Overdrive and
24,000-field Lemmings scenarios. Overdrive retains acceleration and
steering in qualifying; Lemmings rescues 100% in level one. Their reviewed
milestones and limits are in the record. No throughput acceptance, physical hardware timing or audio fidelity
qualification is claimed.

The canonical consumer Release build succeeds with zero warnings. Qualified
and current-main source solution builds succeed with their retained compiler/
dependency warnings. All 21 replay-tool controls pass; earlier attempts with a child-start
timeout failure remain in the JSON record. Source GitHub Actions has 13 jobs with zero executed steps because of the
account payment/spending-limit restriction; this is unavailable CI coverage.
No ROM, media, local artifact or package is committed or published.
