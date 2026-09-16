# CopperScreen product

This repository owns the desktop app, Lightweight A500 engine, CopperDisk,
their tests, headless runner, workloads and performance records. Copper68k is
a shared pinned NuGet dependency maintained in CopperMod. Do not add references
to a sibling CopperMod checkout or reintroduce the old UI-only package split.

Keep emulated hardware single-owner and release execution lightweight. A repository
move must not change cycle ordering, implemented hardware behavior or golden outputs.
Current machine scope and unresolved edges are in
`CopperMod.Amiga/LIGHTWEIGHT_A500_ENGINE_PLAN.md` and
`CopperMod.Amiga/LIGHTWEIGHT_A500_ENGINE_ISSUES.md`.

Build the production solution with `dotnet build CopperScreen.slnx -c Release`.
Run engine diagnostic tests separately with
`dotnet test CopperMod.Amiga.Lightweight.Tests/CopperMod.Amiga.Lightweight.Tests.csproj -c Release --artifacts-path artifacts/diagnostic-tests`.
Do not put that diagnostic project into the production solution's shared outputs.
Host and disk tests are separate from native ROM/media replays. A skipped optional
test is unavailable coverage, not a successful replay.

Correctness and host throughput are separate results. Preserve existing formal
protocols, verified P-core placement, Normal priority and host-load policy v2.
Normal background applications are allowed; process presence is not contamination.
Use `CopperMod.Amiga/G6_HOST_MEASUREMENT_POLICY_V2.md` and the retained scripts.
Noisy/missing-telemetry samples are INVALID / RERUN; never relabel older evidence.
Do not treat an ordinary short runner smoke test as an acceptance measurement.

Never commit ROMs, game media, local build artifacts or credentials. Existing
published package versions are immutable. New source versions are development
versions until explicitly released; moving source does not authorize publication.
