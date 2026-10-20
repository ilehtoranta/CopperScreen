# MSIX packaging and installed validation

Implementation: 2026-10-16, [productization task P1-02](PRODUCTIZATION_ROADMAP.md#p1-02--build-the-store-package-and-installed-test-pipeline).
See the [initial validation record](MSIX_VALIDATION_2026-10-16.md) and subsequent
[signed CopperOS installed validation](COPPEROS_MSIX_VALIDATION_2026-10-17.md).
The [P1-06 signed media continuation](MEDIA_MSIX_VALIDATION_2026-10-17.md) adds
installed media protection, backup/import, recovery and reinstall restoration.
Local signed install/update/reinstall passes. The subsequent
[WACK validation](MSIX_WACK_VALIDATION_2026-10-17.md) reports overall PASS on signed
diagnostic and production-shape packages after the DPI manifest correction;
one optional executable-reference scan remains nonpassing in each report.
The [fresh P1-06 WACK continuation](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md)
also reports overall PASS for the exact signed `1.1.24.0` media-tested update:
all 13 required checks pass, with the optional blocked-executable FAIL retained.
The [P1-07 signed install/update continuation](RELEASE_MSIX_VALIDATION_2026-10-17.md)
qualifies the launcher layout fix and storage persistence. Its
[fresh WACK continuation](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) reports
overall PASS on the exact signed `1.1.26.0` update, with all 13 required checks
passing and the unchanged optional blocked-executable finding retained.
Earlier evidence remains bound to its recorded binaries; later package changes
need fresh installed and certification-kit validation.
The initial package target is Windows 11 desktop x64 (minimum build 22000).
Other architectures/Windows versions are not declared qualified by this work.
The repository packaging/test scripts do not publish to Store, reserve an
identity, provision certificates, change machine trust or enable Developer Mode.

## Building a Store candidate

Use PowerShell 7 on Windows with .NET 10 and the Windows SDK. Supply the exact
identity/publisher values from Partner Center; template values are not a release
identity. The current frozen Copper68k development package remains a prerequisite;
packaging does not release or replace that dependency.

```powershell
./scripts/package-msix.ps1 `
  -IdentityName $env:COPPERSCREEN_STORE_IDENTITY `
  -Publisher $env:COPPERSCREEN_STORE_PUBLISHER `
  -PublisherDisplayName $env:COPPERSCREEN_PUBLISHER_DISPLAY_NAME `
  -PackageVersion 1.1.3.0
```

Use a new output directory for every build. The script publishes a self-contained,
untrimmed x64 application, checks required native/runtime files, generates PNG
logos from the existing icon, copies notices and creates a semantically validated
MSIX with MakeAppx. It declares `runFullTrust` at `mediumIL` for the existing
desktop app and file/input libraries; it requests no elevation. The manifest uses
`packagedClassicApp` and the standard Windows file-system virtualization behavior.
See [Microsoft's packaging model](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes).
Runtime saves explicitly use the current package family's
`LocalState/CopperScreen` folder; they do not rely on virtualization of an
existing portable AppData directory. Same-family updates preserve this root;
uninstall/reset can remove it. Portable data and user-selected exports are separate.

The output retains the MSIX, `package-record.json`, per-file hashes, source input
hashes, SDK/tool information, publish/MakeAppx logs, a layout, per-project locks
and symbols outside the package. RID restore starts from checked-in locks, writes
only private output locks and rejects changes to their existing resolved package
versions/content hashes. The existing ZIP workflow is unchanged.

The installed probe shares the normal logging and window startup path, with an
isolated settings directory. Normal application startup now retains Windows'
normal process priority instead of requesting real-time priority. This is a host
startup policy change, not an emulator timing or throughput result.

`-CertificateThumbprint` optionally signs using a matching private key already in
the current user's Personal store. No key is exported or password accepted on the
command line. Locally installed signed packages must be trusted by the test
machine. Store submission does not require buying a public signing certificate;
Microsoft re-signs approved MSIX submissions. [Microsoft package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

CopperStart/CopperOS assets are not bundled by this initial pipeline. Qualified,
licensed artifact integration, the final identity, rights, Store eligibility,
listing and approval remain release prerequisites. No arbitrary media directory
is copied into a package.

## Diagnostic version pair

Installed validation uses explicitly marked packages with a unique
`CopperScreen.Test.<32 lowercase hex digits>` identity. They cannot replace a
customer/Store identity and must never be submitted as Store candidates. Only
`-InstalledTestBuild` compiles the diagnostic entry point. Normal builds exclude
that code and marker.

```powershell
$id = 'CopperScreen.Test.' + [guid]::NewGuid().ToString('N')
$common = @{
  IdentityName = $id
  Publisher = 'CN=CopperScreen Installed Tests'
  PublisherDisplayName = 'CopperScreen installed tests'
  InstalledTestBuild = $true
  CertificateThumbprint = $env:COPPERSCREEN_TEST_CERTIFICATE
}
$v1 = ./scripts/package-msix.ps1 @common -PackageVersion 1.1.3.0
$v2 = ./scripts/package-msix.ps1 @common -PackageVersion 1.1.4.0
./scripts/test-msix-installed.ps1 -BaselinePackage $v1.PackagePath -UpdatePackage $v2.PackagePath
```

Run on a dedicated interactive Windows test machine/account with the signing
certificate provisioned and trusted by its owner. The application is activated
through `IApplicationActivationManager`, requires real package identity, and
rejects elevated execution. Running the unpacked EXE cannot produce passing
installed evidence. No .NET SDK is needed by the installed application.

The pipeline checks:

1. Install baseline; activate a real desktop window and seed a personal profile,
   remembered machine, controller choice, volume/mute and recent media history
   using the production storage classes.
2. Update the same identity/publisher/architecture to a strictly higher version;
   activate again, verify values and file hashes, and verify that the installation
   path changes while the logical user-data path remains stable.
3. Uninstall, confirm the registration is gone and external synthetic user content
   is unchanged; reinstall and verify new package-private test state is clean.
4. Remove only the test registration created by this run, retaining evidence.
   A pre-existing identity makes the pipeline refuse to start. `-KeepInstalled`
   can retain an owned registration explicitly for diagnosis.

All diagnostic state uses a new run-specific subdirectory through the same
user-data APIs. No real customer profile is seeded or modified. Fixtures contain
no ROM/game data and do not establish emulation or gameplay compatibility.

Windows normally preserves redirected application data across updates and removes
it on uninstall. Existing unvirtualized user files and user-selected external
media have different retention behavior; the pipeline deliberately verifies a
new package-private run and an external original separately. Migration from an
existing portable installation and actual separate Windows accounts still need
their own acceptance checks. [Windows data behavior](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes#appdata-operations-on-windows-10-version-1903-and-later).

## Developer registration and unavailable coverage

An explicit `-DeploymentMode DevelopmentRegistration` can unpack and register the
MSIX layout when Developer Mode is already enabled. Unsigned packages can use
this mode. It tests real package activation and version changes, but it does not
test signed-MSIX trust, WindowsApps staging or Store acquisition. Its report keeps
`InstalledMsixAcceptance=false`, even if its checks pass. The pipeline never
silently falls back from install mode.

Reports use `PASSED`, `FAILED` or `UNAVAILABLE`. Missing signing/prerequisites,
activation failures, timeouts and incomplete reports never become successful
coverage. `installed-test-record.json` retains package hashes, identity/version,
deployment mode, per-phase results and cleanup outcome.

Installed protocol `copperscreen-installed-msix-v2` also retains a handle to each
activated process and requires exit code zero. After the phase report appears,
the process has a separate bounded `-PhaseExitTimeoutSeconds` allowance (default
30 seconds). The report records that allowance and the observed exit status;
nonzero exits, missing exit codes and timeouts fail the run. The earlier five-second
exit-timeout failure is retained in the [WACK continuation](MSIX_WACK_VALIDATION_2026-10-17.md).

## Certification and CI

```powershell
./scripts/test-msix-certification.ps1 -PackagePath $v2.PackagePath
```

Run WACK in an elevated shell on the dedicated trusted test machine with no
pre-existing installation of that identity. It retains raw logs/XML and a package
hash/result record and requires an unambiguous overall PASS. Its result is local
certification-kit evidence, not Microsoft Store certification. Diagnostic-package
results also do not qualify a different production package.

The report must identify the exact candidate/version, declare a complete run
and show every required check passing. Its per-check summary preserves optional
findings even when WACK reports overall PASS; this is not an all-checks-pass claim.
The production and diagnostic packages have separate records. Packaging also
extracts the published EXE manifest with Windows SDK MT, checks PerMonitorV2 and
`asInvoker`/no UI access, and records the manifest source hash.

The manual [installed workflow](../.github/workflows/msix-installed.yml) targets
a dedicated runner labelled `copperscreen-msix`, with an interactive Windows 11
x64 account. Configure `COPPERSCREEN_QUALIFIED_FEED` to the frozen NuGet input
directory. The workflow verifies the CPU package's SHA-512 against the checked-in
lock before restore. Signed install mode also needs the matching trusted signing
certificate; its WACK step needs an elevated runner shell while the activated app
must still run non-elevated. It never provisions trust or publishes packages.

Ordinary CI runs [tooling controls](../scripts/tests/test-msix-tools.ps1) for
identity/upgrade rejection, safe manifest parsing, archive metadata and icon
dimensions. Those synthetic ZIP fixtures are explicitly not deployment evidence.

## CopperOS native storage continuation

Diagnostic builds can include the exact development guest pair with
`-CopperOsSeed <System.adf> -CopperStartRom <CopperStart-DiskBoot.rom>` alongside
`-InstalledTestBuild`. Both parameters are required together and checked against
the embedded qualification metadata. Ordinary production packaging rejects these
proof inputs. The assets remain local build inputs, never repository content.

Without `-MediaPersistence`, that pair adds three native observations:
create/write/read/rename/delete settings and data on baseline; reopen the same
workspace after a process restart; reopen it again after the same-family package
update. It requires stable workspace identity and unchanged complete image hashes
on both reopen phases. Each phase uses the normal Lightweight app session and
retains separate native console captures and binary identities. Missing assets
produce `NativeCopperOsStorage: UNAVAILABLE`; profile/settings checks alone cannot
qualify guest storage.

Configure `COPPERSCREEN_COPPEROS_SEED` and `COPPERSCREEN_COPPERSTART_ROM` on the
dedicated runner. Its workflow now requires both artifacts. Use an already trusted
signing certificate and `DeploymentMode Install` for installed acceptance.
Source replays and changed-install-directory simulations are separate evidence.
See [storage contract, failure recovery and current qualification](COPPEROS_STORAGE.md).

## P1-06 installed media and backup protocol

Add `-MediaPersistence` to `scripts/test-msix-installed.ps1` when both signed
diagnostic packages contain the exact qualified guest pair. The dedicated CI
workflow enables this mode. It uses `copperscreen-installed-msix-v3` and requires
six successful, non-elevated package activations, each with a desktop window and
zero process exit code:

1. Baseline creates native files/settings. Separate synthetic Paula-DMA controls
   check protected ADF/ZIP imports, explicit replacement, locked/missing output,
   dirty eject/reset, save and reopen. Workspace controls export/import a
   versioned backup, refuse replacement/invalid import, and recover damaged and
   missing primaries while preserving the damaged image and previous checkpoint.
2. A fresh baseline process reads native files and verifies all recorded media,
   identities, backups, profiles and settings hashes.
3. A higher package version reads them again from the changed WindowsApps
   directory, with the same package-family LocalState path.
4. The update runs native writes with a read-only primary. The failed checkpoint
   must retain its primary and dirty state while live ADF and `.copperos` exports
   succeed. A backup is imported into a new package-private workspace.
5. A fresh update process boots that imported workspace and reads its native files.
6. After uninstall/reinstall, the old test directory must be empty/absent and all
   external originals and exports must remain intact. The app imports the external
   backup into a new workspace and verifies its contents through a native boot.

`MediaPersistenceAcceptance=true` requires signed Install mode and all these
checks. Development registration retains false. Media and export paths/hashes,
native transcripts and loaded binary hashes are separate from profile/settings
fixtures. The synthetic media control uses its own machine configuration so a
saved profile cannot supply different media to it. No real customer files or
profiles are modified. Diagnostic classes remain excluded from production builds.

The phase timeout is 900 seconds by default (configurable up to 1800); process
exit has a separate 30-second default allowance. Native field limits and expected
content are unchanged. These are correctness timeouts, not throughput results.
Failure reports retain the phase report even if the process exits unsuccessfully.

The automation exercises production storage services under package identity;
opening the desktop window does not establish a manual native-file-picker or
recovery-confirmation walkthrough. Shipping identity/assets, those interactive
checks, separate accounts and final-package WACK remain separate release gates.

## P1-07 installed launcher layout continuation

Add `-LauncherLayout -MediaPersistence` to require
`copperscreen-installed-msix-v4`. The dedicated installed workflow enables both.
All six native storage/media phases above must pass. In each activation, the
diagnostic app also opens the standard CopperOS launcher in a separate,
test-owned settings directory, resizes its native window to logical client sizes
800×600, 1280×720 and 960×640, and scrolls “Save launch profile” into view.
The probe checks the actual client bounds, positive control dimensions, bounded
scroll viewport and absence of horizontal overflow. The controller independently
rejects missing, non-finite or clipped measurements and missing captures.

`LauncherLayoutAcceptance=true` requires signed Install mode and all 18 layout
observations alongside the persistence protocol. Development registration cannot
set this acceptance flag. The measured `renderScaling` is the current physical
monitor's actual scale; the probe does not simulate a DPI change or certify
other monitors. PNGs under `launcher-layout-*/` are renders of the installed
native window's control tree, not desktop screenshots or manual interaction
evidence. Layout settings remain separate from the profile/settings hashes used
to qualify restart/update persistence. These diagnostic observers are excluded
from ordinary production builds.

The layout fixture uses the empty-workspace CopperOS view: Start and Save remain
disabled until a workspace is selected. Its assertions establish rendered control
visibility and scroll reachability; native saves and profile/settings persistence
are exercised separately by the storage phases.

Retain failed reports and packages unchanged, and use a new run identity and
versions after any correction. See the
[P1-07 signed continuation](RELEASE_MSIX_VALIDATION_2026-10-17.md) for the exact
local packages and their results. This continuation does not extend an earlier
package's WACK verdict to newly built binaries. The separate
[P1-07 WACK record](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) covers its exact
signed update.

## P1-07 installed hour sessions

[Installed sessions v1](INSTALLED_SESSIONS.md) runs two sequential 3,600-second
normal-window CopperStart/CopperOS sessions from a new signed diagnostic identity,
then fresh-process storage/media/profile/settings checks and workspace exports.
It records exact payloads, monotonic samples, actual WASAPI callback queue
starvation, memory/log growth and retained failed attempts. It does not perform
an update or replace WACK. The [2026-10-18 evidence](INSTALLED_SESSIONS_2026-10-18.md)
records successful duration/persistence checks on `1.1.30.0`, with audio starvation
and pacing findings still open. Ordinary production builds exclude these observers;
the new diagnostic package hash does not inherit the `1.1.26.0` WACK verdict.
The [audio refill correction and rerun](INSTALLED_AUDIO_PACING_2026-10-18.md)
records two full hours plus native persistence reopens on signed `1.1.31.0`, with
zero software queue starvation. This is a separate exact-package record; the
earlier audio findings and WACK verdict remain unchanged. Release budgets and
physical audio/display qualification remain open.
The [signed audio/pacing upgrade continuation](AUDIO_MSIX_VALIDATION_2026-10-18.md)
now qualifies an actual pre-fix `1.1.30.0` to fixed `1.1.31.0` upgrade, using the
exact unchanged hour-tested update package. Six native activations preserve
CopperOS workspace, profiles and settings through restart/update and qualify
denied-save recovery and clean-reinstall backup restoration; all 18 launcher
observations pass. Existing dated evidence remains unchanged. The
[fresh audio/pacing WACK continuation](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md)
reports overall PASS for this exact `1.1.31.0` hash, with all 13 required checks
passing. The optional blocked-executable scan retains the same 24 messages;
final production/Store-identity WACK and release budgets remain open.

The separate [installed recovery protocol](INSTALLED_RECOVERY.md) adds bounded
active/paused sleep and audio-device checks with a diagnostic-only control channel
and a scoped elevated Windows helper. Its
[first current-PC continuation](INSTALLED_RECOVERY_2026-10-18.md) is PARTIAL on
signed `1.1.32.0`; it retains completed CopperStart S3 observations and the failed
host device method. It does not inherit `1.1.31.0` WACK/update or full-hour verdicts.
The [authorized endpoint retry](INSTALLED_RECOVERY_RETRY_2026-10-18.md) retains a
second pre-execution Windows consent failure, verified enabled audio devices and
absent diagnostic registration. It adds no installed-app coverage.

The [native reopen correction](INSTALLED_RECOVERY_REOPEN_2026-10-18.md) retains
the later `1.1.33.0` installed failure: native restart returned `MA_UNAVAILABLE`
after paused S3, and actual endpoint restoration did not repair the frozen
session. Signed `1.1.34.0` reopens the original backend while retaining queued
audio and guest state. The production build, ten exact-package host controls,
signature and 266 payload files pass verification. The fresh installed matrix
still requires its restarted helper; no native recovery/save/reopen, update or
WACK verdict is inherited from an earlier package. Release budgets remain OPEN.

The [subsequent diagnostic continuation](INSTALLED_RECOVERY_DIAGNOSTICS_2026-10-18.md)
retains `1.1.34.0` failing its active-S3 follow-up. Signed `1.1.35.0` adds bounded
native warning/error capture and a full native device-close control; production
recovery is unchanged. Two installed software-only runs pass native restart/reopen,
exports and fresh-process persistence for CopperStart and CopperOS. The native
controls do not interrupt Windows or qualify physical sound. A separately hashed
physical controller adds explicit display-on preparation after timer wake; its
subsequent [attended physical continuation](INSTALLED_RECOVERY_PHYSICAL_2026-10-18.md)
passes real active/paused S3, actual HDMI disable/restore and native
checkpoint/export/fresh-process disk/workspace/profile/settings persistence for
both boot paths. The owner confirms CopperStart audible recovery. Transient native
errors, CopperStart sample gaps, display-off recovery and release budgets remain
open findings; this adds no update, WACK or new full-hour verdict for this hash.

The [display/audio continuation](AUDIO_DISPLAY_RECOVERY_2026-10-18.md) records new
signed diagnostic `1.1.36.0` and `1.1.37.0` packages with explicit frozen source and
payload attribution. The [version-4 display-only mode](INSTALLED_DISPLAY_RECOVERY.md)
needs no elevated helper. Both boot paths pass observed display-off/on recovery,
native checkpoint/export and fresh-process workspace/profile/settings persistence.
Version 37 primes the existing reserve before native playback and requests MMCSS
for the producing runtime thread. Residual audio underruns remain OPEN. These
results do not extend older update, WACK, physical S3/PnP or full-hour verdicts to
either new package. Production/Store identity and release budgets remain OPEN.

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
