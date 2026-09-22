# Measured optimization targets — 2026-09-22

Follow-up: the [empty-shifter experiment](EMPTY_SHIFTER_OPTIMIZATION_2026-09-22.md)
completed its full comparison and was discarded at the owner's request. Its
counted redundant work did not yield a worthwhile retained improvement. The
ranking below records the original investigation, not a recommendation to repeat
the rejected fast path.

The first concrete target is **zero-state bitplane shifting**, followed by
**clock advancement requested at the current cycle**. Both perform measurable
redundant work in the accepted engine. Neither has a demonstrated FPS gain yet.
The earlier [profile](PROFILE_2026-09-22.md) located busy code; its percentages
alone did not establish these targets. The discarded
[video trials](VIDEO_OPTIMIZATION_TRIALS_2026-09-22.md) remain unchanged evidence.

## Evidence and scope

An isolated source copy of `9be9feef098c9850ee7d8e645121593551a11bb3` counted
operations over the complete retained lores, hires and native Lemmings workloads.
Counters reset after warmup and are copied after the allocation measurement.
All three complete CPU/hardware/output fingerprints match the accepted engine;
all report zero measured steady-state allocations. ROM, both disk archives and
input-script hashes also match the prior profile. Production sources are unchanged.

Instrumentation changes generated code and execution cost. Its FPS values are
not performance evidence. Separately, generated-code inspection used the accepted
uninstrumented binaries. The native disassembly run also completed with the
expected full fingerprint. These results establish operation frequency and
instruction structure, not a speedup or a performance acceptance result.

| Measured operation | Lores, 7,200 frames | Hires, 7,200 frames | Lemmings, 3,600 frames |
| --- | ---: | ---: | ---: |
| Pair shifts with both shifters zero and no pending reload | 214,812,000 | 431,467,204 | 256,861,374 |
| Share of all pair shifts | 41.99% | 42.17% | 71.73% |
| Such pair shifts per frame | 29,835 | 59,926 | 71,350 |
| Advance requests whose target equals the current cycle | 194,400 | 194,400 | 65,354,247 |
| Share of all AdvanceTo entries | 49.09% | 49.09% | 35.02% |
| Such advance requests per frame | 27 | 27 | 18,154 |

## 1. Avoid shifts and stores for an already-empty bitplane shifter

In [ShiftPlayfieldPair](../../CopperMod.Amiga.Lightweight/LightweightVideo.cs),
the no-reload path always extracts pixels, shifts and combines the 96-bit state,
then stores its high and low parts. When both parts are zero, its output and
resulting state are necessarily zero. The baseline generated code confirms a
32-bit store and a 64-bit store, plus shift/merge operations, on this path.

The Lemmings run executes **513,722,748 redundant zero stores** here; hires
executes **862,934,408**. These are instructions writing existing shifter fields,
not a claim about DRAM traffic. A guard after the existing pending-reload check
could return zero when both fields are zero. It must still execute all surrounding
rendering, sprite/collision processing, pending input handling and clock activity.
Blank output alone is insufficient: nonzero hidden shifter state must keep moving.

This is the best first experiment because it identifies specific arithmetic and
stores to remove, with substantial opportunity in all three workloads. The
uncertainty is equally specific: the new test and branch affect every no-reload
pair, and prediction or JIT layout could erase the saving. Counter frequency is
not a CPU-time percentage. Inspect the uninstrumented candidate's generated code
and compare complete workloads before retaining it.

Correctness checks must cover zero-to-nonzero reloads, independent odd/even reload
timing, disabled planes subsequently enabled, border transitions, hires pairs,
HAM hold and sprite/collision output. Do not skip a CCK or defer a register effect.

## 2. Short-circuit clock advancement at the current cycle

[AdvanceToCpuGrant](../../CopperMod.Amiga.Lightweight/LightweightClock.cs)
updates the CPU request candidate, enters `AdvanceTo(firstCandidate, machine)`,
and then resolves output ownership. `AdvanceTo` changes no state when its target
equals `Cycle`. The only other production caller, `AdvanceHardwareTo`, already
guards with `cycle > _clock.Cycle`; source inspection therefore attributes the
counted equal-target entries to the CPU-grant path.

This matters primarily for native execution: 65.4 million entries, versus only
194,400 in each synthetic workload. The generated native Tier1 code **inlines
AdvanceTo**. Consequently, this is a target for removing repeated range/phase/
loop-entry/final-bound checks, not eliminating millions of machine-code calls or
stack prologues. The equal-target path still walks those checks in optimized code.

A candidate could guard this advancement with `firstCandidate != Cycle`, while
unconditionally preserving `UpdateCpuRequestCandidate` and the ownership/wait
loop. Using `!=` preserves the backward-target exception; a plain `>` would not.
Its benefit is likely smaller than counting source-level calls suggests, and the
added branch on progressing requests must be measured. Preserve same-cycle bus
ownership, blitter arbitration, partial CPU phases and interrupt retirement.

## Leads that are not yet established targets

Copper executes 511,560,000 / 511,560,002 / 199,429,223 steps in the three runs.
Source inspection suggests some only republish the next deadline: there is no
accepted word completion and the current phase cannot accept an instruction.
However, step totals do not measure how often both conditions hold. A second,
43-counter probe built successfully but Windows Application Control blocked its
runner. A bounded retry using the unchanged first probe's runner then blocked
the extended engine (`0x800711C7`). There are **no extended-probe results**.

Do not rank Copper deadline changes as a quantified saving yet. Any investigation
must distinguish ordinary empty phases from the wrap dummy, and preserve accepted
outputs, H223–H226/wrap behavior, COPJMP, DMACON, beam changes and blitter-dependent
WAIT. Repeated WAIT mask/target decoding is another unquantified possibility.

Frequent failed device-deadline checks also do not establish waste: they may be
cheap predicted branches preserving required order. Sprite rendering already has
an empty-state fast path. A zero bitplane reload cannot simply be discarded,
because it may need to clear nonzero shifter bits. These are not justified targets
merely because their containing methods were hot.

## Validation and reproduction

[Machine-readable evidence](OPTIMIZATION_TARGETS_2026-09-22.json) contains all 31
counters, derived rates, full fingerprints, input and binary hashes, generated-code
hashes, and the blocked extended-probe attempts. Ignored local evidence is in
`artifacts/target-analysis-2026-09-22/`. `source.zip` preserves the clean source
subset; `instrument.py` reproduces the successful probe from that archive.
`analyze.py` checks exact fingerprints, counter arithmetic, input hashes and the
native generated-code run before writing the JSON record. The later
`instrument-copper.py` describes the blocked extension; it is not needed to
reproduce the successful probe. The isolated project builds with the repository's
NuGet configuration and locked restore, into its own artifacts directory.

Commands for the successful probe use the same runner arguments as the retained
workloads: lores `--synthetic-paula-dma --wide-output --warmup 600 --frames 7200`;
hires `--synthetic-paula-dma --hires --warmup 600 --frames 7200`; native uses the
recorded Kickstart 1.3 ROM, Lemmings disk 1 archive and
`CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json`, with
`--warmup 10920 --frames 3600`.

No production optimization, acceptance benchmark or new waiver is included.
Any retained candidate still requires affected correctness checks, matching full
workload fingerprints, zero measured steady-state allocations, and the unchanged
complete comparison protocol. The one-sided 95% upper frame-time regression
bound must be at most 1% for every retained workload unless the owner accepts
that candidate's complete result. A 5% gain remains an objective, not a forecast.
