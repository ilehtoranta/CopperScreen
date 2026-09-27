# CPU expansion readiness — 2026-09-26

> Historical pre-implementation audit. The [follow-up implementation](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.md) fixes the recorded 020 boot blockers and documents the current experimental options and remaining limits.

The next proposed product slice is **68020 / 68EC020 on the existing PAL OCS
machine**, including settings, saved profiles, command-line selection and native
replays. The stable Copper68k 1.4.1 dependency has demonstrated blockers, so the
application remains on its supported 68000. No new CPU is enabled by this audit.

## Reproducible package findings

The [ROM-free probe](../../scripts/CpuModelProbe/Program.cs) uses the pinned
package through its factory and bus API. It is deliberately outside the production
solution and the normal diagnostic test project.

The command follows the current package pin. The archived results in this
document used stable `1.4.1`; later restores and runs do not replace that evidence.
Run:

```powershell
dotnet run --project scripts/CpuModelProbe/CpuModelProbe.csproj -c Release --verbosity quiet
```

Exit 1 means a demonstrated readiness check failed; exit 0 means only that these
bounded checks passed, not full model conformance. Compilation failures are
distinct from the JSON probe results. The [recorded output](CPU_OPTIONS_2026-09-26.json)
has six passing controls/cases and six failures:

| Case | Result |
| --- | --- |
| 68000 bus spacing and address error | Four-clock minimum separation; 14-byte frame. Both pass. |
| 68010 bus spacing | Two-clock adjacent strobes; zero calls to the supplied timing interface. Its masking wrapper omits `IM68000BusCycleTiming`. |
| 68010 address error | Eight bytes stacked, rather than 58. A format-8 marker alone does not implement the long frame. |
| `MOVE.B ($3000).W,16(A1)` (`$1378`) | Passes on 68000, 68010 and 68040; throws `UnsupportedM68kTimingException` on 68020, 68EC020 and 68030. |
| Enable instruction cache with an `IM68kBus`-only host | 68020 completes the bounded program; 68EC020 throws from its masking wrapper's advertised `IM68kCodeReader`. |

The hardware expectations for four-clock 68010 bus cycles and 29-word fault
frames come from the [Motorola user manual](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf),
sections 9 and 6.2.4 / figure 6-8. The MOVE fixture checks the architectural byte
copy and next PC; it does not assert an instruction timing number. The cache
fixture checks the package's advertised minimum host contract, not cache timing.

The package's repository commit is
`8fb826eacf8e9dc32948b482c878079448bd6e61`; its DLL SHA-256 is
`F82CA4A5FD953B0BEE31EE360212070548DB0DAA80874EEEC36DB0EFD1F1AFE1`.
Relevant source is the pinned
[68010 wrapper and frames](https://github.com/ilehtoranta/CopperMod/blob/8fb826eacf8e9dc32948b482c878079448bd6e61/Copper68k/M68010Interpreter.cs),
[68EC020 wrapper](https://github.com/ilehtoranta/CopperMod/blob/8fb826eacf8e9dc32948b482c878079448bd6e61/Copper68k/M68EC020Interpreter.cs),
and [advanced interpreter](https://github.com/ilehtoranta/CopperMod/blob/8fb826eacf8e9dc32948b482c878079448bd6e61/Copper68k/M68kAdvancedTimingInterpreter.cs).

## Integration experiment

A temporary implementation connected configuration, host validation/settings and
runner selection. An experimental 16-bit bridge split unaligned accesses,
prevented upper 68020 addresses from aliasing motherboard devices, and preserved
one hardware clock. The package's default OCS 020 profiles already convert two
native CPU clocks to one motherboard clock; treating `NativeCycles` as motherboard
cycles would double-count CPU speed.

Both native ROM-only runs stopped during field 60 (59 fields completed):

- 68020: `$1378` at `$00FC047E`, before a usable boot screen.
- 68EC020: missing host code-read capability after enabling the instruction cache.

These are **failed native boots**, not compatibility passes. The one-instruction
ROM-free MOVE case independently reproduces the package failure. The ROM SHA-256
was `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.
No game replay or throughput acceptance was attempted with the new models.

The prototype was withdrawn from production source. Its patch and failed native
captures remain locally under `artifacts/cpu-options-2026-09-26/`; the patch is
unfinished and not an accepted accelerator timing implementation. The original
68000 implementation and the existing native regression work remain intact.

## Proposed implementation sequence

1. Fix the failing 020 instruction family and EC020 capability forwarding in
   CopperMod, with discriminating CPU tests and model-specific timing evidence.
   Audit related MOVE addressing combinations rather than special-casing the ROM
   instruction. Consume a newly released, immutable Copper68k pin afterward.
2. Complete the OCS accelerator bus contract: 24-bit EC020 versus 32-bit 020
   addressing, aligned/unaligned memory and device effects, VBR, exception frames,
   interrupt/STOP/reset, and cache behavior. Keep accelerator-card timing claims
   separate from the package's approximate policy.
3. Enable desktop/profile/runner selection with round-trip and restart tests;
   retain the six-title 68000 baseline and record separate new-model native
   boot/gameplay results. Measure throughput separately under a suitable protocol.

68010 needs its own wrapper, fault-frame and timing work. 68030 shares the observed
MOVE failure. The 68040 passing that one instruction is not evidence of complete
support; its fixed-cycle policy and cache/MMU/FPU scope require separate treatment.
JIT and 68060 remain separate roadmap items. This investigation does not publish
a package or change any existing golden output.

## Validation of the retained changes

The probe builds and reports the twelve cases above against the recorded DLL.
After withdrawing the integration prototype, `dotnet build CopperScreen.slnx
-c Release` passes with zero warnings and errors. Local document links and
`git diff --check` pass. The retained change is tooling and documentation;
the emulator suites and native 68000 corpus were not rerun for it.
