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
The complete shared-core suite subsequently passed 5,528 tests and skipped
109 optional cases.
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

### Live B, C and D channel changes

A separate synthetic AGA test reproduced a live channel discrepancy.
With a Copper switch from A+D to A+B+D during the blit, WinUAE schedules
9 to 12 B reads depending on the write cycle. The previous CopperScreen
engine scheduled zero. It also wrote all 16 destination words when
the Copper cleared D during an A+C+D blit, where WinUAE wrote 1 to 3.
[WinUAE channel selection and shifter](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/blitter.cpp#L1220-L1373)

Changing the channel configuration while an area blit is active can
insert or remove shifter stages. Inserting B can send the A pulse
into both B and the following stage. When A and B later select their
registers on the same clock, the combined selection reads C even
with USEC clear. Removing B can discard the only pulse and leave BUSY
asserted. A fixed channel sequence remains sufficient until a
register write changes the active schedule. After that write, the
engine advances the live stages on an accepted input and advances
the two-clock destination latch even when that input stalls.
An accepted DMA transfer retires with its original address and value.

PAL AGA and OCS boot probes matched cycle-exact WinUAE 6.1 beta 23
in 2,062 transfers across 72 cases. The comparison covers channel,
address, destination word and CCK. The probes switch B at four
Copper timings with and without A or C reads, switch C and D in both
directions, and exercise descending fill. The fill fixture initializes
the C buffer used by an overlapping A and B selection. Engine tests
cover the C transfer with USEC clear, BUSY after the loss of the last
pulse, recovery by a new BLTSIZE and retirement of a D write after
USED clears. A Copper blitter-finished WAIT also produced the same
pass and stall result as WinUAE in two separate boot probes.

Use the [bootable probe generator](../../scripts/generate-blitter-area-probe.ps1)
with `-LiveChannels B`, `BWithC`, `BWithoutA`, `BWithFill`, `C` or `D`.
Add `-Ocs` for the PAL A500. With the WinUAE DMA debugger enabled,
`DBG vl 64 30 8` captures the first case. Repeat at lines 70 through 106
in six-line steps. Each case starts a four-word by four-row blit and
changes BLTCON0 once after startup.

The Release solution build completed with zero warnings. Engine
diagnostics passed 1,483 tests and skipped seven optional native cases.
The original ZooL 2 Swan Lake replay at field 12,850 and the P.P. Hammer,
Banshee, Lemmings 3 and RoboCop 3 replays retained all 13 captured frames
and all final CPU, hardware and output fingerprints. Sqrxz reached its
title at field 1,425 on PAL and NTSC CD32 with identical images to the
preceding build.
Arbitrary active BLTSIZE writes, active BLTCON1 mode changes and
physical signal timing remain open.

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

Uncovered source-only schedules, line-mode data writes, absolute Copper
start timing and physical signal measurements remain open.

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
[WinUAE P.P. Hammer history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19736-L19741)

### Castlemaster and the ice room

The same original SPS 2809 disk loaded level 2, Castlemaster, in
CopperScreen `f13a116` and cycle-exact WinUAE 6.1 beta 23. The replays
used the game’s name and password prompts with player name `HAMMER`
and password `UDICGRAJ`. The CopperScreen input starts the name at field
2,950 and the password at field 3,220, with keys 14 fields apart.
Port 2 fire advances the prompts at fields 2,750, 3,050 and 3,400,
then starts play after disk loading.
[Published level codes](https://manuals.plus/m/20b7a97d11212a193646ac40a312191e3e24a6a49630ad30e22bf2e0d259aa58.pdf)
· [Amiga Computing correction for the player name](https://amigaland.com/dataz/press_magazine/Amiga_Computing/PDF_TXT/Amiga_Computing_Issue_040_1991_Sep.pdf)

At CopperScreen field 5,700, 86,638 of 86,664 playfield RGB samples
match the independent WinUAE capture using the opening-room coordinate
mapping. The status region matches 13,426 of 13,816 samples at rows
58 through 79. Animation positions and countdown values differ between
the captures. The 7,200-field CopperScreen replay moved through the
room and collected a pickup, raising the score to 10. It ended with
CPU `0xC0D4505B618D84E1`, hardware `0x3DF27FC578695A57` and output
`0xFC7AECABC09D609D` and reported no unsupported feature.

Changing the password to `RVJBTFHH` loaded level 30 in both emulators.
At CopperScreen field 5,700, the ice room matches 86,242 of 86,664
playfield samples and 13,370 of 13,816 status samples in an independent
WinUAE capture at a different countdown value. The CopperScreen replay
then moved and jumped through the room while the camera scrolled. Its
final field 7,500 fingerprints were CPU
`0xCA10C5947634AF96`, hardware `0x56905E1DA6F54047` and output
`0x0EDAD125AA9D132E`. It reported no unsupported feature. Other rooms
and physical A500 timing remain unverified.

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

### Live DDFSTOP writes

The [bootable bitplane probe](../../scripts/generate-bitplane-modulo-probe.ps1)
with `-DdfStopSweep` sets up two lores planes and changes DDFSTOP between
`$0048` and `$0070` at six points in either direction. In cycle-exact
WinUAE 6.1 beta 23, moving the stop to `$0048` near the comparator edge
can miss the new position and run to the hard stop. Moving it away from
`$0048` at the same edge can leave the shorter run intact. The previous
CopperScreen engine used the new register value too soon in four of the
twelve cases on each chipset.
[WinUAE DDFSTOP write timing](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L3678-L3686)
· [WinUAE stop comparators](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L9434-L9445)

The bitplane sequencer now observes the change after the comparator
write delay. The PAL A500 and PAL A1200 probes each match all 210 DMA
transfers by plane, address, width, and relative CCK. Capture with
`DBG vl 64 32 13` and repeat every four lines through line 108.
Four automated cases check the last pointer after moving the stop
earlier or later on either side of its sampling edge. A separate
mid-line FMODE sweep between 16- and 64-bit fetches matched all
132 transfers on the existing engine.

The Release solution build completed with zero warnings. Engine diagnostics
passed 1,497 tests and skipped seven optional native cases. The original
ZooL 2, Banshee, P.P. Hammer, Lemmings 3 and RoboCop 3 replays retained all
13 reference-build frame captures and all final CPU, hardware and output
fingerprints. Sqrxz reached its title on PAL and NTSC CD32.

### Live DDFSTRT writes

The [bootable bitplane probe](../../scripts/generate-bitplane-modulo-probe.ps1)
with `-DdfStartSweep` changes DDFSTRT between `$0038` and `$0050` at six
points in each direction. It uses two lores planes and distinct bitplane
addresses per test line. In stock WinUAE 6.1 beta 23, a change near the
start position can prevent the old and new values from comparing on the
same CCK. The previous CopperScreen engine issued 16 extra reads on
each of lines 76 and 80 and missed six reads on line 104 on both chipsets.
[WinUAE DDFSTRT write timing](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L3668-L3677)
· [WinUAE AGA start comparator](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L9418-L9433)
· [WinUAE OCS start comparator](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/custom.cpp#L9558-L9576)

The sequencer publishes the new DDFSTRT comparator four CCKs after the
write and suppresses the comparison on the CCK immediately before
publication. Adding the delay alone produced six extra reads on line 100.
With both edges modeled, the PAL A500 and PAL A1200 each match WinUAE in
all 12 cases, covering 120 DMA transfers per chipset. The plane, address,
width and CCK agree after applying a constant three-CCK offset between
trace origins. Capture with `DBG vl 64 32 13` and repeat every four lines
through line 108. Five automated cases check the final bitplane pointers
when a write moves the start earlier or later around the sampling edge.

The Release solution build completed with zero warnings. Engine diagnostics
passed 1,502 tests and skipped seven optional native cases. The five original
game replays retained all 13 captures and final CPU, hardware and output
fingerprints. Sqrxz retained both title captures and final fingerprints on
PAL and NTSC CD32.

### DDFSTRT rewrites of the same value

Run the same probe with `-DdfStartSweep -RepeatDdfStartValue` to rewrite
the initial value at each of the 12 positions. A Copper MOVE of `$0038`
on line 100 suppressed the start comparator in both PAL WinUAE profiles.
CopperScreen previously skipped writes that left the value unchanged and
issued 16 extra reads on that line. Every DDFSTRT write now participates in
comparator timing. The corrected trace matches all 140 DMA transfers per
chipset by plane, address, width and CCK. Three more regression cases cover
rewriting `$0038` before, on and after the compare edge.

The Release solution build completed with zero warnings. Engine diagnostics
passed 1,505 tests and skipped seven optional native cases. The five original
game replays and the two CD32 region checks retained their captures and final
fingerprints. The same checks passed again with Copper68k 1.5.12.

### Egyptian opening level

CopperScreen `9b3e8e4` selected the Egyptian Lemming in the Ark and reached
the opening temple on the original four IPFs. The 12,600-field replay
skipped the title at field 5,450, moved the mouse by a total of `(392, 188)`
at fields 8,000 through 8,030 and clicked the Egyptian at field 8,200.
The replay drew the moving Lemmings and fixed skill bar and reported no
unsupported feature. WinUAE 6.1 beta 23 also reached the temple after
mounting the requested Egyptian volume, disk three, in DF0.

At field 12,600, the CopperScreen playfield matched 100,230 of 103,040
RGB samples and the skill bar matched 24,238 of 25,600 samples in the
independent WinUAE capture. The comparison samples even x values from
516 through 1,794, playfield rows 70 through 230 and skill-bar rows 231
through 270. The reference coordinates are `(x - 400) / 2` and `2 * y - 54`.
The two captures show different Lemming positions, pointer positions and
timer states. Both engines report BPLCON0 `$5201`, DDFSTRT `$0028` and
DDFSTOP `$00C8` at the measured level checkpoints.

The capture SHA-256 hashes were
`95E4FE5A9FC594C1501C3219BD4A1813E7DA5B416A2024878728F8FDE0600AC4`
for CopperScreen and
`9F8ACA04C5941DF58317B9DEB82F179A1F877FAFA43E962F55837D707222DC7A`
for WinUAE. These checks cover the opening level of each of the three
tribes and the previously recorded Shadow scrolling route.

### Egyptian level 2 and scrolling

A replay on CopperScreen `2b47027` used the spade twice on the opening
level, first digging diagonally left and then down through the upper
platform near the exit. The result screen at field 15,304 reported all
20 Lemmings saved. Selecting Play Next Level continued loading from the
original Egyptian disk in DF2 and reached level 2 by field 18,184.
[Game manual for the spade and level controls](https://db.hfsplay.fr/files/2018/10/06/c9f24f28-42dd-4046-aa9b-1d0b7622142e.pdf)

Moving the mouse against the edges scrolled the second level to the
right by field 18,614 and back to the opening position by field 19,474.
The skill bar stayed fixed and Lemmings continued moving during the
scroll. The Release runner with the DDFSTOP correction reproduced the
level 2 frame at field 18,184 byte for byte. The capture SHA-256 is
`F6C3E0620B0DB1D217F2DBFFDE81EEA45B19534F1E25D5728FCD5657A7038E42`.
With the DDFSTRT correction, a fresh Release replay from boot reproduced
the field 19,474 framebuffer and Chip RAM byte for byte. The framebuffer
SHA-256 is `217334EC8B5364207DEF2850A6C28B5387432AA5E339792B74B60578920DA483`.
The same-value write correction reproduced that capture, Chip RAM, audio
and all three final fingerprints in a second boot replay.
Independent comparison of the second level remains open.

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

### Arcade gyropack trials

The original three-disk release selected F4 at the Arcade Action headlines
and entered the gyropack trial by field 10,500 on CopperScreen. The HUD showed
the objective, altitude, thrust, heading and cannon ammunition. A second
replay switched to the external camera and exercised the pitch and bank
controls. It completed 11,525 fields with CPU fingerprint
`0x30E5AB522869C032`, hardware `0x38467D30EA168A75` and output
`0x1B0E7D2987B48C9F`. WinUAE reached the same headline and the disk C cutscene.
The independent flight comparison remains open.
[RoboCop 3 manual](https://www.gamesdatabase.org/Media/SYSTEM/Commodore_Amiga/Manual/formated/Robocop_3_-_1992_-_Ocean_Software_Ltd..htm)

## Secondary compatibility checks

### Sly Spy: Secret Agent

The PAL A500 check uses the two original Ocean disks, SPS 1259. The ZIP
SHA-256 is `BB3565CDF20CDDEA1DBF2DFF7B4AE6C4C2FBACDF834A05CC3E07F37248BBD1B4`.
The WinUAE 6.0.3 changelog associates this title with a DDFSTRT update
while the previous value matched.
[WinUAE changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L201-L205)

CopperScreen `3c98591` with Copper68k 1.5.12 passed the credits, briefing
and opening skydiving stage. The replay pulsed port 2 fire for 12 fields
every 450 fields from field 4,050 through 7,650. The game accepted the
default agent number, scrolled the sky and awarded 400 points. At field
8,000 it reported BPLCON0 `$5000`, DDFSTRT `$0038`, DDFSTOP `$00D0`
and no unsupported feature.

An independent cycle-exact WinUAE 6.1 beta 23 capture also showed 400
points in skydiving. All 29,256 RGB samples of the equipment panel matched.
The full HUD matched 58,620 of 58,880 samples with game timer and animation
at different phases. The equipment panel comparison samples CopperScreen
x=580–897, y=207–298 at WinUAE coordinates `x - 198`, `2 * y - 54`.
The source capture SHA-256 hashes are
`4AB3F3545CB7CEA1F26570B129001704B53D4BE1B55EC40A0B4BCD56154836CD`
for CopperScreen and
`EF78228DECE8DB2C001DC0F48F05F1D0AC56C89F5D04C4ABA69725921EFF7E12`
for WinUAE. A longer CopperScreen replay used directional and fire input,
scored 800 and drew the deployed parachute over the city at field 10,400.
The specific rasterline behind the historical report and later stages remain
unverified.

### Katakis

The PAL A500 check uses Rainbow Arts Katakis v1.1, SPS 0347. The archive
SHA-256 is `E305FC1207791952D5E1E0BF28281421C4A2F7F40C4EE004957154CEECB5731E`.
At the title's insert-disk prompt, move disk two to DF0. The replay mounts
it at field 4,200 and reaches the opening gameplay at field 6,800. WinUAE
6.1 beta 23 also boots past the prompt with disk two in DF0.
[UAE changelog on Katakis and exception 3](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/ChangeLog#L2245-L2248)

The loader executes `TST.L (A6)` at `$0007F016` with the odd address
`$00DFF001`. CopperScreen with Copper68k 1.5.12 fetches vector 3 during
field 4,421 and enters the same `$000000C0` handler as WinUAE. The seven
stack words at `$00C7FFEC` match exactly in both engines:
`4A95 00DF F001 4A96 2004 0007 F018`. They record the fault address,
opcode, status and saved PC `$0007F018`. The comparison reads the stack
words directly; WinUAE's one-line exception log reports `$0007F018` in
its fault-address field for this case.

All 4,851 RGB samples in the high-score panel match the independent
WinUAE capture at CopperScreen x=472–702, y=246–266 and reference
coordinates `x - 198`, `2 * y - 54`. The other gameplay objects were
captured at different times. The address-error route required no code
change. Later stages and physical timing remain open.

### Days of Thunder

The PAL A500 check uses the original Mindscape disk, SPS 0285, with archive
SHA-256 `48073E1FFD0EE3CE79387C8FF620F8446B4F9BF8706C83AA7D627B0DE745AAEB`.
The WinUAE history identifies an incorrect CHK exception return address
in this game. With Copper68k 1.5.12, CopperScreen booted through the
credits to the options screen at field 5,500. All 128,000 RGB samples of
the options screen matched stock cycle-exact WinUAE 6.1 beta 23 over
CopperScreen x=258–897 and y=44–243 using WinUAE coordinates
`x - 198`, `2 * y - 54`.
[WinUAE CHK history](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19712-L19723)

Selecting Go to Trials reached the qualifying cockpit on both engines.
The CopperScreen replay pulses fire for 15 fields at 2,600, 3,100,
3,600, 4,100, 4,600 and 5,100. In the options menu it presses down for ten
fields at 5,900, 5,960, 6,020 and 6,080, then up over 6,112–6,117 and
fire over 6,140–6,145.
At field 8,200 all 27,520 samples in the upper trim and upper dashboard
match the independent WinUAE capture using the same coordinates. The
regions cover x=258–897 and rows 44–57 and 174–202. Both engines responded
to the gear and throttle inputs in qualifying.
[Days of Thunder manual](https://www.lemonamiga.com/doc/days-of-thunder/435)

The [bootable 68000 probe](../../scripts/generate-68000-chk-probe.ps1)
checks `CHK.W #5,D0` with operands −1, 6 and 5. The first two take vector 6.
Each writes the saved SR, saved PC and expected resume PC to Chip RAM at
`$00070000`. The third resumes without another exception and writes the
`$BEEF` completion marker. Read 22 bytes in the CopperScreen Chip RAM
capture or run `DBG m 70000 2` in WinUAE. Both produced the same records:

| Operand | Saved SR | Saved PC | Expected PC |
| --- | --- | --- | --- |
| −1 | `$2708` | `$0000158C` | `$0000158C` |
| 6 | `$2700` | `$00001596` | `$00001596` |

The probe requires no CPU correction. The original game's CHK stack frame
and progression from qualifying to the full race remain open.

### Spindizzy Worlds

The PAL A500 check uses the Electric Dreams disk, SPS 1179, with archive
SHA-256 `8E9DB60BCEEB7EF9BF3D7935ECF97DC2B5A4B016378AADB9E018302061612000`.
The WinUAE history groups this title with P.P. Hammer in its blitter
timing changes. Both cycle-exact WinUAE 6.1 beta 23 and CopperScreen
entered the Easy cluster's Ramp Run world. Cluster selection requires
holding the direction through the fire-button release.
[WinUAE blitter changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L19208-L19211)
· [Spindizzy Worlds manual](https://www.lemonamiga.com/doc/spindizzy-worlds/1516)

The replay pulses port 2 fire for 12 fields at 2,400, 3,000, 3,600, 4,200
and 4,800. It holds left at 6,550, adds fire at 6,570, releases fire at
6,600 and releases left at 6,640. A fire pulse at 6,800 enters Ramp Run.
Holding down for 30 fields from 7,220 moves GERALD down the ramp and
scrolls the scenery. The space bar at 7,270 brakes the movement.

At field 7,220, the landscape matched 69,590 of 69,696 RGB samples in
the reference capture. All 106 differences lie within GERALD's animated
sprite area at x=724–751, y=120–134. The right clue panel also matches all
3,131 samples. The comparison uses CopperScreen landscape coordinates
x=290–865, y=92–212 and clue panel coordinates x=760–860, y=215–245,
mapped by `x - 198`, `2 * y - 54`.

The Release replay reproduced the starting and scrolled captures byte for
byte through field 7,360 and reported no unsupported feature. This checks
the opening terrain redraw. Further worlds and independent measurements of
the title's blitter activity remain open.

### Alien Breed 3D

The original Team 17 AGA release, SPS 0624, uses two disks. Its archive
SHA-256 is `A7AFF0D799FB1D0C31483F370B871E2D42F779106EA99A0758D6C3C6028A0E03`.
Both engines use a stock PAL A1200 with 2 MiB Chip RAM and Kickstart
3.0 revision 39.106. The boot disk starts in DF0 and the level disk in DF1.
The guest waits for the level disk in DF0 before asking for the printed code.

On `2bd3ac3`, the original loader remained in its fade at field 8,250.
The loop at `$94B8A` reads bit 5 of the low byte of `INTREQR`, explicitly
acknowledges VERTB, and updates the palette. CopperScreen entered the
level-3 handler early enough for it to clear VERTB before almost every
foreground read. WinUAE 6.1 beta 23 advanced 14 fade steps between adjacent
register captures about 0.31 seconds apart. Its DMA trace shows the
foreground read of `$DFF01F` with bit 5 set before interrupt entry.
[WinUAE 020 interrupt dispatch](https://github.com/tonioni/WinUAE/blob/ee784914c8d61bb503d137e358b22fcc4758a294/newcpu.cpp#L4864-L4884)

The stock AGA 68EC020 path now qualifies the pin at the motherboard clock
and defers entry through the instruction after the pending sample. A second
check at the same CPU boundary cannot advance recognition. The same window
on A1200 and CD32 uses the canonical board clock. The 68EC020 samples IPL
on consecutive falling edges. The implementation approximates that timing
at instruction boundaries. Physical sampling, short pulses and mask changes
around instruction retirement still need qualification.
[Motorola M68020 User's Manual, section 6.1.9](https://www.nxp.com/docs/en/reference-manual/M68020UM.pdf)

The [bootable VERTB polling probe](../../scripts/generate-ec020-vertb-poll-probe.ps1)
has SHA-256 `407EE938D0E127DFADE8882C35BC20EE06D83B1D2CF4B103A6E3D5C82F21E95F`.
The word at `$70000` counts successful foreground polls and the word at
`$70002` counts handler entries. After 400 fields the baseline reported
`$0001,$00F2`, and the corrected Release reported `$00F2,$00F2`. Running
`DBG m 70000 2` in stock WinUAE after boot also gave equal counts. The
board-level automated regression checks both normal and scalar execution.

The corrected Release replay reached the level-disk prompt by field 4,800.
It mounted disk 2 in DF0 at 5,000 and pulsed the left mouse button for 15
fields at 5,200, 5,500 and 6,000. It entered the three digits from the
original codebook at 7,000, 7,020 and 7,040, pressed Return at 7,060 and
pressed Space at 7,350 and 7,650. Each key was released eight fields later.
The first playable room appears at field 7,950. Up at 8,100–8,140 moves
toward the door and Right at 8,300–8,330 turns the view. The 8,500-field
Release replay reported no unsupported feature.
[Original manual](https://amigaland.de/wp-content/uploads/2024/03/Alien-Breed-3D-Manual-DE-ENG-FR-IT.pdf)
· [Original codebook](https://amigaland.de/wp-content/uploads/2024/03/Alien-Breed-3D-Code-book.pdf)

At the opening position, 59,904 of 59,904 RGB samples in the 3D viewport
and 5,456 of 5,456 samples in the visible right border match WinUAE.
The comparison maps the 720×568 WinUAE capture to the raw CopperScreen
field using `(2*x + 400) mod 1816` and `floor(y/2) + 27`. The viewport
covers reference x=186–569 and even y=38–348. The right border covers
x=698–719 and even y=50–544. The left and right HUD samples match
31,736 of 31,744 and 31,482 of 31,496 respectively. The mismatches are
confined to one horizontal row on the left and the animated ammunition
bar on the right. The far-right high-resolution sprite comparator and
later rooms remain open.
[WinUAE far-right sprite changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L6056-L6062)

| Capture | SHA-256 |
| --- | --- |
| CopperScreen opening room, field 7,950 | `4930E1B700D29861FC4BB70EBF40B973A0AACFFFFEC98BB82929DACE38B4CD1E` |
| CopperScreen after walking forward, field 8,250 | `65F3FF6C1B1FE997213DFE36A5016E5965367CC32F914750282F67CD80F85A23` |
| Stock cycle-exact WinUAE opening room | `7BC5B5741FB19BF92EC189E501FA1319BE8F451A67F8D7CF2F454FA67BF4B92D` |

Banshee still reached the first mission with both scoreboard panels. Zool 2
asked for a different code-wheel response and reached the Stage 1 wall
climb after entering that response. PAL and NTSC Sqrxz still reached the
title screen at normal drive speed. The NTSC field-1,425 bitmap matched
the retained replay byte for byte. The Release build passed with zero
warnings and engine diagnostics passed 1,511 tests with seven optional skips.

### Charlie J. Cool

The PAL A1200 check uses the two NRC/Nevada AGA ADFs. Disk 1 SHA-256 is
`82B3539B3F1E662234B141C12F56B6E547671B82DC59B2DE7609F19E35E00504`.
Disk 2 SHA-256 is `58AC029ED8632547AB5E165040343FEB41DFD128A1D01C102B604D90A3418EA4`.
Both engines use Kickstart 3.0 revision 39.106, 2 MiB Chip RAM and disks in
DF0 and DF1. The CopperScreen replay pulses joystick port 2 fire at
1,450–1,465 and 1,720–1,735 to skip the credits and select Start. Both
CopperScreen and stock cycle-exact WinUAE 6.1 beta 23 reach level 1-1.

The historical Alice busy-signal issue is relevant to the observed game
configuration. During gameplay both engines report `BPLCON0=$7000`,
`FMODE=$0000` and `DMACON=$03EF`, which select seven lores planes with
blitter priority clear. A WinUAE DMA capture on raster line `$27` shows
CPU byte reads of the busy flag at `$DFF002` interleaved with A and C
source reads and D writes. The generic final-D and CPU-yield probes in
[Area blitter pipeline](#area-blitter-pipeline) cover the motherboard rule.
[WinUAE Alice busy-signal changelog](https://github.com/tonioni/WinUAE/blob/6ae6fb6b84bb9517e0245a80fc9bdca1a8580dde/od-win32/winuaechangelog.txt#L92-L98)

At field 3,400, all 152,960 playfield RGB samples and all 4,480 scoreboard
samples match the independent WinUAE opening capture. The comparison uses
reference x=58–697 and even rows 60–536 for the playfield and 44–56 for the
scoreboard. CopperScreen coordinates are `2*x + 400`, `floor(y/2) + 27`.
Holding Right at 3,450–3,535 on CopperScreen scrolls past the opening cliff
and onto the platform with the crates. WinUAE also scrolls to that platform.
CopperScreen produces audio and reports no unsupported feature at these
checkpoints. The Release replay reproduced both CopperScreen captures byte
for byte. Later levels and a paired game DMA timeline remain open.

| Capture | SHA-256 |
| --- | --- |
| CopperScreen opening, field 3,400 | `ACB5470F0E283E950F20E4914FEAC7E719F42A8FA0C5FA25A7EB516F068B1DC4` |
| CopperScreen scrolled playfield, field 3,560 | `87491C4346A3A1A14FF5672BF8595C18644D5D402A6CF92871EA1B1CE5101585` |
| Stock cycle-exact WinUAE opening | `E9EAC078CDD36E158B24DBB48E140A881210E876AFE48BA00ED9AD5CB8A99A76` |

### Sierra Soccer: World Challenge Edition

The original SPS 1539 archive has SHA-256
`63085ED808A1284D91BF5DE702B79C84185506B5E8140DE0C95D96A646025C70`.
Both engines use a stock PAL A500 with Kickstart 1.3, 512 KiB Chip RAM
and 512 KiB slow RAM. The Presentation disk reaches the animated title.
Booting the Game disk from DF0 reaches the credits, language selection and
the printed manual's flag check. At field 5,450, 127,968 of 128,000 RGB
samples match stock cycle-exact WinUAE 6.1 beta 23. All 32 differences
are inside the printed page number, which was 39 on CopperScreen and 32
on WinUAE. Both engines accept the flag sequence from the corresponding
page of the original manual. [Archived Sierra Soccer manual](https://web.archive.org/web/20250413170545/https://retro-commodore.eu/files/downloads/Game%20Manuals/Amiga/Sierra%20Soccer%20-%20World%20Challenge%20Edition%20(1994)(Sierra)(M3)%5b600dpi%5d%5bocr%5d.pdf)

The original Game disk reaches the USA versus Brazil friendly on both
engines. Start with Game in DF0 and Presentation in DF1. On CopperScreen,
pulse port 2 fire at field 4,800 and the left mouse button at 5,100 to select
English. For the page 39 prompt, choose Canada, Tanzania and England.
Choose Friendly and Play, then select the USA and Brazil and exit each
roster. The field-10,400 center-circle kickoff matches 142,666 of 142,720
RGB samples in the same region used for the demo below. The 54 differences
are in the referee at reference x=348–361, y=138–158, and around the ball
at x=382–385, y=266–270. Port 2 fire at 10,418–10,430 followed by Right
through 10,465 starts play and scrolls the pitch. WinUAE also responds to
kickoff and movement. CopperScreen reaches 01:56 at field 10,640, with
962 stereo sample frames and nonzero output on both channels at both
checkpoints. The ordinary Release runner reproduces both bitmaps byte for
byte and reports no unsupported feature.

The published Amiga Action #57 disk 1 demo opens an England versus Holland
match on the same PAL A500 profile. The ADF SHA-256 is
`9B8FA4FD8FF8D9AA0FAF2B4910454F64E55893B774D49C4D22D39AB9C06B274C`.
The replay uses joystick port 2 for England. It pulses fire at
5,200–5,215, Right at 5,520–5,527 to select Exit on the team-selection
screen, and fire at 5,540–5,555 to load the pitch. Fire at 6,220–6,230
kicks off. Right remains held from 6,230 to 6,265.
[Amiga Action demo disk record](https://gamesdb.launchbox-app.com/publishers/games/16032-europress-interactive)

The demo menu at field 5,000 matches all 128,000 RGB samples. The
field-6,150 kickoff capture matches 142,566 of 142,720 samples against
WinUAE. All differences are within the two match officials at reference
x=350–385, y=138–166. The comparison samples reference x=60–699 and even
rows 66–464 for the menu and 46–490 for kickoff, with CopperScreen
coordinates `x + 198`, `floor(y/2) + 27`. Both engines respond to kickoff
and movement. CopperScreen reaches 00:56 on the match clock at field
6,460. The Release replay reproduced the kickoff and active-match bitmaps
byte for byte and reported no unsupported feature.

The Release captures contain 962 stereo sample frames at kickoff and
961 during the active match, with nonzero output on both channels. The
initial WinUAE Presentation recording is invalid for a timing comparison.
Its 2,600 recorded PAL fields yielded 5,442,217 stereo sample frames in
a stream labelled 48 kHz, more than twice the samples expected for that
interval. Exact audio timing and play through the final whistle remain open.

| Capture | SHA-256 |
| --- | --- |
| CopperScreen original flag check, field 5,450 | `58ABB7B16EA1A702B55D7786BE45DD67E3AEC5829BCC0E5F7DE8C8A2160F415B` |
| CopperScreen original kickoff, field 10,400 | `EAC1894A824271E79C87126F303AEFEF9F2324A9645E4D24D4B3D6054A285ACC` |
| CopperScreen active original match, field 10,640 | `8BAA1A90252FDCCF61C93437B24E54854973D884B923B02017E5BA9F2CD0CF36` |
| Stock cycle-exact WinUAE original kickoff | `67FBABC2F168269F7BB6E7A4E51DF79F3D0EBDD25E09853ED7599AA006FB41A0` |
| CopperScreen demo kickoff, field 6,150 | `D8C7C69EE49C3966AA3D1C0FE2B3B27C8314E6E491404D207F7AC56A12324D52` |
| CopperScreen active demo match, field 6,460 | `84C1751EF6EEF78AE754B874E2433213DB2E3CC80C2FBA99C954C31DEC28DC25` |
| Stock cycle-exact WinUAE demo kickoff | `F8E8061BA117B983EB975500F3E6B336C491473F59D779CD824D5F73815806DB` |
