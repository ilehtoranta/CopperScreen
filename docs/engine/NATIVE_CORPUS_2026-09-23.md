# Five additional native games — 2026-09-23

Five previously unrecorded titles from the supplied collection were exercised:
Lotus Esprit Turbo Challenge, Railroad Tycoon, Worms, Alien Breed II and Alien
Breed: Tower Assault. These are bounded native compatibility observations, not
full-game certification, new golden outputs or performance measurements.

Subsequent identification corrected one selection: the supplied Alien Breed II
SPS 44 set is **AGA**, not an OCS compatibility case. All three disk identities
match the AGA catalog. See the [investigation](ALIEN_BREED_II_INVESTIGATION.md).
The original failed replay below is preserved; it is not an OCS engine defect.

## Configuration and evidence

Checkout `cb946eb5791c31cafa20823f99f5d207e5da6fb5`; the production Release outputs
from the preceding allocation-test investigation were frozen without rebuilding.
That investigation built successfully with zero warnings/errors. The production
CPU is the shipped `1.4.1-locality.1`, **not** the unpublished batch-prefetch
candidate. No engine, CPU, runner or runtime configuration changed for these runs.

PAL OCS A500, 68000, Kickstart 1.3, 512 KiB chip plus 512 KiB slow RAM, one DF0,
read-only media and no hardfile. Conservative CPU batching is used except for the
explicit scalar controls. No unsupported-feature continuation option is enabled.
Full 908 × 313 field images, state and chip RAM are captured every 60 fields,
at the first field and at the final field.

The [media manifest](NATIVE_CORPUS_2026-09-23_MEDIA.json) records archive, entry,
ROM and assembly hashes. Supplied disk images and ZIP archives remain unchanged.
The [run record](NATIVE_CORPUS_2026-09-23_RESULTS.json) preserves all 15 bounded
runs, final states/capture hashes, command arguments and scalar comparisons.
Railroad Tycoon disk B is extracted byte-for-byte for its scripted replacement,
avoiding the existing Windows scripted ZIP-selector bug. The remaining second
and third disks are inventoried, not claimed as exercised.

Local evidence: `artifacts/native-corpus-2026-09-23/`. Each run retains its full
argument array, input snapshot where applicable, exit code, log and captures.
The frozen runner and binary identities are retained in the same directory.
Some diagnostic runs overlap; their capture allocations and reported FPS are
not steady-state allocation or throughput acceptance results. Audio coverage is
limited to nonzero PCM markers; host playback was not listened to.

## Outcomes

| Title / supplied release | Retained replay | Result |
| --- | --- | --- |
| Lotus Esprit Turbo Challenge / Angels ADF labeled `[b doscopy]` | `lotus1-start`, 16,000 fields; [input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus1.json) | Attract artwork, menus, music selection and race reached. Scripted acceleration/steering changes the road/player position and HUD speed. Racing is visible at 12,000, 13,800 and 16,000; nonzero PCM is captured. No obvious persistent corruption in inspected captures. No lap or full-race completion claimed. |
| Railroad Tycoon v855.01 / original ADF with manual-code label | `railroad-map`, 36,000 fields; [input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-railroad.json) | Input selection, disk A → B → A replacement, credits, region and difficulty selection work. The Eastern USA map is generated; a mouse click advances to the opening RailNews newspaper announcing a new railroad and stock sale. Nonzero PCM is captured. Scenario initialization is established; railway construction, train operation, saving and extended play are not verified. |
| Worms / supplied IPF release 230 | `worms-code`, 16,000 fields; [input](../../CopperScreen.Lightweight.Tests/Workloads/corpus-worms-code.json) | Boots to the illustrated manual-code prompt. At the owner's suggestion, a bounded arbitrary `1234` + Return attempt was made. It leads to another code prompt and does not reach a menu or gameplay. This does not establish an any-code crack. The owner has no code table; gameplay coverage remains unavailable. |
| Alien Breed II / supplied IPF release 44 | `alien-breed2-boot` and `alien-breed2-scalar`, 8,000 fields each | Black post-boot loader state; PC `000FF334` repeats at every captured checkpoint from field 1,140 onward. Final drive state is cylinder 17, head 0, DMA inactive; no nonzero audio checkpoints. Unresolved native failure; no gameplay. |
| Alien Breed: Tower Assault / supplied IPF release 279 | `tower-assault-boot` and `tower-assault-scalar`, 8,000 fields each | By captured field 600, shows `Guru Meditation #0000FFFF.2E410026` and remains in Software Failure. No menu, gameplay or nonzero audio checkpoints. Unresolved native failure. |

Railroad Tycoon's intermediate runs are retained: `railroad-start` (12,000)
reaches the disk-B request, `railroad-disk2` (18,000) reaches credits,
`railroad-menu` (23,000) reaches region selection, `railroad-scenario` (28,000)
reaches the disk-A request, and `railroad-roundtrip` (34,000) generates the map.
These are successive exploratory runs, not additional games or full-game passes.

Lotus's supplied dump label is retained for identity, not used to explain away
any defect. A successful runner exit does not mean a game passed: both Alien
Breed titles complete their requested field budgets while visibly failing.

## Failure controls and limits

Subsequent work is recorded separately in the
[Tower Assault investigation](TOWER_ASSAULT_INVESTIGATION.md): the initial Guru
is traced to absent OCS DENISEID readback and corrected in a candidate. The
original results below remain the observations from the frozen pre-change build.

Alien Breed II and Tower Assault were each repeated from cold boot with
`--scalar-cpu`. For **each title**, all 135 captured states, all 135 chip-RAM dumps
and all 135 full field images match the batched run byte-for-byte (405 files).
This rules out a divergence between these two execution modes in the observed
replay. It does not rule out a defect shared by both CPU paths, memory mapping,
IPF decoding, receiver behavior or loader integration. No root cause or supported
memory requirement is inferred from the black screen/Guru alone.

The first follow-up should isolate the loader's failure point using these exact
images and snapshots. Do not add title-specific engine behavior,
discard the current failure record or silently change the A500 profile to obtain
a passing screenshot. Later disk transitions and gameplay remain unverified.

## Reproduction

Use the corresponding selector from the media manifest and an independent output
directory. For example:

```powershell
dotnet artifacts/native-corpus-2026-09-23/runner/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/Team17/AlienBreedII-TheHorrorContinues_44.zip#/AlienBreed2_Disk1.ipf' `
  --frames 8000 --wide-output --boot-probe artifacts/alien-breed2-replay
```

Add `--scalar-cpu` for the control. The other boot checks likewise use 8,000
fields without input. For interactive runs, use the table's final field count and
`--input-script` link. Inputs use zero-based PAL field indices and persist until
replaced. For Railroad Tycoon, first extract the manifest's exact disk A and B
entries to ignored `media/railroad-disk1.adf` and `media/railroad-disk2.adf`;
the checked-in script resolves those paths from
the Workloads directory. Local probe scripts use the equivalent path relative to
their evidence directory. No ROMs or game media are committed.
