# CopperStart Full Contact: combat, defeat and restart — 2026-10-24

The supplied Fairlight Full Contact disks now have a bounded combat and restart
replay with the North & South-qualified CopperStart ROM. The player attacks,
recovers from a knockdown, loses the match, reaches the Top Ten and selects
**Play Again**, returning to a fresh Tong Lo fight. No additional firmware,
CPU or engine correction was established. The earlier
[startup qualification](COPPERSTART_FULL_CONTACT_2026-10-18.md) remains separate.

## ROM and machine

The owning native emitter was rebuilt from CopperStart
`0057fb9c38d82d38122ddf69a4aa960573beedd7` and composed with the retained
476276-byte compiled guest root from
`cf7c9b54aefea723b018bd68e10e829e67684250`. This does not claim recompilation
of that C# root or a fresh full producer solution build. Independent composition
produces identical 524288-byte ROMs with the valid checksum and SHA-256
`fefe13e86ad00e3609ad4e385b5e3752e2407e1b5a9b67affa904d2160d9e8e7`.
The result is byte-identical to the preceding qualified ROM.

The runtime binaries are retained from the merged CopperScreen
`29bc8635fe33ae9e267846ade511e9b368f7158d` build recorded with
[North & South](COPPERSTART_NORTH_SOUTH_2026-10-23.json). The evidence/workload
parent is `71dbe03b52a90c9191008e4f6c7c2d97d26968c6`. Copper68k remains the
pinned unpublished `1.5.5-nitro.1` package. No runtime source changed here.

The machine is PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
Both supplied `[cr FLT]` ZIPs contain standard 901120-byte ADFs. Disk one boots;
the physical script ejects it at field 12400 and inserts disk two at 12450.
There is no media patch, trainer, saved state, guest-memory write from the
harness or host OS service shim. The native comparison uses Kickstart 1.3 on
the same runtime and machine. Exact media, ROM, binary, script and capture
identities are in the [companion JSON](COPPERSTART_FULL_CONTACT_2026-10-24.json).

## Player response and progression

The preserved [Team17 manual](https://evercade.info/wp-content/uploads/2023/03/Full-Contact-Manual-ENG.pdf)
describes directional movement and attacks with the fire button, the game
options and attract mode. This replay explicitly distinguishes player input
from the opponent's automatic behavior.

| CopperStart field | Reviewed observation |
| ---: | --- |
| 3200, 5600, 7200 | Fairlight intro, intact HAM story photograph and title sequence. |
| 12400–16000 | Disk-two handoff, Tong Lo introduction and the first fight. |
| 16000 | Direction/fire input performs an attack. Both ROMs have player health 170 and opponent health 190; the independent CopperStart idle control leaves the opponent at 192. |
| 17000–17200 | Player is down, then the Up input changes action 21 to 1 and lifts the fighter from Y=114 to Y=109 and then 63. The corresponding full rasters show recovery and jumping. |
| 18500–20000 | Further combat, defeat and a fresh attempt. Health resets to 192 at the next introduction. |
| 24000–26000 | The recorded attempts end in defeat; Game Over is followed by the Top Ten. |
| 28000–28100 | Down changes the highlight from Exit to Menus to Play Again; fire selects it. |
| 29000–32000 | Tong Lo's portrait, introduction and a fresh player-controlled fight. |

The health/action values above are read-only observations of this release's
guest data, interpreted alongside its instructions and rendered frames. They
are not new public engine APIs or acceptance fingerprints. The independent
20000-field idle replay shares the same startup input through field 15120;
without the later combat input its opponent remains at full health and the
player stays down. The final script adds recovery instead of treating that
pose as a hang.

The separate native-1.3 replay also exercises attacks and recovery, reaches Game
Over around field 29000, and returns through Exit to Menus to the title menu by
32000. Its later inputs differ from CopperStart to follow its observed phase.
Cross-ROM outcomes, random phases and pixels are not asserted equal.

## Investigated failures and exclusions

An initial exploration mistook the fallen pose for a failed round transition.
The game's code at `$6AEA6–$6AEBC` explicitly waits for Up to leave action 21;
the physical-input replay demonstrates that recovery. A one-time RAM-address-zero
clear was tried and discarded: it did not resolve that pose. The final ROM
retains the preceding bytes, and no caller-data guard was added or removed.

Escape during the fight is a separate unsuccessful exit path in this supplied
release. A matched control sends Escape at field 16200 under both ROMs. Native
1.3 reaches Guru `00000004.00C01570`; CopperStart goes blank and executes invalid
guest control flow. The CopperStart instruction trace shows its CloseLibrary
call returning, followed by the game's RTS at `$6437C` popping zero from
`$7FC18`. This is not a passing exit replay and does not establish a
CopperStart-specific compatibility defect. The earlier unmatched control had
sent Escape after the native game had already left combat; that comparison was
insufficient and was replaced by the matched one.

A temporary null-CloseLibrary diagnostic is also excluded from regression
qualification: it passed the existing CopperStart ROM and failed native 1.3.
The [Exec autodoc](https://d0.se/autodocs/exec.library/CloseLibrary) only guarantees
a null library argument from V36. No new defensive behavior or native-1.3
conformance expectation was inferred from it. Temporary diagnostic test sources
and the zero-page candidate are not part of the final source changes.

## Verification and limits

Three independent reset replays run for 32000 physical fields: CopperStart
normal, CopperStart scalar and native Kickstart 1.3. Complete initial inputs and
the exit control are written before process creation. Each retains 321 sampled
fields and 1605 BMP/state/Chip RAM/slow RAM/PCM files. All 1605 CopperStart files
match between CPU modes, and each reset replay matches every filename, size and
SHA-256 in its corresponding exploratory combat run. All processes exit zero;
all captured unsupported-operation indicators are null. Visual progression,
not process exit or those indicators alone, establishes the gameplay result.
Committed workloads use LF line endings; their parsed physical-input events
are checked equal to the separately hashed local replay inputs.

The exact final ROM passes 11 selected native graphics, Intuition, sprite-priority
and boot-memory guest cases, without failures or skips. The native emitter
Release build has no warnings/errors. Production/engine/disk source is unchanged;
the preceding merged build and engine/disk test results are retained evidence,
not claimed as newly rerun here. This evidence-only change requires input,
identity and documentation checks rather than another production build.

This qualifies bounded combat, knockdown recovery, defeat, score display and
restart. It does **not** qualify beating Tong Lo, later opponents, training or
bonus stages, tournament/two-player play, full game completion, original
protection, successful Escape exit, audio fidelity, installed-app admission or
host throughput. Nonzero PCM establishes sample presence only. All ROMs, media,
raw captures and temporary diagnostics remain local under
`artifacts/copperstart-full-contact-2026-10-24/`.
