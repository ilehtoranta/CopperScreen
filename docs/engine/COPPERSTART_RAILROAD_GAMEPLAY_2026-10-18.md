# CopperStart: Railroad Tycoon gameplay, 2026-10-18–19

This continues the native Intuition/world-generation qualification preserved as
CopperScreen commit `b05e9bd63ae1a3bacba92de421f55f1b637b1436`.
The exact source, ROM, inputs, native reference probes, test results and replay
identities are recorded in [the companion JSON](COPPERSTART_RAILROAD_GAMEPLAY_2026-10-18.json).

## Compatibility faults and corrections

The preceding ROM completes world generation but crashes after the recorded
post-world mouse click. Both normal and scalar negative replays produce the same
805 capture files. Their successful process exits and empty unsupported-chipset
fields do **not** constitute a successful guest replay: an exception storm has
already overwritten guest code, stacks and hardware state.

A separate read-only instruction trace locates the first fault at field 150063.
Railroad calls native Intuition `PrintIText(-216)`, which calls graphics
`Text(-60)` for the cash string `$ 1,000,000`. The public `railf0.font` supplies
CharSpace and CharKern tables and uses JAM2. CopperStart rejects those tables in
its admission check and executes an illegal instruction at `FFAA5E`. No title
patch, guest-state injection, stack reduction or memory-budget workaround is
used to hide that failure.

The corrected native text emitter accepts proportional monochrome metrics in
all eight ordinary draw modes. It composes each complete string scanline, then
applies the requested mode once. This preserves overlap, inverse video,
complement and background/spacing cells. Native template bounds follow all
intermediate pen movements; overhanging ink outside those bounds is clipped.
Scratch allocation failures and cleanup of privately owned storage remain
handled. Soft styles, color fonts and reverse-path drawing still need real
implementations and remain outside this qualification.

`TextLength` now reads the optional public signed spacing/kerning tables without
requiring glyph storage, an allocation marker or the proportional flag. It
includes leading negative kerning. Both text entries consume the full unsigned
low 16 bits of the count; upper D0 bits are ignored, and the length result follows
the Classic native WORD behavior.

A second independent native probe found that `PrintIText` restores the caller's
foreground/background pens and drawing mode after processing its text chain.
CopperStart had left the last text's settings behind. It now restores those
settings while retaining the final cursor position. The linked-chain regression
fails on the old emitter and passes with the correction.

A later gameplay pass reaches two constructed track segments, then loses useful
menu input after selecting Frozen game speed. The higher-priority Intuition
input task remains READY while the game runs inside ROM beam-wait routines;
modifiers are released and the message pool is not full. The standalone
`WaitTOF` override only spins on the beam. The compiled main defers interrupt-exit
rescheduling inside ROM transactions, so this implementation can repeatedly
starve the input task instead of performing the documented task handoff.

`WaitTOF` now blocks through Exec and wakes from a stack-owned VBlank tail
server. It uses the native reserved `SIGF_SINGLE` ($10), without signal
allocation or public Exec/Task metadata legitimacy gates. It detaches the server
and clears its reserved pending signal before returning. The compiled main and
its private transaction guard remain unchanged. This implements the documented
wait rather than adding a title-specific scheduling workaround.

The apparent blank newspaper during an intermediate preview was **not** a
confirmed guest fault: the raw field-155000 BMP matches the later rendered
newspaper byte for byte. The independent native caller-state probe, not that
preview, establishes the `PrintIText` correction.

The engine-selection screen then exposes another fault: CopperStart reports
the same global and low-resolution window mouse coordinates. Native Intuition
uses high-resolution/interlaced global units and screen pixels in the public
Screen, Window and IntuiMessage fields. The game halves its global position,
which makes the right half of its engine picker unreachable with the old units.

The correction implements those native units, default motion scaling with a
fractional remainder, screen-dependent coordinate conversion, and physical OCS
pointer placement. Window activation initializes its mouse fields consistently.
There is no title patch or pointer teleportation. Four-mode instruction cases
cover conversion flags; authored reset/media probes qualify actual 320- and
640-pixel non-interlaced screens. Opening 400-line LACE screens remains outside
this qualification.

## Independent native comparisons

Authored boot blocks use ordinary graphics/Intuition/Exec calls against both
Kickstart 1.3 and CopperStart. Each comparison uses the same authored disk; these
are API oracles, separate from the real-game replay.

- 48 text cases cover all eight draw modes, initially empty/filled planes,
  ordinary spacing, leading positive kerning and overlapping negative spacing.
  Pixels, final cursors and measured lengths match the native reference.
- Metric-only fonts have NULL glyph storage and no proportional flag. The
  ordinary case and a count of 32768 with nonzero upper D0 bits match native.
- Explicitly different caller/text pens and modes confirm `PrintIText` state
  restoration. The original and corrected outcomes are retained.
- A separate authored parent/worker pair registers a VBlank server that wakes
  the higher-priority worker. Across eight parent `WaitTOF` calls, native and
  corrected CopperStart each record eight worker runs; the old spin records
  zero. The worker allocates its own signal dynamically. The permanent
  regression checks both RAM profiles and permits an additional early wake.

- Six mouse comparisons cover low/high resolution, odd movement and clipping.
  Final global, screen and window positions match Kickstart 1.3. Ordinary and
  odd MOUSEMOVE message coordinates match as well. This does not assert complete
  event-stream parity: native repeats motion events while pushing against a
  clipped edge, whereas CopperStart currently emits on position changes. Startup
  RAWKEY streams also differ and are outside this coordinate qualification.

The final ROM retains byte-identical text/raster code from the earlier 48-case
text oracle. Its entire graphics stage matches the signal-WaitTOF composition.
The PrintIText caller-state and scheduling probes are freshly rerun against the
final composition. Code ranges, hashes and vector targets are verified in JSON.

The [Classic graphics autodocs](https://www.theflatnet.de/pub/cbm/amiga/amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node05A6.html)
specify the 16-bit text count. The
[graphics text chapter](https://wiki.amigaos.net/wiki/Graphics_Library_and_Text)
describes the public font tables. Native probes establish the detailed drawing
and caller-state behavior exercised here.

The Classic coordinate units are also described in the [Programmer's Guide to
the Amiga, p. 157](https://retro-commodore.eu/files/downloads/amigamanuals-xiik.net/eBooks/Programmers%20Guide%20to%20the%20Amiga%20-%20eBook-ENG.pdf).
The authored native probes establish the default scaling and pixel conversion
used in this qualification.

## Build and coverage

The source Release solution build passes with zero warnings/errors. Its native
emitter produces exactly the same ROM as the frozen qualification emitter.
The ROM remains 524288 bytes, with the unchanged 466904-byte compiled main,
31582 native graphics bytes (64 supported vectors) and 7142 native Intuition
bytes. ROM SHA-256:
`334cd0e9d2ad25410ab449f79e42e2b254e8085dbbf94e2d89c86f4db8b24d13`.

The source is [CopperStart commit 1b83139](https://github.com/ilehtoranta/CopperStart/commit/1b8313985a9e8f95165e9cd973cd231af64e8a39), snapshot tree `8c9a28e24a55997140a4ecf68a87a4ed218f2497` on
the CopperStart branch `codex/copperstart-intuition-20261018`. Both native DLL identities are pinned
alongside the reused unlinked main and all composition inputs. The compiled
main/Exec/DOS/System binaries are unchanged; prior unit suites for those
components are not relabeled as fresh results.

The emitted graphics/Intuition instruction suite passes 311 cases with no skips.
The exact-ROM reset/media suite passes 98 cases, including two scheduling RAM
configurations and two native mouse coordinate modes, with no skips. A first
setup attempt lacked the manifest at the test loader's required filename; its
88 failures and 10 passes are retained as invalid setup. The corrected fresh
98-case run is the final result. A superseded mouse prototype failed its pointer
sprite Y assertion; corrected hardware placement passes both component and
actual-ROM checks. Its warmup replays were canceled and provide no gameplay pass.
The 14 optional standalone polled-reader tests are excluded from fresh coverage.
The real-media engine and the reset/media test fixture have distinct pinned
engine identities; neither is silently substituted for the other.

## Replay protocol and limits

The frozen Lightweight engine and pinned Copper68k run a PAL OCS 68000 machine
with 512 KiB Chip, 512 KiB slow RAM, one drive and RTC disabled. Original media
identities are retained from the preceding record. Disk changes and all gameplay
inputs enter through ordinary host media/input APIs. There are no host OS shims
or guest RAM/register writes. A host probe permits pausing between fields to
append future physical inputs; its native 160000-field prefix reproduces all
805 original-runner capture files byte for byte.

A separate native control reaches track construction, a London Junction depot,
locomotive/car purchase and Train #1's report at field 203000. An incorrect
locomotive-identification answer imposed the game's two-train restriction. That
restriction was retained, with no bypass. The native control does not demonstrate
a moving route or revenue and is not a full campaign pass.

The final normal and scalar runs each stop cleanly at field 242000 with exit
code zero. All 1215 BMP, machine-state, Chip RAM, slow RAM and PCM capture files
match byte for byte, including final CPU/hardware/cycle state. No captured
unsupported-chipset or video path is reported. The portable capture-set SHA-256
is `ae17cf03358f5f726677e56b4e54d9cc8ade489d058bae4bb117f6d5f733fc32`.

Reviewed final-ROM milestones:

| Field | Observed result |
| --- | --- |
| 143000 | World complete prompt |
| 155000 | Opening newspaper renders beyond the old text crash |
| 174000 | American answer accepted; no two-train restriction |
| 194000 | F8 facility menu responds in Frozen mode after track construction |
| 202000 | London Junction depot built |
| 213000 | Engine picker ready |
| 231000 | Grasshopper selected; Add car menu reached |
| 239000 | Mail Car added |
| 242000 | Train #1 Mail Local report at London Junction |

The engine-picker click uses physical mouse input: global position moves from
(200,100) to (400,16), and the public window position from (100,50) to (200,8).
The old-unit ROM had returned to the depot; the corrected ROM proceeds to car
selection. The next panel takes additional loading time: the depot background
at field 221000 is a transition, not an engine-selection failure.

The final report shows a 0-4-0 Grasshopper valued at $3,000, Mail Local, and
Unloading/loading with zero sacks of Mail. This proves construction, engine/car
purchase and report access. It does not establish route movement or revenue.
The report is retained as the bounded stopping milestone.

The intermediate 221000-field signal-WaitTOF runs also have all 1110 captures
identical between modes, including the old mouse-unit engine-selection failure.

The earlier 202000-field normal/scalar runs have all 1015 capture files
identical, including the stalled input condition. Read-only guest RAM shows
cash (in thousands) falling from 1000 to 994 to 988 across the two track segments,
even though the frozen header retains its earlier amount. This distinguishes a
successful construction operation from its screen-refresh behavior.

These are bounded correctness checks, not host-throughput measurements. Saves,
audio fidelity, a complete campaign and broader AmigaOS compatibility remain
unqualified. ROMs, game media and local capture/build artifacts are not committed.
