# CopperStart: positioned windows, Boolean gadgets and Hired Guns

Follow-up: [native gadget lists and verified Hired Guns graphics startup](COPPERSTART_HIRED_GUNS_GRAPHICS_2026-10-19.md).

2026-10-19. The original Hired Guns warning requester now draws and accepts
both buttons. Cancel restores the CLI; relaunching and accepting the warning
hands control to the original game loader. The post-takeover graphics startup
and gameplay remain **unqualified**.

The [machine-readable evidence](COPPERSTART_HIRED_GUNS_WINDOWS_2026-10-19.json)
records exact source, ROM, build, native controls, media, physical input and
capture identities. This continues the [CIA resource follow-up](COPPERSTART_HIRED_GUNS_CIA_2026-10-19.md).
Source is committed at
[`42432b7`](https://github.com/ilehtoranta/CopperStart/commit/42432b78c7f6903fd3e1b3d6be6bbf3003c85379).

## Native window and gadget behavior

OpenWindow's former full-screen, borderless and no-gadget restrictions are
replaced by actual guest implementation. Classic Layer/ClipRect records give
positioned windows separate border and GIMMEZEROZERO inner origins and clipping.
An owned saved bitmap restores the covered background on close. Public WLayer
is at +124, IFont remains at +128 and GZZ mouse/dimension fields keep their
Classic offsets. The final ABI audit corrected an intermediate field mixup.

Native DrawBorder/DrawImage, gadget list/refresh calls, OnGadget/OffGadget and
Boolean selection/input operate on caller-owned structures. Image PlanePick and
PlaneOnOff map to planar blits; rectangles use clipped byte fills. Immediate and
release-verify messages carry the original Gadget pointer in IAddress. Relative
geometry, disabled and toggle flags are interpreted without private allocation,
zero-link or arbitrary chain-length admission requirements. SetWindowTitles
implements -1 leave-unchanged and NULL title semantics and updates the display.

The original utility exposed the missing SetWindowTitles call immediately after
OpenWindow began succeeding. A read-only instruction trace identified that
entry point; no title-specific workaround or guest memory injection is used.
The implementation remains emitted MC68000 code, with real Exec ports,
messages, allocation and input. No host OS shim or hardware engine code changes.

## ROM and independent controls

The 524288-byte rebuilt ROM SHA-256 is
`5cf0f8821ae15072a97ff2bda4326e9e0318daa8ed05d5f5c7f0b34dacfbe32a`.
It reuses the unchanged main body freshly compiled in the preceding CIA step
(467130 code bytes). This continuation rebuilds the native composition, with
10732 Intuition bytes at $FF4000, 1108 CIA bytes moved to $FF7B00 and 32300
graphics bytes at $FF8000. No fresh main compilation is claimed here.

The production Release solution build completes with zero warnings/errors,
using the recorded SDK root and existing project-reference outputs. Its emitter
produces identical whole-ROM bytes. All 12 qualification input hashes are checked.
The final exact-ROM suite passes **108 cases, zero failures and zero skips**.
Fourteen optional standalone polled-reader cases are excluded, not passes.

Two authored reset probes pass independently against native Kickstart 1.3 and
the rebuilt ROM. They cover ordinary/GZZ geometry, public layer identity,
clipped pixel results, normal/selected Boolean images, IAddress/code, replies,
title sentinels and background restoration. Image planes are allocated in Chip
RAM for native DMA; the first comparison probe incorrectly used slow RAM and
was corrected. The preceding CopperStart ROM fails both probes at OpenWindow,
as expected from its old admission rules. Intermediate drawing and ABI candidates
are diagnostics, not qualification of this final image.

## Original disk replay

Normal and scalar runs cold-start original Hired Guns v1.08.39.25 disk 1 on the
bounded PAL OCS 68000 A500: 512 KiB Chip/512 KiB slow RAM, one read-only floppy,
RTC disabled. Only disk 1 is mounted; other disks in the launch inventory are
metadata. All actions are physical keyboard/mouse events.

At field 12000 the original warning, text, icon and two buttons are visible.
Cancel SELECTDOWN/UP completes at 12090 and the CLI is restored by 12200. The
same original command is typed again from 12300. The warning reopens by 18000;
Accept SELECTDOWN/UP completes at 18058. By 20000 the original loader has cleared
SysBase and installed its own exception vectors. By 25000 it has advanced to its
own graphics startup, whose coloured raster display is retained without a menu
or gameplay pass claim. An independent verified native-ROM launch is needed
before attributing that later display to the launch configuration, CopperStart
or chipset behavior. Full Workbench startup/workbench.task also remains open.

Both runs stop cleanly at 25000 with host exit zero. All **1255 capture files per
mode** match: BMP, CPU/hardware/cycle state, Chip RAM, slow RAM and PCM. No
captured unsupported hardware/video path is reported. The warning, restored CLI
and later startup captures were visually inspected. Agreement between modes
does not establish correct game graphics or gameplay.

The window qualification covers modal smart-refresh requesters fitting the
screen, an inactive background and reverse close order. Arbitrary overlapping
repaint, moving/sizing/depth system gadgets, string/proportional/custom gadget
input, menus and general Layers operations remain outside this surface. The
earlier CIA Kickstart 3.1 control discrepancy remains separate and unresolved.
No new Railroad replay or throughput measurement is claimed. ROMs, media,
local build binaries and captures are not committed.
