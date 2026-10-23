# Desert Strike: three-disk startup and opening flight

2026-10-19. Desert Strike reaches its campaign briefing, loads the supplied third
disk, and responds to takeoff and flight controls on a fresh CopperStart ROM
containing the Miami Chase fix. The recorded flight returns to the frigate and
lands. No additional compatibility correction is needed for this sequence.
The [evidence record](COPPERSTART_DESERT_STRIKE_2026-10-19.json) binds the exact
ROM, all three media entries, input sequences, execution binaries and captures.

## Machine, media and fresh ROM

The machine is a PAL OCS A500: 68000, 8371 Agnus, 8362 Denise, 512 KiB Chip plus
512 KiB A501 RAM, no Fast RAM and one read-only DF0. The supplied three ADFs are
901,120 bytes each and have no crack/trainer label. This result applies to their
recorded hashes, without a claim about other dumps or releases. Disks two and
three are extracted byte-for-byte for the scripted swaps; media is not modified.

The ROM's five root sources and eleven component DLLs remain frozen from the
Miami Chase-qualified composition, at producer source
`9a911bb9887d130e1b72360fab65c132bcaeee16`. It retains the public video-frequency
fields, Lotus 2 supervisor stack and preceding DOS, memory and caller-data fixes.
Disk three was supplied after the build inputs were frozen; its separate media
record binds it to the later replays. No compiler input changed during the build.

The native compiler and graphics linker ran anew, completing at `2026-10-18T21:42:22.8955836+00:00`.
The 512 KiB image is SHA-256
`13BE91C7056B79AC57089AB0553353B4C693EC4ACDBA77E4A7D4289C1FC6777B`, with 451,340 code bytes and 26,098 graphics bytes at `$FF8000`.
The native root passes its compatibility gate with zero managed allocation sites
and zero framework features. The image is byte-identical to the preceding
Miami Chase/Lotus Esprit qualification ROM. Earlier regression evidence remains
applicable; earlier replays are not counted as newly executed tests here.

The unchanged frozen runner uses `Copper68k 1.5.4-nitro-dev.2`. Both actual guest
ROMs execute on the same hardware. `replay.py --firmware native` suppresses private
bootstrap-receipt interpretation after game takeover; the explicit `--rom` path
selects the firmware. No host OS shim, guest-state patch, title-specific engine
rule or defensive-check change is introduced. This frozen standalone composition
has separate scope from the moving-main ROM and standard-app artifact admission.

## Recorded disk and input sequence

The [play sequence](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-desert-strike-play-v1.json)
advances the intro and selects the first campaign. DF0 changes from disk one to
two at field 18,120, to three at 26,120, then back to two at 30,120. Every swap
has an explicit 120-field eject interval. Both ROMs request the return to disk two
after loading disk three; omitting it leaves a normal disk request on screen.
The earlier [end-stop record](DESERT_STRIKE_END_STOP_2026-09-24.md) remains the
historical correction. Its then-unavailable third-disk continuation now has
bounded native and CopperStart evidence.

The recorded backward joystick input starts at 35,000 and lifts the helicopter
off the frigate. Reversing direction returns it over the ship, where it lands.
The [idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-desert-strike-idle-v1.json)
keeps every startup and media event identical, then leaves flight controls
released. Input indices are zero-based; capture labels count completed fields.

Reviewed complete viewports show the title/intro, disk-two request, campaign
menu at 20,000, briefing at 22,000, disk-three request, return disk-two request,
and the frigate by 32,000. At 35,600 and 36,000 the controlled helicopter is
visible over the sea; the idle control remains on deck. The later sequence
returns to the frigate and lands. Captured guest positions support the visual
response: the controlled object moves from `(900, 1990, 11)` to approximately
`(2204.61, 2642.20, 20)` at 36,000, then returns to the ship. These are logical
16.16 guest coordinates, not physical measurements. No persistent graphics
fault or hang is observed in the reviewed sequence.

A repeated PC in the ship scene is the game's ordinary per-frame render wait;
its frame counters continue advancing. Takeoff needs backward input. The earlier
forward-only exploration therefore establishes neither a CPU stall nor a missing
ROM service. The fire presses at 38,200 and 41,000 occur after landing and
do not establish weapon operation; the captured weapon counter remains unchanged.

## Verification

| Check | Result |
| --- | --- |
| Kickstart normal/scalar, 44,000 fields each | All 1,105 raw files at 221 checkpoints and final CPU/hardware/output/cycle fingerprints match. |
| Fresh CopperStart normal/scalar, 44,000 fields each | All 1,105 raw files and four fingerprints match within the pair. |
| Native idle control | All 875 prefix files before the first flight input match; first different image at 35,200. |
| Fresh-ROM idle control | All 875 prefix files match; first different image at 35,200. |
| Preceding versus fresh CopperStart | All 1,105 play captures and four fingerprints match. |
| Exact fresh-ROM guest probes | Video frequencies and inherited VBlank/supervisor stack: 4/4 pass, zero skips. |
| Production Release build | Pass, zero warnings and errors. |
| Frozen native builder | Pass, zero errors; 18 expected CS0436 warnings from root types shadowing frozen component types. |

15 of 17 selected native/CopperStart complete RGB images match exactly.
The differences are the title at 2,000 and intro at 8,000; all selected later
menu, loading, flight and landing viewports match. Differing dynamic images
remain recorded without cropping, scaling or shifting to force identity.
This is separate from strict within-ROM CPU-mode parity.
Sampled PCM is nonzero; listening and audio fidelity were not assessed.

## Limits and reproduction

Coverage is three-disk startup and the recorded opening takeoff, near-ship flight
and landing. Weapons, combat, mission completion, later campaigns, save/load,
alternate media and NTSC remain unverified. No physical timing or host throughput
acceptance result is claimed. Runner completion alone is not a compatibility pass.

Extract the exact disk-two and disk-three entries to ignored
`media/copperstart-desert-strike-disk2.adf` and
`media/copperstart-desert-strike-disk3.adf` for the retained input paths. Use
`scripts/probes/CopperStart/replay.py` with the recorded ROM, disk-one selector,
frozen runner, retained input and `--firmware native --frames 44000 --interval 200
--slow-ram-kib 512`. Repeat cold boots in separate directories with `--scalar-cpu`
and the idle input. Local ignored evidence is retained under
`artifacts/copperstart-desert-strike-2026-10-19/`: frozen inputs, compiler outputs,
commands, complete captures, original-size viewing copies, read-only guest-state
observations and test results. ROMs, game media and build artifacts are not committed.
