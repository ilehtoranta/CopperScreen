# CopperScreen Microsoft Store productization roadmap

Planning baseline: 2026-10-16. Target: the first paid Windows desktop release
through Microsoft Store, including CopperStart and CopperOS support.

This is an implementation and release plan, not evidence of completed support or
authorization to publish. All tasks below are open unless a completed evidence
record is linked. Existing capabilities are starting points, not accepted Store
release results. WHDLoad is outside this roadmap; no integration, licensing or
release dependency is planned for it here.

## Release scope and boundaries

The intended customer journey is: install from Store, start the included
CopperStart/CopperOS environment, run an included demonstration, import supported
user-owned media, save work, close, and reopen with work and settings intact.
The included environment must work without proprietary Kickstart or Workbench
files. Existing user-supplied native Kickstart workflows remain supported within
the declared launch matrix.

For planning, CopperStart supplies the replacement boot/runtime artifact and
CopperOS supplies the guest OS/user environment. A proposed
[versioned CLI-first integration contract](COPPEROS_INTEGRATION.md) records the
responsibilities, target delivery format and unresolved producer handoff. Exact
release versions and scope still need to be frozen in P0-02. This repository has
bounded standalone CopperStart ROM evidence; it does not establish a qualified
CopperOS integration or general replacement-OS compatibility.

- Lightweight remains the single owner of emulated hardware and execution.
  Integrate supported guest artifacts through explicit boot/storage interfaces.
- Keep CopperStart/CopperOS source ownership with their producers. Consume
  versioned, licensed artifacts with hashes and provenance; do not reference a
  sibling checkout or add their compiler/SDK graph to the production solution.
- The superseded Legacy restoration plan is not an implementation prerequisite.
  Do not restore the retired Legacy host or introduce automatic engine fallback.
- Minimal disk boot remains its separately bounded firmware path. Do not expand
  it into a replacement OS as a shortcut to CopperStart/CopperOS support.
- No ROMs, game media, generated OS/boot images, private artifacts or credentials
  enter Git. Authorized release assets belong in the controlled packaging inputs.
- Broader AGA/CPU/peripheral support, full AmigaOS compatibility and unfinished
  hardware accuracy work are included only where the declared launch scope needs
  them. Preserve current behavior and golden outputs during integration.

Current engine behavior remains defined by the [supported machine/API](../CopperMod.Amiga.Lightweight/README.md),
[architecture](engine/ARCHITECTURE.md), [issue register](engine/ISSUES.md) and
[engine roadmap](engine/ROADMAP.md). Product release planning does not replace them.

## Starting evidence

| Area | Repository baseline | Release gap |
| --- | --- | --- |
| Desktop | Welcome/setup, ROM validation, disk drag-and-drop, recent disks, fullscreen, input controls and local crash logging exist. | Verify the complete installed customer journey and recovery cases. |
| Storage | Floppy images, CopperHDF and shared folders have bounded implementation/evidence. | Qualify packaged permissions, persistence, upgrades and protection of originals. |
| Packaging | Self-contained ZIP/MSIX tooling, signed diagnostic persistence and [local WACK overall PASS](MSIX_WACK_VALIDATION_2026-10-17.md) for diagnostic/production shapes. | Final Store identity, Store acquisition/update, clean-machine acceptance and release pipeline; optional WACK reference findings remain documented. |
| Profiles | [Profile store](../CopperScreen/CopperScreenSettings.cs) locates and saves profiles beside application presets. | Separate immutable defaults from writable per-user data. |
| Dependencies | The application pins a Copper68k development candidate from a local feed; controller dependencies include preview packages. | Select and qualify frozen release inputs available to clean CI. |
| CopperStart | [Standard-app integration](COPPERSTART_APP.md) selects a hash-qualified standalone ROM on Lightweight; bounded lifecycle, Lotus 2 and signed installed CopperOS boot/update checks pass. | Controlled artifact distribution and broader release matrix; full-game completion is unverified. |
| CopperOS | Original Shell boot proof plus [native writable workspaces](COPPEROS_STORAGE.md); signed diagnostic restart/update persistence passes for the exact development pair. | Wider ABI/lifetime budgets, rights, clean onboarding and Store release qualification remain open. Shipping and default activation remain disabled. |
| Quality | Production CI, separate engine diagnostics and native replay tooling exist. | Installed release-candidate evidence, consumer hardware coverage and product support operations. |

## Priority and delivery order

P0 resolves launch scope and external dependencies. P1 is required for the planned
first release, including both CopperStart and CopperOS. P2 is a follow-up backlog;
it must not become an accidental dependency of the first release.

| Milestone | Exit criteria | Tasks |
| --- | --- | --- |
| M0: feasible and defined | Store eligibility disposition, rights, product contract and frozen-input plan recorded. | P0-01 through P0-04 |
| M1: installed vertical slice | A packaged app boots the intended CopperStart/CopperOS pair, runs a demonstration and preserves a file after reopening. | P1-01 through P1-05 |
| M2: release candidate | Compatibility, desktop quality, support and commercial submission material meet their criteria. | P1-06 through P1-09 |
| M3: Store release | The exact candidate is certified, its Store install/update is verified, and publication is explicitly authorized. | P1-10 |

P0 investigations and independent implementation can proceed together. Finish
M0 before committing to a public launch date. Create the earliest MSIX vertical
slice before polishing features, so packaging restrictions are exercised early.
Assign named owners and dates when scheduling; the roles below describe ownership
without inventing assignments.

## P0: scope and commercial prerequisites

### P0-01 — Resolve Microsoft Store eligibility

Owner: product/release. Dependency: none.

Review CopperScreen's actual PC computer-emulation, gaming and OS functionality
with Microsoft Store support. Record a written eligibility/category disposition
and any required product changes. Policy 10.13.10 restricts game-system/platform
emulation; the surrounding scope differs between [7.19](https://learn.microsoft.com/en-us/windows/apps/publish/store-policy-archive/store-policy-7-19#1013-gaming-and-xbox)
and [7.20](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies#1013-gaming-and-xbox),
which takes effect on 2026-10-22. Recheck the applicable policy at submission.

Acceptance:

- Record the reviewed feature set, device family, proposed category, policy
  version/date and Microsoft's response/reference. Do not infer approval from a
  category choice or another emulator's listing.
- Translate any conditions into tracked tasks and accurate listing language.
- If eligibility remains unresolved, mark Store launch blocked with its next
  action; do not describe packaging completion as eligibility or certification.

### P0-02 — Freeze the first-release product and artifact contracts

Owner: product and host integration, with CopperStart/CopperOS producers.
Dependency: none; informs P0-01 and P0-03.

The [CopperStart/CopperOS draft contract](COPPEROS_INTEGRATION.md) provides a
bounded CLI-first starting point and records missing producer artifacts. It is
not a frozen release pair, completed scope decision or activation permission.

Acceptance:

- Record the supported Windows versions, architectures, machine/CPU/RAM profiles,
  boot methods, media formats, input devices, languages and known limitations.
  Start with a bounded PAL A500 configuration; additional advertised profiles
  require their own evidence.
- Specify the CopperStart/CopperOS pair: producer versions, boot image and OS
  media formats, machine/memory needs, required interfaces, startup/launch/exit
  behavior, writable storage and compatibility/version handshake. Record whether
  the OS experience uses a shell, desktop or both, and name its launch workflows.
- Require an included, offline first-run path with no proprietary ROM/OS files.
  Define the original or redistribution-cleared demonstration and certification
  content. Freeze a finite compatibility matrix and measurable startup, sustained
  real-time playback, audio and responsiveness budgets before qualification.
- Keep both CopperStart and CopperOS as first-release requirements. Any later
  scope change needs an explicit product decision and updated roadmap/listing;
  unfinished support cannot silently become a passed release criterion.

### P0-03 — Clear distribution rights and customer disclosures

Owner: product/release, with artifact producers. Dependency: P0-02.

Acceptance:

- Inventory shipped managed/native dependencies, CopperStart/CopperOS artifacts,
  fonts, sounds, icons, demonstration content and screenshots. Record versions,
  origin, licence, required notices and commercial redistribution permission.
- Complete [third-party notices](../THIRD-PARTY-NOTICES.md) for the actual bundle;
  retain the repository's [MIT licence](../LICENSE). Verify rights to use relevant
  names/logos and avoid implying third-party endorsement.
- Separate included content from user-supplied native ROMs, OS files and media in
  setup, help and listing. Verify the release bundle contains only cleared assets.
- Publish a privacy statement covering file access, local settings/logs and any
  transmission. Win32 products require a privacy policy under [Store policy
  10.5.1](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies#105-personal-information).
  Any optional diagnostic upload must have a defined consent and deletion policy.

### P0-04 — Make release inputs reproducible

Owner: build/release. Dependencies: P0-02, P0-03.

Acceptance:

- Select and qualify exact Copper68k, controller and other dependency versions;
  record any retained preview qualification. Existing published versions remain
  immutable. Publication of a new dependency is a separate explicit release action.
- A clean CI agent restores locked dependencies and obtains authorized
  CopperStart/CopperOS assets without a sibling checkout, developer cache or
  manually populated local feed. Verify asset hashes and fail clearly on missing,
  mismatched or incompatible inputs.
- Produce a manifest identifying source commit, SDK/tool versions, locks,
  producer versions, licences and input/output hashes. Retain private inputs and
  symbols securely outside Git; store no credentials in scripts or manifests.

## P1: first-release implementation and acceptance

### P1-01 — Separate application files and user data

Owner: host/storage. Dependency: P0-02.

Status: initial host implementation and automated coverage added 2026-10-16;
see the [storage contract and verification limits](PROFILE_STORAGE.md).
Native writable workspaces and local signed MSIX restart/update persistence now
pass; clean-machine and separate-Windows-account acceptance remain open.

Acceptance:

- Read bundled presets as immutable defaults; save edited/new profiles, settings,
  logs and OS writable data under explicit per-user locations or user-selected
  folders. [MSIX package files are read-only](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes).
- Migrate existing portable profiles without overwriting originals; interrupted
  writes, invalid profiles and unavailable directories produce recoverable errors.
- Installed app tests cover standard users, spaces/non-ASCII paths, denied write
  access and two Windows users. No runtime save requires elevation or writes to
  the application package. Updating preserves profiles and guest data.

### P1-02 — Build the Store package and installed test pipeline

Owner: build/release. Dependencies: P0-04, P1-01.

Status: packaging and diagnostic install/update pipeline implemented 2026-10-16;
see [usage](MSIX_PACKAGING.md) and [local validation](MSIX_VALIDATION_2026-10-16.md).
MakeAppx and host/tooling checks pass. The
[2026-10-17 signed installed continuation](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
passes diagnostic install/restart/update/uninstall/reinstall with the exact
development guest pair. The [WACK continuation](MSIX_WACK_VALIDATION_2026-10-17.md)
passes the required DPI check after embedding the executable manifest; both signed
diagnostic `1.1.12.0` and production-shape `1.1.13.0` report overall PASS and all
13 required checks PASS. The [fresh P1-06 WACK run](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md)
also reports overall PASS and all 13 required checks PASS on the exact signed
media-tested update `1.1.24.0`. Optional blocked-executable findings remain in
the evidence. Final Store identity, cleared release assets, Store acquisition/update
and clean-machine/account acceptance remain open.

Acceptance:

- Generate an MSIX for each declared architecture with matching Partner Center
  identity/publisher, increasing package versions, icons and justified
  capabilities. Include runtime/native dependencies, notices and approved guest
  assets; the customer needs no .NET SDK or development tools.
- A clean supported Windows machine installs and launches the package as a
  standard user. Verify update from the previous test package, settings/data
  preservation, uninstall/reinstall and documented data-retention behavior.
- Run the Windows App Certification Kit and resolve failures. Retain the exact
  package hash and results; local checks do not establish Store certification.
  Use Microsoft's [MSIX requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

### P1-03 — Integrate and qualify CopperStart

Owner: host integration and CopperStart producer. Dependencies: P0-02 through
P0-04; installed validation uses P1-02.

Status: standard-app integration implemented for the bounded PAL A500 standalone
ROM slice. See [artifact, configuration and qualification workflow](COPPERSTART_APP.md)
and [dated validation evidence](COPPERSTART_VALIDATION_2026-10-16.md).
Signed diagnostic MSIX boot/update evidence for the exact CopperOS storage pair
now passes. Controlled release distribution and the broader launch matrix remain
open; this does not close P1-04 or certify general
CopperStart/CopperOS compatibility.

Acceptance:

- Expose an explicit CopperStart boot choice backed by the versioned replacement
  artifact on Lightweight. Show its version and requirements; reject missing,
  damaged or incompatible assets with recovery guidance. No Legacy fallback or
  proprietary-ROM substitution occurs.
- Verify cold start, soft/hard reset, stop/restart, floppy startup, declared boot
  device discovery and input/audio/video against the frozen matrix. Add focused
  integration checks without moving hardware scheduling into the host.
- Extend the [existing replacement-ROM evidence](engine/COPPERSTART_COMPATIBILITY_2026-10-15.json)
  using exact ROM/media hashes, configurations, input and inspected milestones.
  For declared disk titles, verify loading, menu and input-driven use separately;
  use native Kickstart controls to investigate failures. Existing bounded Lotus
  evidence is a starting point, not a full-library or full-game claim.

### P1-04 — Integrate and qualify CopperOS

Owner: host integration and CopperOS producer. Dependencies: P0-02 through
P0-04, P1-01, P1-03; installed validation uses P1-02.

Status: native DOS1 writable storage and format-1 user-owned workspaces are
implemented. See [storage behavior, recovery and qualification](COPPEROS_STORAGE.md).
Native source qualification passed on 2026-10-16: seven real guest boots cover
file/directory mutations, relative child I/O, settings/data readback after app
restart and a changed installation directory, full/read-only volume errors, and
recovery export/readback after host save denial. See the
[versioned storage pair](../packaging/guest/copperos-storage.v1.json) and
[hash-bound qualification record](COPPEROS_STORAGE_2026-10-16.json).
Signed local MSIX install/restart/update/uninstall/reinstall passed on 2026-10-17;
see the [installed evidence](COPPEROS_MSIX_VALIDATION_2026-10-17.md). Diagnostic
versions `1.1.9.0` → `1.1.10.0` preserve the exact native image, profile and settings
bytes across restart/update. An initial failed reinstall exposed shared AppData
retention; installed data now uses package-family LocalState explicitly, and the
full rerun passes. The earlier failure remains retained evidence.

The [WACK/DPI continuation](MSIX_WACK_VALIDATION_2026-10-17.md) repeats native
persistence through signed update `1.1.9.0` → `1.1.12.0` with three boots and
verified zero process exits in all four desktop phases. WACK's required DPI check
is corrected; diagnostic and normal package shapes both report overall PASS.

The original read-only [native Shell boot proof](COPPEROS_BOOT_PROOF_2026-10-16.md)
and [exact-pair descriptor](../packaging/guest/copperos-native-boot.v1.json) remain
frozen evidence. The earlier [inspection contract and OS-01 through OS-07 work order](COPPEROS_INTEGRATION.md),
[draft descriptor](../packaging/guest/copperos-contract.v1.json) and
[artifact inventory](COPPEROS_ARTIFACTS_2026-10-16.json) retain their original scope.

The new implementation clones an admitted seed into per-user storage, preserves
existing workspaces through package-directory changes, checkpoints complete
native mutations and retains the last complete disk when a save fails. Recovery
export retains primary dirty state. No Legacy service, host DOS shim or producer
project reference was added to the app. P1-04 remains open for broader
ABI/lifetime/resource qualification, cleared release assets, clean bundled
onboarding and Store acquisition/update qualification.

Acceptance:

- Install the qualified OS assets and boot the specified CopperStart/CopperOS
  pair from a clean profile. Reach the agreed shell/desktop with no external ROM,
  OS installation or development checkout. Report the actual pair versions.
- Exercise the named P0-02 workflows: launch and exit the bundled demonstration
  and commands/apps, accept keyboard/mouse input, create/read/rename/delete a
  guest file and recover from an application error without losing host control.
  Each advertised native-binary/API compatibility claim has an executable fixture.
- Verify OS settings and files after normal exit, host-app restart and package
  upgrade. Test invalid/missing OS assets, failed boot and a full/read-only guest
  volume; give a repair/reset path that preserves or exports user work.
- Record the supported producer pair and an upgrade/migration policy. Reject an
  incompatible pair explicitly; update bundled OS assets through the release
  package rather than silently replacing the qualified environment at runtime.

### P1-05 — Complete onboarding and everyday launch workflows

Owner: desktop UX. Dependencies: P1-01 through P1-04.

Status: first-run content guidance, bounded CopperStart setup, CopperOS workspace
create/resume, saved launch profiles, safe Stop/Restart and in-app demonstration /
export help are implemented on Lightweight. See [onboarding workflows and
verification](ONBOARDING.md). Experimental assets remain labelled previews;
production does not bundle the unadmitted development pair. Final clean-account
offline walkthroughs of the signed shipping package remain open, including
native file pickers, accessibility and the included Shell/Demo path.

Acceptance:

- On a clean offline installation, a tester follows only in-app guidance to
  start the included environment and demonstration. Explain included versus
  user-supplied content before asking for files; no repository instructions needed.
- A second walkthrough imports a supported user-owned disk/native ROM, validates
  the selection and remembers its profile. Broken paths, incompatible ROM/profile
  combinations and unreadable archives have actionable errors without a crash.
- Starting, pausing, stopping, restarting, switching media and releasing captured
  input are discoverable. Unavailable features are hidden or clearly labelled;
  experimental choices do not appear as qualified launch configurations.

### P1-06 — Protect media and qualify persistent saves

Owner: storage/host. Dependencies: P1-01, P1-04, P1-05.

Status: implemented on Lightweight with source and signed diagnostic installed
qualification: protected new
imports, explicit ADF replacement, owner-driven hardfile/shared-folder flushes,
atomic workspace checkpoints, versioned backup/import and confirmed previous
checkpoint recovery. Process-interruption and native storage evidence are in the
[media protection and recovery guide](MEDIA_PERSISTENCE.md). The
[P1-06 signed MSIX continuation](MEDIA_MSIX_VALIDATION_2026-10-17.md) passes
update persistence, backup/import, recovery and restore after reinstall through
six native boots. The [fresh WACK continuation](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md)
reports overall PASS on the exact `1.1.24.0` signed update: all 13 required checks
pass; the optional blocked-executable FAIL remains recorded. Final shipping-package
file-picker/update walkthroughs remain release gates. Previous signed MSIX/WACK
records apply only to their recorded binaries. No Legacy restoration or
transactional guest-filesystem promise.

Acceptance:

- Define the write contract for each advertised format, CopperHDF, shared folder
  and CopperOS volume. Imported originals remain read-only unless the user
  explicitly chooses a writable operation. Unsupported exports fail clearly.
- Verify guest write, explicit flush/save where applicable, close and reopen with
  matching content. Cover changed floppies, write protection, eject/reset/close
  with unsaved data, disk full, permission loss and missing removable storage.
- Document interruption recovery and backup/export. Abrupt termination must not
  alter a protected original; writable volumes either reopen validly or give a
  recoverable diagnosis. Do not promise transactional guest filesystems.

### P1-07 — Qualify compatibility, desktop behavior and performance

Owner: engine/host QA. Dependencies: P0-02, P1-02 through P1-06.

Status: in progress. The [development qualification protocol](RELEASE_QUALIFICATION.md)
defines a finite three-path replay matrix, desktop input/layout controls and a
separately versioned current-PC engine baseline. The owner authorized provisional
observations on this PC and left release hardware/budgets open. Physical-device
checks and final release-matrix 60-minute sessions remain required; no historical
FPS result or WACK verdict closes those gates. The launcher viewport and shared desktop-test
fixture corrections have source qualification and a fresh
[signed installed continuation](RELEASE_MSIX_VALIDATION_2026-10-17.md): diagnostic
`1.1.25.0` → `1.1.26.0`, six passing native activations, 18 installed launcher
layout cases, unchanged workspace/profile/settings hashes through restart/update,
and backup restore after uninstall/reinstall. The installed layout cases use the
current monitor's 100% scale; physical mixed-DPI behavior remains open. The
[fresh P1-07 WACK continuation](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) reports
overall PASS on the exact signed `1.1.26.0` update: 13 required checks PASS,
10 optional PASS and the same optional blocked-executable FAIL as P1-06.
Older package verdicts remain unchanged; final production/Store-identity WACK
remains required.
The [2026-10-17 evidence](RELEASE_QUALIFICATION_2026-10-17.md) records passing
bounded native/desktop controls and nine matching full-workload identities;
all timing observations remain INVALID / RERUN under recorded competing load.
The [2026-10-18 installed-hour continuation](INSTALLED_SESSIONS_2026-10-18.md)
completes sequential 3,600-second CopperStart and CopperOS sessions on signed
diagnostic `1.1.30.0`, with successful native storage/media/profile/settings
reopening and validated workspace exports. Real WASAPI software queue starvation
is observed (1.00% / 12.80% missing requested sample values), with uneven pacing;
the separate correction and rerun follow below. The earlier untraced
preflight close remains unresolved; a separate traced dashboard-sharing defect
was corrected and negatively controlled. Budgets remain OPEN, physical checks
and native Kickstart hour coverage remain required, and this package has no new
WACK verdict. See the [repeatable protocol](INSTALLED_SESSIONS.md).
The [audio/pacing continuation](INSTALLED_AUDIO_PACING_2026-10-18.md) corrects the
independent video deadline that gated an underfilled audio reserve. A new signed
`1.1.31.0` completes both full installed hours and fresh-process native reopens
with zero queue starvation, callback exceptions or submission failures. The
existing eight-slot capacity and native CPU/pixel/PCM work are preserved. Host
timing variation remains recorded; minimum/reference hardware, release latency
and other numeric budgets remain OPEN. This bounded correction does not close
the remaining physical, final-package or native Kickstart hour gates.
The [signed audio/pacing upgrade](AUDIO_MSIX_VALIDATION_2026-10-18.md) qualifies
the retained pre-fix `1.1.30.0` payload updating to that exact unchanged signed
`1.1.31.0` candidate. CopperOS workspace/profile/settings persistence, denied-save
recovery and clean-reinstall backup restoration pass across six native activations;
all 18 installed launcher observations pass. The
[fresh WACK run on this exact hash](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md)
reports overall PASS and all 13 required checks passing; the optional
blocked-executable finding retains the same 24 messages. Final production/Store
identity and the remaining release gates stay open. Earlier dated results remain
unchanged, and release budgets remain OPEN.
The earlier [installed sleep/recovery continuation](INSTALLED_RECOVERY_2026-10-18.md)
retains partial `1.1.32.0` CopperStart S3 evidence and failed host-method attempts.
The [owner-launched endpoint probe](RECOVERY_ENDPOINT_PROBE_2026-10-18.md) later
qualified actual HDMI disable/restore. Subsequent runs exposed a durable post-S3
callback/frame stall. The first host watchdog fix and its evidence remain in the
[restart correction](INSTALLED_RECOVERY_FIX_2026-10-18.md).
The [native reopen continuation](INSTALLED_RECOVERY_REOPEN_2026-10-18.md) retains
signed `1.1.33.0` failing after paused S3 with repeated `MA_UNAVAILABLE` results;
actual endpoint restoration did not repair it. Signed `1.1.34.0` now reopens the
original backend after restart failure, preserves queued audio/guest state and
uses stronger installed health checks. Production build and all ten packaged
controls pass. Its subsequent installed active-S3 follow-up failed with repeated
native initialization errors. The [diagnostic continuation](INSTALLED_RECOVERY_DIAGNOSTICS_2026-10-18.md)
retains that failure and signed `1.1.35.0` software-only stop/reopen, native saves,
exports and fresh-process profile/settings readback passing for both boot paths.
Version 3 adds bounded native error logging; the production recovery algorithm
is unchanged. The subsequent [attended physical continuation](INSTALLED_RECOVERY_PHYSICAL_2026-10-18.md)
passes active/paused S3, actual HDMI disable/restore, export and fresh-process
disk/workspace/profile/settings readback for both boot paths on `1.1.35.0`.
CopperStart audible output before sleep and after final restoration is owner
confirmed. The frozen controller requests the display on after timer wake;
display-off recovery and the cause of earlier failures remain unproven. Transient
native errors and CopperStart sample gaps remain findings. Exact-candidate
update/WACK and audio continuity remain open. Earlier FAILED records are unchanged.
The [display/audio continuation](AUDIO_DISPLAY_RECOVERY_2026-10-18.md) qualifies
actual display-off/on recovery on signed `1.1.36.0` and `1.1.37.0`: both native
boot paths keep advancing while the HDMI callback clock is unavailable, recover
output with zero missing values in the restoration windows, and preserve native
saves, exports, profiles and settings through fresh-process readback. Version 37
primes a fully reopened output before playback; its early reopen counts are zero.
Paired CopperStart scheduling controls support the best-effort MMCSS producer
registration under the recorded host load. Warmup/other-window underruns and a
late 748-value CopperOS refill gap remain OPEN; this is not glitch-free or full-hour
acceptance. Owner-confirmed audible CopperStart recovery is separate evidence.
Fresh exact-candidate S3/PnP, update and WACK remain unrun; see the
[version-4 procedure](INSTALLED_DISPLAY_RECOVERY.md). Older verdicts are unchanged.
The [bounded profiling follow-up](AUDIO_REFILL_TRACE_2026-10-18.md) preserves
stack-sampler effects and a separate GC-only capture. It does not identify a
safe additional correction or close residual refill loss; no GC-policy, queue
capacity or guest-timing change is inferred from those observations.
P1-07 and release budgets remain OPEN.
The [low-intrusion warmup/publication continuation](AUDIO_WARMUP_REFILL_2026-10-19.md)
localizes early slow first-use execution and later UI publication stalls. The
desktop disables Quick JIT and keeps rendered-field audio production independent
of UI dispatch and presentation-lock waits, including running command publication. A retained WASAPI catch-up burst exhausted the eight-block reserve despite short owner work; replay proves ten blocks cover that demand, adding roughly 40 ms of queued PAL audio. Signed diagnostic `1.1.43.0` passes
both installed native paths, both cold processes, the 120-second native reopen
follow-ups, native save/export and fresh-process workspace/profile/settings
readback with zero cumulative missing values. The original 37 CopperOS episode
has no retrospective owner trace, so its precise cause remains unassigned.
The loaded-host capture is INVALID / RERUN for formal performance acceptance.
Physical sound and display/S3/PnP need separate
qualification for this exact hash. P1-07 and release budgets remain OPEN;
earlier dated failures and verdicts are unchanged.

The [signed 1.1.43.0 upgrade continuation](AUDIO_MSIX_VALIDATION_2026-10-19.md)
passes an actual same-family `1.1.42.0` to unchanged `1.1.43.0` update. Six native
activations preserve CopperOS workspace, profile and settings through restart
and update, then qualify denied-save recovery and clean-reinstall restoration.
All 18 launcher observations pass; 15 hash-checked host-state copies remain
inspectable after uninstall. This adds no new audio-hour, physical recovery,
performance-budget or WACK verdict. The earlier audio and dated records remain
unchanged. Release budgets remain OPEN.
The [fresh exact-hash 1.1.43.0 WACK continuation](AUDIO_MSIX_WACK_VALIDATION_2026-10-19.md)
reports overall PASS and all 13 required checks PASS. Ten optional checks pass;
the optional blocked-executable scan retains the same 24 messages as signed
1.1.31.0. No product correction or package rebuild was required. This closes
local WACK for the unchanged diagnostic hash; final production/Store-identity
WACK, dependency review, physical recovery and release budgets remain
OPEN. Lightweight remains the engine and Legacy is unavailable.

The [signed 1.1.43.0 full-hour continuation](INSTALLED_SESSIONS_2026-10-20.md)
completes 3,600 measured seconds for both CopperStart and CopperOS, followed by
successful native save/export and fresh-process workspace/profile/settings
readback. All four processes exit normally; warmup, measured hours and reopens
have zero missing audio samples, starved callbacks, callback errors, submission
failures or suppressed PCM. Memory ranges are descriptive and budgets remain
OPEN. The [capture audit correction](COPPEROS_CAPTURE_VALIDATION_2026-10-20.md)
establishes that the original CopperOS start, midpoint and end PNGs are
byte-identical and contain the Shell text. The earlier blank-image claim was
an incorrect visual review; its dated report and raw files remain unchanged.
LWA-HOST-005 is closed as a false positive. Diagnostic captures now retain
hash/pixel evidence and reject missing fixed-proof text; separate installed
layered probes distinguish guest, session, presenter and captured pixels.
The actual presentation-buffer drops of 19 / 3 remain descriptive observations.
Physical display/pacing and recovery gates remain open. Lightweight stays
active; Legacy remains unavailable.

Acceptance:

- Complete the frozen matrix for native Kickstart, CopperStart disk boot and
  CopperStart/CopperOS. Track boot, usable interaction, visuals/audio, saves and
  reopen separately. Every advertised combination has a result and evidence;
  missing media or skipped replays remain unavailable coverage.
- Exercise declared keyboards/controllers, remapping and reconnect, focus loss,
  mouse capture/release, fullscreen, multi-monitor/DPI changes, sleep/resume and
  audio-device removal/change. Verify keyboard-only setup, accessible control
  names, readable contrast and supported display sizes/scaling.
- Meet the P0-02 budgets on named minimum/reference hardware using the installed
  release candidate, including a sustained 60-minute session for each declared
  boot path. Record crashes, audio underruns, pacing and memory/log growth;
  resolve failures affecting the advertised experience.
- Run production build, focused host/disk tests and separate engine diagnostics.
  Preserve deterministic/golden native evidence. Report correctness, desktop UX
  and throughput separately under the [performance guide](engine/PERFORMANCE.md).
  Historical placement/input protocols stay unchanged; use an applicable,
  separately versioned validated protocol for other hosts. Smoke tests are not
  throughput acceptance and historical H-stage/G6/G7 gates are not prerequisites.

### P1-08 — Ship help, diagnostics and support operations

Owner: desktop/support. Dependencies: P0-03, P1-05 through P1-07.

Acceptance:

- Include version/build and guest-pair information, licence notices, privacy and
  support links, controls, setup help, known limitations and recovery/export steps.
- Provide user-controlled diagnostic export containing relevant versions,
  configuration and errors. Remove personal paths/identifiers and exclude ROM,
  media and guest-file contents by default; let the user inspect before sharing.
- Bound local log retention and handle logging failures without preventing app
  startup or damaging data. Document where local data lives and how to remove it.
- Publish a working support channel, issue-report template, release notes and
  response/triage process, including a plan for a broken Store update and safe
  recovery. Verify links from the installed package.

### P1-09 — Prepare the commercial listing and certification submission

Owner: product/release. Dependencies: P0-01 through P0-03, P1-07, P1-08.

Acceptance:

- Complete publisher verification and applicable payout/tax information; reserve
  the name and set price, markets, supported languages/devices and age ratings.
  Decide trial behavior explicitly; if offered, verify entitlement transitions
  and offline behavior preserve saved work and the documented purchase experience.
- Supply accurate descriptions, screenshots, icons, privacy/support details and
  known limitations. Clearly identify included CopperStart/CopperOS functionality
  and any user-supplied-content requirements; omit unqualified capabilities.
- Prepare certification notes with exact steps and legally usable content for
  both the included environment and claimed import workflows. A clean-machine
  tester can reproduce them without private developer paths or credentials.
  Check the live [submission checklist](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission)
  and requirements again before submission.

### P1-10 — Certify and publish the exact release candidate

Owner: release. Dependencies: all P0 and P1-01 through P1-09.

Acceptance:

- Retain a release record with completed task evidence, unresolved disclosed
  limitations, source/input/package hashes, compatibility matrix and test results.
  No known data-loss, security, startup or advertised-workflow blocker is open.
- Submit the frozen package, resolve certification feedback and record Microsoft's
  certification result. A changed binary or guest artifact requires affected
  checks and submission evidence to be refreshed.
- Verify acquisition and launch through the Store testing/distribution facilities
  available for the product, including price/entitlement behavior and upgrade
  persistence. Local sideloading alone does not close this task.
- Obtain explicit publication authorization for the reviewed package, listing,
  price and markets. Then publish and verify the public Store install and core
  journey, with support and update monitoring ready. Retain a previous known-good
  build and a tested recovery/forward-fix procedure.

## P2: follow-up backlog

These tasks do not block the defined first release and must not be advertised as
included until qualified.

| ID | Task | Acceptance criteria |
| --- | --- | --- |
| P2-01 | Searchable game/application library | Import, search, favourites and launch with per-title machine/input settings; missing paths are repairable and export/import preserves user metadata. |
| P2-02 | Versioned save states | Restore CPU/device state, in-flight DMA/audio/input and media identity deterministically; define writable-disk/shared-folder consistency, reject incompatible states and reset host queues. Guest file persistence remains required independently. |
| P2-03 | Broader machines, peripherals and localization | Select each additional CPU/AGA/NTSC/peripheral/language scope explicitly; add native and installed evidence, update requirements/help/listing and measure relevant throughput separately. |

## Evidence and maintenance

When scheduling, attach an issue/PR, named owner and target milestone to each task.
Close it only with a dated evidence link identifying the candidate and criteria
covered. Automated results, manual installed checks, native replays, producer
tests and throughput measurements must remain distinguishable. Store private
media and generated outputs outside Git; commit only redistributable records.

For implementation, use the repository's standard checks:

```powershell
dotnet build CopperScreen.slnx -c Release
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperDisk.Tests/CopperDisk.Tests.csproj -c Release --no-build --no-restore
dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests
```

Keep engine diagnostics outside production shared outputs. Native ROM/media
replays and applicable performance protocols run separately; unavailable inputs
must remain visible. Documentation-only maintenance of this roadmap requires
link/instruction checks, not emulator suites or new performance acceptance.
