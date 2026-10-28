# Lemmings: restoring the display after the crack intro

2026-10-17, **LWA-NATIVE-012**. This case uses the supplied two-disk 1991
Psygnosis/Spreadpoint release on a PAL OCS A500, 68000 interpreter, 512 KiB Chip
plus 512 KiB slow RAM, read-only DF0. **Both fresh CopperStart and native
Kickstart 1.3 complete the first level with 100% rescued** using the recorded
mouse input. The original fresh CopperStart build crashes when the crack intro
exits. The [machine-readable record](COPPERSTART_LEMMINGS_2026-10-17.json)
identifies the source, ROM/media/input/binary hashes, checks and reviewed
captures. Local evidence is under `artifacts/copperstart-lemmings-2026-10-17/`.

## First incorrect operation

The untouched intro calls `OpenLibrary("intuition.library", 0)` at `$30398`,
copies D0 into A6 at `$3039C`, and calls `RethinkDisplay` through `-390(A6)`.
The bounded ROM has no published Intuition library. OpenLibrary returns zero,
so the next call runs through `$FFFFFE7A`; the subsequent exception cascade
overwrites low vectors and leaves PC `$DFD54E73`, A7 `$00000008`. This happens
around field 601, before the main game loader. A valid library request exposed
a missing firmware service. No caller-data rejection was found in this case.

The fresh baseline ROM reproduces this failure through field 1,000 and is byte
identical to the preceding qualified ROM:
`5C5C0ADFBC4E12A59ECA2DD0236C40EFC633B71AB34190E88418F36BEA4FF4C9`.
The narrow trace and original generated-disk regression reproduce the failure
independently of the final game input sequence.

## Native firmware correction

[CopperStart PR #5](https://github.com/ilehtoranta/CopperStart/pull/5), commit
`244e2e969dea613cc078ec9a63d0fef32012333c`, publishes a small permanent native
`intuition.library` before the
boot-block handoff. It has the public base prefix, real Open/Close counts and
the empty-screen forms of `RethinkDisplay` and `RemakeDisplay`. With no owned
Intuition screens, these clear graphics' active-view pointer, stop the previous
Copper/bitplane/sprite DMA, and blank the display registers/background. They
preserve disk, audio, blitter and master DMA enables and the Amiga callee-saved
registers. Graphics lookup and balanced Open/Close run through guest Exec.

The frozen native graphics component does not implement `LoadView`, so the
empty display is restored directly by guest 68000 register writes. No title,
instruction, PC, media or host-service patch is involved. This remains a
screenless native profile: general Intuition tasks, screens, windows and gadgets
are unimplemented. Other slots in the bounded table trap; later vectors remain
unavailable. The portable/host Intuition owner graph is separate.

## Fresh build and coverage boundaries

This is a fresh native-root rebuild against the preceding frozen Exec, DOS,
device, SDK/compiler and native graphics components. It includes the committed
caller-data audit and CIA startup fixes. The task's disk-boot source is projected
into the qualification namespace; the added Intuition source is compiled
unchanged. The input manifests identify every frozen binary and source snapshot.
It is not a clean rebuild of the moving producer tree, nor a qualification of
the concurrent app/CopperOS storage or graphics development.

The original guest fixture covers a missing graphics provider and a synthetic
graphics base with an active view. It checks the display/DMA effects, D2-D7/A2-A6 and
stack preservation, and balanced library counts. This is native ABI coverage;
the synthetic provider is not a full graphics implementation. The media replay
uses the real frozen native graphics component throughout. Both exact-ROM
fixture cases pass, including the complete callee-saved register capture. The
two compiler-only native cases also pass with their initial fixture; the final
strengthened fixture's project builds without warnings. The full producer
solution builds with one existing sibling-compiler nullable warning. These
source-build inputs are separate from the frozen ROM qualification inputs.

The input is
[`copperstart-lemmings-level1-v1.json`](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-lemmings-level1-v1.json).
It leaves time for the intro, swaps disk 2 after its prompt, enters level one,
selects the digger and assigns it to a walker. Supply disk 2 unchanged at the
ignored `media/lemmings-disk2.zip` alias; its source/archive and entry hashes
belong to the case record. Earlier exploratory input which swapped disks during
the intro is retained separately and is not an engine failure or successful
replay.

The corrected 512 KiB ROM has SHA-256
`05936F0470B0CA1A425A527D073436544BAF185D5A2AF4A06F26D5D815961E6A`,
457,216 compiled code bytes and 26,098 separately linked native graphics bytes.
Its native-root gate reports no managed allocations or framework features.

Each ROM's two 24,000-field replays match **all 605 capture files** (CPU/state,
bitmap, Chip/slow RAM and sampled PCM) between the CPU modes. Reviewed
checkpoints show the disk prompt, menu, level, digger count falling to nine,
tunnel/fall and 100% rescue results. All 30 sampled gameplay images from fields
14,000 through 19,800 also match between ROMs. Three of the 61 images compared
from fields 12,000 through 24,000 differ at menu, level-briefing and results
animation, scrolling text or blinking-prompt phases; these were reviewed. No cross-ROM CPU/RAM
identity is claimed.

The new-ROM Nitro regression reaches driving gameplay through 14,000 fields
and preserves **all 71** screenshots from the preceding qualified CopperStart
run. That regression uses the normal CPU mode; the new Lemmings qualification
uses both modes. CopperScreen's production Release build passes without
warnings. The CPU remains the pinned `1.5.4-nitro-dev.2` package.

These are diagnostic correctness runs, not throughput acceptance. Full-game,
two-player and audio-fidelity coverage remain unavailable. ROMs, supplied game
media, generated disks, captures, packages and build output are not committed.
