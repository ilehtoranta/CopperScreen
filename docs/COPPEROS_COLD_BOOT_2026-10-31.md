# CopperOS single-bank cold-boot fix

Development qualification `diskboot-2026-10-31-a1200-desktop-single-bank.49`
recovers the cold-boot regression recorded in the
[first single-bank qualification](COPPEROS_SINGLE_BANK_2026-10-29.md).
It retains the `.48` Layer drawing improvements,
the standard 512 KiB ROM and the unchanged exec-preemption.10 desktop HDF.

## Investigation and changes

The current baseline reproduces **14.543149995 guest seconds** to desktop
readiness. Rebuilding the retained reset/compiler inputs reproduces the exact
raw ROM SHA `8BA11BDB14EB8441EEA80BB040EE478D43C70D8A82A38AE1BC88A8C35D566447`;
linking the retained `.47` graphics reproduces that qualification byte-for-byte.
The subsequent `.48` uses the same raw reset image and has the same boot time.
This separates the targeted Exec changes from ongoing producer work.

The retained profile exposes expensive destination-address reconstruction and
counter spills in Exec Clear, repeated TLSF size-helper calls, per-block target
register shuffling, and a linear bit search for allocator size bins. The fix
changes three guest Exec source files:

- Clear advances its destination directly, keeps the remaining count local and
  stores 64 bytes per large iteration. Odd leading bytes and exact tails remain.
- TLSF reads and masks block sizes directly. Its boundary proof walks consecutive
  sorted intervals once, keeping other targets outside the hot scan. Zero spans,
  arena bounds, exact boundaries, bin backlinks, bitmaps and coalescing checks
  remain. The old alignment test was always satisfied after `SizeMask`; the
  equivalent nonzero-span check retains its minimum-size guarantee.
- Size-bin selection uses five bounded bit decisions, preserving the original
  most-significant-bit result for every uint, including -1 for zero.

The routines execute from ROM. No emulator/CPU implementation, package pin,
machine configuration, filesystem image, allocator record layout or interrupt
exclusion boundary changes. No public argument checks or private integrity
admission checks were discarded. The separate system-ROM migration remains
outside this frozen qualification.

## Guest measurements

PAL Alice/Lisa, 68EC020 interpreter, 2 MiB Chip RAM, no slow/Fast RAM, read-only
unit-0 FFS HDF with `DH0:2:32:0:255`, and empty floppy drives:

| Image | Cold desktop ready |
| --- | ---: |
| Historical combined `.36` | 12.159355436 s |
| Historical first single-bank `.37` | 14.583213769 s |
| Current `.48`, rerun | 14.543149995 s |
| `.49`, normal and scalar profile | **12.099259775 s** |

The fix saves **2.443890219 guest seconds (16.8%)** against `.48`, and is
**0.060095661 s faster** than the historical `.36` result. Historical images
were not rerun or relabeled. The retained before/after profiles establish the
current bottlenecks and improvement, rather than attributing the entire original
`.36` to `.37` delta to one source edit.

Intermediate trials measured 13.321205 s after the first Clear change,
12.800376 s with direct boundary reads, 12.600057 s with the larger clear block
and shared span extraction, and 12.179387 s with interval scans/local counts.
The last trial was still one PAL field slower than `.36`; the bounded bit search
closes that remainder. Trial images remain separate from the qualified image.

`copperos-a1200-coldboot-optional-symbols-v4` preserves the existing guest
execution and readiness condition, while allowing missing GUI/Home binary/map
pairs to be omitted with `- -`. Those fixtures are absent locally. Consequently
no new fine-grained DrawChanges duration or module-loaded milestone is claimed.
Existing callers supplying both modules retain their earlier protocol.

For `.49`, normal and scalar execution agree exactly on readiness, milestones,
cycles, fields, input cycle, all CPU registers, full Chip RAM, framebuffer,
audio and display registers. Tab is applied at field 1,000; both complete at
field 1,008 with selection rendered. The deliberate settling/input window is
separate from cold readiness. Pixel SHA remains
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
These are guest-cycle diagnostics, not host-throughput acceptance measurements.

## Verification and bundle

The producer passes **31 focused tests**: allocator integrity/coalescing,
compiled clear-range/guard/register/stack/ROM checks, native free controls, and
native/host bit-search comparisons against an independent integer oracle.
Both MC68000 and 68EC020 execute the new native cases. The producer Release
solution builds with four existing dependency warnings.

The app passes **11 native graphics/desktop checks** and **69 launcher/update
checks**. Eight optional historical media/workspace cases are unavailable, not
successful replay coverage. Native app checks include Tab selection, editor
launch, reset and cold reopening.

The normal Release desktop app and the complete production solution built into
separate artifact outputs both pass with zero warnings/errors. The ordinary
whole-solution build could not replace runner DLLs locked by two ongoing utility
replays; those processes were preserved. Both the normal app and isolated app
have their embedded ROM/HDF hashes checked against the exact qualified inputs.
App verification completed on 2026-11-01. See the
[producer source and native tests](../../CopperStart/Docs/NativeCopperOsColdBoot20261101.md).

| Fact | Value |
| --- | --- |
| ROM file | 524,288 bytes, one F80000 bank |
| Used / padding | 520,780 / 3,508 bytes |
| ROM SHA-256 | `3D25D4C903BEEB4FA21D38E9F2DD8B37D76EAFA1150D4686CBB8DF706008A4D6` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |
| ROM checksum | `$FFFFFFFF` |

Graphics remains the exact `.48` emitter selection: 26,048 bytes, with its
initializer and all vector addresses verified against that snapshot. The frozen
compatibility checker passes with zero managed allocation sites; the working
checker for the separate system-ROM migration is not weakened.

Source snapshots/hashes, frozen inputs/compiler, reproducible build scripts,
trial images, final `rom-bit`, `bit-{profile,control}`, paired verification and
build/test logs are retained under `artifacts/coldboot-20261031`. Producer test
and build logs use the `coldboot-` prefix under its own `artifacts` directory.
ROM/media and local artifacts remain ignored and unpublished.
