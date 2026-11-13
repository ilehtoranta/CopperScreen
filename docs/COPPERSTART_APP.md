# CopperStart standalone ROM in CopperScreen

The start screen separates **Amiga model** (500, 500+, or 1200) from
**Boot method** (free Minimal disk boot, your licensed original Kickstart ROM,
or licensed CopperStart ROM when enabled by the build). For native boot, Kickstart 1.3 is
recommended for A500, 2.04 for A500+, and 3.1 for A1200. Other supported native ROM versions
can be selected on any model; compatibility varies by ROM and software. Floppy disks are optional;
with no boot media, native Kickstart shows its insert-disk screen.

The launcher and Configure Amiga share these boot choices and restore the saved
setup for each model/method pair. Free minimal disk boot supports the standard PAL
A500 (OCS, 68000, 512 KiB Chip plus optional 512 KiB slow RAM), A500+ (ECS, 68000,
1 MiB Chip), and A1200 (Alice/Lisa, 68EC020, 2 MiB Chip). It requires a compatible
880 KiB startup ADF and supplies no DOS or Workbench. Fast RAM, hard drives,
shared folders and other hardware combinations remain unavailable with an
explanation and disabled Start. See [boot contract and model evidence](engine/MINIMAL_DISK_BOOT.md).

CopperStart is disabled by default. Build or publish with
`-p:CopperScreenEnableCopperStart=true` to show its option and embed the qualified
ROM/HDF pair. `scripts/package.ps1` and `scripts/package-msix.ps1` expose this as
`-EnableCopperStart`. Disabled builds omit both payloads and reject CopperStart
launches, including saved profiles and command-line requests. Personal setups and
disks are retained. The flag selects a distribution capability; it does not verify
license ownership. Existing CopperStart qualification commands need the enabled
build property; `scripts/test-copperstart.ps1` supplies it automatically.

Desktop tests use the capability in the referenced CopperScreen assembly. The
ordinary suite runs native ROM, model/settings and disabled-CopperStart controls;
CopperStart-only facts and theories report an explicit unavailable-coverage skip
when the capability is disabled:

```powershell
dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release -p:CopperScreenEnableCopperStart=false --artifacts-path artifacts/desktop-native-tests
```

For CopperStart coverage, stage the qualified ROM/HDF pair with
`scripts/prepare-copperos-bundle.ps1`, then use separate build outputs:

```powershell
dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release -p:CopperScreenEnableCopperStart=true --artifacts-path artifacts/desktop-copperstart-tests
```

The enabled build validates the bundle hashes. Missing or invalid embedded payloads
are failures, not skips. Historical seed and external workspace replays also need
their documented environment variables; their absence remains unavailable coverage.
Mixed native/CopperStart scenarios have separate tests so the native portions run
in both builds. Do not use a default-build skip as evidence of a CopperOS replay.

For CopperOS, select a model, choose **CopperStart ROM** under Boot method,
and press **Start CopperOS**. A500 and A500+ selection is experimental:
the current A1200 ROM/desktop pair is admitted for compatibility testing, not
claimed compatible. Guest boot and application testing for these combinations
is deferred. A500 uses 68000/OCS with 512 KiB Chip + 512 KiB slow RAM;
A500+ uses 68000/ECS with 1 MiB Chip RAM and RTC disabled. No chipset timing
or CPU semantics are changed. Both original A500 and current bundled ROM hashes
remain checked; arbitrary ROMs are not admitted as CopperStart.
Existing custom A500 ROM paths and media remain loadable without replacement.
Each model has its own writable disk: `A500-Desktop`, `A500Plus-Desktop`, or
`A1200-Desktop` under the CopperOS user-data folder.
The qualified **CopperStart A1200 ROM pair (1 MiB)** and **8 MiB CopperOS FFS HDF**
are embedded in the application. The HDF starts the native **CopperOS Desktop**
with Home, Files, Editor and About. No ROM, HDF or ADF picker is needed.
The current [Full filesystem qualification](COPPERSTART_FULL_FILESYSTEMS_V2_2026-11-05.md)
adds native OFS/DOS0, FFS/DOS1, classic SFS and SFS2 boot/read/write/quick-format support through
explicit partitions and RDB. OFS/FFS DD and HD DF0 writes, MFM export and fresh
protected reboot are also qualified. Choose a matching DD/HD drive type and
insert formatting targets before boot. DOS2-DOS5 remain read-only. The 512 KiB reset bank
contains graphics; the matching 512 KiB E00000 extension supplies SFS/SFS2 and the
shared filesystem task. Both banks are admitted as one hash-pinned file.
The previous [single-bank qualification](COPPEROS_CSHARP_GRAPHICS_2026-11-01.md)
retains its own graphics and performance evidence. The Full pair has separate
storage and desktop checks and makes no host-throughput claim.
The launcher creates `CopperOS/A1200-Desktop/CopperOS.hdf` in the user's app-data
directory once, mounts it writable with `DH0:2:32:0:255`, and reuses it across
launches, reselection and updates. The embedded seed is immutable. An existing
workspace with a missing disk reports recovery guidance instead of replacing
it. Saved custom profiles retain their own paths and write settings; advanced
storage controls remain in Configure Amiga.

The earlier app-created Shell proof workspace under `CopperOS/A1200` is retained
with all user edits. Its saved launcher configuration switches to the separate
desktop workspace on reopen or reselection. Explicit custom disks/profiles stay
unchanged. See [desktop image and native A1200 verification](COPPEROS_DESKTOP_A1200.md).

Developer builds stage the two qualified outputs with
`scripts/prepare-copperos-bundle.ps1`; see the README. Their SHA-256 identities
are pinned in `Firmware/CopperStartA1200.json`, `Firmware/CopperOsA1200.json`
and the build gate. CopperStart-enabled publish/MSIX outputs carry the embedded bundle;
diagnostic-only A500 floppy assets remain separate.

For the older A500 CopperStart setup, load **A500 + CopperStart
standalone ROM (experimental)** in Settings > Profiles. In Setup, choose the
qualified ROM and a startup disk or an RDB/explicit FFS hard disk. Without a hard disk,
a missing floppy leaves the ROM waiting for DF0;
insert a disk to continue. Native Kickstart remains the default boot method.

Command-line equivalent:

```powershell
CopperScreen.exe --copperstart --rom C:\Firmware\CopperStart-DiskBoot.rom C:\Disks\demo.adf
```

`--profile copperstart` and `--profile lightweight-a500-copperstart` select the
same bundled preset. Personal profiles and last-session settings preserve the
explicit source and ROM path in the user-data directory. Keep the supplied ROM
in a stable user-owned folder when updating the installed app.

## Qualified artifact and machine

The following contract is the existing A500 qualification. A1200 CopperOS HDF
boot uses a [separate ROM and machine contract](COPPEROS_HDF_A1200_2026-10-22.md).
Selecting Amiga 1200 and CopperOS selects this A1200 configuration. Existing
custom profiles, including A500 floppy workspaces, remain loadable; their
boot selector identifies the custom boot method rather than silently converting
them into an A1200 setup.

The embedded [artifact contract](../CopperScreen/Firmware/CopperStart.json) pins:

| Property | Required value |
| --- | --- |
| Consumer qualification ID | `diskboot-2026-10-19-ffs-td64-frontend.1` (development qualification, not a released producer version) |
| Producer base commit | `b45c2e4e50860f87e81e21d2a6e5c2a08e2631ad`, plus the source snapshot recorded with the proof |
| Size | 524,288 bytes |
| SHA-256 | `d21e5223150a8837739e08acc65af52f483b5789c077a32e071f824bc14b7a86` |
| Machine | PAL, 68000 interpreter, 8371 Agnus, 8362 Denise |
| Memory | 512 KiB Chip + 512 KiB slow at `$C00000`; no Fast RAM or RTG |
| Devices | One DD/HD DF0, read-only by default; writable only for an admitted owned CopperOS workspace. Optional existing image on hard disk unit 0: RDB-discovered DOS1/FFS partitions or one explicit 512-byte FFS partition, including TD64 offsets above 4 GiB; RTC disabled; no host directories |

This development ROM adds native wide seek handling, corrected unsigned
file-position arithmetic and shared MODE_OLDFILE writes with actual file and
device protection retained. Full-ROM checks overwrite and read files above
2 GiB and near the 32-bit FFS file-size limit on a 4.125 GiB volume, flush through
the guest device and verify fresh protected-boot readback. It retains sequential
bitmap traversal, validated bitmap cache spans, aligned internal block copies,
buffered range flushing with partial-write retention, range-limited overwrite
preflight with on-demand bitmap checks,
TD64 partition extents above 4 GiB, RDB discovery, multiple
native FFS mounts, signed boot priorities, DD/HD trackdisk support, the early
packet-reply fix and packed font data. No NSD changes are included.
See the [TD64 app and runner qualification](COPPERSTART_TD64_FRONTENDS_2026-10-19.md)
for the refreshed firmware pin and frontend evidence. The [producer FFS qualification](COPPERSTART_FFS_OVERWRITES_2026-10-19.md)
and its dependency-fix follow-up record the native filesystem work. See [previous DOS large-file qualification](COPPERSTART_DOS_LARGE_FILES_2026-10-19.md)
for wide seek and buffered-write coverage, [prior FFS bitmap qualification](COPPERSTART_FFS_BITMAP_2026-10-18.md)
for allocation coverage, [earlier TD64 qualification](COPPERSTART_TD64_2026-10-18.md)
for the addressing continuation, and [RDB configuration and qualification](COPPERSTART_RDB_2026-10-18.md) for
settings, command-line examples and exact provenance. The earlier
[explicit-partition qualification](COPPERSTART_HARDDISK_2026-10-18.md) is historical. Writable floppies
remain limited to owned CopperOS workspaces; explicitly selected writable HDFs
save directly to their backing file. The earlier
[CopperOS storage continuation](COPPEROS_STORAGE.md) retains its own exact ROM
and installed-package evidence.

FFS guest writes and flushing through the normal app runtime and command-line
runner have their own [restart and fresh-readback checks](COPPERSTART_HARDDISK_WRITES_2026-10-18.md).
The [multiple-partition RDB write continuation](COPPERSTART_RDB_WRITES_2026-10-18.md)
adds both-volume writes, pre-shutdown flush evidence, boot-priority reversal,
app reset/replacement and runner restart/fresh protected readback.
The [installed RDB continuation](COPPERSTART_RDB_INSTALLED_2026-10-19.md) repeats
both-volume writes, boot selections and restart/fresh readbacks through a signed
MSIX desktop app, and fixes duplicate hard-disk entries when opening saved profiles.
The [installed-update qualification](COPPERSTART_RDB_UPDATE_2026-10-19.md) preserves
both RDB images and saved settings across a signed in-place update, then verifies
both boot selections, new writes/flushes and fresh protected readbacks.

The [original read-only boot proof](COPPEROS_BOOT_PROOF_2026-10-16.md) and prior
`dev.1` [producer provenance/Desert Dream record](engine/COPPERSTART_DESERT_DREAM_2026-10-16.json)
remain historical evidence. The selected ROM supports custom boot blocks and
bounded DOS0/DOS1 startup, native console and boot CLI. General operating-system
compatibility requires separate evidence.

The selector displays the qualification ID and requirements. The loader accepts
the exact raw ROM, or a ZIP containing exactly one `.rom`, `.bin` or `.kick` file.
It checks bounded size and SHA-256 before creating the machine. Missing,
truncated, damaged, older and foreign ROMs report which artifact to choose.
There is no automatic native-ROM substitution. The profile's historical
`kickstart.version: "1.3"` describes its compatibility target; firmware identity
comes from the artifact contract, never from the CopperScreen Git revision.

Other hardware combinations remain unavailable for this boot choice. Model
changes retain the explicit CopperStart source and explain unsupported
configurations; they do not silently change firmware. The old Legacy presets
are retained as historical settings, not restored implementations. Use the new
standalone preset to return to the supported configuration.

## Execution and qualification

The host loads bytes through `LightweightA500Machine.LoadKickstart`. Guest 68000
code owns Exec/DOS/trackdisk behavior and reaches emulated chipset and CIA
registers. Host display, PCM delivery, keyboard, mouse, joystick, reset, media
insertion and session replacement use the existing standard app adapter. No
hardware scheduling changes, host OS traps, sibling project references or Legacy
fallback were added. CopperBench/clipboard host services remain unavailable.

Run the reproducible diagnostic pipeline with locally supplied inputs:

```powershell
./scripts/test-copperstart.ps1 `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -LotusDiskPath 'C:\Disks\Lotus2.zip#/Lotus Turbo Challenge 2 (Magnetic Fields + Gremlin).adf' `
  -OutputDirectory artifacts/copperstart-qualification
```

The Lotus entry must match SHA-256
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
The committed `corpus-lotus2.json` input is also hash checked. No ROM/media is
downloaded, rebuilt from producer source, or committed. A clean environment also
needs the repository's pinned CPU package/feed; use `-AdditionalPackageSource`
when required.

Without `-LotusDiskPath`, boot/lifecycle qualification runs and the game replay
is explicitly unavailable. Without the ROM environment variable, ordinary host
tests skip both optional native cases; skips are unavailable coverage.

The original generated disk checks boot-block entry, live mouse/joystick
registers, cold start, reset-button restart, Ctrl + both Amiga reset,
paused-session replacement, disposal/reopen and disk insertion after waiting.
The 14,000-field Lotus test runs the production session against an independent
scalar machine with the same ROM, media, memory and recorded input. PCM is
compared every field; selected checkpoints compare CPU registers, Chip RAM and
all pixels, including the app's doubled presentation rows. Captures and the
binary/input identities are written under the chosen output directory. Review
loading, menu and input-driven play in the captures before recording compatibility.
Successful replay completion alone is not a title qualification or performance result.

## Release boundaries

The A1200 CopperStart/CopperOS pair is now bundled using the pinned development
artifacts. Native Kickstart, Workbench and game media remain user supplied.
This does not publish a package or turn the development qualification into a
producer release.
General DOS compatibility, full-game completion, audio listening/fidelity,
other titles and hardware are unverified by this bounded slice. CopperOS native
storage has its own [contract and qualification](COPPEROS_STORAGE.md). Existing producer evidence for Arte and Desert Dream retains its
own exact configurations and scope; it is not relabeled as standard-app evidence.

Installed MSIX boot and update qualification must use the signed installed
pipeline from [MSIX packaging](MSIX_PACKAGING.md). Source-session qualification
does not establish a Store installation result. The
[2026-10-17 signed diagnostic continuation](COPPEROS_MSIX_VALIDATION_2026-10-17.md)
passes installed boot, process restart and package-update readback for the exact
CopperOS writable-storage pair; this is not Store acquisition or the wider
compatibility matrix. P1-04's
[CopperOS integration contract and artifact inspection](COPPEROS_INTEGRATION.md)
retain the earlier inspection; subsequent boot/storage evidence records the
implemented native handoff and workspace support. P1-04 remains
open, and P1-07 owns the wider frozen compatibility matrix.

## RDB disk backups and reinstall recovery

Stop the Amiga, then open Settings > Storage > Backups and choose
**Export hard-disk backup…**. Save the `.copperhdf` file outside app data.
It contains every complete configured HDF and the machine profile, including
RDB metadata, every partition, boot priorities, explicit partition geometry
and disk write protection. Existing backup files are never replaced.

After reinstall, open the same settings page and choose **Restore hard-disk
backup…**. Integrity checks finish before new working disks are published.
Restore preserves the profile settings and write protection, updates the disk
paths, selects the restored machine and remembers it for the next launch.
It never replaces existing disks or mounts the backup itself. Check the
firmware path, then start the Amiga. Firmware, floppy media and shared-folder
contents are external dependencies and are not included in this backup.
Workspace/ADF export remains separate.

The [app settings backup/restore qualification](COPPERSTART_RDB_APP_BACKUP_2026-10-19.md)
records the installed UI flow. The earlier
[manual backup qualification](COPPERSTART_RDB_BACKUP_RESTORE_2026-10-19.md)
retains its historical scope.

[Writes on restored RDB disks](COPPERSTART_RDB_RESTORED_WRITES_2026-10-19.md)
separately cover new guest writes, flushing, re-export, process restart and fresh
settings restore/readback with either partition selected as SYS:.

[Backup/restore failure handling](COPPERSTART_HDF_BACKUP_FAILURES_2026-10-19.md)
qualifies corrupt/truncated inputs, paused/running session rejection and existing
backup preservation through settings, including unchanged disks and saved/unsaved
settings and a fresh full-ROM readback of both partitions.

[Backup/restore I/O failures and interruption](COPPERSTART_HDF_BACKUP_IO_2026-10-19.md)
covers injected disk-full errors, real unwritable destinations/settings, four
forced terminations and fresh native readback. Restore now saves machine selection
before committing its form and rolls back new copies if that save fails.
Interrupted copies created by current builds have an ownership journal and are
automatically cleaned at the next startup or backup/restore. Active operations,
published objects and untracked older files are preserved. The
[automatic cleanup qualification](COPPERSTART_HDF_BACKUP_CLEANUP_2026-10-20.md)
covers four forced interruptions, long installed-app paths, preserved originals
and fresh full-ROM readback of both partitions.
