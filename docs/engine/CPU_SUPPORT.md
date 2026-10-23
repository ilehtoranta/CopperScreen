# CPU support and Copper68k package scope

Updated 2026-10-23. This development worktree pins unpublished Copper68k
`1.5.5-nitro.1`, from CopperMod commit `25dae803fa8476d601acc5c0af9bbac8ed98138f`.
It corrects memory BTST interrupt sampling before the operand read; the
[Nitro record](COPPERSTART_NITRO_2026-10-23.md) retains package identity and consumer checks.
The October 11 worktree used unpublished `1.5.4-qualified-dev.1`.
The merged general logical handler already supports Detroit's reached
`EORI.B #$9E,(A0)+` form; the older dedicated path is not imported. Exact-package
evidence is in the [candidate qualification record](COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.md).
The shared CPU source
is maintained in CopperMod; no sibling project reference is used.
The previously recorded published 1.5.3 release retains the preceding 1.5.2 integer
corrections and was explicitly authorized for publication on 2026-10-07.
Advanced CPU features retain the experimental scope below.

## Availability and scope

| Facility | Implemented behavior | Remaining limits |
| --- | --- | --- |
| 68010 | Engine, runner, profile persistence and experimental desktop choice. Direct motherboard bus with forwarded timing; VBR/SFC/DFC/USP, SR privilege, MOVE from CCR, MOVES, RTD, format-0 exceptions and 58-byte format-8 address-error frames/RTE consumption. | Internal format-8 continuation/retry state is incomplete. Loop-mode and exact physical timing are not qualified. |
| 68040 FPU | Existing CopperFloat ext80 arithmetic is retained and checked through guest instructions. | Full FPSP and exceptional operand conformance is not certified. External 68881/68882 for 020/030 is outside this slice. |
| 68060 FPU | Arithmetic uses the shared ext80 implementation. Own 12-byte NULL/IDLE/EXCP save/restore, exceptional operand payload, deferred arithmetic exceptions and removed-instruction traps. Disabled-FPU faults use the 060 frame. | Compatible OS/FPSP installation and native task switching remain unverified. Old Kickstart task frames are incompatible. Desktop 060 remains unavailable; engine and runner can execute original guest programs. |
| 040/060 MMU | Enabled three-level translation, 4/8 KiB pages, indirect page descriptors, supervisor/write protection at all applicable levels, U/M updates, transparent translation, explicit PFLUSH, 040 PTEST and 060 PLPA. Timed physical table walks and translated supervisor fault stacks. | ATC placement and cache attributes are bounded host policies. Full 040 format-7 writeback/continuation and partially completed operand recovery are incomplete. General-purpose virtual-memory OS use is not qualified. 030 PMMU remains open. |
| 68040 JIT | Optional desktop choice and runner `--cpu 68040 --jit`. Compiled execution, immutable snapshots, code/map invalidation, device deadlines, interrupt acceptance and public counters. | MMU/TT contexts execute through the interpreter. JIT is not provided for other models. Broad native gameplay parity and throughput acceptance remain open. Compilation/invalidation may allocate. |

The default 68000 path remains the existing direct interpreter. Accelerator
timing is an approximate execution policy, not certification of a physical card.
The 010 uses one native clock per motherboard clock, 020/030 two, 040 four and
060 eight. See [architecture](ARCHITECTURE.md) for bus ownership and timing.

## Shared core contracts

The desktop now offers CPU upgrades on both A500 and A1200. Fast RAM is capped
at 8 MiB for 24-bit CPUs and 1 GiB for full 32-bit CPUs; banks above 8 MiB use
virtual Zorro III Autoconfig and require native Kickstart 3.x. See the
[2026-10-08 model/CPU/memory verification](CPU_MEMORY_UPGRADES.md). This host
integration does not change the pinned shared CPU package or certify card timing.

The former 040 flat-map translation has been replaced by the documented root,
pointer and page tables. TC enable is bit 15; bit 14 selects 8 KiB pages. Root
and pointer indexes contain seven bits; the final page index contains six or
five. Root/pointer descriptors require the table type bit; resident and indirect
page descriptors use their documented type fields. Upper-level write protection
is retained in cached translations and checked again on writes. Denied writes do
not mark the page modified.

MOVEC TC/SRP/URP changes do not implicitly flush the ATC. Explicit PFLUSH has
address/global and destination-function-code behavior. PTEST replaces the
matching 040 entry and reports MMUSR; PLPA returns the 060 physical address.
Transparent translation applies supervisor matching and write protection even
with paging disabled. Side-effect-free code inspection does not install an ATC
entry or set descriptor U/M bits; actual accesses still perform timed walks.

040 access errors use a 60-byte format-7 frame, and 060 access errors use a
16-byte format-4 frame with fault address/FSLW. Vector fetch and supervisor stack
accesses remain translated; a recursive fault during stacking halts. Frame
construction tests establish the bounded layout, not complete instruction restart.
040 continuation flags requiring internal recovery explicitly remain unsupported.

The 060 FPU always consumes 12 bytes for NULL, IDLE and EXCP. EXCP restoration
retains the exception vector and ext80 operand. FSAVE can retrieve pending
exception state and clears it to IDLE. Arithmetic exceptions destined for FP
registers are deferred until a following FP instruction; removed 060 forms trap
for guest software handling. No 040/68881 frame substitution is used to make an
incompatible OS appear to boot.

Architectural expectations are taken from
[MC68000UM](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf),
[MC68040UM](https://www.nxp.com/docs/en/reference-manual/MC68040UM.pdf) and
[MC68060UM](https://www.nxp.com/docs/en/data-sheet/MC68060UM.pdf).
Interpreter/JIT agreement and synthetic tests alone do not establish hardware
conformance.

## JIT ownership and invalidation

The accelerator bus provides mapped RAM/ROM code peeks and immutable snapshots
confined to one 256-byte page. Device registers, unfitted RAM, host gateways,
unaligned code and page-crossing snapshots are rejected. Peeks neither advance
hardware nor read live device registers.

Generations are attached to canonical physical RAM pages. CPU writes, chipset
DMA writes, CopperHDF byte/bulk writes and clears invalidate observed code,
including Chip RAM aliases. Reset, ROM overlay and Fast RAM Autoconfig changes
invalidate mapping context. Guest instruction-cache retention and CINV/CPUSH
remain separate architectural concerns. Architectural CACR bit 15 enables the
040 instruction cache; the old JIT test of bit 0 was corrected.

Only the JIT boundary exposes the pure compiled-slice contract. It clamps slices
to the next device-visible deadline, advances the existing machine clock and
accepts pending interrupts before starting another slice. The regression catches
the original failure where a compiled loop continued despite a visible interrupt.
Enabled paging or transparent translation disables compiled execution so timed
walks, permissions and fault behavior stay on the interpreter path.

`LightweightA500Machine.JitStatistics` exposes compiled traces, compiled
instructions, fallback instructions and invalidations. A selected JIT mode or
positive compilation count alone is not proof of compiled execution.

## Restoring the released dependency

From a CopperScreen checkout:

```powershell
dotnet restore CopperScreen.slnx --locked-mode
dotnet build CopperScreen.slnx -c Release
```

The October 7 published baseline used NuGet.org alone. The current development
configuration additionally uses `artifacts/qualified-cpu-20261011/feed`; local
packages are ignored and never committed. The Nitro record identifies the
CopperMod source commit and locally packed `1.5.5-nitro.1` package used here.
Supply that exact package to the local feed before a locked restore; a fresh
pack must be verified against its recorded content identity. This is local
development, with no package publication. Locks retain that content identity.
Published versions remain immutable; further CPU changes require a new version.

## Verification on 2026-10-07

The following implementation checks were recorded against the unpublished
`1.5.3-cpu.4` candidate before stable release metadata was applied. The release
workflow reruns the CPU suite and AHX consumer tests on the immutable release
commit. Published-package consumer checks are recorded separately below.

- Production Release build: zero warnings/errors.
- Shared `Copper68k.Tests`: 4,433 passed, seven optional seeded/external
  conformance checks skipped. Those checks are unavailable coverage.
- Isolated Lightweight engine suite: 1,163 passed, zero skipped, with the local
  KS1.3 and KS3.1 ROM environment variables set. This includes native RTC and
  additional-format disk tests as well as CPU integration regressions.
- Desktop/host suite: 180 passed, six optional native media tests skipped.
- CopperDisk suite: 74 passed, zero skipped.
- Packaged CPU probe: all 13 checks passed. 000 and 010 minimum bus strobe
  separation is four clocks with 92 timing callbacks; 010 address-error frame
  size is 58 bytes.
- Native CPU boot: unmodified KS1.3 loads original bootblock code for 010 in
  normal/scalar mode and 040 interpreter/JIT. The JIT guest enables architectural
  CACR and executes compiled instructions after native disk boot.
- Guest integration: 040/060 FP multiplication produces integer 9; guest MOVEC
  enables paging and reads/writes a non-identity Slow RAM mapping with U/M
  updates. The JIT-selected paging case runs through interpreter fallback.
- JIT regressions check actual compiled execution, fresh code after guest cache
  maintenance, immutable snapshots, CPU/DMA/hardfile invalidation, reset context,
  architectural interrupt frames and CPU/hardware clock agreement.

CPU DLL SHA-256 consumed by the package probe:
`A1058E319509783DFC897B682EF7AA980B47982EE0613B2268E414AF21318D40`.
Local candidate nupkg SHA-256:
`098FB3BC2460B0E11AEFE97F72A084887142B29DDA9E4996C6D471B3F18D42D4`.
Local native ROM identities:

| ROM | SHA-256 |
| --- | --- |
| A500 KS1.3 (34.5) | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| A500 KS3.1 (40.63) | `8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE` |

ROM/media bytes and local packages/results are not committed. Native boot checks
prove the specified bootblock execution, not Workbench/FPSP compatibility or
gameplay. The earlier [060 task-frame trace](CPU_OPTIONS_030_040_060_2026-09-27.md)
remains an OS compatibility blocker. A current 300-field KS1.3/060 diagnostic
reaches ROM failure-loop code, not a validated boot; an `unsupported=none` result
is not success. A500 KS3.1/060 compatibility remains unverified. No 060-aware
installed OS was available for verification.

No new FPS/throughput acceptance was performed. Existing historical native and
performance evidence remains separate and is not relabeled for this release.

## Published release verification

The stable [NuGet 1.5.3 package](https://www.nuget.org/packages/Copper68k/1.5.3)
and symbols were published on 2026-10-07 from commit
`ddc753000b492a15048ba6c16a06e03c0c160a63`, tagged `copper68k-v1.5.3`.
The [release publishing workflow](https://github.com/ilehtoranta/CopperMod/actions/runs/37663704799)
passed all 4,433 CPU tests (seven optional checks skipped), all 18 AHX consumer
tests and package/symbol content validation before publishing. Main CI also
passed. The [GitHub stable release](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-v1.5.3)
retains the validated CI package and symbol artifacts.

CopperScreen restored 1.5.3 from NuGet.org alone into an initially empty
`artifacts/published-release/packages` cache. Locked restore and the production
Release build passed with zero warnings/errors. The isolated engine suite was
rerun against the published package: 1,163 passed, zero skipped, including the
native CPU/disk/RTC checks. The published-package host suite passed 180 tests,
with six optional native tests unavailable. All 13 packaged CPU probes passed.
The previous disk-only evidence remains unchanged; it was not relabeled as a
new run.

The downloaded CPU DLL matches the CI release artifact byte-for-byte. SHA-256:
`90D187D1FCB641E5434EA9E2DBDE5B0EC4B1AF08E313990E60762C0367C0ACF3`.
NuGet content identity in the updated package locks:
`6fih4cf+u9nqmFxeFD23nH3XFmuJiWwCZ41WXRR7odcn/x48+O7KzBlo2/78kiH2jpViMHMe1Q4LUSCp847b7w==`.
The remaining architectural/OS/performance limits above still apply.
