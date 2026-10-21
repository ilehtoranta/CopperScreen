# CopperOS simple desktop implementation plan

Planning date: 2026-10-17. Target: the first useful native CopperOS desktop for
the CopperScreen itch.io release. All deliverables below are planned unless an
existing evidence record is identified. Implementation ownership stays with
CopperStart, CopperOS and CopperScreen respectively.

Build the desktop on a qualified native screen, input and process foundation.
The first complete journey is: boot offline, browse files, run and exit Demo,
open Shell, create and save a text file, close CopperScreen, and reopen the file.
The initial desktop uses one screen with fixed application views. General
overlapping, movable and resizable application windows can follow later.

## Product scope

Retain the current PAL A500, 68000 interpreter, OCS 8371/8362, 512 KiB Chip plus
512 KiB slow RAM, one DF0 configuration. The system is an original DOS1 DD FFS
image cloned into a writable user workspace. Larger RAM, hard disks, shared
folders, RTG and other machine profiles need separate qualification and are not
ways to conceal a failed fit on this target.

| Component | First desktop behavior |
| --- | --- |
| Home | Original desktop artwork, system-volume entry and launch actions for Files, Editor, Shell, Demo, Help and About. All actions have keyboard equivalents. |
| Files | Show the current path, folders, names, types and sizes; open and return from folders; create a folder; rename; delete a selected file or empty folder after confirmation; refresh. Use a paged listing and handle names containing spaces. |
| Editor | One plain-text document, New/Open/Save/Save As, cursor movement, insertion/deletion, scrolling, dirty indicator and Save/Discard/Cancel on leaving. Initial document limit is 16 KiB with a clear refusal for larger input. |
| Launcher | Start the bundled native Demo and Shell through DOS, report failures and restore the desktop after normal child exit. Launch known executables only; files open in Editor through an explicit action. |
| Help and About | Explain included content, controls, workspace persistence, host backup/export and exact component versions. |
| Feedback | Visible focus and selection, pointer and busy state, readable operation errors and overwrite/delete confirmation. |

Use original small icons and a bundled fixed-width font. Start with a proposed
640 by 256 noninterlaced, four-colour PAL screen for readable text; two bitplanes
take 40,960 bytes before Copper lists, pointer, font and other allocations. Verify
display setup and text readability in the first probe before freezing this mode.
Do not allocate a second full-screen bitmap by default.

Keep application views fixed within the desktop. Files and Editor may initially
share its process and small widget implementation; OS windows, event delivery,
focus and resource ownership remain CopperStart services. Shell may use its own
qualified console screen/view while the desktop waits. Only one external
foreground child is required at a time. The desktop must resume after that child
exits; simultaneous independent applications are a later feature.

Themes, arbitrary icon formats and tool types, drag-and-drop, recursive deletion,
copy/move trees, general file associations, multitasking application switching,
MUI/BOOPSI widgets and Workbench application compatibility are outside this first
desktop. Existing MUI and Intuition compatibility work retains its own scope and
acceptance criteria.

## Baseline and the first blockers

| Area | Existing evidence | Work needed for the desktop |
| --- | --- | --- |
| CopperOS boot and storage | Native Shell and Demo run; DOS1 file mutations, workspace checkpoints, restart and diagnostic package-update persistence have separate evidence. See [boot proof](COPPEROS_BOOT_PROOF_2026-10-16.md) and [storage](COPPEROS_STORAGE.md). | Qualify graphical startup, GUI use of these services, safe editor replacement, resource lifetimes and the shipping bundle. |
| App firmware pin | [The current app contract](COPPERSTART_APP.md) selects the writable development ROM and explicitly excludes concurrent graphics work. | Produce one new ROM containing the required writable DOS and native graphics capabilities, then qualify its exact OS pair. |
| Native graphics | The [installed-owner update replay](engine/COPPERSTART_OWNED_LAYER_UPDATES_2026-10-18.md) joins real Layers startup and CMLG drawing on displayed 320/640 Ã— 256 Ã— 2 Views. Simple damage-only/partial/empty updates and allocation rollback, plus Smart concealed drawing/restoration, pass three lifetimes each. All 344 combined checks pass on the unchanged 512 KiB ROM / 1 MiB A500. | The minimal native Intuition screen/window owner is now qualified separately. The native refresh pair and desktop repaint are now qualified separately. Native [IDCMP refresh messages](engine/COPPERSTART_INTUITION_IDCMP_2026-10-18.md) now add coalescing, replies and signal wait/wake. The [native physical input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) now adds pointer/focus, RAWKEY/buttons, close requests, deferred IRQ wakeup and bounded queue recovery. The [text follow-up](engine/COPPERSTART_INTUITION_TEXT_2026-10-18.md) adds default/custom KeyMap text and host focus/capture release. Finish visible layout selection, actual platform capture gestures and the complete shipping ROM/desktop integration. Qualify disjoint damage, native foreign clips, callback continuation and contention edges; measure the complete GUI closure and allocation peaks. Only 842 contiguous ROM tail bytes remain. Keep producer results separate from the shipping app pair. |
| Intuition and Layers | The [compact native Intuition owner](engine/COPPERSTART_INTUITION_STARTUP_2026-10-18.md) opens a real 320/640 Ã— 256 screen and decorated window through public Layers/graphics vectors, with provider pins, rollback and expunge. The ROM fingerprint and older IR01 private actor-query path remain separate. | The [native refresh slice](engine/COPPERSTART_INTUITION_REFRESH_2026-10-18.md) adds partial BeginRefresh/EndRefresh, desktop/frame repair, failed-projection rollback and retry. The [IDCMP refresh slice](engine/COPPERSTART_INTUITION_IDCMP_2026-10-18.md) now adds native coalescing, replies, signal wait/wake and subscription rollback. The [physical input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) now joins pointer/focus, close and shared reply ownership, with queue-pressure recovery at both widths. This selected disk owner does not admit the broader Intuition implementation or Workbench. |
| ROM and RAM | The selected text-enabled Intuition HUNK is 256,972 bytes and runs in 512 KiB Chip + 512 KiB slow RAM, using the qualified console-transport 512 KiB ROM. Its measured text-input installation delta is 36,832 bytes, including 1,808 bytes of reset-lifetime keymap storage; this is not the complete desktop peak budget. Historical full-Intuition ROM sizing concerns a different composition. | Measure complete desktop loader/transient peaks, stacks and event/input queues, plus actual desktop startup and interaction latency. Checked live-window free memory is not a peak budget. |
| Command and library loading | The boot proof uses a bounded streaming single-CODE/self-relocation Shell loader. Current producer disk-library work reports session-pinned HUNK images. | Freeze the supported HUNK forms, real initialization/version checks and retention policy for the selected GUI libraries and applications. |

The source inspection includes ongoing uncommitted producer work. Rebuilds must
identify their complete input snapshots; a producer HEAD or filename alone does
not select the app's compatible artifact. The frozen CLI inspection contract is
historical and remains unchanged.

Producer planning references are CopperStart's `Docs/NativeGraphicsStandalone.md`,
`Docs/NativeWorkbenchStartup.md`, `Docs/IntuitionIR01NativeProviderIntegration.md`
and `Docs/IntuitionGoalExecutionPlan.md`, plus CopperOS's
`tools/ImageBuilder/README.md` and `src/System/Shell/README.md`. Recheck their
current source/evidence before starting each owning task.

## Ownership and implementation locations

| Owner | Changes |
| --- | --- |
| CopperStart | Exec tasks, ports, signals and allocation; native keyboard/mouse/device routing; graphics and utility providers; Layers and Intuition screen/window/event services; DOS loader, child lifetime, console and filesystem behavior. Start in `rom/CopperStart.SystemRom`, `native/CopperStart.Graphics`, and the owning `src/CopperStart.Exec`, `Devices`, `Dos`, `Layers` and `Intuition` projects. Graphics emitters currently live under `host/CopperStart.CopperMod/CopperStart/Graphics/Portable`; their build-tool placement does not authorize host callbacks at runtime. |
| CopperOS | Proposed `src/System/Desktop` for startup, home, Files and reusable controls; proposed `src/Tools/Editor` for the editor model/native entry; existing `src/System/Shell`; `tools/ImageBuilder` for admitted runtime composition. Files and Editor can link into one initial native desktop executable. |
| CopperScreen | Versioned artifact admission, workspace compatibility, launcher/help changes, source and installed qualification. Extend existing `CopperOsBootProofTests`, storage tests and scripts; add a separate native desktop journey fixture. |
| CopperSharp68k and SDK owners | Required compiler or ABI fixes discovered by producer native compilation. Qualify and version these inputs with the producer; do not import their source graph into CopperScreen. |

All guest operations execute native 68000 instructions on Lightweight. No host
DOS/graphics/window service, old Legacy gateway or second hardware owner is part
of this plan. CopperScreen consumes versioned artifacts with manifests and
hashes, without project references to producer checkouts.

## CopperStart work required first

The [2026-10-17 combined producer proof](engine/COPPERSTART_DESKTOP_PREREQUISITES_2026-10-17.md)
qualifies writable DOS and compact native graphics together on the bounded
1 MiB machine. It records current ROM/RAM fit and 51 native execution cases;
the first native window and the full CopperOS/app-pair qualification remain open.

Complete CS1 through CS6 before treating a CopperOS desktop application as an
integration target. CS7 and CS8 must pass before Files and Editor can claim their
launch/save workflows. CopperOS models, layout assets and image-builder changes
can proceed independently against the agreed contract.

| Task | Deliverable and dependencies | Acceptance |
| --- | --- | --- |
| CS1 Combined native baseline | Compose writable DOS/trackdisk and selected native graphics in one standalone ROM. Freeze compiler/SDK, source snapshot, vector inventory and machine profile. | One exact ROM passes existing Shell/Demo and native storage journeys plus graphics probes. Replay affected disk-boot controls, including retained Lotus/Arte/Desert Dream workloads where supplied. Preserve old evidence and disclose unavailable inputs. |
| CS2 Provider lifecycle and placement | The [native provider slice](engine/COPPERSTART_PROVIDER_STARTUP_2026-10-18.md) qualifies disk-loaded Utility V40 and owned Layers V52, real provider pins, rollback and expunge. Its 223,116-byte HUNK stays loaded until reset; the frozen 512 KiB ROM is unchanged. The separate compact Intuition slice now checks its provider lifetime and rollback. The complete GUI placement/peak budget remains open. | Native Exec opens compatible providers; Layers and Intuition acquire/release them; missing, incompatible and partially initialized providers roll back without fake successful pins. Keep post-expunge memory observations separate from live GUI and peak-loader budgets. |
| CS3 Exec and interrupt lifetimes | The [input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) qualifies task-context signal wakeup deferred from VBlank through the native console Schedule boundary, a Wait-blocked worker/application and safe hook retirement. General interrupt-context Exec.Signal behavior remains outside this bounded composition. | A native application blocks on a port/signal, wakes from a guest interrupt-driven producer and exits. Repeated startup/teardown restores owned resources and interrupt state; no continuously polling desktop task substitutes for missing wait/wake. CS4 separately connects physical guest input. |
| CS4 Physical guest input route | The [native input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) qualifies physical RAWKEY/up/shift/repeat, pointer, focus, all buttons, close release/cancel, default backlog limits and pool/ring recovery. The [text-input follow-up](engine/COPPERSTART_INTUITION_TEXT_2026-10-18.md) adds current-default KeyMap translation, Latin-1/dead keys, mixed raw/text delivery and transactional host focus/capture release. OS-level capture gestures remain a release integration check. | CopperScreen keyboard/mouse input reaches a native event consumer. Check held keys, focus changes, button-up, repeat, queue pressure and close with outstanding events. Synthetic WriteEvent tests remain separate from real device delivery. |
| CS5 Screen and window subset | The [native Intuition slice](engine/COPPERSTART_INTUITION_STARTUP_2026-10-18.md) qualifies screen/window creation, title/close decoration, clipped drawing, empty IDCMP port ownership, allocation rollback and display restoration at 320/640 Ã— 256. It retains the 512 KiB ROM / 1 MiB A500. | The [refresh slice](engine/COPPERSTART_INTUITION_REFRESH_2026-10-18.md) now qualifies partial/empty refresh, exposed desktop/frame repainting and allocation-failure retries. Native [refresh message/reply ownership](engine/COPPERSTART_INTUITION_IDCMP_2026-10-18.md) now includes coalescing, wait/wake and queued/delivered teardown. The [input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) now qualifies the shared worker/reply path, pointer and physical input transport. The [joined replay](engine/COPPERSTART_INTUITION_JOINED_2026-10-18.md) now exercises those services together on a shared application port. Bootstrap TextInputEntry explicitly when the desktop needs translated text. The earlier owned-Layers update proof remains the prerequisite, and an allocated IDCMP port does not establish event delivery. |
| CS6 GUI events and ownership | The [input slice](engine/COPPERSTART_INTUITION_INPUT_2026-10-18.md) joins the selected screen/window/refresh owner to physical input for three lifetimes per width. The [joined shared-port replay](engine/COPPERSTART_INTUITION_JOINED_2026-10-18.md) now checks refresh/input coexistence, delayed replies, pressure, partial refresh and subscription/topology rollback in one dispatcher. Finish desktop keyboard navigation, broader dynamic subscriptions and the 100-cycle stress gate. Keep public API admission tied to the frozen owner. | An original native GUI probe opens a window, draws text, responds to physical keyboard/mouse actions, replies to events and closes cleanly for 100 cycles. Queue/reply ownership and library pins return to baseline after warmup. |
| CS7 Native application handoff | After CS3/CS6, qualify DOS executable loading, Process/CLI creation, arguments, working directory, streams, exit results, cancellation request and desktop display restoration. | Launch Demo and Shell, type a command, close normally and restore the GUI/input. Repeat 100 normal child launches. Missing executable, low memory and failed initialization leave the desktop usable. A nonreturning guest still permits CopperScreen input release and restart; arbitrary crash containment is not promised. |
| CS8 Directory and save semantics | After CS1, qualify directory enumeration, name conversion, current-directory locks, rename collisions, deletion errors, short writes, Close/flush results and editor replacement behavior. | Native tests cover a multi-page directory, spaces, file/empty-folder operations, full/read-only media and failed replacement. No direct truncate of the only good copy is used for editor Save. Verify these properties on the final combined ROM. |

The existing Intuition workstream explicitly waits for real providers. Assign
CS2 to those provider owners; Intuition-local development must not manufacture
successful stubs or alter historical admission records. The desktop's small API
list does not waive the lifecycle or ordering prerequisites of those services.
If a separately bounded desktop profile is needed, the producer must define and
qualify it explicitly before a consumer can select it.

Prefer keeping boot, essential Exec/DOS/devices and a compact graphics foundation
in the 512 KiB ROM. Evaluate disk-loaded GUI libraries before expanding ROM
scope. Disk loading shifts memory and initialization costs into the guest; it
does not resolve fit automatically. Use actual measured native closures to choose
placement. Libraries deliberately retained for one boot have a declared fixed
cost; unbounded per-launch pinning is a failure.

## Desktop contract and first integration probe

Before GUI implementation, the producers should write one versioned desktop
contract containing the selected library versions/vectors, SDK structures, native
entries, event/reply ownership, loading rules, stack requirements and error paths.
Use SDK public types and documented register conventions. Do not invent private
pointer layouts shared across the desktop and ROM. Derive public API validation
from the SDK/autodocs and native controls; application document limits must not
become new restrictions on public Amiga entry points.

The candidate API list covers these capabilities; the producer inventory must
resolve their exact admitted entries:

- Exec library/device open and close, memory, message ports, signals and waits.
- Graphics bitmap/raster allocation, pens, font/text, rectangles, copies and
  display synchronization, with real Chip memory and blitter ownership.
- Intuition screen/window open and close, activation, event subscription,
  pointer and refresh through Layers. Support fixed geometry first.
- DOS lock/unlock and current directory; Examine/ExNext or the qualified
  equivalent; Open/Read/Write/Close; CreateDir/Rename/DeleteFile; IoErr; native
  child execution and completion; console opening and orderly close.

The selected text provider uses the current guest KeyMap, bounded built-in US,
Latin-1 bytes and Return CR. Add visible layout selection/loading and agree the
Editor file line-ending rules with this contract. Font coverage, input translation and editor read/write
must agree; unsupported text must not be silently converted or overwritten.

Do not add menus, gadgets or requesters to the required public API solely for
desktop decoration. CopperOS can draw a small toolbar, list, confirmation view
and text fields inside its window and handle their application state itself.

The first native probe must use the actual combined ROM and selected providers,
display a screen/window with a pointer and a label, accept a click and a typed
character, update the label, and close/reopen. It then launches Demo, waits for
normal exit and proves redraw and input work afterward as the CS7 extension.
CS6/D1 requires the window/input cycle; the child extension completes at D2.
Record real pixels, native events, ownership counters and component hashes. A
managed host renderer or a library name in a list cannot satisfy this milestone.

## CopperOS implementation sequence

| Task | Implementation | Dependency and completion |
| --- | --- | --- |
| OS1 Build and startup | Add the native Desktop entry/build manifest and compose a new original system image. Load required admitted disk libraries, establish the desktop Process and open its screen. Keep the proven Shell path available if desktop initialization fails. | CS6 and the contract. Cold boot reaches the desktop surface; a deliberately missing library produces an actionable console error. Image manifest contains only admitted original components. |
| OS2 Home and controls | Implement fixed layout, original icons/font, selection/focus, buttons, text fields, scrolling, confirmation views, busy indicator and Help/About. Use a single event loop with redraw on changes. | CS6 and OS1. Every action works using mouse and keyboard, with no stuck key or pointer after leaving/reentering. Idle waits rather than redrawing continuously. |
| OS3 Launch and return | Connect Demo and Shell actions to the qualified foreground child path. Preserve desktop state, give the child the correct directory/arguments/streams and restore focus/display on exit. | CS7 and OS2. Demo returns; Shell accepts `Echo hello` and exits back; launch errors preserve the desktop. Do not start a second reader on the same console stream. |
| OS4 Files | Add paged directory reads, path/back controls, explicit Open in Editor, known-program launch, new folder, rename, refresh and confirmed deletion. Keep locks and enumeration state owned by the active operation. | CS8 and OS3. Browse nested/multi-page directories, mutate and reopen results through native DOS; low memory, stale entries and read-only/full disks give recoverable errors. |
| OS5 Editor | Implement a bounded text model, caret, line/viewport movement, insertion/deletion, dirty state, file picker using Files and save/leave confirmation. Compile the production model into the native desktop; exclude managed runtime helpers. | CS8 and OS4. Edit, save, close and reopen exact bytes through the native filesystem. Reject over-limit files before altering them; preserve dirty text after save failure. |
| OS6 System composition | Include Desktop, Shell, Demo, font/icons, Help, required libraries and the small admitted command set in the image manifest. Set startup to Desktop with a usable Shell recovery path. | OS3 through OS5. Verify boot block, filesystem bitmap/tree, component hashes, notices, image capacity and free space for editing. Boot without Workbench/Kickstart files or developer folders. |

For Save, write to a unique temporary file in the destination directory, verify
all writes and successful close, then replace through the qualified rename
sequence. Retain the prior file as a backup until the new file is committed.
Define recovery for failure between the rename steps; do not assume replace-on-
rename or atomic multi-file transactions. If the filesystem needs an explicit
flush/commit capability, finish and qualify it in CS8. A save error leaves the
editor dirty and the recoverable prior copy intact. On next open, surface
incomplete-save recovery instead of silently discarding temporary/backup files.
Host workspace checkpoints remain a separate whole-disk protection mechanism.

Leaving the guest editor with unsaved text requires Save/Discard/Cancel. Explain
that CopperScreen Stop/Restart saves completed disk writes, not unsaved text in
the editor's RAM. The launch/reopen journey explicitly saves the document first;
host UI code must not inspect private guest editor state to simulate a save.

## Delivery milestones and dependencies

| Milestone | Tasks and concrete exit |
| --- | --- |
| D0 Contract and initial fit | Freeze the capability list, initial display mode, proposed budgets and source identities. Inventory current ROM/GUI closures and measure an initial placement build, stacks, image capacity and guest RAM. Publish the task backlog against actual producer files; CS2 and D1 must confirm the final combined fit before adding features. |
| D1 Native GUI foundation | CS1 through CS6 and the native integration probe pass on the bounded A500. This is the first CopperStart deliverable to prioritize. |
| D2 Useful launcher | CS7, OS1 through OS3: cold boot to Home, run Demo, use Shell and return. Keep the old CLI boot proof as a separate regression. |
| D3 Files and persistent document | CS8, OS4 through OS6: browse, create a folder, write/edit/save a document, close CopperScreen and read it after reopening. |
| D4 Consumer integration | Admit a new exact ROM/OS pair in CopperScreen, show versions, qualify old/new workspaces and run the entire journey from the self-contained itch candidate. |
| D5 Release qualification | Native regression, input/device checks, 60-minute use, clean-account download/update, recovery, rights/notices/help and support material meet the frozen release contract. |

Critical path: D0 -> CS1 -> CS2/CS3 -> CS4/CS5 -> CS6 -> D1 -> CS7 -> D2 -> D3
-> D4 -> D5. CS8 and CopperOS model/layout/image work can progress alongside the
GUI foundation once D0 defines the contract.

Every native component must pass the producer's freestanding MC68000 checks for
no FPU instructions, managed runtime, managed allocations, exception machinery
or unexpected helper calls. Keep explicit export lists and strip unneeded
symbols; a managed build or HUNK header alone cannot complete a task.

Do not assign a calendar release date before D1 establishes native GUI startup,
ROM/RAM fit and resource lifetime. Estimate the remaining feature work after that
probe; provider integration and compiler closure are the largest uncertainties.

## Budgets to freeze at D0

These are proposed targets for implementation, not measured results or changes
to the existing performance protocols. Freeze the named minimum/reference PCs,
runtime and measurement method before collecting release evidence.

| Budget | Proposed criterion |
| --- | --- |
| Hardware and media | Complete native desktop journey fits the existing 1 MiB A500 and 901,120-byte system image. Record free Chip/slow bytes and largest free block at boot, after library initialization, during Editor save and child launch. |
| ROM | Complete selected ROM, including vectors/data/reservations, fits 524,288 bytes. No payload-only size claim. |
| Workspace capacity | Reserve at least 64 KiB free in a fresh seed for one 16 KiB document, temporary/backup copies and filesystem overhead; verify this with a real save and replacement. |
| Startup | Target usable Home within 60 seconds of cold boot at normal emulated speed on the named reference PC; record guest fields and wall time separately. |
| UI | Target ordinary focus/selection/caret feedback within 150 ms; show busy feedback within 250 ms for disk operations. Measure boot, directory reads, save and executable loading separately. |
| Lifetimes | After declared one-time library/cache initialization, 100 window and 100 child launch/exit cycles leave owned allocation counts, pins, tasks, ports and signals at baseline. Record fragmentation/largest free block as well as total bytes. |
| Sustained use | A 60-minute installed session completes repeated browse/edit/save/launch cycles without crash, progressive memory/log growth or stuck input. Assess real-time audio delivery separately for advertised audio content. |

If code/image/memory budgets fail, first reduce reachable code, simultaneous
buffers and unnecessary library dependencies. If the bounded target still cannot
fit, record the measured deficit and make an explicit product/profile decision;
do not silently enable Fast RAM, expand the ROM or relabel a different machine.

## CopperScreen integration and qualification

Create a new desktop pair descriptor/schema and evidence record. Preserve the
frozen CLI descriptors and historical results. Bind ROM, OS image, Desktop,
Shell, Demo, required libraries, SDK/toolchain and input scripts to versions and
hashes. Update the app's firmware/OS admission only after the new pair passes its
native tests; the present graphics record alone does not admit a new app ROM.

Define old-workspace support explicitly. A new seed must not overwrite a user's
existing disk. Either the new runtime admits the old pair and keeps its CLI
behavior, or it offers a separate new desktop workspace with export/import or a
qualified migration performed on a retained clone. Do not boot an unknown pair
or assume an app update installs missing desktop files into an existing image.

Extend source/native fixtures with an original deterministic desktop journey:

1. Cold boot to Home and verify the actual running pair and native display.
2. Launch Demo, inspect interaction, exit and use the desktop again.
3. Open Shell, run a command, exit and verify restored GUI input.
4. Browse/create a folder, create a document containing a known text fixture,
   save, edit, replace, rename and read it back.
5. Cancel deletion, confirm a different deletion and inspect the resulting tree.
6. Stop/restart the app and read exact saved document bytes through the guest.
7. Exercise full/read-only disk, failed host checkpoint, failed library/program
   load, low memory and interrupted editor replacement, then recover work.
8. Repeat the native journey through an actual package update and an external
   workspace backup/import. Verify original inputs remain unchanged.

Automate native keyboard/mouse input, inspect real pixel milestones and bind
post-run filesystem content to hashes. Use scalar controls for affected engine
regressions; matching two execution modes does not independently establish OS
ABI compatibility. Keep host semantics, compiled MC68000 execution, native boot,
desktop interactions, installed checks and throughput as separate results.
Optional skips mean unavailable coverage.

For itch.io, test the final self-contained Windows ZIP through browser extraction
and the itch app using a clean standard-user account. Verify offline startup,
runtime/native dependency inclusion, real keyboard/mouse, capture/release,
fullscreen, supported DPI, controller/audio device transitions and update data
retention. MSIX evidence remains useful for its own channel but does not qualify
the itch ZIP. Keep Store eligibility/certification on the existing Store track.

Use the standard production build, host/desktop/disk tests and separate engine
diagnostics for implementation changes. Use [release qualification](RELEASE_QUALIFICATION.md)
and [performance guidance](engine/PERFORMANCE.md) for sustained and throughput
work; a desktop smoke run is not performance acceptance. Finish distribution
rights, exact inputs available to clean CI, notices, support and recovery help
before shipping. Do not commit generated ROMs, native libraries, images or media.

## First work items

1. CopperStart: use the selected TextInputEntry/library/event contract for the
   desktop. Keyboard text, custom layouts and release paths are implemented;
   extend Home keyboard navigation to the file/document views, then finish visible layout selection, platform capture
   gestures, broader dynamic subscriptions and the 100-cycle stress gate. The bounded
   [joined refresh/input and shared-port journey](engine/COPPERSTART_INTUITION_JOINED_2026-10-18.md)
   now covers delayed replies, pressure, partial refresh and rollback.
2. CopperOS: the [native Desktop Process and Home](engine/COPPERSTART_COPPEROS_HOME_2026-10-18.md)
   now use one fixed screen/window and one IDCMP drain/reply/Wait loop, with
   native repainting and startup rollback. The required 512 KiB ROM fixes
   self-task MemList reclamation; the bootstrap retains its code/provider once
   per boot. The [native Files view](engine/COPPERSTART_COPPEROS_FILES_2026-10-18.md)
   now adds bounded DOS browsing, path navigation, selection/scrolling and rollback
   within this owner. The [native Editor follow-up](engine/COPPERSTART_COPPEROS_EDITOR_2026-10-19.md)
   now adds a case-preserving font, 16 KiB buffer, cursor/editing and transactional
   Files open. The [native Save follow-up](engine/COPPERSTART_COPPEROS_SAVE_2026-10-19.md)
   now adds Save/Save As, source-change checks, recoverable replacement and
   fresh-ROM byte-exact persistence. General movable windows and broader
   BOOPSI/Workbench admission can follow.
3. CopperStart/CopperOS: join Shell/Demo launch and return, releasing input/display
   ownership for the child and restoring it afterward. Reuse the retained
   provider module; qualify 100 child launches and low-memory/load failures.
4. CopperOS: the [native recovery follow-up](engine/COPPERSTART_COPPEROS_RECOVERY_2026-10-20.md) now adds
   read-only archive inspection, unsaved copies and cooperative busy
   refresh/input/close handling. The [native I/O follow-up](engine/COPPERSTART_COPPEROS_IO_2026-10-20.md)
   now qualifies private directory cursors, bitmap-word work and internal sector
   copies with all nine original-fixture Editor/Save/recovery checks passing.
   Individual DOS calls still block.
   The [guest latency/heap study](engine/COPPERSTART_COPPEROS_MEASUREMENTS_2026-10-20.md)
   now records isolated routine intervals and heap peaks for one instrumented
   journey, with 134,317 fields of passive/control parity and stable post-stop
   geometry. Lowest free is 218,696 bytes; the lowest largest block is 96,040.
   Modeled guest Home repaint is 10.8 seconds and a 16 KiB replacement is about
   18.4 minutes. Prioritize native disk I/O and planar drawing, preserving
   physical write/Close order, clipping, rollback and the same memory bounds.
   The [bounded track-cache follow-up](engine/COPPERSTART_COPPEROS_TRACK_CACHE_2026-10-20.md)
   now reduces 33-byte / 16 KiB replacements by 14.8% / 10.7%, preserving all heap
   minima and 125,714 fields of control parity. The 16 KiB replacement still takes
   about 16.4 modeled minutes; Home repaint remains 10.8 seconds. Continue native
   I/O and drawing work before more features. The development pair advances;
   the app firmware pin and shipping admission remain unchanged.
   The [native MFM follow-up](engine/COPPERSTART_COPPEROS_MFM_2026-10-20.md)
   now reduces replacements further to 214.1 seconds for 33 bytes and
   679.9 seconds for 16 KiB (31.0% / 31.1% below the cache result),
   with unchanged heap minima and 105,586 paired fields matching. Physical
   write/Close ordering and failure rollback still pass. Home repaint remains
   about 10.8 modeled seconds; continue planar drawing and remaining native I/O
   work before adding features. Shipping firmware admission remains unchanged.
   Broader queue-pressure peaks and installed response measurements remain open.
   Then add bounded Files mutation/paging beyond the
   current 32-entry cache and qualify sustained stress. Shell/Demo children must
   fit the 1 MiB budget; the existing full Shell requires extra RAM.
   The instrumented guest study does not complete installed or release measurements.
5. CopperScreen: freeze and qualify the new ROM/OS pair, workspace compatibility,
   restart/update persistence and the itch.io ZIP journey. Preserve the current
   app firmware pin until that separate admission succeeds.

The existing [Store productization roadmap](PRODUCTIZATION_ROADMAP.md) retains
its channel-specific requirements. This desktop plan supplies the concrete
graphical work for its product-scope and CopperOS integration tasks while also
defining the itch.io consumer journey.
