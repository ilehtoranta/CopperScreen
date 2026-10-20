# P1-07 signed MSIX launcher and persistence validation — 2026-10-17

**PASS for this signed local diagnostic continuation. P1-07 remains in progress.**
The launcher layout fix survives real MSIX install/restart/update, alongside
CopperOS workspace, profile and settings persistence. The complete media,
backup/recovery and clean-reinstall restore checks also pass. Execution stays on
Lightweight with the frozen standalone CopperStart/CopperOS pair; Legacy remains
unavailable. This run makes no performance or Store-certification claim.

The [hash-bound record](RELEASE_MSIX_VALIDATION_2026-10-17.json) identifies all
packages, native replays, layout measurements, captures and verification outputs.
Earlier [P1-07 source qualification](RELEASE_QUALIFICATION_2026-10-17.md),
[P1-06 installed evidence](MEDIA_MSIX_VALIDATION_2026-10-17.md) and
[P1-06 WACK](MEDIA_MSIX_WACK_VALIDATION_2026-10-17.md) remain unchanged.

## Exact packages and protocol

Local diagnostic identity: `CopperScreen.Test.37654a7e355b4dafab059fac783ecb6a`.
Both versions contain the launcher fix and the diagnostic-only layout observer.

| Package | SHA-256 |
| --- | --- |
| Baseline `1.1.25.0` | `d97faf52c1c0124cfbd89aebe3b68579cbd13af04a8dc590f9294f8655665a28` |
| Update `1.1.26.0` | `4a930e9cd6724983fc682f87a1d7fb4d77cfd8313a95e4552ab6f88e21227a01` |

The existing `CN=CopperScreen Installed Tests` certificate signed both packages.
Trust was already provisioned; this run neither changes trust nor exports a key.
SignTool `/pa /v` passes for both, with no warnings/errors. Every one of the 266
recorded payload files in each signed archive matches its recorded hash. Package
source inputs match the frozen copies and locked dependency identities.

[`test-msix-installed.ps1`](../scripts/test-msix-installed.ps1)
`-MediaPersistence -LauncherLayout` implements
`copperscreen-installed-msix-v4`. It uses real signed Install mode, a fresh identity,
COM package activation and package-family LocalState. Six non-elevated activations
must each open the normal desktop window, report success and exit with code zero.
The controller checks a separate launcher window in each activation and requires
complete layout evidence. The dedicated installed CI workflow enables this mode
and retains its captures.

## Installed observations

| Phase | Version | Required result |
| --- | --- | --- |
| Seed | 1.1.25.0 | Native file/directory/settings mutations saved; protected-media and backup/recovery controls pass. |
| Baseline restart | 1.1.25.0 | Same native image/workspace, profile and settings read back. |
| Update | 1.1.26.0 | Changed WindowsApps directory, unchanged LocalState/workspace identity and saved hashes. |
| Denied checkpoint | 1.1.26.0 | Read-only primary refuses save; dirty state remains; live ADF and versioned backup export/import succeed. |
| Recovery restart | 1.1.26.0 | Imported recovery workspace boots and reads native files/settings. |
| Uninstall/reinstall | 1.1.26.0 | Previous private state is removed; external originals/backups survive; imported backup boots and reads back. |

The complete saved native image is
`e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`
in all six phases. Guest files/settings survive restart/update; all three host
profile/settings files remain byte-identical through restart/update. The host
fixture retains its non-ASCII profile name, volume 37, mute, controller assignment,
RTC/media choices and recent-media history. Reported loaded app/engine hashes
match the appropriate signed package payload. Synthetic imported-media controls
and the native CopperOS replays remain separate observations.

All **18 installed layout cases** pass: actual client sizes 800×600, 1280×720 and
960×640 in each activation, at the current monitor's measured **100% scale**.
The scroll viewport stays inside the client; horizontal overflow is absent;
“Save launch profile” has positive dimensions and scrolls completely into view.
PNG renders and numeric bounds are retained per case. Baseline/update captures
were visually inspected. This fixture uses the empty-workspace CopperOS view,
where Start and Save are disabled until a workspace is selected. Layout settings
are isolated from the persistence fixture; no click or physical DPI transition
is inferred from these bounds.

Final cleanup removes this run's registration and package-private test directory.
Protected originals and exported backups remain intact outside package storage.
No existing customer profile, package identity or workspace is replaced.

## Verification and remaining gates

- Production Release solution build: zero warnings/errors; installed diagnostic
  types absent from the normal assembly.
- Guest-enabled desktop suite: **22 passed, 0 skipped**. Its separate invocation
  without guest inputs had 19 passed and three unavailable; both reports remain.
- Host suite: **265 passed, 12 optional native checks unavailable**. The six
  installed native boots above provide separate, explicitly identified coverage.
- Packaging-tool controls: **64 passed**, including rejection of missing,
  non-finite, clipped or incomplete layout evidence. Script parsing passes.

Fresh WACK for these exact package hashes remains open; earlier P1-06 WACK verdicts
apply to their recorded packages. Store identity/acquisition/update, admitted
shipping assets, clean-machine/separate-account checks, manual file pickers and
recovery confirmations, physical HID/audio/sleep/mixed-DPI behavior and installed
60-minute sessions remain release work. Minimum/reference hardware and numeric
budgets remain open by owner instruction. See the
[qualification protocol](RELEASE_QUALIFICATION.md) and
[productization roadmap](PRODUCTIZATION_ROADMAP.md).

No ROM, system image, signing key, package or generated workspace is committed or
published. Local build/native evidence remains under the ignored artifacts run
identified in the JSON record.
