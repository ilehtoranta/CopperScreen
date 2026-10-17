# Nitro: race-start wait repaired

2026-10-17, **LWA-NATIVE-011**. Nitro now passes the former race-start wait with
both native Kickstart 1.3 and the fresh source-built CopperStart ROM from the
[earlier boot investigation](COPPERSTART_NITRO_2026-10-17.md). The track, starting
lights and cars render, and the scripted driving input moves the player's car
and scrolls the city. This is bounded first-race coverage, not a completed race
or game.

The [machine-readable record](NITRO_RACE_START_2026-10-17.json) identifies the
package, source, ROM/media/input hashes, tests, reviewed captures and comparisons.
Local evidence is under `artifacts/nitro-race-2026-10-17/`. No ROM, game media,
generated capture or package is committed.

## Two independent timing defects

Nitro's routine at `$184A0` clears VERTB, then tests INTREQ bit 5 at `$184A8`
and branches back at `$184B0`. Its level-3 handler also clears VERTB. The old
execution repeatedly entered the handler before the main poll could see the
request. It remained at `$184B0`, SR `$2004`, through 14,000 fields on both ROMs.

First, Lightweight called the CPU's interrupt sampler only when the live request
was high and unmasked. The CPU therefore retained an earlier sampled level
across the inactive interval. A new assertion could be accepted before its own
setup window. Sampling now continues for low and masked levels; the acceptance
mask still applies before exception entry. Two original guest-program tests
assert the same software request twice, separated by a low interval with IPL
either unmasked or masked. Previously the first stacked PC was `$110A` and the
second `$1108`; both now retain `$110A`. This uses the existing CPU sampling
contract. The [MC68000 manual, section 6.3.2](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf)
distinguishes pending inputs, instruction boundaries and priority masking.

That adapter correction alone still reproduces the game hang. The second defect
belongs to Copper68k: a memory BTST whose operand read waited beyond the nominal
instruction duration could retire with its final instruction fetch deferred.
That fetch is required by the instruction's bus sequence and also determines
the IPL sample. In the reduced CIA-read case, the preceding core finishes at
cycle 40 with only the next opcode queued; the independent CPU also fetches the
following word and finishes at cycle 46 on the same bus.

[CopperMod PR #24](https://github.com/ilehtoranta/CopperMod/pull/24) commits that
required final fetch for static and dynamic memory BTST, in scalar, kind-table
and packed execution. Register-direct forms and 020+ paths retain their existing
behavior. Twelve delayed-read cases fail before the correction; all **24**
delayed/ordinary cases pass afterward. They verify fetch order/address, duration,
IR/IRC state, the IPL sample, flags before the final fetch and absence of writes. No instruction, PC,
register or media patch is applied to Nitro.

## Independent comparison and scope

The diagnostic comparison uses Moira revision
`ce28239b0507ebfe1bf2ad2b9c9252dc2f2cc031`, with precise timing enabled and
Musashi compatibility disabled. After 9,998 unchanged CopperStart fields, it
substitutes only the CPU while retaining the same live Lightweight chipset,
RAM, D0–D7/A0–A7, PC/SR, CIA inputs and physical bus arbitration. Its
prefetch words are primed from unchanged RAM at the current PC; its internal
IPL history starts independently. It escapes the wait within two fields and
returns to `$1834C`. The final qualification
runs use the corrected Copper68k package throughout; Moira is not a production
dependency or a guest-service shim.

The relevant independent implementations are Moira's
[BTST bus sequence](https://github.com/dirkwhoffmann/Moira/blob/ce28239b0507ebfe1bf2ad2b9c9252dc2f2cc031/Moira/MoiraExec_cpp.h)
and WinUAE's
[CE000 instruction generator](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/gencpu.cpp).
These support the narrowed micro-operation expectation; agreement between
emulators does not establish physical timing certification. No WinUAE execution
was performed: its earlier launch was rejected by automatic approval review
as "blocked by policy", and was not retried through another launch method.

## Qualified inputs and results

The development package is **Copper68k 1.5.4-nitro-dev.2**, built from source
commit `40207be34ac08dda76dd6f4ecfa915f3f8652332`, retaining the preceding qualified
source plus the BTST fix. Its SHA-256 is
`60f9fb3bbde26d73b5bd27980c2fdb775b156f1a83e8c9e84786add3523002ad`;
its CPU DLL SHA-256 is
`2ebd2cddde164249d968bd4f43502284d617a7074222ab6638233fda517fe2e7`.
The CPU test dependency, package payload, production app and captured runner use
identical CPU bytes. Existing published package versions are unchanged.
With explicit owner approval, the unchanged asset is distributed through its
[full GitHub release](https://github.com/ilehtoranta/CopperMod/releases/tag/copper68k-1.5.4-nitro-dev.2).
The downloaded package matches the frozen SHA-256; no NuGet.org publication or
package rebuild was performed.

CopperStart remains the same 524,288-byte source-built ROM, SHA-256
`5C5C0ADFBC4E12A59ECA2DD0236C40EFC633B71AB34190E88418F36BEA4FF4C9`.
The earlier record explains its frozen-component build and concurrent-source
exclusions. Native Kickstart 1.3 and the unmodified 166-track extended ADF retain
their earlier identities. The profile is PAL OCS A500, 68000 interpreter,
512 KiB Chip plus 512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0.

Both ROMs complete the unchanged 14,000-field
[Nitro script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-nitro.json).
For each ROM, all **355** batch/scalar image, state, RAM and PCM files match.
The reviewed field-10,000 captures show the starting grid; later captures show
the player's car progressing through the city. A neutral-input control retains
the same menu and selection events and omits driving after field 8,200. Its
captures agree through field 11,000 and then separate from the driving run.
Lotus 2 is also replayed through the first Forest Course race in both CPU modes;
its bounded results and exact comparisons are in the record.

The production Release build passes with zero warnings/errors. The engine
suite reports **1,247 passed, seven optional native skips**; the host suite
reports **195 passed, six optional native skips**. Both original clock probes
also pass with the new CPU package and exact CopperStart ROM.
[Source CI](https://github.com/ilehtoranta/CopperMod/actions/runs/38976324951)
passes **5,417 CPU tests, with 109 optional/deferred skips**, and all **18** AHX
shared-dependency tests. Its validated reports retain **86,663,530 logical cases
in 783 batches**. That full suite uses CI's Release build of the same source
commit; the focused local CPU tests and native/consumer runs exercise the frozen
package. The duplicate local full-suite run was cancelled after completed CI
coverage and remains incomplete in the record. Optional and deferred cases
remain unavailable coverage. The earlier producer suite's
stale CMD_WRITE expectation is retained in its original record and is not
reclassified by these CPU/adapter results.

Audio activity is present and mode comparisons include the captured PCM, but
listening/fidelity, later races, game completion and physical coincident-edge
timing remain unverified. Diagnostic runner rates are not formal throughput
acceptance measurements; no historical performance evidence is relabeled.
