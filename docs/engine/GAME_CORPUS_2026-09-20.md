# Additional native game corpus — 2026-09-20

Four previously unrecorded titles were exercised on accepted `1db2d0de06e8b7d043a8565e4f7a8d75755048c8`.
Lotus Turbo Challenge 2 and Apidya reached bounded interactive gameplay. North &
South showed persistent display corruption, and Super Cars II reached a Guru.
Both failures reproduce identically on pre-disk-fix `a10b22c`; their underlying
cause is not established. This session changes no engine code or golden tests.

Subsequent [North & South investigation](NORTH_SOUTH_INVESTIGATION.md) finds and
corrects a deferred Copper restart defect: the old intro list incorrectly
overwrote the game's setup during handoff. The unmodified CP disk now has clean
menus and borders. The alternate QTX release also has clean menus on the unchanged
engine. The original observations below remain historical evidence; QTX's hash
was added to the manifest.

The [Super Cars II follow-up](SUPER_CARS_II_INVESTIGATION.md) identifies missing
CPU chip-RAM mirrors. The candidate passes the formerly crashing depacker and
reaches the first race with the same 512 KiB chip + 512 KiB expansion profile.
The original failure below remains historical evidence.

## Configuration and identity

PAL OCS A500, 68000, 512 KiB chip plus 512 KiB slow RAM, Kickstart 1.3,
conservative CPU batching, one connected DF0, read-only media, no hardfile.
The production Release runner was frozen under ignored
`artifacts/game-corpus-2026-09-20/runner/`. Captures are full 908 × 313 fields,
every 60 fields and at the final field. No unsupported-feature bypass was used.

| Input | SHA-256 |
| --- | --- |
| Kickstart 1.3 ROM | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| Lightweight engine | `501A3DBD82A29DEA310E308D077BB93965CB31D9F4E8E166C7050CA5166DFA02` |
| Runner | `179EFEBFBD3604BED593A78EE415B8E324F7BA2B339C4190FA72A723D528154A` |
| Copper68k | `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC` |
| CopperDisk | `FC095F534878072328CDA4745016F39C269A4759D15B8264F6B66A2E9875FB16` |
| Reference engine (`a10b22c`) | `33BACBDCA3A8A427D09B405334364B890AB38991C2809A52BB2933942F69E64F` |

The [media manifest](GAME_CORPUS_2026-09-20_MEDIA.json) records archive and
uncompressed entry hashes. Super Cars II disk 2 and Apidya disk 2 were inventoried
but **not exercised**. Other cracks/releases are not interchangeable with these
observations. ROMs, media and local captures are not committed.

## Outcomes and reproduction

| Game / media | Fields | Script | Observed outcome |
| --- | ---: | --- | --- |
| Lotus Turbo Challenge 2 / ADF in ZIP | 13,400 | [corpus-lotus2.json](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus2.json) | Loading, crack prompt, attract screen and starting grid; driving input advances the race. At field 13,020 the HUD shows 61 MPH and score 50,620; final score is 77,950. Bounded gameplay reached, no obvious persistent corruption in inspected captures. |
| Apidya / `Apidya_764.zip#/Apidya_Disk1.ipf` | 10,800 | [corpus-apidya.json](../../CopperScreen.Lightweight.Tests/Workloads/corpus-apidya.json) | Title, options and first-stage scrolling gameplay. Right/fire input moves the player across the screen between fields 8,760 and 8,820; score reaches 20. Later deaths end at the continue screen. Disk 2 and later levels remain unverified. |
| North & South / `[cr CP]` ADF in ZIP | 8,500 | [corpus-north-south.json](../../CopperScreen.Lightweight.Tests/Workloads/corpus-north-south.json) | Crack intro exits; mouse selects English and reaches the main menu. Language and main-menu graphics are persistently corrupted. Gameplay not verified. |
| Super Cars II / disk 1 `[cr Flashtro]` ADF in ZIP | 2,400 | None | Boot ends at `Guru Meditation #0000000B.00C01570`. No gameplay or disk swap verified. |

Example from the repository root, using locally supplied media:

```powershell
$runner = 'artifacts/game-corpus-2026-09-20/runner/CopperMod.Amiga.Lightweight.Runner.dll'
$rom = 'C:/Data/ROM/Kickstart_13.rom'
$disk = 'C:/Data/TestImages/Team17/Apidya_764.zip#/Apidya_Disk1.ipf'
dotnet $runner --rom $rom --disk $disk --frames 10800 --wide-output `
  --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-apidya.json `
  --boot-probe artifacts/game-corpus-replay/apidya
```

Use the corresponding row's media, field count and script for the other games;
omit `--input-script` for Super Cars II. Script indices are zero-based PAL fields,
not milliseconds. Inputs remain held until the next entry; empty entries release
them. Joystick port 1 uses up/down/left/right/fire bits 1/2/4/8/16. Lotus's late
fire hold provides acceleration. The North & South script splits vertical mouse
motion into 55-count steps; the earlier exploratory single 165-count movement did
not select the intended flag and is not the retained reproduction.

Local evidence root: `artifacts/game-corpus-2026-09-20/`.
Retained directories are `lotus2-gameplay`, `apidya-gameplay`,
`north-south-current`, `north-south-reference`, `supercars2-boot` and
`supercars2-pre-diskfix`, with same-name `.log` files alongside. Each capture has
a BMP, register JSON and chip-RAM dump. Useful visual fields are Lotus 13,020,
Apidya 8,760/8,820, North & South 8,500 and Super Cars II 2,400.

## Final complete-workload identities

These identify the exact diagnostic replays; they are not newly accepted golden
outputs. All four runs completed their field budgets with `unsupported=none`.
That status and a zero process exit code do **not** detect guest crashes or
incorrect graphics.

| Game | Final cycle | CPU | Hardware | Pixel/audio output |
| --- | ---: | --- | --- | --- |
| Lotus II | 1904034866 | `6DB3260E1D9123D4` | `42D9E66CEC528962` | `E8FA8868EFD76CE6` |
| Apidya | 1534569652 | `1DFBBE30EB29C948` | `A40E33456FF78380` | `8D02B60E422E730E` |
| North & South | 1207735070 | `FED9435353EF7C22` | `C19B4011DA61219F` | `35A3ECD8E6300710` |
| Super Cars II | 340780654 | `E4814757BD70D78E` | `3F100D4F48E933A5` | `226D903043D2393A` |

The two failing workloads have matching cycles and all three fingerprints on
`a10b22c` and `1db2d0d`, using the same runner, media and inputs. Final BMP SHA-256
also matches between builds:

- North & South: `15D8210AD8713050BF2E0D7A8061DB41F53702515F50E6A19A1B97E1C5AB2AC2`.
- Super Cars II: `CA74CD0FDE2A09E1022027B141610E1989AE76AD087814BF756C83B8959B3F55`.

This excludes the latest disk-control changes as the source of either observed
failure. It does not distinguish older engine defects from media/crack behavior.
Next investigation should find the first incorrect display/data transition for
North & South and the exception/loader path for Super Cars II, with another
known-good release or independent evidence where available. Do not infer a CPU
implementation defect from the Guru number alone.

Audio buffers were nonzero during inspected Lotus and Apidya gameplay, but this
was not an audible music/effects check. The final Apidya continue screen reports
silent audio. These bounded runs do not establish full-game completion, all
controls, second-disk handling, protection correctness or physical chipset timing.
Some diagnostic runs overlapped and captures allocate and write files: their FPS
and allocation totals are **not performance acceptance measurements**. The prior
performance record and default 1% gate remain unchanged.
