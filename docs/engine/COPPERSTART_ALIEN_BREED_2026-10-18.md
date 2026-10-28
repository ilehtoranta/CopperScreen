# Alien Breed: retain the caller's trap-zero vector

2026-10-18. The supplied SPS 998 release's original game disks expose a native
Exec supervisor-entry conflict in the F17-qualified CopperStart ROM. Native
Kickstart 1.3 accepts disk two, reaches the menu and starts level one on the
same PAL OCS 68000 A500 with 512 KiB Chip and 512 KiB A501 RAM. The preceding
replacement ROM remains black through 18,000 fields. The
[evidence record](COPPERSTART_ALIEN_BREED_2026-10-18.json) identifies the exact
media, source lineage, build inputs, commands, probes and reviewed captures.

The loader temporarily changes trap vector zero while it still uses Exec.
Under CopperStart, its supervisor-mode instruction at `$285A0`, field 88,
writes a longword at `$7E`, changing the vector at `$80` from `$00C040AC` to
`$000240AC`. Native Kickstart sees the equivalent original instruction at
`$1768`, field 226, change `$00FC0836` to `$00020836`. Later disk code installs
its own trap handler. Changing that caller-owned vector is not itself a reason
to reject the loader or override its write.

The next Chip allocation is `AllocMem($40000, MEMF_CHIP | MEMF_CLEAR)`.
CopperStart enters it at `$286F4`, field 89. Its allocator calls `SetSR`, whose
old native entry uses an internal `TRAP #0` gate at `$C040BE`. The processor
therefore follows the changed vector into unrelated data at `$240AC`; the
allocation never returns. Native Kickstart's equivalent allocation returns
`$59E8`. Runner completion with no unsupported instruction does not turn the
replacement ROM's black screen into a successful game replay.

The native control's Exec `Supervisor` entry uses a privileged SR instruction
and the privilege-violation vector. The committed
[Beast correction, `9eed3fe`](https://github.com/ilehtoranta/CopperStart/commit/9eed3fe584549b36f0c5de175929e890ec620669),
already implements that approach in CopperStart. It recognizes the private
faulting instruction, provides the callback's real RTE frame, and chains other
privilege faults to the previous handler. An already-supervisor caller receives
the equivalent frame. The guest retains its trap-zero vector throughout.
The [Supervisor autodoc](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node059D.html)
documents the supervisor callback and RTE return contract.

This continuation integrates that committed native-ROM work with the F17 and
Lemmings fixes in producer commit
[`db8b8a1`](https://github.com/ilehtoranta/CopperStart/commit/db8b8a11deec969a31a35184f8558bf7d7bf819a).
Its parents are the F17 source and the Beast supervisor correction. The incoming
commits also contain the native bootstrap-memory and Exec interrupt foundation
used by that correction. This is reuse of an existing source fix; the independent
Alien Breed trace establishes why it is needed by the F17-qualified ROM.

The first integrated compile reaches the original `$FF0000` graphics region,
and its linker correctly rejects the collision. The private graphics resident
now occupies the upper 32 KiB at `$FF8000`, with its source installer recompiled
at that address. The overlap and checksum checks remain enabled. A focused
layout test rejects the overlapping boundary, accepts the required eight-byte
gap and verifies the resulting ROM checksum. This source layout correction is
committed as
[`a897cc3`](https://github.com/ilehtoranta/CopperStart/commit/a897cc31a5c4de4254725897673f5bbd73c93277).

The qualification image freshly compiles the F17/Lemmings disk root, rebuilt
integrated Exec and graphics installer using the recorded frozen SDK/compiler.
Ten other component DLLs, including DOS, devices, System and the graphics
composer, remain frozen. The record identifies all Exec source files and the
five root sources; the preceding four root files are unchanged. This does not
claim execution qualification for every source area in the merged producer
tree or admission of the standard app.

The final 512 KiB image was built at `2026-10-17T22:53:37.4468933+00:00`
(October 18 locally). Its SHA-256 is
`F755C8A0D00B1D99FA54071E0149C111F7BB5BFCE7BA58F55172EA0FD234AB62`,
with 459,318 compiled code bytes and 26,098 graphics bytes. Its native root has
zero managed allocation sites and zero framework features. In the corrected
trace, the loader still changes trap zero. Its Chip allocation now returns
`$A790`, followed by a successful `$60000` allocation at `$C07020`; neither
call depends on that vector.

While qualification ran, the F17 source was merged into current main. The same
supervisor emitter is separately backported in
[CopperStart PR #8](https://github.com/ilehtoranta/CopperStart/pull/8), preserving
main's independent task-completion bridge. All ten focused MC68000 tests pass.
Its Release solution builds with existing compiler and SDK-selection warnings.
GitHub CI cannot start: all 13 jobs have zero steps, with an account-payment or
spending-limit annotation. That is unavailable CI coverage, not passing tests.

The exact-ROM fixture uses original generated boot disks and real guest calls.
Its fitted/absent A501 cases preserve the F17 memory query and reservation
checks. Its caller-trap case installs and executes a guest trap-zero handler,
runs `Supervisor`, allocates Chip RAM and reads a disk sector through native I/O.
Native Kickstart passes all three cases. The old F17 ROM passes the memory cases
but fails the trap case. The exact final image passes all three cases. Two more
guest probes open graphics, initialize caller-owned structures and allocate a
raster with fitted/absent A501 RAM, verifying the relocated graphics installer.
Separate source checks cover supervisor/user entry, unrelated privilege-fault
chaining, bootstrap and Exec interrupt behavior: 38 cases pass without skips.

The retained [Alien Breed input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-alien-breed.json)
swaps the unchanged second game disk and starts the first level, then moves and
fires. The final native CPU-mode pair matches all 905 captured files and final
CPU, hardware, output and cycle fingerprints through 18,000 fields. The corrected
CopperStart pair also matches all 905 files and those final fingerprints. Reviewed
full-field captures show disk two, the menu, mission briefing and level one;
later directional/fire input moves through the bay and scrolls into another
room while ammunition changes. Across native and corrected ROMs, 162 of 181
screenshots match; the record retains the loading/animation differences. The story
disk, later levels, full-game completion and save/write paths remain unverified.

The [fire-only control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-alien-breed-neutral-v1.json)
keeps all earlier events and fire timing, clearing only the directional bits at
fields 16,000, 16,400 and 16,800. Native screenshots match until those inputs;
20 later images differ from field 16,100 onward. The control remains beside the
starting ship, while the directional sequence scrolls into another room.
The corrected ROM repeats this control: its screenshots also match until the
directional events, with the same 20 later differences starting at field 16,100.
Its fire-only run stays beside the ship, while the positive run enters another
room. Fire timing and the media sequence are identical in both inputs.

The exact same fresh ROM also repeats F17 through 26,000 fields, Lemmings through
24,000 and Apidya through 12,000. F17 reaches input-driven racing with acceleration,
steering and track progress. It retains 243 of 261 preceding screenshots. The
only differing race image is field 21,600: 80 pixels in the lap-clock digits;
the car, road and speed agree. Lemmings retains 83 of 121 images and again rescues
100% in level one. Apidya retains 118 of 201 images and reaches its options,
opening-stage play and continue prompt. Boot timing and animation/game state
differ after rebuilding Exec, so these are reviewed functional regressions,
without a cross-ROM CPU/RAM or complete screenshot-identity claim.

The production CopperScreen Release build passes without warnings. The producer
Release solution and focused source-test build pass with the existing sibling
compiler CS8602 warning. These source builds use separate outputs from the
frozen qualification components. Replay binaries are explicitly identified and
remain frozen across the native, old-ROM and corrected-ROM comparisons.

ROMs, game media, generated disks and captures remain local under
`artifacts/copperstart-alien-breed-2026-10-18/`. Disk two is supplied unchanged
through the ignored `media/alien-breed-disk2.ipf` alias. These are bounded native
correctness checks. Sampled PCM equality is not listening or audio-fidelity
qualification, and no host-throughput acceptance measurement is claimed.
