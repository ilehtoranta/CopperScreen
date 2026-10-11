# Detroit AGA startup correction and gameplay

The corrected local build reaches Detroit's management game on a stock PAL
A1200. A new Medium game with one human player reaches Company 1 in Canada,
accepts the default company/car names through native keyboard input, and
processes January 1908 into February. The January report shows Canadian sales,
a £4,939 loss and an updated £55,061 balance. Returning from that report restores
the interactive management screen.

The initial Copper68k 1.5.3 failure remains in
[the original test](DETROIT_AGA_2026-10-08.md). This follow-up changes the shared
CPU and pinned package; guest, ROM and disk bytes are unchanged. Both production
normal/scalar runs complete 34,000 fields with exit code zero and no unsupported
stop. All 1,027 capture files per mode match, including the final CPU, hardware
and output fingerprints. The inspected gameplay milestones also match the
exploratory run byte for byte. Identities and results are retained in
[the machine-readable record](DETROIT_AGA_FIX_2026-10-08.json).

## Correction and ownership

Copper68k's advanced timing interpreter omitted postincrement EORI. The generic
memory handler now recognizes byte, word and long `EORI #immediate,(An)+` on
EC020/020/030 profiles. It writes at the original address, increments once by
the operand size (two for byte A7), preserves X, derives N/Z from the result,
and clears V/C. These expectations follow the
[Motorola programmer's reference manual](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf),
section 2.2.4 and the EORI instruction entry.

Three timing keys are appended without renumbering existing public keys. They
use the existing immediate-logical postincrement formula and retain its
read-modify-write barrier. Indirect, displacement and absolute-long EORI routes
retain their behavior and neighboring tests. This extends the current approximate
operand-shape timing policy; it does not certify physical CPU/accelerator timing.

The correction is in CopperMod's `Copper68k/M68kAdvancedTimingInterpreter.cs`,
`M68kTimingEngine.cs` and `M68kTimingFormula.cs`, with new
`Copper68k.Tests/M68020EoriPostIncrementTests.cs`. CopperScreen adds a motherboard
bus regression in `LightweightCpuIntegrationTests.cs` and consumes the package
through NuGet. No sibling source/project reference, production trace logging or
title-specific engine behavior is introduced.

The final local package is **`1.5.4-detroit-dev.2`**, in ignored
`artifacts/detroit-fix-2026-10-08/feed`. It is unpublished. The preceding `.dev.1`
package is preserved. `.dev.2` changes version/release metadata only; execution
and timing source hashes match the fully tested `.dev.1` correction. Existing
published versions are unchanged and immutable.

## Validation

- All 10 new semantic cases fail on the original implementation.
- The correction passes all 13 new semantic/barrier checks and 158 focused
  new/neighboring operand checks.
- Full CPU suite: 4,446 passed, seven optional conformance checks unavailable.
- Amiga CPU integration group: 18 passed, including both new normal/scalar
  Chip RAM postincrement checks. Full engine suite: 1,183 passed, seven optional
  native checks unavailable.
- Desktop host suite: 195 passed, six optional native checks unavailable.
  Production Release build: zero warnings/errors.
- All 60 native intro files through field 1,800 match the original 1.5.3 replay,
  excluding deliberately changed CPU profile metadata.

The CPU/engine/host suites exercised `.dev.1`'s execution source. Source hashes
confirm `.dev.2` retains that implementation; final gameplay replays consume
`.dev.2` itself. Skips are unavailable coverage, not successful native checks.

## Native replay and limits

PAL, 68EC020 interpreter, Alice 8374/Lisa 4203, 2 MiB Chip, no Slow/Fast RAM,
A1200 Kickstart 3.0 revision 39.106 and two read-only DD drives. The supplied
DLM disk-one ZIP is in DF0 and the supplied second disk is in DF1.

To play in the desktop, choose the stock Amiga 1200 profile with that ROM and
both disks. Leave the DLM intro with left mouse, open Disk 1 in Workbench and
double-click the Detroit car icon. Start a game, select difficulty/player setup
and a territory, then use Return to accept the company and car-model names.

[The retained input](../../scripts/probes/Detroit/gameplay-input.json) leaves
the intro, opens disk one in Workbench, launches Detroit, selects Medium/one
human/Canada, accepts company and car names, confirms month end and returns
from the report. It retains early clicks made before icons were ready and an
ineffective Return on the financial report; successful later launch/back-button
clicks are explicit. There are no guest memory edits or unsupported continuation.
Input timings belong to this identified build/media.

Exploration used a copied host runner accepting live inputs at field boundaries
and recording their frames, with the unmodified engine assembly. Final verification
uses the production runner and static input. [Instructions and analyzer](../../scripts/probes/Detroit/README.md)
require completed native captures and summaries, reject unsupported execution,
compare files/fingerprints and tie inspected exploratory milestones to production
by byte identity. Nonblack output alone does not classify gameplay.
As a negative control, the analyzer rejects the completed 8,000-field Workbench
run when supplied as evidence for the required 34,000-field gameplay replay.

All full-width raster, state, RAM, PCM, disassembly, logs and package artifacts
remain ignored under `artifacts/detroit-fix-2026-10-08/`. Inspection previews
crop/resample for readability; engine display windows and full stored rasters
are unchanged.

Coverage establishes entry and interaction through the first month. Saves,
later decades, every menu and full-game completion remain unverified. No hardware
conformance or performance acceptance is claimed.
