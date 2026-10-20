# P1-07 signed MSIX 1.1.43.0 install/update validation — 2026-10-19

**PASS for this signed local diagnostic install/update continuation. P1-07 and
release budgets remain OPEN.** Windows upgrades the retained `1.1.42.0` payload
to the exact signed `1.1.43.0` warmup/publication candidate. Native CopperOS
workspace, profile and settings persistence pass through restart and update.
Protected media, denied-save recovery, backup restoration after reinstall and
installed launcher checks also pass. Lightweight remains the engine; Legacy is
unavailable and no host DOS shims are restored.

The [machine-readable record](AUDIO_MSIX_VALIDATION_2026-10-19.json) binds the
packages, controller, native transcripts, captures, exports and host-state
snapshots. The prior [warmup/refill evidence](AUDIO_WARMUP_REFILL_2026-10-19.md)
and [1.1.31.0 update evidence](AUDIO_MSIX_VALIDATION_2026-10-18.md) are unchanged.
The qualification date uses Europe/Helsinki; individual receipts retain UTC.

## Exact package pair

Family: `CopperScreen.Test.40a6779884774716bfe9998be653a665`. Registration and
family-private storage were independently absent before this run. This reuses
the audio-tested diagnostic family only after its earlier cleanup; customer
families and unrelated diagnostic packages are outside the test.

| Package | SHA-256 |
| --- | --- |
| Derived baseline `1.1.42.0` | `6524286f39a3143500c29d7f25b6b5435c24dcf252b5c8eafc8afb57c3ae1ad8` |
| Unchanged update `1.1.43.0` | `7a34dc3fbe845959d1ea94f1e85e5b61031e55064797a9f73a42d098319cad02` |

The donor 42 package is retained with SHA-256
`81dc331555a74dd265fc063bfd12b0e8f31ad887fa0666feed0a7278916445c1`.
Only its manifest identity name changes to match the 43 family; the manifest's
remaining semantics and all other **265 payload files** are preserved. The
derived package is newly packed and signed. The donor and its earlier failed
audio verdict are unchanged: using it as a persistence baseline does not
relabel its audio findings.

The update is copied byte-for-byte from the completed audio qualification. It
is neither rebuilt, repacked nor re-signed. Subsequent shared-tree development
is outside these package hashes. All **266 payload files** in each signed
archive match the retained manifests; loaded app/Lightweight binaries in every
native phase match their corresponding signed payload.

The existing trusted `CN=CopperScreen Installed Tests` certificate signs the
derived baseline. SignTool `/pa /v` succeeds for both packages with zero warnings
and errors. No trust changes or private-key export occur. These are local
diagnostic packages with development proof assets, not Store shipping artifacts.

## Installed results

The frozen [controller](../scripts/test-msix-installed.ps1), using real signed
`Install` deployment with `-MediaPersistence -LauncherLayout`, completes
`copperscreen-installed-msix-v4`. This is not development registration. Six
COM-activated processes open their desktop window without elevation, pass their
native phase and exit zero.

| Phase | Version | Result |
| --- | --- | --- |
| Seed | 1.1.42.0 | Native DOS create/write/read/rename/delete and guest settings saved; media/backup controls pass. |
| Baseline restart | 1.1.42.0 | Same native workspace and host profile/settings read back. |
| Update | 1.1.43.0 | Different WindowsApps version directory; identical LocalState path, workspace identity and saved hashes. |
| Denied checkpoint | 1.1.43.0 | Actual host save refused; dirty guest state retained and live ADF/versioned backup exported and imported. |
| Recovery restart | 1.1.43.0 | Imported recovery workspace boots and verifies native files/settings. |
| Clean reinstall | 1.1.43.0 | Prior private state removed; external backup restored and verified by native readback. |

All six native saved-image hashes are
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
The standalone ROM, seed and workspace pair match the qualified inputs. Native
transcripts preserve `COPPEROS STORAGE REOPEN VERIFIED`. Both valid exported
backup archives contain the verified 901,120-byte image and export metadata;
the deliberately invalid archive remains a separate rejection control.

All three host profile/settings files remain byte-identical through baseline
restart and update. The fixture retains its non-ASCII profile name, RTC, port-2
controller assignment, writable-media choice, recent-media history, volume 37
and mute. The controller now copies each real host file immediately after its
phase exits, then checks the copy against the app's hash. **15 copies** survive
uninstall for independent inspection. Fresh reinstall requires empty prior host
profile/settings state before importing the external workspace backup.

All **18 installed launcher observations** pass at 800×600, 1280×720 and 960×640
with measured 100% scaling. The Save action is visible after scrolling and
there is no horizontal overflow. Baseline/update 800×600 renders were visually
inspected. This fixture keeps Start/Save disabled until a workspace is selected;
native persistence is tested separately. It adds no mixed-DPI or manual
file-picker walkthrough verdict.

Cleanup removes only this diagnostic registration and its family-private
storage. External originals, exports, backups, transcripts, captures and host
snapshots remain outside package storage. Their recorded hashes remain intact.

## Verification and limits

The installed protocol passes all six phase, native storage, normal-exit,
persistence, media/recovery and layout checks. **64 packaging-tool controls**
also pass; these synthetic fixtures are separate from installed/native evidence.
The controller change adds inspectable hash-checked host-state snapshots and
does not change the signed application. All 1,626 direct references of the audio
qualification remain unchanged. Of 64 dated files captured at preflight, 63 are
byte-identical. One unrelated RDB documentation note changed during the run;
its before/after hashes and an observed copy are retained, and this continuation
leaves that source note untouched. It is not an input to this upgrade or the
audio qualification. The initial collector's overbroad all-history-unchanged
assumption is retained as an **INVALID ancillary attempt** alongside the corrected
audit. No installed phase failed.

The exact update hash now has separate bounded audio/recovery evidence and a
passing signed install/update/reinstall result. This continuation performs **no
new continuous audio measurement**, physical sound/recovery check, full-hour
run, formal performance acceptance or WACK. It does not transfer the older
1.1.31.0 WACK/hour verdicts to 43. **Fresh WACK for this exact hash remains OPEN**.
Production/Store identity, clean-machine Store acquisition/update, remaining
physical desktop/device coverage, named hardware and numeric release budgets
remain release gates. Nothing was published.