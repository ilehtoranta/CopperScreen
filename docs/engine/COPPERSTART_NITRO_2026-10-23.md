# CopperStart Nitro: extended ADF and first-race gameplay — 2026-10-23

Nitro now boots its supplied extended ADF with the rebuilt CopperStart ROM,
accepts a human driver, advances through name/car selection and reaches the
first race. Acceleration and steering work. Two independent compatibility
defects blocked this: CopperDisk rejected additional cylinders and empty raw
tracks, and Copper68k polled interrupts too late during memory BTST.

## Media and machine

The supplied archive is `Nitro (1990)(Psygnosis)[extended adf].zip`, 1072509
bytes, SHA-256 `5d6cc2705621d85f5869255057dc1f67fe939cd8677647a890e4fedbf38da4e4`.
Its single 1942886-byte ADF has SHA-256
`895c78b4d0bc07b0e9fc12b86bac6d893b84fe76173f3020f26f1c6e224891c7`.
The `UAE-1ADF` table contains 166 single-revolution raw tracks, including
zero-byte/zero-bit entries 147–165. Nonempty raw tracks retain their supplied
bytes and bit lengths. No conversion, trainer or guest-memory patch is used.

The unchanged 524288-byte CopperStart ROM comes from producer commit
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
The comparison ROM is native Kickstart 1.3, SHA-256
`ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53`.
Both use PAL OCS 8371/8362, the 68000 interpreter, 512 KiB Chip plus 512 KiB
slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313 raster.
The earlier [Worms DMA correction](WORMS_SPRITE_DMA_2026-10-22.md) is retained.
No CopperStart source correction or additional ROM rebuild was needed here.

## Corrections and discriminating evidence

CopperDisk originally required exactly 160 tracks, rejecting this image before
either ROM executed. Its preserved-track path now accepts 1–168 tracks, within
the engine's existing 0–83 cylinder range, and exposes the actual cylinder
count. An absent final head is padded as unformatted. Normal sector entries
beyond cylinder 79 encode their physical track number and remain readable
through the sector view. Standard 880 KiB ADF geometry is unchanged.

The next rejection was raw track 147's zero bit length. As in
[WinUAE's extended-ADF reader](https://github.com/tonioni/WinUAE/blob/master/disk.cpp),
an empty raw entry now supplies a zero-filled PAL DD revolution: 12668 bytes,
101344 bits. Declared backing bytes do not become magnetic data when the
declared bit length is zero. Format bounds, unsupported track types and
multirevolution limits remain explicit. These changes remove assumptions that
reject valid media; they do not add guest OS parameter checks.

Five added extra-cylinder loader cases and two empty-raw cases fail before
their respective corrections. The engine regression steps a prepared 166-track
image to cylinder 82/head 1 and verifies the retained raw track and bit length.

With disk admission repaired, both ROMs stalled in the same VERTB polling loop:
`BTST.B #5,$DFF01F` at `$184A8`, followed by `BEQ` at `$184B0`. The vertical
interrupt handler acknowledged VERTB before the next operand poll. An
instruction trace located the first timing discrepancy: Copper68k sampled IPL
on the final prefetch, after the INTREQR operand had already been read.
WinUAE's generated cycle-exact 68000
[BTST implementation](https://github.com/tonioni/WinUAE/blob/master/cpuemu_13.cpp)
samples after address extensions and before the operand read.

Copper68k now uses that earlier memory-BTST sample phase in static/dynamic and
planned/scalar paths. The mandatory final prefetch, Z update before that fetch,
waited-read retirement and nominal instruction cycles are retained. All 24
focused cases fail with the previous sample phase and pass with the correction,
including operand waits and interrupts arriving during those waits. This is
independent emulator implementation evidence, not physical CPU timing proof.
An earlier experiment sampling inactive interrupt pins at every engine boundary
did not resolve the stall and was reverted; no such engine change is retained.

The shared CPU correction is maintained in CopperMod commit
[`25dae803fa8476d601acc5c0af9bbac8ed98138f`](https://github.com/ilehtoranta/CopperMod/commit/25dae803fa8476d601acc5c0af9bbac8ed98138f).
The app, engine and runner pin the locally packed unpublished package
`1.5.5-nitro.1`; lock files retain its content identity. CopperDisk source is
`3.0.1-dev.1`. No package is published, no published package is replaced and no
sibling CopperMod project reference is introduced.

## Bounded replay

The [publisher manual](https://db.hfsplay.fr/files/2019/07/31/Manual_DE_ENG_FR_IT_l1JPE5W.pdf)
documents fire to join, left/right plus fire for initials and car selection,
and fire to accelerate. The recorded input selects joystick 0, enters three
initials and confirms the racing car. Port 0's host joystick-mode bit is set
in every joystick event.

| Completed field | Reviewed observation under both ROMs |
| ---: | --- |
| 5000 | Three-player selection screen; all drivers initially computer-controlled. |
| 5500, 6000 | Fire at 5002 joins the joystick-0 driver; name entry is active. |
| 6500, 7000 | Three fire presses confirm initials and reach racing-car selection. |
| 8000 | First-location skyline/loading presentation. |
| 9000 | First race, visible player car, street scenery, AI cars and gas counter. |
| 9300, 9500 | Held fire accelerates; right steering changes heading and camera position. |
| 9700, 10000 | Further left/right steering, release of acceleration, continued racing and gas consumption. |

The two ROMs reach different race positions at the same completed field; they
are compared for the bounded milestones, not cross-ROM raster identity.
The complete shared [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/nitro-copperstart-race1-v1.json)
contains 18 events. The final input and exit-control files are written before
process creation. Discovery and final qualification start independently from
reset. Each run retains state, both RAM banks, full raster and field PCM at
field 1 and every 100 fields through 10000.

## Verification and limits

All three final reset runs exit successfully at field 10000 without an
unsupported-operation report. Each retains 505 files across 101 captured
fields; every filename, size and SHA-256 matches its discovery run. CopperStart
normal/scalar output is byte-identical. Each run has 48 captured fields with
nonzero PCM. The JSON record retains the separate manifest hashes.

The production Release solution builds with zero warnings/errors. Disk tests
pass 81 cases. Engine diagnostics pass 1256 cases with seven optional native
cases unavailable; host tests pass 195 with six optional native cases unavailable;
desktop tests pass ten. Those skipped tests are not native replay coverage.
The non-synthetic shared CPU suite passes 4475 cases, including the BTST,
interrupt and prefetch checks; 19 optional external cases are unavailable. Its
CPU DLL matches the locally packed package and final replays. The additional
full architectural-matrix run was intentionally stopped after 12m56s and is
unavailable coverage, despite the test tool's partial "Passed!" summary.
No passing full CPU suite or release qualification is claimed.
The [machine-readable record](COPPERSTART_NITRO_2026-10-23.json) retains final
scoped CPU results, the interrupted attempt, actual commands, binary/package/media identities,
source file hashes, capture manifests and reset/discovery comparisons.

The frozen functional harness is retained; it is not a throughput harness.
Nonzero PCM establishes sample presence, not listening or audio fidelity.
ROMs, media, packages, captures and build outputs remain local. Historical
performance inputs, fingerprints and acceptance records are unchanged.

This qualifies the supplied image's boot, interactive selection and bounded
first-race driving. Race/lap completion, later tracks, multiplayer, saving and
other Nitro releases remain unqualified. Native Kickstart runs on the same
engine; the WinUAE diagnostic has no field-matched input/capture comparison.
Neither is a physical hardware oracle or general cycle-accuracy certification.
