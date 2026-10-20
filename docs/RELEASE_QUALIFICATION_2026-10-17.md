# P1-07 development qualification — 2026-10-17

**PARTIAL: bounded correctness and desktop controls pass; release acceptance
remains open.** Nine complete timing runs preserve the reviewed identities and
zero measured allocations, but every timing sample remains **INVALID / RERUN**
under concurrent load. The owner authorized provisional current-PC observations
and explicitly left minimum/reference hardware and numeric release budgets open.

The [machine-readable evidence](RELEASE_QUALIFICATION_2026-10-17.json) binds source,
binaries, inputs, TRX results/skips, native receipts/captures, layout controls and
timing telemetry. See the [protocol and remaining checks](RELEASE_QUALIFICATION.md).
Lightweight remains the sole guest engine. No Legacy, host OS shim or WHDLoad
support was added.

## Correctness and desktop results

| Check | Passed | Unavailable | Result |
| --- | ---: | ---: | --- |
| Final production Release solution build | — | — | PASS; zero warnings/errors |
| Native-enabled host suite | 273 | 4 optional groups | PASS |
| Host controls after the final launcher fix | 265 | 12 optional native entries | PASS |
| Desktop suite with the qualified CopperOS pair | 22 | 0 | PASS |
| Separate engine diagnostics | 1,247 | 7 optional native groups | PASS; isolated diagnostic outputs |
| CopperDisk suite | 74 | 0 | PASS |
| Qualification tooling controls | 38 | — | PASS; synthetic harness controls |

The host suites overlap; their counts must not be added as distinct coverage.
Four native groups were not enabled in the bounded host run: Kickstart 3.1 floppy,
Kickstart 3.1 hard-disk, A1200/3.0 and the earlier read-only CopperOS boot proof.
Their actual skipped entries remain recorded. The writable CopperOS proof below
supplies separate execution evidence. Skips are unavailable coverage, not PASS or
proof that inputs do not exist.

Current dependency: `Copper68k/1.5.4-qualified-dev.1`, SHA-256
`f445a7d289cf6545c46731a0d94e59e7c8c56b189a0c43768f4009874c0cb6f2`.
That pin was already in the repository; no dependency or producer ROM was changed
or published. Engine/disk sources match the captured source set. Native checks,
post-fix checks and the runner retain separate assembly hashes and artifact
directories; they are not one byte-identical package. No cycle order, hardware
behavior or golden changed.

## Bounded compatibility matrix

| Path | Boot and interaction | Video / guest PCM | Saves and reopen |
| --- | --- | --- | --- |
| Native Kickstart 1.3 / Lemmings | Retained 18,120-field gameplay and keyboard/reset regressions pass | Bounded CPU/output identity and active PCM pass | Title-specific saves/reopen and full-game completion not qualified |
| CopperStart / Lotus II | Original disk boot/input, reset/reinsert and scripted opening race pass | Nine scalar-parity image/state checkpoints; per-field PCM parity; 9,979 audible fields of 14,000 | Imported original read-only; title-specific saves and complete game not qualified |
| CopperStart / CopperOS storage | Seven native boots pass Shell file/settings mutations and expected recovery/error cases | Bounded text assertions pass; silent CLI proof supplies no OS audio demonstration | Native saves, restart, directory-change simulation, export/import and reopen pass |

Profile: PAL OCS A500/68000, 512 KiB Chip + 512 KiB slow RAM. CopperStart uses
standalone `diskboot-2026-10-16-dev.8`; CopperOS uses pair
`a500-copperos-cli-boot-proof-dev.1.31e326e7505f`. CopperStart captures at fields
6,000, 11,000 and 14,000 were inspected; the latter two show race countdown/gameplay.
Scalar parity is a regression control, not an independent hardware oracle or a
broad compatibility claim. Actual host audio-device delivery was not qualified.

The seven CopperOS receipts cover native mutations/settings, restart, a changed
package-directory simulation, write-protected/full-volume errors, denied host
checkpoint with dirty data retained, external ADF/versioned backup export and
imported workspace readback. Normal saved/reopened images retain SHA-256
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
The directory-change case is a source simulation. Actual WindowsApps update and
reinstall persistence remain separately bound to the
[earlier signed P1-06 packages](MEDIA_MSIX_VALIDATION_2026-10-17.md).

## Desktop correction

A bounded launcher window could retain an auto-sized middle row and a minimum
launcher height larger than the available client area. After layout settled,
its scroll viewport extended outside the window, leaving Save launch profile
unreachable. The launcher now uses the available star row and its viewport can
shrink. Windowed guest sizing and full framebuffer generation remain unchanged.

The **same test assembly** runs against the retained earlier app binary and the
final corrected binary. All four cases fail on the earlier app and pass on the
correction: logical 800×600 at 100%/125%, 1280×720 at 150%, and 960×640 at 200%.
Assertions wait for platform resize/layout, check rendered button targets and
actual client bounds, and retain captures plus layout metadata. Visual inspection
confirms the saved-profile control fits the corrected 800×600 client area.

The desktop suite also verifies focus loss/explicit input release without key-up,
a fresh press/release afterward, Alt+Enter with the same runtime, keyboard
pause/resume and accessible-name navigation, five text contrast pairs, qualified
CopperOS launch/pause/restart/stop/resume and confirmed checkpoint recovery.
These headless controls do not qualify physical HID reconnect, monitor transitions,
file pickers or assistive tools.

Adding another UI test class exposed independent disposal of a per-assembly
Avalonia session. One collection-scoped fixture now owns the shared dispatcher.
The aborted/failed run and earlier layout attempts remain retained with their
original outcomes. No production test hook was added.

## Current-PC timing observations

Host: Windows 11 Home x64 build 26200, Ryzen 5 5600X, six cores/twelve logical
processors. `copperscreen-release-engine-baseline-v1` rechecks homogeneous topology,
pins CPU 2 at Normal priority, protects SMT siblings 2–3 and records power plan,
.NET 10.0.12, frozen binary/runtime/input hashes and native-counter telemetry.
The immutable October 12 contract supplies expectations before timing; none was
learned or rebased here.

Strict preflight rejected competing jobs before collecting timing samples.
A separate `-LoadedHostDiagnostics` run retains all nine complete observations.
All 359 telemetry intervals identify other build/test jobs; aggregate interference
peaks at 100%. All 359 load-policy violations remain recorded. No other job was
paused/terminated, and this task ran no builds/tests during timing.

**These are INVALID timing observations under concurrent load**, not a valid
baseline, regression/speedup comparison, real-time desktop result or budget
decision. Changing load prevents causal comparison of workload means.

| Engine workload | Samples | Mean FPS | FPS range | Mean ms/field |
| --- | ---: | ---: | ---: | ---: |
| Lores, full output and Paula DMA | 3 | 179.44 | 122.69–279.70 | 6.3607 |
| Hires, full output and Paula DMA | 3 | 267.93 | 238.82–308.64 | 3.7762 |
| Native Lemmings | 3 | 267.38 | 217.86–304.08 | 3.8158 |

All complete identities match; steady-state allocations are zero and unsupported
features absent. Frozen runner/dependency/runtime/ROM/media/script/harness hashes
remain unchanged. No samples were discarded or pooled. Historical records retain
their original scope/disposition. The per-change 1% gate was not evaluated.

## Evidence and remaining gates

Local evidence remains under
`artifacts/p1-07-2026-10-17-09b6422047dd4b0e86598b49320154e4/`.
Native receipts remain in separately dated `negative-replays`, `storage-replays`
and `recovery-replays` folders of the frozen CopperOS proof; JSON references bind
their exact paths/hashes. Generated media/captures stay ignored. Earlier signed
and WACK evidence hashes were checked and preserved.

P1-07 remains open for P0-02's advertised matrix, named minimum/reference PCs and
numeric budgets; installed 60-minute sessions per declared boot path; presentation
pacing, actual sink underruns and memory/log growth; physical controller/remapping/
reconnect, capture, multi-monitor DPI, sleep/resume and audio-device changes;
and installed file-picker/assistive-tool review. An unavailable measurement is
not zero. Native test elapsed time is not a sustained installed desktop session.

A package containing the launcher correction needs fresh signed install/update
validation and WACK. The [previous WACK result](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md)
qualifies only its recorded `1.1.24.0` binary. Final assets, Store identity,
acquisition and certification remain separate gates. Nothing was published,
submitted or committed by this work.
