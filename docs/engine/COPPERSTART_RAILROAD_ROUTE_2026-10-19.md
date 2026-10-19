# CopperStart: Railroad Tycoon route and arrival

2026-10-19. Train #1 runs its two-stop route and arrives at London Crossing.
The June 14, 1830 newspaper announces: **First Train Arrives in London Crossing.
Citizens Celebrate!**

[The companion JSON](COPPERSTART_RAILROAD_ROUTE_2026-10-19.json) pins the ROM,
source, media, diagnostic runner, input, captures and complete mode comparison.
This continues the [purchase/report qualification](COPPERSTART_RAILROAD_GAMEPLAY_2026-10-18.md).

## Verified gameplay

Fresh normal and scalar runs start from reset with the exact rebuilt CopperStart
ROM. Each complete 242000-field purchase prefix matches its retained predecessor
across all 1215 capture files. The continuation then performs ordinary physical
input to extend the existing line, build London Crossing, edit the train's route
and select Slow speed. Read-only inspection corroborates the two station
coordinates, (101,42) and (105,42); it does not modify the guest.

| Field | Reviewed observation |
| --- | --- |
| 265000 | Area display locates London Junction's existing line |
| 280000 | Detail view returns to the existing station/track |
| 283000 | Four eastward construction inputs extend the line to the new endpoint |
| 292000 | London Crossing Depot report |
| 300000 | Both depots and connecting track are visible |
| 308000 | Route-editor hover identifies London Crossing |
| 311000 | Train #1 lists London Junction and London Crossing as scheduled stops |
| 313000 | Frozen speed is visibly selected before starting the run |
| 314100 | Input commits Slow speed |
| 314200–319000 | Train position changes along the line; calendar advances |
| 321000 | Complete newspaper announces arrival at London Crossing on June 14, 1830 |

Cash in the game's read-only WORD at $C3E0E4 falls from 829 to 801 to 751
(thousands): $28,000 for the extension, then $50,000 for the second depot. The
frozen header can retain an older amount; the rendered extension and station
report independently establish successful construction.

F4 centers the new display on the pointer. The Area display was used to locate
the existing line after a zoom centered elsewhere. Incomplete, shifted and black
redraw frames settle into complete displays; they are retained as transitions,
not silently counted as compatibility failures. The arrival paper is only partly
drawn at 320000 and complete at 321000.

## ROM and validation scope

CopperStart source remains clean at [1b83139](https://github.com/ilehtoranta/CopperStart/commit/1b8313985a9e8f95165e9cd973cd231af64e8a39).
No additional guest or engine code correction was required for this route.
The unchanged 524288-byte ROM SHA-256 is
`334cd0e9d2ad25410ab449f79e42e2b254e8085dbbf94e2d89c86f4db8b24d13`.
The preceding clean Release build, production-emitter parity, 311 component cases
and 98 exact-ROM cases remain earlier evidence; they are not relabeled as fresh
tests for this documentation/gameplay continuation.

Both runs stop cleanly at field 321000 with exit code zero. All **1920 capture
files per mode** match byte for byte: BMP, captured CPU/hardware/cycle state,
Chip RAM, slow RAM and PCM. The 384 captured fields include closely spaced
100-field checkpoints during the train run. No captured unsupported hardware or
video path is reported. Capture-set SHA-256:
`23242afd70ce58ec59f1b7784bbd6a1790fa15e453628f0535606a6b7686d6c0`.

The machine remains PAL OCS 68000, 512 KiB Chip plus 512 KiB slow RAM, one
read-only DF0 and RTC disabled, with the pinned Lightweight engine and Copper68k.
The guest executes its own OS and game code. The probe records and inspects
state without guest RAM/register injection or title patches.

The earlier record mislabeled the Train Report's $3,000 as locomotive value.
It is annual maintenance, as described by the [MicroProse manual's Train Reports
section](https://www.bestoldgames.net/download/games/railroad-tycoon-deluxe/railroad-tycoon-deluxe-dos-manual.pdf).
The engine picker separately shows a $15,000 purchase price. The earlier prose
and companion record are corrected with that clarification.

The train carries an empty Mail Car. Route setup, movement and arrival are
verified; freight revenue, completion of the return leg, a full campaign, saves
and audio fidelity remain unqualified. These are correctness replays, not host
throughput measurements. ROMs, media, local builds and captures are not committed.

The next original-media comparison is [Hired Guns](COPPERSTART_HIRED_GUNS_2026-10-19.md).
Its first CopperStart fault occurs in BCPL Echo startup before game entry;
Kickstart 1.3 reaches the main menu. That open issue does not change the verified
Railroad Tycoon route result.
