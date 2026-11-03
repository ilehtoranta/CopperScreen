# Interrupt bookkeeping cost isolation

This diagnostic calls the real frozen engine's interrupt adapter with inactive
IPL or masked IPL3. Each call receives a distinct late sample cycle, rather than
repeatedly using the sampler's cached result for one instruction. It performs no
guest instructions, bus transfers, video, DMA or audio and supplies no native FPS
measurement.

Reflection and expression compilation occur before measurement. The timed loop
contains a common compiled sample setter, a dispatch delegate and loop arithmetic.
Private sample injection is confined to this executable. It targets the pinned
Copper68k 1.5.4 ABI, using a sample with no deferred-fetch token; a changed ABI
requires separately validated tooling. No such reflection enters the engine.

The frozen variants are the known incorrect historical gated adapter, the current
correct adapter, and the cached-interface adapter. The gated variant is a cost
control only and must not be used as a product optimization. Both correct variants
must evaluate every final sample, reject the masked/inactive requests, allocate
zero bytes in the measured loop and finish with identical IPL pipeline states.

Build this project in Release with isolated outputs. The separately versioned
[`measure-interrupt-bookkeeping-v1.ps1`](../../measure-interrupt-bookkeeping-v1.ps1)
runs six balanced permutations, twenty million polls per scenario, and records
thread CPU time, thread cycles, wall time, frozen files, topology, placement and
unchanged host-load policy v2. Tiering is disabled for these FullOpts comparisons;
they do not model production dynamic PGO. Blocking GC applies only to the probe.
Explicit loaded diagnostics retain every violation as INVALID / RERUN.

See the [November 2 investigation](../../../docs/engine/INTERRUPT_BOOKKEEPING_2026-11-02.md)
for exact capsules, results, correctness checks and the pending native performance
acceptance boundary.
