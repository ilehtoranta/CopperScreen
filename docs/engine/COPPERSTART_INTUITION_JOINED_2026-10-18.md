# CopperStart joined native refresh and input — 2026-10-18

The selected compact Intuition owner now has one native application receiving
refresh and physical input on a caller-owned port shared by two windows. See the
[producer contract](../../../CopperStart/Docs/NativeIntuitionJoined.md) and
[machine-readable evidence](COPPERSTART_INTUITION_JOINED_2026-10-18.json).

The combined gate passes **14/14**, zero skips, with **281 checked PNG captures**
across 33 screen lifetimes. Eleven native ROM/media journeys cover startup,
refresh, messages, input and the joined dispatcher; three freestanding payload
checks cover the provider and two guest applications. The provider's actual
native compilation is retained separately and reused through verified hashes.
The final producer production build passes with zero warnings/errors.

The selected portable Layers suite separately passes **526/526**. A broader
534-case audit passed 533 and could not run one unrelated source-generation
check because `build/Generate-LayersNativeDirectVectors.ps1` is absent. Its
generation-test class is excluded from the portable selection; the unavailable
check is not recorded as a pass.

## Changes

Refresh records now capture native qualifier state and window-relative mouse
coordinates at generation. Those values remain stable while the application
holds the notification. Refresh reservations remain independent of the fixed
sixteen-record input pool and use the common native reply worker.

The joined scenario found a Layers reserve bug: a desktop, two separate windows
and an interior cover exceed the former sixteen-fragment small-topology reserve.
The reserve now uses the existing conservative overlap estimate. Prepared-list
alias checks also reuse already-validated private descriptor pointers under the
topology lock, retaining all overlap checks and avoiding repeated TypeOfMem calls
inside the pairwise comparison.

The guest dispatcher handles real REFRESHWINDOW, RAWKEY, mouse, focus and close
messages, calls BeginRefresh/EndRefresh, replies through Exec, and waits on the
shared port's actual signal. The host supplies physical keyboard/mouse events and
private capture acknowledgements. It does not inject IDCMP messages or provide
replacement OS services.

## Checked behavior

- A reserved refresh notification is delivered while all sixteen input records
  are occupied and all subsequent AllocMem calls are refused. Raw-ring overflow
  recovers; repeated close requests coalesce.
- A delivered refresh record survives additional exposure, with a separate
  notification for the other window. Held Shift and mouse coordinates remain
  those captured when the message was generated.
- Failed refresh projection suppresses application drawing and rolls back its
  allocations. Partial refresh retains damage; completion repairs the remaining
  area and cancels redundant notification. Physical focus input during an open
  update completes after EndRefresh releases LayerInfo.
- Shared-port subscription failure preserves the former port and mask. Failed
  window deletion preserves its subscription, port and memory balance.
- Shared-port cleanup removes only the closing window's queued messages. The
  survivor receives physical keys queued during cleanup. Delivered input and
  refresh records remain owned until their actual replies. The caller's port
  survives both window closures.
- Three screen lifetimes per width restore the console and heap baselines;
  input teardown restores the Schedule vector, console capture ownership,
  worker resources and provider pins.

## Frozen profile and limits

The ROM remains **524,288 bytes**, SHA-256
`60565368B67150BC17F42D1D9135A7942AF2260D31B9306FC0B1105F026FAEDA`.
Its compiled root is 465,206 bytes, with 31,906 bytes of graphics at `$FF8000`,
842 contiguous tail bytes and a separate 26,306-byte gap. The provider remains a
disk HUNK; this change does not put GUI services into the ROM.

The selected provider HUNK is **250,800 bytes**, 242,982 bytes of code. The machine
remains PAL OCS/MC68000, **524,288 Chip + 524,288 slow RAM, zero Fast RAM**, DF0.
The joined probe measures a 35,024-byte input installation delta and restores its
baseline after teardown. This is an installation balance, not a complete desktop
peak-memory budget.

Qualification binds the 133-file source snapshot, frozen compiler/SDK/runtime
inputs, ROM and native HUNK identities. Current additional Intuition class and
palette work is outside this selected provider profile. The entire provider HUNK
stays loaded until reset. The CopperScreen shipping firmware pin is unchanged.

## Remaining desktop work

Finish keyboard layout/text translation and host capture/focus-loss release.
Build the native Desktop process with fixed Home, Files and Editor views and one
drain/reply/Wait loop. Join Shell/Demo launch and return with explicit input and
display ownership transfer. Qualify writable storage on this exact ROM, the
100-cycle application/child stress gates, complete loader/stack/queue memory
peaks, GUI interaction latency and the shipping ROM/OS-pair restart/update/itch.io
journey. See the [desktop implementation plan](../COPPEROS_DESKTOP_PLAN.md).

The profile retains the earlier cooperative standalone wakeup boundary; it does
not establish general interrupt-context Exec.Signal behavior or broader
Workbench/Intuition compatibility. Captures and emulated-field counts are
correctness evidence; no formal host-throughput result is claimed.
