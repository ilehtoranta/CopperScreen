# Native software requiring ECS

Validated on 2026-10-01 against mainline engine commit `525f5d3` and Copper68k
`1.5.0`. **Final Fight: Enhanced — Final Edition** reaches its opening level
under native Kickstart, with identical normal/scalar captures through 20,000
fields. No production engine change was required. The
[machine-readable record](ECS_REQUIRED_MEDIA_2026-10-01.json) binds the source,
assemblies, media, input and selected captures.

## Selection and actual ECS dependency

The [author's release page](https://prototron.weebly.com/final-fight-enhanced.html)
targets a 68000 A600 with 2 MiB Chip RAM. The instructions in the author's
linked Final Edition archive require ECS Denise and recommend full ECS.
The [development account](https://prototron.weebly.com/blog/final-fight-enhanced-final-edition-complete-breakdown)
also describes separate native tests with Kickstart 1.3 and 2.05.
The archive was obtained from that author-linked distribution. Only its two
ADFs and instructions were extracted; its bundled ROM was not used. No media,
ROM, asset or game-code bytes are included in this repository.

This title supplies two distinct controls:

| Control | Observed result |
| --- | --- |
| 8375 / 2 MiB Chip / ECS 8373 Denise | Intro, title, Maki selection, both disks, opening level, scrolling, combat animation and continue screen reached |
| Same 8375 / 2 MiB Chip, OCS 8362 Denise | Intro boots, but blue border strips remain where ECS suppresses them |
| Same 8375 / ECS 8373, only 1 MiB Chip | Guest Guru `80000003:00001970`; no game intro/menu reached by field 6500 |

The intro's real guest Copper list writes BPLCON0 `$5201` and BPLCON3 `$0020`:
ECSENA plus BRDRBLNK. At field 2400, the ECS/OCS controls have identical captured
CPU/device state and Chip RAM, apart from framebuffer width. After duplicating
each OCS sample horizontally for comparison, exactly 11,808 RGB samples differ,
all outside the horizontal display window; no RGB sample inside differs.
Framebuffer alpha is excluded from that comparison. This exercises the
[documented ECS border control](https://bastya.net/AmigaDevDocs/hard_c.html).

The mixed 2 MiB Agnus/OCS Denise machine can boot this release. Its dependency
is correct rendering and Chip RAM capacity, not a demonstrated DENISEID boot
gate. This distinction is retained rather than interpreting a boot as proof
that the game's ECS requirement is absent.

Tower Assault, Superfrog, Arte and Inside the Machine were not selected as
ECS-required coverage: their OCS-compatible paths would establish compatibility
on an ECS machine. The author-uploaded TheBullShit source archive was also
inspected, but contains no executable and was not rebuilt or replayed.

## Native replay and milestones

Profile: PAL board, 68000, Agnus 8375 part 318069-10, Denise 8373, 2 MiB Chip,
no slow/Fast RAM, supplied Kickstart 1.3 v34, two read-only DD ADFs, and complete
1816×313 rasters. This is the supported ECS configuration, not a validated
complete A600/Kickstart 2.05 machine profile.

| Milestone | Reviewed capture or input field |
| --- | ---: |
| Animated authored intro | 900, exploratory boot |
| Native title menu | 6100, exploratory launch |
| Maki character selection | 6200 |
| Disk-two asset loading | 6400 onwards |
| Opening level with character, HUD and music/PCM output | 14000 |
| Scripted right movement and scrolling | 16000–16200 |
| Repeated attack input, character/enemy animation | 16300–18100 |
| Continue screen | 18400–20000 |

The frozen [input script](../../CopperMod.Amiga.Lightweight.Runner/Workloads/final-fight-enhanced-final-edition-ecs.json)
exits the standalone intro with the left mouse button, then uses the second
controller port to skip the story, start one player and select Maki. The title
menu returns to the attract sequence after approximately 512 fields; the
initial exploratory 600-field button spacing repeatedly missed it. Bounded
instruction tracing confirmed controller reads and exposed that script timing
error. It did not establish a CPU defect. Normal/scalar exploratory captures
also agreed. The final script adds movement, attack pulses and a second-button
input; the jump action was not isolated visually as a separate passed check.

Both final cold boots execute 20,000 fields. All **606 captured files per mode**
are byte-identical: CPU/device JSON, Chip RAM, empty slow RAM, BMP, PCM and ECS
register metadata. Their final cycle, CPU, hardware and output summaries also
match. Every capture has no CPU/video unsupported feature. The preceding
production build's engine/runner assembly hashes are unchanged and match the
[ECS implementation record](ECS_DISPLAY_2026-10-01.json).

## Reproduce

Use the exact media hashes in the JSON record, naming the extracted images
`disk1.adf` and `disk2.adf`. Supply your existing ROM and media paths; the script
contains no media path or download step. From the repository root:

```powershell
$ecsMedia = 'C:/path/to/FinalFightEnhanced-FinalEdition'
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --rom C:/Data/ROM/Kickstart_13.rom --disk "$ecsMedia/disk1.adf" --adf1 "$ecsMedia/disk2.adf" --drives 2 --input-script CopperMod.Amiga.Lightweight.Runner/Workloads/final-fight-enhanced-final-edition-ecs.json --agnus 8375-318069-10 --denise 8373 --chip-ram-kib 2048 --slow-ram-kib 0 --frames 20000 --boot-probe artifacts/ecs-final-fight-new-normal --boot-probe-interval 200 --boot-probe-extended --wide-output
```

Repeat with `--scalar-cpu` and a fresh scalar output directory. Compare all six
file types, not just final checksums. Local investigation evidence is retained
under `artifacts/ecs-required-2026-10-01`; the canonical complete inventory hash
is recorded in the JSON. This is a separate ECS replay, not an addition to or
replacement of the fixed OCS `native-regression-v1` baseline.

## Coverage limits

This is bounded first-stage compatibility and ECS border-blanking evidence.
No completed level, full game, other characters, two-player mode, independent
audio-quality check, desktop interaction or physical-hardware comparison is
claimed. SuperHires, DIWHIGH, large blits and programmable raster use were not
established by this title. Native third-party coverage of those ECS features
remains open; the earlier native OS/programmed probes retain their own scope.
Normal/scalar agreement establishes consistency, not a hardware oracle.

Runner capture FPS/allocation totals are diagnostic and are not performance
acceptance. Historical protocols and fingerprints are unchanged; see
[PERFORMANCE.md](PERFORMANCE.md).
