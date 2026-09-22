# Managed assembly execution comparison

Read-only diagnostic for distinguishing CLR method/metadata changes from build
identity and debug information. It is outside the production solution and adds
no runtime dependency to the emulator.

```powershell
dotnet run --project scripts/probes/AssemblyExecutionComparison -c Release -- `
  path/to/reference.dll path/to/candidate.dll artifacts/assembly-comparison.json
```

The JSON separates `executableDifferences` from MVID, PE timestamp, debug-directory
and assembly informational-version differences. Check the arrays explicitly:
a successful tool exit means the comparison completed, not that inputs match.
Unknown static-initializer layouts and unsupported native/EnC metadata fail
instead of being omitted.

The checker compares raw method headers and IL, local signatures, exception
regions, implementation flags, type/field layouts, constants, reference token
maps, strings, resources and static data. Different metadata token numbering may
produce conservative differences even when higher-level behavior is equivalent.
This is not a general behavioral-equivalence proof or a throughput measurement.

The [2026-09-22 activation record](../../../docs/engine/CPU_PREFETCH_ACTIVATION_2026-09-22.md)
records matching CPU/engine executable inputs and a negative control that catches
all three changed inlining flags despite identical method IL.
