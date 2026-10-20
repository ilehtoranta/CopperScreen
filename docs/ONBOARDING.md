# First-run onboarding and everyday launches

P1-05 implements the desktop workflows on Lightweight. CopperStart and CopperOS
remain explicitly labelled experimental previews. Legacy and host OS shims are
unavailable. WHDLoad is outside this work.

The normal build includes the emulator and immutable machine presets, **not**
Kickstart, Workbench, games or the development CopperStart/CopperOS boot pair.
The welcome screen explains this before any file picker. The installed diagnostic
build can use its explicitly supplied, hash-qualified pair offline. Production
does not discover diagnostic files or enable distribution of proof media.
Commercial admission of a bundled pair remains a release requirement under
[the integration contract](COPPEROS_INTEGRATION.md).

## Walkthroughs available in the app

### User-owned ROM and disks

1. Choose **Use my Kickstart ROM and disks** and the Amiga model.
2. **Choose ROM** validates the native version against that machine. Choose a
   supported startup disk or archive, or drop one onto the disk controls.
3. **Start Amiga** uses the existing Lightweight session. Missing files,
   incompatible ROMs and unreadable images produce startup guidance and keep
   the launcher available for correction.
4. The last successful launch is remembered. **Save launch profile** keeps a
   separate named setup; select a saved profile and **Load profile** to restore it.
   **Configure Amiga** provides profile naming and advanced configuration.

Imported media open read-only with the welcome defaults. Saved profiles retain
their explicit write-protection choices; this work does not broaden format write
support. Missing saved paths remain visible for repair rather than being dropped.

### CopperStart disk boot preview

1. Choose **CopperStart disk boot (experimental)**. This resets the machine to
   the qualified PAL OCS 68000, 512 KiB Chip + 512 KiB slow RAM, one DF0, no RTC,
   Fast RAM, hard files or host directories.
2. Select the qualified standalone ROM, then compatible user-owned disk media.
   Native Kickstart and older CopperStart ROMs are rejected with recovery guidance.
3. **Start CopperStart** launches the normal app session. This is the guest ROM;
   no host services or Legacy fallback are added.

### CopperOS workspace preview

1. Choose **CopperOS workspace (experimental)**, then the qualified CopperStart
   ROM. In diagnostic packages the qualified pair is already available.
2. Expand **Create a new workspace**, name it, and choose **Create workspace**.
   Select the qualified CopperOS system ADF when prompted. The app checks the
   exact pair and creates its private writable copy; the original is unchanged.
   Existing names never reset or replace user files. Invalid names are rejected
   before the system-image picker.
3. Choose **Start CopperOS**. Wait for the native Shell prompt, type `Demo`, and
   press Enter. `Echo hello` prints a message. The status bar and **More** contain
   these instructions, so the walkthrough needs no repository documentation.
4. Later launches restore the last workspace. To change it, select a workspace
   and **Resume workspace**, then start. The ROM is checked and the existing image
   must be compatible, readable, valid and exclusively available. Failed resume
   preserves the image and offers repair/another-workspace guidance.

Workspace setup assigns the mouse and frees keyboard text input from a previous
game's joystick mappings. Advanced input choices can be saved in a launch profile.

The system disk cannot be swapped through the launcher's ordinary disk controls.
Use a separate workflow/profile for imported disks. Damaged or missing workspaces
are not automatically replaced by a new seed.

## Everyday controls and storage

The app opens with side controls when the monitor has enough horizontal space.
Windows at least 920 logical pixels wide place **Pause/Resume**, **Restart**, **Stop** and disk controls in a
right-side panel, with **Fullscreen**, **Sound**, **Inputs**, **Settings** and
**More** at the bottom. Narrow windows use compact horizontal controls
above the display. The launcher uses the same layout, and the Amiga picture
retains its aspect ratio within the available area. Fullscreen hides controls initially; **F11** toggles
them as an overlay without moving or resizing the picture.
In windowed mode, **F11** also hides or shows controls, freeing their space for
the display. This choice is retained across fullscreen transitions. **F1** shows
the controls again when opening shortcut help.

Restart saves CopperOS storage and restarts guest programs, preserving the prior
pause state. Stop saves storage, releases the runtime and its workspace lease,
and returns to the launcher. Both use the existing execution owner; there is no
second emulator or filesystem service. A failed checkpoint or unsaved ordinary
floppy prevents the transition and keeps the session available for retry/export.

Click the display for guest input. **F10** releases mouse capture; **More → Release
input** releases captured keys and mouse. **More → Export workspace** saves a
versioned `.copperos` backup elsewhere. Saved workspaces also offer previous-checkpoint
export and confirmed recovery; import creates a new workspace. See the
[media protection and recovery guide](MEDIA_PERSISTENCE.md). Export before uninstalling:
Windows removes package-private workspaces, profiles and preferences on uninstall.
Updates retain app data under the established
[workspace](COPPEROS_STORAGE.md) and [profile](PROFILE_STORAGE.md) contracts.

## Verification and remaining release acceptance

The [2026-10-17 verification record](ONBOARDING_VALIDATION_2026-10-17.json)
identifies the local test results, qualified pair, rendered screens and source hashes.

Local validation passed: locked restore; production solution build with zero
warnings/errors; **10 desktop cases**, including both cases using the qualified
pair; **241 host cases**; and **74 disk cases**. Twelve optional native replays in
the host suite were skipped and remain unavailable coverage for this run. Rendered
first-run, CopperOS setup and ready-to-start screens were inspected. No emulator
hardware behavior or timing changed; no engine/performance qualification is claimed.

`CopperScreen.Desktop.Tests/LaunchWorkflowTests` uses the actual styled Avalonia window through the
[headless testing platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).
It covers offline first-run explanations, invalid setup recovery, qualified
defaults, original-media protection, saved missing paths and the production
proof-media gate, including corrupt disk/ROM archives and damaged workspace resume.
The optional qualified-pair lifecycle test creates a real workspace,
opens the standard window, starts, pauses, restarts, stops, resumes, checks the
saved path and releases/reopens the exclusive lease. It is a desktop lifecycle
check, not a native Shell/Demo replay or performance measurement.

Run the host suite with:

```powershell
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release
dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release
```

For qualified desktop lifecycle coverage set `COPPERSCREEN_LAUNCH_ROM` and
`COPPERSCREEN_LAUNCH_SEED` to the pair in the embedded metadata. Without them that
case is skipped and its coverage is unavailable. Optional
`COPPERSCREEN_ONBOARDING_CAPTURE` writes rendered screens for visual review.
Desktop tests run in their own process so their Avalonia application/dispatcher
cannot interfere with the host suite's dispatcher-ordering diagnostics. CI runs
both projects; its qualified-pair case is explicitly unavailable without local assets.

The earlier [native Shell/Demo](COPPEROS_BOOT_PROOF_2026-10-16.md),
[writable-storage](COPPEROS_STORAGE.md) and
[signed installed update](COPPEROS_MSIX_VALIDATION_2026-10-17.md) evidence remains
separate. The earlier [WACK record](MSIX_WACK_VALIDATION_2026-10-17.md) describes
its exact older package hashes; it does not certify this changed UI build.

P1-05's final Store acceptance still requires an admitted shipping pair and an
offline walkthrough of the final signed package by a clean-account tester using
only in-app guidance, including real file pickers, Shell/Demo, keyboard-only
navigation and supported DPI/window sizes. Headless controls do not establish
Windows picker/accessibility behavior or that manual installed walkthrough.
