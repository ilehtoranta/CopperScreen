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

The production build and diagnostics were repeated on pushed revision
`4169eac` with Copper68k 1.5.10. The same Zool 2 replay matched all 39
captures and all three final fingerprints from the run with Copper68k 1.5.7.

### Active area blitter modulo writes

The area blitter held BLTAMOD, BLTBMOD, BLTCMOD and BLTDMOD from BLTSIZE
until the blit finished. The original [area probe generator](../../scripts/generate-blitter-area-probe.ps1)
with `-LiveModulo AD` runs four-row A-to-D copies and sweeps Copper modulo
writes through the first row boundary. The second row starts at
`$00016018` for source A on line 64 in WinUAE 6.1 beta 23. CopperScreen
used `$0001600A`, its original two-byte modulo. A write after the first
boundary on line 76 leaves the second row at `$0001640A` and advances
the third row to `$00016422`. The reference shows the same distinction for
destination writes. The correction matches all 384 transferred addresses
in order. The earlier build differed at 104. The `-LiveModulo BC` sweep
writes BLTBMOD and BLTCMOD with all four DMA channels enabled. It matches
all 768 transfer addresses in order.

The sequencer accepts new CPU modulos for subsequent address requests.
Copper writes pass through a two-CCK delay that can retire early when the
blitter loses a slot. Each accepted transfer keeps its modulo through RAM
output. Four regressions verify a live update for every area channel.
[WinUAE register writes](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L3737-L3776)
[WinUAE blitter modulo processing](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1492-L1528)

The Release build completed with zero warnings and engine diagnostics
passed 1,444 tests with seven optional tests skipped. P.P. Hammer matched
all 25 captures and all three final fingerprints on the PAL OCS profile.
The 11,250-field Zool 2 replay passed the original code wheel, reached
Swan Lake Stage 1 and matched all 39 captures and all three final
fingerprints from the previous build. It reported no unsupported feature.

### Live area minterms and fill carry

An A-to-D probe starts with BLTCON0 `$09FF` and moves `$0900` into it
during a blit. On line 82 in WinUAE, the Copper writes at `$3A`. D writes
the already calculated `$FFFF` at `$3B` and changes to `$0000` at `$3F`.
CopperScreen previously kept writing `$FFFF` through the last word. The
area calculation now reads the new minterm on its next calculation and
preserves the word already waiting for the D transfer.
[WinUAE control writes](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L3778-L3794)

The expanded area probe tests shifted ABCD and descending fill as well
as A+D. Exclusive fill XORs each input bit with the incoming fill carry
before the input bit toggles the carry. With carry clear, an exclusive
fill of `$5000` therefore produces `$3000`. The previous CopperScreen
implementation produced `$6000`, removing the leading edge of the fill.
An active full BLTCON0 write also reloads carry from FCI. BLTCON0L retains
the current carry. At line 130, full and low writes leave the fifth D word
at `$FFF2` and produce `$7555` and `$8AAA`, respectively, for the sixth.
[Commodore Hardware Reference Manual, Blitter Hardware](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_1986.pdf)
[WinUAE fill table and carry setup](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1115-L1137)

The generator's `-LiveMinterm Alternate` mode alternates full and low
writes on PAL AGA. `-LiveMinterm Full` selects full writes and can be
combined with `-Ocs` for a PAL OCS boot. The AGA alternate, AGA full and
OCS full replays matched all 576 destination addresses and values in order
against WinUAE 6.1 beta 23. The ordinary area writes have a consistent
three-CCK beam offset between the traces. Before the fill slot correction
below, the offset in the fill cases ranged from one to three CCKs.

Both the candidate and its upstream control, `e573375`, reached Swan Lake
Stage 1 on the original disks. The 11,250-field replays matched all 39
captures and all three final fingerprints and reported no unsupported
feature. Compared with the earlier modulo-only build, the upstream
control already differed by one pixel column at the right display edge.
The PAL OCS P.P. Hammer replay matched its 25 captures and all fingerprints.
The Release build passed with zero warnings and 1,458 engine diagnostics
passed with seven optional tests skipped. An exploratory 14,400-field
Zool 2 replay reached a wall farther into the stage with a score of 670.

### Descending fill destination slots

With D enabled and C disabled, fill inserts an idle slot after the
ordinary D output. CopperScreen previously waited through that slot
before writing D. In the width-three A+D probe, WinUAE reads the last A
word at `$34` and writes the preceding D word at `$35`. It raises the
interrupt at `$35` on OCS and `$37` on AGA and writes the final D word at
`$38` on both profiles. After aligning the BLTSIZE writes, only the
ordinary D writes were one CCK late in CopperScreen.

The sequencer now places the idle phase after the ordinary D write and
keeps completion tied to the last phase. The area generator's `-Fill`
probe with widths one and three matched 124 DMA address and interrupt
events relative to BLTSIZE on PAL OCS and AGA. The five cases exercise D,
A+D, A+C+D, A+B+D and A+B+C+D. The 576 live-control probe writes also
matched in data, address and relative timing after this change.
[WinUAE fill idle phase selection](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1145-L1155)

The 12,850-field Zool 2 replay reached the same wall in Swan Lake Stage
1 and matched all 66 captures and all three final fingerprints from the
preceding minterm build. The 7,200-field P.P. Hammer replay matched its
25 captures and all three fingerprints. Both runs completed with zero
unsupported features reported. The Release build completed with zero warnings
and 1,460 engine diagnostics passed with seven optional tests skipped.
The build, diagnostics, 124 timing events and both game replays were
repeated after rebasing onto the Copper68k 1.5.11 package update. All game
captures and fingerprints matched the run with the earlier package.

### Manual area data writes

The D-only `-LiveData` probe changes BLTADAT, BLTBDAT and BLTCDAT
during a blit with source DMA disabled. Before the correction,
CopperScreen differed from WinUAE in 163 of 192 destination words
on AGA. In the first shifted A case, the third D word was `$9579`
in WinUAE and `$8246` in CopperScreen. The A and C writes now feed
subsequent area calculations. A B write or DMA read shifts the data
at load time, and the area calculator reuses the held, shifted B
word until another load. The destination pipeline keeps results
already calculated before the register write.
[WinUAE data register writes](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L3715-L3736)
[WinUAE area B shifter and data path](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1411-L1466)

The corrected probe matches WinUAE 6.1.0 beta 23 in all 192 AGA and
192 OCS destination writes, including address, data and relative CCK.
The original ZooL 2 wall replay (12,850 fields, 66 captures), P.P.
Hammer opening replay (7,200 fields, 25 captures) and Banshee first
mission replay (10,500 fields, 36 captures) each match the preceding
mainline in every capture and in the final CPU, hardware and output
fingerprints. Both builds use Copper68k 1.5.11. The Release solution
build succeeds and the diagnostic engine suite passes 1,463 tests
with seven optional tests skipped.

### Live area shifts and fill carry

The `-LiveShift` probe changes A's shift count on A+D blits and B's on
ascending and descending B+D blits. A uses the new count at the next
calculation. B uses it on the next data load and retains the previous
shifted word until that load. Results already calculated remain in the
D pipeline. The `-LiveShift -Fill` variant also changes the initial fill
carry while the descending B+D blit is active. Full BLTCON1 writes
reload the carry from the new FCI bit for subsequent calculations and
row boundaries.

Generate the disk with the [area probe script](../../scripts/generate-blitter-area-probe.ps1).
Use `-Ocs` for a PAL A500. In WinUAE, enable the DMA trace with
`DBG v -4` and capture lines 64 through 130 in six-line steps with
`DBG vl <line> 30 7`.

Before the correction, the AGA probe differed from WinUAE in 148 of 192
writes. The corrected engine matches WinUAE 6.1.0 beta 23 on 192 AGA
and 192 OCS shift-probe writes and all 192 writes in the AGA fill
variant, including destination addresses, data and relative CCK. The
576 preceding live minterm probe writes and 384 live data probe
writes also retain their reference results.
[WinUAE control update](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1045-L1144)
· [WinUAE area data path](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1411-L1466)

The 12,850-field ZooL 2 wall replay, 7,200-field P.P. Hammer opening
replay and 10,500-field Banshee first mission replay matched every
capture and the final CPU, hardware and output fingerprints from the
source-data correction. The Release build passed with zero warnings.
Engine diagnostics passed 1,467 tests with seven optional tests skipped.
The live-shift build also matched all 48 captures and final fingerprints
from the verified 14,000-field Lemmings 3 Shadow replay and all 40
captures and final fingerprints from the preceding 11,600-field
RoboCop 3 driving replay with the dongle enabled.

### Register writes in the original game

A separate diagnostic build traced writes to `$DFF040` through `$DFF074`
while the blitter was active during the 12,850-field original ZooL 2
wall replay. It recorded 30 CPU writes to BLTAPTH and 30 to BLTAPTL
while BUSY was set. Those were the only writes in the traced range
during active blits. The first pair appeared at field 1,300, raster
line 29, at CCKs 165 and 170. The traced replay reproduced the Release
build's CPU, hardware and output fingerprints.

The original code at `$0003ADF0` writes the longword `$000532B6` to
BLTAPTH and BLTAPTL after writing `$8818` to BLTSIZE. The code bytes
at that address also match the WinUAE memory dump from the original
disks.

### Pointer writeback during an accepted area transfer

A WinUAE 6.1 beta 23 watchpoint stopped on the `$32B6` write to
BLTAPTL. The CPU write overlaps the address phase of an A transfer
from `$000532C0`. WinUAE then reads `$000532C2` and `$000532C4`.
CopperScreen also read `$000532C0` first, but then read `$000532B8`
and `$000532BA` because it added the increment to the new pointer
value from the CPU. The DMA output phase now calculates pointer
writeback from the address accepted for the transfer, including the
latched row modulo. The same rule applies to A, B, C and D.

After aligning the two original-game traces to the low-word write,
the corrected engine matches the 21 shared A/D transfer addresses
and destination words. The first 12 transfers also match relative
CCK. Later transfers in that window still have offsets of one CCK
at some positions. The CPU traces show a fetch-order difference
around the write. WinUAE fetches `$0003ADF8` and `$0003ADFC`
before the low half of the write. CopperScreen fetches them 5 and
10 CCK after it. Four diagnostic cases cover writes between the
address and output phases for the four area channels.
[WinUAE area DMA pointer writeback](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1669-L1684)

The Release build completed with zero warnings. Engine diagnostics
passed 1,473 tests with seven optional tests skipped. The original
ZooL 2, Banshee, P.P. Hammer, Lemmings 3 and RoboCop 3 replays
matched all final CPU, hardware and output fingerprints from the
preceding build. The five ZooL 2 captures through field 12,850
also matched byte for byte.

### EC020 fetch order around the pointer write

A standalone A1200 boot probe repeats the `MOVE.L` and the following
`ADDA.L` with custom DMA disabled. WinUAE fetches both successor instruction
longwords before writing the custom-register halves, with CACR enabled and
disabled. A direct trace of the pinned Copper68k 1.5.11 A1200 core on a
logging bus placed the operand write before either successor fetch.

The shared core now fetches aligned instruction longwords in RAM and ROM
for the A1200 profile and holds both words even with CACR disabled.
Immediate moves to memory advance the existing instruction queue before
writing the operand.
A second WinUAE boot probe uses `MOVE.L #$000532B6,([$100,A5])` with the
cache off. The base-displacement longword is fetched at CCK `$08`, the two
successor instruction longwords at `$0A` and `$0C`, the pointer at `$0E`,
and the two custom-register halves at `$10` and `$12`. The correction
preserves that order by advancing the queue after decoding the full
extension and before dereferencing the pointer. Six regression cases
cover aligned fetches, cache control, all three operand sizes, and this
memory-indirect ordering. All 3,125 tests in the focused EC020 suite passed.
[WinUAE displaced MOVE handler](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/cpuemu_21.cpp#L11620-L11643)
· [WinUAE indexed MOVE handler](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/cpuemu_21.cpp#L11902-L11923)
· [Motorola MC68020 and MC68EC020 user’s manual](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf)

A local Release build of the shared core completed the original ZooL 2
Swan Lake wall-climb replay through field 15,150. The timing change
selected a different code-wheel challenge, so the replay supplies the
corresponding original response and resumes joystick input after loading.
The final frame shows the wall climb and all HUD counters. Banshee reached
first-mission combat at field 10,500 and Lemmings reached the scrolling
Shadow stage at field 14,000. Both 68000 controls, P.P. Hammer and RoboCop 3,
retained their CPU, hardware and output fingerprints. The PAL and NTSC
CD32 Sqrxz boots both showed the v1.2 title at field 1,425. None of these
replays reported an unsupported feature.
[CopperMod source correction](https://github.com/ilehtoranta/CopperMod/commit/4ce0a14420d22f4c49b6225022a971244c2561d5)

CopperScreen currently pins Copper68k 1.5.11. The Release solution build
passed with zero warnings. Engine diagnostics passed 1,473 tests and
skipped seven optional native cases. The existing A1200 timing policy
still approximates general instruction overlap and bus phases. Physical
A1200 timing remains unverified.

### Live B, C and D channel investigation

A separate synthetic AGA test reproduced a live channel discrepancy.
With a Copper switch from A+D to A+B+D during the blit, WinUAE schedules
9 to 12 B reads depending on the write cycle. CopperScreen schedules
zero. When the Copper clears D during an A+C+D blit, WinUAE produces
1 to 3 destination writes across the sweep and CopperScreen produces
all 16.
[WinUAE channel selection and shifter](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1220-L1373)

In the isolated B sweep, enabling B adds 7 to 10 B reads while
retaining all 16 destination writes. Disabling B at two of the
tested phases leaves 4 or 5 destination writes. The Copper stayed
in a blitter-finished WAIT after a stalled blit and advanced after
a completed one. Enabling B at another phase
during fill can produce C DMA reads even with USEC clear. These
events give separate tests for changes to the active channel
selection, the phase shifter and BUSY.

### Live A source enable

The `-LiveAEnable` probe changes BLTCON0.USEA in both directions
on D-enabled area blits and covers enabling A with C also active.
CopperScreen previously kept the A enable bit from BLTSIZE, causing
all 12 AGA cases to diverge from WinUAE. USEA now controls whether
the next A slot reads Chip RAM. A read accepted in the address
phase completes at the output phase even if USEA changes in between.

The corrected engine matches WinUAE 6.1.0 beta 23 in 374 transfers
on AGA and 374 on OCS. The comparison covers source and destination
addresses, destination words and relative CCK. The earlier shift,
data and minterm probe sequences also retain their reference results.

The original ZooL 2 wall, P.P. Hammer opening and Banshee first
mission replays matched all 66, 25 and 36 captures, respectively,
and all final fingerprints from the live-shift build. The Release
build completed with zero warnings and the engine diagnostic suite
passed 1,469 tests with seven optional tests skipped.

Live B, C and D changes, source-only schedules, line-mode data
writes, absolute Copper start timing and physical signal measurements
remain open.

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
Later rooms remain unverified.
[WinUAE P.P. Hammer history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19736-L19741)

### Four-plane chipset contention

Another WinUAE changelog entry identifies bitplane DMA taking blitter slots
as a source of rendering glitches in P.P. Hammer. The captured opening-room
Copper list uses four lores planes, with DDFSTRT `$38` and DDFSTOP `$D0` for
one display region. In fields 5,401 through 7,200, BLTPRI stays set across
all 327,856 recorded A+D blit starts.
[WinUAE bitplane contention history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19206-L19210)

The [area probe generator](../../scripts/generate-blitter-area-probe.ps1)
reproduces that display and runs eight-by-eight word blits while the 68000
reads Chip RAM. Use `-Ocs -FourPlanes -CpuPressure -Width 8 -Height 8
-WaitPosition 0x47 -BlitterPriority`. CopperScreen and WinUAE 6.1 beta 23
match all 128 A+D and 256 A+B+C+D transfers by address and CCK. The traces
use the Copper BLTSIZE write to align the horizontal origin.

Remove `-BlitterPriority` and add `-Modes 9` to isolate a shared-bus A+D
blit. The CPU prefetches match before the first arbitration difference.
An instrumented build of the same WinUAE revision shows that startup
holds at CCK `$51` when the waiting CPU earns the next slot. With the
extra startup clock, WinUAE grants slot `$56` to the CPU read at `$40000`
and fetches the first blitter A word at `$58`. The old CopperScreen engine
advanced startup at that arbitration boundary and fetched A at `$56`.
Area startup now uses the same CPU-yield rule as active transfers. A
regression starts the blit while the Copper and bitplanes keep the CPU
waiting and verifies the first A transfer with both priority settings.

After the correction, all 128 shared A+D transfers match WinUAE in the
isolated and full probes. The isolated shared A+B+C+D mode (`-Modes 15`)
also matches all 256 transfers.
[WinUAE blitter startup arbitration](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1741-L1778)

### Source-only C blits

In the full shared probe, C, A+C and A+B+C ran slow because CopperScreen
inserted an idle phase after C when D was disabled. An isolated 3-word C
blit (`-Modes 2 -Width 3 -Ocs -BlitterPriority -WaitPosition 0x47`)
shows three WinUAE C reads at CCKs `$53`, `$55` and `$57`, with BLIT IRQ
at `$56`, the input phase of the final read. The area pipeline now ends
each source-only word on the C slot, tests the freshly read data for the
zero flag, and clears BUSY while the last C output is still pending.

The source-only correction matches all 384 transfers in C, A+C and A+B+C,
and 127 of 128 in B+C. A+B+C+D matches all 256 through the scanline boundary
after the earlier source-only blits.
[WinUAE area shifter stages](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1120-L1152)

The source-only correction retained the CPU, hardware and output
fingerprints of all five original game replays on the preceding startup
build. The Release solution built cleanly, and engine diagnostics passed
1,479 tests with seven optional cases skipped.

### B-only area startup

The B-only case had one fewer startup clock than the other area blits.
In the isolated four-plane probe (`-Modes 4`), both engines reach the
same BLTSIZE write, but the earlier startup schedules a B read at CCK
`$58` where WinUAE reads at `$5A`. Keeping the standard three startup
clocks matches all 64 isolated B reads. A focused test also measures the
first B read and subsequent spacing on a quiet bus.

With all three corrections, the full shared-bus probe matches WinUAE
across all 2,048 source and destination transfers, covering all channel
selections that transfer RAM data in this four-plane setup.
All five original replays retained their prior fingerprints. Sqrxz still
reached its title on PAL and NTSC CD32. The Release solution built cleanly,
and engine diagnostics passed 1,480 tests with seven optional cases skipped.

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
fixed at the right edge. [WinUAE Lemmings history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19933-L19936)

### Terminal bitplane modulo timing

The [bootable probe generator](../../scripts/generate-bitplane-modulo-probe.ps1)
sets up two 64-bit lores planes and sweeps Copper writes to BPL1MOD and
BPL2MOD across the terminal fetches on 14 raster lines. Capture each line
and its successor in cycle-exact WinUAE 6.1 beta 23 on the PAL A1200 profile.
`DBG vl 64 48 6` starts the DMA trace at line 64 and horizontal position
48. Collect the trace through line 117.

WinUAE samples the old modulo when the Copper writes the new one one CCK
before the terminal RAM access. BPL2 on line 96 uses `$0008`, producing
the next line's first address `$00016418`. BPL1 on line 112 uses `$0000`,
producing `$00018010`. CopperScreen sampled modulo at RAM output and
produced `$00016480` and `$00018030`, respectively. Capturing the signed
modulo alongside the accepted DMA address fixes both cases. The candidate
matches all 112 bitplane transfer addresses after aligning each engine's
trace to its first bitplane transfer. Four automated cases verify the old
and new modulo on the two sides of the address-phase edge. No physical
chipset measurement was taken.
[WinUAE bitplane address and modulo pipeline](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L9248-L9272)

On the original disks, the 14,000-field Shadow replay selected the masked
Lemming in the Ark and reached the opening city level. Moving the cursor
to opposite edges scrolled the city in both directions while the skill bar
stayed fixed. All 48 captures matched the pre-fix replay, including fields
11,700 and 12,300 on either side of the first scroll and field 13,800 after
scrolling back. The replay ended with CPU `0x260E5FABF29EFBAE`, hardware
`0x425D28FE61A7129B` and output `0x1A80A903249080BB` and no unsupported
feature report. The Release build passed with zero warnings and the engine
diagnostics passed 1,436 tests with seven optional tests skipped. Repeating
the build after the AGA custom register read corrections in `9b383f0`
also passed with zero warnings and 1,440 diagnostic tests passed. The same
Shadow replay matched all 48 captures and all three fingerprints.

The 7,200-field PAL OCS P.P. Hammer replay matched all 25 captures and
all three final fingerprints from the earlier area blitter build.

WinUAE 6.1 beta 23 also scrolled the opening Shadow level to the right and
back with the skill bar fixed. After alignment using the same coordinates as
the Classic level, the initial left, right and return views matched 125,446,
124,450 and 125,302 of 128,000 RGB samples respectively. The captures came
from different fields with different Lemming positions, pointer positions
and countdown frames.
Later stages and the timing of the game's actual Copper list writes remain
unverified.

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
