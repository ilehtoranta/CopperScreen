# CopperStart: BCPL compatibility and Railroad route replay

2026-10-19. The rebuilt standalone ROM runs Railroad Tycoon through construction,
train purchase, a two-stop schedule, movement and the first London Crossing
arrival. This repeats the [earlier route](COPPERSTART_RAILROAD_ROUTE_2026-10-19.md)
with the new command ABI, startup CLI and keyboard changes. The
[companion JSON](COPPERSTART_BCPL_RAILROAD_2026-10-19.json) records the exact ROM,
build, tests, media, input, executable pins, captures and mode comparison.

## Guest compatibility fixes

CopperStart source is [403e684](https://github.com/ilehtoranta/CopperStart/commit/403e684d114a06c70301673bcbac876cc551a4bf).
The original Hired Guns disk's BCPL Echo exposed a missing command-entry ABI.
RunCommand now supplies the BCPL frame, global vector and call/return helpers
while retaining C arguments and the public stack-size word. The guest runtime
implements binding, arithmetic, allocation, byte access, CLI/segment queries,
basic character/BSTR I/O and legacy argument parsing. Invocation storage and
previous process fields are released/restored after command return.

An authored Kickstart 1.3 probe establishes that legacy ReadArgs returns the
BPTR just after used result storage. Its native result supersedes the initial
incorrect word-count assumption. Caller storage uses its UPB; the BSTR uses its
byte length encoding without an extra argument-legitimacy limit. Unsupported
global entries remain absent. This is partial legacy runtime support.

A startup command failure now leaves an interactive boot CLI while preserving
its failure receipt. The console also uses the actual US bottom-row and keypad
raw-key positions, including Shift behavior. Both changes have physical-input
controls against Kickstart 1.3. No host OS shim, command-name recognition,
executable patch or successful placeholder supplies these behaviors.

## Rebuilt ROM and tests

The 524288-byte ROM SHA-256 is
`7f0c04308f3429fa396e778a4c1913d2148e0077f5bc02817d9f40569e6ccddf`.
The main compiled body is 467076 bytes, with the native BCPL companion at
`$FF3000`, native Intuition at `$FF4000` and native graphics at `$FF8000`.
The production emitter produces the same whole ROM byte for byte.

The Release solution build succeeds with zero warnings and errors using the
recorded dependency root and existing project-reference outputs. The exact
command and log hash are retained in the JSON; no package is published.

Fresh validation is separate by scope:

- **102 exact-ROM cases pass**, with no failures or skips. This includes BCPL
  OFS/FFS, returning to the CLI after a failed startup command, and physical
  bottom-row/keypad input, in addition to the retained native boot cases.
- **9 console semantic cases pass**, with no failures or skips.
- The authored **OFS BCPL, startup-failure CLI and keyboard controls** each pass
  against the explicitly selected Kickstart 1.3 ROM.
- The full main-ROM compilation/boot producer case passes separately.

The 14 optional standalone polled-reader cases are excluded from this exact-ROM
selection, not counted as passes. A native Kickstart 1.3 FFS control is unavailable
without a filesystem handler on that authored disk. Earlier invalid probe
revisions, stale-binary failures and deliberately cancelled compiler runs remain
local diagnostics and are not relabeled as final qualification.

## Railroad Tycoon replay

Both modes cold-start PAL OCS 68000 with 512 KiB Chip and 512 KiB slow RAM,
one read-only floppy drive and RTC disabled. They use the original two supplied
Railroad Tycoon v855.01 disks and the same recorded physical inputs/media swaps
as the previous route. The frozen Lightweight engine and pinned Copper68k are
unchanged. The guest executes all OS and game code.

The replay builds London Junction and London Crossing, connects the depots,
purchases Train #1 with a Grasshopper and Mail Car, and lists the two scheduled
stops at field 311000. Selecting Slow at 314100 starts the train; subsequent
captures show it moving. At 321000 the complete June 14, 1830 newspaper announces
**First Train Arrives in London Crossing. Citizens Celebrate!**

Both runs finish at field 321000 with exit code zero. All **1920 capture files
per mode** match: images, captured CPU/hardware state, Chip RAM, slow RAM and PCM.
The JSON records their deterministic capture-set hash and reviewed checkpoints.
No captured unsupported hardware/video path is reported. No further Railroad
compatibility correction was needed after these guest changes.

The comparison is between modes using this ROM. Construction and route-report
images match the older ROM's reviewed fingerprints; some later map and newspaper
images differ. The current schedule, moving train and complete arrival headline
were reviewed directly. Older ROM capture identities are retained separately.

The Mail Car is empty. Freight revenue, completion of the return leg, a full
campaign, saving and audio fidelity remain unqualified. No new native-Kickstart
route replay or throughput acceptance measurement is claimed.

## Next title

The [Hired Guns startup follow-up](COPPERSTART_HIRED_GUNS_BCPL_2026-10-19.md)
records progress beyond the original Echo crash and the next missing service.
It remains a compatibility failure before CopperStart game entry. The original
startup failure record remains historical evidence for its older ROM.

ROMs, game media, local binaries and captures are not committed.
