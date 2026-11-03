# CopperOS BltClear direct-fence qualification — 2026-11-03

Development bundle `diskboot-2026-11-03-a1200-desktop-single-bank.59` replaces
BltClear's internal WaitBlit vector calls with direct C# hardware fencing. Public
WaitBlit remains available unchanged. The initial Agnus word read and subsequent
BBUSY polling, blitter ownership and completion semantics are preserved.
Shorter count/pattern lifetimes, zero-minterm DMA and an ECS/OCS final-word CPU
tail further reduce setup and fencing. CPU/DMA thresholds are unchanged.
CopperSharp68k recognizes memory BTST and bypasses repeated address loads on
polling backedges without skipping hardware reads. No hand-written assembly,
emulator timing or scheduler change is included.

Median completed public BltClear plus public WaitBlit guest cycles:

| CPU / bytes | Previous .58 | New .59 | Kickstart 40.63 |
| --- | ---: | ---: | ---: |
| M68000 / 768 | 3,180 | 3,128 | 2,196 |
| M68000 / 770, patterned | 3,174 | 3,118 | 2,226 |
| M68000 / 832 | 3,232 | 3,194 | 2,296 |
| M68000 / 4,098 | 10,304 | 10,080 | 8,936 |
| M68EC020 / 770 | 3,190 | 3,134 | unavailable |
| M68EC020 / 832 | 3,320 | 3,264 | unavailable |
| M68EC020 / 4,098 | 10,348 | 10,148 | unavailable |

Two 572-row runs agree exactly, with output/guard/register checks and a DMA-disabled
CPU control. No row exceeds .58 or .56 by more than the unchanged eight-cycle
PAL bus-phase envelope; maximum increases over .58 are six cycles on 68000 and
two on EC020. The protocol uses three warmups and 21 samples, display DMA off,
IPL 7 and a dense crossover grid. Separate OCS/ECS grids pass on both CPUs.
Kickstart still has lower fixed overhead; its EC020 fixture is unavailable.
These are guest-cycle diagnostics, not host-throughput acceptance or a claim
of faster desktop boot. Full protocol, profiles and rows are in producer
`Docs/NativeBltClearDirect20261103.md` and
`Docs/Measurements/NativeBltClearDirect20261103.json`.

The standard ROM remains **524,288 bytes**, with **522,268 used**, **2,020 padding**,
108 bytes before the footer and checksum FFFFFFFF. The generated clear is
**1,136 bytes**, only **14 bytes larger** than .58. All bodies execute from ROM.

Paired A1200 desktop replays match CPU state, full Chip RAM, pixels, audio,
registers, milestones and scripted Tab input. Readiness remains 12.0792278880542
emulated seconds. Pixel SHA-256 remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

ROM SHA-256: `66E22CFAFBDD3337D66216D46EE3E2087FD6F8D7127E422CAE5AFAA2ADAA26CD`.
Unchanged HDF: `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7`.
The manifest and project hash gate select .59. The frozen .58 emitter composition
and .49 reset input exclude concurrent producer changes. Source/runtime receipts,
profiles, test results and size proof remain in producer
`artifacts/graphics-bltclear-direct-20261103/`; paired captures use that path in
this consumer. ROMs, media and artifacts remain local and uncommitted.

Validation passes 1,051 selected compiler cases and 233 frozen graphics cases,
with 19 optional graphics cases unavailable. Four new native cases verify
internal fencing with public WaitBlit replaced by RTS; all four deliberately
fail against the old .58 emitter. Historical expanded-image and unrelated
ColorMap32 exclusions are unchanged.

Final integration validation passes three working-source image identities,
11 app native desktop/DMA checks and 94 launcher/update checks; eight optional
host cases remain unavailable. Both production solutions build successfully
(12 producer warnings, no consumer warnings or errors). The default consumer
build disables CopperStart; app-native qualification explicitly used
`dotnet build CopperScreen.slnx -c Release -p:CopperScreenEnableCopperStart=true`.
Tests against the disabled build were rejected as configuration failures and
were rerun with the feature enabled. Readback verifies the built ROM, unchanged
HDF and .59 manifest resources. No tests were relaxed to obtain these results.
