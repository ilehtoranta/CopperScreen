# CopperStart native Intuition refresh — 2026-10-18

The compact native Intuition owner now implements BeginRefresh/EndRefresh and
repairs exposed desktop and window frames through the installed Layers owner.
It retains the standalone 512 KiB ROM and 1 MiB PAL OCS A500. See the
[producer implementation](../../../CopperStart/Docs/NativeIntuitionRefresh.md)
and [machine-readable evidence](COPPERSTART_INTUITION_REFRESH_2026-10-18.json).
This extends the [startup/window slice](COPPERSTART_INTUITION_STARTUP_2026-10-18.md).

The exact-input qualification passes **6/6**, zero skips: one freestanding
compilation control, two startup journeys and three refresh journeys. Six
startup lifetimes and nine refresh lifetimes check 125 captured frames against
planar memory and displayed pixels. Native allocation faults cover all three
selected damage-projection boundaries, automatic frame/desktop repaint and
failed CloseScreen recovery. Heap baselines, normal clips, flags, lock counts
and final provider pins are checked. The latest producer Release solution build
passes with zero warnings/errors. The prior 344-check Layers qualification is
retained prerequisite evidence and was not rerun here.

BeginRefresh locks LayerInfo before the window Layer and projects actual damage
clips. EndRefresh restores normal drawing and unlocks both. FALSE completion
retains the damage for another pass while clearing WindowRefresh/LAYERREFRESH;
any nonzero BOOL completes the update. The public refresh pair bypasses the
Intuition gate so EndRefresh can release locks awaited by a topology caller.
Partial sessions interleaved with topology changes still require the caller's
outer LayerInfo lock, as documented by the public API.

If BeginUpdate cannot allocate its damage projection, BeginRefresh publishes an
empty drawing clip while retaining the normal head privately. Rectangle/text
calls change no pixels; clipped text still advances its logical cursor once.
EndRefresh restores clips and locks, preserving damage and its outstanding
Refresh flag even when the caller requested completion. Caller Regions and
damage nodes are not rewritten or subjected to invented ownership checks.

The screen owns a Simple desktop Layer. Closing a window repairs exposed
colour-zero desktop clips and frame/title damage without erasing application
contents or changing its pen, mode or cursor. Frame damage remains for the
application's refresh. Failed projections retain damage for retry; the next
application BeginRefresh also retries frame repair. A failed desktop deletion
makes CloseScreen return FALSE before retiring the View or freeing its owner.

The current scope is fixed Simple windows, real cover/uncover damage and public
native graphics calls. The code and all HUNK segments remain loaded until reset.
The selected source snapshot includes concurrent preferences and Workbench/input
work; this refresh result does not qualify those public APIs or their entry.

The unchanged ROM is 524,288 bytes, SHA-256
`DE0C6C8BE7395788081EF26E9B50FCC01DB912FD2BFA8A7924A7230F4979133F`.
It retains 842 contiguous tail bytes and a separate 27,854-byte gap. The disk
payload is 234,916 bytes (227,440 code bytes), SHA-256
`7ADCC9816C2593506A87D02B70748A4DD847A35EF8B745ACA29FEDEEC77B5652`.
The seven payload inputs and eleven ROM producer inputs are hash-checked. The
54-file source manifest is
`40D75587BB6A6871AC676D82321088FE309C89A3CBEA2C65D7FD24DFA92A6FB2`.
Later working-tree changes are outside this snapshot; the earlier unsuccessful
compiler attempt remains retained.

The machine uses 524,288 Chip bytes, 524,288 slow bytes and zero Fast RAM.
Minimum free bytes across checked refresh capture points are:

| Case | Chip free | Total free | Largest Chip block | Largest slow block |
| --- | ---: | ---: | ---: | ---: |
| 320 | 211,736 | 496,112 | 178,968 | 268,240 |
| 640 | 191,256 | 475,632 | 158,488 | 268,240 |
| 640, repaint failures | 191,256 | 474,952 | 158,488 | 264,920 |

Live-screen heap baselines follow one desktop uncover/refresh warm-up because
Layers retains its packed damage rectangle capacity after complete refresh.
Each failed refresh restores that exact baseline; full screen teardown restores
the pre-screen baseline. These capture points are not transient peak budgets.

Refresh messages are the next dependency: coalesce IDCMP_REFRESHWINDOW after
successful system frame repair, retain pending work on failure, implement
subscription/NOCAREREFRESH policy, GetMsg/ReplyMsg, wait/wake and queued/delivered/
replied teardown. The native replay invokes the refresh pair directly and does
not establish an event-driven application.

Physical keyboard/mouse routing, pointer, focus/activation, close events,
qualifiers, key-up/repeat and queue-pressure recovery then need a native journey
on the same admitted composition. Contended lock wait/resume, disjoint damage,
foreign clip regions between partial passes, callback continuation, Smart
Intuition windows, moving/resizing and GimmeZeroZero remain separate checks.
Measure complete loader/transient peaks, GUI stacks and event/input queues.

This is producer correctness evidence. The shipping app firmware pin is unchanged;
no new ROM/OS pair, package publication or throughput result is admitted.
