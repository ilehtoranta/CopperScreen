# Lotus Esprit Turbo Challenge: fresh Miami Chase ROM

Started 2026-10-18; completed 2026-10-19. The supplied Angels ADF reaches its
specification, control and options screens, music selection and input-driven opening race under a fresh CopperStart
ROM containing the Miami Chase fix. No additional compatibility correction is
needed for this recorded sequence. The [evidence record](COPPERSTART_LOTUS1_2026-10-18.json)
binds the exact media, compiler inputs, ROM, execution binaries and captures.

The machine is the bounded PAL OCS A500: 68000, 8371 Agnus, 8362 Denise, 512 KiB
Chip plus 512 KiB A501 RAM, no Fast RAM and one read-only DF0. The supplied disk
is labelled `[cr Angels][b doscopy]`. Its archive is SHA-256
`6DEAA0ED51B727507EACCF775760042E3A94ED22DF89AB571F9F84FF22BBCCB5`;
the unchanged 901,120-byte ADF is
`6A7C7CAC2D122795C3E254348E4519F0838ED7E535BA10C60406C1ADFFE1C02D`.
This observation applies to those exact bytes, without certifying the dump label.

## Fresh ROM and execution

All five native root sources and eleven component DLLs remain frozen from the
Miami Chase-qualified composition. The producer source is
`9a911bb9887d130e1b72360fab65c132bcaeee16`, retaining the public video-frequency
fields, Lotus 2 supervisor stack, Major Motion DOS/HUNK/VBlank corrections and
earlier memory, caller-data and graphics fixes. The native compiler and graphics
linker ran anew; no existing ROM was copied or byte-patched.

The new build completed at `2026-10-18T21:04:27.2052577+00:00`. Its 512 KiB image is SHA-256
`13BE91C7056B79AC57089AB0553353B4C693EC4ACDBA77E4A7D4289C1FC6777B`, with 451,340 code bytes and 26,098 graphics bytes at `$FF8000`.
The native root passes its compatibility gate with zero managed allocation sites
and zero framework features. The resulting ROM is byte-identical to the preceding
Miami Chase-qualified image. This retains the applicability of that image's
previous regression evidence; it does not count those earlier replays as new runs.

The unchanged frozen runner uses the pinned `Copper68k 1.5.4-nitro-dev.2` package.
Each actual guest ROM boots on the same hardware. `replay.py --firmware native`
only suppresses private bootstrap-receipt interpretation after takeover; the
explicit `--rom` path selects the firmware. No host OS service shim is involved.
This separately qualified frozen composition does not replace the standard app's
admitted firmware artifact or establish full moving-main ROM compatibility.

## Observed sequence and checks

The [play input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-lotus1-play-v1.json)
retains the earlier native Lotus sequence: fire advances the screens, then the
joystick accelerates and steers during the opening race. The
[idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-lotus1-idle-v1.json)
keeps identical startup events and releases gameplay controls. Both ROMs use the
same input schedule; no disk swaps are needed.

The reviewed complete viewports show car specifications at field 3,000, control
instructions at 5,000, game options at 7,000, music selection at 9,000 and the
opening race by 11,000. Fields 12,000, 13,000, 13,800, 15,000 and 16,000 retain
road, scenery, traffic, HUD and the one-player garage panel. Acceleration and
steering change the player's progress relative to the idle control. No obvious
persistent corruption or hang appears in the inspected sequence. The race uses
manual gears and the recorded driving remains in first gear; advanced driving
and gear-change coverage are not claimed.

| Check | Result |
| --- | --- |
| Kickstart normal/scalar, 16,000 fields each | All 805 raw files at 161 checkpoints and final CPU/hardware/output/cycle fingerprints match. |
| Fresh CopperStart normal/scalar, 16,000 fields each | All 805 raw files and the same four fingerprint types match within the pair. |
| Kickstart input control | All 625 raw prefix files match before the first input difference at 12,500; first different image at 12,600. |
| Fresh CopperStart input control | All 625 raw prefix files match; first different image at 12,600. |
| Preceding versus fresh CopperStart | Play and idle each retain all 805 raw captures and all four fingerprints. |
| Exact fresh-ROM guest probes | Both video-frequency cases and both inherited-VBlank/supervisor-stack cases pass: 4/4, zero skips. |
| Frozen native builder | Pass, zero errors; 18 expected CS0436 warnings from source types intentionally shadowing frozen component types. |
| Consumer production Release build | Pass, zero warnings and errors. |

Native Kickstart and CopperStart are compared visually at matching field indices;
4 of ten selected complete RGB images match exactly. The remaining
images are recorded as different, without cropping, scaling or shifting to force
identity. This is separate from the strict within-ROM CPU-mode comparisons.
Sampled PCM contains audio; listening and fidelity were not assessed.

## Limits and reproduction

This is opening-race compatibility for the supplied release. Full laps, complete
races, later tracks, two-player modes, alternate media, NTSC, physical timing and
host throughput remain unverified. A completed runner budget alone is not a
compatibility pass. No production firmware, engine, CPU or defensive-check change
is part of this continuation, and no ROM, media or package is published.

Use the retained input with `scripts/probes/CopperStart/replay.py`, the exact
recorded ROM/media and frozen runner, `--firmware native --frames 16000 --interval
100 --slow-ram-kib 512`. Repeat cold boots in separate directories with
`--scalar-cpu` and the idle input. Local ignored artifacts are retained under
`artifacts/copperstart-lotus1-2026-10-18/`, including compiler inputs/outputs,
commands, complete captures, guest test results and original-size viewing copies.
ROMs, game media and build artifacts remain untracked.
