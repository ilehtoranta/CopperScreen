# Lemmings cache revert and bottleneck investigation — 2026-11-03

The interrupt-capability cache is removed. Corrected IPL sampling remains before
the inactive/masked acceptance guard, so both inactive and masked intervals still
update the CPU's sampling pipeline. Engine source now matches HEAD
`214d58a37d67c73256cd1fc2192d19da040ec967`; no hardware behavior, dependency pin or
golden was changed.

The [paired cache measurement](LEMMINGS_CACHE_2026-11-03.md) did not establish an
FPS benefit: paired change −0.7082%, two-sided 95% interval −2.0616% to +0.6640%.
That dated record remains unchanged. This investigation identifies larger current
costs and concrete optimization candidates. It does not attribute the historical
regression to one change or establish a new throughput gain.

The [machine-readable evidence](LEMMINGS_BOTTLENECKS_2026-11-03.json) records source,
binary, input, profile, generated-code and validation hashes. Raw local evidence
is under `artifacts/lemmings-bottlenecks-20261103/`.

## Revert validation

- Production Release solution build passes with **zero warnings/errors**, using
  isolated outputs and one build worker.
- **Five focused engine cases pass**, with zero failures/skips: the two repeated
  interrupt sampling cases (inactive and masked gaps), plus three interrupt-tail
  hardware-progress cases. Diagnostic tests use their separate artifacts path.
- **Two full native Lemmings replays match the complete current identity**, with
  real PCM, zero measured steady-state allocation and no unsupported operation.

Each replay uses 14,520 warmup plus 3,600 measured fields, PAL OCS/68000,
512 KiB Chip plus 512 KiB slow RAM, conservative interpreter batching and full
908×313 output. The ROM, two disk archives and portable input script are the
same inputs as the paired comparison. Both return:

```text
completed=18120 cycle=2574756906 cpu=0x0BC2B7554DA315AB
hardware=0x8F41668D6DF17C3C output=0x36816A410DDEEE52
pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=True unsupported=none
```

These checks exercise the retained behavior; they are not a new performance gate.
The full engine suite and capture comparisons from the
[earlier correctness/isolation work](INTERRUPT_BOOKKEEPING_2026-11-02.md) remain
separate evidence and were not rerun for this exact revert.

## Current profile

The separately versioned
[`profile-lemmings-bottlenecks-v1.ps1`](../../scripts/profile-lemmings-bottlenecks-v1.ps1)
uses a fresh frozen Release capsule, public Copper68k **1.5.4**, .NET **10.0.12**,
and `dotnet-trace` **10.0.745401** with `dotnet-sampled-thread-time`. It explicitly
requests `DOTNET_EventPipeThreadSamplingRate=1`; Microsoft documents this setting
and launched-child exit-code forwarding in the
[dotnet-trace reference](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).
No emulated-cycle counters or per-cycle instrumentation were added.

Topology is rechecked on the homogeneous Ryzen 5 5600X. Runner placement is CPU 2,
protected SMT pair 2–3, Normal priority; the collector uses CPU 0 on another
physical core, also Normal. Child pin delays are 0.199 and 0.178 seconds, before
the measured region. Power remains Balanced. All **151 telemetry intervals**
are present, with **zero competing-job intervals and zero policy-v2 violations**.
All frozen files still match. Profiling overhead and sampling limitations still
exclude these runs from throughput acceptance; their raw FPS is not used.

The accepted batch is **`profiles-4/`**, status `COMPLETE_DIAGNOSTIC_PROFILES`.
Three earlier controller attempts retain their failed status and original data:
collector runtime startup, unavailable reaped-child exit status, and unavailable
final child CPU accounting. The corrected controller holds a native child handle
and checks the collector's forwarded child exit code. Failed batches are not
substituted into the accepted pair.

[`analyze-lemmings-profile-v1.ps1`](../../scripts/analyze-lemmings-profile-v1.ps1)
selects an approximate interior measured-tail window: half the logged measurement
duration, capped at 30 seconds, ending one second before the final sampled
`ExecuteFrame`. Windows are **5.751 and 5.731 seconds**, with **79 and 78 attributed
stack intervals**; the longest intervals are **200.0 and 135.8 ms**. Requesting a
1 ms sampling rate did not produce 1 ms independent intervals in these exported
traces. Percentages are coarse sampled stack attribution, not hardware CPU-cycle
measurements or statistically bounded cost estimates. Inlined device work is
charged to its enclosing method. Inclusive callers overlap their children.

| Exclusive group | Run 1 | Run 2 |
| --- | ---: | ---: |
| CPU interpreter and work inlined into it | 33.20% | 33.45% |
| Device loop and work inlined into it | 28.53% | 30.35% |
| Clock and bus grants | 8.73% | 9.92% |
| Out-of-line bitplanes | 6.82% | 7.07% |
| Out-of-line video and sprites | 7.00% | 4.68% |
| Memory-copy helper | 1.18% | 5.25% |

The device-loop share is not dispatch overhead alone. Small groups vary markedly
(for example, out-of-line blitter 9.29% versus 1.79%); individual percentages must
not be treated as predicted savings. No standalone interrupt-sampling frame was
resolved in these windows, which does not mean its cost is zero.

## Prioritized candidates

### 1. Remove residual publication-context copies on the plain CPU bus

All five measured-window `Memmove` intervals have CPU prefetch/branch parents:
`TopUpPrefetchOne(out)`, `TopUpPrefetchAtRetirement`, or `ExecutePlannedBranch`.
The generated-code inspection gives a concrete explanation to investigate:

- Reflection against the exact profiled assemblies measures
  `M68kInstructionFetchPublicationContext` at **152 managed bytes**, and confirms
  the default A500 CPU's `_instructionFetchWindowBus` is **null**.
- `CaptureInstructionFetchPublicationContext` already returns `default` for that
  case. Adding another guard around snapshot construction would duplicate it.
- Nevertheless, optimized Tier1 `TopUpPrefetchOne(out)` checks the null consumer,
  zeroes the default context, then passes **152 bytes to `CORINFO_HELP_MEMCPY`**
  before proceeding with the fetch. Additional context-copy sites survive in
  retirement paths. The null-consumer path therefore retains stack zero/copy work.
- Tier1 sizes are 2,743 bytes for `TopUpPrefetchOne(out)` and 3,432 bytes for
  `TopUpPrefetchAtRetirement`; size alone is not a performance result.

The next CPU experiment should isolate a plain-bus path that avoids this unused
metadata plumbing while preserving fetches, pending-prefetch ownership, bus
timing, interrupt samples and queue semantics. Trace attribution does not prove
every sampled memory move is this particular context copy, or quantify a gain.

This belongs in the shared Copper68k producer and a separately versioned
development package. The exact published source was inspected read-only at
commit `535e9ecfc543931b46f7c332d9593805382bd7cf`, matching package provenance.
No sibling project reference, package change or source edit was introduced.
The published source explicitly records that a previous blanket readonly-reference
experiment enlarged this path and regressed a direct-bus ROM workload. A wholesale
change from value parameters to `in` is therefore not the proposed experiment.

### 2. Share stable preparation within an ordinary visible hires CCK

The [October profile](PROFILE_2026-10-12.md) candidate is still present in
[`LightweightVideo.RenderHiresCck`](../../CopperMod.Amiga.Lightweight/LightweightVideo.cs).
Its ordinary visible path calls `RenderHiresVisiblePair` twice without device
input between them. Optimized Tier1 code is **903 bytes**, with **two surviving
dual-playfield mode tests** (`0x0400`). Mode selection, placement/output-enable
and palette/output setup are candidates to share across the four output pixels.

Both sprite comparator/compositor operations must remain: sprite state can change
at the second lores position. Keep collisions, shifter/reload order, borders,
display-window edges and dual-playfield behavior. Start with this bounded renderer
change before considering device scheduling changes.

### 3. Simplify the proven active OCS bitplane path

[`LightweightBitplanes.Step`](../../CopperMod.Amiga.Lightweight/LightweightBitplanes.cs)
still passes through repeated active-run checks in `AdvanceDdfControl`,
`TryAcceptInput` and next-event publication. The optimized OCS bodies are **998**
and **439 bytes** for `Step` and `AdvanceDdfControl`. OCS also loads `_ddfMask`
although this specialization uses the constant **`0xFC`**.

A narrowly proven active path could share control decisions and expose that
constant. It must preserve pending output before new control/input, DMA/BPU
cancellation, same-CCK stop/start rules, terminal/modulo phases and beam/field
sync. Raw DDFSTOP is already cached on the machine; adding another register cache
is not needed. This rechecks the earlier candidate, not a measured improvement.

## Boundaries for follow-up

The fresh code inspection is separate from the profiles: the same frozen capsule
runs 14,520 warmup plus 600 inspection fields with selected JIT disassembly and
ordinary tiering/PGO defaults. Its shorter identity is recorded separately and
is not compared to the full 18,120-field fingerprint.

`LightweightVideo.CompleteFrame` already swaps rendering/completed buffers; the
sampled CPU copies do not justify a framebuffer-copy optimization. The device
deadline path already skips the full minimum chain after an active video tick.
Clock/bus samples support inspecting their concrete callees, not replacing the
single-owner scheduler, skipping hardware phases or adding broad batching.

No speculative optimization is applied in this investigation. Each subsequent
candidate needs appropriate timing/behavior tests and native identity/capture
parity, followed by a separate paired unprofiled measurement under the current
versioned protocol. The existing three-workload retention and release boundaries
remain unchanged.
