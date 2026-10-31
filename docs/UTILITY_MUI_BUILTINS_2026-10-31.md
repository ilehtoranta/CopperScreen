# CopperOS native MUI built-in classes — 2026-10-31

This continues the [library opening/version fix](UTILITY_MUI_CALC_2026-10-31.md).
The two reference ROMs remain the qualified standalone A500 CopperStart ROM
and native Kickstart 3.1. MUI requires BOOPSI from OS 2 or later; Kickstart 1.3
is not a MUI reference. These are compatibility diagnostics, not performance
measurements or new application firmware qualification.

## Implementation

CopperOS now lazily registers sixteen real native Intuition BOOPSI classes:
`Notify.mui`, `Family.mui`, `Area.mui`, `Group.mui`, `Rectangle.mui`, `Text.mui`,
`List.mui`, `Listview.mui`, `Window.mui`, `Application.mui`, `Menustrip.mui`,
`Menu.mui`, `Menuitem.mui`, `String.mui`, `Image.mui` and `Cycle.mui`. Their hierarchy and
dispatcher context are recorded in the guest registry. Unknown class names
continue through the external custom-class loader. Idle built-ins retire in
derived-first order only after objects, leases and callbacks are gone.

The production adapter uses real guest Exec allocations, Intuition classes,
DOS vectors and utility hooks. It shares the existing guest-resident base,
control, List and Listview state cores. Native List insertion/clearing and
attribute readback use the same typed packet decoders as the portable cores.
Listview construction initializes its state and adopts its supplied List.
Public ownership and internal construction pins remain separate.

Native integration exposed two additional defects. The public BOOPSI object
pointer follows its twelve-byte private header; class lookup previously decoded
instance bytes as that header. Five host regressions now cover the correct
address, a plausible instance-data decoy and invalid ranges. Compiled guest
methods also exceeded the original command's small CLI stack. The native v20
watch capture stopped at PC `0x22d68c`, SP `0x2197e0`, writing into command code
below its stack lower bound `0x2198ac`. This was guest stack overflow, not a
chipset timing failure. Native entry gates now use Exec StackSwap onto a
temporary 128 KiB stack when less than 32 KiB remains, then restore and free it.

This is bounded class support. The adapter does not yet implement a native
Window presenter or the complete MUI method surface. Native Window Open and
Window pointer queries return zero, even if an Open attribute was requested.
No synthetic successful window, host OS shim or proprietary original MUI
library is supplied.

## Reproduction and evidence

Build the explicit experimental classic-client profile from CopperScreen:

```powershell
dotnet run --project ../CopperOS/tools/MuiMaster.LibraryBuilder -c Release -p:CopperOSUseLocalCopperSharp=true -p:CopperOSMuiClassicCompatibility=true -- 68000 artifacts/utility-mui-2026-10-31/copperos-mui-builtin-68000-v30
```

The resulting development `muimaster.library` is 1,018,472 bytes with 16,851
relocations, SHA-256
`350854a006d8ff5a0ef3d8c5ce91cb9985a7fc4291139cb7cf060c0b6f71c01f`.
The default CopperOS identity remains `copperos-muimaster.library` 0.1; this
profile is experimental 4.1, not an assertion of complete MUI 4 compatibility.

[prepare_mui_calc.py](../scripts/probes/UtilityStartup/prepare_mui_calc.py)
preserves all seven original MUI Calc 1.25 files and installs the receipt-checked
68000 library into hash-pinned private OS prerequisite images.
[prepare_mui_library_probe.py](../scripts/probes/UtilityStartup/prepare_mui_library_probe.py)
with `--builtins --probe-only` adds an original native command without starting
the calculator afterward. Both images are mounted read-only, with PAL OCS,
68000, 512 KiB Chip RAM, 512 KiB slow RAM and 2 MiB Fast RAM.

The [native probe](../scripts/probes/UtilityStartup/mui_builtin_probe.py)
performs two cycles of class acquisition, List construction, insertion,
Intuition GetAttr readback, clearing, Listview adoption, composite disposal and
class release, plus Cycle construction/readback/disposal. It copies results
into a reset-lived Exec resource. The
[capture checker](../scripts/probes/UtilityStartup/inspect_mui_builtin_probe.py)
requires completion, non-null classes/objects, entries 1 then 0 in each cycle,
Cycle active choice 0 with successful native GetAttr, and identical available
public memory after both cycles. An incomplete run or
memory mismatch is a failure, not a pass inferred from process exit status.

The earlier v26 and v28 CopperStart probes completed their methods but lost
484 bytes per cycle. The v28 native Kickstart probe did not complete: its public
binding list contained a cycle after disposal resurrected a freed child link.
Those results are retained as failed evidence. Native child disposal now
handles an already-retired sidecar and an owning Family's active cleanup stage
without attempting a second incompatible state destructor.
The public gateway also refreshes its binding and sidecar immediately after the
native destructor callback, preserving child removals instead of overwriting
the new Next link with the pre-callback snapshot. Two host regressions cover
the changed link and reject changed lease authority.

The narrower v29 MUB2 probe passes under both ROMs. Available public memory
after the two cycles is `[2060992, 2060992]` on Kickstart 3.1 and
`[1107264, 1107264]` on CopperStart. The final captures contain no headless
objects or built-in classes. Its complete replay bounds are 15,000 and 20,000
fields respectively, and the local result receipt is
`artifacts/utility-mui-builtin-probe-v29-result.json`.

## Completed native lifecycle checks

The final v30 MUB3 probe passes under both ROMs. Every cycle constructs a List,
inserts one entry, reads back 1, clears it, reads back 0, adopts it into a
Listview and disposes the composite. It also constructs a Cycle, reads back
active choice 0 and disposes it. All constructors and GetAttr calls succeed.

| Observation | Kickstart 3.1 | CopperStart |
| --- | --- | --- |
| Replay bound | 15,000 fields | 20,000 fields |
| Available public memory after cycles 1 and 2 | 2,061,264 / 2,061,264 | 1,107,024 / 1,107,024 |
| Remaining registry objects / built-ins | 0 / 0 | 0 / 0 |
| MUB3 result | PASS | PASS |

Capture directories are `mui-builtin-probe-kick31-v30/` and
`mui-builtin-probe-copperstart-v30/`, under
`artifacts/utility-mui-2026-10-31/`. Validate them with:

```powershell
python scripts/probes/UtilityStartup/inspect_mui_builtin_probe.py artifacts/utility-mui-2026-10-31/mui-builtin-probe-kick31-v30/frame-015000.fastram artifacts/utility-mui-2026-10-31/mui-builtin-probe-copperstart-v30/frame-020000.fastram
```

The [machine-readable receipt](UTILITY_MUI_BUILTINS_2026-10-31.json) pins ROMs,
the library, private images, copied probe results and changed source files.
No ROMs or media are committed, no guest firmware pin is changed, and no package
is published.

## Host validation

The full MUI suite passes **3,864 tests, zero skipped**, under both the default
development profile and experimental classic-client profile. The packager suite
passes **22 tests, zero skipped**, under each profile. Python source compilation
and pinned original MUI Calc download verification pass. The native lifecycle
result above is separate from those host tests.

`dotnet build CopperScreen.slnx -c Release` was attempted and failed at the
existing `RequireCopperOsBundle` target because the qualified A1200 desktop
ROM/HDF bundle is not staged locally. Its checks were not relaxed. This does
not prevent the separate existing headless runner from executing the A500
native replays; it is unavailable full desktop build coverage.

## Earlier application replay and the Cycle request

The v26 library was replayed with the original unmodified calculator under both
ROMs. The runs reached the same 68-object control tree and both displayed
“Failed to create application” by field 60,000. The read-only runs were stopped
while waiting in that requester; they are diagnostic failures, not passing
application or arithmetic checks. Their directories are
`mui-calc-kick31-v28-long/` and `mui-calc-copperstart-v28-long/` in the artifact
root. Despite those directory names, both use the frozen **v26** library.

The native Kickstart first-failure trace
`mui-calc-first-failure-kick31-v29/trace.json` uses the same frozen v26 image.
It records successful List, Listview and Text construction, followed by
`MUI_NewObjectA("Cycle.mui", ...)` returning NULL at field 5,006 (entry 4,993).
The original calculator keeps assembling controls, then passes that NULL as
the second child of its outer Group, explaining the later failure. This is
not evidence of a Group implementation defect. Cycle is a classic built-in,
so it is now registered with Group as its superclass and initialized through
the existing common-control choice core. The new MUB3 probe also constructs
Cycle with two live caller-owned entries, reads active choice 0 and disposes
it in each allocation-balance cycle. The older MUB2 results retain their
explicit protocol label and do not claim Cycle coverage.

## Final MUI Calc replay: classic Application menu remains unsupported

Both fresh v30 images execute the complete original seven-file package with
the final library. Native Kickstart completes 60,000 fields and CopperStart
completes 70,000 fields. Both display **“Failed to create application.”**
They now have matching 71-control trees: one Cycle, one String, two Windows,
two Listviews, two Lists, 45 Texts, four Rectangles and fourteen Groups. All
sixteen built-ins are registered. The failed Application object has been
retired by the final captures; no Application object remains. The control
trees remain while the original program waits in its error requester.

This clears the missing List and Cycle class failures and gets through the
outer Group construction. It does not establish a working Application, an
open calculator window, arithmetic, menu interaction or teardown after
dismissing the requester.

Source inspection and the native field-40,000 capture identify another classic
API incompatibility. `MUIA_Application_Menu` (`0x80420e1f`) at guest address
`0x2292d0` holds `0x219cd4`, the original caller-owned `NewMenu` vector. Its
first twenty bytes are `01000021b80a0000000000000000000000000000`, with
type 1 and label `Project`. CopperOS's current
`MuiApplicationWindowCore.SetApplicationMenuValue` forwards this attribute to
`SetApplicationMenustripValue`, which requires a registered `Menustrip.mui`
object. The raw classic menu vector is not such an object, so this path rejects
it. Native class registration does not translate that legacy argument. This
is an identified remaining incompatibility, not a new successful application
or GUI result. Native Window presentation also remains unavailable as
described above.

Final capture directories are `mui-calc-kick31-v30/` and
`mui-calc-copperstart-v30/`. The exact replay commands are:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --rom C:/Data/ROM/kickstart-3.1-a500.rom --hdf-ro artifacts/utility-mui-2026-10-31/mui-calc-copperos-ofs-v30.hdf --frames 60000 --fast-ram-kib 2048 --boot-probe artifacts/utility-mui-2026-10-31/mui-calc-kick31-v30 --boot-probe-extended --boot-probe-interval 5000

dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --rom ../CopperStart/artifacts/utility-private-snapshots-classic-rom-v140/CopperStart-DiskBoot.rom --hdf-ffs artifacts/utility-mui-2026-10-31/mui-calc-copperos-ffs-v30.hdf DH0:1:32:0:1023 --frames 70000 --fast-ram-kib 2048 --boot-probe artifacts/utility-mui-2026-10-31/mui-calc-copperstart-v30 --boot-probe-extended --boot-probe-interval 5000
```

Use fresh output directories for reruns; the retained captures and failed
historical results are not overwritten.
