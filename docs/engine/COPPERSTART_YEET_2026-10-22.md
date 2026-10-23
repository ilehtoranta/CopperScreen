# YEET: packed startup and prologue

The [subsequent main integration and first-puzzle qualification](COPPERSTART_YEET_GAMEPLAY_2026-10-23.md) supersedes the integration/gameplay limits below. This record preserves the original pinned candidate and its results.

YEET 1.0.4 now boots, displays its menu and advances through the prologue on a
fresh 512 KiB CopperStart ROM containing the Miami Chase fix. The idle control
stays at the menu. Puzzle boards, map selection and game completion remain
unverified. The [evidence record](COPPERSTART_YEET_2026-10-22.json) binds the ROM,
media, compiler components, execution binaries, inputs and captures.

## Scope and frozen inputs

The supplied `yeet.adf` is 901,120 bytes, SHA-256
`FE52FC8BAC2D0D24620BD425023BFC839160A5B5BBD423A9891508098653D2A8`.
Its OFS startup runs `s/info` and `YEET`; both are S404-packed executables with
adjacent CODE hunks. The native control uses Kickstart 1.3. Both execute on the
same frozen PAL OCS 68000 runner, with 512 KiB Chip RAM, 512 KiB A501 RAM, no
Fast RAM and one read-only DF0. No guest data patch or host OS shim is used.

The historical Miami Chase composition was compiled anew first, producing
`13BE91C7056B79AC57089AB0553353B4C693EC4ACDBA77E4A7D4289C1FC6777B`.
It does not reach YEET startup. The newer source baseline `ac0e78a` then reports
DOS BadHunk 235. The final corrections are committed at CopperStart
`1520f304be7624e81eacf5d2810ffaf612cb2385`, using the recorded compiler
`d99e717607c02e72fa7567b3170834f21976dc44`.

The final image is SHA-256
`083392F3A7C176EAAB681F02BDBA47567A269C3285055970581FCE528443D2AF`.
The fresh CIL compiler produced 476,484 code bytes; the standard native linker
adds 32,378 graphics bytes at `$FF8000` and the native Intuition/services.
The compatibility gate passes with zero managed allocation sites and zero
framework features. The JSON records the raw compiler image and every frozen
component hash. Final native-emitter changes were composed with the standard
linker; the ROM was not hand patched.

CopperStart main received a concurrent 60-file integration at `67590f5` during
qualification. [Source PR 17](https://github.com/ilehtoranta/CopperStart/pull/17)
preserves the tested composition and needs integration and requalification
before merging. This record qualifies that pinned candidate, not moving main
or the entire application ROM admission path.

## Compatibility corrections

The [producer investigation](https://github.com/ilehtoranta/CopperStart/blob/1520f304be7624e81eacf5d2810ffaf612cb2385/Docs/YeetCompatibility20261022.md)
describes the source changes and service limits:

- Accept intermediate implicit HUNK boundaries and expose actual public segment
  allocation headers. Packers can replace hunks and `cli_Module` without an
  allocator-owned prefix; startup unloads the completed module.
- Preserve scratch registers across Disable/Enable/Forbid/Permit. A changed D0
  had turned a valid following allocation into AllocMem(0).
- Add the native keyboard device, shared serial/ring ownership, console library
  unit and RawKeyConvert, named Exec opens and public Screen inquiries.
- Reduce oversized DOS service/command stacks. Retire the early reset stack only
  after DOS commits to its permanent wait, retaining the live supervisor stack
  and publishing correct public bounds/checksum. Otherwise the unpacker falls
  back to Chip RAM and a later graphics allocation fails.
- Make LoadView(NULL) run a blank Copper list with Copper DMA enabled. The native
  reference does this; CopperStart had left music/loading running behind grey
  video because YEET replaces COP1LC without re-enabling that DMA channel.

The ROM remains 512 KiB by sharing identical topaz glyphs and using equivalent
MOVEQ encodings. All 224 glyphs and the service/font storage boundary are tested.
Public caller data is not admitted by message length, zero links or private
allocation markers. Private owned lifetimes, real allocation failures and
hardware limits remain enforced. The emulator engine and CPU package are unchanged.

## Replay and verification

The [prologue workload](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-yeet-prologue-v1.json)
holds fire for 20 fields at each recorded point. The first visible input effect
is at 9,200; the earlier 6,500 press occurs during startup. At 18,000 the controlled
run shows the departure scene, while the [idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-yeet-idle-v1.json)
remains at the menu. Native startup is faster; cross-ROM matching uses complete
scene images at their respective field numbers, not equal boot times. Seventy-one
captured CopperStart images match a complete native image byte-for-byte,
including repeated blank/transition frames and the reviewed prologue scenes.

| Check | Result |
| --- | --- |
| Fresh-ROM normal/scalar, 18,000 fields each | All 455 raw files at 91 checkpoints match; final CPU, hardware, output and cycle fingerprints match. |
| Idle control, 18,000 fields | Same prefix through 9,000; first different image at 9,200; menu retained at 18,000. |
| Native-reference ABI probes | Packed hunks, register preservation, replacement modules, keyboard/matrix, Screen copy, raw keys, Exec opens and null-view DMA covered. |
| Focused Exec/DOS semantic tests | 68 passed, zero failures/skips. |
| Final firmware/component regression selection | 95 passed, three failures, zero skips. The three failures reproduce on the unchanged baseline. |
| Producer Release solution | Passed; four existing SDK preview-resolution NU1601 warnings. |
| CopperScreen production Release solution | Passed, zero warnings/errors. |

The retained failures are the two old empty-display/stub-provider expectations
and the fixed-address read case at `$10000`. They are recorded failures, not
successful coverage. The separate permanent blank-list allocation changed four
component lifetime assertions; those now verify that exactly the library's one
blank list remains and repeated blanking reuses it. All twenty display/Intuition
component and lifecycle cases pass after that test correction.

These are diagnostic compatibility replays, not host-throughput acceptance or
complete game coverage. The JSON separates native guest probes from component
tests and records their distinct frozen execution binaries. Reproduction uses
`scripts/probes/CopperStart/replay.py` with the explicit ROM/media paths,
`--slow-ram-kib 512 --frames 18000 --interval 200`, the workload above and the
recorded runner. `--firmware native` suppresses private bootstrap-receipt decoding
after takeover; the explicit `--rom` selects the actual firmware.
