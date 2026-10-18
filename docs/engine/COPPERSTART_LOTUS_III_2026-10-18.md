# CopperStart: Lotus III — 2026-10-18

Lotus III: The Ultimate Challenge (Fairlight / Crack Inc two-disk ADF release)
passes this bounded CopperStart replay: crack intro, disk-two handoff, title,
options, car/music selection and the opening race. Holding the accelerator
moves the car and reaches 89 KPH; time-up returns to the score display. No new
firmware defect was demonstrated, so this continuation changes evidence only.

The [companion JSON](COPPERSTART_LOTUS_III_2026-10-18.json) records the exact
ROM/media/script identities, commands, capture hashes and reviewed milestones.
The [preceding Full Contact qualification](COPPERSTART_FULL_CONTACT_2026-10-18.md)
owns the rebuilt ROM's fixes, source overlay, build and native-test provenance.

## ROM and machine

The exact Full Contact ROM is reused unchanged:

- CopperStart source commit
  [`55f39525acb1ecebf60e04267907d2d74e6b2aee`](https://github.com/ilehtoranta/CopperStart/commit/55f39525acb1ecebf60e04267907d2d74e6b2aee).
- 524,288-byte ROM, SHA-256
  `F41CE8979C51BA4CC7234ACD58C0A6A228EF1728A59853E41F20E0A62C192324`.
- PAL OCS 8371/8362, MC68000 interpreter, 512 KiB Chip plus 512 KiB A501,
  no Fast RAM, read-only DF0. Original-ROM controls use Kickstart 1.3 and
  the same machine, disks, runner and scripts.
- Frozen runner `67a54cc5…`, engine `5a69574f…`, CPU `f445a7d2…`,
  CopperDisk `335b64cd…`; complete identities are in the JSON.

This qualification does not replace the standard app's separately pinned
firmware contract, publish a package, or relabel older evidence. There are no
new CPU, engine timing, host OS shim or public caller-check changes. No ROMs,
media or local build outputs are committed.

## Inputs and comparison

The first 18,000-field run uses the existing
[`corpus-lotus3.json`](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3.json)
sequence. Only its disk-two path is remapped to the supplied ZIP. Return exits
the crack intro at 3500/3510; DF0 ejects at 5000 and inserts disk two at 5100;
fire presses follow at 6500, 8500 and 10500. Later inputs reach the CopperStart
options and car-selection screens. All **230 capture files per CPU mode** match.

CopperStart passes the disk-two prompt after the first fire press. The native
control passes after the third. This phase-sensitive consumption of a fire edge
has [existing engine investigation and correction evidence](LOTUS_III_CPU_BUS_TIMING_2026-09-25.md).
That historical record uses different engine/CPU binaries; its results are not
relabeled here. Different ROM startup phases and milestone times alone do not
establish a new compatibility fault or a performance result.

The longer script preserves that complete early sequence and adds confirmations
at 18800, 20800 and 22800. CopperStart shows car selection at 18400, music
selection at 19200/20400, race-one information at 21200/22400 and the opening
race at 22800/23600. The manual describes fire-based car/music confirmation;
see the [original game manual transcription](https://www.lemonamiga.com/doc/lotus-3-the-ultimate-challenge/1024).
The game observations, rather than assumed menu timing, determine these inputs.

Holding fire from 24800 accelerates the car: at 25200 the speed is 89 KPH and
the course position/score advances. Time expires by 26400, followed by a score
screen at 26800. Directional events at 26800 and 28000 occur after CopperStart's
time-up; they do not establish steering response during the race.
An automatic driving demo follows at 30000 and the title returns at 32000.

A separate control retains every menu and race-start confirmation through
22820, then supplies no further input. Its captures match the driven run
through 24400. At 25200 the control coasts at a lower speed with zero score,
while the driven run displays 089 KPH and score 63770. The reviewed frames are in
the JSON. An earlier incomplete control stopped
before music/race confirmation; it was intentionally terminated and is excluded.

## Results and limits

Captures include rendered frames, state metadata, Chip/A501 RAM and PCM from
every 400th field, plus the initial field. Raw runner manifests retain their
automatic `unreviewed` classification; the visual review is recorded here and
in the companion JSON. Replay completion alone is not the pass criterion.

| Case | Fields | Coverage |
| --- | ---: | --- |
| CopperStart original script, normal/scalar | 18,000 each | Disk-two handoff, title, interactive options and car selection; all 230 files per mode match |
| Kickstart 1.3 original script | 18,000 | Same disks pass the prompt; title, driving demo and high scores visible |
| CopperStart extended script, normal/scalar | 32,000 each | Menu confirmations, opening race, accelerator response and time-up; all 405 files per mode match |
| Kickstart 1.3 extended script | 32,000 | Same extended inputs and media; milestones recorded separately because startup/menu phases differ |
| CopperStart no-acceleration control | 32,000 | Same selections, then released controls; isolates the later accelerator response |

The native extended run reaches its own car/music selection and race-one
information, then drives at 28000/30000. Its 32000 capture is a later transition.
The two CopperStart race modes end at cycle `4547264000`, CPU fingerprint
`2A961E56DB4CE5AB`, hardware `B3E388499CBD60E5`, output `8AF5A7CB37816CEC`;
unsupported operations: none.

The 18,000-field and 32,000-field cases overlap substantially and are not
independent unique gameplay coverage. CPU-mode parity compares CopperStart
against itself; cross-ROM fingerprints and exact frame timings are not expected
to match. PCM parity covers captured fields, not the full continuous soundtrack.

This is an evidence-only continuation. No new production build or unit-suite
pass is claimed; the preceding exact-ROM qualification retains those results.
Documentation links, JSON consistency, complete replay status, capture counts,
binary identities and parity are checked before committing the record.

Race completion, steering response, the full game, RECS track creation, all menu
choices, audio listening/fidelity, hardware correctness and host throughput
acceptance remain unverified. The ordinary diagnostic replay is not a formal
performance measurement.

Local evidence root: `artifacts/copperstart-lotus3-2026-10-18/`.
