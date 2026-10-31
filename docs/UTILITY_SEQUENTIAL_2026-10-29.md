# Sequential utility verification — 2026-10-29

Work through the collected utilities individually. Verify the main screen
(or basic invocation for a command-line utility), investigate and fix failures,
and commit and push each fix before advancing to the next utility. Previous
[collection and A/B evidence](UTILITY_TEST_CORPUS.md) retains its original inputs
and scope.

A row remains pending until its runtime evidence and input provenance have
been inspected. Missing prerequisites do not count as a successful utility run.
After the fixes, repeat the main-screen checks against the final source before
marking the collection complete.

| Order | Utility | Latest runtime status | Evidence |
| --- | --- | --- | --- |
| 1 | ProTracker 2.3F (2026-05-09 build) | VERIFIED: main editor and selected inputs, both ROMs | [Receipt](UTILITY_PROTRACKER_2026-10-29.json) |
| 2 | X-Copy 5.21 English (1991-11-17), Cyclone 9.11 disk | VERIFIED: DOSCOPY main screen, both ROMs; disk operations unverified | [Latest receipt](UTILITY_XCOPY_DISK_RESOURCE_2026-10-29.json) |
| 3 | Amiga Test Kit 1.21 | VERIFIED: main menu and selected audio controls, both ROMs | [Receipt](UTILITY_TESTKIT_2026-10-29.json) |
| 4 | SysInfo 4.4 | VERIFIED: main information screen, both ROMs, with explicit guest GUI prerequisite; controls unverified | [Latest receipt](UTILITY_SYSINFO_MAIN_2026-10-30.json) |
| 5 | PPMore 1.8a | VERIFIED: main reader and footer, both ROMs, explicit 2 MiB profile and guest prerequisites; original 1 MiB lane fails; navigation unverified | [Main-screen receipt](UTILITY_PPMORE_MAIN_2026-10-30.json) |
| 6 | Personal Paint 7.1c complimentary edition | VERIFIED: main editor, restored caption and requester dismissal, both ROMs; explicit guest prerequisites and 2 MiB Fast RAM; editing/menu commands unverified | [Latest receipt](UTILITY_PPAINT_BACKDROP_2026-10-31.json) |
| 7 | PPShow 4.0 | VERIFIED MINIMUM: ILBM fixture and border, native 3.1 and CopperStart with explicit guest prerequisites; frozen graph, final toolchain sweep pending | [Main-screen receipt](UTILITY_PPSHOW_MAIN_2026-10-31.json) |
| 8 | DiskMaster 2 2.5.30a (V37 build) | VERIFIED MINIMUM: three populated panes, native 3.1 and CopperStart; file operations unverified, frozen graph | [Main-screen receipt](UTILITY_DISKMASTER_MAIN_2026-10-31.json) |
| 9 | MuchMore 4.6 | VERIFIED MINIMUM: text reader and Space scrolling, native3.1/CopperStart; font/scroll-distance differences recorded, frozen graph | [Main-screen receipt](UTILITY_MUCHMORE_MAIN_2026-10-31.json) |
| 10 | MemCheck 1.2 (37.13) | VERIFIED MINIMUM: default segment test starts; ADDRESSONLY finishes with0errors and CLI return on native3.1/CopperStart, guest GUI prerequisite; full segment completion unverified | [Receipt](UTILITY_MEMCHECK_2026-10-31.json) |
| 11 | Amiga LhA 2.15 | VERIFIED MINIMUM:5-member CRC test and CLI return; default4KiB command stack stays in bounds | [Startup receipt](UTILITY_LHA_STARTUP_2026-10-31.json) |
| 12 | DeluxePaint V demo 5.0b9 (1994-08-12) | VERIFIED MINIMUM:32-color editor on native3.1/CopperStart; drawing differs on CopperStart and remains under investigation; final source sweep pending | [Startup receipt](UTILITY_DPAINT_STARTUP_2026-10-31.json) |

The initial lane is the existing PAL A500 profile: MC68000 interpreter,
OCS 8371/8362, 512 KiB Chip and 512 KiB slow RAM, no Fast RAM, RTC or host
folders, and one protected DD DF0. Expanded-machine and OS prerequisites in
the collection manifest still apply; they must be resolved and recorded for
each utility rather than silently changing this lane.

## ProTracker result

Both native Kickstart 1.3 and the rebuilt standalone CopperStart ROM complete
26,000 fields and display the main editor. Three entered notes' complete
1,024-byte pattern, sequencer play/stop state and mouse coordinates match at
eight checkpoints. Both final screens were inspected. The v3 workload starts
its actions at field 24,000, after both ROMs are ready. The earlier v2 captures
remain unchanged.

CopperStart commit `1db841a090dac5b4105a5f05ed7cf7679de95647` fixes the ROM layout, stale native copy
entry branches and repeated small reads during HUNK loading. All 94 selected
loader/copy tests pass and the Release solution builds. The development ROM is
524,288 bytes, SHA-256 `096e4c1a49d01bc94e1f3e25da38b3f141e97992cd69f38e39bc8f3e0e883d71`, checksum `$FFFFFFFF`.
The [producer note](https://github.com/ilehtoranta/CopperStart/blob/1db841a090dac5b4105a5f05ed7cf7679de95647/Docs/NativeHunkReader20261029.md)
records the source changes and remaining ROM headroom.

Cold startup is still slow: the editor appears between fields 21,000 and 22,000.
This verifies the main screen and selected interactions; it does not qualify
nonempty sample playback, disk operations, every GUI control or host throughput.
The app firmware pin is unchanged. Local raw build/replay captures are under
`artifacts/utility-sequential-2026-10-29/`; ROMs, media and captures remain ignored.

## X-Copy investigation

Kickstart 1.3 reaches X-Copy's DOSCOPY application after F1. CopperStart initially
showed a corrupted boot menu and ignored F1. Producer commit
`d35c48e99abab79f9e739d93a5af30c7f34ec488` establishes the boot-time display-fetch
default and keyboard acknowledgement before DOS starts. The corrected menu at
field 2,900 matches the native ROM's pixels exactly, and F1 exits the menu.
All ten selected native boot/console tests pass. The source-built development
ROM is SHA-256 `8ec53c897f37b6b68b0bd7d0b038b238c33c5230cfd09d81189711c189e02601`.
The [producer note](https://github.com/ilehtoranta/CopperStart/blob/d35c48e99abab79f9e739d93a5af30c7f34ec488/Docs/NativeBootTakeover20261029.md)
records the implementation, ROM layout and remaining failure.

The bundled CliMenu program calls DOS Execute to launch XCopyPro and Endcli.
The recovery CLI lacks a registered Shell/CLI resident, so shell acquisition
fails with error 205. Startup continues to SetMap, whose missing keymap resource
leads to an unsupported requester. The 7,200-field corrected-ROM replay still
crashes and does **not** verify the X-Copy application. Continue with ordinary
guest shell execution and repeat this original-media test before advancing.

ProTracker also completes a 26,000-field regression replay under this ROM. Its
complete pattern and sequencer mode match all eight earlier checkpoints. Seven
screens match the previous CopperStart replay exactly; the field-24,500 playback
screen differs. The final editor was inspected. The earlier receipt remains
unchanged, and the [X-Copy receipt](UTILITY_XCOPY_2026-10-29.json) records this
additional regression check.

The remaining ten utilities have not been tested in this sequence. The full
collection goal remains open, and the app firmware pin is unchanged.

### Recovery CLI follow-up

Producer commit `6122489bcdb8aa4a97c021bdd7d7c5348559defa` registers the recovery
CLI for native command launches and reads its command string line by line.
The standalone ROM also reuses its original console glyph source for the public
font and packs the graphics header to fit. Its SHA-256 is
`2a0bbf8cc7aa217be21bb222d9cc1c4fd69012c122b495e653b106be932b662c`.
Fourteen selected native launcher, keyboard and font tests pass; the command
fixtures also pass under Kickstart 1.3 and fail under the preceding CopperStart
ROM. All 44 compatibility-report validator cases pass.

X-Copy now starts and displays **Loading XCopyPro**, but its 12,000-field run
waits for a CIA-B timer flag at `$C1D094` and never reaches DOSCOPY. Continue this
startup investigation before advancing. The boot menu still matches native
pixels. ProTracker completes another 26,000-field run; its main editor was
inspected and its pattern/mode match all eight native-reference checkpoints.

Two existing graphics takeover probe cases fail under both the previous and
current ROM. They remain open and are not counted in the passing launcher/font
set. The independent font checks cover every original glyph and the default cell.
The [latest receipt](UTILITY_XCOPY_SHELL_2026-10-29.json) and
[producer note](https://github.com/ilehtoranta/CopperStart/blob/6122489bcdb8aa4a97c021bdd7d7c5348559defa/Docs/NativeRecoveryCli20261029.md)
preserve the inputs, checks, remaining failures and limits.

### Disk resource follow-up: main screen verified

The bounded startup trace identifies an earlier divergence: X-Copy's
`OpenResource("disk.resource")` returns zero. Its fallback runs before the
program installs its own interrupt vectors, leading to the later timer wait.
Producer commit `1d971eceb8eeeb207cf04b442474c0fb13aa0208` publishes the native
identification record and `GetUnitID`. It also preserves the original internal
DD drive's special ID behaviour, checked against Kickstart 1.3. Shared disk
ownership APIs remain unsupported.

The final standalone ROM is 524,288 bytes, SHA-256
`a71247db3c4aa88d9b4a46c2b4f0157acbf7136cac1a8b50ecb80e88d9f19003`,
checksum `$FFFFFFFF`. Removing replaced graphics constructors and compacting
the pen table saves 492 bytes. The resident at `$FF9E08` leaves 66 bytes before
graphics and 16 before the checksum. Both production solutions build, and all
19 selected native identification/CLI/keyboard/console/font/graphics cases pass
without skips. The native identification control and five compiled drive
geometry/motor cases pass separately.

Both original-media X-Copy runs complete 12,000 fields with the unchanged F1
workload and display DOSCOPY, with RAM selected and DF0 available as source and
target. The screens were inspected; their pre-launch menu pixels match exactly.
The main screens have different free-memory counters. This verifies startup and
the main screen; disk check/copy/format and Cyclone remain unverified.

ProTracker completes another 26,000-field regression with the unchanged v3
workload. The main editor was inspected, and its complete pattern and sequencer
mode match all eight native-reference checkpoints.

The broader compact graphics fixtures retain the same 109 failing cases under
both the prior and changed emitter (143 pass in each). They remain open, along
with the earlier two full-ROM takeover probe failures. A separate pixel
reference fixture calls OS 3.0 AllocBitMap, so its failed Kickstart 1.3 attempt
is unavailable reference coverage. These are not counted in the 19 passing
selected cases. See the [receipt](UTILITY_XCOPY_DISK_RESOURCE_2026-10-29.json)
and [producer note](https://github.com/ilehtoranta/CopperStart/blob/1d971eceb8eeeb207cf04b442474c0fb13aa0208/Docs/NativeDiskIdentification20261029.md).

ProTracker and X-Copy now meet the sequence's main-screen requirement. The next
utility is Amiga Test Kit. The remaining ten and the final current-source sweep
are still pending; the overall collection goal and app firmware pin are unchanged.

## Amiga Test Kit result

Both 7,200-field runs reach the main menu with the current standalone ROM and
native Kickstart 1.3. The unchanged v2 audio workload opens F5 Audio, disables
the four channels individually, then restores them with F7. The reviewed main
and audio screens match pixel for pixel across ROMs. Captured PCM follows
stereo, left-only, silence and restored stereo on both. The main menu and audio
states were inspected; this does not qualify the entire diagnostic suite.

The earlier report that static labels disappear is contradicted by raw pixels
in both the current and original 2026-10-28 captures. At fields 3,100, 3,800,
4,000, 4,200 and 7,200, the static-label crop is unchanged. Masking only the
channel ON/OFF indicators also leaves the entire current audio framebuffer
identical across those fields. The earlier preview interpretation was wrong;
the original captures and fingerprints are preserved. No engine fix is needed
for that observation. The [receipt](UTILITY_TESTKIT_2026-10-29.json) records
pixel hashes, regions and per-channel PCM RMS.

The next utility is SysInfo. Nine utilities and the final sweep remain pending.

## SysInfo startup investigation

An input-free native Kickstart 1.3 run reaches the information screen and
completes 7,200 fields. CopperStart initially fails in the preceding disk
startup command `C:SetPatch_1.38`: its C cleanup reads a zero `pr_ReturnAddr`
and moves SP to `$FFFFFFF4` at field 2,125. This happens before SysInfo starts.

Producer commit `a58bca7053d775d92819d3b04e267b01cc3ba209` publishes the command's
public stack-size word and DOS return frame, restores the parent frame across
nested launches, and reserves the additional metadata without consuming the
requested stack. The authored exit/nested fixtures pass under both native 1.3
and the new ROM and fail under the preceding CopperStart image. All 90 focused
core/compiled-callback tests and 20 selected native guest/instruction cases pass.
The production producer solution builds.

The 524,288-byte development ROM is SHA-256
`7d443d60e1001365a845f16bc7f02b58b066be16ab7a71878a86fe3470584dde`,
checksum `$FFFFFFFF`. The 499,138-byte compiled root is followed by a 50-byte
guest continuation at `$FF9DCA`; 12 free bytes remain before graphics at
`$FF9E08`, and 16 before the checksum. The app firmware pin is unchanged.

The original disk now gets past that first failure. A 36,000-field run still
does **not** reach SysInfo's main screen. At field 10,772 it requests a
640x256, three-plane screen with modes `$9020` and type `$118F`; Classic
Intuition rejects it. Its error cleanup then calls ActivateWindow beyond the
current library image. Separate native screen probes accept the request:
Kickstart 1.3 preserves legacy mode flags, while native A500 Kickstart 3.1
coerces it to available OCS hires. Continue screen coercion and real window
support before advancing. A completed diagnostic run is not an application pass.

The new ROM retains all three prior results: ProTracker's editor and eight
native pattern/sequencer checkpoints after 26,000 fields, X-Copy's DOSCOPY
screen after 12,000 fields, and six matching Test Kit main/audio framebuffers
through 7,200 fields. Screens were inspected. The
[receipt](UTILITY_SYSINFO_STARTUP_2026-10-29.json) and
[producer note](https://github.com/ilehtoranta/CopperStart/blob/a58bca7053d775d92819d3b04e267b01cc3ba209/Docs/NativeCommandReturnFrame20261029.md)
record the remaining failure and bounds. SysInfo, the eight later utilities
and the final source sweep remain open.

## SysInfo native GUI and palette follow-up

Producer commit `b907b7b3e8f9dccad4b957852fd6ba1faf35bbb2` lets the existing
disk-loaded native GUI retire the idle Classic screen owner and coerces legacy
hires requests to the available OCS mode. Its native guest test passes, opening
and closing a real screen, window and Layer with physical input installed.
The generated prerequisite loader retains the whole native module until reset,
including partial initialization that has published graphics callbacks.

SysInfo then reaches `LoadRGB4` with `$00030008` in D0: the low WORD requests
eight colors. Treating the full register as the count faults. All four new
instruction cases fail before the WORD correction and pass afterward. An
independent guest palette probe passes under both native 1.3 and A500 3.1.
The producer solution builds; the separately recorded concurrent TextLength
fixture failure is not counted as a successful graphics suite.

The original disk's information screen is also verified under native A500
Kickstart 3.1 after 12,000 fields. A controlled prerequisite installation keeps
the SysInfo program unchanged and reaches its main screen under native 1.3.
Its startup skips installation when native Intuition already provides the GUI.

A diagnostic-only eight-byte correction to the preceding CopperStart ROM gets
past LoadRGB4, then faults in `FindDisplayInfo` at field 25,671. The existing
native display database needs to join the disk prerequisite. That diagnostic
is not a current-source ROM build, and SysInfo still has no verified CopperStart
main screen. Continue this utility; eight later utilities and the final sweep
remain pending. The [receipt](UTILITY_SYSINFO_GUI_2026-10-29.json) and
[producer note](https://github.com/ilehtoranta/CopperStart/blob/b907b7b3e8f9dccad4b957852fd6ba1faf35bbb2/Docs/NativeSysInfoGui20261029.md)
separate the passing component checks, native application controls and remaining
failure. The firmware pin is unchanged.

### Main screen verified — 2026-10-30

The display database, native task trap delivery and missing startup Copper/VBlank
service are now composed into the disk-loaded GUI prerequisite. The controlled
v5 disk preserves the original SysInfo executable. Actual chipset framebuffer
captures show the main information screen under both CopperStart and Kickstart
1.3. CopperStart completes 32,000 fields; its captures from fields 24,000 through
32,000 are pixel-identical. Native 1.3 completes 12,000 fields. Different
ROM/library versions are visible; cross-ROM pixel identity is not asserted.

The standalone ROM was rebuilt from an isolated archive of producer commit
`b907b7b3e8f9dccad4b957852fd6ba1faf35bbb2`. Its full source-built bytes match
the preceding palette diagnostic image. The guest prerequisite is a separate
source checkpoint; concurrent ROM-layout work is excluded. The rectangle
regression checks 128 white pixels and an adjacent gray pixel after four
fields. Trap cases check real F-line, illegal opcode and TRAP #2 frames under
both ROMs, retaining the native module until reset.

The [new receipt](UTILITY_SYSINFO_MAIN_2026-10-30.json) records exact inputs,
hashes, build and test scope. Earlier failed replays remain historical evidence.
SysInfo controls and benchmarks, the eight later utilities and the final
collection sweep remain unverified. This work does not change the app firmware
pin or qualify host performance.

### PPMore window-console output — 2026-10-30

Producer commit `096407055061bbbf655ed93d3f6cd085c48fd991` implements native
console unit 0 output. Its caller-owned IOStdReq regression passes under both
CopperStart and Kickstart 1.3, checking real glyph pixels, split cursor-position
sequences, inverse text, byte accounting and cleanup. The existing desktop
upgrade regression and SysInfo's 32,000-field main-screen replay also pass.
Concurrent ROM/A1200 and copy-kernel changes are excluded from the commit.

On the separate 2 MiB diagnostic profile, PPMore now opens its reader Window
and console device successfully. It then calls the unimplemented Intuition
`IntuiTextLength` vector at -330 and reaches the provider's rejection trap at
field 31,344. The 40,000-field ordinary replay loses execution; no retained
reader is verified. Native Kickstart 1.3 retains the expected 82-line reader
through 12,000 fields. The original 1 MiB memory failure remains unresolved.

The [window-console receipt](UTILITY_PPMORE_WINDOW_CONSOLE_2026-10-30.json)
records exact inputs, profiles, component passes and application failure.
PPMore remains the current utility; later utilities and the final sweep remain
pending. The application firmware pin is unchanged.

### PPMore reader retained with text measurement — 2026-10-30

Producer commit `d89b64a841e76516a0f070eb16d1f645fa980ff2` implements IntuiTextLength
and installs native graphics TextLength when that vector is missing. The
measurement probe passes against CopperStart and native 1.3, including a
proportional font, spacing/kerning, an adjacent caller-owned TextAttr, default
glyphs, WORD count handling and balanced font ownership. Window-console and
desktop regressions pass with the same generated module, and the solution builds.

The final controlled disk retains the unchanged PPMore program and libraries.
With the explicit 2 MiB diagnostic profile, CopperStart now displays the reader's
82-line title and fixture text through 40,000 fields. Captures from fields
32,000 through 40,000 are pixel-identical. Native 1.3 retains its reader through
12,000 fields. SysInfo's separate original-profile regression still reaches
its main information screen through 32,000 fields.

PPMore remains in progress: the original 1 MiB lane has no reader at 40,000
fields, and the CopperStart reader lacks the footer control strip visible
under native 1.3. Navigation is unverified. The new
[text-length receipt](UTILITY_PPMORE_TEXT_LENGTH_2026-10-30.json) records those
limits, exact inputs, component passes and retained failed-candidate evidence.
The public OpenFont adjacency defect is also recorded separately; the new
IntuiTextLength uses its own attribute snapshot. Later utilities and the final
collection sweep remain pending. No app firmware pin or performance result changes.

### PPMore expanded-profile main screen verified — 2026-10-30

Producer commit `d9bc581681cf4550a472bf4d6f59956e79870946` reserves the classic
ten-pixel bottom sizing border requested by PPMore. The preceding provider
reported two pixels, allowing console output to erase the footer gadgets.
The new native regression fails on that provider and passes on native 1.3 and
the corrected provider, checking actual footer glyph pixels after console I/O.
Text measurement and desktop upgrade regressions also pass; no cases are skipped.

The final controlled disk displays the reader's 82-line title, fixture text and
all seven footer labels under both ROMs. CopperStart's nine captures from fields
32,000 through 40,000 are pixel-identical; the final screen was inspected.
Native 1.3 completes 12,000 fields. SysInfo's original-profile regression still
reaches its information screen through 32,000 fields.

This meets the main-screen criterion only in the recorded expanded diagnostic
profile: 512 KiB Chip, 512 KiB slow and 1 MiB AutoConfig Fast RAM, with the pinned
guest GUI, Shell and library prerequisites. The original 1 MiB lane is also
rerun on the final disk and still fails to display a reader at 40,000 fields.
It is retained as a failure, not counted as an expanded-profile pass. Navigation,
search, packed files, requesters and resize input remain unverified.

The [main-screen receipt](UTILITY_PPMORE_MAIN_2026-10-30.json) records exact
inputs, scoped passes and failures. Proceed to Personal Paint; the remaining
utilities and final source sweep remain open. The app firmware pin is unchanged.

### Personal Paint prerequisites and hardfile control — 2026-10-30

The included manual requires at least 2 MiB RAM and Workbench libraries in
addition to Kickstart. The 685,000-byte program plus guest prerequisites does
not fit a single DD disk. A new local recipe installs the complete unchanged
archive, its libraries and the pinned GUI/Shell into a read-only 16 MiB OFS
hardfile. All 401 files read back byte-for-byte. Its optional system-file import
also checks ADF/ZIP byte equivalence and Amiga's case-insensitive paths using
an existing OFS fixture; that fixture is not a Workbench or paint control.

Native Kickstart 1.3 cold-boots the hardfile on the explicit 3 MiB diagnostic
profile, launches PPaint and reports missing `mathtrans.library`, returning 20.
The actual Workbench 1.3 inputs used by earlier tests are missing locally; their
location has been requested. This is unavailable application-control coverage,
not a paint screen or emulator failure. CopperStart's frozen Classic floppy ROM
does not show hardfile startup within 40,000 fields; its storage/bootstrap
profile needs separate investigation before interpreting application behavior.

The [startup receipt](UTILITY_PPAINT_STARTUP_2026-10-30.json) records identities,
profiles, prerequisite failures and pending work. Personal Paint remains current;
later utilities and the final sweep remain pending. No firmware pin changes.

### Personal Paint storage setup and native FFP prerequisite — 2026-10-30

The archived source establishes why the initial CopperStart hardfile did not
boot: NativeCopperHdfBoot accepts DOS1/FFS partitions, while that image was
DOS0/OFS. An FFS twin with byte-identical guest files boots and launches PPaint
using the documented read-only partition geometry. No engine or ROM fix is
needed for this format mismatch; the original failed setup remains recorded.

PPaint then reports missing mathffp.library. Producer commit
`fd596cd86ea263986a7811c4f859a1b7ce4cc45d` packages the existing native FFP kernel
as an explicit guest-loaded prerequisite. After the installer returns, all 51
existing result and N/Z/V cases pass under CopperStart and under the independent
native 1.3 ROM library. The installer leaves an existing library in place and
retains its separately loaded module until reset. The standalone ROM is unchanged.

The controlled OFS/FFS pair now has 403 identical logical files. Both ROMs launch
the original PPaint program and stop at the same missing mathtrans.library
prerequisite. Workbench 1.3 inputs remain unavailable; the collected utility
archives contain no replacement system library. This is not a verified paint
screen. The [new receipt](UTILITY_PPAINT_LIBRARIES_2026-10-30.json) records exact
profiles, file equality, FFP component passes and remaining coverage limits.
Personal Paint remains current; later utilities and the final sweep remain open.

### Personal Paint native editor control and CopperOS mathtrans — 2026-10-30

A Workbench 1.3 ZIP is now available in the local TestImages directory. Its
system libraries remove the earlier prerequisite blocker. The complete
installation has 580 byte-identical logical files across the OFS/FFS pair.
Kickstart 1.3 reaches Personal Paint's editor and a Memory save mode requester.
Replacing only mathtrans.library with the frozen CopperOS development 68000
build produces the same editor/requester pixels. Return dismisses the requester
and leaves an unobstructed editor. This establishes the native startup control,
not mathtrans conformance or paint-operation coverage.

The library recipe now checks the CopperOS manifest's CPU, name, byte length
and SHA-256. CopperOS's source was untracked when frozen; the receipt retains
binary and compiler input hashes without claiming release qualification.
The original Worms IPF also yields diskfont 34.37 and confirms the earlier
recovered mathtrans binary's provenance. Installing diskfont alone progresses
native startup to a missing icon.library error; the complete Workbench input
is used for the application control.

CopperStart still fails before reaching the editor. The original Workbench
startup first fails in the BCPL Assign command at field 2,181, independently
of PPaint. An explicit direct-launch setup retains that failed lane and avoids
Assign/CD; native 1.3 still reaches its editor. CopperStart's direct-launch
instruction trace then catches a separate fault at field 3,124 in Exec
SetFunction, with a library parameter of 1 and vector offset -18. The origin
of that invalid argument remains unresolved; adding a pointer guard is not a
justified fix. Both failures and the native passes are retained in the
[control receipt](UTILITY_PPAINT_CONTROL_2026-10-30.json).

The external probe adds explicit read-only HDF modes and a versioned 3 MiB
correctness profile, preserving existing ADF invocations. Its Release build
passes with zero warnings/errors. No production engine or ROM behavior changed.
Personal Paint remains current; later utilities and the final sweep remain open.

### Personal Paint diskfont loader correction — 2026-10-30

CopperStart main commit `373939a586dc816d13cd4a63fa474e00d99db16b` fixes the
guest Shell disk-library loader's assumption that InitResident returns a base.
Diskfont 34.37 publishes a valid library but returns 1. The loader now resolves
the resident's public name in Exec's list before patching Close/Expunge.
The former field-3,124 fault is eliminated: the trace opens diskfont successfully
at field 3,125 and proceeds through Personal Paint's own I/O libraries.

The final authored OFS control checks AUTOINIT and direct initialization returning
1, including signature calls, version rejection, full-path reuse and Close
counts. Both cases pass under native Kickstart 1.3 and CopperStart, without
skips. The initial native fixture lacked explicit public library metadata;
its failures remain recorded and the corrected fixture passes independently.
The corrected Shell compiles freestanding and the isolated Release solution
build has zero errors and seven warnings. The ten compiler inputs are pinned.

This is a completed loader correction, not a paint editor pass. The new
40,000-field run eventually corrupts guest PC/stack state without displaying
the editor. A late-startup instruction trace is investigating the next failure.
The original Workbench BCPL Assign failure also remains independently open.
The [diskfont receipt](UTILITY_PPAINT_DISKFONT_2026-10-30.json) preserves all
scoped passes, earlier fixture failures, new image identities and remaining
application limits. Personal Paint remains current; no later utility has advanced.

### Personal Paint next failure: missing VideoControl — 2026-10-30

The late-startup trace catches the next fault at field 24,152: PPaint calls
graphics.library VideoControl (LVO -708) with VTAG_VIEWPORTEXTRA_GET, and the
pinned compact ROM routes that vector to ILLEGAL. Its CM_TYPE_V1 ColorMap has
a non-null ViewPortExtra. The subsequent PC/stack corruption is downstream of
that missing API, not evidence of an emulated instruction-timing failure.

Producer main commit `85004104d3525ae5882b40debcff34e60c535155` records a native
ROM control for this API. Six cases (198 calls) pass under original Kickstart
3.1 on PAL OCS, including allocated and caller-owned maps, caller-owned legacy
maps, non-null pointer queries and non-zero flag-query data. A selected case
fails on the pinned CopperStart ROM without its completion marker. The initial
boot-mailbox observer conflicted with ROM-owned RAM; the final probe publishes
through a private Exec resource and distinguishes that observer issue from the
guest API failure.

The native flag rewrites differ from the retained portable implementation;
copying that implementation unchanged would not satisfy the control. This is
completed characterization, not production VideoControl support or an editor
pass. The [VideoControl receipt](UTILITY_PPAINT_VIDEOCONTROL_2026-10-30.json)
pins the trace, native cases, before-fix failure and coverage limits. The API
correction, Personal Paint editor, later utilities and final sweep remain open.

### Personal Paint classic VideoControl and timer corrections — 2026-10-30

CopperStart main commit `72193edc0d28b8433a21503a313327829d79e550` supplies the
characterized VideoControl controls through the guest GUI provider. The expanded
native 3.1 control passes all six cases (408 calls); the corrected replacement
passes allocated, caller-owned V1 and caller-owned V39 cases (204 calls), without
skips. The compact ColorMap factory receives the OCS sprite/base defaults exposed
by those queries. Foreign factories and implemented vectors retain ownership.

The first candidate removes the ILLEGAL failure but exposes repeated failed
timer.device opens from DOS DateStamp. The classic GUI installer now publishes
the existing queued native timer provider and rolls back a new device on later
installation failure. A V1.3-compatible authored probe passes under original
Kickstart 1.3, fails OpenDevice on the preceding GUI module, and passes on the
corrected module: a queued 100 ms request completes, system time advances by
140 ms and DateStamp by eight ticks. This does not qualify every timer unit or
its precision. The original 1 MiB console footer/glyph regression also passes.

The corrected GUI compiles freestanding, its eight input hashes are rechecked,
and the isolated Release solution build has zero errors and nine warnings.
The matched OFS/FFS images contain 580 identical logical files. A fresh native
1.3 replay reaches the unobstructed editor and matches the earlier RGB hash.
CopperStart still has no visible editor after 40,000 fields. Its two Screen
records contain a front 320×200 one-plane startup screen and a hidden 640×256
three-plane screen with only initialized background/title-border pixels.
Hidden bitmap contents are not a visible main-screen pass. The new bounded
trace shows that PPaint explicitly returns the startup screen to the front
after a helper OpenWindow fails. That request has Top=-9,087 and Height=1;
the native 1.3 counterpart has Top=10 and opens successfully. PPaint's newer
system path derives Top from Screen.BarLayer.Bounds.MaxY, but the replacement
leaves the public BarLayer pointer null. The next investigation is the missing
bar layer, not permissive handling of the resulting invalid window coordinate.
The original BCPL Assign lane remains a separate failure.

The [prerequisite receipt](UTILITY_PPAINT_CLASSIC_PREREQUISITES_2026-10-30.json)
preserves scoped results, image/module identities and application limits.
Personal Paint remains current; later utilities and the final source sweep
remain open. These are correctness runs, not throughput acceptance measurements.

### Personal Paint screen bar-layer correction — 2026-10-31

CopperStart main commit `331e0c924fab877641475b36da296c1a67016ebf` publishes
the real title/status layer as Screen.BarLayer and gives it title-bar bounds.
Private ownership and cleanup remain separate from the public pointer. Native
1.3 and 3.1 each pass two authored cases; both replacement cases pass as well.
They check inclusive bounds, LayerInfo membership and the RastPort backlink.
The dedicated Classic upgrade/window, timer and console footer regressions pass.
The generic ownership fixture fails identically at installer stage 150 with
the previous and corrected modules, before opening any screen; it contributes
no window-ownership coverage for this ROM.

Seven compiler inputs remain byte-identical to the previous qualified module;
only DesktopProviders.dll changes. The corrected HUNK passes freestanding checks
and grows by 60 image bytes. The isolated Release solution builds with zero
errors and 19 warnings. A larger artifact from changed compiler inputs is
retained unqualified, separately from this frozen comparison.

The new native 1.3 replay still reaches its editor with the earlier RGB hash.
CopperStart remains a failure: the helper window now opens at Top=11, but the
next call, Intuition.SetMouseQueue(window, 50), reaches the unimplemented -498
vector and ILLEGAL at field 24,588. This predates the scheduled Return input
at field 28,000, so neither an editor pass nor an input pass is recorded.
The next correction must implement the queue limit and reply lifecycle.

The [bar-layer receipt](UTILITY_PPAINT_BARLAYER_2026-10-31.json) preserves the
native/replacement cases, prior fixture failures, module/media identities and
the next fault. Personal Paint remains current. Later utilities and the final
source sweep remain open; no throughput acceptance result is claimed.

### Personal Paint mouse-queue correction — 2026-10-31

CopperStart main commit `b146ba4f24b42b0fcc93d9fbeb648058016e1fe0` implements
SetMouseQueue and WA_MouseQueue initialization, and connects the limit to
native input delivery. The existing message pool remains the fast path;
overflow records are allocated by the GUI task and retained until reply.
Other event classes can use overflow too, so a full mouse queue does not
consume keyboard delivery capacity. IRQ capture remains allocation-free.

Two native 3.1 cases and both replacement cases pass without skips. Each
checks seven setter results and thirteen delivery observations, including
limits 1/0/50, taken-but-unreplied messages retaining credit, discarded motion,
two 50-message waves, and independent raw-key delivery at full mouse capacity.
The timer, original 1 MiB Classic upgrade/window, and console footer regressions
also pass. The isolated Release solution builds with zero errors and four
warnings; the GUI passes freestanding checks with seven frozen compiler inputs
unchanged and only DesktopProviders.dll updated.

The fresh native 1.3 application control reaches the editor with its earlier
RGB hash. CopperStart still fails before field 25,000, before the scheduled
Return input at field 28,000. The new first-fault trace identifies the next
missing API: graphics.library BltTemplate (-36) reaches ILLEGAL at field
24,856. Neither an editor pass nor successful application input is recorded.
The original BCPL Assign lane also remains separately failed.

The [mouse-queue receipt](UTILITY_PPAINT_MOUSEQUEUE_2026-10-31.json) preserves
the native/replacement checks, module/media identities and application limits.
Personal Paint remains current. Later utilities and the final source sweep
remain open; these runs are not throughput acceptance measurements.


### Personal Paint BltTemplate qualification, 2026-10-31

CopperStart main commit `0354f432a3218876a07bc6316c0ac2c52483e103` implements
BltTemplate through the guest graphics extension. Source bit phase, row stride,
partial bytes, drawing modes, pens, plane masks and layer storage are honored.
Each of native Kickstart 1.3, native 3.1 and CopperStart passes 72 cases, with
identical output for unlayered ports, scrolled smart-refresh layers and scrolled
superbitmap layers. All cases compare complete storage including untouched
rows, masked planes, clipped edges and off-viewport pixels. No tests are skipped.
The timer, original 1 MiB Classic window lifecycle and console footer
regressions pass. The native module passes freestanding checks; its seven
frozen compiler inputs remain unchanged and all eight manifest hashes match.
The isolated Release solution builds with zero errors and nine warnings.

A fresh native 1.3 replay reaches the editor with the previous RGB hash.
The CopperStart replay now draws the toolbar and palette at field 25,000, then
fails before scheduled input. The first-fault trace identifies layered SetRast
(-234) at field 25,449: ROM entry 0x00ffb5d4 branches to ILLEGAL 0x00ffbf42.
The extension's existing signature recognizer rejects this entry's register-save
prefix. This is the next investigation; a CopperStart editor pass remains
unverified. Later utilities and the final source sweep remain pending.

The [BltTemplate receipt](UTILITY_PPAINT_BLTTEMPLATE_2026-10-31.json) records
the module and media identities, native/replacement comparisons and next fault.
These are correctness diagnostics, not throughput acceptance measurements.


### Personal Paint SetRast qualification and editor, 2026-10-31

CopperStart main commit `cf2f28dd820fe6e9e6c88451e04e743bb8f2c547` recognizes
the retained compact SetRast prologue and installs the layered implementation.
It also follows native superbitmap semantics: the complete backing bitmap and
visible screen clips are filled while masked planes are retained. Native 1.3,
native 3.1 and CopperStart each pass 72 cases without skips. Their outputs are
identical across drawing modes, pens and unlayered/smart/superbitmap ports.
The timer, original 1 MiB Classic window lifecycle and console footer regressions
pass. The module passes freestanding checks, all eight frozen-input manifest
hashes match, and the separate Release solution build has zero errors and nine
warnings. Final current-toolchain qualification remains pending.

Fresh matched OFS/FFS images contain 580 identical, byte-readback-verified files.
Native 1.3 reaches its previous editor pixels. CopperStart now reaches its
canvas, full toolbar and palette; Return dismisses the Memory save mode
requester, and the screen remains stable at fields 29,000 and 32,000. Drawing
and menu commands remain unverified.

A caption difference remains. An extra native 3.1 replay first stopped at a
missing ENV assignment, which is unavailable editor coverage. A separately
labeled diagnostic image supplies an empty ENV directory and assignment;
native 3.1 then reaches the editor with its caption. The original matched
1.3/CopperStart pair remains unchanged. Structure captures show that native
backdrop windows have layer flag 0x40, while CopperStart's OpenWindow omits
that flag. Consequently its canvas covers BarLayer rather than sitting behind
it. Propagating and qualifying the backdrop flag is the next correction;
Personal Paint remains current before advancing to PPShow.

The [SetRast receipt](UTILITY_PPAINT_SETRAST_2026-10-31.json) records these
controls, editor/input evidence, media identities and remaining caption issue.
These runs are correctness diagnostics, not performance acceptance.


### Personal Paint main editor verified, 2026-10-31

CopperStart main commit `0906c47d7130c1dfd4b7d68cfd44e3b6bca3c23a` forwards
WindowFlags.Backdrop into layer creation, preserves backdrop order on
WindowToFront, and permits built-in default backfill of concealed smart
bitmaps through the native pixel journal. Guest mutation identity and private
snapshot/operation checks remain intact.

The native comparison exposed the full sequence rather than just priority:
WindowToFront is a no-op for backdrop windows on both native 1.3 and 3.1.
Two creation orders pass on each native ROM and CopperStart, including simple
and smart backdrop flags, ordinary windows above BarLayer, unchanged complete
ordering, and an empty window list after closing. No cases are skipped.
The final timer, original 1 MiB Classic window lifecycle and console footer
regressions pass. The native module passes freestanding checks and all eight
input hashes match; two intended DLLs change and six frozen inputs remain
unchanged. Rebuilding the old Layers source matches all 1,430 method bodies;
only the two intended journal methods change after correction. The separate
Release solution build completes with zero errors and nine warnings.

The final matched OFS/FFS pair contains 580 identical, byte-readback-verified
files. Fresh native 1.3 replay retains its editor pixel hash. CopperStart now
displays its caption, full toolbar, palette and canvas. Return dismisses the
Memory save mode requester at fields 28,000/28,004; the editor is stable at
fields 29,000 and 32,000. This meets the main-screen minimum. Drawing and menu
commands, pixel identity to Kickstart and throughput are not qualified.
The original BCPL Assign lane retains its separate failure.

The [backdrop/editor receipt](UTILITY_PPAINT_BACKDROP_2026-10-31.json) pins the
module, media, controls and inspected screen evidence. Earlier failed and
superseded candidates remain identified; a stale incremental candidate was
rejected before qualification. The external instruction probe gains optional
failed-layer stopping, ring capacity and PC sample filtering without changing
guest results or emulated timing.

Personal Paint's minimum check is complete. PPShow is next; the later utilities
and final current-source/toolchain sweep remain pending.

### PPShow prerequisite controls and first fault, 2026-10-31

PPShow requires Kickstart and Workbench 2.0 or later. The native 1.3 run ends
in a software-failure requester and does not supply positive coverage. The
initial native 3.1 run reports missing V37 iffparse.library. Installing that
library unchanged from the privately supplied Workbench 3.1 disk resolves the
open failure; assigning ENV in a separate native startup resolves the next
requester. Native 3.1 then displays the original 320x200 ILBM fixture: eight
color bars with a white border. Inspected output is stable at fields 6,000 and
8,000. This verifies one image display, not viewer interaction or other formats.

The new OFS/FFS images retain the qualified PPaint system testbed and install
the complete PPShow archive, including its bundled libraries, without executing
PPaint. All 593 files pass preparation byte readback. The native ENV variant
has an explicitly different startup and an empty ENV directory. CopperStart
retains the direct startup because the old Workbench Assign lane has its own
recorded failure. These differences are recorded rather than described as an
identical media A/B result.

CopperStart's first captured fault is field 3,626, cycle 515,379,228:
PPShow calls graphics.library SetRGB32CM at LVO -996; vector 0022007C targets
the pinned ROM's ILLEGAL stub at 00FF9FBE. The full replay completes 32,000
fields with corrupted execution and does not display the fixture. Completion
and an empty runner unsupported field do not establish application success.
The [receipt](UTILITY_PPSHOW_2026-10-31.json) records inputs and trace evidence.
PPShow remains current; fixing and qualifying the palette API is pending.

### PPShow palette component corrected; application still pending

CopperStart main commits `54b2e14` and `14adce0` implement compact ROM
SetRGB32CM and native comparison controls. Each control covers 36 color writes
and an out-of-range pen. Native Kickstart 3.1, generated compact graphics and
an installed ROM all pass, with no skips. The tests compare complete map and
table buffers, sentinels, callee registers and stack. Native instruction
observation establishes type-zero aliasing and the low blue-nibble behavior;
initial assumptions that failed the native control are not retained as oracles.

The clean-main Release solution builds with zero errors and ten warnings.
Its native Classic ROM compilation succeeds, but composition fails: compiled
system code extends beyond the fixed graphics start, and the graphics block
would also cross the footer. The raw compiler output is not a usable ROM.

The separate completed single-bank `.44` snapshot provides a diagnostic layout
control. Its captured source and dependencies reproduce its ROM byte-for-byte.
Adding only the new palette file and composition call produces a valid 512 KiB
ROM, grows graphics by 146 bytes and retains 502 bytes before the footer.
The installed palette control passes on that image, and all three controls
also pass after rebuilding the current shared producer graphics sources.
This proves the sampled API correction and fit on that frozen layout, not
Classic A500 integration or final current-toolchain acceptance. The separate
producer prerequisites are not included in the utility commits.

Both frozen baseline and palette candidate complete 32,000 fields against
the same PPShow FFS image but remain at the CLI without launching the viewer.
Their final CPU/hardware/output fingerprints and RGB hashes agree. These are
failed startup controls, not application passes. Native 3.1 remains the positive
image-display reference; the original Classic fault remains preserved.

The [palette receipt](UTILITY_PPSHOW_PALETTE_2026-10-31.json) records source,
ROM, dependency, build and runtime identities. PPShow remains current until
Classic ROM integration and an inspected viewer replay are complete.


### PPShow Classic palette fit, FindColor and next layer failure

CopperStart main commits `ba520ff` and `a91a3dc` fit the compact palette and
FindColor entries into the original Classic ROM reservation. Native controls
also corrected the initialized RastPort extent and AskSoftStyle's line-fit
condition. Ten focused tests pass without skips against native V40, generated
code and the installed candidate. The candidate still uses frozen compiler
output/dependencies; final current-source/toolchain qualification is pending.

The corrected GUI loader is recorded in the [loader receipt](UTILITY_PPSHOW_LOADER_2026-10-31.json).
ReadArgs succeeds. PPShow now opens its screen, then CreateUpfrontHookLayer
returns zero for the window's (0,0)-(319,199) bounds and flags 66. The prior
screen-bar layer succeeds. An 8,000-field preceding-candidate replay shows a
gray display after cleanup, not the ILBM fixture. The bounded corrected-ROM
trace stops at that failed layer call. The [FindColor receipt](UTILITY_PPSHOW_FINDCOLOR_2026-10-31.json)
records identities, native controls and the current failure. PPShow remains
current, with later utilities and the final source sweep pending.


### PPShow minimum viewer verified, 2026-10-31

CopperStart main commit `85cb965` corrects the native pixel journal's rejection
of a Smart window's concealed hook destination. The independent native 3.1
control and corrected provider both create the window, receive one callback
for the display and one for its concealed backing surface, and close with an
empty window list. Rebuilt frozen Layers matches all 1,394 baseline method
bodies; only the two journal methods change. The source Release solution builds
with zero errors and ten existing warnings.

Fresh 593-file OFS/FFS images contain the corrected provider and matching
canonical loader. CopperStart displays the original ILBM fixture and border,
with identical full-frame RGB from fields 4,000 through 12,000. Native 3.1
retains its earlier image hash at fields 6,000 and 8,000. The full frames differ
only in 186 pointer pixels within a 20x16 rectangle; the fixture and border
match. The native-only ENV assignment remains an explicit startup difference.
Native 1.3 is outside PPShow's stated OS requirement and is not a passing lane.

The [main-screen receipt](UTILITY_PPSHOW_MAIN_2026-10-31.json) records ROM,
provider, loader, media, controls and visual evidence. This completes PPShow's
minimum viewer check, not its commands, other image formats or throughput.
The ROM/compiler dependency graph remains frozen and final current-source /
current-toolchain qualification is still pending. DiskMaster 2 is next; no
later utility is marked complete by these PPShow results.


### DiskMaster V37 startup and compact relocation correction

Native Kickstart 3.1 displays DiskMaster's three-pane interface. Return at
fields 6,000/6,004 dismisses About and exposes the SYS directory listing.
CopperStart initially lacks GadTools. A private relocatable 40.4 module from
the supplied native ROM exposes the rejected V37 short relocation tag 0x3F7.
CopperStart main `fb2bd72` accepts that tag; all 32 loader core tests pass, and
an authored native LoadSeg control passes on native 3.1 and the corrected ROM.
The older CopperStart ROM fails that same control.

Main commit `205d3e6` packs the unchanged command bridge into verified ROM tail
padding and routes Shell disk-library loading through installed DOS vectors.
The corrected ROM remains 512 KiB; PPShow retains its previous image hash.
The current source solution builds with zero errors and seven warnings.
Frozen input provenance remains distinct from final current-toolchain coverage.

GadTools now opens at fields 2,456/2,459. The next actual call is missing
intuition.library RefreshWindowFrame (LVO -456), which traps at field 2,798.
The [startup receipt](UTILITY_DISKMASTER_STARTUP_2026-10-31.json) records media,
ROM, native controls, module provenance, successful opens and the next fault.
DiskMaster remains current; MuchMore has not started.

Main producer `4582592` implements RefreshWindowFrame using native frame and
gadget rendering. The authored corruption control passes on native 3.1 and
the corrected provider; the older provider fails before completion. DiskMaster
now runs through 10,000 fields and displays three panes, but no directory
entries or command labels. It remains unverified. The latest saved guest loop
reads Window.GZZHeight (+114) as zero before subtracting 12 and dividing by the
font height. Inner-dimension initialization is the next investigation; no later
utility has started.

Producer main `a0d6888` publishes GZZWidth/GZZHeight at open and successful
resize for ordinary windows. The native reference and corrected provider pass
all three framed/borderless/sizing controls; the old provider fails all three.
DiskMaster now displays the SYS file listing, full commands and RAM contents.
Frames 8000 and 10000 are identical. The PPShow fixture regression retains its
qualified hash. The [main-screen receipt](UTILITY_DISKMASTER_MAIN_2026-10-31.json)
records inputs and scope; file operations and final current-toolchain checks
remain unverified. MuchMore 4.6 is next.

### MuchMore 4.6 default scrolling screen

The complete pinned archive and deterministic text fixture are installed and
read back. Native 3.1 displays the header and numbered text lines. CopperStart
shows no reader. A target-PC capture after OpenScreenTagList at field 2737
confirms a null result for a 640x512 two-plane hires screen at (0,0). The
bundled source doubles its height for draggable display modes and default soft
scrolling. The native provider currently rejects heights above 283 and requires
the complete bitmap height to fit its physical display bounds. This failure is
preserved in the [startup receipt](UTILITY_MUCHMORE_STARTUP_2026-10-31.json).
Tall-screen/viewport behavior is next; no alternate scroll-mode workaround or
MuchMore success is claimed. MemCheck, LhA and Deluxe Paint remain pending.

Producer main `188ec4a` admits the virtual bitmap and compiles its visible slice,
retaining bitmap ownership and public dimensions. A native-reference control
verifies 512-row allocation,256-row visible height and vertical raster-offset
rebuild. CopperStart passes it; the older provider returns a null screen.
PPShow retains its qualified image hash. The initial below-text-clip sample
difference is recorded explicitly. MuchMore next traps at ScrollVPort (-588),
field3246; the reader remains unverified and MemCheck has not begun.

Producer main `a252d6c` adds vertical ScrollVPort support to the compact native
Copper-list owner. Both hardware and intermediate lists change; a later rebuild
preserves the scrolled pixels. Two native reference and two candidate controls
pass, with zero skips. The prior provider passes the rebuild path and fails the
direct vector. Current source solution builds with zero errors and seven warnings.
The MuchMore replay now displays its text and responds to Space starting/stopping
scrolling. Font glyphs/metrics and elapsed scroll distance differ from native;
those limits remain explicit in the [receipt](UTILITY_MUCHMORE_MAIN_2026-10-31.json).
PPShow remains byte-identical. Main `01b0802` corrects one recorded control field.
MemCheck is next; final current-source/current-toolchain checks remain required.

### MemCheck1.2 runtime prerequisite and memory-test lanes

The generic version error on bare CopperStart is caused by the linked runtime
opening intuition.library37 while the boot provider is33. Exec40.0 and DOS51.71
already meet the version check. Loading the qualified guest GUI provider allows
the default invocation to enter its segment test and advance patterns. The
8000-field default capture is a prefix, not a completed memory certification.

A fresh ADDRESSONLY guest on each ROM reports ADDRESS TEST FINISHED and
0 ERRORS APPEARED, then returns to its CLI prompt. The original appended
C:Echo measurement command independently fails on CopperStart: the Echo-only
control reproduces failure without MemCheck. The original failed images remain
preserved, and the separate command failure is explicit in the
[receipt](UTILITY_MEMCHECK_2026-10-31.json). No OS version was patched and no
emulator change was needed for these MemCheck results. LhA2.15 is next.

### LhA2.15 compressed-input qualification

The original SFX payload at offset6112 is pinned and installed unchanged as
an archive-test fixture. The complete distribution is retained and only
lha_68k is invoked. Native3.1 tests all5members successfully and returns to CLI.
CopperStart needs the same private GadTools module qualified for DiskMaster.
With it loaded, LhA says No files tested; its subsequent Operation successful
is not a successful archive test.

Public-vector tracing initially showed Shell metadata and data. Independent
on-disk inspection then disproved the proposed DOS-selection diagnosis: the
preparation script reused its archive-payload variable for the replacement
Shell and installed those bytes as the archive. Native DOS read the prepared
file correctly. The old inputs are invalid CRC-test inputs and remain preserved.

The script now keeps archive and Shell bytes separate and independently checks
the installed fixture against the pinned 167691-byte payload. The corrected
input exposed a DOS packet-path stack overflow. It first prevented Wait from
switching tasks, then corrupted the saved Shell stack pointer. CopperStart
commit `f1ea3b2` uses scalar packet/port fields and releases private-record
copies before nested Exec calls. Validation, allocation-free release storage,
cancellation and Forbid/Permit boundaries remain intact.

The retained 512 KiB A500 ROM now tests all five members successfully and
returns to the CLI. A capture over fields 2000–6000 observes 16,020,080 selected
task instructions with 56 bytes of minimum command-stack headroom. The packet
method's local frame falls from 472 to 160 bytes. All 21 focused native
release/signal/Close tests pass, including 15 internal Close outcomes. The
current-toolchain Write suite still has the same eight failing cases as its
unchanged-source control. PPShow retains its exact qualified image hash.
The separate Echo failure persists; it is not presented as fixed.

The final fresh-source sweep remains pending: current main compiles a raw ROM
but graphics composition overlaps by 3176 bytes. Fresh-source Shell and GUI
payload compilation checks both pass. DeluxePaint is next after this utility
fix and evidence are pushed. The [receipt](UTILITY_LHA_STARTUP_2026-10-31.json)
retains the invalid-input correction and the intermediate failed candidates.


Deluxe Paint investigation, 2026-10-31: the complete 29-file demo floppy reaches
the editor and draws a persistent stroke under native Kickstart 3.1. CopperStart
initially omitted DF0 while booting SYS: from HDF. Producer commit `8997abe`
mounts the prepared DF0 node during DOS initialization. A native public-DOS
regression reads distinct bytes from a nonbootable floppy, and the no-floppy
LhA replay retains its exact successful output. Both pass on the composed
512 KiB candidate. CopperStart's existing disk-loaded FFP module passes all
51 numerical/CCR cases and resolves DPaint's next missing-library error.
The subsequent 30000-field run reaches a graphical requester for missing
`workbench.library`; the CopperStart editor remains unverified. See the
[DPaint receipt](UTILITY_DPAINT_STARTUP_2026-10-31.json) for inputs and scope.

CopperStart commit `65966d7` publishes its existing native Workbench API with
Classic GUI prerequisites. The fresh native upgrade regression passes, including
public Workbench opening and its previous window/display checks. DPaint now
requests `PROGDIR:libs/dpblit.library` unsuccessfully. A bounded trace stops on
that failed public OpenLibrary at field 25748; the foreground process has no
program-directory lock. The Shell program-directory regression is in progress.

The program-directory file regression fails before CopperOS `9159cf0` and passes
after it. Foreground commands now borrow their executable's parent directory
and restore the previous process home after returning. CopperStart `44ec7bb`
contains this native regression. DPaint still failed because disk-library opens
run on a worker that copied only the current directory. CopperStart `8e526bc`
also carries and duplicates the caller's home lock for that worker, then restores
its context. The new library-open regression fails before this change and both
program-directory regressions pass afterward (2 executed, no skips). DPaint
replay `copperstart-library-home-v60` is pending; no editor pass is inferred.

The library-context fixes let DPaint open its bundled dpblit library. The next
first-fault capture stops at graphics SetWriteMask (-984). CopperStart `0abcd45`
adds the missing Classic planar vector. Its native upgrade regression passes,
including a high-bit mask argument, byte result and drawing after restoring
all planes; the producer solution builds with no warnings or errors. The next
40000-field run avoids that fault but returns to the Shell before the editor.
The hidden console bitmap confirms the prompt, rather than an application wait.
A public-vector capture confirms the custom dpaint font opens successfully;
dialog setup is the current investigation. Utility12 remains IN PROGRESS.

The dialog failure is a missing native `frameiclass`: GadTools creates a string
gadget frame through NewObjectA and receives zero. CopperStart `7accfbc` adds
its geometry, attributes, planar drawing and lifecycle. Public guest probes
match native Kickstart 3.1 in 48 pixel-exact drawing cases and 16 odd-dimension
geometry cases. The two new instruction cases pass. Two older getter failures
also occur with the unchanged provider component; the complete instruction
suite is therefore not reported as passing. The producer solution builds with
zero warnings or errors.

Original-floppy replay `copperstart-frameclass-v89` now reaches a populated
Screen Format chooser (PAL STANDARD+, 320x256). Clicking Use in
`copperstart-use-v96` causes corrupted execution before the editor. The next
bounded trace follows that click. Deluxe Paint remains IN PROGRESS.

The first post-Use trap is a patterned RectFill at field36064. CopperStart
`cf9f959` recognizes the retained ROM's direct layer veneer and installs the
existing native pattern renderer. Its recognition regression fails before the
fix; all three pattern instruction tests pass afterward. The freshly compiled
GUI and complete producer solution also pass their checks. The original-floppy
replay reaches the editor and tool palette, then falls back to16 colors with a
memory requester. Dismissing it works; a persistent drawing stroke is not yet
verified. The next identified issue is Intuition's unconditional four-plane
screen limit, despite its display database advertising32-color LORES modes.
The corrected resolution-dependent admission passes13 native instruction cases.

CopperStart `303b456` admits normal OCS low-resolution depth5 while retaining
the depth4 high-resolution limit. The screen instruction suite passes all13
cases, the fresh native GUI payload compiles, and the full producer solution
builds with zero warnings or errors. Original-floppy replay
`copperstart-depth-draw-v109` completes45000 fields at BPLCON0=$5200 and shows
the full32-color editor without the memory requester. This establishes the
requested main-screen minimum. CopperStart's scripted stroke still differs
from the persistent line in native `kick31-draw-v15`; drawing is not certified.
The final fresh-source sweep of the collection remains pending.
