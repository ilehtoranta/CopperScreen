# CopperOS A1200 desktop boot optimization — 2026-10-25

This retained predecessor is followed by the
[graphics](COPPEROS_DESKTOP_GUI_SPEED_2026-10-25.md) and
[blitter/repaint](COPPEROS_DESKTOP_BLITTER_2026-10-25.md) qualifications.

The qualified development firmware reduces cold desktop startup from 2,739 to
1,763 PAL fields: **54.867 to 35.316 modeled guest seconds, 35.6% less time**.
The HDF, desktop/GUI program bytes, machine configuration and graphics ROM bank
are unchanged. The completed Home framebuffer is byte-identical. These are guest
work measurements, not host FPS, physical A1200 measurements or an installed
release/Store qualification.

## Measured boundary

The [diagnostic probe](../scripts/probes/CopperOsBoot/README.md) uses the normal
PAL Alice/Lisa, 68EC020 interpreter, 2 MiB Chip RAM, no slow/Fast RAM and an exact
DOS1 FFS HDF on unit 0, with empty floppy drives. Readiness requires the native
`CopperOS.Desktop` public port, its waiting task, an empty message queue and the
GUI input worker's completed wait state. ROM startup, loading, initialization,
first paint, scheduling and hardware time are included.

| Milestone | Original guest seconds | Optimized guest seconds |
| --- | ---: | ---: |
| GUI HUNK loaded | 25.26 | 9.64 |
| Desktop HUNK loaded | 29.67 | 11.64 |
| Desktop public port | 30.83 | 12.04 |
| Intuition installed | 31.15 | 12.28 |
| Desktop screen | 34.94 | 15.50 |
| Desktop window | 41.61 | 22.04 |
| First ready desktop | 54.87 | 35.32 |

Module discovery has a 20-field interval; public readiness is sampled every field.
The source of elapsed time was established with instruction-level guest-cycle
sampling, not inferred from host process CPU use. The baseline attributes 6.92 s
to the filesystem read routine and 2.87 s to its byte writer, 5.16 s to DOS clear,
4.76 s to Exec clear and 3.97 s to Exec copy. These are exclusive costs, not
inclusive call times. Unmapped native code remains unattributed. Setup and first
painting still account for substantial remaining startup time.

For both ROMs, the observer and independent public `ExecuteFrame` control match
the exact completed field/cycle counts, CPU registers, Chip RAM, framebuffer and
PCM. No observer callback or counter was added to the production engine.

## Native changes

The producer changes are in CopperStart:

- `CopperSharpRomMemoryPlatform.Clear` peels an odd leading byte, clears aligned
  longwords in groups, and retains exact byte tails. MC68000 longword accesses
  require even alignment, not four-byte alignment.
- `CopperSharpRomMemoryPlatform.Copy` retains the original forward/backward
  overlap behavior. Matching parity permits longword groups with head/tail bytes;
  opposite parity stays byte-wide. Backward groups retain descending store order.
- `CopperSharpNativeDosPlatform.Clear` reuses that native clear primitive.
- `AmigaDiskFileSystemCore.Read` copies each already-validated sector payload as
  a span. Overlapping or wrapping caller/scratch ranges retain the old forward
  byte loop. Header/data validation, OFS checksums, verified-prefix errors,
  pointer-table traversal, offsets, lengths and filesystem writes are unchanged.

No CPU package, hardware scheduling, bus timing, display geometry or host DOS
shim changes are involved. No unthrottled boot mode or faster fitted CPU is used.

## Frozen build and identities

The original 2026-10-22 compiler, SDK, System, Devices, SFS and runtime inputs
are retained. The original Exec/DOS source set was reconstructed using portable
PDB SHA-256 document identities and retained source snapshots. The loader source
was recovered semantically: the complete reconstructed build reproduces the
original compiled reset bank and combined ROM byte-for-byte, including all method
bodies, layout, vectors and checksum. This prevents unrelated current producer
work from entering the candidate.

| Artifact | SHA-256 |
| --- | --- |
| Original 1 MiB ROM | `e7631f0ead8165bd53f9db7450d6e58cc5b58dfb216b35e29c17c433f9593165` |
| Reproduced original reset bank | `73cf68b243a2f575d98b0be7dac562a84b3921d5d490896671bc7f08dc6bd158` |
| Optimized 1 MiB ROM | `028a12aff709cdca19e29000f6ae13f5174fbab49bc75b192c24100232fcf780` |
| Unchanged desktop HDF | `e113a115bc2518014b79526f00e07c8bccacac3dc2537f30734115f4085f82ce` |

The native core grows from 502,102 to 502,464 bytes. The original E00000 graphics
bank is retained exactly. Consumer firmware identity is pinned in
[CopperStartA1200.json](../CopperScreen/Firmware/CopperStartA1200.json) and the
production build gate. This is a new development qualification, not a package
publication or a change to previously published versions.

Local evidence is under `artifacts/copperos-boot-speed/`: original and candidate
source snapshots, `rebuilt-baseline`, `candidate-native`, `candidate-rom-v2`,
`baseline-profile-dos1`, `baseline-control-dos1`, `candidate-profile`,
`candidate-control`, `memory-tests`, `app-validation` and `ui-validation`.
Compiler manifests and profiles bind exact inputs. No ROM, HDF or build output
is committed. The first probe omitted DOS1 metadata and failed to boot; its
`baseline-profile` output is retained as invalid setup evidence. Initial compiler
setup failures are separate from the reproduced baseline and final candidate.

## Validation and application integration

Compiled native memory/alignment/overlap/register checks and filesystem semantic
checks pass **59 cases with zero skips**, including exact unaligned tails, OFS/FFS
sector boundaries, checksum failures, partial reads and aliasing scratch buffers.
The 68000 and 68EC020 clear cases cover guards and saved registers.

The app boot test now has a 1,900-field cold-start bound independent of host
speed, followed by raw-keyboard Editor startup, reset and cold reopen. The normal
UI test requires the native desktop after 2,000 fields. Additional native HDF
replays cover the original Shell system's commands, read-only protection, writes
and full disposal/reopen. Host tests cover workspace and firmware upgrades.

The final consumer native selection passes **3 tests with no skips**: bundled
desktop boot/input/reset/reopen plus read-only and writable/cold-reopen original
HDF replays. The desktop UI/upgrade selection passes **12 tests with no skips**.
The normal app session observes 1,784 fields / 35.737 guest seconds at its
20-field readiness checkpoints. CopperScreen's production Release solution
build passes with zero warnings/errors. CopperStart's isolated current-source
Release solution build passes with six compiler/provider warnings and no errors;
the shipped candidate uses the frozen inputs above rather than that wider build.

The existing desktop workspace keeps its HDF, geometry and write protection.
On normal reopening/reselection the app extracts the new hash-named ROM cache
and updates only its owned firmware path. The old ROM cache and Shell workspace
remain intact; explicit custom profiles retain their selected firmware. No guest
documents are replaced or reseeded.
