# CopperOS graphics compiler audit, 2026-11-01

The producer audit and reproducible C# probes live in CopperStart:

- [Audit and migration gates](../../CopperStart/Docs/NativeGraphicsCompilerAudit20261101.md)
- [Probe harness](../../CopperStart/tools/GraphicsCompilerAudit/README.md)
- [Retained measurements](../../CopperStart/Docs/Measurements/NativeGraphicsCompilerAudit20261101.json)

The measured M68000 C# AND row loop costs 66 CPU cycles per row versus 38 in
the existing qualified kernel; explicit paired rows cost 55 versus 33.
These are flat-bus steady-state costs, not completed RectFill or desktop timing.
The audit identifies native memory/atomic semantics, memory-destination ALU
selection, address-register loops, DBRA, direct native entries, narrow arithmetic
and bounded helper inlining as the migration work. It also identifies SDK
wrapper annotations that can use existing compiler support.

The frozen `.53` ROM remains 524,288 bytes, with only 22 contiguous bytes free
before its footer. Migration must replace executable ranges and qualify the
entire linked image. No ROM/HDF, bundle manifest, production compiler or engine
was changed in this audit. No new full Kickstart/desktop replay is claimed.

The later [production C# migration](COPPEROS_CSHARP_GRAPHICS_2026-11-01.md)
replaces four public graphics routines, qualifies a complete `.54` ROM and
records new installed comparisons against `.53` and Kickstart. The figures above
remain the original audit baseline.
