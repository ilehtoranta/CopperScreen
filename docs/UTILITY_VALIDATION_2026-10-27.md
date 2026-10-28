# Initial Amiga utility exploration — 2026-10-27

Follow-up: the ROM size blocker was resolved by the
[2026-10-28 rebuild and paired utility comparison](UTILITY_AB_2026-10-28.md).
The observations below retain their original date and inputs.

The four collected ADFs reach their initial application/menu screens under
native Kickstart 1.3 on the existing PAL A500 Lightweight runner. Selected
keyboard and audio actions provide additional bounded coverage. CopperStart
comparisons are **UNAVAILABLE**: the pinned A500 ROM was not found in the checked
local inputs, and rebuilding the current producer source failed its ROM-size
check. The app firmware pin and emulated hardware configuration were not changed.

This is diagnostic runner coverage. It does not establish installed desktop-app
behaviour, full utility compatibility, real-Amiga equivalence or host throughput.
The [collection catalogue](UTILITY_TEST_CORPUS.md) retains the other utilities,
licences and prerequisites.

## Inputs and build

- Production build: `dotnet build CopperScreen.slnx -c Release` passed with zero
  warnings and zero errors. No production engine or CPU changes were made.
- Machine: PAL, MC68000 interpreter with the existing conservative batching,
  OCS 8371 Agnus / 8362 Denise, 512 KiB Chip + 512 KiB slow RAM, no Fast RAM,
  RTC or host directories, one protected DD DF0.
- Native ROM: 262,144-byte Kickstart 1.3, SHA-256
  `ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53`.
- Utility disk identities: the unchanged
  [collection manifest](../scripts/qualification/amiga-utilities-v1.json).
- Consumer source: Git HEAD `c82d37a3561550d11751966e750f4e60756e3b93`, with
  pre-existing working changes. The local receipt hashes the actual runner,
  engine, CPU and disk DLLs; HEAD alone is not the build identity.

## Observations and limits

| Utility | Observed native result | Remaining scope |
| --- | --- | --- |
| ProTracker 2.3F | Editor appears; Space and Z/X/C enter notes, playback advances pattern rows, Space stops playback. | No sample/module was loaded. This is editor/sequencer input coverage, not an audible tracker playback or save/readback qualification. |
| X-Copy 5.21 | Original disk's boot menu appears; F1 launches the DOSCOPY application screen. | No disk read/check sweep, copy, format or Cyclone external-hardware operation was exercised. |
| Amiga Test Kit 1.21 | Main menu appears; F5 opens Audio. Channel toggles produce stereo, then left-only output, all-off silence and restored stereo. | Static labels disappear during the toggle sequence while ON/OFF fields continue updating: unresolved rendering observation. No complete memory/CIA/video/floppy/peripheral suite was run. The menu's ROM identification text is not treated as a ROM-verification oracle. |
| SysInfo 4.4 | Main screen enumerates Kickstart/libraries, 68000, OCS/STD Denise and installed memory. | The exploratory mouse/click sequence ends in a black display. It does not establish that the intended Speed control was selected, nor a successful benchmark or quit. Cause remains unresolved. |

All four initial 3,000-field boot observations and the four 4,200-field v1 action
runs completed without an engine unsupported-feature stop. A completed runner
process is not an application-operation pass. SysInfo's extended 7,200-field
mouse sequence also ends black while execution continues; keep that observation
separate from its successful initial-screen check.

The final v2 batch completed **four 7,200-field runs (28,800 fields)**, with no
engine unsupported-feature stops and unchanged before/after hashes for all four
protected disks. These are execution checks; individual operations retain the
limits in the table above.

Test Kit v2's captured PCM supports the channel-control observation:

| Captured field | Requested channel state | Left RMS | Right RMS |
| ---: | --- | ---: | ---: |
| 3,100 | All enabled | 9420.72 | 2807.12 |
| 3,800 | Only channel 3/L enabled | 3708.11 | 0 |
| 4,000 | All disabled | 0 | 0 |
| 4,200 | All restored with F7 | 2155.27 | 2357.44 |

RMS uses the captured signed 16-bit sample values, not normalised amplitude.
Different fields contain different music; the values are observations, not new
audio goldens. `native-actions-v2/audio-observations.json` retains the sample
counts and register context. No real-hardware or independent audio reference
was compared.

The initial SysInfo mouse script used two large deltas; v2 subdivides the same
total movement. Neither script gives an established successful Speed result.
Retain both as exploratory inputs rather than assigning a title-specific engine
fix or rewriting the expected framebuffer.

## Missing CopperStart ROM and rebuild

The consumer contract still requires qualification
`diskboot-2026-10-19-ffs-td64-frontend.1`, a 524,288-byte ROM with SHA-256
`d21e5223150a8837739e08acc65af52f483b5789c077a32e071f824bc14b7a86`.
No matching raw ROM was found in CopperScreen's local artifacts or the checked
ROM/firmware inputs. The producer's documented
`artifacts/ffs-dependency-validation-20261019/native-td64/CopperStart-DiskBoot.rom`
was also absent. The user authorised a rebuild after reporting deletion of old
artifacts.

The current CopperStart producer checkout is at
`e01ac58d0446544eeadb3d829d2d1c859ef5babd`, with substantial working changes.
Its existing `build/Build-DiskBootRom.ps1 -FileSystemProfile Classic` was invoked
with a fresh local artifact directory. Compiler/SystemRom managed builds passed;
the native linker rejected the result:

```text
C68K0007:: Linked code requires 535648 bytes but ROM has only 524260 code bytes.
```

The code exceeds the available area by **11,388 bytes**, before the graphics
composition step. No runnable ROM or successful native compatibility report was
produced. The local failure receipt retains the compiler/producer input assembly
hashes, source provenance and log. No producer source changes, larger-ROM
substitution, firmware-pin update or package publication were made for this run.

Restoring the original qualified producer inputs/ROM, or separately producing
and validating a replacement standalone A500 ROM that fits its contract, remains
necessary for a CopperStart comparison. Current source cannot be assumed to
reproduce the missing historical binary.

## Reproduction and local evidence

The reusable diagnostic script is
[test-utility-boot.ps1](../scripts/test-utility-boot.ps1). It checks the native
reference identity, verifies the four utility inputs, keeps DF0 protected,
records before/after media hashes and writes captures plus DLL/input hashes.
Its v2 scenarios run for 7,200 fields with snapshots every 100 fields:

```powershell
./scripts/test-utility-boot.ps1 `
  -NativeRomPath C:/Data/ROM/Kickstart_13.rom `
  -OutputDirectory artifacts/utility-ab/new-native-run
```

Supply `-CopperStartRomPath` only with the exact qualified ROM. An explicit
`-DevelopmentCopperStartRomPath` instead requires a matching standalone A500
Classic build manifest and labels results as unqualified development evidence;
it does not authorise app firmware adoption. The failed rebuild here cannot be
used with either option. Omitting both records unavailable paired coverage.

Input scripts are under `CopperMod.Amiga.Lightweight.Runner/Workloads/`:

- `utilities-protracker-editor-v1.json`: edit, enter Z/X/C notes, play/stop.
- `utilities-xcopy-launch-v1.json`: boot-menu F1 only.
- `utilities-testkit-audio-v1.json`: F5 audio menu.
- `utilities-testkit-audio-v2.json`: audio menu, disable channels 0/1/2/3 in
  sequence, then F7 to restore all channels.
- `utilities-sysinfo-speed-v1.json` and `-v2.json`: unqualified mouse/click
  explorations; the filenames describe their intended action.

Ignored local evidence is under `artifacts/utility-ab/2026-10-27/`:

- `native-*-boot/` and corresponding logs: initial 3,000-field observations.
- `native-actions-v1/`: 4,200-field action runs and `qualification.json`.
- `native-sysinfo-actions-v2/` and `native-sysinfo-actions-v2-extended/`: mouse
  subdivision control and extended black-display observation.
- `native-actions-v2/`: current 7,200-field scenarios and machine-readable
  execution/DLL/ROM/disk/input identities.
- `copperstart-rebuild/` and `copperstart-rebuild.log`: failed producer build,
  staged input assemblies, source provenance and `rebuild-failure.json`.

Each extended snapshot includes full framebuffer BMP, machine JSON, Chip/slow
RAM and one field of signed 16-bit little-endian stereo PCM. Sparse PCM snapshots
are not a continuous audio recording. Captures, ROMs, media and producer build
outputs remain outside Git. Existing regression/performance goldens are unchanged.
