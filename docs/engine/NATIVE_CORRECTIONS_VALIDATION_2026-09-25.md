# Native corrections: hidden-part and throughput validation

Desert Dream's two hidden sections now have a native replay through the final
greetings. No further engine change was needed. The combined performance
comparison for the Alien Breed byte-WORDSYNC, Desert Strike end-stop and Desert
Dream live-pointer corrections is **VALID**, but low-resolution throughput does
not meet the default limit. Hires and native Lemmings pass.

The [machine-readable record](NATIVE_CORRECTIONS_VALIDATION_2026-09-25.json)
contains all 36 timing samples, complete identities, independently checked
statistics, exact binary/input identities and hashes of local evidence.

## Desert Dream hidden sequence

Boot the original **disk B** in DF0, then hold both mouse buttons and the
joystick fire button together. The original bootblock polls CIA-A PRA bits 7
and 6 plus bit 2 of the POTINP high byte at file offsets `$60`, `$6A` and `$74`.
Once all three are low, it loads five sectors starting at `$66F` into `$7A000`.
This independently agrees with `RunHiddenPart` in StingRay's source, supplied
with the [WHDLoad installer](https://www.whdload.de/demos/Kefrens_DesertDream.html).
The replay uses the original disks, not that installer or its guest patches.

The [retained input script](../../CopperScreen.Lightweight.Tests/Workloads/desert-dream-secret-part.json)
presses mouse buttons 1+2 and joystick-port-1 fire at field 1,000, then releases
them at 1,120. The field-1,000 capture confirms the original input wait before
the press. The first section introduces a line effect; it automatically loads
the second section at `$40000`, containing the animated checkerboard, Kefrens
logo, credits and greetings. Final greetings are visible by field 12,000 and
remain visible with an animated background through field 30,000.

Both normal and scalar runs complete **30,000 fields**, with no unsupported
feature stop. All **120** saved BMP, CPU/device-state, chip-RAM and slow-RAM files
are byte-identical between the two modes. Final PC is `$4000E`, cycle
`4,262,928,034`. This extends the separately retained
[72,000-field main-demo run](DESERT_DREAM_LIVE_POINTERS_2026-09-25.md).

Profile: PAL OCS A500, 68000, 512 KiB chip plus 512 KiB slow RAM, Kickstart 1.3,
read-only DD DF0 and 908-wide output. The disk-B SHA256 is
`3E34D376C9805E0E64A3227B04E0D0BCE0C1531F3E90938329BF5079CD214F97`.
The external capture probe loads the unchanged production engine with SHA256
`7EB9C87905BD8CA790DFF21BB918A0B34F7E4B94FE8489F673058BC4D1581FDC`.

Installed WinUAE 6.0.3.0, using the same media and PAL OCS memory/CPU profile,
also enters through the three-button wait and reaches the second section's
final greetings. CPU, memory and blitter cycle-exact modes are enabled, JIT
and waiting-blits disabled, and drive speed is 100%. Official input events,
read-only debugger observations and screenshots are retained. This supports
the sequence and visible result; it is not a frame-aligned comparison or a
physical-hardware oracle. Per-pixel and audio-sample accuracy remain unverified.

With the supplied local media, reproduce the sequence using:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/Desert Dream (Kefrens).zip#/Desert Dream (Kefrens) B.adf' `
  --input-script CopperScreen.Lightweight.Tests/Workloads/desert-dream-secret-part.json `
  --wide-output --frames 30000 --boot-probe <fresh-output-directory>
```

Add `--scalar-cpu` for the scalar control. The external probe saved every
1,000 fields; the production boot probe has a different capture interval.

## Combined performance result

The separately pinned
[native-corrections comparison v1](../../scripts/run-lightweight-native-corrections-comparison-v1.ps1)
compares the frozen pre-investigation engine (`4333ABB6…`) with the current
engine (`7EB9C879…`). It measures all three compatibility corrections together;
it cannot attribute cost to one correction. Both capsules use the **same frozen
runner and dependencies**, stable Copper68k 1.4.1 and .NET 10.0.12. The latest
rebuilt runner was not substituted on only one side.

The host topology was rechecked: homogeneous AMD Ryzen 5 5600X, 12 logical
processors, selected logical CPU 2 with SMT sibling 3 protected. Normal priority,
affinity, host-load policy v2, ten-second cooldowns, frozen workload lengths and
six balanced pairs per workload are unchanged. The sample order is
`R1 C1 C2 R2 R3 C3 C4 R4 R5 C5 C6 R6`. All telemetry remains valid, and all
36 complete CPU/hardware/output identities match with zero measured allocation.
Intentional telemetry-rejection probes appear in the transcript before sampling;
they are preflight checks, not invalid measurement intervals.

| Workload | Paired frame-time change | One-sided 95% upper bound | Default disposition |
| --- | ---: | ---: | --- |
| Lores Paula DMA | +1.4898% | +3.3621% | ACCEPTANCE_REQUIRED |
| Hires Paula DMA | -0.6733% | +0.4938% | PASS |
| Native Lemmings | -1.0536% | +0.3907% | PASS |

Positive values mean slower execution. Statistics use paired log ratios and
Student t with five degrees of freedom, independently recomputed from the
recorded samples. The default upper-bound limit remains **1%**. The low-resolution
result is not accepted by this report, and no earlier package-specific exception
is extended to these corrections. Its samples are valid and must not be relabeled
as noise or replaced selectively. Any optimization needs a new complete comparison;
an owner exception would need to cover this exact result explicitly.

The earlier isolated WORDSYNC comparison remains **INVALID / RERUN**, with its
original evidence intact. This new combined measurement establishes current-build
throughput but does not turn that earlier experiment into a valid isolated result.

## Validation boundary

This follow-up adds a workload script, a frozen comparison protocol and evidence;
it makes no engine changes. The previous production Release build and **726**
passing engine tests therefore remain the relevant build/test result; those suites
were not repeated for documentation and replay inputs. Protocol preflight, all
36 measured identities, all 120 normal/scalar capture hashes and the statistics
were checked. ROMs, media, binaries and captures remain ignored local artifacts
under `artifacts/native-corrections-validation/`.

Desert Dream's main and hidden sequences are now covered by bounded native runs.
Active BLTSIZE restart, coincident pointer/output writes, live control/modulo/data
changes and active line-mode reprogramming remain open hardware-model edges.
This is not a general OCS conformance sign-off.
