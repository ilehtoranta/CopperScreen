# Copper68k 1.5.0 mainline release — 2026-09-27

The owner authorized publication and requested integration into mainline.
[Copper68k 1.5.0](https://www.nuget.org/packages/Copper68k/1.5.0) is the public
release of the validated CPU development work. The historical `ocs020` suffix
referred to the original OCS/68020 integration effort. A minor release now covers
the added CPU API, including experimental 030/040 and a diagnostic MC68060 core.
It does not certify complete advanced-CPU timing or accelerator compatibility.

## Source and package boundary

CopperMod merged the development branch into `main` while preserving the prior
CopperScreen extraction. The release source is
[`fd12917a8c19c172071f8a3b5d8d75a9c1a2b8b5`](https://github.com/ilehtoranta/CopperMod/commit/fd12917a8c19c172071f8a3b5d8d75a9c1a2b8b5),
tagged `copper68k-v1.5.0`. CPU implementation and test sources are unchanged from
the validated `.56` commit `d9123624f549137b272dfa714ef5c17f4e43aa46`;
package version, release documentation and packaging integration were updated.
The normal packing gate now checks the retained AHX consumer. Its old
pre-extraction Amiga test project depended on removed CopperStart components.
The CPU test gate remains in place; active emulator integration belongs here.

The [publishing workflow](https://github.com/ilehtoranta/CopperMod/actions/runs/36336934105)
uploaded both the package and symbols with NuGet Trusted Publishing. The
[mainline CPU/AHX CI](https://github.com/ilehtoranta/CopperMod/actions/runs/36336932496)
also passed. The trusted workflow keeps its historical filename because the
NuGet identity policy is bound to that path; it does not determine the version.

The package was downloaded anonymously from NuGet.org after indexing completed.
Its NuGet repository signature passes `dotnet nuget verify --all`, its repository
metadata names the release commit, and its 167 XML member names match `.56`.

| Public artifact | SHA-256 |
| --- | --- |
| `copper68k.1.5.0.nupkg` (345,427 bytes) | `cf2b293f5a2d7c18f29fca0e193c9d15cccc0c751576239c3e50bb147cadf470` |
| Packaged `Copper68k.dll` | `5df5bbd7c37e5995ef718f476db5d31a81efe41cee54861baaf2806c37155855` |

CopperScreen pins `1.5.0` in all three direct references and ten consumer/probe
lockfiles. The conditional local feed was removed. The production solution,
isolated engine diagnostics and probe projects restore from NuGet.org into a
fresh package cache with the HTTP cache disabled. The restored package matches
the anonymous download. No sibling source reference or media dependency was added.

## Validation

| Check | Result |
| --- | --- |
| Production Release solution build | Pass; zero warnings/errors |
| CPU suite using the exact downloaded DLL | 3,475 pass; 6 optional external cases unavailable |
| Retained AHX consumer in CopperMod | 18 pass |
| Engine diagnostics in separate artifacts output | 802 pass |
| Host tests | 112 pass; 3 optional native cases unavailable |
| Disk tests | 74 pass |
| Replay-tool negative controls | 21 pass |
| Original 040 Lotus III replay, 18,000 fields | All 95 captures match `.56` byte for byte |

The downloaded DLL is identical in the desktop, runner, engine diagnostics and
CPU test fixture. The Lotus replay preserves the ROM, both disk archives and ADF
members, and the original input script, including the press at field 6,500.
It retains the validated driving demo and menu continuation. Final identities
remain motherboard cycle `2557702545`, CPU `23F9E514495C578D`, hardware
`D3FAC3ED960A6990` and output `4F7F9395A4ED32CE`.

The [machine-readable manifest](COPPER68K_MAINLINE_1_5_0.json) records public
package provenance, commands, locks, test results, workflow receipts and all
capture hashes. Raw logs and local artifacts are under
`artifacts/copper68k-mainline-release/` and are not committed. The earlier
[`.56` fetch evidence](LOTUS_III_040_FETCH_2026-09-27.md) retains its candidate
identities, normal/scalar comparison and 000/030/HDF checks. Neither `.55` nor
`.56` was published; existing public package versions remain untouched.

## Retained limits

Advanced CPU profiles remain experimental. The 040 fetch implementation remains
demand-driven, with approximate instruction timing, cache geometry and pipeline
behavior. The 060 remains diagnostic-only and cannot boot the supported native
Kickstart 1.3 task/FPU layout. See the [CPU scope record](CPU_OPTIONS_030_040_060_2026-09-27.md).
Player-controlled Lotus III racing and full completion remain unverified.
Optional skipped cases are unavailable coverage, not successful native replays.
This is correctness and release validation; it does not replace any historical
throughput measurement or hardware golden.
