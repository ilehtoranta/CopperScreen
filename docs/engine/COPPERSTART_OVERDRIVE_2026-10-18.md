# Overdrive: prefer expansion RAM for unrestricted allocations

2026-10-18. The supplied original two-disk Overdrive IPF release exposes a
bootstrap memory-order mismatch in the Alien Breed-qualified CopperStart image.
Native Kickstart 1.3 reaches the menu, loads disk two and enters qualifying on
the same PAL OCS 68000 A500 with 512 KiB Chip and 512 KiB A501 RAM. The preceding
replacement ROM leaves a frozen loading logo and an invalid PC.
The [evidence record](COPPERSTART_OVERDRIVE_2026-10-18.json) identifies source
lineage, fresh compilation inputs, exact media, commands, probes and reviewed
captures.

The loader copies temporary code to `$78000`, then calls
`AllocMem($50000, MEMF_CHIP)` at `$78010` and `AllocMem($40000, 0)` at `$78020`.
Native Kickstart returns `$59E8` and `$C04AD0`. The replacement returns `$A790`
and `$78000`. Its free-chunk header at `$78000` has already been overwritten by
the relocated instructions: the longword at `$78004` is `$203C0005`. Scanning it
as a free-memory size accepts the second request outside the physical Chip bank.
The loader's later buffer writes cross `$80000` and mirror into low memory,
overwriting exception vectors. The first bad interrupt target appears at field
1,656. Completing the runner without an unsupported-feature report is not a
successful loading result.

The early native memory-list dump has A501 priority `0` before Chip priority
`-10`. CopperStart has both priorities `0`, with Chip inserted first. The fix
changes the bootstrap Chip priority to `-10`, retaining priority `0` for expansion
banks and the existing priority-sorted allocator. This follows the classic
[Amiga programming guidance](https://wiki.amigaos.net/wiki/Programming_in_the_Amiga_Environment):
requests without a Chip requirement use available expansion memory. Explicit
Chip requests still select Chip, and unrestricted requests can fall back to
Chip when expansion memory is absent or full. Caller-supplied AddMemList
priorities remain effective. No new caller-data check or game-specific path is
introduced.

Four host regression cases cover unrestricted/public preference and fallback
with absent/exhausted A501 RAM. The old bootstrap fails both preference cases
and passes both fallback cases. All 42 focused bootstrap, native-supervisor and
Exec-interrupt cases pass after the fix. An independent generated original boot
disk performs guest AllocMem, TypeOfMem and FreeMem calls for generic, public,
explicit Chip and large unrestricted buffers. Together with the retained F17
memory and Alien Breed caller-trap/native-I/O probes, native Kickstart passes
all five cases; the preceding replacement passes four and fails fitted-A501
preference. Initial fixture invocations stopped at a hash-letter-case mismatch
before guest execution; those attempts are retained as unavailable coverage.

The fresh baseline compiles the five unchanged Alien Breed native root sources
with their recorded managed/component inputs. The corrected image uses the
same root sources and ten other frozen component DLLs, rebuilding Exec from the
84 recorded sources with only BootstrapCore changed. Both use the frozen
SDK/compiler and graphics composer, with the source installer and private
resident at `$FF8000`. This is separate from qualification of the complete
moving-main ROM, its wider resident services or the standard application.

The fresh baseline was built at `2026-10-18T08:16:30.2780374+00:00`. Its
459,318 code bytes and 26,098 graphics bytes produce the same SHA-256 as the
preceding Alien Breed image:
`F755C8A0D00B1D99FA54071E0149C111F7BB5BFCE7BA58F55172EA0FD234AB62`.
It repeats the four passing/one failing guest cases, and its 2,000-field replay
reaches the same invalid PC before the first input event.

The corrected 512 KiB image was built at `2026-10-18T08:37:24.4475311+00:00`.
Its SHA-256 is
`5CA306B3A7ECE26A366ADC90E487FC384276A5463C22C3CE6B98AA4A118764D0`,
with 459,374 compiled code bytes and the retained 26,098-byte graphics resident.
Its native root has zero managed allocation sites and zero framework features.
All five exact-ROM memory/caller-trap/I/O cases pass, as do both retained graphics
guest probes. The unchanged loader's corrected allocation trace returns `$9348`
for its Chip request and `$C08468` for the unrestricted buffer, before any input
or game-state differences.

The qualified native source fix is
[`84525a2`](https://github.com/ilehtoranta/CopperStart/commit/84525a27cb625d03b59be803d2988c6b5bfc5a16).
[CopperStart PR #9](https://github.com/ilehtoranta/CopperStart/pull/9) separately
backports the same priority change over the Alien Breed Supervisor correction,
preserving main's independent bootstrap/task-completion layout. The source CI
workflow filters automatic PR runs to main, so this stacked PR has no automatic
CI coverage. Local tests and native-ROM execution are separate results.

The existing corpus input establishes the original disk swap and interactive
race/statistics selection. The retained
[qualifying input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-overdrive-qualifying-v2.json)
then selects qualifying, acknowledges the circuit briefing, and applies late
acceleration and steering. The
[coasting control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-overdrive-coast-v2.json)
clears only Fire at the four driving events, preserving steering, menus and
media. The [manual transcript](https://www.lemonamiga.com/doc/overdrive/1183)
identifies Fire as the accelerator and left/right as steering.

The final native conservative/scalar pair matches all 1,155 captured
BMP/state/Chip/slow-RAM/sampled-PCM files and final CPU, hardware, output and
cycle fingerprints through 46,000 fields. Coasting and powered views match
before field 40,000; 30 later screenshots differ from field 40,200. Reviewed
views at 40,400 and 40,600 show movement through the circuit and steering,
while the coasting control stays near its starting position. The first input
exploration also proved acceleration, but placed steering after a later load
transition; the final input brings those steering events into the active interval.

The corrected-ROM conservative/scalar pair also matches all 1,155 captured files
and the four final fingerprints. Its powered and coasting screenshots agree
before the driving inputs, then differ in 30 images from field 40,200. Reviewed
40,400/40,600 views show acceleration, scrolling and steering; the control stays
near the starting position. The recorded replacement-ROM scenario is GP circuit
2, while native Kickstart selects a buggy on four-by-four arena circuit 2. Both
reach qualifying through the same disk/menu sequence. These are input-driven
qualifying outcomes, without a complete-lap or full-race claim.

The same exact ROM repeats Alien Breed through 18,000 fields, F17 through
26,000, Lemmings through 24,000 and Apidya through 12,000. Alien Breed retains
its opening-level movement/firing and all 181 preceding screenshots. F17 retains
acceleration, steering and circuit progress, with 187 of 261 screenshots matching.
Its later differences include HUD and road images, so the record does not claim
identical race rendering. Lemmings again rescues 100% in level one and retains
83 of 121 images. Apidya reaches opening-stage play and the continue prompt,
retaining 118 of 201 images. General allocation addresses and startup/game state
can differ after memory preference changes; cross-ROM RAM/CPU identity is not a
gate. The record retains every screenshot comparison and reviewed outcome.

The qualified-source Release solution passes with one existing compiler warning.
The narrow backport over the Alien Breed Supervisor change passes all 25 selected
tests and its Release solution builds with the existing compiler and SDK-selection
warnings (five total). Its physical Chip-bound test finds the bank by attributes
so that its assertion is independent of memory preference and main's private
header placement. The consumer Release build passes without warnings.

ROMs, media, generated disks and captures remain local under
`artifacts/copperstart-overdrive-2026-10-18/`. The disk-two alias contains the
original payload unchanged. These are bounded native compatibility checks;
full-race/game completion, saves, other profiles, audio fidelity and throughput
acceptance remain unverified.
