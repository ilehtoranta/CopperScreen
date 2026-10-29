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
| 2 | X-Copy 5.21 English (1991-11-17), Cyclone 9.11 disk | IN PROGRESS: boot menu fixed; application launch still fails | [Receipt](UTILITY_XCOPY_2026-10-29.json) |
| 3 | Amiga Test Kit 1.21 | PENDING | Not yet verified in this sequence |
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
