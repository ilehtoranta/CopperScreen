# Engine diagnostic probes

These are separate, bounded development executables. They are not in the
production solution and must not run during formal retention measurement.
Their assembly name uses the engine's existing runner friend boundary so probes
can select scalar/batched execution or call a gateway without widening public APIs.
They reference this repository's engine and its exact pinned CPU package.

## Copper interrupt and polling timing

```powershell
dotnet run --project scripts/probes/CopperPollingTiming -c Release -- artifacts/new-polling-timing
```

Use a new output directory. This needs no commercial ROM or game media. It
generates an independently authored test ROM and runs 100 fields in scalar and
normal execution, checking chip/slow RAM parity. A Copper interrupt sets a flag;
the CPU loop clears and tests that flag with the addressing forms implicated in
Lotus III. The handler records interrupted PCs in slow RAM `$C09100` and the
consumed-edge count at `$C09008`. The generated ROM includes the interrupt-vector
bytes needed for a separate PAL OCS 512+512 KiB WinUAE comparison.

The count and PC distribution are diagnostic observations, not hardware golden
values or a gameplay test. The independent four-clock CPU-transfer regression
lives in `LightweightCpuBusSpacingTests`. See the
[investigation](../../docs/engine/LOTUS_III_CPU_BUS_TIMING_2026-09-25.md).

## CPU trace regression

```powershell
dotnet run --project scripts/probes/Copper68kTrace -c Release
```

No ROM or game media is needed. The synthetic ROM installs vector 9, enables T
and executes a NOP. The handler sets D0=42 and stops. The required 68000 exception
frame reduces A7 from `$70000` to `$6FFFA`. Development pin `1.4.1-trace.1` passes
in both execution modes. The earlier `1.4.1-boundary.1` instead executed the
failure marker D0=1 without a frame; that behavior remains a failure.

The earlier source's [ExecuteSingleInstruction](https://github.com/ilehtoranta/CopperMod/blob/713ad6c1bc1bc31efc038996c50faccec31208d6/Copper68k/M68kCore.cs#L2768)
has no architectural trace dispatch. Its exception-entry code clears T, but that
does not implement tracing at instruction retirement. The repair is committed in
the shared CPU project and covers trace/other-exception/STOP/RTE ordering and batch
admission. See [development source and restore instructions](../../docs/engine/CPU_TRACE.md).
This repository does not patch
the CPU DLL, duplicate an interpreter or simulate trace through an interrupt API.

## Active hardfile service throughput

```powershell
dotnet run --project scripts/probes/CopperHdfThroughput -c Release -- artifacts/new-disposable.hdf
```

The path must not exist. The probe creates a disposable 4 MiB file, warms up
sector read/write requests, then measures 131,072 requests per direction. It
checks error/actual counts, content and Update, and requires zero measured
steady-state allocations. Creation, JIT warmup and final flush are outside the
reported interval. Normal OS file caching applies. This measures synchronous
gateway service, not complete guest FPS, physical drive speed or retained-workload
acceptance. Record placement/host conditions and keep its result separate from
the unchanged retention protocol.
