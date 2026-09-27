# Lotus III: 020 Chip RAM cache and instruction corrections — 2026-09-26

The experimental 020 disk-two stall has a demonstrated CPU-layer cause: the
instruction-cache policy incorrectly excluded Chip RAM even with `CACR=1`.
Correcting that policy lets the original first fire press at field 6,500 reach
Lotus's prompt-completion code. Further execution exposed a separate `MOVE.L`
decoder error that corrupted copied instructions, plus missing operand forms.
No game, input schedule, ROM, disk, chipset ordering or bus cost was adjusted.

This follows the [lost-edge investigation](LOTUS_III_020_INVESTIGATION_2026-09-26.md).
That record describes the retained `.50` behavior; it is not rewritten as a pass.
Raw diagnostics remain under `artifacts/lotus3-020-cache/`. The accompanying
[manifest](LOTUS_III_020_CACHE_2026-09-26.json) binds the sources, packages,
inputs, tests and captures used here. This package was unpublished during the
original validation and was later released as an experimental prerelease; the
release status and exact public artifact are recorded at the end of this page.

## Independent cache regression

[MC68020UM section 4](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf)
specifies the instruction cache and its software invalidation controls.
[Commodore's `CacheClearE` autodoc](https://www.theflatnet.de/pub/cbm/amiga/amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node059D.html)
states that existing Amigas enable instruction caching for Chip RAM while
inhibiting its data cache; custom registers are not cacheable. The prior 020
profile's unconditional Chip RAM exclusion contradicted that instruction policy.

The retained [ChipInstructionCache probe](../../scripts/probes/ChipInstructionCache/README.md)
generates an independently authored ROM with no licensed bytes. It copies code
to Chip RAM, enables the cache, calls a function returning `1`, overwrites its
instruction to return `7`, calls again, invalidates the cache and calls once more.
The three results must be `1, 1, 7`.

| Execution | Results | Meaning |
| --- | --- | --- |
| Copper68k `.50`, 68020, normal and scalar | `1, 7, 7` | Incorrectly observes the modified instruction immediately |
| Cache correction `.51`, 68020 and EC020, both modes | `1, 1, 7` | Retains cached instruction until guest invalidation |
| WinUAE 6.0.3, same generated ROM | `1, 1, 7` | Independent reference agrees |

WinUAE uses PAL OCS, 512 KiB chip + 512 KiB slow RAM, 68020 at 14,187,580 Hz,
compatible execution and CPU/memory cycle exactness, with JIT disabled. Saved
configuration queries and debugger output establish STOP and the three values
at `$C09000`. Its process was closed after inspection. This validates cache
visibility, not cache-line bus timing or physical accelerator conformance.

Four package tests cover cache enabled/disabled on both 020 models; the two
enabled-cache cases fail before the correction. Four engine tests check the
guest's stale-code/flush sequence in normal/scalar modes. Device fetches remain
uncached, and 030/040 cache policy is unchanged.

## Native causal trace

With only the cache correction, the first scripted press interrupts at `$741AA`
on field 6,500, motherboard cycle `923777958`. RTE returns there at `923779420`
with the edge byte set; `$741B8` consumes the edge at `923779456`. The previous
`.50` execution returned to `$741B6` after its bit test had already missed it.
The disk-two prompt is passed without changing the input script. Cache-only
execution then stops explicitly on previously unreached opcode `$3091`.

The next semantic defect occurs much earlier than its eventual exception. At
field 1,807, PC `$6A840`, opcode `$22B9` should copy a longword from absolute
memory into `(A1)`. The decoder instead classifies every extended source in that
family as immediate. It writes `$0006A862` to `$C173A2`, where the memory source
contains `$4EB90006`, corrupting a later subroutine call. The resulting Line-A
exception at field 7,533 is a consequence, not a missing exception-handler
instruction to work around.

The corrected mask distinguishes absolute short/long, PC displacement/index and
immediate sources. A 30-case matrix covers those forms on 020, EC020 and 030:
the 24 memory-source cases fail with the original decoder; the six immediate
controls pass. All pass after correction. The external write observer's field
7,000 JSON, framebuffer, chip RAM, slow RAM and PCM match uninstrumented
candidate captures byte for byte. Its reflection-based bus wrapper remains an
ignored diagnostic artifact; no production observer hook was added.

## Instruction continuation coverage

The newly reached instructions use the existing bounded operand-shape timing
policy. Their data/flag/address expectations follow the instruction definitions
and effective-address rules in Motorola's
[M68000 Family Programmer's Reference Manual](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf).
Coverage includes signed displacements, PC-relative bases, source/destination
aliasing, byte-stack alignment, arithmetic overflow/carry and untouched bytes.
Normal and scalar execution agreement is an integration check, not independent
hardware timing evidence.

The continuation matrix has 1,419 cases across 68020, EC020 and the shared 030
executor. It adds the memory MOVE forms, indexed arithmetic/comparisons,
post-increment logical/arithmetic operations, quick address arithmetic, signed
address subtraction, negation, byte arithmetic shift, bit modifications and
PC-relative jump forms exercised along this path. New advanced handlers retain
the 040 fallback. No 030/040 host support is enabled.

## Final package and validation

At validation time CopperScreen pinned the development candidate
**`Copper68k 1.4.2-ocs020.52`**, built from owner commit
`79101066e8960e5fa0f9012b6fa35818db565781`. The cache
correction is owner commit `3955b5ec5a4e78fc4c55ff80608b026acdec0d1f`; the decoder
and continuation changes are commit `79101066e8960e5fa0f9012b6fa35818db565781`
on the local CopperMod branch `codex/experimental-ocs020` at validation time.
Both patches are retained
under `artifacts/lotus3-020-cache/source/`. The manifest binds the original
validation package and production runner binaries to the replay results. The
release addendum records the later published package hash and source commit.

Intermediate opcode investigations used an isolated runner copy with an unpacked
CPU build. The results below use the production runner and the pinned package.
Production projects keep their NuGet boundary; no sibling project/source
reference was introduced. The workspace feed was local and ignored during
validation; it now mirrors the published prerelease for locked restores.

| Check | Result |
| --- | --- |
| Production Release solution | Pass, zero warnings/errors |
| Locked restore | Solution, isolated diagnostic project and all three CPU probes pass |
| CPU tests | 3,107 passed; six optional external conformance tests unavailable |
| Engine tests, isolated outputs | 754 passed |
| Host tests | 98 passed; three optional native tests unavailable |
| Disk tests | 74 passed |
| Final cache probe | `1, 1, 7` on both 020 models, normal and scalar |
| Cache-disabled polling probe | 68000 and 68020 normal/scalar JSON and RAM remain byte-identical to the earlier investigation |

All four final native runs complete **18,000 fields** with the original
Kickstart 1.3, untouched two-disk media and unchanged input script. No CPU,
chipset or video unsupported state is reported. The two advanced models use
`ocs-accelerator-v1`, 512 KiB chip + 512 KiB slow RAM and the existing OCS bus.

| Native comparison | Captures compared | Result |
| --- | --- | --- |
| 68020 normal vs scalar | 95 files: state JSON, BMP, chip RAM, slow RAM, PCM | Byte-identical |
| 68020 normal vs EC020 normal | Same 95 files | Byte-identical |
| Packaged 68020 vs final unpacked candidate | Same 95 files | Byte-identical |
| Packaged 68000 vs retained `.50` 68000 baseline | Same 95 files | Byte-identical |

Visual inspection establishes the Magnetic Fields logo at field 7,000, the
Lotus III title at 9,000, a driving **DEMO** at 10,000, the options menu at
12,000 and car selection at 18,000. Earlier demo captures also show the changing
road, opponents and speed. The later scripted presses leave the final run at
car selection; this is not verified player-controlled racing.

The advanced runs finish at motherboard cycle `2557838300`, with CPU fingerprint
`49D607FF8E9FA8DD`, hardware `52970C8E596514D2`, output `257DA82648A40612`.
The 68000 retains cycle `2557703978`, CPU `5F5BEE5D7DFD194F`, hardware
`9C79BFD36438BAD4`, output `DB03B181E11EBD9C`. These are replay identities,
not new hardware goldens.

The models remain experimental. Complete game compatibility, physical cache
refill/interrupt timing, broader instruction conformance and player-controlled
racing remain unverified. Six-title 68000 evidence from the initial implementation
is retained unchanged; this correction reruns Lotus specifically. No throughput
acceptance measurement or golden update was made. Raw runner FPS is diagnostic
and does not satisfy the performance protocol.

## Release status update — 2026-09-26

After the validation above, Copper68k `1.4.2-ocs020.52` was published to
[NuGet.org](https://www.nuget.org/packages/Copper68k/1.4.2-ocs020.52) as an
experimental prerelease. The release tag
[`copper68k-v1.4.2-ocs020.52`](https://github.com/ilehtoranta/CopperMod/tree/copper68k-v1.4.2-ocs020.52)
points to `7e2576b002a18bb2c1c3ccd406bec5d4e7f56b45`. The successful
[trusted-publishing workflow](https://github.com/ilehtoranta/CopperMod/actions/runs/36261336674)
ran the Copper68k CPU tests, built and validated both packages, then uploaded
the `.nupkg` and `.snupkg`. The public `.nupkg` SHA-256 is
`D9EF63BDB3B50206EFAD682C9DF06F2D0403A676AFBFEE84C82C726575D84548`.

The release commit changes only the publishing workflow and package release
notes after the code commit used by the 18,000-field native replays. The
release workflow's CPU suite passed, and the production solution rebuilt
against the downloaded public package with zero warnings or errors; locked
restores now resolve the published artifact. The native
replays remain the bounded validation described above; full game compatibility,
player-controlled racing and physical accelerator timing are still unverified.
