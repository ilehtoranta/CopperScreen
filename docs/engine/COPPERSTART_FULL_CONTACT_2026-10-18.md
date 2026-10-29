# CopperStart: Full Contact native compatibility — 2026-10-18

Full Contact (Team17, Fairlight ADF crack) now shows its crack intro, clean HAM
photograph and complete title, accepts the disk-two swap and reaches a fight
against Tong Lo through 24,000 PAL fields. Normal and scalar CPU modes produce
identical capture files and final fingerprints. This is bounded compatibility
evidence; complete-game progression and audio quality remain unverified.

The [October 24 combat continuation](COPPERSTART_FULL_CONTACT_2026-10-24.md)
adds input-discriminated attacks, knockdown recovery, defeat, Top Ten and
Play Again with the later rebuilt ROM. It separately records the unsuccessful
Escape path under both ROMs; winning the first opponent remains unqualified.

Source: CopperStart commit
[`55f39525acb1ecebf60e04267907d2d74e6b2aee`](https://github.com/ilehtoranta/CopperStart/commit/55f39525acb1ecebf60e04267907d2d74e6b2aee),
branch `codex/copperstart-full-contact-20261018`, based on `9eed3fe`.
The [companion JSON](COPPERSTART_FULL_CONTACT_2026-10-18.json) records exact media,
commands, inputs, captures, binaries, tests, source archive and log hashes.
The preceding case is [Shadow of the Beast](COPPERSTART_BEAST_2026-10-17.md).

## Failures and native fixes

The game's intermediate RLE buffer spans `$5A1E3–$79760`. CopperStart's live
supervisor stack was `$77F6C`, inside that buffer; the original Kickstart 1.3
control used `$C80000`. At the first RLE pass, both runs have the same compressed
input and general registers. The buffer differs in exactly 66 bytes at
`$77F2A–$77F6B`: a six-byte exception frame and sixty saved-register bytes.
This shifts the final palette by one word, corrupts the HAM photograph and clips
the later title. Disk-read data was identical; the corruption occurred afterward.

The owned native linker now relocates the compiler's one six-byte bootstrap
stack instruction at `$FE2878` to a native A501 selector, before construction of
the live C# call chain. The probe restores its two RAM words and live registers.
With fitted 512 KiB A501, SSP starts at `$C7FFF0`, aligned below the allocator's
last boundary words. Native initialization reserves `$C7DFF0–$C7FFF0`, publishes
the actual supervisor bounds and checksum, and returns the old `$70000–$78000`
Chip reservation through Exec. Physical memory limits stay intact. Chip-only
machines retain the existing `$76000–$78000` runtime stack.

The Fairlight intro exposed a separate startup assumption: it installs its own
Copper list and writes only `$8100` to DMACON, enabling bitplanes while assuming
the OS has already enabled Copper DMA. Original-ROM startup provides COPEN;
CopperStart did not. The intro polls VBlank and explicitly clears interrupt
master, so changing VBlank interrupt policy does not solve this failure.
On exit, it also restores `GfxBase+$26` (`gb_copinit`), formerly zero.

The native graphics initializer now allocates an eight-byte Chip list containing
`BPLCON0=0` and the terminating WAIT, publishes it as `gb_copinit`, installs it
and writes `$82C0` to enable master/Copper/blitter DMA. The list must reside in
Chip RAM for custom-chip access. These register bits and Copper memory access
follow the Commodore [register reference](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_a.html)
and [Copper chapter](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_2.html).
The startup defaults themselves are supported by the native control and the
guest's actual register writes and library-field access.

These are general native startup fixes. They introduce no title recognition,
public caller-legitimacy checks, host OS shims, CPU changes or emulator timing
changes. Genuine allocation failure and private producer-layout checks remain.

## Qualified ROM and provenance

- ROM: 524,288 bytes, SHA-256
  `F41CE8979C51BA4CC7234ACD58C0A6A228EF1728A59853E41F20E0A62C192324`.
- Frozen pre-link C# image: SHA-256
  `4C3D6E2DF91BF6A20138B9C7382AB9B9CC6CFF14E950F0F902D632DF3BB2B86C`;
  448,542 code bytes, unchanged from the Beast qualification.
- Rebuilt native graphics/bootstrap block: 26,560 bytes at `$FF0000`;
  41 supported vectors, unchanged. Only `CopperStart.Graphics.dll` changes
  among the eleven recorded producer/fixture inputs.
- Composition replaces the six-byte stack primitive, installs the native block
  and renews the ROM checksum with the normal owned emitter. No active-stack
  copying or host CPU/RAM injection is used.

The ten frozen inputs retain the preceding record's concurrent-source
provenance. The JSON and local source-only archive identify this precise
overlay; they do not claim reproduction from one clean source commit.
ROMs, game media, raw RAM and local build outputs are not committed.

## Validation

Both production solutions build successfully. CopperScreen Release reports zero
warnings/errors. CopperStart Release reports one existing CS8602 warning in the
local CopperSharp compiler's `M68kSpillAndCopy.cs:74`, and zero errors. The frozen
native emitter and final test fixture each build with zero warnings/errors.

The final filtered fixture passes **67 cases, zero skipped**: **60 native guest
ROM cases and 7 host disk cases**. It covers custom boot blocks, DOS/startup/
console paths, native interrupts, graphics and the two new regression probes.
The stack theory covers fitted A501 and Chip-only configurations; with A501 it
allocates the former Chip stack area, receives real VBlank interrupts and checks
that caller data survives. A separate guest installs a Copper list without
reenabling DMA and observes the list's software interrupt request.

Against the preceding Beast ROM, the fitted-A501 stack case fails while the
Chip-only case passes; the Copper-without-DMA-write case also fails. All three
pass in the final suite. Intermediate/focused results overlap the final suite
and are not added to its total. Fourteen unchanged polled-reader tests were
excluded, with no new coverage claim. Native fixture and media runner engine
identities differ and are recorded separately.

All media runs use the frozen Lightweight runner, MC68000, PAL OCS 8371/8362,
512 KiB Chip plus 512 KiB A501, no Fast RAM, read-only DF0, and captures every
400 fields. Native Kickstart 1.3 controls use the same machine/media/script.

| Replay | Fields | Reviewed result |
| --- | ---: | --- |
| Full Contact, native control | 24,000 | Intro, photograph, title, disk-two swap and fight |
| Full Contact, CopperStart normal/scalar | 24,000 each | Fairlight logo/starfield/scroller at 800–3200; clean photograph at 4800/5600; complete title at 7200; disk-two prompt at 10000; fight at 16000; health loss/fallen fighter through 24000 |
| Beast regression, normal CPU | 32,000 | Three prompted swaps; WELCOME/game at 24000; animation at 26400; game remains at 32000 |
| North & South regression, normal CPU | 26,000 | Opening message at 22000; strategy map at 24000/26000 |
| Arte regression, normal CPU | 40,000 | Effects at 8000/16000; credits at 22000; textured corridor at 32000; artist page at 40000 |

Full Contact normal/scalar runs match **all 305 files per mode**: rendered frames,
capture metadata, Chip/A501 RAM and PCM. Their final cycle is `3401962782`, CPU
`A8FDF152C766B747`, hardware `500A21218C8A041E`, output `630D553D6435F275`;
unsupported operations: none. At field 5600, all 122,912 photograph/palette bytes
in `[$33814,$51834)` match the native control, SHA-256
`66a791db76cff63dd1a65c560e4a31f7e70a743a0056fed57bfbb0d4f3c25f06`.

The Full Contact script adapts the existing IPF workload's later events by
3,900 fields, uses Fairlight ADF disk images, exits the intro at 3600, swaps disk
two at 12400/12450, and sends later fire/direction input. The IPF workload is a
sequence reference, not the tested media. Cross-ROM final fingerprints and
fight outcomes differ with startup/random phases; equality is claimed only
between the two CopperStart CPU modes. An independent no-input control was not
run, so visible animation/health loss is not proof of each scripted input.

The first `$C80000` stack candidate collided with allocator boundary words and
failed boot. Stack-only, interrupt-policy and default-list/wrapper trials are
excluded from final qualification. Only the final ROM hash above supports the
reported passes. Full game/demo completion, audio quality, hardware acceptance
and host throughput remain outside this bounded replay. A short replay is not
a performance acceptance measurement.

Local evidence root: `artifacts/copperstart-full-contact-2026-10-18/`.
