# Sequential utility verification — 2026-10-29

Work through the collected utilities individually. Verify the main screen
(or basic invocation for a command-line utility), investigate and fix failures,
and commit and push each fix before advancing to the next utility. Previous
[collection and A/B evidence](UTILITY_TEST_CORPUS.md) retains its original inputs
and scope.

A row remains pending until its current-source runtime evidence has been
inspected. Missing prerequisites do not count as a successful utility run.
After the fixes, repeat the main-screen checks against the final source before
marking the collection complete.

| Order | Utility | Current-source status | Evidence |
| --- | --- | --- | --- |
| 1 | ProTracker 2.3F (2026-05-09 build) | VERIFIED: main editor and selected inputs, both ROMs | [Receipt](UTILITY_PROTRACKER_2026-10-29.json) |
| 2 | X-Copy 5.21 English (1991-11-17), Cyclone 9.11 disk | VERIFIED: DOSCOPY main screen, both ROMs; disk operations unverified | [Latest receipt](UTILITY_XCOPY_DISK_RESOURCE_2026-10-29.json) |
| 3 | Amiga Test Kit 1.21 | VERIFIED: main menu and selected audio controls, both ROMs | [Receipt](UTILITY_TESTKIT_2026-10-29.json) |
| 4 | SysInfo 4.4 | PENDING | Not yet verified in this sequence |
| 5 | PPMore 1.8a | PENDING | Not yet verified in this sequence |
| 6 | Personal Paint 7.1c complimentary edition | PENDING | Not yet verified in this sequence |
| 7 | PPShow 4.0 | PENDING | Not yet verified in this sequence |
| 8 | DiskMaster 2 2.5.30a (V37 build) | PENDING | Not yet verified in this sequence |
| 9 | MuchMore 4.6 | PENDING | Not yet verified in this sequence |
| 10 | MemCheck 1.2 (37.13) | PENDING | Not yet verified in this sequence |
| 11 | Amiga LhA 2.15 | PENDING | Not yet verified in this sequence |
| 12 | DeluxePaint V demo 5.0b9 (1994-08-12) | PENDING | Not yet verified in this sequence |

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
