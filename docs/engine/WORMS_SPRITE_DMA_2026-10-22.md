# Worms sprite DMA and gameplay follow-up — 2026-10-22

Worms' vertical blue bars came from incorrect sprite DMA scheduling in
Lightweight. The same artifact appeared with CopperStart and native Kickstart
3.1 in the [preceding gameplay record](COPPERSTART_WORMS_GAMEPLAY_2026-10-22.md).
This correction changes the engine and retains the qualified CopperStart ROM
from source commit `cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
No ROM rebuild or guest OS change is required for this engine correction.

## Cause and correction

A read-only instruction-boundary probe reproduces the earlier native 3.1
field-17500 raster byte for byte. Sprite 6 has POS `$9E94`, CTL `$A100`: start
158, stop 161. Its first data word changes, but DATB retains `$2480`. The old
slots put its second word at output H=`$32`, where the scrolled playfield's
bitplane request wins. Only two bytes advance per data row, the stop control
pair also remains incomplete, and stale pixels repeat down the display.

Commodore's *Amiga Hardware Reference Manual*, third edition, Figure 6-9,
printed page 195, places audio at `$0D/$0F/$11/$13`, then sprite 0 at
`$15/$17` through sprite 7 at `$31/$33`. With the engine's retained address
input followed by RAM output convention, outputs are one CCK later. Sprite
outputs now span `$16/$18` through `$32/$34`, two CCK earlier than before.
Sprite 6 finishes at `$30`, before the first conflicting bitplane output at
`$32` for DDFSTRT=`$30`.

The conservative DDF/channel eligibility table is removed. The existing
physical pending-output arbiter decides which individual words are stolen.
For example, a one-plane display does not consume the early slots of a
four/six-plane display and can retain sprite 7. A genuinely denied second
data word still reuses the previous DATB latch. Accepted transfers, field
reload, Denise input delay and the canonical clock retain their contracts.

The sprite-only intermediate exposed an audio overlap in Superfrog: audio 3
was still scheduled at address `$15`, colliding with sprite 0. Its first data
word stayed stale while the pointer advanced only for the second. Correcting
Paula's four address slots to `$0D/$0F/$11/$13` restores their separation and
removes the resulting green trail. This follows the same documented slot
diagram; no title-specific branch or synthetic sprite clearing is involved.

The final native Worms trace programs sprite 6 for rows 130–132. Both words
advance, the zero control pair arrives at row 133, and the channel disarms.
The game world differs after the timing correction, so this is a separate
run with identical physical inputs, not a claim of identical old/new state.

## Verification

Four authored scrolled-playfield pixel/DMA cases fail on the previous engine
and pass with the sprite correction. A fifth case with audio 3 active fails
on the sprite-only intermediate and passes after the audio correction. They
check pointer advancement through the stop pair, disarming, both data words
and the absence of trailing pixels. Existing slot, accepted-transfer,
contention, sample/IRQ, bus-latch, beam-resume and ECS/AGA phase fixtures are
updated to the documented schedule without changing their tested contracts.

The production solution builds with zero warnings/errors. The final full
engine diagnostic suite passes **1255 tests**, with **seven optional native
tests unavailable**. The focused audio/new-sprite run passes 93 cases and
overlaps the full suite; these counts are not added together.

The Superfrog original-media regression replays movement, jumps and life
loss through field 12200 using its retained CopperStart ROM. The character
remains visible without the intermediate green trail. All **615 capture
files** match between normal/scalar execution, including raster, both RAM
banks, state and PCM. No snapshot reports unsupported hardware; 94 of 123
captured fields contain nonzero PCM. This establishes sample presence, not
audio fidelity or level completion.

The CopperStart run accepts the printed Table B, row 10, column G code and
reaches Training at field 104000 without the blue bars. Its old movement
script walks the worm into the water on the newly generated terrain; the
statistics screen follows. Physical mouse input returns to the menus and
starts a second Training round on a snow landscape. At 115000 the active
worm has 100 health. F2 selects the grenade and Up adjusts its aim; the HUD
reports a three-second fuse with low bounce. Space press/release inputs make
two throws. The camera follows the first, and the second produces a crater
visible at 116200. The active arrow returns and the worm retains 100 health.
Both landscapes render without the repeated vertical bars.

The native 3.1 control also reaches Training and exercises movement/bazooka
inputs through field 20500 without the blue bars. These are functional
gameplay observations; a completed campaign or winning level is not claimed.

The final normal/scalar replays both exit zero at field **116200**. All
**5815 capture files** match byte for byte across 1163 captured fields,
covering state, both RAM banks, full raster and PCM. No snapshot reports
unsupported hardware. One captured field contains nonzero PCM; this confirms
sample presence without a fidelity claim. All 5815 replay captures also
match the corresponding interactive normal-run captures.

The interactive scalar discovery run stopped at 115420 with a
Windows file-sharing IOException in the frozen harness's control-file reader.
Its captures up to that point match the normal run. The final pair replays
the complete saved physical-input sequence from reset, with its exit control
written before launch; it does not restore or patch guest state. Capture
sampling is field 1 and every 100 fields through 116200. Extra interactive
segment-end captures remain separate diagnostic evidence.

## Provenance and limits

The original Worms Disk 1 IPF remains read-only. The machine is PAL OCS
8371/8362, 68000 interpreter, 512 KiB Chip plus 512 KiB slow RAM, no Fast RAM
or RTC, one DF0 and the full 908×313 raster. No media, protection, ROM or
guest-memory patches, extra RAM, host OS shim or display cropping are used.
The unchanged frozen media harness, CPU and disk binaries run with the new
engine DLL; the production benchmark CLI is a separate executable contract.
An initial mistaken positional CLI launch is excluded from replay evidence.
Intermediate sprite-only runs are retained separately from final results.

Exact input, source patch, binary, media, test and capture identities are in
the [machine-readable evidence](WORMS_SPRITE_DMA_2026-10-22.json). Captures,
ROMs, media and the manual scan remain local. These are bounded compatibility
and regression checks, without a throughput or complete silicon-timing claim.
The unresolved comparator/control edges in LWA-VIDEO-006 remain open. Game
completion, multiplayer, saves and original disks 2/3 are not qualified.

Primary reference: [Commodore HRM third-edition scan, Figure 6-9](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_3rd_Edition.pdf).
