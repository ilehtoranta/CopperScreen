# Superfrog character visibility — 2026-10-20

The later [Magic Woods 1-1 completion replay](SUPERFROG_COMPLETION_2026-10-20.md)
extends the bounded gameplay coverage recorded below, using this same ROM and
corrected engine. Its normal/scalar completion and native-control limits are
reported separately.

Superfrog's missing character was a shared Lightweight sprite DMA fault.
CPU/Copper writes to SPRxPOS and SPRxCTL reached Denise's display registers,
but did not update Agnus's vertical DMA controls. After an earlier descriptor
marked the channel exhausted, the game could program a new position and a
pointer to pixel data without any new data fetches occurring. Both CopperStart
and native Kickstart 1.3 reproduced the fault.

The common custom-register path now delivers position/control writes to the
existing sprite DMA owner. Reprogramming clears its stale exhausted state;
the pointer need not reference another descriptor header. The mandatory field
control reload remains in place, and a POS already fetched from RAM retains
its matching CTL fetch across an intervening CPU/Copper position write.
Accepted output addresses and Denise's input delay are unchanged. No title
recognition, display crop, host OS shim, additional caller-legitimacy check or
release logging/allocation is introduced.

The [Commodore hardware reference](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_4.html)
describes VSTART/VSTOP in the sprite control registers as the DMA controls;
automatic descriptor loading writes those same registers. They are not private
descriptor metadata that can ignore processor writes.

## First divergence and regression checks

A reset-based trace of the prior corrected-viewport engine reaches field
12000 and steps one more field through its existing scalar CPU quantum. At
line 32 the Copper programs SPR0 pointer `$538AC`, POS `$9C82` and CTL `$BF81`.
Denise reflects the new position/stop, while Agnus retains POS `$0007`, CTL
`$FFFC`, `Active=false`, `Exhausted=true`. Its character-data pointer never
advances. The external probe reads state without guest bus reads or memory
patches. This resolves the visibility uncertainty in the
[earlier bounded replay](COPPERSTART_SUPERFROG_2026-10-20.md).

Three channel-reprogramming cases fail on the old source and pass after the
fix. A full-frame pixel test verifies actual fetched sprite output. A fifth
case preserves the second control word when a position write intervenes. That
boundary case failed the initial fix and prompted the control-pair correction;
the initial media runs are retained separately, not counted as final coverage.

The final diagnostic suite passes **1250 cases**, with zero failures and **seven
optional native cases skipped** because their inputs are not configured. Those
seven cases are unavailable coverage. The earlier 200-case sprite run overlaps
the final suite. The production Release solution and the frozen live probe
both build with zero warnings/errors. Diagnostic tests use separate outputs.

## Continued original-media gameplay

Fresh normal, scalar and native-control runs restart from reset with the final
engine and original SPS 35 Disk One IPF. The unchanged 524288-byte CopperStart
ROM has SHA-256
`83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`,
producer `b78ddc09302cd506687327be1c1434891444cc4a`. This engine fix needs no ROM
rebuild. Its earlier ROM/component checks retain their original evidence.

| Field | Reviewed behavior under both ROMs |
| ---: | --- |
| 11000 | Character visible at the Magic Woods 1-1 start. |
| 11100 | Walking, log-bridge crossing and collection to score 350. |
| 11200–11520 | Spike-pit life loss, start-area respawn and temporary disappearance/blink followed by visible recovery. |
| 11592–11670 | Visible jump, vertical/horizontal camera scroll and landing beyond the first spike pit; score 650, 87 coins required. |
| 11700–11736 | Nearby pickups reach score 1700, then health drops from four to three units; score 1300, 86 coins required. |
| 11900–12100 | Another spike-pit life loss and respawn at the acquired checkpoint beyond the first pit; score and coin count retained. |
| 12200 | Active gameplay at the checkpoint, score 1300 and 86 coins required; character temporarily absent in this post-respawn snapshot. |

The [preserved Team17 instructions](https://www.emuparadise.me/GameBase%20Amiga/Extras/Instructions/S/Superfrog.txt)
describe joystick movement/jumping, coin requirements and the restart pill.
Only ordinary physical input is used. No state restoration or guest-memory
injection is involved. Fire after respawn does not demonstrate a powered
weapon, and no weapon kill or full combat qualification is claimed.

All **670 files per CopperStart mode** match byte-for-byte through field
12200: full 908×313 BMPs, CPU/hardware state, Chip RAM, slow RAM and PCM.
All captured unsupported/video-unsupported flags are null. Nonzero PCM occurs
in 105 snapshots per CopperStart mode; listening/fidelity is unassessed.
All three final processes exit successfully. The native control reaches the
same bounded states; cross-ROM state/image equality is not claimed.

Character pixels are reviewed across multiple fields and visible states.
At field 12200, the guest Copper program sets all four character channels to
encoded HSTART 464/480, beyond the 454-lores-pixel raster; field 12100 uses
on-screen starts. This explains the later post-respawn disappearance without
the old missing-DMA fault. The full-size HUD review records a peak of 1700
and final 1300, correcting an interim small-preview reading of 1800.

The bounded profile is PAL OCS 68000 interpreter, MOS 8371 Agnus, MOS 8362 Denise,
512 KiB Chip plus 512 KiB slow RAM, no Fast RAM, RTC disabled and one read-only
DF0. Level/game completion, Disk Two, saving and full combat remain unqualified.
The Story Disk is not rerun with this engine image. Broader sprite comparator,
manual-data and stolen-slot/silicon timing edges remain open. This is native
media compatibility coverage, not throughput acceptance or full hardware proof.

[The evidence record](SUPERFROG_CHARACTER_2026-10-20.json) preserves source/patch,
ROM, media, binaries, trace, tests, actual input segments and capture-manifest
identities. The replay is
[recorded here](../../CopperMod.Amiga.Lightweight.Runner/Workloads/superfrog-copperstart-visible-gameplay-v1.json).
Local artifacts remain under `artifacts/superfrog-character-2026-10-20`.
No ROMs, media or build artifacts are committed; no packages are published.
