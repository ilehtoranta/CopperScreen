# Small MUI native interaction diagnostic

Protocol v30 waits for a laid-out, nonempty OK hit rectangle before clicking
the original Icon Update result requester, then waits for modal-object disposal
and native window unlinking before closing the main window. Earlier attempts
that clicked the zero-size initial rectangle are invalid driver coverage.
`requester-probe-fixture` separately exercises genuine guest `MUI_RequestA` calls;
its registered return witness does not replace the original utility/file checks.
Icon result-dialog waits are bounded at 120,000 fields. Retain older runner
binaries and their recorded results when building this protocol.

This correctness harness observes the pinned CopperOS muimaster.library 4.1 and 12.1 private ABIs. Current protocol v30 records the observed version and owner-operation count. It uses the public conservative A500 machine constructor, 512 KiB Chip + 512 KiB Slow + 4 MiB Fast, PAL OCS, a 68000 interpreter and read-only hardfiles. No host folder, guest-memory writes, OS shims or synthetic method calls participate. It is not a throughput acceptance measurement.

Build with `dotnet build scripts/probes/MuiUtilityInteraction/MuiUtilityInteraction.csproj -c Release --artifacts-path artifacts/mui-small-utilities-harness-v30`. The default references are the frozen Calc v4 engine dependencies. `FrozenRunnerDirectory` can select another explicitly qualified binary directory; keep engine and CPU hashes in every result.

Arguments: `ROM HDF new-output hdf-ro|hdf-ffs max-fields [menu-fixture|fault-stop|string-fixture|join-requester-fixture|join-prefs-fixture|popasl-fixture|icon-update-fixture]`.

Protocol v15 adds `icon-update-fixture`: type the owned RAM source and target
paths, click Start, require the utility's success requester, acknowledge it and
quit. UI success is separate from actual file evidence. The original executable
detaches into `MUI_Icon_Update_process`; Startup-Sequence continuing does not
mean its GUI has quit. The first v26 file snapshots were premature and cannot
qualify a write. Protocol v17 requires the revised guest after-command to observe
that named process running and then gone before reading the actual files. Use
`inspect_icon_update_fixture.py` v2 to require exact before bytes, unchanged source,
updated target pixels/type and preserved target position/stack. Inputs are created
for this test and copied to RAM through guest DOS; the hardfile stays read-only.

Protocol v16 adds `join-prefs-fixture`: open Prefs, capture the actual controls,
click Use and Quit. It chooses buttons from the currently open containing window.
This is preferences discovery, not a Split/rejoin byte-integrity pass.

Protocol v18 adds `join-picker-fixture`. It waits for two mapped, structurally
valid native StringInfo controls before cancelling the Split requester and
records their actual buffers, IDs and geometry in captures. Merely opening the
requester window does not establish that its file controls have been built.
This mode still does not split a file.

Protocol v19 stops on an unexpected exception whose PC points into the vector
table or beyond the 24-bit address space. These cannot establish utility
compatibility, including failures before MUI loads. Keep the first fault capture
instead of executing a fault storm. The expected ROM bootstrap and privilege
transitions retain their existing treatment.

File fixtures now use a bounded native DOS copy helper rather than the general
Copy executable. CopperStart's original v26 preparation faulted at PC 0x10 before
MUI loaded; retain that failed preparation separately. `--fixture-setup-only`
omits the utility, records the before bytes and parks with genuine Exec.Wait.
A generic runner MAX_FIELDS is not a fixture verdict: inspect the before-file
witness independently. No host memory writes or host filesystem bridges occur.

Image preparation's `--join-write-fixture` adds an owned 12,345-byte RAM input
and the original client's five-line preferences format (j/out/4096/4096/0).
`mui_join_fixture.py` supplies real DOS before/after readers; the after reader
retries until every expected file has its full length. It does not assume that
returning to Startup-Sequence means the utility has exited. The independent
`inspect_mui_join_fixture.py` checks every byte of four parts (4096/4096/4096/57),
the original input and the joined file. Fixture creation and opcode decoding
alone are not a guest file-operation pass.

`inspect_mui_layer_lock_cycle.py` checks the observed CopperStart two-task
paint/refresh lock cycle using recorded task/wait/Layer semaphore ownership.
Its diagnostic ABI is deliberately bounded; it is not a general stack unwinder.

Protocol v9 adds `string-fixture`: click the first visible empty String field
(top/left order), type `test` and Return with ordinary raw-key down/up input,
verify its contents through the read-only diagnostic ABI, then close normally.
It records `stringInputPassed` separately. This checks text entry, not the
application's file operation or acceptance hook. Build v9 into a fresh output
directory; keep earlier runner binaries frozen for their recorded replays.

Protocols v12 and later release raw keys after two fields. Earlier v9 typing
held each key for 1,000 fields, allowing ordinary key repeat and causing an
expected-text mismatch. Preserve those failed experiments; they are not typing
passes. Keep a longer interval between separate keypresses for guest processing.

Protocol v10 adds `join-requester-fixture`: click MUIJoin's Split button, wait
for a distinct native requester window, find its standard absolute-geometry
Cancel gadget and use hardware mouse input to cancel. Require the requester to
unlink, the original MUI window to remain linked and dispatch to unwind before
closing the main window. An unavailable gadget or requester is a failure or
unavailable coverage, never a cancellation pass. This does not split a file.

Protocols v11 and later admit a native ASL system close gadget as cancellation.
Protocol v13 also admits CopperOS Intuition's public CloseGadget flag plus its
header geometry: its drawn system gadget is handled directly and need not be
an application Gadget node. GadTools need not expose button text in GadgetText.

Protocol v14 adds `popasl-fixture`: click the first visible Popasl's actual
button, require Active while a distinct native requester is open, cancel by
hardware mouse input, require Active to clear and dispatch to unwind, then
verify main-window close and cleanup. This tests the asynchronous file-popup
path. It does not establish selected-file defaults, hooks or actual icon writes.

The default waits for a native window with no active MUI dispatch, observes another 5,000 fields and uses real hardware mouse input on the native close gadget. This is appropriate only when that application's close gadget actually quits. MUIMenu intentionally hides instead; use `menu-fixture` to look for the configured Test Utility button and then click QUIT. Missing buttons are captured and listed in `unavailableActions`; they are never counted as executed. `inputAndCleanup: PASS` describes only the accepted quit and verified cleanup, not complete utility functionality. A registry-empty observation is followed by 5,000 fields and a bounded check that the original native window is absent from its screen list.

The optional `fault-stop` observes private frozen CPU diagnostic properties through reflection. Fault sites are captured once per vector/PC. Ordinary supervisor-entry privilege traps and ROM bootstrap probes are not startup failures. A new non-privilege RAM fault with live MUI objects stops the run. This supplements screenshot review; it is not a proof that no earlier fault occurred.

The image preparation script is `../UtilityStartup/prepare_small_mui_utilities.py`; it verifies both base image hashes and original archive hashes, installs every original archive member unchanged, and checks the complete guest filesystem inventory after readback. CopperStart MUImem additionally runs the existing CopperFfp installer. Run Python with the local lhafile/amitools environment. All original archives, ROMs, private hardfiles, generated guest commands and captures remain ignored local artifacts.

`../UtilityStartup/inspect_small_mui_compatibility.py` reads completed captures only. `manual_stack_delay_probe.py` prepares an initial DOS Delay diagnostic; `manual_stack_timer_probe.py` prepares the stronger asynchronous timer WaitIO comparison. Pass `--normal-stack` after the output directory for its normal-stack control. Both publish a copied named Exec resource; use `inspect_manual_stack_probe.py` on a completed capture to read it. Its generic harness MAX_FIELDS result is not the probe verdict: inspect the resource's stage (1 = entered the tested wait, 3 = returned and freed the allocation) and recorded task bounds.

The dated compatibility report in docs records exact captures, hashes, failed harness experiments and unavailable operations. Do not replace failed evidence with a later pass.

Protocol v8 requires empty object and class registries **and** zero native owner
operations before starting the 5,000-field settling period, including the final
menu-button release path. Older protocols could enter settling while class
retirement was still active and incorrectly report activity after quit. Preserve
those raw results and report the verifier limitation; do not relabel them passes.
Captures also include user and supervisor stack pointers. The read-only
`inspect_muimem_code_guard.py` checks the original helper damaged by the manual
stack failure against the pinned original executable, without patching either.

Use image preparation's --menu-witness option to install the original native MenuWitness command. After a menu launch replay, inspect a completed capture with inspect_menu_launch_witness.py. A clicked button alone does not establish that the command executed.

`mui_library_presence_probe.py` and `inspect_mui_library_presence.py` record and
inspect actual guest OpenLibrary results in a copied Exec resource. A generic
runner MAX_FIELDS is not the probe verdict. `inspect_muimem_layout.py` checks
the known static label widths and heading separation in the default Topaz-8
diagnostic profile; visual review and live-counter testing remain separate.

`exec_availmem_probe.py` and `inspect_exec_availmem_probe.py` perform actual guest
Exec calls and verify copied TOTAL/type-filtering/allocation/LARGEST observations.
`inspect_muimem_maximum_counters.py` v2 compares the visible stable Maximum row
with the native Kickstart formula (MemHeader Upper - Lower + 32). Its initial
v1 omitted the 32-byte header and is not the validated oracle. Free/used/largest
snapshot comparisons remain separate because the app and capture are not atomic.

Protocol v20 adds `join-write-fixture`: use the original MUIJoin with an owned
`RAM:muijoin` drawer, select the source through real ASL Drawer/File controls,
split it, select its four part rows with mouse/Shift input, rejoin, and quit.
The private directory contains exactly the source and four parts before rejoin.
Row selection is bounded to the observed ASL 40.6, Topaz-8 geometry and needs
visual review of the captured five-file list and selection. Missing required
actions or controls fail the protocol. Actual guest DOS readers snapshot source,
parts and joined file; `inspect_mui_join_fixture.py` must independently verify
every byte. GUI cleanup alone is not a file-operation pass.

The icon-write fixture similarly snapshots native DOS reads before and after
the original program's detached process has exited. Its independent verifier
checks the copied image and type, unchanged source and preserved target position
and stack size. Preparation uses guest DOS calls; no guest RAM is host-patched.
Both file protocols keep their inputs and writes in owned RAM fixtures and mount
the backing hardfiles read-only. Setup-only probes park in Exec.Wait and have
their own byte verdict; MAX_FIELDS on those probes is not utility success.

Protocols v19 and later preserve the first observed vector-table or out-of-24-bit
PC fault and stop, except expected privilege-entry traps. Earlier binaries and
their output directories remain frozen. Native functional limits are correctness
bounds and provide no host-throughput acceptance evidence.

Image protocol v2's `--respect-icon-stack` reads the unchanged executable's
DiskObject StackSize and sets the following CLI command's stack preference with
`native_stack_preference.py`. Use `inspect_utility_task_stack.py` on the idle
observed capture to check the native window port owner's public Task bounds.
This does not prove a manually selected SP follows those advisory bounds.
Preserve the earlier undersized-stack failures; no larger-stack launch is a
pass until its independent functional checks and cleanup complete.

The optional v21/v22 scalar trace preserves interrupt sampling and hardware
advancement while recording instruction boundaries. `MUI_TRACE_FILE_ACCEPTANCE=1`
traces the Split filename return; `MUI_TRACE_GLOBAL_LONG` and
`MUI_TRACE_GLOBAL_EXPECTED` select a read-only physical Chip/Fast RAM watch
(hexadecimal addresses/values) after Split is pressed. The trace stops before
executing a low/unpeekable PC or after observing an unexpected nonzero global.
These are diagnostic stopping conditions, not emulated exceptions or a normal
replay pass. Trace output is separate from the ordinary frozen v20 runs.
