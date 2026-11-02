# Small MUI utility compatibility fixes — 2026-11-01

Completed 2026-11-02: the four original utilities pass the bounded functional
workflows below against CopperStart and Kickstart 3.1, using CopperOS MUI.
The baseline and intermediate failures remain recorded below and in the
[original A/B report](UTILITY_MUI_SMALL_TOOLS_AB_2026-11-01.md).

| Utility | CopperStart | Kickstart 3.1 | Verified operation |
| --- | --- | --- | --- |
| MUImem 1.1 | PASS | PASS | Counter/label checks, close and cleanup |
| MUIJoin 1.0 | PASS | PASS | Preferences, actual split/rejoin file bytes, cleanup |
| MUIMenu 1.00 | PASS | PASS | Exactly one command execution, quit and cleanup |
| MUI Icon Update 1.2 | PASS | PASS | Actual updated icon, preserved target metadata, unchanged source, cleanup |

MUImem/MUIJoin/MUIMenu retain their v48 results with library SHA-256
`3a89edbbb3e5e1eeb7556e9e3bd61941adf093c61c9b6b86eb1d4a9e187d1906`.
Icon Update uses the requester-fixed v49 library, SHA-256
`5065a4ea443cb6f12ec6267e07cee7e03cba4c19bad80c41634288b59338be30`,
in fresh v56 runs. This is not a complete matrix rerun on one common library
binary. Each result retains its own exact input provenance.

Fixes cover native MUI ownership, input-message lifetime, owned String getters,
requester class services and CopperStart guest classic-icon writing. Original
utility binaries are unchanged and use their authored launch-stack requirements.
The frozen engine and ROMs remain unchanged during the final replays. CopperStart
uses an isolated diagnostic ROM and a separately loaded guest icon writer;
this does not qualify a production ROM or claim ROM residency for that writer.

All 3,973 classic MUI host tests and 17 focused classic-icon encoder/store tests
pass with zero skips. CopperScreen Release builds without warnings/errors;
CopperStart Release builds with zero errors and 12 warnings. These are separate
from native replay results. The following chronological sections retain earlier
pending/failed observations; the completed v56 result at the end supersedes
their current-status wording without changing their recorded outcomes.

## Changes under test

- CopperStart context save, restore and asynchronous result delivery accept a
  mapped application stack outside advisory Task stack bounds. Owned saved-frame
  validation remains. MUImem uses precisely this kind of manually allocated stack.
- CopperOS routes classic `OM_ADDMEMBER` / `OM_REMMEMBER` through Family ownership.
- Native built-ins now register Radio, Popstring and Popasl. Popstring/Popasl adopt
  their String and Button children through the ordinary Group tree. This provides
  construction, layout and disposal; it does **not** implement a file requester.
- Native Window construction rejects a missing root. Failed Group/Application
  construction continues adopting later valid children so failure teardown owns
  them, including children after a NULL tag value.
- Group measurements use the existing call-local native MinMax cache. Live
  callbacks still invalidate the cache, including through the Group entry point.
- Plain composite Group notification registration/removal reaches its children.
- Popstring reads of String contents forward to the owned String control.
- The experimental classic profile targets version 12.1 to admit the original
  v8/v10 clients for testing. The separate default development identity remains
  version 0.1. Admission is not evidence of implemented requester, keyboard,
  Radio input, or complete MUI 3 compatibility.

## Verified evidence

The production CopperStart solution built with seven warnings and no errors.
Focused Exec tests produced 44 passes and two failures; the same two inhibition
test failures reproduce with the original context implementation. They are not
reported as passes. The baseline override and logs remain in CopperStart's
`artifacts/utility-mui-stack-baseline-20261101/`.

The native alternate-stack timer replay now returns and frees its stack on the
rebuilt CopperStart ROM. The unchanged normal-stack control also returns. Both
open timer.device successfully, and both reach stage 3 at 5,000 fields. Evidence:
`artifacts/mui-small-utilities-fix-2026-11-01/stack-retest-v2/`.

This ROM is an explicitly recorded diagnostic snapshot of current source. It
preserves ROM-resident execution changes and uses a coherent FF9400 graphics
placement in the snapshot because the current default FF9C00 placement exceeds
the 512 KiB image. It is not a qualified production firmware replacement. Source
hashes and the two layout overrides are recorded in CopperStart's
`artifacts/utility-mui-stack-isolated-v2/snapshot-receipt.json`.

The default MUI host suite passed all 3,913 tests with no skips after the Group
cache and notification changes. The 28 focused tests also passed. These do not
replace native ROM/media replays. The later Popstring forwarding/version changes
also pass all 3,913 classic-profile tests with no skips.

The first native membership-only MUIMenu reruns show the configured launcher row
and accepted hardware mouse interaction on both ROMs. Remaining objects fall
from 15 to 12, consistent with ownership of the three-object row. Command
execution and complete disposal are not established by this intermediate build.

The registration/composition-only Icon Update reruns reach native window creation
without the previous illegal-instruction failure, but both time out at 160,000
fields during layout measurement (51 objects, 19 classes, active dispatch). These
are retained failures, not successful launches. The subsequent Group cache fix
has a fresh paired replay.

## Current replay inputs

The layout/notification candidate is the classic 68000 library v7, 1,258,652 bytes,
SHA-256 `afda46db18455104cab49948daeb3a1710ece89592fd81f639cc711387b47593`,
still Resident 4.1. New images and runs are under
`artifacts/mui-small-utilities-fix-2026-11-01/images-layout-v7/` and
`runs-layout-v7/`. They compare the snapshot CopperStart ROM above against the
unchanged native Kickstart 3.1 ROM. Every image has an archive/file inventory
receipt. Original utility files remain unchanged.

The menu fixture now launches an original test command which publishes a copied
named Exec resource. `inspect_menu_launch_witness.py` requires one validated
resource with launch count exactly one. Button acceptance alone is insufficient.
The hardware interaction harness v6 accepts only the known 4.1/12.1 private
library layouts and records the observed version in its result.

The subsequent v8 candidate includes String-content forwarding and Resident
12.1. It is 1,258,936 bytes, SHA-256
`cb1d6b0d3f743df9a66d51df1bebc2cf01dc6a78b41ddff79327227b801b2f94`.
All eight fresh private images are prepared under `images-v8/`. The replay batch
records process/output paths in `jobs-v8.json`, starts MUImem/MUIJoin on both ROMs,
then MUIMenu/Icon Update on both ROMs. These are pending results, not passes.

The read-only `inspect_mui_native_stack.py` derives its code relocation from the
NewObjectA native vector and build receipt. The library name can point at a
compiler literal and must not be used as an image-base anchor. Stack words are
symbol candidates, not a validated unwind. Corrected 25,000-field v7 Icon Update
evidence is inside font/control Setup; at 50,000 fields it is in Group grid axis
layout. The old v2 100,000-field native capture is also in Group grid layout.
This establishes startup progress, not successful window presentation or proof
that the cache change resolves the whole latency problem.

## Completed candidate results and follow-up

The diagnostic CopperStart ROM's SHA-256 is
`12f7f6ffd4ca03b0cfca9267afe2c250e3bc58fee7ac317e656c2dedbb82fc29`.

| Candidate / utility | CopperStart | Kickstart 3.1 |
| --- | --- | --- |
| v8 MUIJoin | Open, 5,000-field observation, hardware close, empty object/class registries, 5,000-field settling and OS-window unlink pass at field 50,551. | Same bounded pass at field 44,051. |
| v8 MUImem | Passes the previous icon-loading/version blockers and constructs 106 objects, then faults at field 48,553: illegal instruction PC 0x10. No window-open pass. | Passes version admission and constructs 106 objects. Times out in startup at 120,000 fields, depth one; no window-open pass. |
| v7 MUIMenu | Configured command executes exactly once. QUIT removes all headless objects, but native classes remain/reappear; cleanup replay fails at field 104,101. | Command executes exactly once. All headless objects are gone, but 19 native classes remain at the 120,000-field limit. Cleanup fails. |
| v7 Icon Update | Times out in startup at 120,000 fields: 51 objects, 19 classes, depth one. | Same bounded startup timeout at 120,000 fields. |

MUIJoin's result covers launch/observation/close only. Split/rejoin bytes, ASL
selection/cancellation and preferences input are not tested by that replay.
The menu witness was inspected in both final RAM captures, not inferred from
button state. Earlier CopperStart field-75,000 witness absence was an intermediate
capture before command completion; it is not the final command verdict.

MUIMem's native field-50,000 callback nesting later unwinds to depth one. That
sample alone does not establish an infinite recursion or counter leak. Its
CopperStart first fault is retained for separate investigation.

The v7 native Menu cleanup retains a public application binding in lease-released
state. Source review also finds a stale-predecessor hazard: dynamic child removal
can retire the predecessor captured before the parent's recursive native disposal.
The v9 fix re-finds the current predecessor and next link immediately before
unlinking, retaining binding/lease authority checks. Both a removed and a surviving
predecessor mutation have regression coverage. All 3,915 classic-profile host tests
pass with this change. A v9 native build and fresh paired menu replay are queued;
the subsequent v9 paired replay verifies this native cleanup fix (see below).

The v8 first wave has now completed. Its second wave (Menu/Icon Update on both
ROMs) is running. The v9 focused native-public-object tests pass 9/9, and its
complete classic suite passes 3,915/3,915 without skips. Native compilation is
in progress. The diagnostic watchers and process receipts remain under the local
artifact root; a launched process is not a completed replay.

All runs retain the A500 OCS 5 MiB diagnostic profile, frozen engine/CPU binaries,
read-only HDFs and ordinary hardware mouse input from the baseline report. No
guest-memory writes, synthetic MUI calls or host OS shims participate. These are
correctness diagnostics, not performance acceptance measurements. ROMs, original
archives, hardfiles, generated commands, binaries and captures stay uncommitted.

## Subsequent native results and stack correction

The v9 MUIMenu replay passes on both ROMs: field 109,351 on CopperStart and
101,601 on Kickstart 3.1. Both final captures have zero objects, classes and
dispatch depth, with the original OS window unlinked. The named command witness
proves exactly one execution on each ROM. Evidence is under `runs-menu-v9/` and
`menu-v9-*-witness-final.json` in the same local artifact root.

The v12 per-layout Area-policy cache resolves Icon Update's bounded startup
timeout. Both native windows were visually inspected: fields, image areas,
checkboxes and Start button draw. Hardware close and empty-registry/window-unlink
checks pass at fields 109,951 (CopperStart) and 94,901 (Kickstart 3.1). These
results do not qualify an actual icon update or file requester.

The earlier statement that CopperStart Menu classes "remain/reappear" needs a
verifier qualification. The old final-button release path could begin settling
with classes still retiring and an owner operation active. In the v12 CopperStart
captures, classes decrease from 19 to 5 with one operation active; that is not
evidence of regrowth. Protocol v8 now requires zero classes, objects, dispatch
depth **and** owner operations before settling, on both cleanup paths. Old raw
failures are retained and are not relabelled passes. The v7 Kickstart timeout's
retained application binding was separately fixed by the v9 unlink change.

MUImem's CopperStart failure was traced to actual program-code corruption.
Its original helper at guest address `0x4fdcfc` matches the original executable
at field 25,001, but is overwritten by a saved native frame and an `MUO1` owner
record at the field-48,553 fault. The program uses an allocated 10,808-byte stack
at `0x4fdd40..0x500778` while Task metadata still says
`0x498090..0x49909c`. The native MUI stack gate checked only distance above
StackLower, incorrectly treating this distant small stack as sufficient space.

The gate now checks both recorded bounds and swaps to its owned 128 KiB stack
when SP lies outside them. CopperStart StackSwapCall now permits restoring the
actual mapped return stack alongside stale advisory Task bounds. It still checks
mapped/even pointer and return-PC requirements. Four instruction-level tests
execute the emitted 68000 gate, including small and out-of-bounds stacks. All
26 builder tests and 46 focused CopperStart stack/context tests pass without
skips. No third-party executable or emulated CPU behavior was changed.

The rebuilt diagnostic ROM is 524,288 bytes, SHA-256
`f4790c40fa6d340a91f4707d7eff5e5aba5aced16e0c84f22b33ab284859e052`.
It retains the snapshot's ROM-resident execution and explicit FF9400 placement;
it is not a production firmware qualification. Its composition manifest is in
CopperStart `artifacts/utility-mui-stack-gate-rom-v13/`.
The v13 classic MUI library is 1,260,812 bytes, SHA-256
`6133bd60ffcc84b0480271d94aaad10abff110a4411867c3cdbf62baf89e36ba`.

Fresh v13 runs use those inputs, the unchanged Kickstart 3.1 ROM and frozen
engine/CPU. The ceiling is explicitly widened to 240,000 fields for the larger
MUImem UI; earlier 120,000-field results remain unchanged. This is a correctness
diagnostic and carries no performance acceptance claim.

| v13 utility | CopperStart | Kickstart 3.1 |
| --- | --- | --- |
| MUImem | Open/observe/hardware-close/cleanup pass at 188,551. | Same bounded pass at 166,051. |
| MUIJoin | Same bounded pass at 47,801. | Same bounded pass at 41,451. |
| MUIMenu | Command executes exactly once; hardware QUIT and cleanup pass at 102,601. | Same bounded pass at 94,851, with exactly one command execution. |
| Icon Update | Open/observe/hardware-close/cleanup pass at 110,001. | Same bounded pass at 94,901. |

Both MUImem v13 field-50,001 captures preserve the original helper bytes, beyond
the original failure point. Read-only guard and Task-bound receipts are retained
as `mem-v13-*-code-guard-50k.json`, `mem-stack-corruption-v13.json` and
`mem-stack-task-bounds-v13.json`. These are bounded observations, not a proof of
all program memory or every stack use.

## Functional input follow-up

Native String controls previously had no keyboard input path. The next candidate
adds hardware-click focus, keymap-based text translation, empty owned-buffer
initialization and String content/acknowledge notifications. Its full classic
host suite passes 3,923 tests with no skips. Native compilation and a hardware
typing replay are pending; these host tests do not qualify guest keyboard input.
Protocol v9 adds a `string-fixture` action which clicks an empty String field,
types `test` and Return using real raw-key down/up input, inspects contents, then
closes normally. It never writes guest memory or invokes synthetic MUI methods.

Remaining functional coverage includes real Split/rejoin bytes, ASL selection
and cancellation, live memory counters, Radio selection and actual icon file
updates. Popasl registration/composition is complete, but the native asynchronous
requester path is not implemented. Do not infer file-requester compatibility from
successful construction or launch/close results.

The keyboard candidate has now compiled: 1,263,620 bytes, SHA-256
`f0bd64110f4a7e4b1d63ddb261375c6230297fa2a0dc1823253e35fd59d9d50a`.
Its paired Icon Update typing replays are under `keyboard-v18/` and remain
pending. CopperScreen's production solution build passes with zero warnings and
errors (`production-build-v18.log`).

A separate native `OpenLibrary` probe now establishes provider availability on
the actual test images. Both ROMs open ASL 40.6 and mathtrans 37.1; keymap is
40.0 on CopperStart and 40.4 on Kickstart 3.1. The probe publishes copied results
in a named guest Exec resource, then closes each library normally. Generic
harness MAX_FIELDS is not its verdict: `library-presence-*.json` independently
validate a complete resource and the actual return values. The file-picker gap
therefore cannot be attributed to a missing ASL provider.

Visual review of MUImem v13 also finds overlapping columns on **both** ROMs.
The launch/close passes above do not mean its display is correct. Observed RAM
shows SameSize groups allocated only eight pixels per column despite longer
labels (for example `Maximum`). Both observed captures still preserve the
original executable helper. Typed group records show equal sizing enabled:
the largest child minimum can exceed the smallest finite child maximum when
individually valid fixed-size children differ. The equal-size selection kept
those contradictory bounds and later clamped the group to the smaller maximum.
The next source candidate raises that common maximum to the required minimum,
with horizontal and vertical regression cases; native confirmation is pending.
Read-only geometry, attributes and typed stores are retained in
`mem-v13-layout-records.json`. This is a layout finding, not a font or ROM-specific
rendering diagnosis.

The equal-size fix passes all 3,925 classic-profile tests with no skips. Its
native library is 1,263,644 bytes, SHA-256
`bab2b289d7ff0b75f860e0f0ac30499df14b51de7e0f63637d0a3d57a158de30`.
Paired MUImem runs are now active under `equal-v19/`. The read-only static-label
geometry checker supplements visual review and does not test live counters.

A separate MUIJoin Split/requester cancellation replay is active under
`join-requester-v13/`, using the fixed v13 library and protocol v10 hardware
input. It must observe a distinct native requester window and a real Cancel
gadget, then verify requester removal and MUI dispatch return. Missing or
unsupported gadget geometry is unavailable coverage, not a successful cancel.
Neither Split nor Join output bytes are yet qualified.

All eight v13 launch/observation/cleanup replays are now complete and pass their
bounded checks. All finish with zero objects, classes, dispatch depth and owner
operations, followed by the settling interval and native-window unlink check.
Both v13 Menu witnesses independently report exactly one command execution.
The default development-profile host suite also passes all 3,925 tests without
skips. Functional tests and the MUImem rendering correction remain pending as
recorded above; this is not a declaration of full utility compatibility.

## Functional follow-up: requesters, typing and memory counters

MUIJoin's original Split button opens the real ASL file requester on both ROMs.
Hardware requester cancellation, return to the main window and final cleanup
pass at field 58,851 on CopperStart (`join-requester-v15/`) and 52,001 on
Kickstart 3.1 (`join-requester-v14/`). The first v10 requester checker expected a
GadgetText label, which GadTools need not expose. Its runs were explicitly
cancelled as unavailable harness coverage, not passes. Protocol v11 admits the
public system close gadget; v13 also admits CopperOS Intuition's public
CloseGadget flag and header geometry, since its drawn system gadgets are handled
directly rather than linked as application Gadget nodes. The native requester
actually disappears and dispatch unwinds before main-window close is attempted.

The first keyboard replay produced `ttttest` on both ROMs. This is retained as
`STRING_INPUT_MISMATCH`, not a typing pass. The fixture held each raw key down
for 1,000 fields (20 emulated seconds), allowing normal key repeat. The corrected
fixture releases after two fields and keeps the longer interval between keys.
Fresh paired runs using the same library and images are under `keyboard-v19/`.

MUImem's equal-size correction passes the known-label width and heading
separation checks on both ROMs (`mem-v19-*-layout.json`). Kickstart's complete
open/observe/close/cleanup replay passes at field 159,851. CopperStart's layout
also looks correct, but the equal-size replay reaches the 240,000-field ceiling
in cleanup with 106 objects, 19 classes and depth one. A geometry pass does not
establish application exit. Read-only user-stack inspection at field 225,001
has candidates in DrawControl/PopClip/InstallClipRegion; these are not a
validated unwind or proof of a particular Layers defect.

A deeper counter check exposes another CopperStart bug: Exec AvailMem ignores
MEMF_TOTAL and returns current free memory, making MUImem's Maximum and In-Use
rows incorrect. Native Kickstart 3.1 independently establishes that TOTAL sums
`mh_Upper - mh_Lower + 32` for each matching MemHeader, with LARGEST taking
precedence. This was checked directly in the user's native ROM and recorded in
`native-availmem-reference.txt`. The first inspector omitted those 32 bytes and
is retained as a failed verifier experiment. Corrected v2 results match all three
native Maximum values: Chip 523,264, Fast 4,718,592 and Total 5,241,856. They fail
on CopperStart. Free/used/largest snapshot accuracy is not established by this
stable-capacity check.

The separate native AvailMem guest probe passes on Kickstart 3.1 after 5,000
fields: TOTAL filtering, stability across a real temporary allocation and
LARGEST precedence all match. Its named Exec resource is the verdict, rather
than the generic harness MAX_FIELDS result. Evidence is
`availmem-native-kickstart31-v25.json`. The corrected fixture decodes completely
as 59 MC68000 instructions. An earlier unexecuted image preparation contained
an invalid TST address-register instruction; it was corrected before replay
and is not used as evidence.

CopperStart's portable AvailMem implementation now implements that native TOTAL
rule without changing ordinary free queries or LARGEST precedence. Twenty-five
focused memory/vector/context tests pass. The existing isolated ROM snapshot has
only this additional source hunk applied; its receipt is
`availmem-v24-receipt.json`. A fresh 512 KiB ROM build is in progress, preserving
the previous ROM-resident kernels and coherent graphics placement. Native counter
confirmation still requires that new ROM.

## Native asynchronous Popasl candidate

The native file-popup path now has an implementation candidate. It forwards
constructor ASL tags, passes the owning screen and private input-port request,
prepares filename/drawer defaults and calls the start hook on the application's
task. A guest DOS process runs the real ASL requester with an owned message and
explicit library base. The application's input loop includes its reply signal
and performs completion, stop-hook/String updates and Active-state publication.
Disposal waits for an active requester; the worker never changes MUI object
state. These ownership and hook requirements follow the [MUI Popasl reference](https://github.com/amiga-mui/muidev/wiki/MUI_Popasl).

The bounded candidate implements file requesters. It does not claim font/screen
popup defaults or full Popstring/Popasl conformance. Its library is 1,272,840
bytes, SHA-256
`1f32db668215223fab5d252b92e972d990b39b593902c82b6fa9142c7343698b`.
All 3,933 classic host tests and 26 builder tests pass without skips. Tests cover
classic path split/join, truncation and the owned worker-message codec; these
are not proof of asynchronous guest execution. A fresh paired Icon Update replay
under `popasl-v23/` uses the real popup button, requires Active while the native
requester is open, cancels through native mouse input, requires Active to clear
and verifies final cleanup. The subsequent results are recorded below.

## Latest completed checks and remaining failures

The corrected hardware typing tests now pass completely on both ROMs, including
exact `test` readback, Return, native close, empty registries and window unlink:
CopperStart field 156,701 and Kickstart 3.1 field 140,251. Evidence is under
`keyboard-v19/`. The default development-profile host suite also passes all
3,933 tests without skips for the first asynchronous candidate.

The rebuilt 524,288-byte diagnostic CopperStart ROM has SHA-256
`396c58ee218967d79bfdecbe709ac0fe05007491c4b136e4c07cd1df60fa36de`.
Native graphics composition succeeds with 28,122 bytes and 78 vectors. Its native
AvailMem probe now passes the same TOTAL/filter/allocation/LARGEST checks as
Kickstart (`availmem-native-copperstart-v25.json`). A full MUImem replay with this
ROM and the unchanged v19 library/image is running under `mem-availmem-v24/`.
The compiler/SDK and dependent assembly binaries were rebuilt and their hashes
differ from v13; use the full composition manifest. The source snapshot has the
single additional AvailMem hunk, but this is not a byte-level one-change ROM
comparison. All input versions remain explicit development diagnostics.

The first asynchronous Popasl candidate opens the real file requester with
Active set on both ROMs, and the native close input removes that requester.
Completion then stalls: Active stays set and the worker's reply remains queued.
Both bounded runs fail with `ASL_REQUESTER_CANCEL_TIMEOUT`, at fields 186,901
(CopperStart) and 174,001 (Kickstart). These are retained failures.

The native field-125,001 capture establishes why: the popup's Family chain reaches
its Application, but all portable `ApplicationInitialized` flags are zero.
ApplicationObject's portable getter therefore reports no application, preventing
the completion pump from collecting the reply or returning its wait signal.
The diagnostic records the actual queued reply in
`popasl-v23-native-pump-diagnostic.json`. The worker completed its real ASL call;
the failure is application ownership selection in the MUI completion path.

The v24 source fix uses the already selected native Application's actual Family
ownership chain and tests that detached popups and other applications do not
match. All 3,935 classic host tests pass without skips. A fresh native library
build and paired popup replay are queued; the CopperStart replay uses the new
396c58ee diagnostic ROM. This source fix is not yet a native completion pass.
Real selected-file/default-hook behavior, icon writes, Split/rejoin bytes and
Radio input remain unqualified.

## Continued native investigation, 2026-11-02

The popup ownership correction compiled after changing the traversal to a local
cursor: the compiler rejects `starg` for APTR, so modifying the method argument
could not produce a guest library. The successfully compiled v25 library has
1,272,984 bytes and SHA-256
`1586d966911c4755859206d68e9237d45bafcbe4919558625208a613d98b9597`.
Fresh paired runs are under `popasl-v25/`; compilation alone is not a completion
pass. The earlier v23 cancellation timeouts remain failures.

MUImem's actual displayed Maximum row now passes with the rebuilt 396c58ee
CopperStart ROM and unchanged v19 MUI library. At field 137,351 it shows
516,096 Chip, 4,587,488 Fast and 5,103,584 Total, matching the guest MemHeader
extents including the 32-byte headers. Evidence:
`mem-rom-v24-copperstart-maximum.json`. This qualifies the counter correction,
not exit behavior.

The field-150,001 cleanup capture establishes the remaining deadlock:
application task `0x42d0a0` owns layer `0x66e660` and waits for LayerInfo
`0x66d730`; Intuition refresh task `0x4f7050` owns that LayerInfo and waits for
the same layer. The actual wait records and both public semaphore owners agree.
`mem-rom-v24-lock-cycle.json` records the independently checked cycle. The
MUI user-stack candidates include PopClip/InstallClipRegion, but the ownership
records, rather than an unvalidated stack unwind, establish this diagnosis.

The native built-in paint scope now acquires LayerInfo before the layer and
releases them in reverse order. Its nested per-object BeginUpdate abstraction
continues to use a layer lock, preserving visible ClipRects; it does not call
Layers BeginUpdate for ordinary object painting. This ordering also matches the
[Intuition refresh guidance](https://developer.amigaos3.net/autodocs/intuition.library/BeginRefresh.html).
All 3,935 classic host checks pass without skips. The native wrappers use the
SDK's named LockLayerInfo/UnlockLayerInfo vectors; the v30 native build is
pending and no corrected exit pass is claimed yet. Its replay controller uses
fresh images for all four tools and runs one ROM pair at a time. MUIJoin's
preferences action remains discovery rather than a file-integrity test.

The functional icon fixture supplies two owned 16x16 one-plane classic icons,
copies them to RAM with guest Copy and drives the unchanged utility through
actual keyboard/mouse input. The v26 preflight confirms exact input bytes.
However, the original SAS/C executable detaches into
`MUI_Icon_Update_process`, so the first after-command read files before the GUI
finished. Its unchanged target is not an application write-failure verdict.
Those raw observations remain retained. The revised guest command waits until
that process has been observed running and then gone; protocol v17 and file
inspector v2 require both lifecycle observations before accepting after bytes.
Real writes remain unqualified until a new complete replay.

MUIJoin preferences discovery runs under `join-prefs-v26/` use actual Prefs,
Use and Quit controls. They are intended to identify the real chunk-size controls
before configuring a bounded RAM Split/rejoin fixture. They do not establish
split/join output correctness.

## Popup completion and classic control setters

The v25 Popasl ownership fix now passes the real requester lifecycle on both
ROMs. CopperStart completes at field 146,551 and Kickstart 3.1 at 130,051:
the native requester opens, Active is set, hardware cancellation unlinks it,
Active clears, and the main application closes with zero objects, classes,
dispatch/callback depth and owner operations after settling. Evidence is in
`popasl-v25/{copperstart,kickstart31}/result.json`. This qualifies asynchronous
file-requester cancellation/completion, not selected-file/default-hook behavior.

MUIJoin's actual preferences show Postfix `j` and Append `lha`, but Length and
Buffer are empty. Its field-55,901 Kickstart capture retains the correct raw
integer attributes (720,000 and 200,000) while its owned String contents and
integer records are empty/zero. The original executable sets String_Integer
through classic OM_SET and later parses String_Contents when Use is pressed.
The native adapter was sending that OM_SET to the raw object setter instead
of the control setter, so it never materialised decimal text or owned copies.
The shared tag parser now has explicit raw/control strategies. Native control
OM_SET/OM_UPDATE and MUIM_Set/MUIM_NoNotifySet enter the typed control setter;
constructor and direct internal raw writes retain their previous route. The
same parser preserves operation-local NoNotify handling.

Five discriminating regressions (integer formatting through OM_SET/OM_UPDATE,
owned caller text and NoNotify restoration) pass, and the complete classic host
suite passes 3,940 tests without skips. Two additional public Set-method cases
have been added and the expanded suite is running. The CopperStart preferences
navigation completes at 98,551 fields with v25, but the empty numeric fields
remain a known defect in that navigation result. Kickstart's old run unexpectedly
loads another MUI instance after Use, invalidating its cached library observation;
it is not a preferences pass. Retain that failure for the typed-setter retest.

The paint-lock native candidate v30 is 1,273,552 bytes, SHA-256
`955dad695465f51b6b2882f54073f0b172f252b18be1fb3c15b0efae4de87cdf`.
Both MUImem runs are active. Its compiler/SDK/runtime input hashes differ from
v25, and the Kickstart field-50,001 capture shows a high dispatch-depth value
while construction is still in progress. This is not a corrected exit pass or
a proven compiler regression. Subsequent native composition v32 uses the
previously verified v25 compiler/SDK/runtime/builder binaries and the updated
MUI source assembly; `mui-small-tools-frozen-v32/frozen-input-receipt.json`
verifies every retained dependency hash. No earlier frozen directory is changed.
The v30 child MUImem runs continue; its controller was retired before launching
the other tools. The unstarted v31 controller/composition were superseded by v32.

The owned Split/rejoin fixture now contains a 12,345-byte deterministic input
(SHA-256 `c2349f4afe0adcc26724c9a8101ec50fd4b17cf511cac103aa7e8a1ccd9a6093`),
four expected parts of 4,096/4,096/4,096/57 bytes and a joined output. Its five-line
preferences format is established by the unchanged guide and executable's fscanf
call at file offset 0xFCA. Actual guest DOS readers and a separate byte verifier
are implemented; both command bodies decode as valid 68000 instructions. This
is prepared coverage, not a Split/rejoin pass. Fresh requester-control discovery
runs under `picker-controls-v31/` wait for real StringInfo controls before cancel.

## Completed paint-lock replay and functional file protocol

The v30 paint-lock fix now passes MUImem open/observe/close/cleanup on both ROMs:
CopperStart completes at 192,501 fields and Kickstart at 168,501. Both finish
with zero objects, classes, dispatch/callback depth and owner operations, followed
by the settling period and native window unlink. The static table-label geometry,
the Maximum counters against actual Exec regions and the original executable's
previously damaged helper all pass independently on both observed captures.
Evidence: `paint-v30/mui-mem-11/` and `mem-paint-v30-*-{layout,maximum,code-guard}.json`.
The high intermediate Kickstart depth at field 50,001 later unwinds normally;
it is not evidence of a depth leak or compiler regression.

The frozen-dependency v32 setter library composes successfully: 1,359,004 bytes,
SHA-256 `be30c9544f50711b64ee035632059fba67e9fe5d0b56c4e78d78dc33e0b9b74c`.
Its actual Kickstart preferences now show `720000` Length and `200000` Buffer,
confirming that classic OM_SET reaches the owned String conversion. The later
Use action still replaces the observed MUI instance on Kickstart, so that run
remains a failure for full preferences navigation. The controller was retired
before launching its subsequent tools; child preferences runs remain evidence.

Icon Update's first functional Kickstart run accepts both typed RAM paths and
the Start button, but times out at field 259,101 without a result dialog. The
unchanged original source sets `show_results = TRUE` and requests a dialog for
both success and error. The native requester presenter constructed Window.mui
without RootObject and tried to attach it afterward, contradicting the native
constructor's mandatory root contract. Current source now supplies the root in
OM_NEW, then confirms its ownership without attempting a runtime replacement.
A regression constructs and disposes the real headless Window/root relation
through that tag plan and checks the native constructor guard. All 3,943 classic
host tests pass without skips. Native requester confirmation remains pending.

The window-close investigation also found that the event pump dispatches the
original IntuiMessage to callbacks and replies afterward, while a callback can
synchronously close its own window. Classic [CloseWindow documentation](https://developer.amigaos3.net/autodocs/intuition.library/CloseWindow.html)
states that closing deallocates the window's IDCMP messages. This is a concrete
lifetime violation to address in the Kickstart preferences investigation;
the resulting native fix has not yet been implemented or verified.

The old CopperStart icon fixture faults at PC 0x10, field 1,842, before the
utility loads. Its failed Copy-based preparation is retained separately and
was stopped after the fault; it is not an Icon Update verdict. A bounded guest
DOS read/write helper now prepares the owned inputs. Setup-only probes validate
the exact icon and 12,345-byte file input on both ROMs (`fixture-setup-v32-pass.json`).
The generic probes end at MAX_FIELDS while parked in real Exec.Wait; their
separate before-file byte witnesses establish setup success, not utility output.

The Split/rejoin fixture's v2 protocol additionally creates an owned
`RAM:muijoin` directory. Both ROMs independently pass CreateDir/copy/readback
(`join-dir-setup-v34-pass.json`). Versioned witness cookies retain correct
interpretation of the earlier RAM-root fixture. The functional v20 harness uses
the real ASL Drawer/File string gadgets, types with ordinary raw keys, selects
the four part rows with mouse/Shift input, accepts the requester, quits and waits
for actual guest DOS snapshots. Its row geometry is bounded to the observed
ASL 40.6, 8-pixel-font diagnostic profile, with captures requiring visual review.
The independent verifier must match the source, all four parts and the joined
file byte-for-byte. Fresh paired runs under `join-write-v34/` use a separately
recorded 500,000-field functional limit. They are running, not yet file passes.

Harness v19 and later stop after decisive vector-table/out-of-24-bit PC faults
and preserve the first capture. This prevents continuing a corrupt preflight.
No ROM, original executable, guest RAM or CPU register is patched by the host.

## Owned input delivery and further file-operation results

The input lifetime fix is now implemented in `MuiNativeIntuiMessageDelivery.cs`.
Normal IDCMP messages are copied into owned guest memory and replied before
callbacks run. RAWKEY delivery also owns its four bytes of dead-key history.
Opaque provider payloads keep the existing borrowed-message path; this is not
an assertion of arbitrary IDCMPUPDATE or tablet support. Five new regressions
cover close-time reclamation, raw-key history, extended envelopes and rejected
unmapped history. All 3,948 classic MUI tests pass with no skips. The focused
delivery/requester run passes all eight tests. Logs are
`../CopperOS/artifacts/mui-small-tools-delivery-v36-{focused,full}.log`.

The v36 native candidate is compiled directly against the frozen v25 SDK and
composed with its unchanged compiler, runtime and builder inputs. The artifact
project and hash receipt remain under ignored build outputs; production project
references are unchanged. Native replay confirmation is still pending.

The v33 CopperStart preferences test completes Use, Quit and cleanup at field
100,251. Kickstart still fails with that older v32 library at its 240,000-field
bound after Use. These are separate results, not a paired pass.

The functional v34 CopperStart Split run faults at PC 0x10 after accepting the
correct `ram:muijoin` drawer and `muifix` filename (field 82,019). It is not a
successful split or rejoin. The Kickstart run has returned from Split and its
native requester visibly lists the four parts at the expected lengths. Rejoin
and independent output-byte verification remain pending. All raw failed runs
are retained under `artifacts/mui-small-utilities-fix-2026-11-01/`.

## Original launch-stack correction

The v37 read-only watch identifies the first damaging write, before the final
invalid-PC cascade. At field 44,573, native graphics instruction `0xFFBE74`
(`MOVE.L 0x3E(A3),0x4C(A4)`) writes at `0x51808C`, overlapping the high word of
MUIJoin's DOS base at `0x51808E`. SP/A4 is `0x518040`, below the window owner's
Task lower bound `0x518200`. The DOS base changes from `0x004249D8` to
`0x000049D8`; a later graphics write changes it again. The later CloseLibrary
therefore tries to invoke a vector through a damaged base. This is observed
stack/data overlap, not a CPU instruction-semantics diagnosis.

The launcher missed the original icon's stack request: MUIJoin's unchanged
DiskObject specifies 8,192 bytes, while the original diagnostic launch has only
4,108 bytes between its public Task bounds. MUImem also ships an 8,192-byte
request; MUIMenu requests 4,096 and Icon Update requests 8,192 (its source also
starts its detached program with `__stack = 20000`). The undersized launches
remain raw failed evidence; they are not final verdicts for the authored launch.

Image protocol v2 adds `--respect-icon-stack`. It verifies the original icon,
reads its StackSize and runs a tiny genuine guest command before the unchanged
utility. That command uses FindTask and public Process/CLI structures to set
`cli_DefaultStack` in longwords. It changes neither the current Task's stack nor
the executable, and performs no host RAM/register injection. A separate public
Window.UserPort/MsgPort.SignalTask inspector checks the following application's
Task bounds at a quiescent capture. Those bounds remain advisory for programs
such as MUImem that manually change SP.

The inspector rejects the old 4,108-byte launch against 8,192 and confirms the
separate Kickstart Stack-command control has 32,768 bytes. The original Stack
command itself faults in CopperStart before starting the utility (v38, field
2,088); that control is not a utility verdict. The larger v39 control was
superseded by the exact icon-sized v40 input before obtaining a verdict.

Fresh v40 paired preferences, Split/rejoin, memory, menu and icon runs are
queued/running with the same v36 MUI binary and frozen hardware/ROM inputs.
Native task-bound checks, file witnesses and cleanup remain required. The v36
message fix alone did not resolve Kickstart preferences: the old library was
freed and replaced after Use. That run was stopped after capturing the decisive
replacement; it is not a pass. CopperStart v36 preferences do pass at 99,251
fields. The icon-sized Kickstart replay must establish whether the remaining
preferences problem has the same stack cause.

Both v40 preferences launches now independently meet the icon request:
CopperStart has 8,204 bytes of public Task bounds and Kickstart exactly 8,192.
CopperStart completes Use/Quit/cleanup at field 99,401. Kickstart still replaces
the MUI instance after Use with that corrected stack. The separately measured
32,768-byte Kickstart control also fails in the same way. Thus the remaining
Kickstart Use failure is not resolved by correcting the application stack.
Evidence is `author-stack-v40-prefs-*-bounds.json`,
`author-stack-v40-kickstart31-prefs-replacement.json` and the retained cancellation
receipts. A fresh scalar trace records the original program's Application Input
return IDs, preference getters and parse returns, stopping at master-library
Close before destruction can overwrite the evidence. This is still a diagnostic,
not a successful preferences replay.

## Authored-stack functional results and stronger checks (2026-11-02)

With the original icon stack requests, frozen v36 MUI and v24 CopperStart ROM,
the v40 native functional replays complete on both ROMs:

| Utility / operation | CopperStart fields | Kickstart 3.1 fields | Independent evidence |
| --- | ---: | ---: | --- |
| MUImem observe and close | 203,001 | 186,101 | Layout, maximum counters, original helper bytes and task-bound checks |
| MUIJoin Split and rejoin | 178,451 | 175,151 | Actual DOS output: 4,096/4,096/4,096/57-byte parts and 12,345-byte joined file |
| MUIMenu configured command | 103,651 | 95,601 | Exactly one execution recorded in the actual registered Exec resource |

These runs end with zero objects/classes/callback depth/active operations,
native window unlinking and a further 5,000 settled fields. Output proofs are
`join-author-stack-v40-*-registered-bytes.json` and
`menu-author-stack-v40-*-registered-witness.json` under
`artifacts/mui-small-utilities-fix-2026-11-01/`. Join source bytes are unchanged.
The periodic fixture's first three parts are identical; this does not establish
exhaustive part-order coverage. Kickstart MUImem's quiescent capture observes
the temporary 128 KiB MUI stack, so its bound check is not a measurement of the
original CLI launch stack.

File/command inspectors now walk the public Exec ResourceList with backlink
and sentinel checks. A matching cookie in an unregistered copied allocation is
not accepted. Existing positive Join/Menu evidence still passes this stricter
check. Icon Update's old after-reader expired while its detached GUI was alive;
those v36 runs are retired as invalid fixture coverage, not utility failures.
The guest reader now allows 1,000,000 Delay(1) polls, beyond the 500,000-field
functional bound, and records its remaining budget and timeout separately.
Actual icon-write qualification remains pending.

## Kickstart preferences alert and String getter ownership

The earlier “MUI replacement after Use” interpretation is superseded by the
native v42/v45 traces. The old v36 library enters Exec.Alert with D7
`0x81000005` at field 70,473. The subsequent ROM RESET unmaps Fast RAM before a
fresh boot loads another library. This is a reboot, not an ordinary library
close. The later privilege exception inside the ROM alert handler is not the
initial failure or evidence of a CPU semantics defect.

The retained mouse-up IntuiMessage has Class=MouseButtons, Code=0xE8 and a
nonzero IAddress equal to the preferences window. IAddress is undefined for
this message class. The initial ownership fix mistakenly borrowed any envelope
with a nonzero IAddress; callbacks could then close its owning window before
the message was replied. The v44 fix owns classic envelope-only messages
regardless of these undefined bits, without following them as pointers.
Opaque gadget/update payloads and extended tablet messages remain borrowed.
All 3,965 MUI regressions pass, with no skips. Fresh v46 controls using v44 get
past Use on both ROMs; full final qualification is separate from this control.

A second, independent bug is proven by reading the unchanged original MUIJoin
globals. Before Use, its suffixes are `.j` and `.out`; afterwards both suffixes
are empty. This also affects the earlier CopperStart preferences GUI/cleanup
PASS, which therefore is **not a semantic preferences PASS**. Numbers remain
4,096/4,096/0. The new read-only
`inspect_mui_join_preferences.py` locates the pinned original program and its
DOS-relocated A4 data, and rejects both v46 after-Use captures as
`FAIL_PREFERENCES_CHANGED`. It likewise rejects the old v40 CopperStart capture.

The built-in class correctly returns String's owned text, but the subsequent
native facade projection overwrites it with the raw initializer pointer. The
original program clears its destination buffer before GetAttr, so that stale
pointer now points to an empty string. Typed String.Integer setters can also
synthesize text absent from the raw facade tags. The v47 fix preserves
successful String.Contents/String.Integer class getters, including forwarded
Popstring text. Failed getters retain facade fallback; Window.Window still
applies deferred IDCMP, and Window.Open retains its existing handle-based rule.
All 3,971 MUI regressions pass without skips, including caller-buffer clearing
and integer-to-owned-text coverage. The frozen-SDK native build succeeds with
zero warnings/errors; native library composition and paired semantic replays
are pending. GUI-only v46 controllers were retired before launching further
qualification through their insufficient preference gate; raw runs are retained.

The v47 native library composes successfully (1,361,380 bytes; 22,369
relocations). Fresh v48 after-Use captures independently preserve `.j`, `.out`,
4,096-byte chunks/buffers and zero MaxMemory on both ROMs:
`candidate-v48-prefs-*-early-values.json`. The same verifier accepts the old
before-Use control and rejects the old after-Use controls, so this is a
before/after semantic correction rather than a weakened GUI-only criterion.
Native Quit/cleanup then passes at 99,401 CopperStart fields and 90,551 Kickstart
fields. Both have zero objects/classes/depth/active operations, native window
unlinking and 5,000 further settled fields. The stronger combined gate is
`candidate-v48-prefs-proof.json`; the independent collector repeats the value
inspection in `candidate-v48-prefs-*-values-independent.json`. This establishes
both preferences corrections for this bounded original-client workflow.

## V48 final getter/message candidate matrix

Library SHA-256 is `3a89edbbb3e5e1eeb7556e9e3bd61941adf093c61c9b6b86eb1d4a9e187d1906`.
The independent collector is `candidate-v48-summary.json`. Historical results
are retained under their original binary and input identities.

| Operation | CopperStart | Kickstart 3.1 | Additional proof |
| --- | ---: | ---: | --- |
| MUIJoin preferences Use/Quit | PASS 99,401 | PASS 90,551 | Original suffix/numeric globals preserved |
| MUImem observe/close | PASS 208,951 | PASS 186,101 | Labels, maximum counters, original code and stack minimum |
| MUIJoin Split/rejoin | PASS 178,451 | PASS 175,151 | Actual source/parts/joined DOS bytes |
| MUIMenu execute/Quit | PASS 103,651 | PASS 95,601 | Exactly one registered command execution |
| Icon Update actual update | FAIL 352,106 | FAIL 265,751 | CopperStart unsupported icon write; Kickstart result dialog timeout |

All passing rows additionally require empty MUI registries/depth/operations,
native window unlinking and 5,000 settled fields. Rejoin selections were visually
reviewed on both ROMs: four parts selected, original source excluded
(`candidate-v48-visual-review.json`). The same periodic-fixture and temporary
MUImem-stack limitations above still apply.

## Application requester service fix and native probe

The requester presenter passed the eight-byte `MUIR` formatting-service record
to class construction and disposal, whose lease state requires the distinct
`MUI9` record. Consequently its first button construction returned NULL even
after the earlier Window.RootObject correction. `MuiNativeRequesterPlatform`
now resolves the owner's class service through its named codec, validates that
private record's magic and uses it for requester construction/disposal. Host
tests exercise the real attached owner, distinct embedded records and rejection
of an incorrect private magic. All 3,973 classic MUI tests pass with zero skips.

The v49 MUI library is 1,361,540 bytes, SHA-256
`5065a4ea443cb6f12ec6267e07cee7e03cba4c19bad80c41634288b59338be30`.
Standalone genuine guest `MUI_RequestA` probes display the actual text, receive
ordinary hardware OK clicks, return to the guest caller, dispose the application
and pass full cleanup/settle on both ROMs: 21,001 CopperStart fields and 17,051
Kickstart fields. Registered resource oracles confirm the caller completed its
request/disposal path (`candidate-v50-requester-*-resource.json`). A single OK
button correctly returns zero; a returned zero alone is not presentation proof.
Original Icon Update must still complete its own actual-file workflow.

Harness v27 explicitly records 120,000-field icon result-dialog/close waits and
uses up to 750,000 fields for the affected replay. Earlier v20 evidence is
unchanged. These are separately recorded bounded correctness budgets; no
performance protocol or expected fingerprint has been weakened.

## Disk-loaded classic icon writer

The CopperStart v48 failure enters the unsupported `icon.library` write vector
and reaches invalid PC `0x10`. Its live A6 is the icon library at `0x49121A`;
this is distinct from the earlier undersized-stack DOS-base corruption.
The provider is installed by the disk-loaded CopperShell guest program, rather
than the ROM. Its `StoreDiskObject` was false/unimplemented and the public write
slot was replaced by an unsupported instruction. The ROM input remains pinned.

The native guest writer serializes classic V1 headers, normal/selected images,
strings, tool types and V36 drawer extension data. It accepts a caller-owned
78-byte synthetic DiskObject without requiring a private adjacent FreeList.
It writes `name + ".info"` through public DOS calls, handles partial writes,
closes the file and preserves failures in the BOOL/IoErr result, as specified
by the [PutDiskObject autodoc](https://developer.amigaos3.net/autodocs/icon.library/PutDiskObject.html).
All 17 focused encoder/store tests pass, including odd packed strings,
allocation rollback, open/write/flush failures and unchanged caller data.

An optional isolated provider is linked into a 5,380-byte HUNK, SHA-256
`75d604e359fe4e34923c4184585f6b4c6b0ff3eeaf3993c8fe668144f8671d3a`.
Its ordinary guest loader uses LoadSeg and SetFunction and retains the segment
in its named Exec resource. It neither generates nor copies executable code at
runtime. This avoids rebuilding the concurrently changing Shell and leaves its
frozen `L:CopperShell` bytes, the ROM and other guest files unchanged. It is
guest-loaded code under normal loader ownership, not evidence of ROM residency.
There are no host OS callbacks or host RAM/register patches.

V53 private API/utility images verify every retained guest file after adding
the provider, loader and optional public-API byte probe. The API probe compares
actual DOS reads of the source and rewritten target, the retained segment,
patched vector and actual writer call count. It gates the original utility
replay. V53's native API oracle now passes: both files are exactly 130 bytes,
the source is unchanged, the target contains the expected source image/type and
retained target position/stack, the actual public vector points at the retained
module and its call count/result are exactly one/success. IoErr is zero.
Evidence is `candidate-v53-api-copperstart-bytes.json`. No MUI application is
launched in this API probe; its raw viewer reaches MAX_FIELDS, not a GUI PASS.
The dedicated registered-resource byte oracle provides the separate native API
verdict. The original CopperStart Icon Update replay has started after that
gate; Kickstart's v50 original replay is also active. Workbench change
notification and newer icon formats remain outside this classic-file scope.

## Reference-window coverage and driver layout correction

V54 repeats the standalone request with a genuinely opened reference window on
both ROMs. Public GetAttr confirms a nonzero native reference handle before
the call. Hardware OK, return to caller, disposal and cleanup/settle pass at
23,301 CopperStart fields and 18,701 Kickstart fields, with independent
`--require-reference-window` resource proofs. The CopperStart variant also
loads the retained icon writer. The guest provider project builds with zero
warnings/errors separately from its frozen-SDK native build.

V48 CopperStart forensics additionally confirms the exact unsupported slot:
`icon.library -84` jumps to `0x491452`, whose first four bytes are
`4AFC FFAC` (ILLEGAL followed by the write LVO). The actual arguments are
`ram:dst` and a valid `E3100001` DiskObject. This is not a new CPU defect or
another corrupted DOS base (`candidate-v48-copperstart-unsupported-icon-vector.json`).

V50's original Kickstart run now creates the expected success requester with
the fixed library, but exposed a driver race. At field 223,951 its OK object's
Width and Height were still zero; the old driver selected `(0,0)`. By field
250,001 layout had produced `(Left=4, Top=94, Width=312, Height=10)`, whose
actual center is `(160,99)`. That invalid click is unavailable workflow
coverage, not a library failure or GUI PASS. The affected runs were retired
with `icon-layout-fixture-retirement-v55.json`; their evidence remains intact.

Frozen harness v30 waits for a nonempty hit rectangle before selecting OK and
waits for disposal of the modal Window object as well as native unlinking and
quiescence before closing the main window. V56 restarts both unchanged
original Icon Update workflows with the same v49 library and guest images.
The independent final-file and CopperStart retained-writer ownership oracles
remain mandatory; the completed requester/API probes do not replace these
original-utility results.

The current CopperStart Release solution build completes with zero errors and
12 warnings (`../CopperStart/artifacts/utility-icon-writer-v56-production-build.log`).
The separate guest-provider project build has zero errors/warnings. Neither
build replaces the frozen native replay inputs or constitutes a performance
acceptance measurement.

## Completed v56 original Icon Update A/B

| ROM | Fields | Hardware input and cleanup | Independent file proof |
| --- | ---: | --- | --- |
| CopperStart | 409,451 | PASS | PASS |
| Kickstart 3.1 | 247,801 | PASS | PASS |

Evidence: `artifacts/mui-small-utilities-fix-2026-11-01/candidate-v56-icon-summary.json`.
The independent registered-resource checks read actual guest DOS file snapshots
before and after the original application exits. The source remains unchanged;
the target receives the source image/type while retaining its position and stack
metadata. They also require the original detached task to have appeared and exited.
CopperStart additionally verifies the live public icon vector, retained guest
module and actual writer call/result. Both runs require empty MUI registries,
native window unlinking and 5,000 further settled fields after quitting.

Visual review is recorded in `candidate-v56-visual-review.json`: CopperStart's
later progress capture visibly shows the successful-update requester, and both
final captures show restored underlying screens. The early Kickstart dialog
capture precedes completed repaint and is not used as visual success proof.

These are bounded functional checks. Exhaustive utility options, full MUI
conformance, nonperiodic/reordered join fixtures, Workbench refresh notification,
newer icon formats and performance acceptance remain outside this result.

## Commit integration checks — 2026-11-02

The scoped changes were applied to isolated checkouts of the remote main
branches, preserving concurrent source work. CopperOS passes 3,973 MUI tests
in each of its classic and default profiles, plus 26 library-builder tests,
with zero skips. CopperStart passes all 30 focused icon/AvailMem/stack/context
tests with zero skips. Its Release solution builds with zero errors and
13 warnings on the newer integration base. The native interaction harness
builds with zero warnings/errors against the retained engine dependencies.
These source-integration checks are separate from the frozen native A/B
replays above; no production ROM qualification is inferred from them.
