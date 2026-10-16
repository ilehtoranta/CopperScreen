# CopperStart compatibility: Super Cars II

Case 4, 2026-10-16. The fresh CopperStart ROM reaches Super Cars II's first race,
responds to acceleration and steering, and advances the lap counter from five to
four. No additional firmware defect was demonstrated in this sequence, so no
firmware change or title-specific workaround was added.

The [machine-readable record](COPPERSTART_SUPERCARS2_2026-10-16.json) preserves
the exact ROM, producer assemblies, media, input, test results, capture hashes
and reviewed milestones. The previous case is
[Desert Dream](COPPERSTART_DESERT_DREAM_2026-10-16.json).

## Fresh ROM and method

The full managed native-test project was rebuilt from clean CopperStart commit
`33eef25141c4f92558b2ea6e4dae3d89c5be040c`, including caller-data audit
`2645cb5d9504bfd3427d31c53baffcce416f7fba` and the earlier memory-limit/checksum
fixes. Isolated producer outputs were frozen before native ROM compilation;
concurrent later source edits are excluded. The build succeeded with one
existing compiler nullable warning. Local development compiler/SDK inputs are
identified by their assembly hashes; nothing was published.

The resulting 512 KiB image has SHA-256
`ced694c1acc8d1ffe13e6c5a755741da11cf9aa3b12eb6ab95571ef72cad1e83`
and 447,342 bytes of generated code. The compiler reports converged optimization,
zero managed allocation sites and zero runtime features. This image differs
from the previous case's ROM, which did not include the caller-data audit.

The unchanged, frozen Lightweight runner boots each external ROM normally on a
PAL OCS 68000 A500 with 512 KiB Chip RAM and 512 KiB slow RAM. The tested media
is the Flashtro variant of Super Cars II disk 1. Native Kickstart 1.3 and both
CopperStart CPU modes use the same
[recorded input sequence](../../CopperScreen.Lightweight.Tests/Workloads/corpus-supercars2.json).
No host OS services, guest-PC injection or media modifications are involved.

## Results

- Native Kickstart and CopperStart pass the title/intro screens, one-player Easy
  options and race briefing, then enter race 1. Held acceleration moves the
  viewport and speed meter; the scripted steering interval changes the race
  view; four laps remain by field 18,400. The speed bar empties after release at
  field 18,500. Early AI positions differ between ROMs; exact native-ROM output
  equivalence is not claimed.
- Both CopperStart runs complete 19,000 fields. All 480 batch/scalar capture
  files are identical: 96 sets of image, state, Chip RAM, slow RAM and PCM.
- All 89 selected caller-data audit cases pass: 82 semantic and seven compiled
  MC68000 cases. This component recheck is separate from native ROM coverage.
- All 27 native boot/device tests and 22 native DOS/script/console tests pass
  against the fresh producer/image, with no failures or skips. The DOS tests
  validate producer hashes before reusing the image.
- Independent boot-block probe replays, with and without slow RAM, preserve the
  physical memory limits, `$FFFF` ExecBase checksum sum, CIA B DDRB `$FF` and
  ADKCON `$1100` fixes. The native probe controls remain in case 3.

The same fresh ROM also retains the preceding three titles' reviewed sequences:

| Title | Fields | Comparison with case 3 |
| --- | ---: | --- |
| Lotus Turbo Challenge 2 | 14,000 | All 71 images and PCM fields, plus final CPU/hardware/output fingerprints, match; opening race and acceleration response retained. |
| Arte by Sanity | 40,000 | All 101 images and final CPU/hardware fingerprints match; later 3D scenes, starfield and intro return retained. |
| Desert Dream by Kefrens | 72,000 | All 73 images and PCM fields, plus final CPU/hardware/output fingerprints, match; disk II loads and closing credits are reached. |

Arte's sampled PCM at field 3,200 differs by at most 33 on the signed 16-bit
scale; the other 100 PCM captures match. Its cumulative output fingerprint also
differs. This is disclosed evidence, not exact cross-ROM audio parity.

## Scope and local evidence

This qualifies the recorded first-race sequence and machine, not a completed
race or game. Two-player mode, other levels, disk 2 and alternate media remain
unverified. Listening and audio fidelity were not assessed. Separate Utility,
Icon, Layers and retained-host audit paths are not all reachable from this
native boot root; the broader audit's tests are distinct coverage. The full
portable suite was not rerun.

Local artifacts are under
`artifacts/copperstart-supercars2-2026-10-16/`, including the fresh ROM in
`audited-rom/`, native control, both game runs, regression captures and test
results. ROMs, media and build artifacts remain untracked. These are compatibility
results, not throughput acceptance measurements.
