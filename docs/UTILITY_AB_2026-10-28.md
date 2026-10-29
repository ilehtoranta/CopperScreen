# CopperStart / Kickstart 1.3 utility comparison — 2026-10-28

The A500 CopperStart ROM now rebuilds successfully at **524,288 bytes**, including
native graphics. The four initial utilities were rerun against native Kickstart
1.3 on the same bounded PAL A500 engine. **Test Kit's selected audio scenario
works under both ROMs. ProTracker, X-Copy and SysInfo show startup differences
under CopperStart.** These are exploratory compatibility results, not full title
qualifications or host performance measurements.

The [previous missing/oversize ROM blocker](UTILITY_VALIDATION_2026-10-27.md) is
resolved for this diagnostic comparison. The rebuilt ROM is a development input;
the application's existing firmware qualification/pin was not replaced.

## ROM and consumer identities

| Property | Result |
| --- | --- |
| Producer command | `build/Build-DiskBootRom.ps1 -FileSystemProfile Classic` |
| Composed ROM size | 524,288 bytes |
| ROM SHA-256 | `6d8468bb6bd8b0c5b221be865af619f1e4683f4af8085272a689e8e80bd4c181` |
| Whole-ROM end-around-carry checksum | `$FFFFFFFF` |
| Compiled root | 487,450 bytes: 476,444 code + 11,006 read-only data |
| Graphics | 26,123 bytes, resident at `$FF99E0`, 72 supported vectors |
| Space before graphics-placement limit | 10,686 bytes |
| Compiler size mode | `on+generated+frame`; MC68000, `--clr always`, fixed-point peephole, FPU disabled |
| Producer compatibility report | Compatible; zero managed allocation sites, runtime helpers or external native targets. The existing audited constant-string-table rule is retained. |
| Kickstart reference | 262,144 bytes; SHA-256 `ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53` |

The ROM hash reproduces the producer's documented final BCPL-sharing image in
`CopperStart/Docs/NativeRomSize20261028.md`. Its managed compiler/SystemRom and
graphics builds passed without errors. The consumer build
`dotnet build CopperScreen.slnx -c Release` passed with zero warnings/errors.
The runner, engine, Copper68k and CopperDisk DLLs are byte-identical to the
2026-10-27 utility run. Producer source/assembly hashes, map, compatibility
report, graphics metadata and build log are retained in local evidence.

Both sides use PAL, MC68000 interpreter with conservative batching, OCS
8371/8362, 512 KiB Chip + 512 KiB slow RAM, no Fast RAM/RTC/host directories, and
one protected DD DF0. The original disks and input scripts are unchanged from
[the collection](UTILITY_TEST_CORPUS.md). Only the ROM differs within each pair.

## Reviewed outcomes

| Utility | Kickstart 1.3 | Rebuilt CopperStart | Scope/conclusion |
| --- | --- | --- | --- |
| ProTracker 2.3F | Editor appears; note entry and sequencer play/stop work. | No editor by field 7,200; keyboard input appears in the console. A separate 16,000-field run with **no input** still does not reach the editor and ends with a patterned console display. | Startup gap persists beyond the original input window. No sample playback or save/readback qualification on either side. |
| X-Copy 5.21 | Boot menu appears; F1 launches the DOSCOPY application. | White screen already present at field 3,000, **before F1**; no menu/application by field 7,200. | Boot-menu/startup gap, not evidence of a failed disk-copy operation. No read/check sweep, writes, format or Cyclone hardware tested. |
| Amiga Test Kit 1.21 | Menu, F5 Audio, individual channel muting and F7 restore work. | Same selected menu/audio-control behaviour. | Six reviewed framebuffers are byte-identical across ROMs. Audio follows stereo → left-only → silence → restored stereo on both; PCM is not claimed bit-identical. |
| SysInfo 4.4 | Initial library/hardware screen appears. The existing exploratory mouse/click script then ends black. | No SysInfo screen at fields 3,000 or 7,200; dark console/display remains. | CopperStart startup gap precedes the intended Speed action. Native successful Speed selection/benchmark/quit remains unestablished. |

All **eight 7,200-field runs (57,600 fields)** completed, with 73 snapshots each,
no engine unsupported-feature stops, and unchanged protected-media hashes.
Completion is an execution result, not a compatibility pass. All 365 captured
framebuffer/JSON/PCM/Chip-RAM/slow-RAM files per native utility reproduce the
previous native run byte-for-byte.

Test Kit's static labels disappear during the channel-toggle sequence under
**both** ROMs. The reviewed fields 3,000, 3,100, 3,800, 4,000, 4,200 and 7,200
have identical complete framebuffers across the pair, including that behaviour.
This does not isolate a CopperStart defect or establish real-Amiga correctness.

**2026-10-29 correction:** the missing-label interpretation above was wrong.
Rechecking the original raw captures confirms unchanged static-label pixels
through the audio toggles, as do the current-source runs. See the
[pixel evidence and current result](UTILITY_TESTKIT_2026-10-29.json). Original
captures, hashes and dated execution results remain unchanged.

| Test Kit field / requested state | Native left/right RMS | CopperStart left/right RMS |
| --- | ---: | ---: |
| 3,100 / all channels enabled | 9420.72 / 2807.12 | 9421.23 / 2805.67 |
| 3,800 / only channel 3/L enabled | 3708.11 / 0 | 3709.67 / 0 |
| 4,000 / all disabled | 0 / 0 | 0 / 0 |
| 4,200 / all restored | 2155.27 / 2357.44 | 2155.54 / 2357.74 |

RMS values use the one captured field's signed 16-bit samples. Sparse snapshots
are not a continuous recording. No independent physical audio/video oracle was
used, and no cross-ROM RAM/PCM golden was substituted.

## Startup follow-up evidence

The input-free ProTracker control retains the same ROM, disk, machine and
protected-drive settings. It completes 16,000 fields without an unsupported stop,
but the final PC is `$00063F1A` and A7 is `$FFFFE2FC`, outside writable RAM for
this configuration. This is a failing startup observation; it does not identify
the first incorrect instruction or justify a CPU/chipset fix.

The subsequent [targeted ProTracker investigation](PROTRACKER_STARTUP_2026-10-28.md)
isolates the startup failure: Classic CopperStart returns zero for
`OpenLibrary("intuition.library", 0)`, then ProTracker calls `OpenScreen` through
the null base at field 7,368. The process handoff and stacks are valid at that
point. Kickstart successfully opens both the library and screen. Source
inspection also identifies a follow-on requirement: the optional CopperStart
Intuition provider rejects ProTracker's 12-pixel-high behind-screen. The earlier
captures remain unchanged; this diagnosis explains their downstream corruption.

The subsequent [native Classic ROM fix](PROTRACKER_FIX_2026-10-28.md) supplies
screen, input and CIA-B services. Its separate 11,000-field pair reaches the
editor and matches note data, sequencer play/stop and pointer motion. This is
a new development ROM/scenario result; it does not replace this table's original
ROM identity or 7,200-field observations.

Read-only disk inspection records ProTracker's startup command as `PT2.3F`.
SysInfo's startup chain runs `C:SetPatch_1.38`, `C:Initialize`, then
`SYS:SysInfo/SysInfo`. These supplied programs are part of the workload's boot
dependencies. The current evidence does not establish which SysInfo startup
component first fails. X-Copy's final sampled PC stays at `$0000A88A` with a
white screen; the supplied menu/application entry needs a separate targeted
investigation. Guest OS, loader and API assumptions remain candidates; no
title-specific engine changes were made.

## Reproduction and evidence

Rebuild the producer into a fresh directory, then run the existing diagnostic
harness against the newly composed file and its matching manifest:

```powershell
../CopperStart/build/Build-DiskBootRom.ps1 `
  -FileSystemProfile Classic -ArtifactRoot C:/PrivateEvidence/new-rom

./scripts/test-utility-boot.ps1 `
  -NativeRomPath C:/Data/ROM/Kickstart_13.rom `
  -DevelopmentCopperStartRomPath C:/PrivateEvidence/new-rom/CopperStart-DiskBoot.rom `
  -OutputDirectory artifacts/utility-ab/new-pair
```

The script defaults to both ROMs. `-RomSelection Native` or `CopperStart` runs
one side for independent diagnostic scheduling. This comparison used separate
invocations while the ROM built; their receipts were combined only after the
protocol, configuration, DLL, disk and input-script hashes matched. No host
throughput conclusion is drawn from these runs.

The [machine-readable result](UTILITY_AB_2026-10-28.json) records the reviewed
outcomes. Raw evidence is Git-ignored under `artifacts/utility-ab/2026-10-28/`:

- `copperstart-rebuild/`, `copperstart-rebuild.log`, `source-provenance.json`
  and `rom-identity.json`: ROM, staged inputs, source hashes and build receipts.
- `native-actions-v2/` and `copperstart-actions-v2/`: separate complete runs,
  original invocation receipts, logs and captures.
- `paired-actions-v2/qualification.json`, `observations.json` and
  `verification.json`: combined identities and checked results.
- `paired-actions-v2/comparison-3000.png` and `comparison-7200.png`: full-frame
  side-by-side overview sheets; individual captures remain unchanged.
- `protracker-no-input-16000/` and its log: longer input-free startup control.
- `startup-inspection.json`: read-only original disk startup-script inspection.

ROMs, media and captures are not committed. The app firmware pin, production
engine/CPU source, published packages and retained regression/performance
fingerprints are unchanged. The other eight collected utilities remain NOT RUN.
