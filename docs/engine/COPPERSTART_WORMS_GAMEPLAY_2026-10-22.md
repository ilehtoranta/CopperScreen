# CopperStart Worms RAM and gameplay follow-up — 2026-10-22

The rebuilt ROM reaches Worms Training gameplay from the original IPF media,
including movement and bazooka firing. This follows the
[original loader audit](COPPERSTART_WORMS_2026-10-22.md).
The [CopperStart source change](https://github.com/ilehtoranta/CopperStart/commit/cf7c9b54aefea723b018bd68e10e829e67684250)
contains the native fixes and ROM composition.
The initial contiguous-memory failure came from a misaligned public HUNK
allocation. Training exposed a second memory shortage caused by idle OS
allocations. The fixes retain the bounded machine and also correct nine
further native startup incompatibilities.

## Changes

- The DOS loader uses native public hunk allocations: the eight-byte size/link
  header followed by payload. The old private 20-byte owner first misaligned
  the public header and prevented the Ocean packer from freeing about 32 KiB.
  An intermediate alignment correction passed that loader but retained private
  prefixes, fragmenting memory after public frees. Private prefixes are now
  removed. Training separately exposed excessive idle OS residency. Public
  links provide rollback ownership until commit, then the final hunk chain.
  UnLoadSeg accepts caller-built public chains without an LSEG marker and frees
  them without erasing payload. Process cleanup does not inspect freed storage.
  Native 1.3/3.1 probes verify adjacent Chip hunk coalescing, exact AllocAbs
  reclamation, caller-chain unload and retained payload words. The prior ROM
  fails both new coalescing/caller-chain cases.
- Exec's library Open slot (-6) returns SysBase. Returning zero made Worms loop
  on a valid OpenLibrary("exec.library", 0). Permanent Close/Expunge semantics
  are preserved.
- Native Intuition GetScreenData (-426) copies caller-owned CUSTOMSCREEN data
  or opens and reuses the Workbench screen. OpenWorkBench/CloseWorkBench provide
  the bounded PAL 640×256 two-plane screen. Native 1.3 and 3.1 cap copies at the
  346-byte public Screen prefix, including a requested 360-byte copy. That
  observed limit is retained. No private owner marker is required from callers.
  Shared unsupported-vector ILLEGAL code recovers space in the 512 KiB ROM.
- InitResident accepts a supplied disk Resident without requiring rt_EndSkip
  to be above the tag. The original mathtrans.library relocates that pointer
  into a different hunk: a captured tag at $C13538 has rt_EndSkip $C0D4A8.
  The old comparison prevents initialization and makes Worms retry loading it.
  Native 1.3/3.1 accept authored AUTOINIT tags with zero or lower EndSkip values.
  MatchWord and self MatchTag validation remain.

- Native console.device supports the CONU_LIBRARY (-1) open mode and
  RawKeyConvert (-48), including default US Shift/Caps/Control characters,
  keypad, cursor/function sequences, linked InputEvents and caller-owned
  KeyMap quads/string descriptors. Neither a window nor mn_Length certifies
  the IORequest. The missing device previously left a NULL base and a call
  to $FFFFFFD0 at field 60987. Native 1.3 consumes the linked-event probe;
  native 3.1 returns overflow after the first character. CopperStart retains
  the permissive 1.3 behavior. Window units and full Alt/dead-key composition
  remain unqualified.
- Native mathffp.library supplies all twelve classic vectors using integer
  MC68000 arithmetic. Its missing base previously led to a call to $FFFFFFDC
  at field 61203 while Worms initialized a 360-entry table with SPFlt. The
  original disk mathtrans.library continues to load through DOS. Native 1.3
  and 3.1 agree on 51 boundary cases and a deterministic 512-case Add/Sub/Mul/Div
  corpus, including defined N/Z/V flags. The exact corpus SHA-256 is
  `3115d2d8a9ce8eb74e91193eb4cf0ffb6d98d5cbed69e34a8c44037ab40858c6`.
  Classic addition/subtraction rounds before carry/cancellation normalization
  and ignores operands at exponent differences of 24 or more. The native 3.1
  reset probe explicitly initializes its deferred ROM Resident before opening
  it; it still executes the native ROM arithmetic.

- The read-only native filesystem accepts the game's zero-access Lock on
  `twstuff/tw6`. Its old LocateObject guard rejected every access other than -2
  with DiskWriteProtected (214), causing repeated directory lookup and a black
  display. Independent native 1.3/3.1 probes accept zero access; the previous
  CopperStart ROM fails with 214 while its shared-lock control succeeds.
  Lock access does not request a media write. The guard is removed and the
  caller's access value is preserved. Mutating commands still fail on this
  read-only provider; general conflicting-lock arbitration is unqualified.
  This corrects the provisional interpretation of repeated disk activity as
  advancing asset loading: captured filesystem objects and DOS packets reveal
  repeated lookup instead.

- The graphics VBlank server leaves a direct caller COP1LC takeover intact.
  After lookup succeeds, Worms waits for protection input with a valid custom
  list in Chip RAM, but the old server overwrites its Copper root every field.
  It now publishes gb_LOFlist through COP2LC without resetting COP1LC. Repeated
  LoadView(NULL) is also a no-op when ActiView is already NULL. Independent
  native 1.3/3.1 probes retain caller-generated green/blue raster bands across
  twelve fields and the repeated NULL call. The previous ROM loses the bands.
  Nonblank LoadView still publishes the OS root explicitly. No engine change,
  direct guest patch or game-specific branch is added.

- Native keyboard.device supplies the serial interrupt data that Blitz reads
  from CIA-A resource +$64. It acknowledges KDAT in the guest IRQ, maintains
  raw code/qualifier pairs and the pressed-key matrix, and supports unit zero
  and KBD_READMATRIX. The direct-reader flag at +$3D and ring at +$70/+$72/+$74
  remain usable during Forbid. Console and Intuition consume that same queue
  when the direct-reader flag is clear, replacing their former task-only
  physical sampling. This allows the game to receive input during takeover.
  Independent native probes verify z down/up, left Shift, qualifiers, matrix
  bits, device identity and the base+$34 unit pointer. CopperStart Exec is V34,
  so this device reports V34 and uses its +$106 matrix and documented exact
  thirteen-byte io_Length requirement. Native 3.1's different capacity rule
  returns at most fifteen bytes and is recorded separately. Both references
  accept the authored IORequest with mn_Length zero. Asynchronous ReadEvent,
  repeat/reset handlers and reset-chord behavior remain unqualified.

- A four-sector guest CPU cache prevents tiny DOS reads from repeatedly seeking
  between OFS metadata and data tracks. After protection succeeds, Worms reads
  TW5 a few bytes at a time; the previous ROM advances only about 4.6 KiB in
  10000 fields. The optional 2064-byte cache uses existing public guest RAM,
  falls back to uncached reads on allocation failure, and invalidates with the
  physical media-change generation. An authored scattered OFS file completes
  128 four-byte reads within 2000 fields on native 1.3/3.1 and the rebuilt ROM;
  the prior ROM completes only ten. This functional bound is not a throughput
  measurement. Real trackdisk I/O and filesystem checksums remain effective.

## Idle OS memory

Training startup subsequently requests 126008 contiguous Chip bytes. The
same 1940 game allocations occur on native 3.1 and CopperStart; the failure
comes from additional idle OS residency. Trackdisk now allocates its 28160-byte
DMA/decoded/bounce workspace only for an active polled read and releases it on
all return paths. Device metadata, filesystem state, the permanent DOS owner,
command stacks and script workspaces use ordinary Public memory. Resident DOS
service processes also use that policy: filesystem, console and startup stacks
no longer force Chip storage merely to reserve A501 space for program hunks.
Explicit Chip allocations remain for DMA-reader and display storage.

After successful DOS process publication, the boot root retires the original
bootstrap SSP and user stacks; it publishes its real private SSP bounds and
updates the Exec checksum before freeing them. Its private SSP is 4096 bytes,
the parked completion task uses 2048 bytes, and the Intuition input task uses
2048 bytes. Custom boot-block failure/return paths retain their live stacks.
The permanent DOS installer uses the boot root's once-only latch and trusts
its own compiled AUTOINIT result, while retaining allocation-failure rollback.
The general dynamic installer keeps its separate binding validation.

External script commands temporarily release the dead 5120-byte parser prefix,
while the script-frame tail and a separate argument copy remain owned. On return,
AllocAbs reacquires the old prefix, or the live tail moves to a new workspace
if a command has kept an allocation there. Script frames are rederived after
relocation. Exhaustion closes the retained script handles before releasing the
tail. The original by-value scripting overload preserves its storage address.
The optional sector and decoded-track caches are released after the last file
closes and recreated when needed. The permanent filesystem state is 1088 bytes;
its separate 5632-byte track cache retains full-track reuse during file reads
and falls back to direct sector I/O if that optional allocation fails. A parked
interactive CLI also releases its 6152-byte parser workspace: FGetC waits using
the existing DOS input buffer, then UnGetC returns the first byte before the
full parser workspace is allocated and the unchanged line reader resumes. A native constrained-memory/read probe passes on 1.3/3.1, fails
on the previous ROM, and passes with these lifetimes. A separate retained-memory
probe and nested-script relocation case cover command return.

The permanent DOS library also replaces its 223 per-vector 36-byte RAM
trampolines with six-byte JSR stubs and a shared ROM bridge at $FF5000. The
stub return address identifies the vector; a ROM lookup retains the existing
complete Classic/private/MorphOS target surface. Each call has a register
frame on its own stack, including across waits and guest callbacks. The bridge
preserves the existing argument lanes, D2-D7/A2-A6, scalar D1 preservation and
D0/D1 pair results, including the pair target's CCR. Public vectors remain
six-byte absolute JMPs.

After AUTOINIT completes, the permanent owner is marked DROM and releases its
unused old frame bytes, trampoline tail and AUTOINIT function table: 7560 bytes
in total, with eight-byte FreeMem boundaries kept inside the dead storage.
Names, stubs, DOS/provider state and all active lifetime records remain owned.
DROM explicitly distinguishes this permanent split storage from an unloadable
DOWN envelope; the general dynamic installer and its ownership validation are
unchanged. Six synthetic MC68000 ABI cases cover minimal/full frames, scalar
and pair returns, preserved registers, arguments and condition codes. Native
ROM/media tests separately cover the compiled targets and real Exec ownership.

The default ROM font's immutable public CharLoc table now resides at $FF4C80.
Its 224 ULONG entries retain exactly the former CopperStart values, with data
SHA-256 `82cacd064ff6ba869ac9d33ba993bd8cc6c6585a7094e846fd91954546e0ec32`.
The mutable TextFont node remains in RAM; moving the table releases another
896 bytes of early OS residency. Native 1.3/3.1 independently expose the default
ROM font's character locations in ROM with TypeOfMem returning zero. The prior
CopperStart image returns Public/Fast RAM. The probe compares this storage
contract separately from glyph metrics: native topaz has some ten-pixel glyph
extents despite an eight-pixel character advance. The replacement glyph data
and metrics are unchanged by the move.

Relative WORD graphics/Intuition function arrays, compact immediates and
backward branches, and shared Intuition restore/return code recover enough ROM
space for these services. Public RAM vectors remain six-byte absolute JMPs.
The final 524288-byte image has SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The compiled main image is 476276 bytes, graphics 28818, Intuition with its three
companions 12500 and CIA resources 1030. An independent relink is byte-identical.
The packed addresses include font locations $FF4C80, DOS bridge $FF5000, misc.resource $FF514C, BCPL $FF5250, Intuition $FF59E0,
CIA $FF8AC0 and graphics $FF8EC8. Link-time checks enforce region separation.

These changes execute entirely in the guest ROM. The machine remains PAL OCS
8371/8362, a 68000 interpreter, 512 KiB Chip plus 512 KiB slow RAM, no Fast RAM,
RTC disabled, one read-only DF0 and the full 908×313 raster. The original ZIP/IPF
and runner/engine identities are retained in the accompanying JSON. No host OS
shim, game/protection patch, extra RAM or engine change is involved.

## Original media gameplay

The final ROM accepts the published booklet code for Table B, row 10, column G,
loads the assets, and reaches the main menu at field 100000. Both executions
then select Game Start, Training and the 2UP team using physical mouse input.
Training renders its terrain, active worm, health and Bazooka HUD at 104000.
Right/left raw-key transitions move/turn the worm. Space press/release fires;
the capture at 106646 shows a projectile immediately left of the worm. At
107200 the worm has 95 health after the shot, and control has returned.

At the menu, the largest Chip block is **127128 bytes**, compared with **119528**
on the font-only intermediate ROM. Training's **126008-byte** cleared Chip
allocation succeeds and is present in the game's allocation list at 104000.
Both menu runs have the same 1940 game allocations totalling 465318 bytes;
ordinary Public allocation placement differs slightly with the OS residency.
The machine remains 512 KiB Chip + 512 KiB slow RAM, with no Fast RAM added.

A thin vertical blue graphics artifact is visible in both CopperStart and
the native Kickstart 3.1 control. It remains unresolved; these replays do not
establish artifact-free rendering. Game completion, multiplayer, saves and
original disks 2/3 are outside this check.

Both executions exit zero after field 107200. All **5375 capture files** match
byte for byte, covering 1075 captured fields with state, both RAM banks, raster
and PCM. No snapshot reports
an unsupported hardware event. Two snapshots contain nonzero PCM; this confirms
sample presence, without an audio fidelity claim. The canonical physical-input
workload and exact run identities are retained in the
[machine-readable evidence](COPPERSTART_WORMS_GAMEPLAY_2026-10-22.json).

## Independent probes

An authored two-hunk executable detaches and frees a 12-byte public Chip hunk,
then reclaims the same address with AllocAbs. The observed free-list gain is
16 bytes. The old CopperStart ROM fails alignment; native Kickstart 1.3/3.1
and the corrected ROM pass. Host unload/rollback expectations account for the
native public header sizes, without private per-hunk padding.

Separate reset probes cover OpenLibrary/OldOpenLibrary of Exec, its direct
Open/Close slots, unchanged open counts and rejection of minimum version 127;
caller Screen storage, the native copy limit, Workbench dimensions/modes and
closing; and AUTOINIT libraries with zero/lower EndSkip values. Before-fix
failures and native reference successes are retained separately. Instruction
tests also cover zero-, one- and full-prefix copies with destination guards.
Focused/native-reference/compile runs overlap the full suite and are not
summed as additional unique coverage.

The final native suite passes **600 tests**, with no skips. Focused host and
vector-installation tests pass **161**. Each independent native reference ROM
passes all eighteen independent probe cases. The final suite includes both product
FFP reset probes. Focused runs overlap the suite; their counts are not added.

The final ROM's Apidya regression reaches opening meadow combat/respawn at
field 6500, with visible respawn at 6400. Normal and scalar captures are
identical across 330 files; all snapshots report no unsupported hardware events and 49 of 66 contain nonzero
PCM. This establishes PCM presence, without an audio fidelity claim.

## Native reference and historical correction

The native Kickstart 3.1 control enters the printed Table D, row 21, column Q
code, reaches the main menu, selects Training and a team, and reaches gameplay.
Physical cursor-right and Space press/release inputs exercise movement/firing.
The control ends at field 17500 and exits zero. The menu displays "THE
DIRECTOR'S CUT"; the media identity is the supplied Worms_230 ZIP and its
recorded hashes.

The earlier audit transcribed the native 1.3 prompt as Table A, row 32, column
C. A fresh full-raster capture and visual booklet lookup confirm **Table C,
row 32, column C**, code 418. This corrects the transcription; the earlier ROM's
loader failure and its recorded hashes remain historical evidence.

## Build provenance and limits

The production CopperScreen solution builds with zero warnings/errors.
The full CopperStart solution remains unavailable: restore reports six errors
involving SDK .Amiga/.Support 0.1.0-preview.1-g20 dependencies and a downgrade
through CopperOS.MuiMaster. Affected components and the standalone ROM build
separately using the retained qualified compiler/runtime and two frozen SDK
DLL snapshots. The snapshots come from an existing CopperSharp68k working
build at daab9bcf82ef6cf54b391cea8ff5e556cb352776 plus unrelated WIP, and are
not a clean released SDK producer. Exact hashes and build logs are recorded.
No package is published and no sibling source reference is introduced.

Native tests use the older retained fixture engine; original media uses the
frozen live engine from the Superfrog correction. Those are separate results.
These runs assess bounded compatibility, without a host throughput or
historical performance acceptance claim. ROMs, media and captures stay local.

Primary references: [InitResident](https://developer.amigaos3.net/autodocs/exec.library/InitResident.html),
[GetScreenData](https://developer.amigaos3.net/autodocs/intuition.library/GetScreenData.html),
[OpenWorkBench](https://developer.amigaos3.net/autodocs/intuition.library/OpenWorkBench.html),
[FreeMem](https://developer.amigaos3.net/autodocs/exec.library/FreeMem.html).
