# Additional games and demos — 2026-09-21

Seven previously unrecorded titles were exercised on source commit
`9bdc53a30263043db6e4fdacd7122ed748fee748`. Major Motion and Alien Breed reached
interactive gameplay. Arte and Desert Dream advanced through bounded sequences
without obvious persistent corruption in the inspected captures. Inside the
Machine has reproducible effect corruption; Miami Chase has overlapping menu
text after its disk swap. Lotus III remains at its disk-2 prompt.

These are native compatibility observations, not exhaustive correctness passes,
new golden outputs, or throughput measurements. No engine or runner code changed.

## Configuration and evidence

PAL OCS A500, 68000, 512 KiB chip + 512 KiB slow RAM, native Kickstart 1.3,
conservative CPU batching unless explicitly labeled scalar, one connected DF0,
read-only floppy media, no hardfile. Full 908 × 313 field captures were retained
every 60 fields and at the final field, together with registers and chip RAM.
No unsupported-feature bypass was enabled. Inputs use zero-based PAL field
indices; empty script entries release controls.

The production Release build succeeded with zero errors and one CS8034 warning:
Windows Application Control blocked the HotPathGuard analyzer. Initial native
launches also failed before emulation with `0x800711C7`, confirmed by Code
Integrity event 3077. On the user-requested retry the same frozen runner loaded
successfully. No file unblock or machine security-policy change was needed.
The failed attempts remain under `blocked-attempt/` and are not native failures.

Local evidence root: `artifacts/native-corpus-2026-09-21/`. The frozen `runner/`,
`binary-hashes.json`, `retained-results.json`, scripts, complete logs and original
captures are ignored local artifacts. The [media manifest](NATIVE_CORPUS_2026-09-21_MEDIA.json)
records archive and uncompressed-entry identities, including the three exercised
second disks. Desert Dream disk B was inventoried but not exercised. ROM/media
and third-party reference images are not committed.

| Input | SHA-256 |
| --- | --- |
| Kickstart 1.3 | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| Engine | `5934338EFBB493787F29644030F0B6F9971CB4BF3972061E40D6BB2D5AF44C8F` |
| Runner | `182C691B5E7FE48F1A2294DB7AF2E7E3BA7C8B4F553E95274B849FD0098DDBE1` |
| CopperDisk | `54FE3F78B595E5EBE417C3DEC504005052AB437A90DCFBC65FA6440E3828C8F3` |
| Copper68k | `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC` |
| CopperFloat | `4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235` |

This is a newly built set of assemblies; do not substitute the earlier benchmark
binary hashes. `LightweightA500Machine.cs` still has the accepted source hash
`C4B2AF31CB6285A992884133B0F4E65FAD70E59BE32826C4E8260D36B30646FF`.

## Outcomes and retained replays

| Title | Fields | Evidence directory / input | Observed outcome |
| --- | ---: | --- | --- |
| Major Motion | 8,500 | `major-motion-play` / [script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-major-motion.json) | Native DOS boot, title and player selection; key `1` starts play. Road, cars and HUD advance; scripted joystick input exercises acceleration, steering and fire. Gameplay captures at 6,180, 6,960, 7,740 and 8,500 have no obvious persistent corruption. |
| Alien Breed | 18,000 | `alien-breed-gameplay` / [script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-alien-breed.json) | IPF disk 1 boots to the request for disk 2; the extracted original disk-2 IPF is accepted. Menu, level-one briefing and gameplay follow. Movement, firing and scrolling are visible at 14,700, 16,380 and 18,000. Later levels and the story disk remain unverified. |
| Lotus III | 18,000 | `lotus3-mouse` / [script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3.json) | Return passes the crack/manual-code screen. The disk-2 prompt persists after mounting the supplied matching release's disk 2 and trying joystick fire plus a left mouse click. No gameplay. This is an unresolved loader/input wait, not an established disk-controller defect. |
| Miami Chase | 18,000 | `miami-chase-disk2` / [script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-miami-chase.json) | Intro, credits, briefing, disk-2 request and menu reached. The supplied archive mixes an unlabeled disk 1 and `[cr AGL]` disk 2. Menu/high-score text overlaps and remains smeared while the logo stays recognizable. The script does not establish gameplay. Media/release effects versus an engine defect remain unresolved. |
| Arte / Sanity | 18,000 | `arte-boot` / no input | Six-minute budget progresses through the introductory tunnel, artwork, rotating disc, particles, flower, dot landscape and later colour effects. No obvious persistent corruption in sampled scenes. The full ending was not established. |
| Desert Dream / Kefrens | 18,000 | `desert-dream-boot` / no input | Disk A progresses through credits, spaceship/Earth, Egypt/pyramids, title and the following object scene. No obvious persistent corruption in sampled scenes. The second disk, later effects and ending remain unverified. |
| Inside the Machine / DESiRE v1.0.1 | 18,000 | `inside-machine-boot` / no input | Opening motherboard, Amiga, title and 2D tunnel render recognizably. Face-light and rotozoomer effects are corrupted, followed by further noisy scenes; the demo still reaches the DESiRE end logo. A zero exit status is not a correctness pass. |

The initial no-input game runs each used 8,000 fields. Additional menu-only and
disk-request runs remain under `alien-breed-play`, `lotus3-play`, `lotus3-disk2`
and `miami-chase-play`; these are exploratory evidence, not extra passing titles.
`major-motion-boot`, `alien-breed-boot`, `lotus3-boot` and `miami-chase-boot`
retain the initial observations. Their scripts, where changed later, were saved
separately in the evidence root.

Audio samples were nonzero in the gameplay and active demo captures. This was
not an audible music/effects or host audio-delivery check. Full-game completion,
all controls, every scene and physical chipset timing are not certified.

## Reproducible failures and next investigations

### Inside the Machine: C2P/display corruption

The author's [technical account](https://blog.grahambates.com/posts/inside-the-machine/)
describes a four-plane, blitter-converted face-light effect and a HAM7 rotozoomer.
The corresponding [face reference](https://blog.grahambates.com/posts/inside-the-machine/images/face-lights.png)
and [rotozoomer reference](https://blog.grahambates.com/posts/inside-the-machine/images/roto.png)
show coherent shapes and textures, unlike our fragmented face at field 3,840
and noisy texture at 4,380. These are independent visual expectations, not a
cycle-level hardware oracle or a frame-aligned pixel comparison.

The supplied NFO specifies OCS 512k/512k. Author source was inspected read-only
at [commit 822a4031](https://github.com/grahambates/inside-the-machine-public/tree/822a4031cbbe94aed271d6a746d556d233574fb6).
The four-plane face scene already fails before the later HAM7 scenes, so blaming
HAM7 alone is not supported. Start by checking chunky-to-planar blits and source
buffer contents, then the display fetch path; do not alter media timing or add
title-specific behavior without evidence.

A 4,300-field scalar-CPU replay reproduces byte-identical BMPs at fields 3,600,
3,840 and 4,200. This excludes conservative CPU batching as the differentiator
at those checkpoints, but does not isolate CPU, blitter or display semantics.

| Field | Matching scalar/batched BMP SHA-256 |
| --- | --- |
| 3,600 | `F8DD975F443E65E6F2CFF1299BDDFB85C07E29B530CF911C5D7900CBFB8459D7` |
| 3,840 | `BEE72A8929116B8D0D030B099A798A833A14854EA21FEA165A048D080709DF3F` |
| 4,200 | `0FAA4D2C97122A5F60FC46CCD9FA3B1CFC2E3FDA2B7F891D2E2CD0BFC8248944` |

### Miami Chase: overlapping menu text

Disk 2 is read successfully and reaches the menu. Captures 11,460, 13,080,
14,700 and 18,000 show text accumulating/overlapping while the title remains
recognizable. Retain the exact mixed-release media identities when comparing
with another emulator or real machine. The 16,000-field scalar replay has
byte-identical BMPs to the batched run at fields 11,460, 13,080, 14,700 and 15,960;
the failing 13,080 capture's SHA-256 is
`711EC05D97806EE16FC43C7B4031A6457A1834311537FDD8B6A6FEAEF80BDFCE`.
The underlying cause has not been assigned to a subsystem.

### Runner: scripted ZIP-entry paths on Windows

`NativeInputScript` applies `Path.GetFullPath` to the entire media selector,
turning `archive.zip#/entry.ipf` into `archive.zip#\entry.ipf` on Windows.
`Program.ReadImage` recognizes only `#/`, so construction fails with
`DirectoryNotFoundException` before emulation. Direct `--disk archive.zip#/entry`
works. `alien-breed-scripted-zip-failure.log` preserves the reproduction.
Using the exact extracted entry allowed the native swap tests to continue.

The host-only correction should resolve the archive path independently and
preserve the entry selector. Add Windows absolute/relative selector coverage;
this session records the defect without changing the frozen runner.

## Reproduction

Use the media manifest's selected entry for each initial `--disk`. Before the
three game swap scripts, extract the exact original entries into the ignored
repository `media/` directory as `alien-breed-disk2.ipf`, `lotus3-disk2.adf` and
`miami-chase-disk2.adf`. Their bytes must match the manifest. This avoids the
scripted-ZIP host defect without converting IPF tracks or modifying archives.

```powershell
$runner = 'artifacts/native-corpus-2026-09-21/runner/CopperMod.Amiga.Lightweight.Runner.dll'
dotnet $runner --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/Team17/AlienBreed_998.zip#/AlienBreed_GameDisk1.ipf' `
  --frames 18000 --wide-output `
  --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-alien-breed.json `
  --boot-probe artifacts/native-corpus-replay/alien-breed
```

Use the outcome table's script and field count for the other games; omit
`--input-script` for demos. Add `--scalar-cpu` for the explicitly scalar checks.
Do not overwrite retained evidence directories when reproducing.

Captures allocate memory and write files, and some diagnostic runs overlapped.
Their FPS/allocation totals are not performance acceptance evidence. No formal
benchmark was run or needed for these documentation/input-only additions; the
accepted performance record and default one-sided 95% upper-bound limit of 1%
remain unchanged.

## Complete-workload diagnostic identities

All retained runs below completed their stated field budgets with `unsupported=none`.
That reports implemented-feature acceptance, not absence of guest or visual bugs.
The failing captures above demonstrate why these identities are diagnostic records
and must not be promoted to correctness goldens.

| Replay | Fields | Final cycle | CPU | Hardware | Pixel/audio output |
| --- | ---: | ---: | --- | --- | --- |
| Major Motion gameplay | 8,500 | 1207735054 | `7913B0150DB06163` | `674D7CFDECAA195F` | `BBECC9A1939A4A5E` |
| Alien Breed gameplay | 18,000 | 2557704038 | `87F3A12F9CCD5671` | `B9B39A3469EBFFA3` | `81E0ABC1B06A2FA3` |
| Lotus III mouse/joystick attempt | 18,000 | 2557704034 | `D5287FD12F279BAB` | `D19AE50DFE8CA6C7` | `A09F231F16B2DB40` |
| Miami Chase disk 2 | 18,000 | 2557704046 | `A4F4907875FC5125` | `EDC878C83754F974` | `34BF8DA80259E6FA` |
| Arte | 18,000 | 2557704032 | `F15BB28E659DEA2D` | `B4C892A023312BDE` | `DF41781EEC922DE3` |
| Desert Dream | 18,000 | 2556214910 | `C73800F93E269B8C` | `2EED5C117EB29296` | `208EACCD38A676EB` |
| Inside the Machine | 18,000 | 2557704030 | `8953C7873A196DFC` | `30DF46790E671E23` | `B9B4D078BAE4E870` |
| Inside the Machine scalar | 4,300 | 610906644 | `05DB78CA051E3F0D` | `93BF6B1F31DAC8CD` | `6487552B18CD5C96` |
| Miami Chase scalar | 16,000 | 2273500034 | `EFD68AB282E3A749` | `859F3DA9D4593B6C` | `DEC17EA186BA8006` |

## Investigation follow-up

The [final-row modulo investigation](BLITTER_FINAL_MODULO.md) repairs the Inside
the Machine effect corruption and Miami Chase menu accumulation through one
generic blitter pointer correction. The failures and identities above describe
the original corpus build and remain historical evidence. Corrected captures,
changed pixel identities, tests and the new performance comparison are recorded
separately; Lotus III and scripted ZIP paths remain unresolved.
