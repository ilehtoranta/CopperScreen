# Tower Assault: OCS chip identification

2026-09-23. **Candidate correction; a valid complete comparison exceeds the
default performance gate. No performance exception granted.**
The owner subsequently requested the [DMA latch optimization](DMA_LATCH_OPTIMIZATION_2026-09-23.md).
Its new verification is separate from the original candidate evidence below.
The subsequent [scoped-local optimization](SCOPED_LOCALS_2026-09-24.md)
passes the unchanged 1% upper-bound limit on every retained workload while
preserving all 1005 native captures. Its complete-candidate result supersedes
the pending performance decision for current source, not the original evidence
and measured throughput below.
The original [five-game corpus](NATIVE_CORPUS_2026-09-23.md) and its failures
remain unchanged evidence. Scope is PAL OCS / 68000 / Kickstart 1.3 / 512 KiB
chip plus 512 KiB slow RAM, using supplied IPF release 279.

## Failure and cause

The original replay shows Guru `0000FFFF.2E410026` by captured field 600.
A bounded external instruction probe against the frozen production assemblies
traces the first failure to the loader's chipset detection, before gameplay:

1. At PC `$0059F4`, the loader reads `$DFF07C` and tests bit 2. Lightweight's
   generic register storage returns zero for this absent OCS register.
2. This selects the AGA flag at `$787D`, the larger allocation path and an
   additional `$80000` stack offset. The resulting user stack is near `$CFFB00`,
   outside the fitted `$C00000–C7FFFF` expansion RAM.
3. In the relocated loader, `RTS` at `$C00682` reads an unmapped return address
   (`$FFFFFFFF`). Address-exception entry reaches ROM `$FC081A`, then Exec Alert
   `$FC3012` at field 220. A later reset produces the visible Guru.

An ignored diagnostic counterfactual sets only the stored `$07C` word to
`$FFFF`, without changing RAM, disk data or CPU execution. It passes the former
failure, keeps the stack near `$C7FAxx` and reaches the disk-2 prompt. This
isolates the cause; a constant register value is not the production correction.
There is no evidence here for adding RAM mirrors or changing Copper68k.

## Hardware evidence and model boundary

The [Commodore Hardware Reference Manual, third edition, Appendix C p.299](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_3rd_Edition.pdf)
states that original 8362 Denise lacks DENISEID; reading that address observes
bus residue. The [Commodore Denise specification, register description](https://www.amigawiki.org/lib/exe/fetch.php?media=de%3Aparts%3Adenise_specs.pdf)
also distinguishes absent early-revision readback from ECS's implemented ID.
Neither document specifies a complete board-level decay/CPU/DMA phase truth table.

The bounded digital policy uses the preceding CCK's completed DMA word when
present and `$FFFF` otherwise. Supporting implementation research is
[WinUAE custom.cpp at 55dcc024](https://github.com/tonioni/WinUAE/blob/55dcc024c093e76ec9b03131198c57584886dc38/custom.cpp),
whose OCS/ECS unreadable-register path makes this distinction and explicitly
retains uncertainty about coincident cycles. Legacy has a similar policy; neither
emulator is an independent hardware oracle. The vAmigaTS Denise-ID probe was
inspected, but its available OCS reference image was not treated as a physical
OCS measurement. RANDY of COMAX's AGA notes are third-party programming research,
not a Commodore specification.

Consequently, the **absent-register defect and native correction are established**;
electrical decay, refresh/dummy cycles, exact coincident edges and full unreadable
register side effects remain unverified. No blanket open-bus or OCS-completeness
claim is made. Other write-only register readback is outside this change.

## Implementation and verification

The first candidate keeps one DMA word and its canonical output cycle. Copper, bitplanes,
sprites, audio, disk and blitter update it only when completing actual memory
transfers, including accepted operations that survive cancellation. `$DFF07C`
uses this sample only in the immediately following CCK; otherwise it returns the
idle value. Word/byte/long CPU accesses retain existing arbitration and completion
timing. Reset discards the sample. Untimed memory helpers and debugger inspection
do not drive the bus. No CPU instruction bookkeeping, per-cycle polling, new
scheduler, allocations or title detection is added.

Sixteen focused regressions cover absent storage, byte lanes, all four audio
channels, stale samples, canceled/accepted DMA, disk DMA writes, the second word
of a long read, debugger/setup access and both reset paths. The first three
storage cases fail against the original code. They verify this bounded model;
they do not manufacture physical timing evidence.

- Production Release build: **0 warnings, 0 errors**.
- Isolated diagnostic Release engine suite: **685 passed, 0 skipped**.
- CopperDisk: **74 passed, 0 skipped**.
- Host: **95 passed, 0 skipped**, with native ROM/media/input supplied.

## Native follow-up

The frozen candidate passes the former Guru, requests disk 2, loads its title,
high scores and options, then reaches the opening level with active audio.
The bounded [input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-tower-assault.json)
ejects at field 3200, inserts the exact disk-2 entry at 3300, supplies fire at
6500/8500, Escape at 12100, Return at 13000, then movement/fire through 17020.
The replay ends at field 20000. Extract disk 2 to ignored
`media/tower-assault-disk2.ipf`; source ZIPs are never modified.

Scalar and batched runs have **335 matching states, 335 matching chip-RAM dumps
and 335 matching full field images** (1005 files). Audio is nonzero at 206
checkpoints; no unsupported-feature stop occurs. A control replay omitting inputs
from field 14000 remains at the mission briefing, while the scripted fire press
advances into the level; all captures preceding that input match.
Another control retains that fire press but omits movement/fire from field 16000.
Its captures match through field 15960; the movement replay diverges at field
16020 and visibly scrolls the level by 16140 while the idle control stays at the
landing site. This establishes bounded control response, not level completion.
The [native result record](TOWER_ASSAULT_NATIVE_2026-09-23.json) retains snapshot
and binary identities, input/log hashes and final states.

Disk-2 SHA256 is
`256B2FBDF5242342CFB0959F5AD3B8FB837ADB418D23316B2AE7B085C5158827`.
The [original media manifest](NATIVE_CORPUS_2026-09-23_MEDIA.json) retains the
ROM, archive and all entry identities. Disk 3, level completion, save/load and
whole-game compatibility remain unverified. Capture runs allocate for evidence
and their FPS is not performance acceptance evidence.

Alien Breed II repeats the former black-screen stop at PC `$000FF334` through
8000 fields on this candidate. All 405 state/RAM/image files still match its
original corpus replay. The subsequent [Alien Breed II investigation](ALIEN_BREED_II_INVESTIGATION.md)
identifies the supplied SPS 44 set as the AGA edition by all three exact disk
identities. It is outside the supported profile; OCS compatibility remains
unverified with the correct edition unavailable locally.

## Frozen comparison

Only the engine differs in the frozen reference/candidate runners:

| Assembly | SHA256 |
|---|---|
| Reference engine, pre-change September 23 corpus | `1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683` |
| Candidate engine | `2076345FF2476EE86A1F77CB9B553641D4ECFC1363705051BD75F7F9A426EFA3` |
| Shared Copper68k locality.1 | `982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F` |
| Shared runner | `9AB9627DA0BC4207F1AB0D37621241C9012C77C22DDBCF4B3106C0B59E3240C0` |
| Shared CopperDisk | `5D2625E5EB504F8FF60921CFA3616ACCC9D0CDE0CFC77A554A24EAD2B718956A` |
| Shared CopperFloat | `4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235` |

Engine source in the reference is unchanged at accepted HEAD `cb946eb`.
The production build has current build metadata in its other assemblies; native
and comparison freezes reuse the original corpus's common assemblies to isolate
the engine change. Both independent complete Lemmings identity checks match the
retained expected CPU/hardware/output/cycle/PCM result with zero measured allocation.
These unpinned concurrent checks establish identity only, not speed.

[OCS identification comparison v1](../../scripts/run-lightweight-ocs-identification-comparison-v1.ps1)
versions only the frozen binary bindings and experiment name from the preceding
CPU comparison. The measurement body, six balanced pairs, workload lengths,
Normal priority, verified homogeneous topology/SMT protection, host-load policy
v2 and Student-t upper-bound calculation remain unchanged. No expected gameplay
identity is learned from timing samples. Each workload must have matching complete
fingerprints, zero measured allocations and a one-sided 95% upper frame-time
regression bound ≤1%; earlier exceptions do not transfer.

Preflight passed on Ryzen 5 5600X, logical CPU 2 with sibling 3 protected, Normal
priority, including bad-fingerprint and missing/noisy-telemetry rejection checks.
Protocol SHA256:
`7675D317DC4EB4B42ABF708220EF4ACE8F0CDDF52BE2DF3CC92295390E495AEF`.

Attempt 1 is **INVALID / RERUN**. Lores R1/C1 completed with identical full
workload fingerprints and zero measured allocation. During C2, protected-sibling
activity stayed at or above 25% for 10.1340784 seconds, invalidating the entire
series. Only two samples were retained (R1 380.61 FPS, C1 370.44 FPS); these are
raw diagnostic values, not an accepted regression estimate. Hires/native timing
did not run. No partial sample is pooled into a later attempt, and no 95% bound
or ≤1% claim can be made. Builds, tests and native captures had finished before
measurement began.

The owner initially chose **leave performance verification pending**, then
requested renewed measurement with the Alien Breed II investigation. That earlier
pause is superseded; the invalid attempt remains unchanged evidence. Original
attempt hashes are:

| Evidence | SHA256 |
|---|---|
| `performance-attempt-1/samples.json` | `103AFD17E48910AB0E9B092312C6B17F748B4CDE881960AA8A8D78DB5FE63EE3` |
| `performance-attempt-1/protocol.log` | `41EAA284B6E79D7BD2979AF344206AEDD119BD471B48FDD799182E31351A566A` |
| `performance-attempt-1/INVALID.txt` | `959D4958E27583A0DDD16FCBB167DB8691FD811B2D87FB897A17BDFA2AFE597A` |

Attempts 2 and 3 also remain **INVALID / RERUN** because competing .NET build/test
work appeared. Attempt 2 stopped during lores R1 with no completed samples.
Attempt 3 completed twelve lores samples and hires R1 before stopping during
hires C1; the competing process was a Copper68k Release test run. None of these
partial samples enters the final estimate. Idle build-server presence alone was
not treated as interference, and no other user's processes were stopped.

After the owner confirmed those jobs paused, attempt 4 completed **VALID**:
36 samples, all complete fingerprints matching, zero measured allocations and
valid telemetry under the unchanged rules. Every valid sample is retained.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Gate |
|---|---:|---:|---:|---:|---|
| Lores | 388.85 | 378.37 | +2.7952% | +5.0003% | Acceptance required |
| Hires | 341.48 | 351.20 | -2.8678% | +1.3831% | Inconclusive; exceeds 1% bound |
| Native Lemmings | 329.62 | 326.93 | +0.8247% | +2.1427% | Inconclusive; exceeds 1% bound |

All three upper bounds exceed the user's default limit. The candidate remains
uncommitted and unaccepted for performance; a valid measurement does not imply
acceptance. Alien Breed II required no engine change, so this comparison isolates
the Tower Assault correction described above.

The [machine-readable performance record](TOWER_ASSAULT_PERFORMANCE_2026-09-23.json)
retains all samples, complete fingerprints, independently recomputed paired
statistics, binary/protocol hashes, local raw-evidence hashes and invalid-attempt
history. There are 1,453 valid telemetry intervals; the longest above-limit
sibling/package streaks are 1.137/1.133 seconds, below the 10-second rejection
threshold. No competing build/test workload appears in the valid series.

## Separate Tower Assault gameplay throughput

[Tower throughput v1](../../scripts/run-lightweight-tower-throughput-v1.ps1)
runs the frozen candidate six times with the same placement, priority, telemetry
policy and input script. Each run warms up for 20,000 fields (including boot,
disk swap and opening-level entry), then measures 3,600 further fields with wide
output and real PCM. The full identity was independently established before
timing, not learned from measured samples. Protocol SHA256:
`E0D3AAB4DA0B4632326C5125EC85B23B45582E8C19AAE74F74C0DF13F5F043B4`.

The six FPS values are **291.41, 290.98, 289.87, 290.84, 292.28 and 295.24**:
arithmetic mean **291.77 FPS**, range **289.87–295.24 FPS**. All complete
fingerprints match and measured steady-state allocations are zero. All 507 host
telemetry intervals are valid, with no above-limit interval or competing job.
The result is **VALID throughput only**: the reference reaches a Guru before
gameplay and cannot supply a comparable baseline. This headless active-level
throughput does not establish a speedup, desktop presentation FPS, whole-game
compatibility or acceptance of the separate regression bounds.

Local raw evidence lives under ignored `artifacts/tower-assault-investigation/`.
Instruction/ROM/RAM dumps, media and generated binaries are not committed. No
package publication is included.
