# Native MUI Window and Calc verification — 2026-11-01

Candidate **v48** passes the bounded original MUI Calc mouse workflow on
CopperStart and native Kickstart **3.1**, using the separate 5 MiB correctness
profile. Both visibly compute `5 Enter 6 Enter +` as `11`, close through the native
gadget, and finish with zero MUI objects/classes and dispatch/callback depth.
Both host profiles pass 3,902 tests with zero skipped. The classic NewMenu fix,
native rendering, physical input, List array insertion and cleanup are included.

The [reviewed final receipt](UTILITY_MUI_CALC_NATIVE_2026-11-01.json) pins inputs,
results and reviewed captures. CopperStart retains old pixels on its bare screen
after the process exits; desktop restoration, paging, menus, keyboard and full
MUI compatibility remain unqualified. The retained 3 MiB load failure remains
FAIL. Versioned milestones below preserve their original, narrower claims.

## Cleanup change



The String AttachedList and Listview external-scroller disconnect walks already

hold each candidate's headless object/class record. They now classify that record

directly instead of searching the complete object registry for every candidate.

Two host tests measure mapping checks while doubling an unrelated control tree

and enforce growth below threefold for these individual walks. This is a guest

algorithm check, not a host throughput measurement or full-cleanup proof.



The isolated cleanup-only v35 library is 1,026,276 bytes, SHA-256

`28d1d7b02cf5896f6ee56e66abc2c767861bdef7c3d096453d186082c94f2f82`.

It retains the v34 native lifetime behavior. Both private original-client images

retain all seven original package files. Replays complete the same

100,000 native Kickstart / 110,000 CopperStart fields as v34, with the same ROMs

and machine. They finish without unsupported-hardware conditions, but retain

47 native Kickstart / 44 CopperStart objects, compared with 58 / 55 in v34.

This optimization improves progress but does not complete cleanup.

Logs: `artifacts/utility-mui-calc-kick31-v35.log` and

`artifacts/utility-mui-calc-copperstart-v35.log`; captures are under

`artifacts/utility-mui-2026-10-31/mui-calc-{kick31,copperstart}-v35/`.



## Native window foundation



The v36 foundation calls real Intuition OpenWindow/CloseWindow through the

existing pinned provider. NewWindow is serialized by the SDK codec. Successful

opens publish one native handle to the typed Window lifecycle and the public

Application event registry. Failure restores closed state; repeated requests

are idempotent. Native destruction clears the cached handle before the provider

call, including Family-owned windows whose headless records have been removed.



The foundation still needs integration with root layout,

rendering and input/notification dispatch. Its initial default rectangle is

provisional. It is not a rendered calculator, an arithmetic pass, or a completed

native window implementation.



Classic host validation passes 3,880 tests, zero skipped, including three native

window lifecycle cases and the two cleanup-walk cases. The successful default

output-path log is `artifacts/utility-mui-native-window-tests-v36c.log`.

The isolated-output run `v36` encountered the existing source gate's fixed

repository-root assumption; moving isolated output under CopperOS (`v36b`)

passes all 3,880 tests. These are separate retained results.

The default development profile also passes all 3,880 tests, zero skipped,

in `artifacts/utility-mui-native-window-tests-default-v36.log`.



The native compiler completes successfully; its log is

`artifacts/utility-mui-native-window-build-v36b.log`. An earlier isolated-output

builder attempt (`v36.log`) failed because its local SDK-support restore assets

were absent. No native success is inferred from host tests.



MMC3 extends the existing MMC2 probe with two real Window_Open/Window_Window

open/close cycles. It checks non-null native handles, zero closed handles,

caller-owned NewMenu preservation and balanced available memory across cycles.

Its command is 1,196 bytes, SHA-256

`5c28279d238264511eab9c5c0e45aa7405ca3b9951500fea964e8323273428e9`.

The original MMC2 command remains unchanged. MMC3 passes under both ROMs:



| Result | Kickstart 3.1 | CopperStart |

| --- | --- | --- |

| Completed fields | 35,000 | 45,000 |

| Available memory after cycles 1 / 2 | 2,001,872 / 2,001,872 | 1,048,468 / 1,048,468 |

| Final objects / classes / callback depth | 0 / 0 / 0 | 0 / 0 / 0 |



The [receipt](UTILITY_MUI_NATIVE_WINDOW_2026-11-01.json) pins the v36 library,

private images, final captures and native handles. It proves bounded real

open/close and repeated cleanup, not visual presentation or cold-start memory

balance before Intuition's first window.



## Rendering and deeper cleanup candidate



A v35 guest stack capture resolves into Application cleanup's scheduler getters.

The next change reads existing raw ownership records during destruction instead

of publishing new scheduler and Window interaction stores for every control.

It preserves structural queue admission and releases owned cycle-chain nodes

even after their child targets are gone. Host tests check both that lifetime

case and zero allocations from scheduler cleanup.



The v38 candidate also connects the built-in entry to native Application/Notify

dispatch, retains actual Window_Open projection, and uses the current owner's

Graphics/Intuition providers for bitmap drawing and font metrics. Nested group

setup/layout/drawing and cleanup use the existing control cores. A host test

draws a copied Text label in a nested group and verifies that cleanup clears its

render port. Native font-role aliases use the screen's real font.



Both profiles pass 3,883 host tests, zero skipped. The classic log is

`artifacts/utility-mui-native-window-tests-v38.log`, and the default log is

`artifacts/utility-mui-native-window-tests-default-v38c.log`.

The isolated native compiler completes: the v38 library is 1,215,876 bytes,

SHA-256 `c70bf1addcb406cf9d8b50b704daf082dda47ce43bcc9e955c98b71aea0ba93d`.

See `artifacts/utility-mui-native-window-build-v38c.log`. Both packager profiles

also pass 22 tests, zero skipped, in `utility-mui-native-window-packager-classic-v38.log`

and `utility-mui-native-window-packager-default-v38.log` under `artifacts/`.

Earlier attempts retain

the missing caller-provided library annotation, unsupported mutable APTR

argument, and shared compiler output-lock failures. Those issues were corrected

before this candidate. Native rendering and input remain unverified. Automatic

control input registration still needs auditing against the native Window pump.



The original-client v38 replays are running for 100,000 Kickstart / 110,000

CopperStart fields, under `mui-calc-{kick31,copperstart}-v38/`. The native Text

window probe adds owned Text roots, holds the real windows for 1,000 fields per

cycle, and captures every 500 fields. Those runs are

`mui-native-window-text-probe-{kick31,copperstart}-v38/`, bounded to 30,000 /

35,000 fields. MMC4's 1,484-byte command has SHA-256

`ae6c4c3d31f350777ebc142bc645bda821656cb612b535f40f163fdb7f2782ec`.

These tests do not establish native rendering or complete full-client cleanup.



## v38 counterevidence and next candidate



Both MMC4 runs complete but fail qualification. Native Kickstart loads the

library and creates the underlying Intuition window, then returns Open=0 /

Window=NULL after presentation setup fails. A scalar observer stops immediately

after Present returns false (PC `0x2422ec`, D0=0, field 7,982). Its trace resolves

the failure to AreaTextColor.Setup calling ResolveMuiTextColor: the native

adapter returned false rather than a successful unavailable RGB override.

Artifacts are `mui-native-window-present-stop-v38/` and the corresponding log.



CopperStart's v38 MMC4 library-open observation is NULL. Its failed-run Fast

MemHeader reports 1,077,284 free bytes, below the 1,137,168-byte compiled code

body (and the complete HUNK file is larger). Native Kickstart loads this same

file. These are additional guest software RAM requirements, not an emulator

throughput result. No RAM setting or ROM pin was changed to turn this failure

into a pass.



The opt-in `--preload-mui` / `mui-before-gui-v1` allocation-order experiment

opens/closes the library before CopperGui without calling object services. Its

separate CopperStart run reaches an invalid guest PC `0x43f32835`; it is failed

diagnostic evidence and is not adopted as a working startup path. Standard

images retain the original GUI-before-MUI order.



The v39 source corrects the color-resolution contract, avoids importing event

handlers for unregistered Scrollgroup/Numeric/Radio/Scrollbar/Gadget classes,

and reuses the shared handlers for the native Text/String/Cycle/Image controls.

It paints Listview frames and lets the owned List child render once. Ordinary

per-object painting retains visible clip rectangles rather than clearing Layer

damage between a Group and its children. Closing a partially prepared tree no

longer constructs runtime stores for untouched children.



The classic suite passes 3,885 tests, zero skipped, in

`artifacts/utility-mui-native-window-tests-v39c.log`. The default suite also

passes 3,885, zero skipped, in `utility-mui-native-window-tests-default-v39.log`.

The v39 compilation completed (1,189,004 bytes). Its separate 5 MiB MMC4

runs passed window lifetime and repeated-cycle memory checks, but produced

blank content. They are retained as rendering failures in

`artifacts/utility-mui-native-window-text-result-v39-5m.json`.



## Separate 5 MiB resource diagnostic



The `a500-ocs-5m-diagnostic-v1` profile uses the same pinned ROMs and interpreter,

512 KiB Chip RAM, 512 KiB Slow RAM, and 4 MiB Fast RAM. It is a resource

diagnostic, not a replacement for the earlier 3 MiB profile or a product preset.

CopperStart maps this Fast RAM at `0x400000`; Kickstart maps it at `0x200000`.

Capture inspectors now read each dump's recorded mapping and validate its size.

The earlier 3 MiB library-load failures remain failures.



The v40 adapter resets the foreground pen after background painting and maps

absent decorative pens to valid indexed-screen pens. MMC4 completes 30,000

Kickstart and 35,000 CopperStart fields. Both repeat complete open/close cycles

with identical available memory between cycles; the scalar receipt is

`artifacts/utility-mui-native-window-text-result-v40-5m.json`.

Visual inspection confirms `Native MUI` inside the real window on both ROMs:

`mui-native-window-text-probe-kick31-v40-5m/frame-010500.bmp` and

`mui-native-window-text-probe-copperstart-v40-5m/frame-010000.bmp`, under the

dated artifact directory. An earlier Kickstart capture at field 9,000 precedes

completion of drawing and is blank; it is not the final visual result.



The current v41 library is 1,191,864 bytes, 19,246 relocations, SHA-256

`26db9d081d5d3b196e5513d4db7504588ca5e09f50826ea19e7c516bc74ccf4a`.

Both host profiles pass 3,893 tests, zero skipped, in the v41 test logs.

It adds bounded Text/Image pointer capture, hidden/disabled ancestor checks,

generation checks against stale captures, cancellation, and avoids executing

CallHook twice through built-in and external fallback projection.

MMC5 exercises real BOOPSI Window.HandleEvent packets and a native notification

hook; it expects one hook call per accepted release and none on an outside

release. This is synthetic guest message coverage, not physical CIA mouse input.

Both MMC5 and unchanged full MUI Calc startup replays are running with v41 in

the separate 5 MiB profile. Full calculator layout, arithmetic, physical input

and normal-quit cleanup are not yet qualified. The objective remains active.



The first MMC5 attempt used the IAddress slot for MouseX and is an invalid

input-probe attempt. The corrected command uses IntuiMessage MouseX/Y at

offsets 32/34, records all four event results per cycle, and has SHA-256

`2567e5722ae85b8f1ff86db28c66749d732d0dc2d689b7b8045405817a59e897`.

Its initial 20,000 / 22,000-field captures are incomplete, not passes. At

12,000 Kickstart fields, the first complete sequence records four handled

events and one hook invocation. Longer, otherwise identical 40,000 / 45,000

field replays are under `mui-native-window-pointer-probe-*-v41c-5m/`.



At field 30,000 both longer MMC5 captures contain the completed two-cycle

marker and pass all scalar checks. Each cycle handles all four events; the

cumulative callback counts are 1 then 2. Available memory is

3,947,728 / 3,947,728 bytes on Kickstart and 2,994,324 / 2,994,324 on CopperStart.

Both registries contain zero objects/classes and callback/dispatch depth is

zero. The progress receipt is

`artifacts/utility-mui-native-window-pointer-result-v41c-progress.json`;

final-bound captures will be checked separately. These results qualify the

bounded message/notification/lifetime path only.



The final MMC5 bounds (40,000 / 45,000 fields) also pass, with the same memory

and callback observations and empty object/class registries. Receipts are

`artifacts/utility-mui-native-window-pointer-result-v41c-5m.json` and

`artifacts/utility-mui-native-window-pointer-state-v41c-5m.jsonl`.



The v41 original-client runs complete their 100,000 / 110,000-field bounds

without emulator unsupported features, but have not finished native control

preparation. Titled windows alone are not calculator rendering passes. Final

state is retained in `artifacts/utility-mui-native-window-calc-state-v41-5m.jsonl`.

The v42 source narrows object-list identity traversal to Next/Boopsi while

retaining full 64-byte record admission, null handling and traversal limits.

It adds no cache or host callback. Both host profiles pass 3,894 tests, zero

skipped, including a truncated-record admission regression. Native compilation

is running; no native v42 result is claimed yet.



The v42 compilation subsequently completes: 1,192,000 bytes, 19,249

relocations, SHA-256

`34cd2ca1ae2851d2365a3ecd1fe6b7ead52185ffd42b4626f63ae5af8e78a51f`.

Its full-client runs use a separate 220,000-field bound and recorded mouse

input (`artifacts/utility-mui-native-window-close-v42.json`) to attempt normal

close-gadget quit after startup. Results are pending; this is neither an

arithmetic pass nor proof that the attempted input reaches the gadget.



The initial v42 calculator pair was interrupted before close input because

pointer placement at field 10,000 preceded CopperStart public-screen creation.

Those captures are unqualified. The v42b pair uses identical images and a

separate recorded script (`utility-mui-native-window-close-v42b.json`) that

first moves to the screen origin at field 140,000, then positions the pointer

and attempts clicks at fields 150,000 and 171,000. The second horizontal

placement covers the two native pointer scaling possibilities; actual gadget

delivery must still be established from captures. Both runs retain the

220,000-field bound and the explicit 5 MiB resource diagnostic profile.



The final v42 MMC5 captures (30,000 Kickstart / 35,000 CopperStart fields)

pass all checks. Available memory is 3,947,608 / 3,947,608 bytes on Kickstart

and 2,994,196 / 2,994,196 on CopperStart; callback counts are 1 then 2, all

four events per cycle are handled, and final object/class counts and both

dispatch depths are zero. Receipts are

`artifacts/utility-mui-native-window-pointer-result-v42-5m.json` and

`artifacts/utility-mui-native-window-pointer-state-v42-5m.jsonl`.

The full v42b calculator pair remains running. This completed bounded probe

does not qualify calculator arithmetic, physical input delivery or its normal

quit path.



## Adaptive physical-input diagnostic



The [native interaction probe](../scripts/probes/MuiCalcNativeInteraction/README.md)

builds separately and references the existing Release runner's three binary

inputs, avoiding an engine rebuild from unrelated moving worktree sources.

Its engine SHA-256 is

`e0c814475caf09c50024762067fcf76071ec9bad5a4aa390f801efe9c545c4e2`,

matching the native runner copy; the CPU SHA-256 is

`ff3a73b2dd2a62329be1fcdad1eab8ca3a62d95f73bcfc03a4b87bd25955837a`.

The probe build passes with zero warnings/errors in

`artifacts/utility-mui-calc-native-interaction-build-v1d.log`.



The `mui-calc-native-mouse-v1` protocol waits for a real open Window and zero

dispatch/callback depth, reads visible button geometry from guest records,

and drives ordinary hardware mouse input using the native Window's reported

pointer coordinates. It captures each `5 ENTER 6 ENTER +` step, then attempts

normal close and waits for empty native registries. It makes no guest-memory

writes or synthetic guest-method calls. Accepted clicks never imply an

arithmetic pass: the result bitmap and final CLI still need visual review.



A reader check against the known Kickstart MMC5 image reaches the open window

at field 5,451, correctly reads the `Native MUI` label and 312-by-167 root at

4,11, and captures its rendered pixels. It stops with the expected missing

`5` button because this fixture has no calculator keypad. This validates the

reader against a real control window; it is not an arithmetic test. Artifacts

are under `mui-calc-native-interaction-reader-kick31-v42/`.



Adaptive original-client runs on both ROMs are under

`mui-calc-native-interaction-{kick31,copperstart}-v42/`, bounded to 300,000

fields, using the same v42 images and 5 MiB diagnostic configuration. Results

remain pending. Separate v42b captures confirm both clients progress through

62 prepared Area objects into native Group layout; titled but blank windows

at that stage remain incomplete rendering evidence.



The separate v42b Kickstart field-140,000 image subsequently shows the String

area and `trig`, `sin`, `cos` controls being painted. It is partial painting,

not a completed calculator display. CopperStart's field-120,000 image is black;

the field-150,000 image after scheduled mouse movement shows its titled window

again. No screen-blanking cause or successful close-gadget delivery is inferred

from those images. The adaptive interaction pair is still in startup.



No ROMs, media or build artifacts are staged or committed. No emulator hardware,

ROM pins or desktop bundle requirements are changed by this work.



## v42 full-client failure and measured-layout candidate



Both adaptive runs finish Window.Open, then reject the keypad geometry before

any arithmetic click: Kickstart at field 172,101 and CopperStart at 212,801.

The `5` key is 78-by-5 pixels at 82,137 on Kickstart and 79-by-5 at 81,137

on CopperStart. Both windows are the provisional 320-by-180 size. These are

unusable key rectangles, not arithmetic passes. The completed 220,000-field

fixed-input runs also do not prove complete quit: Kickstart retains 77 objects

with depth 0/0; CopperStart retains 64 with depth 0/3. See

`artifacts/utility-mui-native-window-calc-state-v42b-final.jsonl`.



The new regression exposes a missing native class-specific measurement pass:

nested Group children were consumed as plain Areas, losing their descendants'

preferred extents. The candidate measures children first using the existing

Group, List, Listview and common-control cores. Temporary guest records supply

those bounds during layout and are freed before drawing. Callbacks and DoMethod

invalidate the snapshot and use bounded live composite measurement instead.

The native platform remains a four-byte carrier; its temporary transaction is

an owned guest-memory frame that preserves the canonical owner context.



Automatic window axes now use measured preferred content plus real borders,

within the actual screen bounds; explicit positive dimensions remain intact.

[Intuition resizing is asynchronous](https://wiki.amigaos.net/wiki/Window_Structures_and_Functions),

so the adapter waits for the actual dimensions with no Layer held before layout.

The candidate uses the existing bounded Screen projection for dimensions. The

full SDK Screen aggregate and the first enlarged platform value were rejected

by the pinned native compiler; those failed build logs remain retained.



Both profiles pass 3,901 host tests, zero skipped, in

`utility-mui-native-window-tests-v43g.log` and

`utility-mui-native-window-tests-default-v43e.log` under `artifacts/`.

Native compilation completed successfully in

`utility-mui-native-window-build-v43d.log`, targeting

`copperos-mui-native-window-68000-v43b/`.



MMC6 adds a twenty-line owned Text root, then records the real native Window

width/height after Open returns. It requires width 320 and height 200–256,

alongside the existing notification, input ownership and repeated-memory checks.

Its 2,472-byte command has SHA-256

`edcab057f75c745dd92493f8b49f5ff306683003d7b46a6ccade61500852b599`.

All earlier default command hashes remain unchanged. Native MMC6 results

are recorded below; full calculator arithmetic/quit remains pending.



The v43b native image subsequently builds successfully: 1,201,340 bytes,

19,481 relocations, SHA-256

`5c2dcecd1262baf6bfca5c555af406905a467ff3eda5f13aeb5db30464878f88`.

MMC6 runs completed under `mui-native-window-resize-probe-*-v43b-5m/`, bounded

to 40,000 Kickstart / 45,000 CopperStart fields and capturing every 2,000.

The extended inspector still passes both retained v42 MMC5 captures; the

backward-check receipt is `artifacts/utility-mui-inspector-backward-check-v43.json`.



Both final MMC6 captures pass. Kickstart grows both native windows to height

213; CopperStart grows them to 214. Available memory is 3,938,472 / 3,938,472

bytes on Kickstart and 2,985,604 / 2,985,604 on CopperStart. Each cycle handles

all four events and cumulative callback counts are 1 then 2. Both final

registries have zero objects/classes and depth 0/0. Receipts are

`artifacts/utility-mui-native-window-resize-result-v43b-5m.json` and

`artifacts/utility-mui-native-window-resize-state-v43b-5m.jsonl`.

Visual inspection shows all twenty lines inside the resized window at

`mui-native-window-resize-probe-kick31-v43b-5m/frame-006000.bmp` and

`mui-native-window-resize-probe-copperstart-v43b-5m/frame-010000.bmp`.



The adaptive full-client pair ran against v43b under

`mui-calc-native-interaction-{kick31,copperstart}-v43b/`, with the same frozen

engine binaries and 300,000-field bound. No calculator arithmetic or complete

normal-quit pass is claimed yet. Captured v42 numeric keys have Text_SetMin=1;

their five-pixel heights are not caused by an explicit disabled text minimum.



## Visible setter updates — v44 candidate



The v43b measurement fix gives the `5` key a ten-pixel height on both ROMs.

Kickstart completes Window.Open at field 149,951 and accepts `5` at 182,651,

then `Enter` at 213,901. CopperStart completes Open at 190,251, but the v1

15,000-field press timeout expires at 205,901 while dispatch depth is two.

That run is incomplete; it proves neither an input failure nor an accepted click.



The Kickstart `frame-182651-after-0.json` contains a noninteractive display Text

with contents `5`, but the matching BMP still has a blank display. This is a

confirmed stale-paint bug, independent of arithmetic. Native setters now repaint

prepared, shown objects after public state/notification projection. Constructor,

cleanup and disposed targets have no repaint. Drawing also stops at empty

geometry, including inactive page subtrees whose descendants retain minimum

rectangles; direct redraw checks those ancestor rectangles too.



Both host profiles pass 3,901 tests, zero skipped, with updated display and empty

parent regression assertions. Logs are `utility-mui-native-window-tests-v44b.log`

and `utility-mui-native-window-tests-default-v44b.log`. The earlier `v44` logs

retain a test-fixture compile error corrected before these passes.

Native compilation succeeds in `utility-mui-native-window-build-v44.log`:

1,202,072 bytes, 19,541 relocations, SHA-256

`3428a0b5719cc62924cdf42359fc3dee893a8dc4ad0198a9a26b70565cc14bdd`.



The remaining v43b Kickstart and CopperStart v2 runs were deliberately stopped

for this confirmed rendering fix. The retained interruption receipt is

`artifacts/utility-mui-native-interaction-v43b-interruption.json`. They remain

incomplete, with no arithmetic or normal-quit pass. The new mouse protocol v2

uses the same machine/input sequence but bounds each press/release at 60,000

fields and captures CPU registers. Its successful build log is

`utility-mui-calc-native-interaction-build-v2.log`.



MMC7 adds a real post-Open Text setter/getter to the existing MMC5 ownership and

notification sequence, then holds the native window for a separate pixel check

of `11`. This label is a repaint probe, not a calculator arithmetic result.

The command is 2,408 bytes, SHA-256

`45003a839cca5fe93153e850ffd1386d9c82738b13c2fdb1f9235c7ab7037670`.

All MMC2–MMC6 command hashes remain unchanged. Native MMC7 and full Calc results

for v44 remain pending. The extended inspector still passes both final v43b

MMC6 captures (`artifacts/utility-mui-inspector-backward-check-v44.json`).



Fresh v44 app images retain all seven original package files: native OFS SHA-256

`14fa00e4ba968122ae470e24088e0a657a9afd8e7c177ce64244d4405965c944`,

CopperStart FFS `ce986189d5286e75bdbb7d5f1145eaf8733827f13fa9af1e5e987047da6a6ef8`.

MMC7 is running under `mui-native-window-update-probe-{kick31,copperstart}-v44-5m/`

with 40,000 / 45,000 fields and 2,000-field captures. The mouse-v2 full-client

pair is running under `mui-calc-native-interaction-{kick31,copperstart}-v44/`

with an explicit 600,000-field bound. Both use the frozen engine/CPU and separate

5 MiB correctness profile described above. No v44 native pass is claimed yet.



The final v44 MMC7 scalar/lifecycle captures pass on both ROMs, with exactly

3,938,560 / 3,938,560 available bytes on Kickstart and

2,985,172 / 2,985,172 on CopperStart. Both end with zero objects/classes and

depth 0/0. Receipts are `utility-mui-native-window-update-result-v44-5m.json`

and `utility-mui-native-window-update-state-v44-5m.jsonl` under `artifacts/`.

These results do not qualify repainting. Denser 250-field captures retain the

counterexample: Kickstart `mui-native-window-update-visual-kick31-v44-5m/`

`frame-004750.bmp` still displays `Native MUI` after the setter/getter has

recorded `11`. The 12,000-field CopperStart visual run stops before its setter;

it supplies no post-update pixel coverage.



The v44 visibility check incorrectly consumed a Window's default zero Area

geometry. Window owns an Area tree without having an Area rectangle itself.

The v45 correction stops geometry ancestry at that Window boundary, while

retaining empty-page checks below it. The host regression now attaches its

nested tree to an actual registered Window, then verifies a visible `11` update

and suppression under an empty nested Group. Both host profiles pass all 3,901

tests, zero skipped, in `utility-mui-native-window-tests-v45.log` and

`utility-mui-native-window-tests-default-v45.log`.



Both v44 full Calc runs were deliberately stopped for this confirmed boundary

fix, retaining `artifacts/utility-mui-native-interaction-v44-interruption.json`.

No arithmetic or normal-quit pass is claimed. Native v45 compilation succeeds

in `utility-mui-native-window-build-v45.log`: 1,202,124 bytes, 19,544 relocations,

SHA-256 `2a39eadeedd0f95994d7c7d2859d4755daab34628824c6c42641073986765299`.

Its focused MMC7 pair completes under

`mui-native-window-update-probe-{kick31,copperstart}-v45-5m/`, using the same

40,000 / 45,000-field bounds and 500-field captures. Both final captures pass:

available memory is 3,938,520 / 3,938,520 bytes on Kickstart and

2,985,124 / 2,985,124 on CopperStart, with zero objects/classes and depth 0/0.

The receipts are `artifacts/utility-mui-native-window-update-result-v45-5m.json`

and `artifacts/utility-mui-native-window-update-state-v45-5m.jsonl`.

Visual inspection confirms the updated `11` on Kickstart at

`mui-native-window-update-probe-kick31-v45-5m/frame-006500.bmp` and CopperStart at

`mui-native-window-update-probe-copperstart-v45-5m/frame-011500.bmp`.

This is a Text setter repaint pass, not calculator arithmetic.



The fresh full-client mouse-v2 pair is running under

`mui-calc-native-interaction-{kick31,copperstart}-v45/` with a 600,000-field bound.

Its OFS image SHA-256 is

`a37b3455e141a0bfbdd048f850dfb6c78ed8d8c2ce1e673b2c4f3f03eb18f7f7`;

FFS is `deb3bf09a1d7f4773c30cf07856c81ed451fc27c42f8a1112d1f7659a75a4f07`.

Both retain all seven original Calc files and the frozen machine/engine profile.

The active checks remain `5 Enter 6 Enter +` with visible `11`, then physical

native-close input, a returned CLI, and empty native registries. These full-client

arithmetic/quit results remain pending.



## Indexed display background — v46 candidate



The retained v43b Kickstart `frame-182651-after-0` capture also exposes a

separate color collision: the numeric display Text has Background `4`, while

the real Window RastPort bitmap has depth two. The native unavailable-pen

fallback maps `4` to foreground pen `1`; ordinary text also selects pen `1`.

This aliases the fill-stage background and foreground policy. The later v45

client capture nevertheless visibly renders `5` after repaint, so this static

collision is not evidence that client arithmetic fails. The numeric button `5`

instead requests Background `2`, which is available on that screen. The native

background-role fallback below is verified independently with Background `4`.



The graphics contract now distinguishes background-pen selection from ordinary

pen selection. Its default delegates to the existing SetPen, preserving other

providers. The native indexed adapter uses screen background pen `0` when a

background value is unavailable in a known one-through-seven-plane palette;

available values remain intact. Foreground/decorative selection keeps its prior

policy. This is the initial indexed profile's fallback, not a full background

image/skin provider. It does not rewrite the client's Background attribute.

Area, explicit DrawBackground and common-control background fills use that role.



Both host profiles pass all 3,901 tests, zero skipped, in

`utility-mui-native-window-tests-v46.log` and

`utility-mui-native-window-tests-default-v46.log`. The Window-owned display test

now verifies background pen `0` versus text pen `1` after a `11` update on both

one- and two-plane profiles. Native compilation succeeds in

`utility-mui-native-window-build-v46.log`: 1,202,272 bytes, 19,548 relocations,

SHA-256 `ef6afb50adacc5a183b35b2864d9afe23f006b81cb09603894a3fce6beca5ec2`.

This intermediate candidate is not guest-replayed; the combined v47 candidate

below adds the required List rendering changes. The v45 full-client pair

continues unchanged while that candidate is prepared.



MMC8 extends MMC7 with Calc's Background `4` on the rendered Text and verifies

that its public getter remains `4` after the update. The physical color/text

check remains separate. The command is 2,516 bytes, SHA-256

`5b3300a425c519f669adf96768387f2a4c0412e56d5061d66660ef620f0376d3`.

MMC5–MMC7 byte hashes are unchanged; no native MMC8 pass is claimed yet.



## Native List refresh and row contrast — v47 candidate



List row emission previously inherited the Area/stripe fill pen. The existing

stripe host test even retained that pen after emitting text. Rows now restore

foreground pen `1`, then reapply any resolved text-color override. Stripe fills

use the background role, keeping rows legible on a one-plane native screen.



The native built-in dispatcher now observes the List core's existing redraw

request counter around ordinary methods and repaints prepared Lists when that

counter changes. This covers mutations without special-casing Calc's payload.

Unprepared/disposed objects remain no-ops; Quiet suppresses painting until the

core flushes its batched request. Direct Draw is excluded from counter tracking.



Both host profiles pass all 3,902 tests, zero skipped, in

`utility-mui-native-window-tests-v47b.log` and

`utility-mui-native-window-tests-default-v47b.log`. The earlier `v47` logs pass

before the final stripe-background/Quiet assertions; the `v47b` logs cover the

current source. The new regression verifies insertion, two-row quiet flushing

and clearing the visible list, alongside foreground/background assertions.



Native compilation succeeds in `utility-mui-native-window-build-v47.log`:

1,202,812 bytes, 19,554 relocations, SHA-256

`df0510ae1984182899387a846c720fee51bb0ec88c1610d488ce04bc3d4024bc`.

Both app images retain all seven original package files: OFS SHA-256

`a22be424fc078432530e7ee96e6bb249799747a5d78c85d837bc5ba49d903f3e`,

FFS `98949828546177b8abe6ddc6bbd554dd78f63c0641b1d23aa0bfa65f53d444aa`.



MMC9 combines MMC8's indexed Text update with a List in the second owned Window.

It opens that native Window, inserts `11` and `12` through real List methods,

records two entries and the native handle, holds it for pixel inspection, then

closes and disposes both cycles. The getter still preserves Background `4`.

The command is 2,892 bytes, SHA-256

`030f864ce72cbd83e42eeda49178e73d392c3c072ffda66e793980d52db3777a`.

MMC7/MMC8 hashes remain unchanged. The updated inspector still passes both

retained final v45 MMC7 captures (`utility-mui-inspector-backward-check-v47.json`).



MMC9 is running under `mui-native-window-list-rows-probe-{kick31,copperstart}-v47-5m/`

with 40,000 / 45,000-field bounds and 500-field captures. Its native pixel and

lifecycle results are pending. The `11`/`12` labels in this probe are rendering

fixtures, not calculator arithmetic results. Fresh full-client v47 arithmetic

and normal-quit qualification will follow this native probe.



Both final MMC9 captures pass the complete scalar/lifecycle checks. Available

memory is 3,936,880 / 3,936,880 bytes on Kickstart and

2,983,988 / 2,983,988 on CopperStart. Both final registries have zero objects and

classes, with dispatch/callback depth 0/0. Receipts are

`artifacts/utility-mui-native-window-list-rows-result-v47-5m.json` and

`artifacts/utility-mui-native-window-list-rows-state-v47-5m.jsonl`.

Visual inspection confirms Text `11` and both List rows `11`, `12`:



| ROM | Background-4 Text update | List insertion/stripe paint |

| --- | --- | --- |

| Kickstart | `mui-native-window-list-rows-probe-kick31-v47-5m/frame-006500.bmp` | `mui-native-window-list-rows-probe-kick31-v47-5m/frame-009500.bmp` |

| CopperStart | `mui-native-window-list-rows-probe-copperstart-v47-5m/frame-011500.bmp` | `mui-native-window-list-rows-probe-copperstart-v47-5m/frame-015500.bmp` |



The fresh mouse-v2 full-client pair is now running under

`mui-calc-native-interaction-{kick31,copperstart}-v47/`, with the same frozen

engine/profile and 600,000-field bound. The earlier v45 pair also continues:

Kickstart accepted `5` at field 171,951 and its matching BMP visibly renders

`5`, confirming that the repaint fix reaches the original client. Its first

Enter and CopperStart's first click are still being processed at this update.

No full-client arithmetic or normal-quit pass is claimed for either version.



## Classic collection method routing — v48 candidate



The original executable uses `MUIM_List_Insert` (`0x80426c87`), not

`MUIM_List_InsertSingle`: file offsets `0x3912`, `0x4100`, `0x414c` and `0x4194`

contain that array method. The decoded packet uses count `1` and bottom position

`-3`. The v45 Kickstart `frame-204051-after-1` capture accepts Enter and clears

the number display, but both List headers still have zero entries and zero

redraw requests. Its private global `0xb16(A4)` targets the main `List.mui`

object (`0x35d734`, A4 `0x219c60` in that capture).



Native dispatch had only intercepted InsertSingle and Clear; its generic

headless dispatcher has no collection path. The new adapter routes registered

List/Listview packets through the existing `MuiCollectionDispatcher`, keeping

the shared typed codecs and mutation semantics. Forwarded Listview operations

track the actual child List's redraw counter. Quiet Lists are also excluded from

tree drawing so parent repaint cannot bypass batching. The host native-collection

regression now sends the exact classic array packet through this adapter.

Both profiles pass 3,902 tests, zero skipped, in

`utility-mui-native-window-tests-v48.log` and

`utility-mui-native-window-tests-default-v48.log`.



The superseded v45/v47 full-client pairs were stopped for this confirmed API

gap. `artifacts/utility-mui-native-interaction-v45-v47-interruption.json` records

the reason and retained captures. No arithmetic or normal-quit pass is inferred.



Native compilation succeeds in `utility-mui-native-window-build-v48.log`:

1,254,232 bytes, 20,427 relocations, SHA-256

`54c3d69c4a1358a3f4281cdebbf94db6001935e7ef5b3aabbe43e86c61a2e816`.

The seven-file original package remains intact. App OFS SHA-256 is

`e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799`;

FFS is `7f081c3e6ad136dcf015f472f6b65f05d94b90856ca9a71dc7377824a55852be`.



MMC10 retains MMC9's two phases and ownership checks, but inserts each List

entry using the client's array/count/bottom packet. Its 2,920-byte command has

SHA-256 `5ac671d6e752c7b6182f74bd42d0c19d2e3648fa0eb4f8a31787970a9b3aabe8`.

MMC9's command remains unchanged. The extended inspector still passes both

final v47 MMC9 captures (`utility-mui-inspector-backward-check-v48.json`).

MMC10 is running under `mui-native-window-list-array-probe-{kick31,copperstart}-v48-5m/`

with 40,000 / 45,000-field bounds and 500-field captures. No native array pass is

claimed yet.



The mouse diagnostic v3 also builds successfully with zero warnings/errors in

`utility-mui-calc-native-interaction-build-v3.log`. It retains the hardware input

sequence and 60,000-field press/release bounds, captures empty registries before

executing a further 5,000 guest fields, and fails on registry reactivation during

that tail. Library expunge is accepted only after that cleanup capture. The

settled CLI and arithmetic images still require visual review. Earlier v1/v2

receipts retain their original observation scope.



Both final MMC10 captures pass. Available memory is 3,888,920 / 3,888,920 bytes

on Kickstart and 2,936,036 / 2,936,036 on CopperStart; final object/class counts

and dispatch/callback depth are all zero. Receipts are

`artifacts/utility-mui-native-window-list-array-result-v48-5m.json` and

`artifacts/utility-mui-native-window-list-array-state-v48-5m.jsonl`.

Visual inspection confirms both array-inserted rows at

`mui-native-window-list-array-probe-kick31-v48-5m/frame-009500.bmp` and

`mui-native-window-list-array-probe-copperstart-v48-5m/frame-015500.bmp`.

This qualifies that native packet/rendering/lifecycle path, not Calc arithmetic.



The fresh full-client pair now runs mouse-v3 under

`mui-calc-native-interaction-{kick31,copperstart}-v48/` with a 600,000-field bound,

the unchanged frozen engine/CPU, ROMs and separate 5 MiB correctness profile.

The original client's stack entries, visible result `11`, normal close and settled

CLI/registry state still need verification. No full-client pass is claimed yet.



The original client's first `5 Enter` now succeeds on both ROMs: Kickstart at

field 205,451 and CopperStart at 244,551. The matching `after-1.bmp` captures in

each v48 interaction directory visibly show `5` in the stack and a cleared input

display. Read-only inspection confirms the live List cookie and one entry / one

redraw request; the log List remains empty. The pinned state receipt is

`artifacts/utility-mui-calc-first-enter-state-v48.json`. This resolves the earlier

zero-entry counterexample in the actual client; `6 Enter +` and normal quit still

remain pending at this milestone.




## Final original-client review (v48)

Both `mui-calc-native-interaction-{kick31,copperstart}-v48/result.json` runs
terminate successfully with input/cleanup PASS. Actual hardware mouse releases
accept `5`, `Enter`, `6`, `Enter`, `+` in order. No guest method injection or RAM
writes drive the application. Manually reviewed arithmetic images at fields
306,301 (Kickstart) and 353,901 (CopperStart) visibly show `11.000…` in the stack.

Cleanup is observed at fields 328,001 / 396,201. After a further 5,000 guest
fields, the final captures are 333,051 / 401,251. Read-only RAM checks validate
the live MUI registry cookie and zero objects, classes, dispatch and callback
depth at both observations. Kickstart visibly returns to its `1>` CLI prompt.

CopperStart's MUI window is unlinked (`Screen.FirstWindow = 0`), its
`MUI_calc_process` is absent from the task lists, and the parent CLI has no
loaded command/module and return code 0. The final image nevertheless retains
old calculator pixels. The installed CopperGui provider owns only a screen-bar
layer; this startup runs `C:CopperGui` then Calc, with no Workbench desktop
backdrop underneath the closed window. These observations support normal
process exit and MUI resource cleanup; they do not qualify desktop restoration.
No MUI workaround clears pixels outside its owned window.

The immutable raw result files continue to require visual arithmetic review;
the separate final receipt records that completed review and hashes the images
and RAM captures. Earlier incomplete/failed runs remain unchanged. This is a
bounded native compatibility result, not complete MUI/API qualification or a
host throughput measurement. No ROM, media, product preset or engine/CPU pin
was changed for this result.

## Stale-pixel investigation follow-up

The [focused investigation](UTILITY_MUI_STALE_PIXELS_2026-11-01.md) supersedes
the earlier environmental explanation: the missing backdrop exposes a Layers
default-background backfill gap. The captured LayerInfo selects default
backfill, but a focused current-source deletion diagnostic produces zero fills
where one is required. The no-backfill control passes. Normal MUI teardown
remains verified; background restoration is a confirmed defect awaiting repair.

## Default-background repair follow-up

The [Layers fix and v50 original-client retests](UTILITY_MUI_BACKFILL_FIX_2026-11-01.md)
now pass native OS window unlinking and background restoration on CopperStart.
The v48 library binary remains unchanged. Both ROMs still visibly compute 11;
CopperStart now has zero stale pixels after closing. Earlier milestone receipts
retain their original results.
