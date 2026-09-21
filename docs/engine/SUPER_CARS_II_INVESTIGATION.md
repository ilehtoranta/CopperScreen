# Super Cars II — chip RAM mirror correction, 2026-09-20

The Flashtro disk's early Guru is caused by missing A500 CPU chip-RAM mirrors.
This is a Lightweight memory-map defect. The user reports that Legacy runs the
game with the same 512 KiB chip + 512 KiB expansion configuration. The CPU core
correctly raises a line-F exception when the old memory map supplies `$FFFF`.

## Reproduction and cause

Reference: committed `19c525b`, including the North & South Copper fix. Native
PAL OCS/68000, Kickstart 1.3, 512 KiB chip + 512 KiB slow RAM, one read-only DF0,
no HDF. ROM and disk identities are in the existing
[media manifest](GAME_CORPUS_2026-09-20_MEDIA.json) and
[corpus record](GAME_CORPUS_2026-09-20.md). The supplied disk is
`C:/Data/TestImages/OK/Super Cars II (1991)(Gremlin)(Disk 1 of 2)[cr Flashtro].zip`.

An external single-instruction probe starts after 360 ordinary fields and retains
only a bounded history. At field 381 the depacker copies code through `$080002`
and returns there. Lightweight had discarded those writes and returns `$FFFF`
on the instruction fetch. Vector 11 targets `$FC082A`; the exception frame saves
SR `$2008` and PC `$080002`. This produces the previously recorded
`Guru #0000000B.00C01570`. No media modification or title-specific engine rule
is needed.

## Hardware basis and implementation

Primary source: [Commodore A500 Service Manual, schematic 312511 sheet 2, printed
page 39](https://amiga.net.au/files/Tech_Amiga/Commodore_Amiga_500_Technical_Manual.pdf).
The default JP2 connection routes CPU **A23** to Agnus A19, selecting the expansion
bank at `$C00000`; the alternative A19 connection selects `$080000`. Agnus receives
the lower address lines and uses the same onboard RAM when A19/A20 change within
the low chip-memory decode. The service-manual memory map, printed page 2, marks
the unused part of the low 2 MiB region as reserved; it does **not** explicitly
label those addresses as mirrors. The alias conclusion follows from the default
wiring and address decode, not from that reserved label alone.

Supporting implementation evidence is Legacy's `ChipRamCpuDecodeSize = $200000`
and masked CPU chip accesses, inspected at CopperMod commit
`ada86020ad7a9b298cd7f689c3731d8d779684d1`. Legacy was inspected read-only;
no runtime/project reference was added and no fresh Legacy native replay is claimed.
The downloaded manual SHA-256 is
`532CDDCEC7550ADFCFC15E791F5A0AB6EB18DDB302EDFE1240141C81DB1379E1`.

The candidate maps CPU byte/word/long accesses and instruction fetches below
`$200000` to the fitted 512 KiB with mask `$07FFFF`. Alias accesses use the same
Agnus arbitration as the base chip window. Scalar and conservative-batch
instruction peeks agree. `$200000` remains outside chip RAM; this adds no RAM,
ECS slow-RAM DMA alias, new scheduler, callback or per-cycle allocation.
Existing reset-overlay behavior and other unmapped address regions are outside
this correction's scope.

Ten focused tests fail before and pass after the correction: all three mirrors,
24-bit CPU address wrapping, byte/word/long visibility, physical-bank crossing,
the decode boundary, refresh contention and scalar/batched execution from a
mirror. The full isolated engine suite passes 649 cases; CopperDisk passes 74.
The production Release build has zero warnings and errors.
After recording and validating the corrected native identity below, all 95 host
tests pass with the supplied native media enabled, including all three optional
native checks (818 passing cases across engine, disk and host suites; zero skips).
The earlier two native failures remain in `host-tests.log`; the passing run is
`host-tests-corrected-identity.log`.

## Native validation and changed boot identity

The same unmodified Super Cars II disk now passes the depacker, displays the
title, loads the manual-check screen and enters Easy race 1, Bagley Marsh.
The first 19,000-field scripted replay reaches the track with animated traffic
and real, nonzero audio buffers; its up-direction hold did not accelerate the
player. A subsequent acceleration/steering replay uses the retained
[input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-supercars2.json).
That replay confirms player acceleration and steering: the red car changes
position/orientation, the course scrolls and the lap counter decreases from five
to four by field 18,600. The final field 19,000 has cycle `2699806078`, PC
`$075EC4`, nonzero PCM and no unsupported feature. Capture directories
`gameplay-fixed/` and `driving-fixed/` preserve the two distinct input experiments;
the retained script reproduces `driving-fixed/`.
Final BMP SHA-256:
`091D8461C89A76B378284BCDD3F67A5FED1B92C276185A2BCF766A66EBE54439`.
Disk 2 and race completion remain unverified; no disk swap is claimed.

Replay from the repository root with the candidate Release runner:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/OK/Super Cars II (1991)(Gremlin)(Disk 1 of 2)[cr Flashtro].zip' `
  --frames 19000 --wide-output `
  --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-supercars2.json `
  --boot-probe artifacts/supercars2-replay
```

The retained external replay probe captures every 600 fields rather than the
runner's 60-field interval; both apply the same `NativeInputScript` to ordinary
`ExecuteFrame` calls. Captures use peeks, not guest bus reads. Neither is a
performance measurement.

The correction also changes Kickstart's RAM-size probe, before media loading.
A diagnostic source copy logs one access into the mirror during the first 120
fields. A separate instruction probe compares unchanged production binaries:

| Probe step | Reference | Candidate |
| --- | --- | --- |
| `$FC05A6`, write at `$080000` | Cycle 5,255,212; write discarded | Same cycle; updates physical address zero |
| `$FC05AA`, branch on base-memory test | Zero: continues to compare the unmapped read | Nonzero: detects the alias immediately |
| `$FC05B0`, RAM-probe exit | Cycle 5,255,266 | Cycle 5,255,246 |
| Detected chip-memory endpoint | `$080000` | `$080000` |

This explains the 20-cycle change without changing RAM capacity. The initial
host run preserves its failures: 93 tests pass, two native replay tests reject
the old final-cycle expectation. Those failures are evidence of a changed golden
identity, not passing tests. Updated host expectations retain this hardware
reason. A separate replay of the unchanged input script visually reaches
Lemmings level one, with the same final HUD counts/time as the retained prior
capture (10 out, 00% in, 3:31 remaining). This replay does not assign a digger;
the older assertion comment overstated that script's actions and is corrected.

Lemmings preflight: 10,920 warmup + 3,600 measured fields, real PCM, zero measured
allocations, no unsupported feature. The initial preflight used `--wide-output`
(same 908-pixel native framebuffer, reported as `image-wide`). This untimed-for-
acceptance diagnostic changes the identities as follows:

| Identity | Reference | Candidate |
| --- | --- | --- |
| Final cycle | 2063189682 | 2063189662 |
| CPU | BCF441F9BABF1113 | 249E90223C45891F |
| Hardware | 206DA179786FCB2A | F4951BBFA89CA99A |
| Pixel/audio | F7328C7C24582FCB | 45AB46F7600E1C9F |

Frozen candidate engine SHA-256:
`AE033B1E1F787E37ACFDFA355DFCE97EFF6F7C0433CB7CB7ED915F136DA03FC6`.
Evidence is under ignored `artifacts/supercars2-investigation/`: fault trace and
RAM snapshots, before/after boot probes, bounded native captures, diagnostic
source copy, build/test logs and unchanged production candidate. ROM, media and
captures are not committed.

## Performance status

The user requested benchmarking after correctness validation. The separately
versioned [chip-mirror comparison v1](../../scripts/run-lightweight-chip-mirror-comparison-v1.ps1)
compares the combined Copper-restart/memory-map candidate with performance-accepted
`1db2d0d`, so the unmeasured Copper change is included. Both directories use the
same frozen runner and dependencies, changing only the engine assembly. A fresh
preflight with the exact frozen runner and benchmark arguments reproduces the
candidate identities above with zero measured allocations (`benchmark-native-preflight.log`).

This uses the earlier ERSY comparison's explicit changed-state native method:
synthetic fingerprints must match across builds; every native result must match
its independently validated, role-specific full identity. Negative probes reject
the opposite build's identity, allocation, missing work and corrupted hashes.
The native script, frame budgets, balanced six-pair order, Normal priority,
topology checks, host-load policy v2 and one-sided 95%/1% criterion are unchanged.
Neither native identity is learned from timed samples. The candidate engine is
pinned in addition to the reference and shared dependency hashes.

The complete `retention-1` series is **VALID**, with 36 retained samples, 1,572
telemetry intervals, verified CPU 2 / SMT sibling 3 on the Ryzen 5 5600X, and Normal
priority throughout. No concurrent builds/tests ran. Every sample has zero
measured allocations, real PCM and its required complete-workload identity.
Post-run hashes confirm unchanged frozen binaries, engine/test sources, protocol
and host-load helper. Raw `samples.json`, `summary.json`, `telemetry.jsonl`,
`protocol.log` and sample logs remain under the local evidence root.

| Workload | Reference mean FPS | Candidate mean FPS | Reference mean ms/field | Candidate mean ms/field | Paired frame-time change | One-sided 95% upper bound | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Lores + Paula DMA | 396.15 | 391.92 | 2.52454 | 2.55179 | +1.0785% | +2.2310% | ACCEPTANCE REQUIRED |
| Hires + Paula DMA | 356.90 | 357.75 | 2.80211 | 2.79527 | -0.2418% | +0.2671% | PASS |
| Native Lemmings | 265.63 | 265.33 | 3.76467 | 3.76899 | +0.1154% | +0.6240% | PASS |

Displayed FPS/ms means are arithmetic; paired changes and bounds use the frozen
log-ratio/Student-t calculation across six pairs. The native result is explicitly
a changed-state comparison, justified above; it is not byte-identical reference
and candidate output. The old v5 protocol and its invalid North & South series
are unchanged and are not reclassified by this result.

The numerical 1% gate is **not passed**: lores exceeds both the mean and upper-bound
limit. On 2026-09-21 the user explicitly accepted this scoped lores exception and
authorized commit/push of the frozen combined candidate. Hires/native pass
independently. The valid measured values and ACCEPTANCE REQUIRED numerical result
are preserved; owner acceptance does not relabel them as a numerical PASS.
The default one-sided 95% upper-bound limit remains 1% for subsequent changes.
No changes were committed or pushed during benchmarking.
