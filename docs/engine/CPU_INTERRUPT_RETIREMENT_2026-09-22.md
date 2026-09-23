# Copper68k interrupt-sample retirement comparison — 2026-09-22–23

**Correctness checks pass; the third formal series is valid and complete, but
the candidate does not meet the 1% upper-bound gate.** Native Lemmings improves
by 1.17% paired FPS on average. Lores and hires are slower on average, and all
three upper regression bounds exceed 1%. No exception has been accepted.

## Candidate and reference

The candidate is the owner's uncommitted change on CopperMod commit
`986ca2b0306c52bd782333847eb0059a2b9f03e8`. It guards
`ResolveDeferredInterruptSamples()` when no deferred-timing bus exists and moves
its original implementation into `ResolveDeferredInterruptSamplesCore()`, marked
`NoInlining`. Buses implementing deferred timing keep the original sequence.
The [JSON record](CPU_INTERRUPT_RETIREMENT_2026-09-22.json) preserves the exact
source patch, source hash and tested binary identities.

Reference CPU is the shipped `1.4.1-locality.1` package, SHA256
`982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F`.
Candidate CPU is
`31D67DABDCF29CC8118CBABCBC846EAF389B895916C95573BAC2B6BC1C157E9E`.
Both frozen runtime directories use the retained engine `8E390EE8...` and runner
`40B97941...`; **only Copper68k.dll differs**. This preserves the established
workload contract while comparing the new change against the currently shipped
CPU, not the older trace package used in the preceding optimization experiment.

The rebuilt candidate differs from the owner's earlier `3D55F1AD...` DLL in four
debug/build-identity records. The assembly comparator found zero executable
differences across 28,189 records, including all 4,251 method bodies. The actual
`31D67DAB...` DLL was used by the validation and formal attempts below. No package
was repacked, published or replaced, and the production pin remains unchanged.

## Validation

| Check | Result |
| --- | --- |
| Production and isolated diagnostic Release builds | Pass; zero warnings/errors |
| CPU Release build | Pass; one existing NU1902 dependency warning |
| CPU tests using candidate | 1,499 passed; six optional external-corpus cases unavailable |
| Engine diagnostics using candidate | 669 passed, none skipped |
| Host tests using candidate and supplied native media | 95 passed, none skipped |
| Complete lores, hires and native Lemmings replays | Retained full fingerprints match; zero measured allocations |

The separate replay timings are diagnostic only. CPU-only microbenchmarks in the
owner's CopperMod notes do not establish application FPS. CopperDisk is unchanged;
its tests were not rerun in this CPU-only comparison.

## Formal attempts

The [versioned harness](../../scripts/run-lightweight-cpu-interrupt-retirement-comparison-v1.ps1)
changes CPU identities and protocol naming from the previous locality comparison.
It retains six balanced pairs, 600/7,200 synthetic and 10,920/3,600 native
warmup/measurement frames, ten-second cooldowns, Normal priority, LP2 with LP3
protected, host-load policy v2 and the one-sided 95% Student-t upper regression
bound. The limit remains **at most 1% for each workload**. Topology, live telemetry
and positive/negative preflight probes passed on this Ryzen 5 5600X host.

| Attempt | Completed samples | Stop reason | Classification |
| --- | --- | --- | --- |
| 1 | Lores R1, C1, C2, R2 | Sibling interference at least 25% for 10.387 seconds | INVALID / RERUN |
| 2 | Lores R1, C1, C2 | Sibling interference at least 25% for 10.344 seconds | INVALID / RERUN |
| 3 | All 36 samples | Completed after owner reported system free | VALID; gate not met |

All completed sample fingerprints match and report zero allocations. The first
two attempts did not reach hires or native timing. No builds, tests or profiles
ran during any formal attempt. A bounded process check after the first interruption found
low CPU activity, so a fresh retry was attempted without changing placement or
thresholds. After the second interruption, the owner explicitly requested:
**"Leave performance verification pending."** The owner subsequently reported
**"The system is free now."**, authorizing a fresh third series with the same
frozen builds, placement, protocol and thresholds.

Raw samples and telemetry remain under ignored
`artifacts/cpu-interrupt-retirement-2026-09-22/attempt-1`, `attempt-2` and `attempt-3`.
The JSON preserves all completed samples and artifact hashes. Attempts are not
pooled or reclassified, and earlier accepted performance evidence is unchanged.

## Completed result

Attempt 3 completed after local midnight on 2026-09-23. All 36 complete workload
fingerprints match, every allocation count is zero, and all 1,446 telemetry
intervals are valid. Maximum continuous threshold exceedances were 1.130 seconds
on the selected CPU, 4.557 on its sibling and 2.270 across the package, below the
unchanged ten-second rejection limit. Short spikes remain included. All paired
statistics and telemetry checks were independently recalculated from raw files.

| Workload | Reference mean FPS | Candidate mean FPS | Paired FPS change | Paired frame-time change | One-sided 95% upper regression bound | Gate |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Lores | 376.58 | 372.85 | -0.9689% | +0.9783% | +6.1871% | INCONCLUSIVE; above limit |
| Hires | 340.61 | 335.94 | -1.4615% | +1.4832% | +6.2545% | ACCEPTANCE_REQUIRED |
| Native Lemmings | 321.61 | 325.49 | +1.1686% | -1.1551% | +1.3522% | INCONCLUSIVE; above limit |

FPS columns are arithmetic means; paired changes use log frame-time ratios.
Positive frame-time values mean slower execution. Lores and hires keep the CPU
stopped during steady-state measurement, so their results are retention checks,
not attribution of active CPU dispatch cost. This variation cannot be excluded
after passing the declared host policy. The native gain is modest and does not
establish compliance with the upper-bound requirement. No older exception applies.

The production package remains unchanged. Adopting this candidate requires the
owner's explicit acceptance of these measured exceptions or a future qualifying
comparison; the present complete result is not reclassified by either action.

To reproduce, use a fresh evidence directory:

```powershell
./scripts/run-lightweight-cpu-interrupt-retirement-comparison-v1.ps1 `
  -LogicalProcessor 2 `
  -ReferenceDirectory artifacts/cpu-interrupt-retirement-2026-09-22/reference `
  -CandidateDirectory artifacts/cpu-interrupt-retirement-2026-09-22/candidate `
  -RomPath C:/Data/ROM/Kickstart_13.rom `
  -AdfPath 'C:/Data/TestImages/Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip' `
  -InputScriptPath CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json `
  -EvidenceDirectory artifacts/cpu-interrupt-retirement-2026-09-22/attempt-4
```
