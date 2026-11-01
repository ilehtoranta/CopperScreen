# Classic MUI Application_Menu compatibility — 2026-10-31

The preceding registration work was committed and pushed to `main` before this
fix: CopperOS `3e81672` and CopperScreen `3b46c8b`. ROMs, media and unrelated
workspace changes were excluded. The [previous built-in report](UTILITY_MUI_BUILTINS_2026-10-31.md)
retains the original failed Application construction evidence.

## Implementation

The experimental classic native `MUI_NewObjectA("Application.mui", ...)`
boundary now converts a caller-owned GadTools `NewMenu` vector passed through
`MUIA_Application_Menu` into real native Menustrip/Menu/Menuitem objects using
the existing `MUIO_MenustripNM` factory. The copied constructor tags pass that
strip through `MUIA_Application_Menustrip`. Caller tags, vectors and strings
are not rewritten. The default development profile retains the strict
MorphOS-style Menustrip-object setter.

The adapter handles TAG_MORE, TAG_SKIP and TAG_IGNORE, preserves effective tag
order, treats a null classic menu as absent and preserves an existing borrowed
Menustrip object. It rejects duplicate legacy menu initializers and malformed
tag lists. It copies the effective tags before invoking the factory, rechecks
the copied initializer count after allocation, frees scratch tags and releases
unadopted generated strips on failure. Already-adopted or already-destroyed
strips are not disposed a second time.

Native menu constructors now initialize the existing typed menu state. Explicit
disabled flags survive initialization; TRUE is only the missing-attribute
default. Generic object teardown releases menu-owned strings and state as well
as its child tree. Built-ins already dispose their children through the public
gateway, so that path no longer nests the external-BOOPSI retained-child
snapshot protocol. External class snapshot behavior remains unchanged.

The intermediate v32 probe rejected the Application because the typed menu
state was missing. It also exposed 704 bytes lost per disposal cycle and dead
public child bindings. Those are retained failed diagnostics, not successful
menu compatibility evidence. The final v33 implementation supplies the state
and keeps the built-in disposal path separate from external snapshots.

The v33 full-client exit exposed a separate constructor ownership omission:
Application_Window tags had public bindings but were not attached to the
headless lifetime tree. v34 adopts each supplied closed Window during native
Application construction and records its owner. This supplies ownership only;
it does not create a native window, event port or presenter. The MMC2 probe
includes both closed windows so its balance check covers that complete graph.

## Inputs and native probe

All files are under `artifacts/utility-mui-2026-10-31/`. The v33 development
library is `copperos-mui-classic-menu-68000-v33/muimaster.library`, 1,025,880 bytes,
16,991 relocations, SHA-256
`a8a67aae79e9a615aee59ef43ae3bbc934289c193021a2dbfc85618b5c25dfab`.
It remains the explicitly selected experimental 4.1 classic profile; the
default development identity remains 0.1. Nothing is published or staged into
the desktop app's firmware bundle.

The final v34 library is
`copperos-mui-classic-menu-68000-v34/muimaster.library`, 1,026,272 bytes,
17,013 relocations, SHA-256
`9f30e72842c5faecf04a04a836e6639bfc189ce3084729b47cf1864ead9734ca`.
The v33 identity above is retained as intermediate evidence.

The ROMs, GUI provider and OS prerequisite hardfiles remain pinned to the
previous report. Both runs use the 68000 interpreter, PAL OCS, 512 KiB Chip RAM,
512 KiB slow RAM and 2 MiB Fast RAM, with read-only HDFs. All seven original
MUI Calc 1.25 files are unchanged.

[mui_classic_menu_probe.py](../scripts/probes/UtilityStartup/mui_classic_menu_probe.py)
creates an original 68k command which supplies a Project menu with ordinary,
separator and disabled items. It constructs an Application, reads its Menustrip,
verifies one title through native Intuition GetAttr, verifies the obsolete
getter alias, verifies Application ownership of the strip plus two closed
windows, disposes the complete tree and repeats. Copied results remain
in a reset-lived Exec resource. The checker also verifies the original input
tags and menu records are unchanged and requires exactly equal available
public memory after both cycles.

The final v34 MMC2 probe passes under both ROMs:

| Observation | Kickstart 3.1 | CopperStart |
| --- | --- | --- |
| Applications and Menustrips | Non-null in both cycles | Non-null in both cycles |
| Native getter results / title count | Successful / 1 | Successful / 1 |
| Alias getter | Same Menustrip object | Same Menustrip object |
| Application children | Strip + two closed Windows | Strip + two closed Windows |
| Available public memory after cycles 1 and 2 | 2,053,440 / 2,053,440 | 1,099,708 / 1,099,708 |
| Final registry objects / built-ins | 0 / 0 | 0 / 0 |

Images are `mui-classic-menu-probe-ofs-v34.hdf` and
`mui-classic-menu-probe-ffs-v34.hdf`, prepared with
`prepare_mui_library_probe.py --classic-menu --probe-only`. Capture directories
are `mui-classic-menu-probe-kick31-v34/` and
`mui-classic-menu-probe-copperstart-v34/`.

```powershell
python scripts/probes/UtilityStartup/inspect_mui_classic_menu_probe.py artifacts/utility-mui-2026-10-31/mui-classic-menu-probe-kick31-v34/frame-025000.fastram artifacts/utility-mui-2026-10-31/mui-classic-menu-probe-copperstart-v34/frame-035000.fastram
```

This proves bounded native constructor compatibility and repeated ownership
cleanup. It does not qualify menu presentation, input, image menus or the full
classic MUI API. These are guest compatibility diagnostics, not host performance
measurements.

## Validation

The full MUI host suite passes **3,875 tests, zero skipped**, under both profiles.
Eleven added cases cover classic tag conversion and rollback, native menu
constructor completion, disabled defaults, copied-string lifetime and direct
generic disposal, and closed Window adoption without opening a native window.
The packager suite passes **22 tests, zero skipped** in each
profile. The previous desktop solution build limitation is unchanged: its
required qualified A1200 ROM/HDF bundle is not staged locally. No bundle check
or firmware pin was changed for these tests.

The earlier MUB3 List/Listview/Cycle probe passes with the intermediate v33 library
under both ROMs, with equal available memory after its two cycles:
2,053,904 / 2,053,904 on Kickstart and 1,100,176 / 1,100,176 on CopperStart.
Those captures are `mui-builtin-regression-kick31-v33/` (15,000 fields) and
`mui-builtin-regression-copperstart-v33/` (20,000 fields).

## Original-client v33 replay and return-path evidence

Native Kickstart completes 60,000 fields and CopperStart completes 70,000
fields, using `mui-calc-copperos-ofs-v33.hdf` and
`mui-calc-copperos-ffs-v33.hdf`. Both original clients receive a non-null
Application object, construct the Project menu and three Menuitem objects,
then tear down the Application/menu and return to the CLI without a calculator
window. The old “Failed to create application” requester is gone. No emulator
unsupported-hardware condition is recorded.

The original command's constructor return is stored in its global Application
variable. The native image contains the call at `0x21d020`, followed by the
store of D0 at `0x21d028` into `0xa56(A4)` and a non-null branch. Its final
global at `0x21a6b6` contains `0x34bb8c`, the Application observed in the earlier
live tree. The corresponding CopperStart global at `0x29928e` contains
`0x00c2ecbc`. This distinguishes successful public construction from merely
seeing a temporary internal object during a failed constructor. The live tree
reaches 77 objects, including Application, Menustrip, Menu and three Menuitems.

Native Window presentation remains unavailable. The built-in Window_Open
getter deliberately returns zero until a real native presenter exists. The
client queries that attribute and has a “Failed to open window.” exit path;
the final captures show CLI return rather than an interactive calculator.
No arithmetic, menu interaction or window presentation is qualified by this
fix. Return to the CLI is not a passing calculator scenario.

These intermediate captures are `mui-calc-kick31-v33/frame-060000.*` and
`mui-calc-copperstart-v33/frame-070000.*`. v33 did not release all closed Window
objects; it is not a complete Application lifetime pass.

## Final v34 original-client replay

The final candidate is replayed for 100,000 fields on native Kickstart and
110,000 on CopperStart. Both construct the Application and its menu tree,
and enter disposal after failing to open a native calculator window. The
menu objects are released. Cleanup of the larger control tree progresses
slowly and does not complete within these bounds: 58 registered objects remain
on Kickstart and 55 on CopperStart, with three active native callbacks in each.
Both runs complete their requested fields without an unsupported-hardware
condition. The replays finish on 2026-11-01; the artifact directory retains the
2026-10-31 start date. This is unavailable
full-client teardown coverage, not a clean shutdown or arithmetic pass.
The MMC2 small-tree ownership and balanced-memory result remains a separate
passing result. No host-throughput qualification is implied by these runs.

Captures are `mui-calc-kick31-v34/frame-100000.*` and
`mui-calc-copperstart-v34/frame-110000.*`. The
[machine-readable receipt](UTILITY_MUI_NEWMENU_2026-10-31.json) pins the exact
library, images, captures, source changes, test logs and prior pushed commits.
