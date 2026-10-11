# Copper68k qualification candidate

`1.5.4-qualified-dev.1` is an unpublished candidate built from CopperMod mainline
`5b849dbff704d5a339b64a9ceb46c39b66c5b74e`, which includes the approved synthetic
suite from PR #21. It passes the approved known-coverage gates; publication is
not authorized. Results and identities are retained in the
[machine-readable record](COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.json).

The mainline general logical handler already passes all ten Detroit EORI
postincrement semantic cases. It retains its read-modify-write barrier and
established operand ordering. The older dedicated Detroit execution/timing path
is therefore not imported. Explicit sparse-bus regressions now check sized writes,
one write per destination byte, untouched memory/registers, flags, exact PC and
the byte A7 stride. No production CPU execution code is changed by this candidate.

The clean consumer archive is CopperScreen `b64fc62f86039d357a055dff61d1fd1f7832444f`.
It uses the exact package through NuGet, with no sibling source reference. The
CPU DLL SHA-256 is `f445a7d289cf6545c46731a0d94e59e7c8c56b189a0c43768f4009874c0cb6f2`;
its bytes match the CPU test assembly dependency, package payload, native runner,
host tests and separately built engine diagnostics.

## Completed validation

- Focused CPU/logical tests: 230 passed; full CPU suite: 5,393 passed, zero failed,
  109 optional/deferred tests unavailable.
- Required architectural report gate: all 86,663,530 cases in 783 batches pass,
  with no required mismatches, unsupported execution or untested combinations.
- AHX: 18 passed.
- Clean archive and default-pin Release builds: zero warnings/errors. Locked
  production restore passes. Host 195 passed with six optional skips, disk 74
  passed, separately built engine diagnostics 1,183 passed with seven skips.
- Four explicit Kickstart 1.3 CPU cases pass on 68010 batch/scalar and 68040
  interpreter/JIT, including positive compiled-instruction evidence. This supplies
  coverage for the native CPU group skipped in the ordinary run; other optional
  fixtures remain unavailable. Skips are not counted as passes.

The frozen local package is required until explicitly published. Rebuilding
package content requires a new development version and regenerated locks; do
not overwrite this candidate. Its original package metadata records the stage
before full qualification; this record supplies the completed results.

The native replay retains the ROM/disk/input identities in the
[earlier Detroit record](DETROIT_AGA_FIX_2026-10-08.md), stock PAL A1200 profile,
two read-only disks and 34,000 fields in normal and scalar modes. New results
belong to this candidate, not the earlier package. The analyzer requires the
explicit `--cpu-version 1.5.4-qualified-dev.1` and completed, matching captures;
its historical `.dev.2` expectation remains the default.

Both replays complete all 34,000 fields with no unsupported stop. All 1,027
capture files per mode match, as do final CPU, hardware and output fingerprints.
Inspected screens show Company 1's January management, the January profit/loss
report with Canadian sales of £1,250 and a £4,939 loss, and the return to February
management with a £55,061 balance. The analyzer rejects the default historical
package expectation for these captures and rejects a request for 34,200 fields.
Inspection previews crop/resample only for readability; the full stored rasters
and emulated display window are unchanged. Saves and later gameplay remain
unverified. Capture-inclusive throughput is not a performance result.

The five unclear qualification areas remain explicitly deferred under
CopperMod's `docs/COPPER68K_QUALIFICATION_SCOPE.json`. Native gameplay is a
compatibility result; synthetic integer coverage is not physical timing, FPU,
enabled-MMU or general OS qualification. Publication remains a separate,
explicitly authorized release step. No package, ROM, media or native capture
is committed.
