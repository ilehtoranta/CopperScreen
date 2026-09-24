# Optional native replay

The [September 23 corpus](engine/NATIVE_CORPUS_2026-09-23.md) adds five games on
the shipped locality CPU. Lotus Esprit Turbo Challenge reaches interactive racing;
Railroad Tycoon completes disk A → B → A loading, scenario selection and map/new
company initialization. Worms reaches its manual-code check; the owner's suggested
arbitrary-code attempt does not advance to gameplay. Alien Breed II stays black
and Tower Assault enters a Guru by captured field 600. Both failures have 135
byte-identical state/image/RAM checkpoints in scalar and batched CPU runs. Their
original failures are retained as compatibility evidence, not a performance result.
The [Tower Assault follow-up](engine/TOWER_ASSAULT_INVESTIGATION.md) identifies
false AGA detection from absent OCS DENISEID readback. Its candidate reaches
disk-2 loading and the opening level with audio. The
[Alien Breed II investigation](engine/ALIEN_BREED_II_INVESTIGATION.md) identifies
all three supplied disks as the AGA edition by exact catalog hashes. That replay
is outside the OCS profile; the OCS edition remains unavailable and unverified.
The [single-latch optimization](engine/DMA_LATCH_OPTIMIZATION_2026-09-23.md),
[direct DMA word-access follow-up](engine/DMA_WORD_ACCESS_2026-09-23.md) and
[fixed-size palette candidate](engine/VIDEO_PALETTE_2026-09-23.md) retain all 1005
Tower Assault state/image/RAM captures and complete workload fingerprints. The
word-access candidate also repeats those checks after the host update to .NET
10.0.12; the palette candidate is built and verified on that runtime.
Performance acceptance is recorded separately. The palette candidate fails the
confidence-bound gate and its edit is discarded; its passing native evidence
remains attached to the measured binary. The preceding word-access candidate
also has no performance exception. The subsequent
[register-bank trial](engine/REGISTER_BANK_2026-09-24.md) retains all four
complete workload identities and all 1005 Tower Assault captures. Its valid
performance comparison also misses the 1% gate; inline register storage is
discarded, with native evidence preserved against that frozen candidate.
The subsequent [fixed-bounds candidate](engine/FIXED_BOUNDS_2026-09-24.md)
preserves the original arrays and likewise passes all four identities and all
1005 Tower Assault capture comparisons. Its valid performance series passes
hires and native Lemmings but remains inconclusive for lores under the 1% gate;
native correctness does not constitute performance acceptance.
The [scoped-local follow-up](engine/SCOPED_LOCALS_2026-09-24.md) also passes all
874 tests, all four complete workload identities with zero measured allocation,
and all 1005 Tower Assault capture comparisons. Its complete v8 comparison passes
the 1% upper-bound limit for every retained workload without an exception. The
bounded native and physical-hardware coverage limits are unchanged.

The [live WORDSYNC follow-up](engine/DISK_LIVE_WORDSYNC.md) removes the three
Team17 boot stops below on its identified candidate. Superfrog reaches attract
gameplay, F17 Challenge loads disk two and reaches a race, and Overdrive loads
disk two and responds at race selection. The record separates later native checks
and the required performance gate from the original batch-2 failures.

The [second September 21 corpus](engine/NATIVE_CORPUS_2026-09-21_BATCH2.md) adds
six games and extends two previously tested demos. It exposes three native IPF
boots blocked by live WORDSYNC changes, Alien Breed SE's black loader wait and
a late Desert Dream failure; all reproduce on the pre-blitter-fix engine. Arte
reaches later credits and closing artwork. Desert Strike reaches a disk-2 load
but stops at the drive-range guard; Xenon 2 reaches its game-disk request, with
that second disk unavailable locally. This is diagnostic coverage, not a new
engine change or performance gate.

The 2026-09-21 [game and demo corpus](engine/NATIVE_CORPUS_2026-09-21.md)
adds seven titles: Major Motion and Alien Breed reach interactive gameplay;
Arte and Desert Dream advance through bounded demo sequences. Inside the Machine
has reproducible effect corruption, Miami Chase has overlapping menu text, and
Lotus III remains at its disk-2 prompt. Alien Breed's IPF disk-2 swap and Miami
Chase's ADF disk-2 load are exercised. A Windows scripted-ZIP path defect is
recorded separately from emulation. These are the original corpus outcomes.
The [blitter follow-up](engine/BLITTER_FINAL_MODULO.md) corrects the first two
visual defects with final-row modulo handling and records the new native
identities and performance gate separately.

The 2026-09-20 [additional game corpus](engine/GAME_CORPUS_2026-09-20.md) records
bounded Lotus II and Apidya gameplay, persistent North & South menu corruption,
and a Super Cars II boot Guru. Both failures reproduce identically before the
latest disk-control fixes. Media hashes, input scripts and diagnostic identities
are retained; second disks and full-game completion remain unverified.

The [North & South follow-up](engine/NORTH_SOUTH_INVESTIGATION.md) corrects a
Copper restart defect after the user reported real-A500 success. A frame restart
held by disabled DMA now consumes the current list pointer when its bus output
completes. Untouched CP menus and borders render correctly; four independent
probe patterns agree with published hardware photographs. The initial inference
about the CP release is withdrawn. Gameplay and performance acceptance remain
separate from this visual correction.

The [Super Cars II follow-up](engine/SUPER_CARS_II_INVESTIGATION.md) identifies
missing CPU chip-RAM mirrors as the boot Guru's cause. The candidate now passes
the depacker and reaches the first race with the same RAM sizes and disk. The
hardware correction also changes Kickstart's memory-probe exit by 20 cycles;
the unchanged Lemmings script still reaches level one. Previous native identities
and the initial golden-test failures are preserved in the investigation.
The complete benchmark passes hires/native but exceeds the lores 1% bound;
the user accepted that scoped exception on 2026-09-21. Second-disk coverage
remains unverified.

The 2026-09-20 [disk-control follow-up](engine/DISK_CONTROL_EDGES.md) repeats native
Workbench format/write/export and fresh-machine reopen against accepted `a10b22c`.
Both builds produce matching CPU/hardware/output fingerprints and byte-identical
exported ADFs; final candidate captures show both completion markers. All three
native host checks also pass. This is functional retention, not physical Paula
phase certification; the record preserves an initial incompatible-runner failure.

The 2026-09-20 [nonstandard OCS investigation](engine/NONSTANDARD_OCS.md) records
ten bounded display-test ADF replays and comparison with published hardware
photographs. Two additional nonstandard blitter-width runs disagree with those
photographs and remain unsupported; their diagnostic bypass is not passing
coverage. ROM/media hashes, candidate identities and limits are recorded separately
from performance acceptance.

The normal focused suite skips three tests unless all three environment variables
are set. These are correctness/allocation checks, not formal throughput samples.

The 2026-09-20 [keyboard/CIA milestone](engine/KEYBOARD_CIA.md) adds a ROM-only
keyboard check through the desktop session: cold synchronization completes,
Caps Lock on/off is acknowledged, and Ctrl–Amiga–Amiga resets and boots again
without rewinding machine time. The two existing Lemmings runs retain their
accepted complete-output identities and the ordinary run retains its CPU identity.
The native keyboard test verifies guest acknowledgement and reboot, not physical
MCU scan or reset-pulse timing. No reference fingerprint was changed for this work.

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
drive is rejected. These original checks use standard 880 KiB ADF data; ZIP is supported.
Read-only IPF is now also accepted; its separate compatibility status is below.

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

## Storage extension 2026-09-18

This candidate follows `6b5cb1b`; its performance budget is independent of the
earlier OCS exceptions. See [STORAGE.md](engine/STORAGE.md) for interface, source
provenance, test scope and the unresolved IPF/CPU failures. Raw local evidence
is under ignored `artifacts/storage-2026-09-18/`.

The final frozen engine is
`5EB543BCF6AD41E718D72C9ADA2445C64D05341BF233AD59584C83318A076FAA`,
with CopperDisk `4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F`.
The native ROM remains SHA-256
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.

### Writable ADF retention

The final engine repeats native Workbench format/write/read/export (14,000 fields)
and a separate-machine reopen (3,500 fields), retaining every earlier fingerprint:

| Run | Cycle | CPU | Hardware | Complete picture/PCM output |
| --- | --- | --- | --- | --- |
| Format/write/export | 1989428000 | `81D3D33E1508A04B` | `D26342811EFE536A` | `73AAEB98672A0FB1` |
| Fresh reopen | 497357000 | `26BF001A5044702A` | `FFB7A4170792D93A` | `A07E2B7BC331854E` |

Logs/captures: `adf-format-final`, `adf-reopen-final`; exported disposable disk
`adf-formatted-final.adf`. Final images were inspected and show the proof text and
WRITE/REOPEN TEST FINISHED markers. Both supplied-input native host tests pass in
the 93-test host suite (`host-final2.log`).

### Native CopperHDF OFS

[prepare-lightweight-hdf-native.py](../scripts/prepare-lightweight-hdf-native.py)
creates disposable fixtures from a supplied 880 KiB OFS Workbench 1.3 ADF/ZIP in
a **new** output directory. The preparation changes a startup script and block
checksums; guest execution performs all required Format/Copy/file-write operations.
It creates a bootable partition HDF and a blank RDB HDF with partition offset 32
sectors. ROM/filesystem code is never included in the repository.

Use the Release runner with the native ROM and wide output:

1. Partition: `--hdf <fixtures>/partition.hdf --frames 1800`; omit all floppy media.
2. RDB setup: `--adf <fixtures>/format-rdb.adf --hdf <fixtures>/rdb.hdf --frames 17000`.
   The guest Quick Formats DH0, copies Workbench and writes its ready marker.
3. RDB cold boot: `--hdf <fixtures>/rdb.hdf --frames 2000`; omit all floppy media.
   The copied startup now writes and reads `hdf-proof.txt` from its own SYS volume.
4. Shut down, independently verify each image, then repeat each no-floppy boot in
   a fresh process and verify again. Add unique `--boot-probe` directories to retain
   register logs and BMPs. Probe FPS is diagnostic only.

The complete corrected sequence is retained in `final-native2/`: `rdb-format`,
`partition-write`, `rdb-write`, both `*-reopen` and both `*-final` repetitions.
Images show RDB FORMAT COPY FINISHED or HDF COLD BOOT WRITE FINISHED with the proof
text. Both no-floppy final runs report `adf=False`, `unsupported=none`. Earlier
`final-native/` and `hdf-rdb-format-probe3` attempts are superseded; the latter
used an unsupported shell append form and is not counted as a completed recipe.

[verify-lightweight-ofs-proof.py](../scripts/verify-lightweight-ofs-proof.py)
reads the files independently of CopperDisk and validates root/header/data block
checksums, file ownership/sequence, length and content. Use `--offset-sectors 32`
for the RDB fixture and zero for the partition fixture. Both final files contain
33 bytes, SHA-256
`D09F9C0F01892196C725C46E6D4B5CF0593682782742F2A9353053DED29A28E1`.

| Final image | SHA-256 after guest write, flush and fresh-process repetitions |
| --- | --- |
| Partition | `99C4423CD81F20C0F06F8ACD6C32404B2813CC63CBFF9BA710D361556C93292E` |
| RDB | `9674DEBD9279FBCE44D330D51DA3258785240719083F0A1291F013987B3B0C6E` |

The startup writes again on each boot, so whole-image hashes can change with DOS
timestamps while the independently checked file hash remains stable. OFS is the
native baseline. Additional filesystem-handler/native coverage is unavailable.
Read-only/range/error/QUICK-reply behavior has deterministic engine tests; this
does not certify every filesystem or malformed guest program.

### Native IPF: incomplete

The [six-image manifest](engine/storage-native-media-2026-09-18.json) records ZIP
and individual IPF SHA-256 values for the supplied two-disk sets. Full Contact
completes a bounded title/loading/disk-two/gameplay replay. The corrected joystick
script selects Start Game, shows the Tong Lo introduction at field 10,200, active
fighting at 12,000 and a lost round with depleted player health at 14,400. Final
field 18,000: cycle `2550504358`, CPU `542163D9AD25832B`, hardware
`BC7544DF26E5659B`, complete output `29ED54CB36E7E75C`, `unsupported=none`.
Captures/log: `ipf-fullcontact-gameplay/`, `fullcontact-gameplay.log`. Probe
allocation totals include capture work and are not steady-state measurements.
The runner's generic gameplay-unverified banner describes automatic classification;
the frame captures above were inspected separately. Earlier 12,500-field input
failed to select Start Game reliably and is retained as an unsuccessful attempt.

Beast reaches intro/music and its disk-two prompt. The earlier swap at field 6,500
was premature: it replaced disk one while its loader still needed disk-one data,
then passed a corrupt packed-data length to the depacker at PC `$68EA8`. The first
address error is at field 7,432, PC `$68EAA`; this does not establish a receiver
defect. The corrected replay retains the intro skip presses, shows the disk-two
prompt at field 10,440, ejects at 10,500, mounts at 10,550 and presses fire at 10,600.
The disk-two game title appears by 12,000; inspected frames show crouching at
17,220, an attack at 17,460 and an airborne jump at 18,120. Final field 19,000:
cycle `2699507626`, CPU `0AE8BFCF33AE1148`, hardware `28887E76E8A938C0`, output
`FF75125AE1D7A59B`, `unsupported=none`. Evidence: `ipf-beast-gameplay-final/` and
`beast-gameplay-final.log`. This verifies bounded native loading, disk swapping
and gameplay, not the entire game or every OCS display edge. The earlier
`beast-gameplay` attempt also lacked the intro skip presses and remains failed.

Operation Thunderbolt originally passed its PAL check after the CIA latch correction
but hit the independently reproduced CPU trace exception defect. The development
CPU pin and a subsequently exposed Paula transition now permit bounded gameplay;
see the trace-fix record below. Second-disk acceptance remains incomplete.

Bounded Full Contact (18,000 fields) and Beast (19,000 fields) input scripts are
under `CopperScreen.Lightweight.Tests/Workloads/ipf-*-storage.json`; copy the
user-owned disk-two ZIPs into the documented ignored `media/ipf-*-disk2.zip`
aliases, supply disk one with `--ipf`, and run with `--input-script`, `--wide-output`
and a fresh `--boot-probe` directory. Both record the successful bounded input
sequences. They are not performance golden replays. Tracked script SHA-256 values:
Full Contact `8FE594777642E64B5A4DB2F4665B16BF8DF99C14022F82B38B5FA37DD9E6414F`;
Beast `B98D660517CB1D9EC097D4A4684B4EA48CF9EB21D4B43349D770AC8A2DFF58A0`.
Thunderbolt's original trace failure occurred without input before gameplay. Earlier raw
captures use `ipf-fullcontact-*`, `ipf-beast-*`, `ipf-thunderbolt-*` and
`thunderbolt-*-trace.log`. The latter traces and the ROM-free CPU probe distinguish
the CPU failure from receiver correctness. Earlier premature/incorrect input
attempts remain recorded and are not silently relabeled as passing.

### Copper WAIT wake-up correction, 2026-09-18

The owner reported an early blue upper-left border. The saved Shadow of the Beast
US replay reproduced it: at scanlines 60, 80, 100 and 112 the sky started at x272
in the uncropped 908-pixel output, while DIWSTRT `$2C90` places the picture at x288.
The Copper list waits on `$3C41/$FFFE` before writing COLOR00 `$0678`.

The defect was WAIT completing its wake-up while bitplane DMA owned the required
memory slot. [Commodore HRM chapter 2](https://bastya.net/AmigaDevDocs/hard_2.html)
specifies the extra memory cycle for WAIT wake-up and higher bitplane DMA priority.
In the existing physical phase convention, six-plane DDF `$38` has BPL5 output at
`$3F`, so matching input `$3E` cannot wake the Copper. Wake-up moves to input `$40`;
the MOVE words then complete at `$45` and `$49`. Previously they completed at `$41`
and `$45`, making COLOR00 four CCKs (16 output pixels) early.

Only the wake-up transition now checks higher-priority bus availability. No
palette delay, display-window clipping, media behavior or title-specific rule was
added. The check runs only after the comparator succeeds. The new 0/4/5/6-plane
regression distinguishes contended wake-up from ordinary uncontended timing;
the six-plane case failed before the fix. This uses the manual plus the existing
DMA phase contract; no new physical-hardware capture is claimed. vAmiga's
[Copper event implementation](https://github.com/dirkwhoffmann/vAmiga/blob/master/Core/Components/Agnus/Copper/CopperEvents.cpp)
also gates wake-up on bus availability, as supporting implementation evidence.

Release engine SHA-256:
`AF2550B2F47AC851AD2E29B5ECED7F7B39BB2944E63DC9C0931BB2C7ED38DAAF`.
Production Release build: zero warnings/errors. Isolated engine diagnostics:
551 passed. Host tests: 93 passed with both native Lemmings replays enabled and
their existing expected fingerprints unchanged. The unchanged 19,000-field two-disk Beast input script completed with
cycle `2699507626`, CPU `0AE8BFCF33AE1148`, hardware `28887E76E8A938C0`, corrected
output `9B9FF47082FAC0A5`, `unsupported=none`. CPU/hardware fingerprints match the
earlier replay; the changed image fingerprint is expected for this correction.
The border is black through x287 on all four sampled rows and the sky starts at
x288. Frame 18,120 was visually checked; disk-two loading and scripted gameplay
remain present. This does not certify the entire game.

Local evidence is `artifacts/beast-border-2026-09-18/`: `test-before.log`,
`engine.log`, `build.log`, `native.log`, `native/frame-018120.bmp` and
`pixel-check.json`. Prior captures and fingerprints remain unchanged.
Performance acceptance is recorded separately in [PERFORMANCE.md](engine/PERFORMANCE.md).

### Thunderbolt trace and audio startup correction, 2026-09-18

Exact unpublished CPU pin: `1.4.1-trace.1`. [CPU_TRACE.md](engine/CPU_TRACE.md)
records architecture, source ownership, local-feed restore requirements and the
[source/package/assembly hashes](engine/copper68k-trace-development-2026-09-18.json).
The earlier `1.4.1-boundary.1` failures above remain historical evidence.

With the CPU fix alone, protection reaches decoded game code but waits indefinitely
at `$9828` for AUD0's interrupt. A bounded owner-thread trace shows DMA being
disabled and AUD0DAT being written during the last DMA word. The queued data was
discarded at the word boundary because the channel was not already marked manual.
The common audio byte loop now admits that pending word with the interrupt clear.
Four tests failed before the Paula change; all pass after it. No title-specific
behavior or media alteration was added.

Production Release build: zero warnings/errors. Isolated engine: **555 passed**;
CopperDisk: **74 passed**; host: **93 passed**, zero skips with both native Lemmings
replays enabled. Their existing complete-workload expectations remain unchanged.
Copper68k's separate Release suite has **1,499 passes and six unavailable optional
external-corpus tests**. The ROM-free vector-9 integration probe passes both
scalar and batched execution.

The six-image manifest's Thunderbolt FR disk-one IPF ZIP and Kickstart 1.3 were
replayed with the [18,000-field input script](../CopperScreen.Lightweight.Tests/Workloads/ipf-thunderbolt-storage.json).
Fire/left mouse are pressed for twenty fields at 6,000, 8,000, 10,000, 12,000 and
14,000. No swaps occur. Use the production Release runner with `--rom`, `--ipf`,
`--input-script`, `--frames 18000` and a fresh `--boot-probe` directory.

Inspected captures show the opening airliner story (no-input field 6,120), active
road/enemy gameplay (scripted field 7,980), a score of 200 and the continue prompt
(9,000), another active gameplay scene (10,800) and mission failure (13,320).
This establishes bounded protection/loading/gameplay progress. **Second-disk
handling remains unverified**; the script does not attempt to complete the game.
The runner's generic gameplay-unverified banner is not an automatic classifier;
the listed captures were inspected separately.

Final scripted run: cycle `2557836000`, CPU `F160527585CC788B`, hardware
`AA41D3F54CE6D407`, complete output `D8E60B27F942357B`, `unsupported=none`.
The preceding no-input 10,000-field run ends at cycle `1421020000`, CPU
`58B69693AB591C66`, hardware `1C1756FE60FF69ED`, output `46C35DA041BC7242`.
Local evidence: `artifacts/trace-exception-2026-09-18/`, including `audio-trace2.log`,
`paula-before.log`, `engine-final.log`, `host-native-final.log`, `disk-final.log`,
`thunderbolt-audio/` and `thunderbolt-input/`. Probe FPS/allocations include capture
work and are not performance acceptance measurements.

### ERSY absent-source synchronization, 2026-09-19

The [beam/synchronization record](engine/BEAM_SYNC.md) records the pinned
hardware probes, media hashes, exact RAM values and remaining phase discrepancies.
ERSY2 now holds `$4000` across absent HSYNC while its CPU handler continues and
clears ERSY. The accepted reference advanced into `$41xx`. ERSY1's within-line
toggle values remain identical between builds. Neither probe's pre-hold read
phases match all A500 photograph expectations; no full-probe certification is
claimed. The host retains the last synchronized framebuffer instead of trying
to reproduce the photograph's loss-of-sync distortion.

The unchanged supplied Lemmings script was replayed for 14,520 outputs. The final
capture shows active level one, ten lemmings out and the dug passage. Both native
host gameplay and keyboard press/release tests pass with the corrected boot
fingerprints. Kickstart now detects an absent genlock source and clears ERSY;
the older native fingerprint is retained in the beam record and frozen
performance protocol. Local evidence is under `artifacts/beam-sync-2026-09-19/`,
including `native-candidate-lemmings/`, both ERSY probe captures and test TRX files.
This is bounded native progress, not full game or cycle-exact chipset certification.

The subsequent optimized engine (`BB3F5233EA3FB132E89C018BB76B1CB4260144EFC61CE5A558D67CF7B3A0D7CA`)
passes all 94 host tests, including both native replays, with the same corrected
expectations. Local result: `artifacts/beam-opt-2026-09-19/terminal-host.trx`.
The separate ERSY comparison v1 checks the independently recorded old/new native
identities; its throughput disposition is recorded in the performance guide.

The final compact-fetch-order engine (`7778A0F26FCC2D2EE1F66C69172F48D7DFB6E61D2CD6623693A47FBDFC7D4C85`)
also passes all 94 host tests with the same native expectations and no skips:
`artifacts/beam-opt-2026-09-19/fetch-final-host.trx`. Optimization did not rebase
the corrected gameplay or keyboard fingerprints.
