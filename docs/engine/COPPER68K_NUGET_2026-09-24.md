# Copper68k NuGet package comparison

2026-09-24. **VALID; native Lemmings passes, and the owner accepted the lores and hires upper-bound exceptions for this exact package comparison.** Their statistical dispositions remain INCONCLUSIVE; the default 1% gate remains unchanged for other candidates.

NuGet.org lists stable Copper68k [**1.4.1**](https://www.nuget.org/packages/Copper68k/) as the latest version (updated September 24), and the project already pins 1.4.1. This is a newly published stable build at the same SemVer, not a higher-version package. The reference was the previously accepted `1.4.1-locality.1` development assembly; the candidate was the currently repository-signed NuGet 1.4.1 assembly. The NuGet lock content hash matches the content hash emitted by `dotnet nuget verify --all`. The comparison therefore measures the stable assembly against the earlier accepted CPU binary; the semver pin itself does not change.

## Method and identity

The two bundles use the same accepted Lightweight engine (`683C0AB519FBCCDE85770977269BB965FF88C39D20411C7550ACF62597B7A5E8`), runner, CopperDisk, CopperFloat, runtime, ROM, disk images and scripted inputs. Only `Copper68k.dll` changes: reference `982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F`; candidate `F82CA4A5FD953B0BEE31EE360212070548DB0DAA80874EEEC36DB0EFD1F1AFE1`. The candidate archive SHA-512 is `4307758F0E4E89C491535849800C16AD24F2E54DFBBF5B62A81BD616A6AB59D97DE9BB427241513049F5CF4138FF07892FDC911462BC9B4F17BDAAF1C2367606` and its repository signature verifies.

The unchanged v2 host-load policy ran on the Ryzen 5 5600X, logical CPU 2 with sibling 3 protected, Normal priority and pinned .NET 10.0.12. Six alternating pairs were measured for each workload. The gate is the one-sided 95% Student-t upper bound of paired log frame-time ratios (`n=6`, `df=5`, `t=2.015048`), capped at 1%. All 36 complete workload fingerprints match their previously accepted identities and each reports zero measured allocation. All 1,432 telemetry intervals are valid; host-load streaks remain under the policy's 10-second invalidation threshold.

| Workload | Reference mean FPS | NuGet 1.4.1 mean FPS | Paired frame-time change | One-sided 95% upper bound | Result |
|---|---:|---:|---:|---:|---|
| Lores | 400.02 | 396.65 | +0.8521% | +1.4785% | Inconclusive |
| Hires | 364.25 | 361.87 | +0.6605% | +1.9898% | Inconclusive |
| Native Lemmings | 337.97 | 346.31 | -2.4124% | -1.0405% | Pass |

Native Lemmings gains **2.47% paired FPS** in this comparison. Lores and hires have mean frame-time changes below 1%, but their one-sided upper bounds exceed the limit. On 2026-09-24, the owner accepted these two bounds for this exact package comparison; the raw statistical dispositions remain inconclusive. This does not change the default limit for future candidates. These headless engine throughput results do not represent desktop presentation FPS.

NuGet.org’s release notes report a passing result for the combined candidate. This comparison holds the accepted Lightweight engine fixed and isolates the CPU assembly change, so the paired results here are separate evidence and do not inherit that earlier outcome.

The result and all 36 raw samples are in the [machine-readable record](COPPER68K_NUGET_2026-09-24.json). The exact comparison is frozen in [protocol v1](../../scripts/run-lightweight-copper68k-nuget-comparison-v1.ps1); raw logs and host telemetry remain in the ignored local `artifacts/copper68k-package-benchmark/` folder. No package or production source files were changed for this measurement.
