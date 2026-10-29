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
| 2 | X-Copy 5.21 English (1991-11-17), Cyclone 9.11 disk | PENDING | Not yet verified in this sequence |
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

X-Copy is next. The remaining eleven utilities have not been verified in this
sequence, and the full collection goal remains open.
