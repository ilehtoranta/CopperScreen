# Optional native replay

The normal focused suite skips two tests unless all three environment variables
are set. These are correctness/allocation checks, not formal throughput samples.

Place your own copy of Lemmings disk 2 ZIP at `media/lemmings-disk2.zip`. The media
directory is ignored by Git. Set paths for a native 256 KiB Kickstart 1.3 ROM and
Lemmings disk 1, then run:

```powershell
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_ROM = 'path/to/Kickstart_13.rom'
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_ADF = 'path/to/Lemmings-disk1.zip'
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_SCRIPT = (Resolve-Path 'CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json').Path
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release
```

The workload uses the previously accepted SR-cracked Lemmings disk set. Different
releases are not interchangeable with its frozen input and fingerprints. Each
native test runs 14,520 fields; one also presses and releases Return during
gameplay. Both require active audio, no reported unsupported feature, and the
accepted pixel/audio fingerprint `C65F87325946E5DA`. The ordinary replay also
requires CPU fingerprint `6AE7090DA8AFB7E3`, at cycle `2063321634`.

This portable host-test script adapts the original frozen workload's local
disk-2 path. It contains input actions, not copyrighted game content. Runner
exploratory scripts may still contain original-machine disk-swap paths; the
historical performance harnesses also require a different exact script hash.
Use the [performance guide](engine/PERFORMANCE.md) for that separate tooling gap.
Interactive sound, controls and residual interlace behavior remain separate
live checks; replay success is not a fresh hardware-correctness claim.

## Four-drive runner checks

Use `--drives 4`, `--adf` for DF0 and `--adf1`, `--adf2`, `--adf3` for the external
drives. The default remains one connected drive; mounting an image in a disconnected
drive is rejected. All images must contain standard 880 KiB ADF data; ZIP is supported.

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- `
  --rom 'path/to/Kickstart_13.rom' --drives 4 `
  --adf 'path/to/Workbench13.adf' --adf1 'path/to/volume1.adf' `
  --adf2 'path/to/volume2.adf' --adf3 'path/to/volume3.adf' `
  --frames 4000 --boot-probe artifacts/four-drive-check
```

Boot-probe JSON includes all connected drives' mounted/control/spindle state as
well as the existing DF0 fields. This is diagnostic correctness evidence, not a
throughput measurement. Verify native detection and reads from each distinct
volume, then insertion/ejection and reset; simply mounting four images is insufficient.

Input-script media entries accept zero-based `drive` (omitted means DF0):

```json
[
  { "frame": 1000, "drive": 2, "ejectAdf": true },
  { "frame": 1060, "drive": 2, "adfPath": "replacement.adf" }
]
```

Media paths resolve relative to the script. Media-changing entries require boot
probe mode or must precede the measured interval, as with single-drive scripts.
Connecting drives is configuration at startup; scripts change media, not hardware.

## Four-drive verification — 2026-09-17

Release engine SHA-256:
`9186289E9887460C5C6CE8BAB858B940DF3DE41CFDA4DB3738F3373A63D0D7C5`.
The production solution built with zero warnings/errors. The isolated diagnostic
suite passed 401 tests; the ordinary host suite passed 88 tests, with its two
optional native tests run separately and both passing (gameplay and native
keyboard press/release, 14,520 fields each). An earlier attempt encountered a
transient Windows assembly-load policy error; the final build's tests completed
without changing host security settings. This is correctness evidence, not a
new throughput protocol or performance acceptance.

The [four-drive workload](../CopperMod.Amiga.Lightweight.Runner/Workloads/workbench-four-drive-readonly.json)
ran for 7,200 fields with native Kickstart 1.3 and these user-supplied inputs:

| Slot/action | Input | SHA-256 of the supplied file |
| --- | --- | --- |
| ROM | `Kickstart_13.rom` | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| DF0 | Workbench 1.3 rev 34.20 `[m]` ZIP | `5FA3B92E8BF18E5E5DB8CB5E9C58D407A67AF2C892B2E31E2913FE7AF0BAEC1B` |
| DF1 | `SysInfo.adf` | `77E64513B230B2DCEB8012FC747AC6B0FBA2C0297B662DFAC30EE1E11599830C` |
| DF2 initially | `timing-test.adf` | `0201A27B2D181AF134B2798EF161E96C3C10D629CC58831697B09B880C4312F4` |
| DF3 initially | Hired Guns v1.08.39.25 disk 2 ZIP | `F33E3E6987EE3457821F89670502708B4330574215B1894FB583D487192C6CAC` |
| DF2 replacement | Hired Guns v1.08.39.25 disk 3 ZIP | `3BCF2FF4434B2B02FC9DF6C988E8AFA34396EFC690528E9EED7D3BC6E22E1BC7` |
| DF3 replacement | Hired Guns v1.08.39.25 disk 4 ZIP | `6792DEA14C8ECF63EA90F6AF51832AC755E529E543A707344BEC4C1CCEA821A3` |

Place the two replacement ZIPs at `media/four-drive/hiredguns-disk3.zip` and
`media/four-drive/hiredguns-disk4.zip` (ignored by Git). Supply the initial images
through the runner flags above, with `--frames 7200` and
`--input-script CopperMod.Amiga.Lightweight.Runner/Workloads/workbench-four-drive-readonly.json`.
Use the matching inputs for the scripted mouse coordinates; other disks/desktop
layouts require their own workload.

Observed native behavior:

- Workbench boots from DF0; the desktop identifies SysInfo in DF1, the deliberately
  non-DOS timing disk as `DF2:NDOS`, and Hired Guns in DF3. All four drives show
  native head movement and completed disk-change acknowledgement.
- The script opens the SysInfo directory in DF1; the 4,080-field capture shows
  its file icon. This reads directory/icon content, not just drive identification.
- DF2 is ejected at field 4,200 and receives Hired Guns disk 3 at 4,260. DF3 is
  ejected at 5,200 and receives disk 4 at 5,260. Both new volumes appear on the
  desktop. DF1 is ejected independently at 6,200; its mounted state becomes false,
  with disk-change asserted and the other three media retained. Workbench retains
  the open SysInfo directory/volume reference after ejection.
- Final cycle `1023134400`, CPU fingerprint `E240A69E1E34B849`, output fingerprint
  `781F19E1E194C429`, no engine/video unsupported report. Local diagnostic captures
  are under `artifacts/df0-df3/workbench-final/`; ROMs, media and captures are not
  committed. Fingerprints record this workload/build, not an independent hardware oracle.

The existing single-drive Lemmings runner replay also retained cycle `2063321634`,
CPU `6AE7090DA8AFB7E3` and picture/audio `C65F87325946E5DA`, with zero allocations
in its 3,600-field measured interval after 10,920 warmup fields. Those are unchanged
correctness fingerprints; the ordinary run does not satisfy the formal host
throughput measurement protocol.

Native multi-drive game-loader coverage, reset during native external-drive I/O,
and physical simultaneous-read/switching phase accuracy remain unverified. The
synthetic tests separately cover reset and shared receiver/DMA behavior. Disk
writing was unsupported in this four-drive read-only verification build; the
new write evidence is recorded below. Nonstandard/preserved-track media remains unsupported.

## Dual-playfield verification, 2026-09-17

The retained dual-playfield engine SHA256 is
`5F6E27939729365C6AE470D4605E7FB3EB9C3981B093FB59A67D48D839595C1B`.
The production Release build succeeds and all **446 engine diagnostic tests pass**,
including 45 dual-playfield fixture cases. The full suite also passes with hardware
intrinsics disabled, exercising the scalar shifter fallback. The feature adds lores/hires separation,
transparency, independent scrolling, sprite masking, interlace and mid-line controls
within the existing pipeline. See the [display contract](engine/ARCHITECTURE.md#dual-playfield-output).

A native Kickstart 1.3 / Shadow of the Beast disk-1 PNA replay completes 3,600 fields:

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release --no-build -- `
  --rom 'C:\Data\ROM\Kickstart_13.rom' `
  --adf 'C:\Data\TestImages\Shadow of the Beast (1989)(Psygnosis)(Disk 1 of 2)[cr PNA].zip' `
  --frames 3600
```

Disk ZIP SHA256 is `D3361A4E71A22614DE4B183D2F345EE15853E258F02FDCA1114BE419151FA49A`.
Final cycle is `511567222`, CPU `CE9B15EB668802E2`, hardware `C71E660C04138C5C`,
picture/audio `67BC69BA6B24C5DB`, 284,204 pixels and 1,924 audio samples, with no
unsupported-mode report. The earlier diagnostic capture has the same fingerprints
and visibly renders the crackintro. Its snapshots show BPLCON0=$4600, independent
modulos 20/0 and nonzero audio. This establishes native dual-playfield **intro**
coverage, not game completion or independent hardware timing proof.

Local capture/log paths are `artifacts/dual-playfield/beast/`, `beast.log` and
`beast-final.log`; the optimized engine repeats these fingerprints in
`beast-optimized.log`. Media and generated artifacts are not committed. Cold-boot
allocations and probe FPS are diagnostic only; they are not steady-state acceptance
measurements. The existing Lemmings native fingerprints also match across all 12
controlled reference/candidate replays, with zero allocations in the measured
gameplay window. Performance is recorded separately in the
[measurement guide](engine/PERFORMANCE.md#dual-playfield-optimized-candidate-2026-09-17).
Lores and hires meet the confirmed 95% upper-bound criterion. Native Lemmings
improves by 2.100% on average but its +1.431% upper bound remains statistically
inconclusive against the 1% ceiling. On 2026-09-17 the owner explicitly accepted
that native exception for this build and workload. The raw result and future 1%
criterion remain unchanged; see the scoped acceptance in the measurement guide.

## OCS completion probes, 2026-09-18

These checks exercised intermediate builds, **not a final accepted OCS candidate**.
The assembly hash of the native format/write build was not captured when it ran;
later DLL hashes must not be assigned to it. The owner's retry subsequently
resolved the execution block and repeated the checks on identified builds below.
See [the active work record](engine/OCS_COMPLETION.md).

Collision probes from pinned
[vAmigaTS commit 0489f55](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Denise/Sprites/collision)
ran for 900 fields each. Copper-list readouts were `sprcoll1=$8000`,
`sprcoll8=$8401`, `sprcoll8d=$840B`, `sprcollbrd1=$8020` and
`sprcollbrd2=$8000`, with no unsupported-mode reports. The sprcoll8 and border
values match published hardware expectations. Original failing border captures
remain alongside the corrected captures. Emulator reference images are not
independent hardware proof.

Native UART `tbeirq1`, `tsre1` and `txirq0` also ran for 900 fields each without
unsupported-mode reports. Broad output patterns appeared, but the TBE comparison
contains stripe/phase differences against the available A500 ECS photograph.
This establishes execution coverage, not verified OCS serial/IRQ phase accuracy.

For disk writing, a disposable Workbench 1.3 boot copy started a guest script that
formatted/verified a blank writable DF1 as `OCSWRITE`, wrote `proof.txt`, copied it
to RAM, and printed `PAL OCS floppy write verified` and `WRITE TEST FINISHED`.
The source Workbench ZIP and original blank remained unchanged. All local files
below are under ignored `artifacts/ocs-completion-2026-09-18/`; media is not committed.

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --adf artifacts/ocs-completion-2026-09-18/workbench-write-test.adf `
  --drives 2 --adf1 artifacts/ocs-completion-2026-09-18/blank-write-test.adf `
  --writable-drive 1 --frames 14000 `
  --boot-probe artifacts/ocs-completion-2026-09-18/native-format `
  --export-adf 1 artifacts/ocs-completion-2026-09-18/native-formatted.adf
```

Guest verification reached cylinder 79 and completed. Strict export recovered all
11 valid sectors on each changed track. A fresh machine mounted the exported ADF
write-protected and a separate boot script printed its proof file followed by
`REOPEN TEST FINISHED` after 3,500 fields. Both captures were visually inspected;
both runs reported no unsupported modes. This covers native guest format/write/read,
sector export and independent reopen, not analog splice timing or desktop dialogs.

| Run | Final cycle | CPU | Hardware | Picture/audio |
| --- | --- | --- | --- | --- |
| Format/write/export | 1989428000 | 81D3D33E1508A04B | D26342811EFE536A | 73AAEB98672A0FB1 |
| Reopen/read | 497357000 | 26BF001A5044702A | FFB7A4170792D93A | A07E2B7BC331854E |

Evidence: `prepare-native-write.py`, `native-format.log`, `native-format/`,
`native-reopen.log` and `native-reopen/`. The native images were prepared as
disposable standard OFS media with corrected block checksums; the guest executed
the actual format and file I/O. Probe FPS and cold-boot allocations are not
performance acceptance measurements.

### Identified retry builds

The retry engine `1FBDE6E8FF93737082C6D721DD26853E481D5A1AD5BA6A6E0568410DAFD7C42E`
repeated the format/write/export and fresh-machine reopen runs with every fingerprint
in the table above unchanged. Both final images were inspected and show the proof
text and completion markers. Evidence uses `native-format-retry-1` and
`native-reopen-retry-1`; `native-retry-1-build-hashes.json` identifies the binaries.
Both optional native host-adapter tests also ran explicitly and passed, including
the keyboard press/release replay (`host-native-retry-1.log`).

Repeating all collision probes exposed missing bit 0 in sprcoll8/8d after the earlier
BPU-disable guard. Retaining comparison through the remainder of the current DIW
repairs that regression without reintroducing the border failures. The latest
profile-optimized engine `754C8A3DE511F65DD82E52F4485F89E888E0A7E6921736A019CC4B44BA5EB7C0`
returns `$8401`, `$840B`, `$8020`, `$8000` in six saved snapshots per probe. These
match the pinned OCS photographs, including
[sprcoll8](https://github.com/dirkwhoffmann/vAmigaTS/blob/0489f55d22ef7560d924a304e30998aea6864264/Denise/Sprites/collision/sprcoll8/sprcoll8_A500_OCS.JPG)
and [sprcoll8d](https://github.com/dirkwhoffmann/vAmigaTS/blob/0489f55d22ef7560d924a304e30998aea6864264/Denise/Sprites/collision/sprcoll8d/sprcoll8d_A500_OCS5.JPG).
The exact pixel-phase rule remains a bounded model, not a separately measured
silicon timing result. Raw failed and repaired captures remain available.

The same optimized engine subsequently repeated the full native format/write/export
and fresh-machine reopen checks. Every fingerprint in the table above matches;
both final BMPs were inspected and show the proof text and their completion
markers. Evidence uses `native-format-final-optimization`,
`native-reopen-final-optimization`, `native-final-optimization-build-hashes.json`
and `verify-final-optimization.ps1`. These are the same engine bytes used in the
completed formal comparison: its six native candidate samples also retain the
established Lemmings CPU/hardware/picture/audio fingerprints. The performance
bounds exceed the default limit in two workloads; the owner accepted both specific
exceptions for this build. See
[PERFORMANCE.md](engine/PERFORMANCE.md#ocs-completion-work-2026-09-18--accepted-with-scoped-exceptions).

The full host suite also passed on this engine: **91 passed, zero failed/skipped**,
including both supplied-input native cases (`host-final-optimization.log`).
The three UART probes repeated for 900 fields each and retain their earlier complete
fingerprints after removing diagnostic FPS/allocation totals; logs/captures use
the suffix `-final-optimization`. The existing UART physical-phase uncertainty
and outstanding manual desktop dialog checks are unchanged.
