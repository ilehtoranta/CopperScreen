# CopperStart: Inside the Machine — 2026-10-18

Inside the Machine v1.0.1 (DESiRE) now advances past the custom loader, renders
its effects and reaches the closing logo under the rebuilt CopperStart ROM.
The failure was missing CIA event-counter startup, not a defensive argument
check. The native fix restores that OS startup state without changing the
demo, emulator, CPU package or public caller requirements.

CopperStart source commit: [`185430afc13cbe98dd349f9972788b68b99bd592`](https://github.com/ilehtoranta/CopperStart/commit/185430afc13cbe98dd349f9972788b68b99bd592), on
`codex/copperstart-inside-machine-20261018`, based on `55f3952`.
The [companion JSON](COPPERSTART_INSIDE_MACHINE_2026-10-18.json) records exact
source, producer, ROM, media, commands, capture hashes and test identities.
The [previous title](COPPERSTART_LOTUS_III_2026-10-18.md) used the unchanged
[Full Contact ROM](COPPERSTART_FULL_CONTACT_2026-10-18.md).

## Cause and fix

The [earlier audit](COPPERSTART_AUDIT_FOLLOWUP_2026-10-16.md) left this demo's
black-loader failure unresolved. It also reproduces on the preceding Full
Contact ROM through 18,000 fields: the demo's frame task advances, but its
custom loader never starts disk DMA. Both CPU modes reproduce it identically.

The [demo's pinned source](https://github.com/grahambates/inside-the-machine-public/tree/822a4031cbbe94aed271d6a746d556d233574fb6),
`framework/framework_trackloader.asm`, sets disk-settling deadlines from CIA B
TOD, then yields while waiting. In the failing run TOD is zero and the deadline
is 235 scanlines ahead; the difference stays at -235. The visible VSync loop is
the yielding task, not evidence that the disk deadline is progressing.

An external observational probe reads the frozen engine's CIA state without
writing it. At fields 200, 400, 600 and 800, both old-ROM TOD counters remain
stopped at zero. Kickstart 1.3 starts both; the final CopperStart ROM does too.
For example, corrected CIA B advances from 122871 at field 400 to 248071 at
field 800, exactly 400 PAL fields of 313 scanline events.

The owned native cold-start initializer now selects counter access with CRB7
clear, writes each counter's high/middle/low bytes and starts counting on the
low-byte write. CIA A receives VSync events; CIA B receives HSync events. This
follows the Commodore [8520 TOD description and register map](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_f.html).
Timer control and interrupt policy otherwise remain unchanged. There is no
title recognition, artificial disk delay, host service or new caller-data guard.

## Qualified ROM and build provenance

- 524,288 bytes, SHA-256
  `98BAD185B36AC86CB8B5F83590DEA113EC0182C04F286BEE7CAFDCC2BD755291`.
- Native graphics/bootstrap block: 26,612 bytes at `$FF0000`, 52 bytes larger;
  the existing 41 supported graphics vectors are unchanged.
- Frozen pre-link C# image: 448,542 code bytes, unchanged SHA-256
  `4C3D6E2DF91BF6A20138B9C7382AB9B9CC6CFF14E950F0F902D632DF3BB2B86C`.
- Only the graphics/bootstrap emitter changes among the eleven recorded
  producer/fixture inputs. The normal owned composer retains the six-byte
  bootstrap-stack relocation, installs the native block and renews the checksum.

The source-only local snapshot and preceding frozen-input provenance are in
the JSON. This is an exact source/binary overlay, not a claim of reproduction
from one clean source commit. ROMs, media, raw RAM and build outputs remain local.

Both production solutions build successfully. CopperScreen Release has zero
warnings/errors. CopperStart Release has the existing CS8602 warning in the
local CopperSharp compiler, with zero errors. The frozen native emitter and
final fixture each build with zero warnings/errors. The solution builds use
the recorded existing local dependency/restore configuration; frozen media
runner binaries are identified separately from newly built production outputs.

## Tests and replay review

The new guest samples both TOD counters without initializing them, waits three
independent raster wraps and checks their field/scanline advancement. Both
Chip-only and fitted-A501 cases fail on the previous ROM with `0 -> 0`, then
pass on the final ROM. The final filtered fixture passes **69 cases, zero
skipped: 61 guest ROM cases, 7 host disk cases and 1 host ROM-composition case**.
The checksum/composition test does not execute a guest despite its class name.
The focused two-case pass
overlaps that total. Fourteen unchanged polled-reader cases were excluded,
with no new pass claim for them.

All media runs use the same frozen Lightweight runner, MC68000, PAL OCS
8371/8362, 512 KiB Chip plus 512 KiB A501, no Fast RAM and read-only DF0.
The supplied NFO specifies the same 512/512 KiB machine. Native Kickstart 1.3
uses the same profile and media. The ZIP contains macOS metadata with another
`.adf` suffix, so every actual run explicitly selects
`DESiRE-InsideTheMachine.adf`. Initial unselected-ZIP launches were rejected
before emulation and are excluded from replay results.

| Replay | Fields | Reviewed coverage |
| --- | ---: | --- |
| Previous CopperStart normal/scalar | 18,000 each | Black loader; frame task advances; no custom-loader disk DMA; all 230 capture files per mode match |
| Native Kickstart 1.3 | 18,000 | Motherboard, Amiga, effects, credits and closing DESiRE logo |
| Final CopperStart normal/scalar | 18,000 each | Motherboard, title, face, tunnel, circuit/greetings, credits and closing logo; all 230 capture files per mode match |
| Full Contact regression, normal CPU | 24,000 | Photograph/palette retain all 122,912 native-control bytes; disk-two fight, health loss and fallen fighter retained |
| Lotus III regression, normal CPU | 32,000 | Disk swap, menus, car/music selection, opening race, advancing speed/score and time-up retained |
| Arte regression, normal CPU | 40,000 | Effects, credits, textured 3D corridor and artist page; exact reviewed fields in JSON |

Captures every 400 fields include rendered images, state metadata, Chip/A501
RAM and PCM from sampled fields. Raw manifests keep their automatic `unreviewed`
classification; visual milestones and the bounded result live in this report
and JSON. Cross-ROM exact timings/fingerprints are not expected to match.
Both final CopperStart modes end at cycle `2557836004`, CPU fingerprint
`9264F8FAAF081263`, hardware `E592ADCFCB86009E`, output `B792FDE1A33A7FF1`;
unsupported operations: none.
The [older blitter correction](BLITTER_FINAL_MODULO.md) remains separate engine
evidence using different binaries; its goldens and performance results are not
replaced by this ROM compatibility continuation.

This is sampled native compatibility, not a frame-by-frame hardware trace or
continuous audio-fidelity assessment. Alternate hardware, the HD executable,
all possible demo interactions and host throughput acceptance remain unverified.
No package is published and no standard-app firmware pin is updated.

Local evidence root: `artifacts/copperstart-inside-machine-2026-10-18/`.
