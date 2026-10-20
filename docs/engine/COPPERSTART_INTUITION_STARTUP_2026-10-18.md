# CopperStart native Intuition startup — 2026-10-18

The producer now creates a real native screen and first decorated window on the
retained standalone 512 KiB ROM and 1 MiB PAL OCS A500. Its separate disk entry
uses real Exec/DOS, installed Utility/Layers and graphics vectors. It does not
use the ROM Intuition capability fingerprint or admit the older IR01 private
actor-query implementation. See the [producer implementation record](../../../CopperStart/Docs/NativeIntuitionStartup.md)
and [machine-readable evidence](COPPERSTART_INTUITION_STARTUP_2026-10-18.json).

The selected classic NewScreen/NewWindow journey uses 320/640 × 256, two
bitplanes and a 240 × 90 Simple-refresh window. It draws title text, close
decoration and a clipped rectangle, owns an empty IDCMP_CLOSEWINDOW port, closes
the window/screen and restores the console's complete frame. Three lifetimes
pass at each width. The close decoration is imagery; physical close-event
delivery is still separate work.

Startup borrows compatible providers and rejects old ones. Failed starts close
acquired pins and roll back only newly installed prerequisite owners. The
Intuition owner supports deferred expunge, with live graphics/Layers pins and
zero final pins. Its generated veneers return through retained module data;
a native probe clears the exact reallocated expunged block before return.

The retained gate passes **3/3** checks without skips: an exact-input payload
control and two native ROM/media journeys. The same matching graph was compiled
and replayed separately without managed allocations/runtime helpers. Each width
checks three prerequisite allocation failures, five Intuition provider failures,
an existing old Intuition owner, seven screen and twenty-three window allocation
failure boundaries, plus a port-creation failure after the Layer exists. Every
checked refusal restores its heap baseline. Six window images are checked
against both planar memory and displayed pixels; eight console frames establish
baseline/restoration. The Release solution build passes without warnings/errors.
The earlier [344-check owned-Layer result](COPPERSTART_OWNED_LAYER_UPDATES_2026-10-18.md)
and its 88 host semantics are separate retained evidence, not rerun counts.

The ROM remains `DE0C6C8BE7395788081EF26E9B50FCC01DB912FD2BFA8A7924A7230F4979133F`:
524,288 bytes, 463,658 root code bytes, 31,906 graphics bytes, 842 contiguous
tail bytes and a separate 27,854-byte gap. The new disk HUNK is 231,052 bytes
(223,628 code bytes), SHA-256
`C4F08698F13A3433BED8CA2F1BB14807934F57B98331EE7379ECC22CE5EAB314`.
The loader retains every HUNK segment until reset, including after library
expunge. The original eleven ROM producer inputs and seven payload inputs are
hash-checked. Fifty retained source/build files identify the exact snapshot.
Subsequent working-tree Workbench/public-screen additions are preserved outside
this snapshot and are not admitted by this result.

Minimum checked **live window** free memory, in bytes:

| Width | Chip free | Total free | Largest Chip block | Largest slow block |
| --- | ---: | ---: | ---: | ---: |
| 320 | 215,552 | 508,056 | 182,784 | 274,528 |
| 640 | 195,072 | 487,576 | 162,304 | 274,528 |

The machine has 512 KiB Chip, 512 KiB slow and zero Fast RAM. These points
include the retained code/32 KiB probe stack, bitmap, Layer, window and port;
they are not transient peaks or the complete desktop/event/input budget.

The next slice is BeginRefresh/EndRefresh, Refresh-flag retirement and desktop/
frame repainting while the screen stays open, followed by IDCMP refresh/close
delivery, replies and wait/wake. Physical keyboard/mouse routing, pointer,
focus/activation and outstanding-event teardown then need a native journey on
this exact composition. Multiple windows, disjoint damage, tagged callers and
Smart Intuition windows remain separate qualification. Existing Workbench/input
source is a separate entry and is not covered here.

This is producer correctness evidence. The CopperScreen app firmware manifest
is unchanged; this does not admit a new app ROM, original Workbench startup,
package publication or host-throughput result.
