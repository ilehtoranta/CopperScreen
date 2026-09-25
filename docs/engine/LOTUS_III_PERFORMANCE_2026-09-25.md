# Lotus III bus-timing performance verification — 2026-09-25

The default acceptance limit is a one-sided 95% upper frame-time regression bound
**at most 1% for every workload**. On 2026-09-25, the owner accepted this exact
Lotus comparison with a scoped exception for the two inconclusive rendering bounds.
The default limit remains unchanged for other candidates.

## Comparison scope

The reference is the frozen pre-Lotus engine
`7EB9C87905BD8CA790DFF21BB918A0B34F7E4B94FE8489F673058BC4D1581FDC`.
The candidate is the bus-spacing correction
`7EE0B872B7E2F4A491FCBBC4D8BDC2BBFD577DF34A1EBB62CF4B0148018EE3E9`.
Both use the identical frozen production runner, Copper68k 1.4.1, CopperDisk,
other dependencies and .NET 10.0.12 runtime. Only the engine DLL/PDB vary.
This measures the additional Lotus correction; it does not replace the earlier
combined native-corrections result or its unmet lores gate.

The separately versioned
[comparison v1](../../scripts/run-lightweight-lotus-bus-timing-comparison-v1.ps1)
retains six balanced pairs per workload, full rendering and real stereo PCM,
zero measured allocation, Normal priority, verified homogeneous CPU/SMT placement,
host-load policy v2 and the paired-log Student-t calculation (df=5, 2.015048).
All identities are fixed from validation runs before the formal timing samples.
No historical harness or fingerprint is changed.

## Native input preflight

The old Lemmings input script was rejected for this comparison: the candidate was
still in its introduction when the script exchanged disks, and later captured a
black screen in the loader. Its apparent FPS is not gameplay performance.

The new [native script](../../CopperScreen.Lightweight.Tests/Workloads/lemmings-cpu-bus-timing-v1.json)
delays every event from the old field 3600 onward by 3600 fields, including the
disk exchange, menu positioning and level start. The initial crack-intro click
remains at 600. Both builds receive exactly the same revised script. Native
warmup correspondingly moves from 10,920 to 14,520 fields; the measured interval
remains 3,600 gameplay fields, ending at 18,120. The original script and its
earlier results remain unchanged.

Separate preflight captures verify gameplay rather than accepting a new hash of
a failed replay. Normal/scalar execution is also compared for each complete
workload; preflight FPS values are diagnostic only and are excluded from the
formal statistics. The lores and hires intervals remain 600 warmup plus 7,200
measured fields.

All six build/workload combinations match their scalar controls exactly after
excluding FPS and the CPU-mode label. Both native captures show level-one gameplay
at field 13,920 (two lemmings out, time 4:55), before the 600-field gameplay warmup,
and at 18,120 (ten out, time 3:31). Their final BMPs are byte-identical, SHA-256
`0919509F56690100BF0E74604EE95E55C3504DCF9007BD98F8E8565A9A146D58`.
The native CPU/hardware/audio fingerprints differ because CPU timing changed;
each build's independently validated identity is pinned separately. Synthetic
identities remain identical across builds.

Host: AMD Ryzen 5 5600X, one processor group, six physical cores and twelve logical
processors with one efficiency class. The protocol rechecks topology, pins logical
CPU 2 at Normal priority and monitors its SMT sibling 3, selected-CPU interference
and aggregate load. The runtime, all capsule files, ROM, both disks, new input
script and unchanged host-load helper are pinned before timing. The preflight
passes identity and telemetry rejection probes plus monitored live telemetry.

With those frozen capsules and local media, reproduce using a fresh evidence
directory (add `-ValidateOnly` for validation without timing):

```powershell
& scripts/run-lightweight-lotus-bus-timing-comparison-v1.ps1 `
  -LogicalProcessor 2 `
  -ReferenceDirectory artifacts/lotus3-performance/reference `
  -CandidateDirectory artifacts/lotus3-performance/candidate `
  -RomPath C:/Data/ROM/Kickstart_13.rom `
  -AdfPath 'C:/Data/TestImages/Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip' `
  -InputScriptPath CopperScreen.Lightweight.Tests/Workloads/lemmings-cpu-bus-timing-v1.json `
  -EvidenceDirectory artifacts/lotus3-performance/new-series
```

This is engine throughput for the three specified workloads. It does not measure
desktop presentation, device pacing, Lotus racing performance or every OCS title.
No new engine changes were made during performance verification; the correction's
existing Release build and test results remain separate correctness evidence.

## Result

**VALID / OWNER ACCEPTED:** native gameplay passes, but both rendering workloads
are **INCONCLUSIVE** under the unchanged 1% upper-bound limit. No measured paired
mean exceeds 1%; the uncertainty prevents certifying the requested limit.

| Workload | Paired frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | --- |
| Lores Paula DMA | -0.3076% | +1.3998% | INCONCLUSIVE |
| Hires Paula DMA | +0.8489% | +2.4203% | INCONCLUSIVE |
| Native Lemmings | -3.0196% | -1.1329% | PASS |

Positive values mean slower execution. Arithmetic mean reference/candidate FPS
is 397.36 / 398.61, 358.47 / 355.46 and 336.20 / 346.65, respectively; the gate
uses paired geometric frame-time ratios rather than ratios of those FPS means.

All 36 runs match their prevalidated complete identities with zero measured
allocation and no unsupported bypass. The 1,540 telemetry intervals are valid;
the maximum gap is 1.14291 seconds. Longest continuous threshold excursions are
0 seconds on the selected CPU, 1.10430 seconds on its sibling and 1.14246 seconds
for aggregate load, all below the policy's ten-second rejection duration. No
active competing build, test or benchmark was detected. Every sample is retained.
The statistics, sample order, fingerprints, input transformation and telemetry
were independently recomputed and checked after the complete series finished.

The [machine-readable evidence](LOTUS_III_PERFORMANCE_2026-09-25.json) binds both
capsules, source change, runtime/protocol logs, inputs, scalar checks, gameplay
captures, all samples and telemetry. Local raw evidence remains under
`artifacts/lotus3-performance/performance-v1/`; preflight validation is separate
under `protocol-validation/`. ROMs, media and build/capture artifacts remain local.

On 2026-09-25, after reviewing these results, the owner stated: "I can accept this.
Please commit and push." This accepts the exact candidate and comparison above,
including the lores +1.3998% and hires +2.4203% upper bounds. Their statistical
dispositions remain INCONCLUSIVE; the exception does not turn them into measured
PASS results. All samples and the original protocol remain unchanged.

This exception applies only to the Lotus CPU bus-spacing correction. It does not
change the default 1% limit or extend to the earlier combined native-corrections
lores gate, which remains unmet.
