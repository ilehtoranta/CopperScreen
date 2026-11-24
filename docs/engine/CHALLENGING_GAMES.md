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

## Other baseline observations

The initial 2,100-field CopperScreen replays at `e086a46` reached the Zool 2
code-wheel screen, the P.P. Hammer disk-operation screen, the DMA Design logo
in All New World of Lemmings, and the expected access-denied screen in
RoboCop 3 with no dongle fitted. These captures establish boot checkpoints.

The WinUAE changelog specifically identifies an Alice mainboard busy-signal
correction for Zool 2 AGA. In FMODE 0 with seven lores planes and blitter
priority off, CPU activity can postpone the final D write after the blitter
reports completion. Its changelog also records an earlier, separate bitplane
DMA overrun fix for the title. [WinUAE changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L94)

The same changelog records historical P.P. Hammer CPU and blitter timing
improvements and an All New World of Lemmings bitplane modulo correction.
[P.P. Hammer history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19736-L19741)
· [Lemmings history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19933-L19936)
