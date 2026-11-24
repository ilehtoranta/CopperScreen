# Challenging Amiga games

The replays use original disk images identified by Software Preservation Society
release number and archive SHA-256. Local archives and disk images stay outside
the repository.

| Target | Release | Disks | Archive SHA-256 |
| --- | --- | ---: | --- |
| Banshee | Core Design, 1994, AGA, multilingual, SPS 0633 | 4 | `CCAF21C6A9A27086D368FF760B7A248531FA2A98EBE168DB8A13E383E8996286` |
| Zool 2 | Gremlin, 1993, AGA, SPS 0415 | 2 | `34CBCACFB989589B415432DB57CE96EAD8D9A147458F20A8A192EE2DCB7B44C7` |
| P.P. Hammer and His Pneumatic Weapon | Demonware, 1991, SPS 2809 | 1 | `DA084D1AC4D85991B97C8B8FA265AE6DA73533D2F9AFE272EC6374F621F9C7ED` |
| All New World of Lemmings | Psygnosis, European AGA release, SPS 0625 | 4 | `A4E64063AFAB2437DF10D068E7F31A24E0A1D8B26DB5C230451549250BF93D51` |
| RoboCop 3 | Ocean, European dongle edition, SPS 0805 | 3 | `B67CCF3AE5DF8282597676A6175296E25D337A9B5C2C212A3B16BB0DB1C4E41B` |

Sources are the [SPS archive mirror](https://archive.org/details/commodore-amiga-games-sps)
and the [Lemmings Archive](https://www.camanis.net/lemmings/lemmings3.php).

The AGA baseline uses a PAL A1200 with an EC020, 2 MiB Chip RAM, no expansion
RAM and Kickstart 3.0 revision 39.106. The ROM SHA-256 is
`293BEEA59D7DE4CAAB3E07223D66E844AD5268EB06A04F405B523A0A1543D64C`.
The OCS baseline uses a PAL A500 with a 68000, 512 KiB Chip RAM, 512 KiB
slow RAM and Kickstart 1.3. The ROM SHA-256 is
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.
Disks are mounted in release order, starting at DF0. WinUAE 6.0.3 uses equivalent
PAL hardware and cycle-exact emulation. CAPSImg 5.1 decodes the IPF tracks.

## Banshee

WinUAE's historical changelog identifies border sprites as the reason the
scoreboard was missing. It also records separate fixes for horizontal subpixel
scrolling and sprite alignment. The current Lisa model enables border sprites
with BPLCON0.ECSENA and BPLCON3.BRDRSPRT when border blanking is off. It drops both
the display-window and first-BPL1DAT gates for that mode. [WinUAE changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19225-L19227)
· [WinUAE 6.0.3 Lisa display logic](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/drawing.cpp)

CopperScreen had applied the first-BPL1DAT gate even with border sprites enabled.
A focused DMA test puts a red sprite before the first bitplane fetch and repeats
the case on a line above the playfield. Both cases rendered only the background
before the fix. The third case keeps BRDRSPRT clear and checks that the background
remains visible. The correction uses the existing Lisa compositor and sprite DMA.

The native Banshee replay on `e086a46` reached the first mission's “Get Ready”
screen but drew only the right half of the scoreboard. Frame 8,400 has the
scoreboard at x=512–767, with two CopperScreen samples per WinUAE pixel.
The comparison sampled even x values on rows 48–292 and used the corresponding
WinUAE rows and columns in the cycle-exact “Get Ready” capture. All 31,360 RGB
samples matched WinUAE after the correction. The baseline matched 17,740.
The remaining 13,620 samples were black in the baseline and matched the missing
left panel in WinUAE. The playfield and “Get Ready” legend are visible in both
the corrected frame and the reference.

| Capture | SHA-256 |
| --- | --- |
| CopperScreen baseline, frame 8,400 | `BADB036D56741C1122DC18BDACED77E483894040B0BBE36A379E5B9F122CC1BC` |
| CopperScreen with the Lisa correction, frame 8,400 | `A3F3CA97EC9AD4E1736187506C3A3F817F69BE34376E9A4F73724875D8EDC7DE` |
| WinUAE 6.0.3, “Get Ready” | `077BB19126E3FA7BFF70303232FD9F6E9EEC35431E99ABCA8015DD9D44925BCC` |

The replay pressed joystick port 2 fire for ten fields starting at input
fields 3,900, 4,700, 5,500, 6,300 and 7,100. Use the runner's
`--input-script` with `joystickPort1: 16` on each press and an entry ten
fields later to release it. Start with the four disk images fitted and use
`--agnus alice --denise lisa --cpu 68ec020 --chip-ram-kib 2048 --slow-ram-kib 0`.

The interactive replay then applied port 2 states 16 at field 8,550,
24 at 8,650, 20 at 8,800, 17 at 8,950, 18 at 9,150 and 16 at 9,350.
The corresponding releases were at 8,565, 8,700, 8,850, 8,985, 9,185 and
9,370. The replay completed 10,500 fields with no unsupported feature reported.
Opening mission captures show horizontal and vertical player movement, enemy
aircraft, bullets, explosions, scrolling water and updates to the restored
scoreboard. WinUAE also reached the opening mission with the same disk set.
This verifies the opening mission and exact scoreboard output at the measured
checkpoint. Later levels and physical Lisa timing remain open.

The Release solution build passed with zero warnings. The engine diagnostics
passed 1,417 tests and skipped seven optional native cases. An isolated build
of the sprite correction passed all 88 AGA sprite tests.

## Zool 2

The SPS 0415 release accepts the original code-wheel response and proceeds
through the options and character-select screens into Swan Lake Stage 1 on
CopperScreen, WinUAE 6.0.3 and WinUAE 6.1 beta 23. The Commodore Combat
pressing, SPS 2427, also requires the wheel. The interactive wheel supplied
the response for each engine's challenge. [Zool 2 code wheel](https://steveleonardweb.co.uk/?page_id=7116)

CopperScreen `bdb72d5` completed 11,250 fields with no unsupported feature
reported. The first-stage captures consistently show missing HUD counters.
Both engines show the player and foreground objects across the gameplay
sequence. The independent reference captures show different player positions
and score values.
At the CopperScreen checkpoint, BPLCON0 was `$7401` and DMACON was `$03DF`.

The stage Copper list writes SPRxPOS, SPRxDATA and SPRxDATB repeatedly on
the same scanline to draw the HUD. The WinUAE 6.1 beta 23 register dump
shows that seven of the eight channels have zero SPRxCTL in this scene.
Lisa arms on a data write independently of Alice's vertical DMA comparator.
It can also perform another horizontal match without the position write
replacing data already in the shifter. Two focused tests reproduce the missing
pixels in those cases and pass with the Lisa correction. [WinUAE sprite-write logic](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/drawing.cpp#L2711-L2763)

The corrected 11,250-field replay shows player movement, jumping, pickups,
scrolling and live score, time and lives counters in Swan Lake Stage 1.
Of 76 captured fields, 62 were byte-identical to the baseline. The 14 captures
from field 9,300 through 11,250 changed only within HUD rows 45–56.
At field 9,600 the baseline and corrected BMP SHA-256 hashes were
`D942F2BDABE35A490A233B9FAC5DFAB7EA798EFDBB5D3D39A11249F56F94C72E` and
`055FB17D4DD56BC37598FD056F7D925660543F22D3128D9AF649D23E03D16E2F`.
Both replays ended with CPU fingerprint `0xC0583D2D81EAE174` and hardware
fingerprint `0xCDC80A370169C5D7`. The final output fingerprint changed from
`0x429056CF8B5D00FB` to `0x1C203DFD633C79C4`.

The Banshee replay matched all 71 captured frames and all three final
fingerprints from its 10,500-field verification. The Release solution build
passed with zero warnings. Engine diagnostics passed 1,419 tests and skipped
seven optional native cases.

The WinUAE 6.1 beta 5 changelog identifies an Alice mainboard busy-signal
correction for Zool 2 AGA. In FMODE 0 with seven lores planes and blitter
priority off, CPU activity can postpone the final D write after the blitter
reports completion. The changelog also records an earlier, separate bitplane
DMA overrun fix for the title. [WinUAE changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L92-L98)

### Area blitter pipeline

The [probe generator](../../scripts/generate-blitter-area-probe.ps1) creates
a bootable ADF with known channel selections and Copper starts. With
`-Overlap`, matching PAL A1200 replays copy three words one word forward in
Chip RAM using A+D and A+B+C+D. The unmodified input has words `$1111`,
`$2222`, `$3333` and `$4444`. WinUAE leaves `$1111 $1111 $2222 $3333` in
memory. CopperScreen `18e5a39` left `$1111` in all four words because it
wrote D before fetching the second source. The destination latch now writes
the preceding result while the current sources advance. The source pointers
apply their modulo with the last read of each row. The destination pointer
applies its modulo with the last write. Both overlapping-copy regressions
fail on the old engine and pass with the pipeline. [Commodore Amiga Hardware Reference Manual, §6 Pipeline Register](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_3rd_Edition.pdf)

With `-Width 1`, the A1200 A+D probe in WinUAE 6.1 beta 23 fetches A six
CCKs after the Copper writes BLTSIZE, reports IRQ two CCKs after A and writes
D three CCKs after A. A+B+C+D fetches A, B and C six, seven and eight CCKs
after BLTSIZE, reports IRQ at ten and writes D at eleven. CopperScreen
reproduces those relative events in both cases. The OCS probe has the same
transfer ordering and reports the IRQ two CCKs sooner than AGA. The engine
retains the final D separately after BUSY clears. [WinUAE area blitter completion](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L408-L461)

Using `-Width 1 -SevenPlanes -CpuPressure -WaitPosition 0x2F` places the
drain in the seven-plane fetch window. For A+D at raster line `$76`, WinUAE
shows IRQ at horizontal slot `$3C`, the final D at `$44`, then the next CPU
transfer at `$4C`. CopperScreen's focused test sets up the same arbitration
condition with an all-channel blit. It observes the old destination after
IRQ and obtains the new value from the subsequent CPU read. Disabling the
interrupt's arbitration effect in a diagnostic probe lets that read see the
old value. The engine counts all blocked CPU chip slots toward a nice-mode
yield, including bitplane DMA, and suppresses the yield for the CPU request
that observes Alice's completion pulse. [WinUAE AGA CPU arbitration](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L12344-L12362)

The 11,250-field Zool 2 replay passed the original code wheel and reached
Swan Lake Stage 1 with scrolling, movement, jumping, pickups and live HUD
counters. Of 39 comparable captures, 31 matched the earlier sprite-fixed
build byte for byte. The eight changes start after entering gameplay and
the runs show different positions, scores and animation states. The replay
ended with CPU `0x6A1322DAF11C0F43`, hardware `0xAA48D0AC246FFD9D` and
output `0x90011C90E526E705`.

The same build also replayed Banshee through the opening mission, P.P.
Hammer through the first room, Lemmings through the first Classic level and
RoboCop 3 through the driving sequence. Banshee matched all 36 sampled
frames from the earlier replay, P.P. Hammer matched all 25 and Lemmings
matched all nine. RoboCop 3 matched 34 of 40 captures and continued driving
with different positions in the later captures. Per-field and final
fingerprints can change with the new bus ordering. All five replays ran
without unsupported feature reports. The Release build passed with zero
warnings. Engine diagnostics passed 1,432 tests with seven optional native
tests skipped.

Absolute Copper start timing, source-only channel schedules, physical signal
measurement and a matched capture of the game's active blitter register
rewrites remain open.

## P.P. Hammer and His Pneumatic Weapon

The original SPS 2809 disk passed the disk-operation screen, played the
intro and entered Level 1 on CopperScreen `61e2d42` and cycle-exact WinUAE
6.1 beta 23. The early 2,100-field probe had ended while disk loading was
still in progress. An 8,000-field replay used brief port 2 fire presses to
advance the prompts. It completed without an unsupported feature report.

The CopperScreen field 6,450 capture and WinUAE's opening-room capture match
86,362 of 86,664 RGB samples from the playfield at CopperScreen x=260–887,
y=80–217. For each sample, WinUAE x is `x - 198` and its double-height y is
`2 * y - 54`. The differences are confined to the animated flames, player
outline and pickup. These are captures from different gameplay fields.

A second 7,200-field CopperScreen replay moved and jumped through the room,
picked up items and used the hammer. The score reached 100 and the camera
scrolled as the player moved. The WinUAE replay also showed movement,
jumping, item collection and scrolling. Both replays showed the animated
flames and running countdown. The CopperScreen replay ended with CPU
fingerprint `0x29F2B9E8C02194C4`, hardware `0x652E5424481AC45A` and output
`0x5253696AB2B81319`, with no unsupported feature report.

The opening-room capture SHA-256 hashes were
`36B3D7687C628514B372D6EF6CADB36BACE3EBA5FADDBAF6D32582ECBFE91BD0`
for CopperScreen and
`73D8A3CBC93A2724D607D6631DA93ABCCABC1F93A27DB1C8E5F51C27A1AD4DA5`
for WinUAE. The opening-room comparison required no emulator change. The
historical WinUAE timing note identifies a broad CPU and blitter improvement.
Later rooms and exact CPU and blitter contention remain unverified.
[WinUAE P.P. Hammer history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19736-L19741)

## All New World of Lemmings

The original SPS 0625 set boots on the PAL A1200 profile with all four disks
mounted. CopperScreen `0347773` and cycle-exact WinUAE 6.1 beta 23 reached the
title, the animated Ark menu and the first Classic level. The CopperScreen
replay used mouse movement and a left click on the Classic Lemming on the
Ark's roof. It completed 11,500 fields and drew the playfield, moving
Lemmings, mouse cursor and skill bar without an unsupported feature report.
The final CPU, hardware and output fingerprints were `0xB596337889663E10`,
`0x104B88F5AB42E4EE` and `0xEC9DCD3447260BA2`.

The title logo matched all 5,328 sampled RGB pixels. At field 11,500,
125,214 of 128,000 samples from the first-level playfield and skill bar
matched the independent WinUAE capture. The captures show different
animation positions and timer states. The first-level comparison samples
even CopperScreen x values from 516 through 1,794 and rows 70 through 269.
Reference coordinates are `(x - 400) / 2` and `2 * y - 54`.

The first-level capture SHA-256 hashes were
`65027C8257D07112A070745B9614C8C860005D5740FEFFEE28BC53BBD0393975`
for CopperScreen and
`A49D7E450D88F2A56A4A2D78C29F457B89253CA924B56032B8AC0D7845C99A55`
for WinUAE. The hardware checkpoints show DDFSTRT `$0028`, DDFSTOP `$00C8`
and a modulo of `-8` at the CopperScreen frame boundary. A WinUAE register
dump from the level shows FMODE `$0003` and a modulo of zero at a different
raster position.
The changelog records a historical cycle-exact bitplane modulo correction.
Moving the cursor to both edges in a 13,750-field replay left this level's
view fixed while animation continued. WinUAE also kept the first-level view
fixed at the right edge. Modulo write timing and scrolling in a larger
level still need a matched trace. [WinUAE Lemmings history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19933-L19936)

## RoboCop 3

The initial 2,100-field CopperScreen replay of SPS 0805 reached the expected
access-denied screen with no dongle fitted. With its RoboCop 3 dongle option
enabled, WinUAE 6.1 beta 23 reached the original developer logo, credits
and language selection.

The original boot code configures CIA-A PRA bit 7 as an output, samples
JOY1DAT, pulses the fire line and samples JOY1DAT again. It requires bit 8
to change across the pulse and repeats the check on the access-denied screen.
CopperScreen fits the accessory explicitly through
`LightweightA500Configuration.RoboCop3DongleEnabled` or the runner's
`--robocop3-dongle` option. The CIA output clocks the dongle's vertical
counter bit on the rising latch transition. The affected JOY1DAT readback
uses bits 8 and 0. Focused tests cover the output-direction requirement,
repeated writes, two pulses, unplugged behavior and the other port.
[WinUAE dongle model](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/dongle.cpp#L151-L201)

With the accessory enabled, the PAL A500 replay passed protection and
entered English Movie Adventure, including the Delta City introduction
and the animated Mediabreak. A separate 11,600-field replay entered
Arcade Action, selected the driving scenario from the headlines and
accelerated and steered the car with the cursor keys. Both replays used
the unmodified disks. WinUAE also reached playable driving. The right
dashboard fascia matched all 11,620 sampled RGB values from independent
driving captures. The comparison uses CopperScreen x=732–897, y=174–243
and WinUAE coordinates `x - 198`, `2 * y - 54`.

The driving replay ended with CPU fingerprint `0xBEC477F258AD96EC`,
hardware `0x800B30BC29E9F590` and output `0x0730A40FB12432B0`, with no
unsupported feature report. The same build without the accessory still
showed access denied. The Release solution build passed with zero warnings.
Engine diagnostics passed 1,427 tests and skipped seven optional native
cases. Physical dongle signal timing and later missions remain unverified.
