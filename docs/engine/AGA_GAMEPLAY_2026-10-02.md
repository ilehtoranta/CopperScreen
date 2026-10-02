# Supplied AGA games: CPU continuation and gameplay verification

UFO: Enemy Unknown reaches input-responsive strategy gameplay on the stock PAL
A1200 profile. The replay starts a new game, selects Beginner, places and names
the first base `codex`, advances the Geoscape clock, and opens the interception
screen showing the base's ready Skyranger and two interceptors. Both execution
modes complete 22,000 fields and match every one of the 139 capture files.

Alien Breed 3D II: The Killing Grounds passes its earlier startup stop, accepts
the levels-disk replacement in DF0, reaches its main menu and responds to Play
Game, and renders the first 3D level. Forward movement and right turning change
the scene, and firing reduces ammunition from 20 to 16. Both modes complete
72,000 fields and match all 871 capture files. Some completed rasters omit most
of the viewport or HUD, so continuous visual stability remains open. This is
bounded input-responsive gameplay evidence, not complete game acceptance.

The shared CPU changes are pushed to CopperMod main:
[`d2cd5b1`](https://github.com/ilehtoranta/CopperMod/commit/d2cd5b1a9ce81652a9b1477ff39fb14f2133d197)
and the indexed-PEA continuation
[`d26e449`](https://github.com/ilehtoranta/CopperMod/commit/d26e4498b661999b0c4be494c67cf214fc1c12b4),
privileged status restore
[`2159d29`](https://github.com/ilehtoranta/CopperMod/commit/2159d29104a605aa322404d83f59a356a40c0e57)
and sized postincrement AND
[`58d0e32`](https://github.com/ilehtoranta/CopperMod/commit/58d0e32b643eb5df05c19765f3c7245ad09f4723).
Renderer operand continuations are in
[`80ee048`](https://github.com/ilehtoranta/CopperMod/commit/80ee048f9fc58983c08467d06fb3ddbc8ac17bd5)
and PC-indexed address addition in
[`49ac596`](https://github.com/ilehtoranta/CopperMod/commit/49ac5967ba59be7a961fb15a9b5462019dbd6506).
The movement correction is
[`098a135`](https://github.com/ilehtoranta/CopperMod/commit/098a1350b9944a7da34263e4be36da2ba4be27e6).
The validated **Copper68k 1.5.2 development candidate is unpublished**.
CopperScreen still pins public **1.5.1**, so these candidate results do not
describe the unchanged application's package behavior.

The [original startup investigation](AGA_GAMES_2026-10-02.md) and its failure
captures remain historical evidence. This follow-up retains its own source,
package and native replay identities in the
[machine-readable record](AGA_GAMEPLAY_2026-10-02.json). ROMs, media, packages,
RAM, rasters and audio captures remain ignored. No guest executable, startup
script or disk was patched.

## Machine, inputs and scope

Both games use native A1200 Kickstart 3.0 revision 39.106, 68EC020, Alice 8374,
Lisa 4203, 2 MiB Chip RAM and zero Slow/Fast RAM. UFO's four supplied disks are
mounted in DF0–DF3. Alien Breed uses its 2 MiB program disk, levels disk and sound
disk in DF0–DF2. Its alternate 4 MiB program and editor disk are not covered.
The requested levels disk is moved into DF0 only after the program has loaded
and displayed its explicit disk prompt.

The supplied Alien Breed 2 MiB build requires an AGA Amiga, according to the
[original manual](https://www.gamesdatabase.org/Media/SYSTEM/Commodore_Amiga/Manual/formated/Alien_Breed_3D_II-_The_Killing_Grounds_-_1996_-_Ocean_Software_Ltd..htm).
The [original UFO technical supplement](https://openretro.org/file/35eb49586dc283dfa050cd5946c40a9b1be9919c/TechnicalSupplement_ocr.pdf)
specifies AGA A1200/A4000 and 2 MiB RAM. These supplied titles exercise a native
AGA game path beyond an intro or original chipset probe.

Normal input is retained in the [UFO strategy workload](../../CopperScreen.Lightweight.Tests/Workloads/aga-ufo-strategy.json)
and [Alien Breed opening workload](../../CopperScreen.Lightweight.Tests/Workloads/aga-ab3d2-opening.json).
The latter's media path identifies the supplied local levels disk; reproduction
on another machine must resolve the same hash-identified media. Inputs include
ordinary mouse clicks, menu selections, keyboard entry and the requested disk
swap. They do not bypass CPU execution or modify guest code.
The final Alien Breed control sequence begins after the observed first-level
transition: Keyboard at field 68,100, forward movement at 68,200–68,500, right
turn at 68,600–68,900 and fire at 69,000–69,100. Space is also sent at 69,400.
The new replay ends at 72,000 and captures every 500 fields. The earlier longer
input sequence and its failure remain separate evidence; they are not relabeled
as passing this new workload.

The first rendered room before movement is retained at field 71,000 in the
earlier `renderer-pc-ab-normal` run. The final candidate shows the camera against
the wall after forward movement at 68,500, a different room angle after turning
at 69,000, and the changed ammunition count at 70,000. These are visual checks
of the original completed rasters, with ordinary scripted inputs. The health
display also changes from the opening 200 to 240 during movement. Space is sent,
but an operated door is not verified.

At field 69,500 most of the viewport is absent while the HUD remains, and at
72,000 both are largely absent. Field 70,000 has the rendered scene and HUD.
The runner reads the completed double-buffered raster, so an unfinished host
capture is not an established explanation. Both execution modes reproduce these
images. Source inspection and copied-state CPU continuation do not establish
whether the cause is guest behavior, Copper/window ordering, interrupt phase or
the approximate CPU timing policy. A focused native register trace and independent
reference are needed before a display/timing correction. No compensating crop,
delay, IRQ change or guest patch is applied. See LWA-AGA-001 in [ISSUES.md](ISSUES.md).

## Shared CPU correction

The full-format indexed effective-address implementation now supports admitted
MOVE/MOVEA, CLR, LEA, PEA, JMP and JSR forms. It consumes null, word and long base
displacements; latches and scales signed indexes; supports independent base and
index suppression; and performs the ordinary timed pointer read for preindexed
and postindexed memory indirection. PC-relative forms use the extension-word
address. Reserved encodings stop explicitly before consuming displacement words.
Other instruction/destination combinations retain their unsupported boundary.

Address-only LEA, PEA and jumps do not read the final operand. PEA calculates the
address before changing the stack; JSR resolves aliased stack bases/indexes
before pushing the PC after every extension word. Opening UFO's interception
screen exposed `$487B` with full extension `$0170` at `$4FEA0`: a PC-relative PEA
with the index suppressed and long displacement `$0000008A`. The pushed address
is `$4FF2C`, based on the extension at `$4FEA2`.

The continuation also implements the ordinary sized SUB/SUBA absolute-long,
quick postincrement arithmetic, ORI/ANDI absolute-long, EORI displacement, SUBI
absolute-long, immediate bit modification with postincrement, immediate BCHG
registers, brief indexed MOVEM.W/L loads, and further MOVE/ADD/SUB/CMP operand
forms reached by the two games. MOVEM resolves its address before loading base
or index registers, consumes the mask before the PC-relative extension, and
sign-extends word loads. Its full indexed forms remain unsupported.

Alien Breed's first briefing exposed Kickstart `$46DF` at `$F8158E`,
MOVE.W (A7)+,SR. The privileged route checks privilege before reading or
advancing the source, increments the old stack bank, then applies the restored
SR and its supervisor/master stack selection. Both replay modes stopped at
field 65,863 before this correction; all 403 capture files matched. Those
failure captures remain evidence of the earlier candidate.
The following briefing click exposed `$C01C` at `$EFC14`, AND.B (A4)+,D0,
at field 68,008. Sized postincrement AND reads without changing source memory,
preserves untouched data-register bits and X, updates NZVC, and uses the A7
byte stride when applicable.
The following native stop was `$8039` at `$C15CC`, OR.B absolute-long to D0,
at field 68,009. Both frozen-package replay modes stopped there and matched
all 421 captures. CPU-only continuation from copied RAM/registers exposed
nearby operand gaps: absolute-long OR.B/W, absolute-long to brief indexed
MOVE.B/W/L, absolute-long NEG.B/W/L and CMPA.W/L, PC-displacement TST.B/W/L
and MOVE.B/L to absolute-long memory, and PC brief indexed ADDA.W/L.
CMPA sign-extends word sources and leaves An and X unchanged. PC bases use
the extension-word address, and ADDA leaves CCR unchanged while synchronizing
an A7 destination. The new memory-to-indexed MOVE route retains its full-format
destination unsupported boundary and 040 fallback policy.

That private diagnostic continuation infers the idle supervisor stack from
earlier native captures and restores the active user stack separately. With
these corrections it executes 147,433 instructions before a CIA access ends
the probe. It has no chipset or interrupt schedule and is not native gameplay
evidence. Complete native boots remain the verification path.
The native renderer continuation independently reaches the predicted `$D6FB`
ADDA.W PC-indexed stop at `$D4E7C`, field 68,011, with the ammo/energy display
visible. That exploratory failure predates the PC-indexed correction; a HUD
without a rendered 3D view is not gameplay verification.
The subsequent frozen-package replays both render the opening room, then stop
at field 85,004 when forward input reaches `$904C` at `$C8FE6`, SUB.W A4,D0.
All 523 capture files match. The shared correction subtracts the low address
word from the low data word, preserves the upper data word and An, and sets
XNZVC from the word arithmetic. Six regressions cover the captured operands,
borrow, overflow, zero and A7 as a read-only address source. The corrected
CPU-only movement continuation reaches the next CIA access after 149,983
instructions; that remains a diagnostic, not a full-machine replay.

These are shared opcode and operand implementations. There is no title-specific
dispatch, arbitrary wait, fallback engine or extra device clock. Regressions
cover extension consumption, pointer-read order, suppression, signed/scaled
indexes, operand widths, aliased registers, A7 byte stride and active stack
pointers, complete return PCs, arithmetic flags, CCR preservation and surrounding
memory. The existing 040 fallback control and explicit RMW barriers are retained.

Architectural expectations follow Motorola's
[programmer reference](https://www.nxp.com/docs/en/reference-manual/M68000PM.pdf).
Dynamic full indexed costs follow the cache-case effective-address and operation
tables in [MC68020UM](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf), within
the existing approximate advanced CPU policy. This is not physical A1200
CPU/cache/bus timing certification.

## Validation

The pack script validates package contents and repository metadata. The package
was restored from a fresh local NuGet feed and cache into an isolated export of
CopperScreen `de7e624`. The runner, CPU test host and engine diagnostic host use
the exact packaged CPU DLL. Production and diagnostics retain separate outputs;
no sibling source-project reference was added.

| Check | Result |
| --- | --- |
| Copper68k full suite | 4,049 passed; six optional external corpora unavailable. |
| Retained AHX consumer | 18 passed. |
| Pushed CPU source CI | [Passed](https://github.com/ilehtoranta/CopperMod/actions/runs/37059503676). |
| Production solution with candidate NuGet override | Release build passed; zero warnings/errors. |
| Engine diagnostics with separate outputs | 1,076 passed; zero skipped. |
| Ordinary host suite | 149 passed; six optional native cases unavailable in that run. |
| Separate supplied native A1200 desktop replay | One passed; two boots, eight-plane RGB24 checks and persistent DOS output. |
| CopperDisk suite | 74 passed; zero skipped. |
| UFO normal/scalar native strategy replays | Both complete 22,000 fields; all 139 capture files identical. |
| Alien Breed normal/scalar opening-level replays | Both complete 72,000 fields; all 871 capture files identical; movement, turning and ammo response observed. Continuous visual stability remains open. |

Scalar parity is a control, not an independent hardware oracle. These are
correctness diagnostics; elapsed times and runner FPS are not throughput
acceptance measurements. A skipped optional corpus or native case remains
unavailable coverage.

UFO tactical combat, complete games, every disk transition, save/load, Alien
Breed's alternate 4 MiB build, continuous Alien Breed visual stability and
physical hardware timing remain unverified.
The candidate needs explicit release authorization before NuGet publication.
The application's dependency can be updated after the package is available to
clean restores.
