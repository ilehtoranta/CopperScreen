# CopperStart native keyboard and mouse — 2026-10-18

The compact disk Intuition owner adds an explicit physical input profile via
`copperstart.intuition.input` / `NativeDesktopProviders.InputEntry`. See the
[producer contract](../../../CopperStart/Docs/NativeIntuitionInput.md).
The final combined gate passes **11/11**, zero skips: two actual freestanding
compilations and nine native ROM/media journeys. It checks 225 captures across
27 screen lifetimes, including six physical-input lifetimes at 320/640 pixels
and 44 input/console captures. See the [exact evidence](COPPERSTART_INTUITION_INPUT_2026-10-18.json).
This result does not admit a new shipping ROM/OS pair.

The native console remains the sole CIA keyboard reader. Its raw handoff feeds
RAWKEY, key-up, modifier qualifiers and repeat. VBlank samples the mouse and
clock; a retained native Schedule wrapper signals the common Intuition worker
from task context. Both the worker and application use real Exec.Wait. This is
the bounded cooperative standalone composition. General interrupt-context
Exec.Signal behavior remains outside this qualification.

The profile supplies a hardware pointer, window-relative coordinates, active
and inactive title repaint, focus transitions, three mouse buttons and close
requests on release inside the close gadget, with release-outside cancellation.
REPORTMOUSE and RMBTRAP control movement/right-button delivery; the default
unreplied movement/repeat limits are independently five and three. A fixed pool
of 16 notifications shares the native reply worker with
refresh. Bounded raw/mouse queues discard intermediate input on overflow and
reconcile held state after replies resume. Pending focus coalesces to the final
state, and repeated close clicks coalesce under pressure.

Tentative context/sprite/task/port allocations and refused hook installation
roll back. Teardown releases capture, removes the IRQ, restores Schedule and
disarms sprite zero before returning to the console. Context memory remains
retained while a suspended wrapper or delivered message references it.

The earlier ROM predates the raw-key handoff. This profile selects the retained
console-transport ROM, SHA-256
`60565368B67150BC17F42D1D9135A7942AF2260D31B9306FC0B1105F026FAEDA`.
It remains 524,288 bytes: 465,206 root bytes, 31,906 graphics bytes at 0x78000,
a separate 26,306-byte gap and 842 contiguous tail bytes. The runtime target
remains MC68000/PAL/OCS, 512 KiB Chip plus 512 KiB slow RAM and no Fast RAM.
System, Devices and DOS producer DLLs differ from the earlier ROM; its evidence
is retained independently. The shipping app firmware pin is unchanged.

The selected HUNK is 250,580 bytes (242,766 code), SHA-256
`A507DA057498A501BBC358F248031DE8A9801A594DF1C51B886744B422F50BF0`.
Its 64-file frozen source manifest is
`F55ED6F4591C1C51A3333B4CFB67F9A9758A576228B50EF95F262DAE10B951C1`.
Compiler, ROM and runtime inputs are hash-bound. Concurrent class/palette and
broader Workbench changes are outside this selected owner. Earlier failed and
superseded attempts remain separate, and the old 344-check Layers suite was not
rerun here. The producer solution build passes with two shared-compiler CS8602
warnings and zero errors. There is no throughput result.

Next join refresh and physical input in one application event loop; this gate
checks their case families independently on the same payload. Finish the
chosen keyboard layout/text translation, host capture/release
and focus-loss key release. Extend delivered-input/shared-port lifetime and
contention cases, then run the planned 100-cycle stress gate. Join the original
CopperOS desktop process, fixed Files/Editor views, safe save/error recovery and
Shell/Demo launch/return. Measure loader peaks, stacks and retained queues, then
qualify the exact writable ROM/OS bundle and workspace/update journeys. See the
[desktop plan](../COPPEROS_DESKTOP_PLAN.md).
