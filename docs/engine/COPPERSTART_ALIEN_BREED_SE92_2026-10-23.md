# CopperStart Alien Breed Special Edition '92 — 2026-10-23

The current CopperStart ROM boots the supplied original SPS 615 IPFs, accepts
the physical Disk 2 swap, and runs one-player Deck One. Movement, scrolling,
firing, key/ammunition pickups, opening doors, Intex terminal entry, mission
objectives and return to gameplay work. Escape returns to the game menu.
Storage-area aliens and weapon flashes remain visible, and enemy contact
reduces the player's health. No new ROM or
engine compatibility defect was established. This is opening-deck coverage;
no deck completion or campaign progression is claimed.

## Configuration and media admission

The unchanged 524288-byte CopperStart ROM comes from producer commit
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The engine retains the [Worms sprite/audio DMA correction](WORMS_SPRITE_DMA_2026-10-22.md),
source `4b71596195a96c4cbbb1abe7a3e673588d8fbc9f`, binary SHA-256
`4c6a6129ae498031ac2bb321bcbb33b90ba2d766068edf7a36b2a2e27d4c81ff`.
The [F17 replay](COPPERSTART_F17_2026-10-23.md)'s Copper68k, CopperDisk,
CopperFloat and functional host harness binaries are retained unchanged.

The profile remains PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus
512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0 and a full 908×313
raster. Both supplied SE92 entry hashes match the SPS 615 OCS entries in
[MAME's original-media manifest](https://github.com/mamedev/mame/blob/master/hash/amigaocs_flop.xml).
The IPFs are loaded directly from the ZIP. No guest-memory patch, protection
bypass, extra RAM or title-specific emulation behavior is introduced.

The first candidate, `AlienBreedII-TheHorrorContinues_44.zip`, was initially
selected as an A500 title. All three hashes instead match SPS 44 in
[MAME's AGA manifest](https://github.com/mamedev/mame/blob/master/hash/amigaaga_flop.xml).
The [publisher manual](https://evercade.info/wp-content/uploads/2023/03/Alien-Breed-II.pdf)
distinguishes its AGA version's larger machine requirements. On the bounded
A500, CopperStart normal/scalar and native Kickstart 3.1 remain black through
field 8000; Kickstart 1.3 does the same through 5000. All settle at `$0FF338`.
A bounded external instruction trace shows large BSS zero-initialization
overwriting aliased loader code on the undersized machine. No OS argument
rejection or CPU defect was established. This candidate is **unavailable A500
coverage, not a successful replay**. A correct AGA-machine replay is not claimed.
Its media identities, runs and diagnostic trace are retained in this record.

## Native gameplay replay

| Completed field | Reviewed CopperStart observation |
| ---: | --- |
| 5000 | Disk 2 request. Boot Disk is ejected at 5002; Disk 2 is inserted at 5202 and acknowledged with fire at 5402. |
| 7000, 8000 | SE92 title and credits. |
| 9000 | Main menu with one-player game selected. |
| 10000, 11000 | Shuttle Bay Two / Deck One mission briefing. |
| 12000, 13000 | Player and shuttle visible in gameplay. |
| 13100–14500 | Movement and scrolling through the bay, firing/ammunition use, key and ammunition pickups. |
| 15400 | Bay door opened with a key; maintenance area reached. |
| 16000 | Contact with computer terminal. Space at 16002 begins terminal entry. |
| 16800, 17000 | Interactive Intex main menu. |
| 17400 | Mission objectives displayed after joystick menu selection. |
| 18400, 18600 | Abort Intex Network returns to gameplay at the terminal. |
| 18900 | Next passage opened and bay junction reached. |
| 19600–19900 | Storage-area aliens, firing and health loss from contact. |
| 20100, 20300 | Continued movement and firing in storage, with ammunition and health changes. |
| 22000 | Main menu after Escape at 20302. Idle presentation resumes by 23000. |
| 24000 | Fire at 23002 skips the idle presentation back to the menu. |

The [preserved manual](https://www.lemonamiga.com/doc/alien-breed-special-edition-1992/49)
describes joystick movement/fire, Space for terminal entry and Escape to the
game menu. Default one-player settings are retained. No trainer, cheat,
password, saved-game load or guest-state restoration is used. Terminal
purchases and the complete weapon set are not qualified by menu navigation.

Native A500 Kickstart 3.1 on the identical profile and media also completes
the swap, Deck One startup, movement, pickups, doors, terminal round trip,
storage-area contact and game-menu return. Enemy timing and some resource
counts differ between the ROMs; their RAM and complete gameplay images need
not match.

## Verification and provenance

All three final reset-based runs exit successfully at field 24000, with no
unsupported operation reported. Each retains 1205 files across 241 captured
fields. CopperStart normal and scalar captures are byte-identical; each final
run also reproduces all 1205 files from its corresponding discovery run.
CopperStart retains 182 fields with nonzero PCM; native Kickstart retains 183.

The complete [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/alien-breed-se92-copperstart-intex-v1.json)
and immutable exit-control file are
written before the final processes start. Discovery and qualification are
separate runs. Captures retain state, both RAM banks, full raster and field
PCM at field 1 and every 100 fields through 24000.

The [machine-readable record](COPPERSTART_ALIEN_BREED_SE92_2026-10-23.json)
retains commands, ROM/media/input/binary identities, capture manifests and
harness provenance. The retained functional harness is not rebuilt. The
separate ABII instruction probe is diagnostic only and adds no guest patches.
Production code and ROM are unchanged, so production builds and emulator
suites are not rerun for the documentation/workload change. The preceding
Worms record retains those separate qualified results. ROMs, media, captures
and build outputs remain local.

Later decks, completion, two-player operation, saving and full shop/weapon
coverage remain unqualified. Nonzero PCM establishes sample presence, not
listening or audio fidelity. Native Kickstart on this engine is not a physical
hardware oracle. No host-throughput or complete hardware-timing acceptance
claim is made.
