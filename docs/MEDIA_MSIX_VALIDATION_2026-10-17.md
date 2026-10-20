# P1-06 signed MSIX media validation — 2026-10-17

**PASSED:** signed diagnostic installation/update, protected media, native saves,
workspace backup/import/recovery, and restore after uninstall/reinstall.
Both `InstalledMsixAcceptance` and `MediaPersistenceAcceptance` are true.
Lightweight owns execution; Legacy and host DOS shims remain unavailable.
This is local installed acceptance, not Store certification or shipping admission.

The [machine-readable record](MEDIA_MSIX_VALIDATION_2026-10-17.json) binds the
packages, payload hashes, signing, loaded binaries, phase reports, native
transcripts, media hashes, cleanup, controls and retained failed attempt.
Earlier [source qualification](MEDIA_PERSISTENCE_VALIDATION_2026-10-17.json),
[signed storage validation](COPPEROS_MSIX_VALIDATION_2026-10-17.json) and
[WACK records](MSIX_WACK_VALIDATION_2026-10-17.json) remain unchanged.

## Candidate and signing

The Windows x64 host reports build 26200. The already trusted local test
certificate was reused: `CN=CopperScreen Installed Tests`, thumbprint
`510C8C13859A287EFFE367DB25E2BB05E9AA34AB`, expiration
`2026-10-30T21:06:30Z`. No trust setting changed and no private key was exported.
Windows SDK SignTool `/pa` verification passed for both packages with zero
warnings/errors. These local test signatures are not timestamped.

Identity: `CopperScreen.Test.8d0a9bf60d3d4b7cbf8cd470ed20d9fc`.

| Signed diagnostic package | SHA-256 |
| --- | --- |
| `1.1.23.0` baseline | `65847e8aea04ab734e5cb41d7980f7e7709b57ae6ee4d87b4a13593b3a4eab35` |
| `1.1.24.0` update | `0d11e9de6e032de49d391c819ce847e74023d72f76b85f7474e2192f9eb9f1cc` |

All 266 payload hashes match each package's build record. Both packages have
identical source input hashes; their version metadata differs. Loaded native
replay assembly hashes match the corresponding signed payloads. Production
assembly metadata inspection confirms all installed diagnostic classes are absent.
Only the frozen, hash-bound CopperStart/CopperOS development proof assets are
included in these diagnostic packages. No ROM/system image or CPU dependency
was rebuilt or published. Production asset admission remains unchanged.

## Installed results

| Activation | Version | Result |
| --- | --- | --- |
| Seed and media controls | `1.1.23.0` | PASS; native create/write/read/rename/delete and settings; protected imports, explicit ADF save, backup/import and previous-checkpoint recovery. |
| New-process readback | `1.1.23.0` | PASS; native files and all recorded workspace/media/profile/settings hashes retained. |
| Package-update readback | `1.1.24.0` | PASS; WindowsApps directory changed; package-family LocalState, identities and content retained. |
| Denied native checkpoint | `1.1.24.0` | PASS; read-only primary unchanged; dirty state retained; live ADF and versioned backup exports succeed; backup imported into a new package-private workspace. |
| Imported recovery workspace readback | `1.1.24.0` | PASS; a fresh packaged process boots the imported workspace and verifies native files. |
| Clean reinstall and external backup restore | `1.1.24.0` | PASS; old diagnostic directory empty/absent; external originals/exports retained; backup imported into a new workspace and read through native CopperOS. |

All six activations opened a real desktop window, ran non-elevated under the
expected package identity and exited with code zero. Windows installed the
packages into WindowsApps; these were not development registrations. The six
native boots retain the reviewed complete image SHA-256
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
Native files include `S:User-Settings` (`volume=37` plus LF) and
`S:Persistent-Data` (`CopperOS data survives restart` plus LF).

The ordinary ADF/ZIP case is a separate synthetic Paula-DMA device control,
not an OS/ROM compatibility replay. It checks read-only import, another mounted
original's protection, refusal of implicit replacement, locked and missing output,
dirty eject/reset, explicit save and reopen. Both imported originals remain
byte-identical. Workspace controls reject invalid imports, existing workspace
replacement, existing backup replacement and exports into managed folders.
Recovery validates `Previous.adf`, preserves damaged current bytes separately,
and restores a missing primary. These use production storage services under
package identity; no shipping fault hooks were introduced.

Profiles/settings retain a non-ASCII name, controller/RTC/media choices,
volume 37, mute and recent media. Their three files remain byte-identical through
restart and update; bundled presets remain unchanged. Final cleanup confirms
the owned registration and entire run-specific data directory are absent, while
external originals and exports still match their recorded hashes. The signing
certificate remains provisioned for later local tests.

## Corrections and controls

The first signed pair (`1.1.21.0` → `1.1.22.0`, different identity) remains
**FAILED**. Its native write passed, but the new synthetic device control loaded
the profile fixture's deliberately empty disk. The control now constructs an
independent machine configuration; a regression checks coexistence with that
saved profile. The failed package/phase reports and binaries are retained.

A host interruption control also observed a sharing violation immediately after
the killed writer signalled exit. The expected primary/previous bytes matched,
and the lease became available afterward. The harness now waits at most five
seconds for Windows sharing-lock release **after** its writer exits; a live-owner
lock still fails immediately and all image assertions remain unchanged. That
failed TRX is retained alongside the accepted rerun.

Final verification: production Release solution build, zero warnings/errors;
265 host checks passed, with 12 optional native entries unavailable in that
command; 49 MSIX tooling controls and changed-script parsing passed. The signed
native boots above provide separate execution evidence. No engine behavior,
timing, expected fingerprint or package dependency changed.

## Reproduction and remaining gates

Build a fresh uniquely identified signed diagnostic pair with the qualified
`-CopperOsSeed`, `-CopperStartRom` and `-InstalledTestBuild` inputs, then run:

```powershell
./scripts/test-msix-installed.ps1 -BaselinePackage '<baseline.msix>' -UpdatePackage '<update.msix>' -MediaPersistence -OutputDirectory '<new evidence directory>'
```

See [protocol, timeout and CI details](MSIX_PACKAGING.md#p1-06-installed-media-and-backup-protocol).
Local evidence remains under
`artifacts/p1-06-msix-signed-2026-10-17-8d0a9bf60d3d4b7cbf8cd470ed20d9fc/`;
the failed identity is recorded through its `run-config.json` reference. Artifacts
remain ignored; nothing was committed or submitted to Store.

Native file-picker and recovery-confirmation UI walkthroughs were not performed
by this automation. Separate-account/clean-machine, Partner Center acquisition,
final shipping identity/assets and final-package WACK remain release gates.
No new WACK run is claimed for these packages. Physical power loss, host quota
exhaustion and removable-device detachment were not tested; this does not promise
a transactional guest filesystem. There is no performance result.
