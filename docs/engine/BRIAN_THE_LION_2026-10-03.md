# Brian the Lion CD32 hard-disk rip

The supplied Brian the Lion media exposes sixteen missing CPU operand families,
a byte/word MOVE decode defect and Paula's undocumented software request bit 14.
The CPU corrections allow
native installation and title execution; the Paula correction enables the
level-6 input handler and makes the saved-game menu respond to joystick fire.
The corrected candidate reaches the first jungle level and completes the
15,000-field input replay in both execution modes. All 367 captures match.
A control run confirms an input-dependent change in the guest player state;
visible player movement and audio remain unverified. This is bounded native
level-entry coverage, not a complete gameplay pass.

The shared CPU work belongs to the unpublished Copper68k 1.5.2 source in
[CopperMod PR #19](https://github.com/ilehtoranta/CopperMod/pull/19).
CopperScreen still pins published 1.5.1. The private candidate is restored as
a package into isolated outputs; there is no sibling project reference.
The [machine-readable record](BRIAN_THE_LION_2026-10-03.json) retains identities,
commands, input, intermediate failures, tests and capture hashes. ROMs, media,
installed files, RAM dumps, audio, rasters and private packages are not committed.

## Supplied media and native installation

The six supplied ZIPs contain 880 KiB FFS ADFs named `Brian the Lion CD32
(1994)(Psygnosis)(AGA)(M4)(Disk N of 6)[HD, CD32 rip]`. Their volumes are
`b1` through `b6`, with no executable bootblock. They are installation data,
not six bootable game disks or a CD image. The supplied installer requires
AGA and joins six segments into a 1,981,238-byte LZX archive.

The test fixture expands the earlier native Workbench 3.1 RDB/FFS fixture
to 16 MiB, copies the supplied archive and unpacker, and boots a small LF
startup script under native A1200 Kickstart 3.0 revision 39.106. The guest
executes the supplied `unlzx`; its installation log reports all six files OK.
The installed `GAMEFILE` is 5,111,840 bytes. The supplied launcher uses
`CDLOAD.BAK`; the game and loader are unmodified. Only the test fixture's
startup script is generated. The fixture's original and resulting hashes,
installed-file hashes, ROM identity and source ADF identities are retained.

Every replay uses stock PAL 68EC020, Alice 8374, Lisa 4203, 2 MiB Chip RAM,
zero Slow RAM and zero Fast RAM, through the existing native HDF path.
The supplied hard-disk rip does not test original CD32 optical boot, CD audio,
Akiko, or a serial CD32 gamepad.

## CPU stops and corrections

Each reached family has focused fail-before/pass-after coverage in Copper68k.
Unsupported execution is never continued and no guest opcode is patched.

| Native stop | Instruction | General correction |
| --- | --- | --- |
| Native unpacker, field 156, `$32F86` | `$3D8C`: `MOVE.W A4,(d8,A6,D0)` | Word An stores to indexed memory, including admitted full extensions |
| Native unpacker, field 385, `$32D30` | `$843B`: `OR.B (d8,PC,Xn),D2` | Byte/word PC brief indexed OR |
| Game launch, field 172, `$1F148` | `$4278`: `CLR.W (xxx).W` | Byte/word absolute-word CLR; existing long route retained |
| Language selection, field 1,911, `$622C0` | `$31FB`: `MOVE.W (d8,PC,Xn),(xxx).W` | Sized PC indexed MOVE to absolute-word |
| After intro, field 2,945, `$108E76` | `$4AB8`: `TST.L (xxx).W` | Sized absolute-word TST |
| Title setup, field 2,957, `$2260C` | `$48F8`: `MOVEM.L D0-D1,(xxx).W` | Long register-list stores to absolute-word |
| Title update, field 3,068, `$22A22` | `$5378`: `SUBQ.W #1,(xxx).W` | Sized absolute-word SUBQ |
| Same update, field 3,068, `$22A28` | `$31FC`: `MOVE.W #200,(xxx).W` | Byte/word immediate MOVE to absolute-word; existing long route retained |
| Other Game menu, field 8,301, `$24016` | `$0A50`: `EORI.W #224,(A0)` | Sized immediate EOR to indirect memory |
| New Game continuation, field 8,160, `$109D7C` | `$0838`: `BTST #n,(xxx).W` | Immediate byte BTST to absolute-word |
| Native ROM exception path, field 8,161, `$F80A6C` | `$0497`: `SUBI.L #n,(A7)` | Sized immediate SUB to indirect memory |
| Level VBlank handler, `$109D04` | `$11C0`: `MOVE.B D0,(xxx).W` | Byte/word Dn stores distinguish short/long absolute addresses |
| Same handler, field 8,161, `$109D0E` | `$31EE`: `MOVE.W (12,A6),(xxx).W` | Sized address-displacement MOVE to absolute-word |
| New-game continuation, field 8,188, `$14119A` | `$0A79`: `EORI.W #n,(xxx).L` | Sized immediate EOR to absolute-long memory |
| Further continuation, field 8,214, `$1401BC` | `$8C70`: `OR.W (d8,A0,Xn),D6` | Byte/word An brief indexed OR; existing long route retained |
| First-level loading, field 9,860, `$10C3AA` | `$359B`: `MOVE.W (A3)+,(50,A2,D1.W)` | Sized postincrement MOVE to brief indexed memory; updated source/base/index alias ordering |
| First-level input continuation, field 11,335, `$1108BA` | `$E0ED`: `ASR.W (586,A5)` | One arithmetic word shift in displacement memory, with flags and read-modify-write barrier |

The 145 focused cases cover operand widths, surrounding memory, signed
absolute-word addresses, extension-word PC bases, signed/scaled indexes,
aliased source/index registers, full source extension consumption, CCR/X,
MOVEM ordering and empty masks, quick count eight, borrow/overflow/zero and
read-modify-write barriers, immediate EOR widths and byte BTST modulo-eight
with only Z changing, SUBI arithmetic flags and indirect A7 preservation.
Postincrement indexed MOVE covers sized source reads/stores, updated aliased
destination bases and address-register indexes, and A7 byte strides. Its
full destination extension remains explicitly unsupported.
Memory ASR cases cover retained sign, outgoing carry/extend, zero, cleared
overflow, signed displacement, surrounding memory, unchanged An, the existing
LSR route and read-modify-write plans.
Eight MOVE regressions place LEA immediately after the short-address store;
two long-address controls retain both extension words and the following LEA.
Shared 020/030 controls and the 040 fallback remain
covered. Timing follows the existing bounded advanced model; the zero-wait
MOVEM cache-case assertion does not certify physical instruction durations.
Architectural expectations follow [Motorola's programmer reference](https://www.nxp.com/docs/en/reference-manual/M68000PM.pdf)
and [MC68020UM](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf).

The MOVE decode defect initially hid behind the ROM exception handler's
missing SUBI. Candidate `.23` enters illegal-instruction vector 4 with the
stacked PC `$109D0A`, inside the following LEA. The old mask discarded the bit
distinguishing short and long absolute destinations. It combined the short
address `$0052` with the following LEA opcode `$4DF9`, wrote to the wrong
address and continued at LEA's extension word. Corrected candidate `.24`
finishes the store at `$109D08`, executes the complete LEA and reaches the
next genuine missing operand at `$109D0E`. The later recovery/reset stop on
`$4CF8` at ROM `$F802C8` is retained as a consequence of the earlier failure,
not confused with the original game defect. Both `.23` modes stop there with
all 211 captures and final HDF hashes identical; that is matching failure,
not a native gameplay pass.

## Paula bit-14 request

CPU-only candidates reach the language screen, Reflections intro and animated
Brian title. An 8,000-field title replay and subsequent 10,000-field input
replay complete, but the title's level-6 input handler never runs. Its counter
at `$39CF8` remains zero. The guest installs vector `$78`, enables the master
interrupt gate and acknowledges software `INTREQ` bit 14. Lightweight masked
that request out of the register latch, visibility scheduler and priority
encoder.

Software-written bit 14 now latches/readbacks/clears as an independent level-6
request. `INTENA` bit 14 still gates all interrupts, and enabling it alone
does not create a request. CPU/Copper writes retain the existing one-CCK
visibility delay and canonical owner clock. Ordinary hardware requests retain
their original mask and scheduling. Four new discriminating tests cover
request/readback/acknowledgement, master enable, delayed Copper set/clear and
waking a stopped CPU through the level-6 autovector. Three fail before the
correction; the master-enable control already passes.

The behavior is implemented independently by [WinUAE's interrupt controller](https://github.com/tonioni/WinUAE/blob/master/custom.cpp)
and [Minimig's Paula controller](https://github.com/retrofun/MinimigAGA-MiST-TC64/blob/master/rtl/minimig/paula_intcontroller.v).
It is ordinary Paula behavior, not a CD32-only device. Physical propagation
timing remains bounded by the existing digital model.

With the correction the guest handler counter advances and joystick fire
opens the restore/saved-game menu. The first three empty save slots are not
restorable; the fourth slot starts a new game. Selecting it leaves the title
and enters further game loading. Earlier attempts and their completed rasters
remain retained rather than being called gameplay passes.

## First-level replay and control

Candidate `1.5.2-aga-dev.29` reaches the world map labeled The Steamy Jungle
and then renders the jungle scene and status bar. The input sequence holds
Right at fields 11,000–11,300 and presses Fire at 11,500–11,550. Both modes
complete 15,000 fields with exit code zero, no unsupported CPU/display report,
identical CPU/hardware/output fingerprints, all 367 capture hashes identical
and identical final HDF hashes. The timer advances in the completed rasters.

The control repeats the same boot, language, menu and level-entry inputs,
then omits those movement/fire events. It also completes 15,000 fields. The
guest player structure at `$14BF9A` contains X/Y words at offsets `$21A/$21E`
and velocity longs at `$24A/$24E`, identified from the guest movement code.
At field 10,000 both runs contain X=47, Y=863 and zero velocity. At field
11,500 the input run contains X=34, while the control retains X=47; these
values persist to field 15,000. This establishes an input-dependent guest
state change. The observed direction and limited displacement do not yet
establish correct visible locomotion or jumping.

The sampled jungle rasters do not establish a visible player sprite. All
sampled PCM chunks are zero. Player visibility, correct movement/jumping,
sound and later-level progression remain open; an independent native
reference is needed to separate guest/media behavior from CPU, input or
display ordering. No additional Akiko, optical drive, CD audio or serial
CD32 controller requirement has been demonstrated by this hard-disk rip.

Earlier candidate `.28` reaches the level but stops on memory ASR at field
11,335. Both modes fail there with all 283 captures matching. That matching
failure is preserved separately from the successful `.29` continuation.

## Validation and limits

Final CPU source is `e7bce76667d2768e042ff4967c695220b4b78f16` in CopperMod;
the private package is `1.5.2-aga-dev.29`. Its nupkg SHA-256 is
`98e0b0401930237d39abc255bc4b15194f8cdfbc0bf73c966d37dc848428c1ff`
and CPU DLL SHA-256 is
`9d19908ce129631b9347d1494128c75f95420de5448f4a84c1a62c556239bd8f`.
The package, native runner and consumer outputs retain matching manifests.
The Lightweight executable source is clean commit `80763f1`; subsequent
report changes do not change executable source or replay ordering.

The full CPU suite passes 4,253 cases, with six optional external corpus
cases unavailable; all 18 AHX cases pass. The final isolated private-package
production Release build passes with zero warnings/errors. Separate consumer
checks pass 1,080 engine diagnostics, 149 host cases and 74 disk cases; six
optional native host cases are unavailable. The Paula correction also passes
the ordinary production build and all 1,080 engine diagnostics on public
1.5.1. Native ROM/media replays and optional host tests are separate coverage.

Initial Windows CRLF startup scripts caused DOS `FailAt` parsing to fail
before the unpacker ran. The LF fixtures correct that harness error. Private
candidate `1.5.2-aga-dev.15` has an invalid TST timing-model initializer and
fails before native continuation; it is excluded from successful coverage.
The corrected formula is in later uniquely versioned packages. Original
failures and invalid attempts are preserved in the record.

Normal/scalar agreement is a determinism check, not a hardware oracle.
Runner elapsed times and FPS are diagnostics, not throughput acceptance.
Package publication and the application's dependency update remain separate
release work requiring the pending authorization.
