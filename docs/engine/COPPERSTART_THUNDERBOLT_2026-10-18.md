# CopperStart: Operation Thunderbolt — 2026-10-18

Operation Thunderbolt (Ocean, FR/en, Les Justiciers 2 compilation IPF release)
passes this bounded disk-one CopperStart replay. It loads through the protected
startup into the first mission, displays enemies and firing, consumes ammunition,
reaches score 200 and displays the continue screen. The native Kickstart 1.3
control reaches the same milestones. No new firmware defect was demonstrated;
this continuation changes qualification evidence only.

The [companion JSON](COPPERSTART_THUNDERBOLT_2026-10-18.json) contains exact
ROM/media/input identities, commands, captures, mode comparisons and reviewed
milestones. The [preceding Inside the Machine qualification](COPPERSTART_INSIDE_MACHINE_2026-10-18.md)
owns the unchanged rebuilt ROM's source, producer, build and native-test evidence.

## Frozen inputs and scope

- CopperStart source commit
  [`185430afc13cbe98dd349f9972788b68b99bd592`](https://github.com/ilehtoranta/CopperStart/commit/185430afc13cbe98dd349f9972788b68b99bd592).
- Exact 524,288-byte ROM, SHA-256
  `98BAD185B36AC86CB8B5F83590DEA113EC0182C04F286BEE7CAFDCC2BD755291`.
- PAL OCS 8371/8362, MC68000 interpreter, 512 KiB Chip plus 512 KiB A501,
  no Fast RAM and read-only DF0. All four runs use the same supplied disk-one
  IPF; native controls use the recorded Kickstart 1.3 ROM.
- Same frozen runner `67a54cc5…`, engine `5a69574f…`, CPU `f445a7d2…`
  and CopperDisk `335b64cd…`; complete identities are recorded in JSON.

The committed
[`ipf-thunderbolt-storage.json`](../../CopperScreen.Lightweight.Tests/Workloads/ipf-thunderbolt-storage.json)
sequence is used unchanged. It presses joystick fire and the left mouse button
at 6000, 8000, 10000, 12000 and 14000, releasing after twenty fields each.
No disk swap or deliberate aiming/mouse movement is included.

Both supplied disks are available, but only disk one is exercised. Disk-two
handling and full-game progression remain unverified. The [existing native
storage record](STORAGE.md) also leaves that transition unqualified; this report
does not replace it or infer that a decoder accepting disk two proves gameplay.

## Reviewed milestones

Both ROMs execute the game's native disk loader and progress through its story
intro. CopperStart shows the title/high scores at 4800, the Taito logo at 5600
and the opening mission at 6400/7200. Enemy gameplay appears at 8000, score 200
and reduced ammunition at 8800, the continue screen at 9600, and a fresh mission
after the next scripted press at 10800. The later reviewed fields are recorded
in JSON. This is positive sampled gameplay evidence, not a pass inferred from
an exit code or nonzero audio.

The native control shows its title earlier at 4000, the same high scores/Taito/
mission sequence, enemies at 8000, score 200 and the continue screen at 8800/9600,
and another mission at 10800. Different boot and interrupt phases change exact
scene timing and final fingerprints; cross-ROM byte equality is not claimed.

A separate CopperStart control retains the 6000/6020 start press/release and
omits every later firing pulse. All 100 capture files through 7600 match the
firing run before the first omitted event at 8000. Later ammunition/score and
screen differences are reviewed in the JSON to separate input response from
automatic enemy animation. This control does not establish deliberate aiming.

| Replay | Fields | Coverage |
| --- | ---: | --- |
| CopperStart, normal CPU | 18,000 | Story/title, first mission, enemies, firing/ammunition, score and continue/retry screens |
| CopperStart, scalar CPU | 18,000 | Same exact recorded inputs; all 230 capture files and final execution fingerprints match normal mode |
| Kickstart 1.3, normal CPU | 18,000 | Same media, machine and input sequence; equivalent bounded gameplay milestones |
| CopperStart, start-only control | 18,000 | Same first mission start, then no later firing inputs; identical prefix and reviewed later differences |

Each run captures every 400th field plus the initial field, including rendered
images, state metadata, Chip/A501 RAM and PCM from sampled fields. Raw manifests
retain their automatic `unreviewed` classification; visual review and the bounded
result are recorded here and in the companion JSON. Mode parity is a consistency
check, not independent hardware proof or continuous audio-fidelity evidence.

Both CopperStart modes end at cycle `2557836004`, CPU fingerprint
`B21C9BBFDD169307`, hardware `D6631BAC61D8888B`, output `1569C57E8FEC58F6`;
unsupported operations: none.

## Change boundary and validation

There are no new firmware, public caller-check, CPU, engine, disk-format or host
OS shim changes. The [older trace/Paula corrections](CPU_TRACE.md) remain separate
native CPU/chipset evidence using their own binaries and tests. Reaching gameplay
here establishes progress past this release's startup protection; it does not
certify every trace exception or receiver timing edge.

This is an evidence-only continuation. No new production build or unit-suite
pass is claimed. The exact ROM's preceding qualification retains those results;
the new work is the four native-media replays and their mode/input comparisons.
Documentation links, JSON consistency, replay completion, capture counts, binary
identities and comparisons are checked before committing.

Disk-two handling, full game completion, deliberate aiming/movement, audio
listening/fidelity, alternate hardware, cycle correctness and host throughput
acceptance remain unverified. ROMs, media and local artifacts are not committed;
no package is published and no standard-app firmware pin is changed.

Local evidence root: `artifacts/copperstart-thunderbolt-2026-10-18/`.
