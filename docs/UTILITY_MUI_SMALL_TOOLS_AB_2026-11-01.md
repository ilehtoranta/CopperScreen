# Four small MUI utilities: native ROM comparison — 2026-11-01

## Result

All four original utilities were launched with CopperOS's experimental classic-client muimaster.library on CopperStart and native Kickstart 3.1. None is fully compatible in this bounded diagnostic lane. The programs and original support files were not patched or recompiled.

| Utility | CopperStart | Kickstart 3.1 |
| --- | --- | --- |
| MUImem 1.1 | Startup stalls in icon loading before MUI initialization. The corrected fixture installs mathffp.library 37 first. | Explicit MUI version rejection requester. Executable requests version 10; requester text says “V12+”. |
| MUIJoin 1.0 | Prints “Can't Open MUIMaster Library” and returns to CLI. | Same message; CLI reports return code 20. |
| MUIMenu 1.00 | Main window and static controls render; configured launcher row is absent. Native close hides as designed. QUIT removes the OS window, but 15 objects/16 classes leak. | Same missing launcher row and intended hide behavior; QUIT removes the OS window, but 15 objects/16 classes leak. |
| MUI Icon Update 1.2 | Illegal instruction during notification setup, before native window opens. Later unrestricted execution corrupts PC/stack and display. | Same startup stage fails: illegal instruction at PC 0x8, then software failure 80000004. |

These are failures/partial coverage, not application passes. Memory refresh/accuracy, split/rejoin bytes, launcher command execution, icon image/metadata replacement, requester cancellation, keyboard entry, ARexx and complete Workbench drag/drop remain unavailable or untested. The previous passing MUI Calc arithmetic/close evidence is unchanged.

## Qualified inputs and protocol

Reuse `a500-ocs-5m-diagnostic-v1`: 68000 interpreter, PAL OCS 8371/8362, 512 KiB Chip + 512 KiB Slow + 4 MiB Fast, public conservative machine constructor and read-only HDFs. No host folders, OS shims, synthetic guest API calls or guest-memory writes. This is a correctness investigation, not a throughput measurement or a change to the product preset.

- CopperStart v140 ROM SHA-256: `adaaece2e195e4be9524cf5413630bebcf099c1b2cb99dbd17e19127251907a3`.
- Native Kickstart 3.1 A500 ROM: `8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee`.
- CopperOS classic-profile 68000 MUI v48, Resident version 4.1: `54c3d69c4a1358a3f4281cdebbf94db6001935e7ef5b3aabbe43e86c61a2e816`.
- Frozen engine: `e0c814475caf09c50024762067fcf76071ec9bad5a4aa390f801efe9c545c4e2`.
- Frozen CPU: `ff3a73b2dd2a62329be1fcdad1eab8ca3a62d95f73bcfc03a4b87bd25955837a`.
- CopperStart FFS base: Calc v50-backfill, `4473c347e20345e0f13171876b0147bbd63c4cb00cd79a5e19a21ab63b084695`, with the fixed v2 guest Intuition/Layers provider. Native OFS base: Calc v48, `e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799`.

The standalone harness references the frozen Calc v4 dependencies, rather than the runner outputs rebuilt by the subsequent production build. Each original archive hash is checked against the corpus manifest. Every original archive member is installed unchanged under `Utilities/`; full filesystem inventories and installed-file hashes are checked after readback. Private images, binaries, screenshots and RAM dumps stay outside source control. The machine-readable [receipt](UTILITY_MUI_SMALL_TOOLS_AB_2026-11-01.json) pins selected captures and image receipts.

## Compatibility findings

### MUI version gates

The current classic-client library advertises major version 4; it was introduced for the MUI 2.0 Calc client, not full MUI conformance. MUIJoin's unchanged binary executes `moveq #8,d0` before Exec OpenLibrary (file offset 0xf38). Both ROMs reject that requirement. MUImem requests version 10 (offset 0xb74); its native requester literally says “muimaster.library V12+ could not be opened”. Keep the actual binary requirement distinct from that message text.

Do not qualify either utility by merely raising the Resident version. MUImem also names Radio.mui, which is absent from the sixteen registered native built-ins. String keyboard input and the broader MUI 3 features are not qualified by Calc's earlier pass.

### MUIMenu: dynamic family membership

The original source's AddButtFunc constructs an HGroup containing the configured text button and shortcut, then calls `DoMethod(addVgroup, OM_ADDMEMBER, buttons[itnum])`. The MUI native/headless dispatcher implements the MUI Family methods but does not route BOOPSI OM_ADDMEMBER/OM_REMMEMBER to those operations. The dynamic group and its two Text objects remain detached. On both ROMs “Test Utility” has zero geometry and no containing window; the row is visibly absent, while ADD/DELETE/CHANGE/ABOUT/SAVE/QUIT and the status line render.

The fixture is the program's documented ASCII `S:MUIMenu.data`, with a single synchronous `C:Echo MUI-MENU-FIXTURE` entry. The hidden button is recorded as unavailable, never as clicked or executed. The original close notification returns the program's HIDE_ID; it is intentionally different from QUIT. No cleanup failure is inferred merely from that hide action.

A separate hardware mouse replay skips the unavailable launch action and clicks QUIT. At field 100,001 both ROMs have removed the native window and returned to CLI with code zero, but retain the same 15 MUI objects (6 Text, 3 Group, 2 Image, 2 Rectangle, 2 String), all 16 classes, no Window/Application objects and dispatch/callback depth zero. Library open count is zero. The earlier native capture at 75,001 already contains those same 15 objects. This is a MUI ownership/cleanup failure; OS window unlinking succeeds. The private runs were stopped after capturing this bounded failure rather than reporting a cleanup pass.

The edit window additionally requests Popasl.mui, which is unregistered in this build and has no external class file in the private images. Editor/file requester compatibility is not established even though the main window opens.

### Icon Update: missing class and invalid retained object

The unchanged Create_Window source requests Popasl.mui for both source and target file selection. Only sixteen built-ins are registered; Popasl is absent. The captured state on both ROMs has 39 objects and 16 classes, a Window object with no root-object attribute/native handle, and an Application which has nevertheless been returned to the client.

The first native RAM fault occurs at field 17,813, vector 4, PC 0x8. The client is installing its original `MUIM_Notify / MUIA_AppMessage / MUIV_EveryTime / MUIM_CallHook` notification. Its DoMethod source operand is `0x21ea4c`, which is absent from the live object registry. That region has been reused for other library state. The unchanged SAS/C DoMethod helper loads the class pointer from object-4, dispatches via Hook.Entry at class+8, and reaches address zero. The first CopperStart non-privilege RAM fault is field 24,163, vector 4, PC 0x10. Both captures precede a native window opening.

This establishes an invalid retained object/constructor-failure propagation problem. Unsupported Popasl and acceptance of the incomplete window/application are involved in the failed construction; a full allocation-by-allocation ownership trace is still needed before selecting the precise source fix. It is not evidence of icon data corruption, Layers backfill, ARexx execution or a successful icon operation. The earlier 25,001-field screenshots show the later native software failure and CopperStart's damaged display; the bounded first-fault replay is the diagnostic evidence for the startup failure.

### MUImem: CopperStart startup stall

The first CopperStart fixture omitted `C:CopperFfp`; its blank display is not MUI compatibility evidence. A fresh image adds that existing qualified prerequisite before CopperGui and MUImem. The corrected capture confirms mathffp.library version 37, a valid icon.library base, and no MUI objects/window by field 30,000. The saved program return address is the instruction after icon.library GetDiskObject (original file offset 0x678). Startup has not reached the later OpenLibrary version-10 call, so the zero MUI pointer alone must not be interpreted as an observed version rejection on this ROM.

MUImem's runtime allocates 10,808 bytes and moves SP with EXG rather than updating Task stack metadata. In the corrected capture the application's allocation is `0x4fded0..0x500908`, its saved context is `0x4fff46`, and the recorded task stack bounds remain `0x4980c0..0x4990cc`. Current CopperStart context restore/result helpers check those bounds. The native comparison below isolates a CopperStart alternate-stack task-resume compatibility failure. The out-of-bounds metadata checks in context load/result helpers are a source-level candidate consistent with this failure; identifying/removing the exact offending guard and retesting the original MUImem are follow-up implementation work. This is not evidence of a malformed icon or a proven icon parser defect.

A separate alternate-stack DOS Delay diagnostic returns successfully on both ROMs. CopperStart Delay only advances its DOS tick state, so this does not establish that a real blocked task can resume on the alternate stack. The stronger timer WaitIO comparison runs CopperGui first so the bounded guest timer provider is present, opens timer.device successfully and requests a one-second timer. At 5,000 fields:

| Timer probe | CopperStart | Kickstart 3.1 |
| --- | --- | --- |
| Normal stack control | Stage 3: wait returned and allocation freed | Stage 3: wait returned and allocation freed |
| Manual alternate stack | Stage 1: wait did not resume | Stage 3: wait returned and allocation freed |

The timer reply has arrived in the failing CopperStart capture (`mn_Node.ln_Type=7`, IO error 0), yet the application remains waiting. The failing CopperStart probe's saved context is `0x4fbeee`, inside its allocated alternate stack `0x4f96d0..0x4fc108`, while task metadata still records `0x4f86c0..0x4f96cc`. This reproduces the relevant resume failure without MUI, an icon parser, original application changes or host memory intervention. An earlier timer attempt omitted CopperGui and could not open the device on CopperStart; it is unavailable coverage and is excluded from this comparison. The DOS Delay probe passes on both ROMs but does not exercise a real blocked CopperStart timer wait.

## Evidence and limitations

Original-distribution integrity and licensing are in the [acquisition report](UTILITY_MUI_SMALL_TOOLS_2026-11-01.md). New runtime harness/image/inspection sources are under `scripts/probes/MuiUtilityInteraction/` and `scripts/probes/UtilityStartup/`.

The local root is `artifacts/mui-small-utilities-2026-11-01/`. Selected original runs are `runs/`; bounded first-fault/FFP reruns are `interaction-v4/`; MUIMenu button/QUIT replays are `interaction-v5/`. Earlier native-close-only MUIMenu runs correctly demonstrate hide, not application shutdown. The v2 fault harness experiment failed on conversion of the CPU's negative sentinel. The v3 experiment stopped on ordinary privilege traps too early; it is excluded from compatibility conclusions. Its raw evidence remains retained. v4 deduplicates fault sites and excludes ordinary privilege/ROM bootstrap probes from the startup-failure stop condition.

## Follow-up order

1. Correct MUI construction/ownership failure handling and implement native Popasl support; keep original clients unchanged.
2. Route classic OM_ADDMEMBER/OM_REMMEMBER through Family ownership/mutation semantics, then rerun MUIMenu launch, edit and leak-free quit.
3. Correct alternate-stack task resumption in CopperStart and rerun MUImem until it reaches its MUI version gate.
4. Add required Radio and related client behavior before deliberately qualifying a newer classic MUI version profile. A version-number-only change is not a compatibility fix.

No CopperOS/CopperStart implementation source, ROM, package version or published bundle was changed for this investigation. The standalone final diagnostic harness builds in Release with zero warnings/errors. Host suites and a production rebuild are not substitutes for the native runs recorded here, and no broader emulator acceptance result is claimed.
