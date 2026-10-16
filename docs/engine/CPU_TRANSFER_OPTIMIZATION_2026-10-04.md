# CPU-transfer bookkeeping — 2026-10-04

The next [bus investigation](BUS_CLOCK_INVESTIGATION_2026-10-03.md) target is
implemented as a bounded candidate. CPU word grants return at the data strobe
on the canonical clock; callbacks omit eight redundant strobe-advance checks.
The common Agnus classification also precedes CIA decoding. **Performance
acceptance remains PENDING** under owner-reported background load. Correctness
and generated-code diagnostics do not establish an FPS improvement.

The selected minimal **R1** includes the preceding
[following-slot prototype](BUS_PHASE_OPTIMIZATION_2026-10-04.md). That frozen
prototype is the incremental code-inspection reference. The unchanged,
[validated chipset R2](CHIPSET_REGRESSION_2026-10-03.md) is the reference for the
pending overall performance comparison. Neither its passing gate nor previous
exceptions transfer. Pre-ECS recovery remains pending at the owner's request.

The later [complete grant-locality comparison](GRANT_LOCALITY_2026-10-04.md)
includes this change and **passes all three 1% upper-bound gates** against
validated chipset R2. The frozen standalone variant described here remains
unmeasured; its individual contribution is not isolated. The pending language
below records that earlier stage, while the linked combined result is current.

## Transfer contract

Agnus arbitration already advances the machine to the returned grant cycle.
The removed caller advances therefore performed no work on chip, fitted slow
RAM and custom-register transfers. CIA and non-contended transfers still advance
hardware before any register or memory effect, through their existing grant
fallbacks. Completion advancement remains after the effect.

The implemented Agnus regions are disjoint from both CIA byte lanes, including
the second byte tested for word accesses. Classifying Agnus first avoids CIA
range/lane tests on the common RAM path. The 24-bit address masks, overlay
decision, requested/current-cycle clamp, ten-clock peripheral alignment and
unconnected lanes retain their existing behavior.

Long operands retain two grants and two memory effects. The first effect
precedes its completion and the second grant; Agnus requests remain four CPU
clocks apart. Blitter CPU-request begin/end, mandatory refresh, device ownership,
DMA completion/cancellation and the package's next-address-phase timing contract
are preserved. No device state, allocation, scheduler, dependency or CPU-core
semantics are added or changed. One engine source file differs from the frozen
following-slot candidate.

## Verification

| Check | Selected candidate |
| --- | --- |
| Production Release build | PASS, zero warnings/errors |
| Isolated engine diagnostics | **1,089 passed**, zero failed/skipped |
| CopperDisk tests | **74 passed**, zero failed/skipped |
| Native-enabled host suite | **163 passed**, same case set as validated chipset R2 |
| Full retained lores, hires, Lemmings and scalar Lemmings | Complete identities match; zero measured steady-state allocations |
| ECS SuperHires, programmed NTSC SuperHires and AGA RGB24, normal/scalar | **182 captured-file hashes match**; all six final disposable hardfiles match |

Eight new cases pass before and after the engine change. Six exercise byte,
word and long reads/writes in non-contended space across a field boundary,
including odd CPU phase and stale requests. Two exercise CIA byte/word interrupt
reads exactly at an aligned timer-underflow edge, then verify clear-on-read.
The existing odd-cycle ROM check now also verifies the owner clock. Existing
refresh, longword spacing, physical boundary, DMA contention, custom-read strobe,
blitter fairness and accelerator diagnostics also pass.

The first baseline-only field test incorrectly expected VERTB at field+1,
before its existing one-CCK latch. Moving the new test request to odd field+5
makes all nine focused baseline checks pass before any engine mutation. The
earlier failing log is retained; hardware behavior and golden outputs were
not changed. Final restored-source engine and host suites were repeated after
the separate code-layout experiment below.

All 47 selected source hashes and both frozen capsules verify. A fresh production
build reproduces the selected engine DLL SHA-256:
`DB373E3556BC87AD4EE58D877015EEB54D3A275E6EE2F92EB496F2EBEA366132`.
Only engine DLL/PDB differ in the retained comparison input bag. Its common XML
documentation remains identical to the reference, as in the prior harness;
current generated XML is retained separately and does not execute.

## Native generated code and variant selection

Complete native Lemmings replays captured Tier1 code on pinned .NET 10.0.12 at
Normal priority. Both frozen identities match. All diagnostic timings are
excluded because of background load and concurrent correctness work.

In the captured common chip-RAM `ReadWord` route, Agnus classification bypasses
four CIA lower-bound branches and the CIA-result branch. After arbitration,
execution reaches the memory decoder without the former owner-clock comparison:
**six conditional checks are removed from that route**. Its physical grant calls
and data/completion phases remain present. This counts the identified route,
not all dynamically executed branches in a frame.

| Captured Tier1 body | Following-slot reference bytes | Selected R1 bytes | Shared-fallback R2 experiment bytes |
| --- | ---: | ---: | ---: |
| `GrantCpuWord` | 588 | 655 | 543 |
| `ReadByte` | 880 | 894 | 2,163 |
| `ReadWord` | 1,086 | 1,098 | 1,011 |
| `ReadLong` | 2,231 | 2,254 | 4,485 |
| `WriteByte` | 2,016 | 2,141 | 2,390 |
| `WriteWord` | 1,395 | 1,410 | 2,570 |
| `WriteLong` | 2,842 | 2,887 | 4,402 |
| Seven-body sum | **11,038** | **11,339** | **17,564** |

The selected R1 gains a little cold fallback code while removing common-route
checks. A subsequent experiment shares one CIA/non-contended advance. It passes
the same complete correctness checks, but changes the JIT's inlining choices:
several callbacks acquire larger wait-loop copies. It remains frozen and
unselected. Its code growth is a locality concern, **not a measured throughput
regression**. These body sums exclude outlined helpers and shared callees;
they are not total JIT footprint, cache-miss or branch-miss measurements.

The [machine-readable record](CPU_TRANSFER_OPTIMIZATION_2026-10-04.json) retains
source/binary/input pins, raw test outcomes, replay identities, full body sizes,
branch/call counts and variant selection. Selected evidence is under
`artifacts/cpu-transfer-optimization-2026-10-04/`; the unselected experiment
and its independent validation are under its `r2/` subdirectory.

## Pending acceptance comparison

The prepared [CPU-transfer comparison v1](../../scripts/run-lightweight-cpu-transfer-comparison-v1.ps1)
compares validated chipset R2 with the complete selected candidate. It preserves
the validated homogeneous-host v2 workloads, six balanced pairs, placement,
Normal priority, full identities, zero-allocation requirement, runtime pins,
negative probes, rejection checks and host-load policy. Only labels, the
topology type name and frozen engine pins change. The historical and preceding
versioned scripts remain intact. AST/logic checks pass; live preflight and
throughput measurements remain pending.

Require valid telemetry and a **one-sided 95% upper frame-time regression bound
≤1% for each retained workload**. No FPS claim or exception request is warranted
from these under-load correctness and code-generation runs.
