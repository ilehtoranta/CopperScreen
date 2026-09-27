# Lotus III: 040 uncached instruction-fetch correction

The original Lotus III input now passes the disk-two prompt on the experimental
040, reaches the title and runs a driving demo. The first press remains at field
6,500 and its release at 6,520. No game bytes, disk contents, input timings,
chipset rules or per-instruction delay constants were changed.

The correction is in Copper68k owner commit
[`d9123624f549137b272dfa714ef5c17f4e43aa46`](https://github.com/ilehtoranta/CopperMod/commit/d9123624f549137b272dfa714ef5c17f4e43aa46),
packed once as **local, unpublished `1.4.2-ocs020.56`**. CopperScreen consumes
that package through its normal pinned dependency. This is an architectural
fetch correction within the experimental profile, not full 040 timing certification.

## Independent discrepancy and bounded implementation

The [earlier investigation](LOTUS_III_040_INVESTIGATION_2026-09-27.md) showed an
input race: an interrupt arriving after BTST preserves its failed result through
RTE, and the next main-loop iteration clears the new edge. Its shifted-input
experiment established continuation, but did not establish correct CPU timing.

[MC68040UM](https://www.nxp.com/docs/en/reference-manual/MC68040UM.pdf), section
4.2 and the paragraph following table 7-3, provide an independent fetch contract:
uncached instruction transfers use aligned longwords starting at a half-line
boundary. A holding register can retain loops within the first six bytes of that
half-line even when CACR.IE is clear. The `.55` core instead fetched individual
words at the requested PC and retained no such uncached loop.

The new frontend reads the required aligned longwords through the existing bus;
the fitted OCS bridge still splits them into 16-bit transfers. It retains the
documented short-loop case, including across taken branches, and observes reset,
instruction-cache maintenance, privilege/translation changes and host mapping
generations. Ordinary data writes do not silently refresh held instructions.

The 040's integer fallback also had a separate 68000 fetch queue. A discriminating
EOR test showed it executing changed memory despite an already held opcode. It
now delegates opcode and extension reads to the 040 frontend; its 68000 prefetch
queue stays inactive. JIT max-speed fetching keeps its existing policy.

This frontend remains **demand-driven**. Consuming the fourth word retires the
half-line; speculative reads of the next half-line, their deferred faults and
pipeline overlap are not implemented. Enabled-cache geometry/fills, fallback
operand timing and the fixed instruction-cost model remain approximations.
Chapter 10's BCLK pipeline timings cannot be implemented by simply adding one
delay to every instruction. No claim is made that every bus phase now matches
physical 040 hardware or a particular accelerator board.

## Discriminating checks

The first nine ROM-free cases produced seven failures on the unchanged `.55`
source: five transfer alignment/width cases and two retained-loop cases. Reset/
replacement and the interrupt-return control passed. A further fallback case
failed before its fetch path was unified. The completed 22-case regression covers
alignment, retention/replacement, reset, context and host-map changes, cache
maintenance, optimized/scalar execution and interrupts on both sides of BTST.

Four existing assertions assumed disabled caches always reread modified code.
Their fixtures now execute outside the half-line before checking reread behavior,
so they continue testing CACR selection and refreshed FPU extension decoding.
The shared test bus also counts longword instruction reads, keeping transfer
comparisons meaningful.

The [Copper polling probe](../../scripts/probes/CopperPollingTiming/README.md)
now accepts 030 and 040. Its independently authored ROM bytes are unchanged:
SHA-256 `b4bbf99e61796e74ca96a880080b944b68d8ee3b3bfa22ba56ce49076426373b`.
In 100 fields the 040 consumes 33 edges before and 51 after, with matching
normal/scalar RAM in each case. The 030 stays at 35 with all seven output files
byte-identical. These counts describe the execution policies; a larger count is
not evidence of more accurate hardware timing.

The primary manual supplies the architectural expectations. The previous WinUAE
manual-input continuation remains supporting compatibility evidence; it is not
a matching fixed-field trace or a timing oracle for this correction.

## Original input and native results

PAL OCS, 512 KiB Chip + 512 KiB slow RAM, no Fast RAM, matching Kickstart 1.3 and
both supplied FLT/Crack Inc disk images were retained. The original effective
input hash is `a5fb0d22f79daeecefaa02e2cc2cf05ba39bf209a683de95e5e02e4fceb3e649`.
The earlier `.55` failure and two-field input variant remain unchanged evidence.

The new trace records the field-6,500 interrupt saving PC **`$741A6`**, before
the bit test. The handler writes `$10` at motherboard cycle `923642746`; RTE
returns at `923644123`. BTST reads the edge and execution reaches its consumption
path at **`$741B8`**, cycle `923644190`. All 40 trace captures through 7,000 fields
match the uninstrumented replay. CACR remains zero.

| Validation | Result |
| --- | --- |
| Production Release build | Pass; zero warnings/errors |
| CPU tests using the exact packaged DLL | 3,475 pass; six optional external suites unavailable |
| Engine diagnostic tests, separate outputs | 802 pass |
| Host tests | 112 pass; three optional native tests unavailable |
| Original 040 Lotus replay, normal/scalar, 18,000 fields | All 95 captures byte-identical; no unsupported stop |
| Visual field 10,000 | Single-player driving **DEMO**, 146 km/h |
| Visual field 15,000 | Car selection |
| 030 replay versus preceding `.55`, 18,000 fields | All 95 captures unchanged |
| 68000 replay versus `.55` and retained 2026-09-26 baseline | All 95 captures unchanged against each |
| 040 native HDF + 2 MiB Fast RAM, normal/scalar, 2,400 fields | Allocation/free-list checks and OFS file persistence pass; all 21 captures match |
| 040 native RDB HDF without Fast RAM, 2,400 fields | Cold boot and OFS file persistence pass |

The corrected 040 replay ends at motherboard cycle `2557702545`, CPU fingerprint
`23F9E514495C578D`, hardware `D3FAC3ED960A6990`, output `4F7F9395A4ED32CE`.
This is a new development result, not a replacement hardware golden.
Player-controlled racing and full game completion remain unverified. Printed
runner FPS is diagnostic only; no throughput acceptance measurement was made.

## Reproduction and package boundary

The [manifest](LOTUS_III_040_FETCH_2026-09-27.json) records package/source identities,
ROM/media/input hashes, commands, test results, trace events and every capture
hash. Raw evidence stays under `artifacts/lotus3-040-fetch/`. Package SHA-256 is
`8e9a48969a9b4c80e2e4e0dc540c2520ffabf8a48fca741b1cffef12f88e3207`.
The runner, desktop, diagnostic tests, exact-package CPU tests and trace probe
use the same packaged DLL. All ten consumer/probe locks resolve `.56`; XML
member names remain unchanged, and package README/icon/symbols are present.

A clean checkout needs the exact local `.56` package in `artifacts/copper68k-feed`
until a release is explicitly authorized. The existing `.54` publication approval
does not authorize `.56`. Neither `.55` nor `.56` has been published here.
No ROM, media, generated HDF, credential or build artifact is committed.

Supply matching media, place disk two at `media/lotus3-disk2.adf` as referenced by
the portable original script (or adjust only that path in a local copy), and use
a fresh output directory:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --cpu 68040 --rom "path/to/Kickstart_13.rom" --disk "path/to/lotus3-disk1.adf" --frames 18000 --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3.json --boot-probe artifacts/lotus3-040-fetch-new --boot-probe-interval 1000 --boot-probe-extended
```

Add `--scalar-cpu` and select another new output directory for the comparison.

## Authorized mainline release follow-up

The owner subsequently authorized publication and requested mainline integration.
The validated CPU source was merged into CopperMod `main` and released as
`1.5.0`, replacing the historical `ocs020` development suffix. CopperScreen now
pins that public version without a local feed. The `.56` identities and results
above remain the original candidate evidence; `.56` itself was not published.
See [public-package and mainline verification](COPPER68K_MAINLINE_1_5_0.md).
