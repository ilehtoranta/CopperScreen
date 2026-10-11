# Kick Off 3 AGA blank fields: current-source investigation

The current-source replay reaches Practice, blanks for 11 completed fields
while the guest clears its palette, then retains visible Practice output
through field 15,000. All 907 captured files match the unmodified production
scalar runner. No production engine or CPU timing change was needed.

The historical `LWA-AGA-002` observation remains open: its original input and
captures are unavailable, and the build has changed. This investigation explains
the observed transition blank on current source; it does not establish the
cause of the historical mostly blank idle capture.

## Measured current-source result

- The native cursor reaches `(226,170)` inside the guest's Practice hit rectangle
  before fire at field 9,500. All 51 snapshots from 10,000 through 15,000 retain
  Practice menu ID `$15`; the final screenshot was also inspected visually.
- At field 9,502, the CPU submits 32 zero writes to `COLOR00`–`COLOR31`
  from PC `$19F8B4`, inside the guest's palette-clearing loop. Raster samples
  are black for fields 9,502–9,512 inclusive.
- At field 9,513, the CPU submits all 32 restored palette entries from PC
  `$19F89C`. Visible output returns in that completed field. Each field from
  9,513 through 15,000 has 5,956 nonblack sampled RGB values; no further sampled
  blank or low-output field occurs.
- The 11 black fields still fetch 25,600 bitplane words each, with nonzero
  data. This is guest-controlled palette blanking during screen reconstruction,
  rather than a stopped bitplane DMA stream.
- Both native runners exit successfully. The diagnostic trace covers every
  completed field from 6,001–15,000 and every sampled raster from 8,801–15,000.
  All 907 capture files per mode match byte for byte, including raster, RAM,
  audio and state. No captured state reports unsupported CPU or video execution.

The analyzer also rejects an exploratory replay whose cursor stopped at
`(226,160)` outside Practice, despite matching normal/scalar captures. This
negative control prevents deterministic execution of the wrong menu from
being reported as Practice coverage.

## Historical evidence and reproduction boundary

The [October 3 continuation](AGA_ADDITIONAL_MEDIA_FIXES_2026-10-03.md) used
Copper68k `1.5.2-aga-dev.10`. Its field 9,500 showed Practice options and
12,000 was mostly blank with the CPU in its menu loop. Normal/scalar parity
established determinism, not the reason for the blank output.

The old record retains input and capture hashes, but its local input script
and captures are no longer available in this checkout. Current source pins
public Copper68k `1.5.3` and also contains intervening chipset changes. This
investigation reconstructs a new input sequence; it cannot attribute any
different result to one specific source change or reproduce the old run
field for field. Historical fingerprints are not replaced.

## Profile and independent reference

Both supplied DLM ADF-in-ZIP disks are unchanged. Native execution uses PAL,
68EC020, Alice 8374/Lisa 4203, 2 MiB Chip RAM, no Slow/Fast RAM and A1200
Kickstart 3.0 revision 39.106. No guest code, memory or media is patched and
unsupported execution is never bypassed.

Installed WinUAE 6.0.3.0 renders the main menu and Practice options with the
same ROM/disk contents. Live queries confirm A1200/AGA, 68020 with 24-bit
addressing, CPU/memory/blitter cycle-exact settings, real CPU speed, 2 MiB Chip,
zero Slow/Fast and 100% floppy speed. Its host warp mode does not provide
field alignment with Lightweight. Two retained Practice screenshots match;
this is a bounded independent software observation, not hardware certification.

WinUAE executable SHA-256:
`F3E55700FD811CD543FDFEBF6C3221CFAA3D385828F110C6D3EB534C64503123`.

## Guest mechanisms and discriminating controls

The native menu code at `$19F8A2` waits for its field flag and writes zero to
all 32 colour registers. The palette restore routine begins at `$19F88A`.
Black output can therefore coexist with running Copper and nonzero bitplane
DMA while the guest rebuilds the screen.

The main menu's native hit test at `$19EEC4` compares half the stored cursor X
and the stored cursor Y against half-open rectangles. The Practice entry at
`$1A4D7C` requires `84 <= X/2 < 156` and `165 <= Y < 181`. A visually overlapping
cursor is insufficient to prove selection. The retained input targets stored
coordinates `(226,170)` before fire. Practice's menu ID is `$15`; the analyzer
requires that ID in every snapshot from field 10,000 through 15,000.

An earlier reconstruction selected Anco Cup. That run had 19 black fields,
9,502–9,520, while DMA fetched 25,600 words per field and guest drawing
progressed. It then retained its completed menu through field 15,000.
Other exploratory inputs overshot Practice or stopped at Y=160 outside its
hit rectangle; they are excluded from Practice validation. A late live-input
attempt was not consumed and supplies no input or rendering evidence.

## Reproducible tooling

[Probe instructions](../../scripts/probes/KickOff3/README.md) retain input
generation, isolated diagnostic-copy preparation and analysis. The copied
engine records submitted CPU/Copper register writes, successful DMA fetches
and sampled completed rasters; production engine files are not changed.
Palette writes are included, and the last field is flushed. The diagnostic
runner is compared with the unmodified production scalar runner.

The analyzer rejects missing or duplicate field records, incomplete native
captures, a missed Practice click, an unexpected idle menu ID, unsupported
execution or capture mismatches. RGB sampling is every sixteenth framebuffer
element and is not a complete pixel-conformance test.

The result and provenance are retained in the accompanying
[machine-readable record](KICK_OFF_3_BLANK_FIELDS_2026-10-08.json).
ROMs, media, snapshots, disassembly, diagnostic binaries and reference
screenshots stay under ignored local artifacts. No performance acceptance or
football-gameplay claim follows from this investigation.
