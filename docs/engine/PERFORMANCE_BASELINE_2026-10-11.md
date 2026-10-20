# Current Lightweight baseline under background load — 2026-10-11

The owner requested a rough baseline while other tasks continued. The current
production Release build completed with zero warnings and errors at HEAD
`575d5cf81681c7ffa5c7d0cd40bbda6a7d16af53`, using Copper68k `1.5.4-detroit-dev.2` and the local CopperDisk
`3.0.0` source project. A frozen runner bundle was measured serially, three full
runs per workload. No other jobs were paused, stopped or excluded.

These are **descriptive loaded-host rates only**. They do not supply an idle
baseline, a causal comparison with earlier builds, a performance acceptance
measurement or a 1% regression decision. Formal host-load policy v2 and all
earlier valid, invalid and pending records remain unchanged. This collection
observed load with the existing telemetry helper but deliberately did not apply
its rejection policy, as expressly requested by the owner.

## Results

FPS measures uncapped headless engine throughput, including ordinary framebuffer
and real PCM production. It is not desktop presentation FPS. Means are arithmetic
means of the three per-run FPS values; all runs are included without trimming.
The measured profile is PAL OCS/68000 with 512 KiB Chip and 512 KiB slow RAM,
conservative interpreter batching, one floppy drive, no Fast RAM, RTC, hardfile
or shared directory. ECS/AGA, JIT and upgraded-CPU throughput are not measured here.

| Workload | Mean FPS | Median FPS | Range | FPS in workload run order |
| --- | ---: | ---: | ---: | --- |
| lores | 237.13 | 259.48 | 151.35–300.56 | 151.35, 259.48, 300.56 |
| hires | 177.57 | 188.16 | 110.23–234.31 | 110.23, 234.31, 188.16 |
| lemmings | 240.32 | 226.61 | 220.09–274.25 | 220.09, 226.61, 274.25 |

The order was lores, hires, Lemmings, Lemmings, hires, lores, hires, Lemmings,
lores. Lores/hires each used 600 warmup and 7,200 measured complete fields.
Lemmings used 14,520 warmup and 3,600 measured complete fields, with the
September 25 CPU/bus-timing input script, including the second-disk swap.
Warmup, media preparation and output reporting are outside the runner timer.

All **nine runs** reproduce the pre-existing complete workload identities in
`scripts/run-lightweight-grant-locality-comparison-v1.ps1`, including field/cycle
counts, CPU state, hardware state, pixels and sampled picture/PCM output.
The current runner adds `executionMode=Interpreter`; this value was explicitly
checked and only that metadata field was removed for comparison with the older
identities. No hardware/output identity or expected value was changed.
Every run reports **zero measured thread allocations**, real PCM and
`unsupported=none`. A separate log read and summary calculation verify all results.
This is bounded retained-workload verification, not complete emulation or
correctness coverage for every recent change. No emulator test suites or extra
native compatibility replays were run for this benchmark-only request.

## Host and observed load

Host: AMD Ryzen 5 5600X, six cores/twelve logical processors, Windows
`10.0.26200`, .NET SDK `10.0.401`, runtime `10.0.12`, Balanced power plan.
Windows CPU-set topology was rechecked. The runner was pinned to logical CPU 2
at Normal priority; SMT sibling 3 was observed. Placement was checked while each
process ran. This separately identified descriptive collector does not alter a
historical hybrid-host or homogeneous-host acceptance protocol.

There were 243 telemetry intervals and 0 telemetry errors.
Other build/test jobs were observed in 243 intervals. Peak sampled
package CPU activity excluding the runner reached 99.46%.
The following averages are weighted by observation duration and cover warmup
and measurement together; they are not isolated measured-field load averages.

| Workload | Selected CPU, other activity | SMT sibling activity | Package, other activity |
| --- | ---: | ---: | ---: |
| lores | 19.61% | 53.47% | 60.56% |
| hires | 30.36% | 73.63% | 76.78% |
| lemmings | 17.24% | 64.34% | 64.07% |

Changing interference and SMT contention can affect these rates materially.
No regression percentage or confidence bound against the October 4 accepted
candidate is inferred. An idle, frozen paired comparison is needed to assess
the cumulative performance effect of the intervening changes.

## Reproduction and provenance

Production build:

```powershell
dotnet build CopperScreen.slnx -c Release -p:LightweightDiagnostics=false
```

Copy the current Release runner output into a frozen directory. Run its DLL with
Normal priority and affinity mask `4` (logical CPU 2 on this rechecked host):

```text
dotnet <capsule>/CopperMod.Amiga.Lightweight.Runner.dll --synthetic-paula-dma --wide-output --warmup 600 --frames 7200
dotnet <capsule>/CopperMod.Amiga.Lightweight.Runner.dll --synthetic-paula-dma --hires --warmup 600 --frames 7200
dotnet <capsule>/CopperMod.Amiga.Lightweight.Runner.dll --rom <Kickstart_13.rom> --adf <Lemmings-disk1.zip> --input-script <lemmings-cpu-bus-timing-v1.json> --warmup 14520 --frames 3600
```

The workload JSON was copied byte-for-byte into the local evidence directory so
its unchanged `../../media/lemmings-disk2.zip` reference resolves to repository
`media/`. Its events and disk bytes were unchanged. Local ROMs and ZIP archives
were read only and are not included in the repository.

| Input | SHA-256 |
| --- | --- |
| `Kickstart_13.rom` | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| `Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip` | `5FC56B436722688BA37836E273A3A1C84D724235A91B39272768FF3E4CAC0A59` |
| `lemmings-cpu-bus-timing-v1.json` | `2A02229461094001E2BE4A65E3114338CF96A1C5003CD59C532927C586CE44BA` |
| `lemmings-disk2.zip` | `0C1CB9C49CBC29B56D2F974A90C510752E256148F8E8CC10CFBD026DB73DEA1A` |

| Frozen assembly | SHA-256 |
| --- | --- |
| `Copper68k.dll` | `5780F5084FD44E2D4732969BC0F9CE7AE7E3BEFFC1135DED92BC46D9B21399F2` |
| `CopperDisk.dll` | `55C1F52FDBF12022ABD97C22951D0C7F5EA751411A7C5D8C1C74836881EA0BBA` |
| `CopperFloat.dll` | `4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235` |
| `CopperMod.Amiga.Lightweight.dll` | `0406A9652DED281326B32675D472631F212AF2C586961B599F3858EEF1DD5A47` |
| `CopperMod.Amiga.Lightweight.Runner.dll` | `F76F7FA6BBD7BB58DB999FA77768BF367CDA8C17213440BAE04A71EDE4D35A67` |

Before/after hashes confirm that all captured source files, frozen binaries,
native inputs and collector inputs remained unchanged throughout collection.
The unrelated pre-existing edit to `scripts/probes/Detroit/analyze_gameplay.py`
was left untouched. No engine source was changed or package published.

The [durable JSON record](PERFORMANCE_BASELINE_2026-10-11.json) stores all nine
FPS values, complete fingerprints, arguments, input/binary hashes, topology and
load summary. Local build logs, stdout/stderr, raw telemetry, source/file manifests,
collector and frozen bundle remain under `artifacts/background-baseline-2026-10-11/`.
They are not committed. Collector SHA-256: `45ABDAFD3ED5E823E3530CEC420810B14005BB2582C483B3166A0241CCF350B0`.
