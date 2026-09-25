# Lotus III disk-two prompt investigation — 2026-09-25

Follow-up: the [CPU bus-spacing correction](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md)
passes the original prompt and reaches driving demos. The initial investigation
below is retained as evidence of the failure before that correction.

Status: **OPEN — the supplied release loses the fire edge in its prompt loop.**
The disk-controller diagnosis is not established. No engine, CPU package or guest
code was changed during this investigation.

## Reproduction and scope

The current Lightweight candidate reproduces the corpus failure with the supplied
FLT / Crack Inc two-disk ADF release, Kickstart 1.3, PAL OCS, 68000, 512 KiB chip
and 512 KiB slow RAM. The untouched 18,000-field replay remains at the disk-two
prompt: PC `$741B6`, cycle `2557704034`, cylinder 17 / head 0, zero remaining disk
DMA words and no unsupported-feature stop. The original script ejects disk one at
field 5000, inserts the exact extracted disk two at 5100 and first presses fire
and the mouse button at 6500. See the [original corpus](NATIVE_CORPUS_2026-09-21.md)
and [input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3.json).

The unchanged frozen engine DLL has SHA-256
`7EB9C87905BD8CA790DFF21BB918A0B34F7E4B94FE8489F673058BC4D1581FDC`;
the Copper68k 1.4.1 DLL has
`F82CA4A5FD953B0BEE31EE360212070548DB0DAA80874EEEC36DB0EFD1F1AFE1`.
Media identities, local evidence paths and hashes are in the accompanying
[evidence manifest](LOTUS_III_INVESTIGATION_2026-09-25.json). ROM, media, RAM dumps
and screenshots remain local artifacts.

Normal and scalar runs match all 28 checkpoint files through 7000 fields
(state, framebuffer, chip RAM and slow RAM at each 1000-field checkpoint).
This excludes the current batching optimization as the source of this observed
divergence; it does not establish hardware timing correctness.

## Where the press disappears

The interrupt handler reads the joystick fire pin at `$75C6C` and constructs the
new-press bit at `$75C92`. The host press reaches those instructions correctly.
The prompt loop then clears that bit before testing it:

```text
$69B42  CLR.B  $81A(A3)       ; new-press flags
$69B46  JSR    $741A6
$69B4C  TST.W  $742(A3)       ; completed fade/wait
$69B50  BEQ    $69B42

$741B0 BTST   #4,$81A(A3)    ; fire edge, reached after timer comparison
$741B6 BEQ    $741BE
$741B8 BSR    $741C2         ; initiate completion when edge survives
```

At field 6500 the external instruction probe records:

| Event | CPU cycle | Observation |
| --- | ---: | --- |
| Interrupt dispatch begins | 923643642 | Beam 248 / CCK 11; interrupted PC `$69B42` |
| Handler entered | 923643686 | PC `$704A0` |
| Fire pin read | 923644098 | PC `$75C6C`; controller pins `$3F`, fire asserted |
| Edge about to be stored | 923644200 | PC `$75C92`; calculated edge `$10` |
| Handler has returned | 923645412 | PC `$69B42`; byte `$C0881A` is `$10` |

Executing the returned-to `CLR.B` discards that edge. At the following interrupt,
held fire generates no new edge. That interrupt returns to `$741BE`, after the
test; the next loop clears any pending edge again. The trace prints two-byte
values for the byte flags, so byte `$10` appears as word `$1000` there.

The disk-ID call at `$69B5A`, followed by comparison with `LFC2`, lies after this
wait. The observed failed press has not reached it. This is concrete evidence
for an input-consumption race, not evidence that disk two failed its ID check.

Changing the first Return press by a field, using later fire pulses, holding a
direction before pressing fire and trying 24 short fire pulses did not advance
the bounded native replays. No guest-memory patch, artificial CPU delay or
spindle-phase adjustment was used for these results.

## WinUAE comparison and its limits

Installed WinUAE 6.0.3, with the same ROM/media and PAL OCS 512+512 KiB profile,
compatible 68000 execution, CPU/memory/blitter cycle exact enabled and JIT off,
passed the prompt and reached the road background and high-score screen. This
establishes that the supplied disks can advance on the reference. It does not
verify driving or a complete game.

A later reference restart also missed presses at the prompt. The polling code
matches the native disassembly. A reference DMA trace shows the Copper writing
`$8010` to INTREQ, IPL becoming 3, and an interrupt frame saving PC `$741AE`.
That return location is before the fire test and permits consumption of an edge.
The native sample instead saves `$69B42`, which discards it. These are separate
runs, not identical initial CPU/prefetch states or a first-divergence proof.
WinUAE's DMA display coordinates also need normalization before comparing beam
phases numerically with the native instruction-boundary trace.

The combination suggests that instruction/bus/interrupt timing affects which
part of this narrow polling window receives the edge. It does **not** identify
an incorrect CPU instruction duration, IPL sample point or Copper event yet.
The original software race and a possible emulation timing error must remain
separate findings.

The official [WHDLoad Lotus 3 package](https://www.whdload.de/games/Lotus3.html)
was inspected as supporting research. Its source replaces the disk-insertion
path. That bypass is not a hardware correction or proof of this race's cause;
no WHDLoad patch was applied to the native replay.

## Next discriminating work

Reduce this polling loop and its Copper-triggered input interrupt to a small
media-independent probe. Compare instruction bus transfers, prefetch and IPL
recognition from matched starting states against the pinned WinUAE reference
and applicable 68000/hardware evidence. Correct the first independently justified
divergence in Copper68k or Lightweight, then replay the original untouched disks.
Do not adjust interrupt timing merely to land inside this title's polling window.

No compatibility fix, gameplay pass or throughput acceptance is claimed. This
record and the issue classification are documentation changes; validation was
the bounded native/reference investigation, checkpoint hashing and link checks,
not a new emulator unit-suite or formal performance run.
