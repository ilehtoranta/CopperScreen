# P1-07 signed MSIX audio/pacing update validation — 2026-10-18

**PASS for this signed local diagnostic install/update continuation. P1-07 remains
in progress; release budgets remain OPEN.** The retained pre-fix `1.1.30.0` payload
upgrades to the exact signed `1.1.31.0` audio/pacing candidate. CopperOS workspace,
profile and settings persistence pass, alongside media protection, denied-save
recovery, backup restoration and launcher checks. Every native replay uses
Lightweight; Legacy remains unavailable and no host DOS shims are added.

The [hash-bound record](AUDIO_MSIX_VALIDATION_2026-10-18.json) retains package
provenance, signatures, loaded binaries, all six phases, native transcripts,
captures, exports and host-state snapshots. Earlier
[launcher/update evidence](RELEASE_MSIX_VALIDATION_2026-10-17.md),
[installed hours](INSTALLED_SESSIONS_2026-10-18.md) and
[audio/pacing correction](INSTALLED_AUDIO_PACING_2026-10-18.md) remain unchanged.

## Exact upgrade and signing

Diagnostic family: `CopperScreen.Test.af6f00c7f3b04b94ab921e70b62d2229`.
The prior hour run had removed its registration and private storage; both were
independently verified absent before this new install. Existing customer families,
profiles and workspaces are outside this test.

| Package | SHA-256 |
| --- | --- |
| Baseline `1.1.30.0`, derived diagnostic identity | `99cd8b3cb93569e31d5d304d682a6215d2c845336a9798e9dddf4ee92d590f3b` |
| Update `1.1.31.0`, unchanged hour candidate | `640d6040998d7d5fca19207224a7cd5627f46045dc39f989e74dae24004ae01e` |

The baseline comes from the retained signed package with SHA-256
`48ebeefebca8e44c9b9c9bf6fa487ae553ef09d9635d56192159275327f055c1`.
Only its manifest identity name changes to permit a same-family update; it is
then newly packed and signed. All other 265 payload files remain unchanged.
The donor package and its historical evidence are preserved. Metadata inspection
confirms the baseline contains the old steady-video throttle/deadline methods
and the update removes them.

The update is copied byte-for-byte from the completed two-hour audio/pacing run;
it is neither rebuilt nor repacked. Subsequent working-tree changes are outside
these package hashes. Each signed archive's complete 266-file payload is checked
against its retained manifest. Loaded app and Lightweight engine hashes in every
native phase match the appropriate signed payload.

The existing trusted `CN=CopperScreen Installed Tests` certificate signs the
baseline; the update retains its existing signature. SignTool `/pa /v` succeeds
for both with zero warnings/errors. Trust is unchanged and no private key is
exported. These are local diagnostic packages containing development proof assets,
not an admitted Store shipping payload.

## Installed results

The frozen [installed controller](../scripts/test-msix-installed.ps1), using
`-DeploymentMode Install -MediaPersistence -LauncherLayout`, completes
`copperscreen-installed-msix-v4`. Windows performs genuine signed deployment,
not development registration. All six COM-activated processes open their desktop
window, pass their phase and exit zero without elevation.

| Phase | Version | Observed result |
| --- | --- | --- |
| Seed | 1.1.30.0 | Native create/write/read/rename/delete and settings saved; media and backup controls pass. |
| Baseline restart | 1.1.30.0 | Same native workspace image, profile and settings read back. |
| Update | 1.1.31.0 | New WindowsApps directory; identical LocalState/workspace identity and saved hashes. |
| Denied checkpoint | 1.1.31.0 | Host save refused; dirty state retained; live ADF and versioned backup exported/imported. |
| Recovery restart | 1.1.31.0 | Imported recovery workspace boots and verifies native files/settings. |
| Clean reinstall | 1.1.31.0 | Prior package-private state removed; external backup restored and verified by native readback. |

The native saved image is identical in all six phases:
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
The frozen standalone ROM and CopperOS seed/pair match their qualified hashes.
Native transcripts retain `COPPEROS STORAGE REOPEN VERIFIED` after reopening.
The two valid exported backup archives independently pass format-1 metadata,
901,120-byte image, seed/pair and image-hash checks; the intentionally invalid
backup remains a separate rejection control.

All three host profile/settings files remain byte-identical through baseline
restart and update. The fixture preserves its non-ASCII profile name, RTC,
port-2 controller assignment, writable-media choice, recent-media history,
volume 37 and mute. Copies of 15 host-state files are retained outside
package storage, hash-checked against their phase reports. Fresh reinstall
requires empty host profile/settings state before importing the external backup.

All **18 installed launcher observations** pass at 800×600, 1280×720 and 960×640,
with the current monitor's measured **100% scaling**. The profile-save control is
visible after scrolling, with no horizontal overflow. Baseline/update 800×600
renders were visually inspected. This empty-workspace layout fixture keeps Start
and Save disabled until a workspace is selected; native persistence is exercised
separately. These observations do not certify mixed-DPI transitions or manual
file-picker interaction.

Cleanup removes only the diagnostic registration and family-private storage.
External originals, saved ADFs, backups, native transcripts, layout captures and
host-state copies remain in the evidence directory. Original imported-media
hashes remain intact through update, uninstall and reinstall.

## Verification and remaining gates

- The full installed v4 protocol passes, including six native activations and
  all process-exit, persistence, media/recovery and layout assertions.
- Packaging-tool controls: **64 passed**. These synthetic controls are separate
  from the real installed/native observations above.
- Every nested hash reference in the earlier audio/pacing record still matches.
  The candidate's production build, focused audio controls, host/desktop tests,
  native parity and full-hour results remain in that earlier exact-package record;
  this continuation does not rebuild or relabel them as new tests.
- All 27 previously dated top-level evidence files captured at
  preflight remain unchanged. The record also preserves an INVALID ancillary
  collector attempt and its corrected passing hash audit; no installed phase failed.

The exact update hash therefore has both separate full-hour audio evidence
(zero software queue starvation/callback errors in both hours) and a passing
old-to-fixed signed upgrade/persistence result. This run adds no new hour-long
audio measurement and makes no physical audio/video or performance-budget claim.
Host timing variation and release budgets remain open.

**Fresh WACK on this exact `1.1.31.0` package remains open.** The earlier
[`1.1.26.0` WACK verdict](RELEASE_MSIX_WACK_VALIDATION_2026-10-17.md) is unchanged
and does not cover this hash. Final production/Store identity, clean-machine and
Store acquisition/update validation, the remaining physical desktop/device matrix,
and minimum/reference hardware and numeric budgets remain release gates.
