# Final-row blitter modulo investigation, 2026-09-21

## Finding and correction

Inside the Machine v1.0.1 exposed an area-blitter pointer defect. At a row
boundary, `AdvanceWord` applied the enabled channels' signed, even modulos only
when another row remained. The final row therefore left each nonzero-modulo
pointer one modulo short. A later `BLTSIZE` without reloading the pointers
continued from the wrong word. The fix performs the same pointer updates for
every completed row, including the final row, in ascending and descending mode.
Disabled channels remain unchanged; line mode follows its separate path.

The [Commodore Hardware Reference Manual, BLTxPT register description](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_1989.pdf)
(printed page 264, PDF page 276) specifies the completed pointer as the last data address plus increment and
modulo. This is the hardware basis, independently of Legacy or the demo.
The [author's pinned source](https://github.com/grahambates/inside-the-machine-public/tree/822a4031cbbe94aed271d6a746d556d233574fb6)
provides the reproducer: `c2p-2x2.asm` splits a one-word-wide C2P pass at the
1024-row OCS limit, then its interrupt writes another `BLTSIZE` without reloading
pointers. The face pass has 1,800 rows, with A/B modulo 2. The wrong continuation
reads interleaved data. Descending passes have the analogous problem.

No title detection, media mutation, CPU change, scheduling layer, allocation or
additional clock phase is added. This corrects area pointer end state; it does
not certify every intermediate blitter pipeline phase or nonstandard line width.

## Regression evidence

Six new deterministic cases fail on the original engine and pass after the fix:
four all-channel end-pointer cases (ascending/descending and positive/negative
modulo), plus two 1,800-word strided copies split into 1,024 and 776 rows without
pointer reload. The original engine reads the `DEAD` sentinel on continuation.
Two old pointer assertions excluded the final modulo and have been corrected to
the manual's documented rule; their old expectations are retained in Git.

Release production build: zero warnings/errors. Focused blitter tests: **36 pass**.
Complete isolated engine diagnostics: **655 pass**, no skips. CopperDisk: **74 pass**.
The first native-enabled host run passed 93 and failed two output goldens: the
same generic correction changes Lemmings pixels. CPU/beam identities are retained.
The old output is preserved below and in the old protocols; this is an explicit
hardware-correction update, not a fingerprint learned from timing samples.
The final host run passes **95 tests**, including both native replays, with no
skips (`host-tests-final.log`). The two initial golden failures remain in
`host-tests.log`.

Local evidence is `artifacts/inside-machine-investigation/`: before/after test
logs, unchanged reference and candidate binaries, primary-source downloads,
full native captures, input scripts and measurement telemetry. Initial diagnostic
starts failed with Windows application-control error 0x800711C7; subsequent
normal builds/tests ran. No security policy was disabled. The logs remain; failed
starts are not counted as passing tests.

## Native evidence and limits

Use the [corpus media manifest](NATIVE_CORPUS_2026-09-21_MEDIA.json) and unchanged
bounded scripts from the [original corpus](NATIVE_CORPUS_2026-09-21.md).
Profile: PAL OCS, 68000, Kickstart 1.3, 512 KiB chip plus 512 KiB expansion.
Source archives and ROM were not modified. Demo replay is 18,000 fields without
input. Miami Chase repeats its 18,000-field disk-2/menu script.

Inside the Machine's face, rotozoomer, tunnel and later scenes now render coherent
images, and the run reaches the DESiRE closing logo. The face/roto comparison uses
[the author's effect images and explanation](https://blog.grahambates.com/posts/inside-the-machine/).
The earlier four-plane failure was not evidence of a HAM7-specific defect.
This is sampled native visual verification, not a frame-by-frame hardware trace.
Miami Chase's menu/high-score text clears between screens rather than accumulating.
This repairs the recorded visual symptom; gameplay with the supplied mixed-release
disks remains unverified. Lotus III and the scripted ZIP-selector defect remain open.

| Replay | Final cycle | CPU / hardware (unchanged) | Original output | Corrected output |
| --- | ---: | --- | --- | --- |
| Inside the Machine, 18,000 fields | 2557704030 | `8953C7873A196DFC` / `30DF46790E671E23` | `B9B4D078BAE4E870` | `9ABCF87EAF325734` |
| Miami Chase, 18,000 fields | 2557704046 | `A4F4907875FC5125` / `EDC878C83754F974` | `34BF8DA80259E6FA` | `97FF1A337929326D` |
| Lemmings, warmup 10,920 + measured 3,600 fields | 2063189662 | `249E90223C45891F` / `F4951BBFA89CA99A` | `45AB46F7600E1C9F` | `7D31C3889FF73760` |

All three finish with `unsupported=none` and real PCM. The separate Lemmings
reference/candidate capture runs cover 14,520 fields, preserve the level-one
scene and show continued moving lemmings. Their all-boot diagnostic output hash
is a different measurement from the warmup-excluded identity above. No independent
hardware image fingerprint is claimed. The changed Lemmings expectation follows
the independently justified pointer correction and inspected native replay.

## Frozen comparison

The [blitter-modulo comparison v1](../../scripts/run-lightweight-blitter-modulo-comparison-v1.ps1)
pins accepted `9bdc53a`'s engine and the corrected candidate. The shared runner,
CPU, disk and float libraries remain identical. Only native pixel output changes;
native CPU/hardware/cycle, dimensions, PCM and work budget are identical.
Historical protocols and identities are unchanged. Timing follows the previous
six balanced pairs, Normal priority, verified homogeneous topology and host-load
policy v2. The one-sided 95% upper frame-time bound must be at most 1% in every
workload; prior exceptions do not transfer.

| Component | SHA-256 |
| --- | --- |
| Accepted engine | `AE033B1E1F787E37ACFDFA355DFCE97EFF6F7C0433CB7CB7ED915F136DA03FC6` |
| Candidate engine | `DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365` |
| Shared runner | `40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF` |
| Copper68k | `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC` |
| CopperDisk | `4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F` |
| CopperFloat | `4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235` |

## Completed performance result

The complete **36-sample series is valid**, but the default gate is **not met**.
CPU 2 and protected SMT sibling 3 were verified on the Ryzen 5 5600X; priority
remained Normal. Host-load policy v2 telemetry was valid, with no concurrent
builds, tests or other replays. Every synthetic complete-workload identity matches
across builds; every native sample matches its prevalidated per-build identity.
All samples report zero measured steady-state allocation and real PCM. Final
engine/dependency hashes match the freeze. Independent recomputation reproduces
the protocol's statistics from all six pairs, without trimming or substitution.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| lores | 387.97 | 378.53 | +2.5863% | +6.2317% | ACCEPTANCE_REQUIRED |
| hires | 348.46 | 351.22 | -0.7905% | +0.6797% | PASS |
| lemmings | 255.64 | 257.46 | -0.7274% | +1.1355% | INCONCLUSIVE |

| Pair | Lores R / C FPS | Hires R / C FPS | Lemmings R / C FPS |
| --- | --- | --- | --- |
| 1 | 386.43 / 388.71 | 347.74 / 351.77 | 244.22 / 256.15 |
| 2 | 385.67 / 354.36 | 347.61 / 353.52 | 263.26 / 262.56 |
| 3 | 395.29 / 386.18 | 355.17 / 352.96 | 259.48 / 255.57 |
| 4 | 394.86 / 393.62 | 349.68 / 347.94 | 251.51 / 255.50 |
| 5 | 384.14 / 392.22 | 350.28 / 347.67 | 255.34 / 253.21 |
| 6 | 381.41 / 356.06 | 340.28 / 353.44 | 260.05 / 261.77 |

The slower lores C2/C6 samples did not cross the sustained-interference threshold
and remain included. Native Lemmings is faster on average, but its upper bound
also exceeds 1%. These two results require explicit owner acceptance; neither is
waived by the hires pass or older exceptions. Acceptance was pending at this
initial measurement; the later repeat and its scoped acceptance are below. No commit,
push or package publication is included in this investigation.

Evidence: `comparison-v1/protocol.log`, `samples.json`, `summary.json`, all 36
individual logs and `telemetry.jsonl` under the local investigation directory.
The prior `protocol-validation/` run exercised positive/negative identity checks,
missing-telemetry/noise rejection and live topology/telemetry without measuring.
Protocol SHA-256: `A29D56AFD012B45ED7B0583F55C1F7DC5F105869BD8808D348991035A5D4441D`.
Source/test snapshots and hashes are in `final-source-hashes.json`. Previous
protocols, native output identities and accepted performance records remain intact.


## User-requested repeat, 2026-09-21

At the user's request, the complete unchanged six-pair protocol was run once
again with the same frozen reference, candidate, dependencies, inputs, CPU 2 /
protected sibling 3, Normal priority and host-load policy v2. There were no
concurrent builds, tests or other replays. This is a separate **valid 36-sample
series**, recorded under `comparison-v1-repeat-1/`. No samples were pooled with,
substituted into, or removed from the first series. Its above-limit results
remain valid historical evidence.

All synthetic fingerprints match across builds and all native fingerprints
match their prevalidated per-build identities. Every sample has zero measured
steady-state allocation and real PCM. Telemetry and placement checks pass.
Post-run hashes confirm unchanged binaries, protocol and source/test snapshots;
independent recomputation confirms the following statistics.

| Workload | Reference mean FPS | Candidate mean FPS | Paired frame-time change | One-sided 95% upper bound | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| lores | 379.96 | 385.90 | -1.5263% | -0.0478% | PASS |
| hires | 343.48 | 349.46 | -1.7626% | +1.2977% | INCONCLUSIVE |
| lemmings | 251.48 | 257.47 | -2.3416% | -1.1599% | PASS |

| Pair | Lores R / C FPS | Hires R / C FPS | Lemmings R / C FPS |
| --- | --- | --- | --- |
| 1 | 386.37 / 394.67 | 345.67 / 347.26 | 245.72 / 256.26 |
| 2 | 387.40 / 395.15 | 345.84 / 341.39 | 250.03 / 254.47 |
| 3 | 387.64 / 394.24 | 318.19 / 348.73 | 246.22 / 256.64 |
| 4 | 370.22 / 366.47 | 344.32 / 346.91 | 252.97 / 257.61 |
| 5 | 381.01 / 381.70 | 350.85 / 355.89 | 258.55 / 260.20 |
| 6 | 367.14 / 383.16 | 356.01 / 356.60 | 255.38 / 259.65 |

The repeat passes lores and native Lemmings. Hires improves on average, but the
one-sided 95% upper bound is **+1.2977%**, above the required 1%. Therefore this
complete repeat also does **not** meet the default gate. Explicit acceptance of
this repeat's hires exception was granted by the owner; see below. Better individual workload results
from different series are not combined to declare a passing full comparison.
The slow hires R3 and other variable samples remain included because telemetry
did not invalidate them. No further engine changes, commit, push or publication
were performed for this benchmark request.

## Owner acceptance, 2026-09-21

The owner explicitly accepted the complete repeat's **+1.2977% hires upper-bound
exception** and authorized commit and push. Acceptance applies to frozen engine
`DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365`
and `comparison-v1-repeat-1` against the recorded accepted reference. Lores and
native Lemmings pass that repeat. Hires remains numerically INCONCLUSIVE, and
the earlier complete series retains its original results. No samples or separate
series are pooled or relabeled. The default 1% upper-bound limit remains in force
for future changes. This authorization does not include package publication.
