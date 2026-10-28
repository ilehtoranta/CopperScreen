# Miami Chase: native boot frequency fields

2026-10-18. Miami Chase reaches its PAL intro, disk-two menu and input-driven
opening street scene on a fresh CopperStart ROM containing the Lotus 2 stack fix.
The [evidence record](COPPERSTART_MIAMI_CHASE_2026-10-18.json) binds the exact ROM,
unchanged supplied media, execution binaries, input, probes and reviewed images.

The machine is a PAL OCS A500: 68000, 8371 Agnus, 8362 Denise, 512 KiB Chip RAM,
512 KiB A501 RAM, no additional Fast RAM and one DF0 drive. The supplied archive
contains an unlabeled disk one and a disk two labeled `[cr AGL]`; this result is
limited to those exact two ADFs. Both extracted entries are 901,120 bytes. No
media bytes are changed.

## Failure and correction

The original loader copies ExecBase's `VBlankFrequency` byte at offset `$212`
to low RAM `$100`. At `$1758` it compares that byte with 50. Any other value takes
its NTSC branch and bypasses the PAL-only intro. Native Kickstart 1.3 publishes
50 in both `VBlankFrequency` and `PowerSupplyFrequency`; the preceding CopperStart
ROM leaves both bytes zero, although its own physical video probe correctly
reports PAL. The preceding image skips the intro and remains at the disk-two
prompt through 18,000 fields despite the recorded swap and fire presses.

An original 68000 boot-block probe reads both public byte fields using the
boot-supplied A6. Kickstart passes with and without A501 RAM. The preceding ROM
and a freshly compiled unchanged Lotus 2 baseline fail both cases, reporting
zero. A completed replay alone does not establish compatibility.

`NativeDiskBoot` now publishes those fields from the existing bootstrap video
standard before passing control to disk code: 50 for PAL and 60 for NTSC.
An unknown probe result remains unclassified. No caller-data validation,
allocator ownership requirement or title-specific execution rule is added.
With the isolated frequency correction, the PAL intro plays and disk two reaches its
menu at field 11,000. That handoff succeeds before the failing-baseline and corrected
scripts first differ at field 11,200. No disk bytes, guest state or ROM bytes were
patched.

The fresh corrected image was compiled at `2026-10-18T19:52:16.2277079+00:00`. Its 512 KiB ROM is
SHA-256 `13BE91C7056B79AC57089AB0553353B4C693EC4ACDBA77E4A7D4289C1FC6777B`, with 451,340 code bytes and 26,098 graphics bytes at
`$FF8000`. The native root passes the compatibility gate with zero managed
allocation sites and zero framework features. The fresh unchanged baseline is
SHA-256 `3C1AE78F3EE089407A1C056D749F7D1A7CC69BAC864EE80FFDAB02ABE10AE16B`.

Only `NativeDiskBoot.cs` changes in the qualified native composition. Eleven
component DLLs, four other root sources, compiler/SDK, native compile options,
graphics composer and all execution binaries remain frozen from Lotus 2. The
source inputs were frozen before the fix was committed; their file hashes match
qualified source commit `9a911bb9887d130e1b72360fab65c132bcaeee16` and the narrow
current-main backport `ee37f0b2b9b3f00bfeb1615da571f89a1e9f0f5a`
([source PR 12](https://github.com/ilehtoranta/CopperStart/pull/12)).
The earlier fresh-build directory retains the originally proposed Desert Dream
name; that title was already qualified and was replaced by Miami Chase before
any new media replay. Frozen compilation inputs were not edited during the build.

## Replay and validation

The unchanged headless runner boots each actual guest ROM on the same hardware.
`replay.py --firmware native` suppresses interpretation of private bootstrap
receipts after takeover; it does not select the ROM or provide host OS services.
The pinned CPU package remains `Copper68k 1.5.4-nitro-dev.2`.

The [play sequence](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-miami-chase-play-v1.json)
exits the intro, swaps DF0 to disk two, starts within the menu's active window,
then applies directions and fire. The
[idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-miami-chase-idle-v1.json)
uses identical startup events and leaves the gameplay inputs released.
Local replay scripts resolve only the disk-two path to the exact extracted entry;
all input events and their fields remain identical to the retained scripts.

Both Kickstart and corrected CopperStart show the intro credits, portrait briefing,
disk-two menu and opening street gameplay through field 22,000. The idle controls
establish the response to the recorded steering sequence. Seven of eight selected
full-viewport RGB images match between ROMs: the menu at 11,000 and 11,200, and gameplay
at 14,000, 14,600, 15,600, 18,000 and 22,000. The field-13,000 transitional wipe
differs. These cross-ROM observations are separate from the stricter equality checks
within each CPU-mode pair.

| Check | Result |
| --- | --- |
| Kickstart normal/scalar, 22,000 fields each | All 555 raw files at 111 checkpoints and final CPU/hardware/output/cycle fingerprints match. |
| Corrected CopperStart normal/scalar, 22,000 fields each | All 555 raw files at 111 checkpoints and final fingerprints match. |
| Native input control | All 350 raw files before gameplay input match; first different image at field 14,600, after steering begins. |
| CopperStart input control | All 350 prefix files match; first different image at field 14,600. |
| Original frequency boot probe | Native 2/2 and corrected 2/2 pass; preceding and fresh unchanged images each fail the two expected zero-frequency cases. No skips. |
| Retained exact-ROM guest probes | 19/19 pass: Exec/memory/IRQ, caller-owned graphics storage, relative DOS reads, HUNK allocation flags, inherited VBlank, upper-Chip working area, boot returns/retries and non-DMA reads. No skips. |
| Focused bootstrap host tests | Qualified source 13/13; current-main backport 15/15. No skips. Host tests are separate from native ROM coverage. |
| Release builds | Consumer and qualified source pass without warnings; current-main source passes with four existing development-dependency warnings. |

The fresh-ROM Lotus 2 regression retains all 161 earlier images and sampled PCM files
across 16,000 fields. The opening race reaches 61 MPH and 47,890 points at field 13,000,
with the later title return retained at 16,000. Arte retains all 101 earlier images and
sampled PCM files through 40,000 fields, including credits at 24,000, the textured scene
at 32,000 and the intro return at 40,000. Some state and RAM captures differ with the
firmware change; full cross-ROM capture identity is not claimed.

Source CI for the exact backport head is unavailable: all 13 jobs have zero
executed steps. GitHub reports that an account payment or spending-limit
restriction prevented them from starting. This is not a successful CI run.


## Limits and retained evidence

This qualifies the recorded opening sequence and its input response. Later
stages, two-player modes, a complete game, alternate media and physical NTSC
hardware remain unverified. Independent mains/video frequencies are outside
the tested PAL machine. Full moving-main ROM qualification and standard desktop
admission are separate from the frozen standalone composition tested here.
Selected complete viewports were reviewed without cropping or resizing;
transitional wipes are not persistent corruption. Audio is present in captured
fields, but listening, fidelity, exact hardware timing and host throughput were
not assessed. No ROM/media patch, host OS shim, CPU/engine change or package
publication is part of this work.

Local ignored evidence is under `artifacts/copperstart-miami-chase-2026-10-18/`,
with the fresh unchanged compiler outputs in the adjacent
`artifacts/copperstart-desert-dream-2026-10-18/`. The record includes hashes and
paths for the frozen sources, assemblies, manifests, replays, input controls,
test results and full-viewport viewing copies. ROMs, media and build artifacts
remain untracked.
