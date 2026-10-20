# CopperOS signed MSIX validation — 2026-10-17

**PASSED:** local signed diagnostic installation, native CopperOS creation,
process restart, same-family package update, uninstall and clean reinstall.
`InstalledMsixAcceptance` is true; `StoreCertified` remains false. Lightweight
owns all guest execution; Legacy and host DOS shims remain unavailable.

The [machine-readable record](COPPEROS_MSIX_VALIDATION_2026-10-17.json) binds
packages, signing, installed phase reports, native transcripts, binary hashes,
independent filesystem verification and the earlier failed attempt. The
[2026-10-16 source qualification](COPPEROS_STORAGE_2026-10-16.json) is unchanged.

## Signing and candidate

The interactive, non-elevated Windows 11 Home x64 host reports build 26200.
Initially it had no code-signing certificate. With explicit user authorization,
a dedicated self-signed certificate was created in `CurrentUser/My`; its public
certificate was imported into `LocalMachine/TrustedPeople` through an elevated
helper. Its private key is non-exportable and was never exported. Developer Mode
was not enabled. Ordinary packaging/testing scripts still do not provision trust.

- Publisher: `CN=CopperScreen Installed Tests`.
- Certificate thumbprint: `510C8C13859A287EFFE367DB25E2BB05E9AA34AB`.
- Expiration: `2026-10-30T23:06:30+02:00`.
- Identity: `CopperScreen.Test.0ec4c2e7adf54d4bb7b271cbff72c5f3`.
- Windows SDK SignTool `/pa` verification passed for both packages, with no
  warnings/errors. These short-lived local test signatures are not timestamped.

| Signed package | SHA-256 |
| --- | --- |
| `CopperScreen-1.1.9.0-win-x64.msix` | `2f22c530278c06ad00c85806c7f2eea23d532207507e2c22f7ab579829b57e34` |
| `CopperScreen-1.1.10.0-win-x64.msix` | `bef03946ebd71bec3f0dc0ce0d433eca803f3c5643a7141f592693b992a59638` |

Both are diagnostic builds, not Store candidates. All 266 payload hashes and
recorded source input hashes match. Production assembly inspection confirms the
installed diagnostic probe is absent. Both packages include only the already
qualified development guest pair: CopperStart `diskboot-2026-10-16-dev.8` and
CopperOS storage seed `0.2.0-dev.1`. Their original artifact hashes remain intact;
no guest binary was rebuilt for this continuation.

## Observed behavior

| Installed phase | Version | Result |
| --- | --- | --- |
| Seed/create | `1.1.9.0` | PASS; native DOS file/directory mutations and settings/data readback, 21,360 fields. |
| New-process readback | `1.1.9.0` | PASS; same workspace, pair and complete image, 8,960 fields. |
| Package-update readback | `1.1.10.0` | PASS; changed WindowsApps installation directory, same user-data root and complete image, 8,960 fields. |
| Uninstall/reinstall | `1.1.10.0` | PASS; package-private profiles/settings and workspace absent, external synthetic original retained. |

All four activations opened a desktop window with the expected package identity
and a non-elevated token. Windows reported installed packages, not development
registrations. The pipeline removed its diagnostic registration after testing.

The three native boots retain the exact workspace image SHA-256
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
Native DOS verified `S:User-Settings` (`volume=37` plus LF) and
`S:Persistent-Data` (`CopperOS data survives restart` plus LF). Independent
readback verifies all original files plus these two files, no scratch/temporary
directory, and exact bitmap ownership: 934 allocated blocks and 824 free,
excluding the two reserved boot blocks. The independent image reader is static
verification; the separate installed native boots establish execution coverage.

Personal profile, last-machine and recent-session files remain byte-identical
through restart and update. Checks include a non-ASCII profile name, controller,
RTC/media choices, volume 37, mute and recent media history. Bundled presets remain
unchanged. These host fixtures and native guest fixtures have separate checks.

## Storage correction discovered by installation

The first signed pair, `1.1.7.0` → `1.1.8.0` under a different test identity,
passed native restart/update persistence but **failed** clean reinstall: its data
remained under the shared portable AppData root. That overall FAILED record is
retained; it is not relabeled as acceptance. Its synthetic leftover data was moved
into the local evidence directory after verifying the exact diagnostic path,
image hash, absence of links and removed package registration.

`CopperScreenUserData` now uses the current Windows package family to select
`%LOCALAPPDATA%/Packages/<family>/LocalState/CopperScreen/` explicitly. Portable
builds retain `%LOCALAPPDATA%/CopperScreen/`. The probe reports the family and root,
and the pipeline rejects a shared portable path. Same-family updates preserve the
installed root; uninstall/reset can remove it. Export valuable guest work before
uninstalling. Existing portable data is preserved separately; no automatic
cross-installation migration was added. See [the storage contract](PROFILE_STORAGE.md).

## Other verification and remaining gates

The production Release solution build passed with zero warnings/errors. Host
tests passed: 241, with 12 optional native cases skipped in that command. Those
skips are unavailable coverage; the three installed native boots above provide
their own passing evidence. All 22 MSIX tooling controls and script parsing passed.

Local output is retained under
`artifacts/copperos-msix-signed-2026-10-17-725100b56ba14c7fb68f8a66187ecd07/`;
`Fixed/` contains the accepted candidate and installed reports. Media, package
binaries and the public certificate remain ignored artifacts. The short-lived
test certificate remains provisioned for repeat local tests; its private key
exists only in the current user's certificate store. To retire it, remove only
the named thumbprint from machine `TrustedPeople` with elevation and from the
original user's `My` store. Microsoft documents the [test signing trust boundary](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).

WACK was not run. Partner Center identity, Store acquisition/update, clean-machine
and separate-account acceptance, release asset rights/distribution, broader
CopperOS ABI/resource/application workflows and bundled onboarding remain open.
This run is neither Store certification nor shipping admission, and contains no
performance measurement.
