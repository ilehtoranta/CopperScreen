# MUI Calc with the CopperOS development library

MUI Calc 1.25 is now the thirteenth utility in the local collection. The complete
original package and CopperOS's real 68000 MUI library were installed in two
private hardfiles. The explicit experimental classic profile opens under
both ROMs, with native version/open-count regressions passing. The subsequent
[native built-in class implementation and replays](UTILITY_MUI_BUILTINS_2026-10-31.md)
get past the original `List.mui` failure. No calculator window or arithmetic
result is qualified yet. This document preserves the earlier failed-open
and v4 class-creation evidence; the linked report records the current work.

## Input and distribution

[MUI Calc](https://aminet.net/package/util/wb/MUI_Calc125) is Robert Poole's small
RPN calculator for MUI 2.0. Its documentation describes listview, paging controls
and menus without additional custom-class packages. `MUI_Calc.doc` sections 7
and 10 describe giftware and permit redistribution with all files included.
Keep all seven original files, including documentation, icons and the ARexx
example. Donations are optional. Original MUI is a separate product; it was
not downloaded or included in these images.

The [manifest](../scripts/qualification/amiga-utilities-v1.json) pins both the
25,119-byte archive and its 569-byte Aminet readme. Archive SHA-256:
`ca41cecb89027fa2a3fd199df2ff200a98819646d2c32876490992b01c4d35fd`.

```powershell
./scripts/get-utility-test-media.ps1 -Id mui-calc-125
./scripts/get-utility-test-media.ps1 -Id mui-calc-125 -VerifyOnly
```

## CopperOS library and image preparation

The build used CopperOS commit
`4b1fb23e00f0b06294ae9a682bbd3227bd854459`, with no MUI source changes. From
the sibling CopperOS repository:

```powershell
dotnet run --project tools/MuiMaster.LibraryBuilder -c Release -p:CopperOSUseLocalCopperSharp=true -- 68000 ../CopperScreen/artifacts/utility-mui-2026-10-31/copperos-mui-68000-v1
```

Build exit code was zero. The real relocatable HUNK is 389,844 bytes with 4,919
relocations, SHA-256
`555e8dba7b086b11b75c42e443b818e84c733f21d9b92ca0937659a0bb7a259d`.
Its Resident and library header retain `copperos-muimaster.library`, version 0,
revision 1. CopperOS's builder explicitly describes this as a development
artifact and does not claim full public MUI compatibility.

[prepare_mui_calc.py](../scripts/probes/UtilityStartup/prepare_mui_calc.py)
requires `lhafile` and `amitools`, a hash-pinned local OS prerequisite image and
the matching library build receipt. It retains all seven original package
files in `MUICalc/`, installs the library under its development filename and
the `Libs/muimaster.library` filename alias, and replaces the startup sequence.
Both library copies are byte-identical: the alias changes no Resident identity,
version or application bytes. The recipe reads back every installed file and
compares the complete before/after filesystem inventories. It refuses to
overwrite earlier outputs and writes an adjacent JSON receipt.

Example from the CopperScreen root with the local Python tool dependencies:

```powershell
python scripts/probes/UtilityStartup/prepare_mui_calc.py --base artifacts/utility-sequential-2026-10-31/final-dpaint-ffs-v189.hdf --base-sha256 d96e6e0371f21ebdc9b9e0155205092811cf9cc50c01fc8e6f7d3f4075a99048 --archive media/utilities/originals/MUI_Calc125.lha --library-root artifacts/utility-mui-2026-10-31/copperos-mui-68000-v1 --output artifacts/utility-mui-2026-10-31/mui-calc-copperos-ffs-v1.hdf
```

The native control used `--native-reference`, the OFS base
`artifacts/utility-sequential-2026-10-31/final-dpaint-ofs-v189.hdf`, SHA-256
`3f833093db1f92a2bc869432ab657289daa3b747a58921418e6819ab04e736a0`, and
output `mui-calc-copperos-ofs-v1.hdf` in the same MUI artifact directory.
The native flag adds `ENV:` and `T:` assignments to RAM. Both images retain
the existing private OS prerequisites and qualified v174 `CopperGui` provider.
This recipe does not acquire ROMs or proprietary OS files; those inputs must
already be available locally. All media and build outputs remain Git-ignored.

## Initial A/B result (development identity, v1)

Both runs used PAL OCS, MC68000, 512 KiB Chip, 512 KiB slow and 2 MiB Fast RAM,
read-only hardfiles, 10,000 fields, no scripted input, and extended boot captures
every 1,000 fields. Each runner exited zero; final images visibly show
`Failed to open muimaster.library.` Neither run showed a calculator. Runner
completion alone is not an application pass.

| Side | ROM SHA-256 | Hardfile SHA-256 |
| --- | --- | --- |
| CopperStart standalone A500 v140 | `adaaece2e195e4be9524cf5413630bebcf099c1b2cb99dbd17e19127251907a3` | `abb2eec6851b7a62964ff06f10ab05656651f0a407e2d864eefc5fd0d77fc311` |
| Native Kickstart 3.1 A500 | `8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee` | `8ad2ec41e64b0ed4c85bf9fd12a13a5abb86cef7b7f33f31cf1b97d3397d00dc` |

Artifacts are under `artifacts/utility-mui-2026-10-31/`:
`mui-calc-copperstart-v1/`, `mui-calc-kick31-v1/`, their adjacent logs, the
two hardfiles and their JSON receipts. The native reference uses the same
CopperOS library; it is not an original-MUI golden run. This expanded-memory
diagnostic does not change the app's qualified firmware or machine profile.

The external startup observer in `mui-calc-open-trace-v1/trace.json` captured
the unmodified application's call:

```text
OpenLibrary("muimaster.library", 4)
entry cycle 309616066; return cycle 413841084
return D0=0 at field 2912; call PC=0x00300094
```

CopperOS's `MuiNativeLibrary.cs` initializes header version 0 and its Open
entry accepts only a requested version no greater than that header version.
The requested version 4 is therefore already incompatible with the current
development artifact. Resident naming may also require explicit compatibility
packaging; this trace does not isolate all loader checks. No version spoofing,
binary patch or public-compatibility claim was introduced.

This initial result motivated the explicit compatibility profile below. Stack
list, paging controls, menus, quit and the `5 ENTER 6 ENTER +` arithmetic check
remain unavailable, rather than passed or skipped successes.

## Compatibility fixes and continued testing (classic profile, v4)

Three CopperOS fixes address distinct loading and native-OS problems:

1. `CopperOSMuiClassicCompatibility=true` explicitly builds `muimaster.library`
   4.1, marked CopperOS experimental, with matching Resident and initialized
   header identities. The default development build remains separately named
   0.1. No application or output-library bytes are patched.
2. The library Open vector now consumes only A6. The native Kickstart trace
   showed D0 containing the library base, which our old Open implementation
   incorrectly compared with the version. [Exec owns minimum-version
   admission](https://developer.amigaos3.net/autodocs/exec.library/OpenLibrary.html);
   D0 is not treated as another version check inside our Open vector.
3. The private semaphore idle check recognizes the complete native state
   observed under Kickstart 3.1: type `NT_SIGNALSEM` (15), queue count -1,
   self-linked empty wait queue, null owner and zero nesting. It continues to
   accept CopperStart's existing idle form and pre-initialization zero storage.
   Mixed states, busy queues, nesting and owned semaphores remain rejected.

The third fix was needed because native `MUI_NewObjectA` initially returned
before provider acquisition. After it, the native trace opens utility, DOS,
graphics, Intuition and keymap providers, then reaches class lookup, matching
the CopperStart path.

Build from CopperOS, preserving a distinct output directory:

```powershell
dotnet run --project tools/MuiMaster.LibraryBuilder -c Release -p:CopperOSUseLocalCopperSharp=true -p:CopperOSMuiClassicCompatibility=true -- 68000 ../CopperScreen/artifacts/utility-mui-2026-10-31/copperos-mui-68000-classic-v4
```

The v4 HUNK is 389,912 bytes with 4,920 relocations, SHA-256
`e29109205cd3b64a524bd579a0a765ba22218649bff87a5a8dc373cd012c69a5`.
The builder passed its freestanding/native-only checks. Use the preparation
recipe above with `--library-name muimaster.library`, this build directory and
new output filenames. No development-name alias is needed for this profile.

| Final private hardfile | SHA-256 |
| --- | --- |
| `mui-calc-copperos-ffs-v4.hdf` | `5d1b8f92247935f5ef5ec73ad9bc2998fc4d5bb8e05c508d5472ec5518cd31fd` |
| `mui-calc-copperos-ofs-v4.hdf` | `717c28dfef275c2e2634263734c20fd6f481804e783f71273c4e76bd9e507cc5` |

The ROMs, machine, read-only mount modes and 10,000-field replay settings are
the same as v1 above. Outputs are `mui-calc-copperstart-v4/` and
`mui-calc-kick31-v4/`. Both show the application's failed-creation requester.
The replay commands, with local protected ROM/media inputs, were:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --rom ../CopperStart/artifacts/utility-private-snapshots-classic-rom-v140/CopperStart-DiskBoot.rom --hdf-ffs artifacts/utility-mui-2026-10-31/mui-calc-copperos-ffs-v4.hdf DH0:1:32:0:1023 --frames 10000 --fast-ram-kib 2048 --boot-probe artifacts/utility-mui-2026-10-31/mui-calc-copperstart-v4 --boot-probe-extended --boot-probe-interval 1000
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --rom C:/Data/ROM/kickstart-3.1-a500.rom --hdf-ro artifacts/utility-mui-2026-10-31/mui-calc-copperos-ofs-v4.hdf --frames 10000 --fast-ram-kib 2048 --boot-probe artifacts/utility-mui-2026-10-31/mui-calc-kick31-v4 --boot-probe-extended --boot-probe-interval 1000
```

The external first-failure traces are `mui-calc-first-class-copperstart-v11/`
and `mui-calc-first-class-kick31-v11/`:

| Observation | CopperStart | Kickstart 3.1 |
| --- | --- | --- |
| `OpenLibrary("muimaster.library", 4)` | Non-null, field 2906 | Non-null, field 321 |
| First class request | `List.mui` | `List.mui` |
| External `mui/List.mui` lookup | NULL | NULL |
| `MUI_NewObjectA` return | NULL, field 2942 | NULL, field 333 |

The library's native registry starts empty. `MuiClassServiceCore.GetClass`
adopts an existing registered built-in class or loads `mui/<classid>`. The
packaged image has no bootstrap that installs its built-in headless MUI class
implementations as native public BOOPSI classes. No `List.mui` class package
is present. This is the next implementation gap; changing versions or adding
more filename aliases does not supply a class dispatcher. No stub class or
fabricated successful object was introduced.

## Validation of the fixes

The full CopperOS MUI host suite passed **3,856 tests, zero skipped**, under
both default and classic profiles. Four new semaphore cases check the native
idle state and reject queue counts 0, 1 and -2 for that native form. The
existing malformed-gate rejection tests remain intact. The packager suite
also passed **22 tests, zero skipped**, under each profile, checking relocated
Resident identity and the complete vector table.

The standalone packaged lifecycle harness was attempted but did not execute:
its independent Exec compilation requires an unrooted
`copperstart.exec.find-task-by-pid` export. An earlier attempt also used the
wrong SDK fallback path; the explicit local SDK corrected that build setup.
Neither attempt counts as a passing lifecycle run. Its client now includes
the D0 regression and its fixture checks the selected profile's identity.

Instead, [prepare_mui_library_probe.py](../scripts/probes/UtilityStartup/prepare_mui_library_probe.py)
installs a small original native command into a copy of each v4 image. It calls
real Exec and library vectors and publishes copied results in a reset-lived
`MuiLibraryProbe` Exec resource. All seven original MUI Calc files and library
bytes are retained. [inspect_mui_library_probe.py](../scripts/probes/UtilityStartup/inspect_mui_library_probe.py)
validates that resource in the captured Fast RAM, including completion magic,
non-null base, version, exact results and counts; it does not infer success from
the process exit code.

Final probe images are `mui-library-probe-ffs-v12.hdf` and
`mui-library-probe-ofs-v12.hdf`, with adjacent preparation receipts. Runs are
`mui-library-lifecycle-copperstart-v12/` (4,000 fields) and
`mui-library-lifecycle-kick31-v12/` (1,000 fields), in the same artifact root.
Both pass these observations:

```text
OpenLibrary version 4: non-null, header version 4, count 1
OpenLibrary version 5: NULL, count remains 1
Second OpenLibrary version 4: same base, count 2
Direct library Open with D0=base: same base, count 3
Three CloseLibrary calls: counts 2, 1, 0
```

This proves the bounded native opening/version/count regression under both
ROMs; it does not qualify expunge, class construction or the entire MUI API.
Run the capture checker from CopperScreen:

```powershell
python scripts/probes/UtilityStartup/inspect_mui_library_probe.py artifacts/utility-mui-2026-10-31/mui-library-lifecycle-copperstart-v12/frame-004000.fastram artifacts/utility-mui-2026-10-31/mui-library-lifecycle-kick31-v12/frame-001000.fastram
```

The [machine-readable receipt](UTILITY_MUI_COMPATIBILITY_2026-10-31.json)
pins the final library, private images, captures, traces and changed source.
These are compatibility diagnostics, not performance measurements, full MUI
compatibility or a new app firmware qualification.
