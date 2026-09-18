# CopperScreen product

This repository owns the desktop app, Lightweight A500 engine, CopperDisk,
their tests, headless runner, workloads and performance records. Copper68k is
a shared pinned NuGet dependency maintained in CopperMod. Do not add references
to a sibling CopperMod checkout or reintroduce the old UI-only package split.

Keep emulated hardware single-owner and release execution lightweight. A repository
move must not change cycle ordering, implemented hardware behavior or golden outputs.
Current machine scope and API are in `CopperMod.Amiga.Lightweight/README.md`;
execution contracts and unresolved edges are in `docs/engine/ARCHITECTURE.md`
and `docs/engine/ISSUES.md`. Lightweight is the active engine; Legacy and
CopperStart are unavailable in the standard application.

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
