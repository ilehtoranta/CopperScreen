# Lotus Turbo Challenge 2: boot-time interrupt stack compatibility

2026-10-18. Lotus Turbo Challenge 2 reaches its title, menus and input-driven
opening race with a fresh CopperStart ROM containing the Major Motion fixes.
The [evidence record](COPPERSTART_LOTUS2_2026-10-18.json) binds the exact ROM,
unchanged supplied media, execution binaries, input scripts, probes and images.

The [October 24 gameplay continuation](COPPERSTART_LOTUS2_GAMEPLAY_2026-10-24.md)
crosses the first checkpoint with the later rebuilt ROM, then separately uses
the game's password field to load the Night Course and exercise pause/resume.
Natural course completion and the second checkpoint remain unqualified.

The machine is a PAL OCS A500: 68000, 8371 Agnus, 8362 Denise, 512 KiB Chip RAM,
512 KiB A501 RAM, no additional Fast RAM and one DF0 drive. The supplied archive
contains one complete 901,120-byte ADF. No media bytes are changed.

The preceding Major Motion image loses control before the title. The disk reads
and first unpacking stage match native Kickstart. Its next copy places the packed
stream at `$772BD-$7EFFF`, overlapping CopperStart's reset-time supervisor stack.
A vertical-blank interrupt saves its six-byte exception frame and 60 register
bytes at `$77F2A-$77F6B`. Those 66 changed stream bytes corrupt both subsequent
unpacking stages. The jump to `$5F10C` consequently finds zeros; native Kickstart
finds the expected loader instructions and continues. Native Kickstart's SSP is
in A501 RAM. The bounded diagnostic traces and stage hashes are in the record.

An original boot probe fills `$77000-$77FFF`, leaves the ROM interrupt dispatcher
running for several vertical blanks, then checks every word. Kickstart passes;
the preceding ROM reports corruption in the longword ending at `$77F2C`. The
fresh unchanged baseline reproduces that failure. A completed headless run by
itself is not a compatibility pass.

NativeDiskBoot now allocates an 8 KiB public guest supervisor stack using the
existing memory-list preference. Fitted A501 RAM is preferred, with ordinary
Chip allocation as the fallback. The launch code saves the reset continuation,
sets the separate user and supervisor stacks, and builds its RTE frame on the
allocated SSP. Boot retries and the DOS completion reuse that stack. The return
entry moves from offset 88 to 96 to leave room for the longer launch sequence.
The reset-time Chip stack remains reserved. No caller-data validation or
ownership requirement is added.

The corrected image was freshly compiled at `2026-10-18T18:17:50.6705845+00:00`. Its 512 KiB ROM is
SHA-256 `3C1AE78F3EE089407A1C056D749F7D1A7CC69BAC864EE80FFDAB02ABE10AE16B`, with 451,064 code bytes and 26,098 graphics bytes at
`$FF8000`. The root has zero managed allocation sites and zero framework features.
The freshly compiled unchanged baseline is byte-identical to Major Motion's
`9F938923B7E58DF756D6EB6E7A6E50C7A5BC901EFA8FB6F7AB3ABA5D2277290C`.

Only NativeDiskBoot.cs changes in this bounded native composition. Eleven
component DLLs, four other root sources, compiler/SDK, native compile options,
graphics composer and all execution binaries remain frozen. The CPU package is
`1.5.4-nitro-dev.2`. The qualified source is
[`ab86f46`](https://github.com/ilehtoranta/CopperStart/commit/ab86f46784ba5f22b8e8a82a42c4d726c1a4e075);
the current-main backport is [CopperStart PR #11](https://github.com/ilehtoranta/CopperStart/pull/11).
This does not qualify the complete moving-main composition or change standard
application ROM admission. The image retains the Major Motion DOS/CWD,
sequential-read, HUNK Chip/Fast and boot-VBlank repairs, plus the earlier
Overdrive, Alien Breed, F17, Lemmings and caller-owned trap-zero corrections.

Both ROMs use the existing [Lotus 2 corpus input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus2.json):
fire at fields 6,020, 9,020 and 9,600 advances the title/menu/start sequence;
driving begins at 11,520. The separate [idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-lotus2-idle-v1.json)
keeps all three start inputs and omits driving. Both positive CPU-mode pairs
run 16,000 fields and match all 805 raw capture files at 161 checkpoints,
including full images, CPU/device state, Chip/A501 RAM and sampled PCM. All four
execution fingerprints match within each pair. Cross-ROM identity is not a gate.

Native Kickstart and CopperStart each have 580 identical pre-driving control
files; their first differing captured image is field 11,600. Full 908x313 viewport
review confirms acceleration, steering, changing road position and scoring.
At field 13,000, both positive runs show 61 MPH and 47,890 points; each idle control
shows 9 MPH and zero points. The sampled title, menu, countdown and early driving images
agree with the native control. The race timer expires and both runs return to the title
by field 16,000; stage completion is not claimed. At the later shared field 14,000, both
show 9 MPH and timer 11, with slightly different road position and scores of 77,930
under Kickstart and 77,940 under CopperStart. Cross-ROM trajectory identity is not
required.
The bounded run covers opening play; it does not establish later-stage or
full-game completion. The complete viewport is preserved without crop or scale;
labelled JPEG viewing copies supplement the authoritative raw captures.

Nineteen exact-ROM guest cases pass with zero skips: six Exec/memory/interrupt
cases, two graphics/caller-storage cases, four relative DOS cases, one Chip-hunk
case, two boot-VBlank/working-area cases and four boot-return/retry/native-I/O
cases. Host semantic coverage is separate: 13 focused bootstrap cases pass on
the qualified lineage and 15 on current main, with zero skips. Both source
solution builds pass; the qualified build has zero warnings and current main
retains four development-dependency warnings. The consumer production Release
build has zero warnings and errors.

The same corrected image also completes the retained 46,000-field Overdrive,
40,000-field Arte, 18,000-field Major Motion and 24,000-field Lemmings scenarios.
Overdrive retains qualifying movement and steering. Arte reaches its credits and returns
to the intro, preserving the large-allocation workload. Major Motion reaches opening
play and a high-score result; its new trajectory differs from the preceding ROM.
Lemmings rescues 100% in level one. These are bounded replay regressions, not full-game
or physical timing qualifications.
No engine/CPU implementation, title-specific hook, ROM/media byte patch or host
OS shim is introduced. Audio fidelity, physical hardware timing and host
throughput acceptance are unverified. Source GitHub Actions cannot start its 13
jobs because of the account payment/spending-limit restriction; all have zero
executed steps. This is unavailable CI coverage.
No ROM, media, local build artifact or package is committed or published.
