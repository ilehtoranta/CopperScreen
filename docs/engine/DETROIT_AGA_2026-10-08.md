# Detroit AGA native test, 2026-10-08

This is the retained initial 1.5.3 failure. The
[startup correction and gameplay follow-up](DETROIT_AGA_FIX_2026-10-08.md)
records the current development fix; the original fingerprints remain unchanged.

The supplied Detroit disk set **does not reach the game on the tested stock
A1200 profile**. The DLM intro animates, but leaving it
reaches an unsupported Copper68k 1.5.3 instruction-timing form. Normal and
scalar execution stop at the same instruction with all 67 capture files
identical. No engine, CPU package or guest/media changes were made.

## Profile and media

PAL, 68EC020 interpreter, Alice 8374/Lisa 4203, 2 MiB Chip RAM, zero Slow/Fast
RAM and native A1200 Kickstart 3.0 revision 39.106. Two DD drives are connected:
disk one in DF0 and disk two in DF1, both read-only.

Supplied ZIPs:

- `Detroit (1994)(Impressions)(AGA)(Disk 1 of 2)[h DLM].zip`
- `Detroit (1994)(Impressions)(AGA)(Disk 2 of 2).zip`

Archive and uncompressed media hashes, exact source and runtime assembly hashes,
CPU profile, input, capture identities and final states are retained in the
[machine-readable record](DETROIT_AGA_2026-10-08.json). The source working tree
already contained unrelated changes; HEAD alone does not identify the tested
engine. Neither supplied disk is rewritten.

## Observed results

The no-input control completes 6,000 fields with exit code zero and remains
in the animated DLM intro. Inspected captures show rotating background graphics
and changing credits/text; the captured PCM samples are silent. This is intro coverage,
not game loading or gameplay.

The start script presses left mouse at field 1,800 and releases it at 1,820.
Both execution modes leave the intro and throw:

```text
Copper68k.UnsupportedM68kTimingException:
Unsupported exact MC68020 timing for opcode 0x0A18 at 0x0001F2D4
in profile A1200_68EC020_14MHz.
```

Both exit with code 1 after 1,996 completed fields. The exception capture
label is `frame-001996`, but execution has already advanced into the next
field: cycle `283584658`, beam line 94/CCK 107, PC `$1F2D4`, SR `$0018`,
A0 `$1F56E`, A7 `$20774`. The final captured state has null `unsupported`
and `videoUnsupported` properties; the exception and failed exit are the
authoritative failure evidence, not those null properties.

Offline disassembly of the unchanged captured guest RAM identifies:

```text
0001F2CC  207c0001f56e  movea.l #$1f56e,a0
0001F2D2  6004          bra.b   $1f2d8
0001F2D4  0a18009e      eori.b  #$9e,(a0)+
0001F2D8  4a10          tst.b   (a0)
0001F2DA  66f8          bne.b   $1f2d4
```

This is a reached immediate byte EOR with address-register postincrement in
the shared CPU timing interpreter. The observed stop is a missing supported
timing form, not evidence of an illegal guest opcode or an AGA raster fault.
The corrected CPU form must be implemented and tested in Copper68k's owner
repository, then consumed as a new pinned package and replayed here. A release
or package publication is not authorized by this compatibility test.

All 67 files in each scripted capture directory match byte for byte, including
RAM, state, completed raster, audio and the CPU profile. Matching failure modes
establish determinism, not correctness. The requested 8,000-field runs terminate
early; they are not successful complete replays.

## Validation and limits

`dotnet build CopperScreen.slnx -c Release` succeeds with zero warnings/errors.
[The retained input and replay commands](../../scripts/probes/Detroit/README.md)
reproduce the failure without a guest patch or unsupported continuation.
Local evidence is under `artifacts/detroit-2026-10-08/`.

Title screen, management interface, meaningful game actions, saves and full-game
completion remain unverified. No audible playback is established by the sampled
PCM; silence alone does not demonstrate an audio defect. Both drives were read during startup, but this
does not establish the later disk-two workflow. No independent reference or
hardware conformance check was performed; no performance acceptance is claimed.
