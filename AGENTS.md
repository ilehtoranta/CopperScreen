# CopperScreen product

This repository owns the desktop app, Lightweight A500 engine, CopperDisk,
their tests, headless runner, workloads and performance records. Copper68k is
a shared pinned NuGet dependency maintained in CopperMod. Do not add references
to a sibling CopperMod checkout or reintroduce the old UI-only package split.

Keep emulated hardware single-owner and release execution lightweight. A repository
move must not change cycle ordering, implemented hardware behavior or golden outputs.
Current machine scope and API are in `CopperMod.Amiga.Lightweight/README.md`;
execution contracts and unresolved edges are in `docs/engine/ARCHITECTURE.md`
and `docs/engine/ISSUES.md`. Lightweight is the active engine; Legacy remains
unavailable. CopperStart uses only the qualified standalone guest ROM and bounded
A500 configuration documented in `docs/COPPERSTART_APP.md`; do not restore host OS shims.

## Lightweight execution principles

Hardware correctness and a small execution cost are requirements together. Preserve
implemented behavior while simplifying how it runs; feature growth must not make
unfitted hardware or unused optional services part of the default A500 hot path.

- Retain one execution owner, one canonical integer CPU-cycle clock and explicit
  address/input/output phases. Keep compact device state and shared CPU/Copper
  register semantics. Do not introduce parallel timelines, generic event queues,
  requester graphs, per-device virtual dispatch or speculative schedule caches.
- Preserve every required CPU fetch, contention decision, accepted DMA transfer,
  pixel, collision, PCM sample and interrupt sample. Inactive or masked IPL can
  still require sampling. Matching a game or fingerprint does not justify skipping
  work that implements hardware behavior.
- Optional metadata, tracing, JIT and unfitted chipset features must stay behind
  their actual consumer boundary. Avoid constructing, zeroing or copying unused
  snapshots. Zero managed allocations alone does not establish a lightweight path:
  inspect stack copies, repeated branches, field loads and generated code too.
- Resolve immutable fitted traits outside recurring CCK work and specialize shared
  implementation where useful. Apply live register controls at their existing
  phases. Share preparation only over a proven stable interval; retain individual
  sprite/comparator, shifter, collision and transfer operations within it.
- Reuse output buffers and existing canonical state. Prefer removing redundant
  work to adding caches, mirrored state, callbacks or framework layers. A cache
  needs measured benefit and correct invalidation; its presence is not an
  optimization result.
- Keep diagnostics out of recurring Release hardware execution: no per-cycle
  logging, diagnostic polling, counters or callbacks. Preserve zero steady-state
  managed allocations for the default interpreter at stable output geometry;
  loading, geometry changes and optional compilation have separate costs. Use
  bounded external probes or separate diagnostic builds. Do not add production
  abstractions solely to make tests or profiles easier.
- Start from the actual workload profile and change one bounded source of cost.
  Freeze the reference before editing, check affected behavior, and inspect normal
  optimized code when the intended saving depends on JIT decisions. Smaller code
  or fewer source lines is not an FPS result. Measure throughput separately with
  unchanged work and inputs; disclose invalid timing and unmeasured retention.
- Fix shared CPU costs in Copper68k through its maintained package boundary.
  Do not work around them by weakening engine timing or adding sibling source
  references. Keep current limitations and evidence in the engine documentation.

## Validation and evidence

Build the production solution with `dotnet build CopperScreen.slnx -c Release`.
Run engine diagnostic tests separately with
`dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests`.
Do not put that diagnostic project into the production solution's shared outputs.
Host and disk tests are separate from native ROM/media replays. A skipped optional
test is unavailable coverage, not a successful replay.

Correctness and host throughput are separate results. Use
`docs/engine/PERFORMANCE.md` for measurement requirements and harness availability.
Preserve historical protocols and their verified P-core placement, Normal priority
and host-load policy v2. The retained native harnesses require a hybrid host and
older frozen inputs; the guide records the audited AMD host's incompatibility.
Recheck topology and input compatibility on each host. Portability needs a separately versioned, validated
protocol, not silently weakened placement or changed expected fingerprints.
Normal background applications are allowed; process presence is not contamination.
Noisy/missing-telemetry samples are INVALID / RERUN; never relabel older evidence.
Do not treat an ordinary short runner smoke test as an acceptance measurement.

G6/G7 Legacy cutover is retired. Completed Lightweight H-stage procedures and
dated approval/STOP/GO language under `docs/history/` are historical evidence,
not active instructions or prerequisites for ordinary development. The forwarding
notes under `CopperMod.Amiga/` point to current guidance. Documentation-only changes
need link/instruction checks, not emulator suites or new gameplay/FPS acceptance.

Never commit ROMs, game media, local build artifacts or credentials. Existing
published package versions are immutable. New source versions are development
versions until explicitly released; moving source does not authorize publication.
