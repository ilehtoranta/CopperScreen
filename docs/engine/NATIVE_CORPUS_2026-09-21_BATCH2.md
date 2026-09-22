# Additional native corpus, batch 2 — 2026-09-21

Six previously unrecorded games were selected from the supplied local collection:
Superfrog, F17 Challenge, Overdrive, Alien Breed Special Edition '92, Desert Strike
and Xenon 2. The three standalone demos in `C:/Data/TestImages` had already been
tested; no further demo path was supplied during this run. Arte and Desert Dream
were therefore extended beyond their previous 18,000-field budgets rather than
reported as new demo titles. No additional media was downloaded.

## Build, profile and method

Source checkout: `3e9517a8c98ca25595c77a2dcbc5ca3f10a50ce6`, including the accepted
final-row blitter correction from `c92b16b`. The existing verified Release binaries
were frozen without rebuilding or changing engine/runner code. PAL OCS A500,
68000, Kickstart 1.3, 512 KiB chip plus 512 KiB slow RAM, conservative CPU batching,
read-only DF0, no hardfile. The explicitly labeled Desert Dream two-drive check
adds disk B in DF1 from construction. All other runs have one connected drive.

| Component | SHA-256 |
| --- | --- |
| Engine | `DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365` |
| Runner | `182C691B5E7FE48F1A2294DB7AF2E7E3BA7C8B4F553E95274B849FD0098DDBE1` |
| CopperDisk | `54FE3F78B595E5EBE417C3DEC504005052AB437A90DCFBC65FA6440E3828C8F3` |
| Copper68k | `0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC` |
| CopperFloat | `4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235` |
| Kickstart 1.3 | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |

The [media manifest](NATIVE_CORPUS_2026-09-21_BATCH2_MEDIA.json) records archive and
uncompressed-entry hashes. Archives/ROM are unchanged. Initial images are loaded directly
from selected ZIP entries. Desert Strike disk 2 is extracted byte-for-byte into
ignored local `media/` for its scripted swap, avoiding the already-recorded
Windows ZIP-selector path bug. No media writes are enabled. Supplied
second disks are inventoried, but an inventory is not replay coverage.

Local evidence root: `artifacts/native-corpus-2026-09-21-batch2/`. It retains frozen
`runner/` and `reference-runner/`, full 908 x 313 BMPs, chip-RAM dumps and state
snapshots every 60 fields plus the final/unsupported boundary, logs, exact command
records, script snapshots, media/binary hashes and reference comparison results.
The reference differs only in the engine DLL: accepted pre-blitter-correction
`AE033B1E1F787E37ACFDFA355DFCE97EFF6F7C0433CB7CB7ED915F136DA03FC6`.
No unsupported-feature continuation flag is used. Earlier exploration logs and
scripts are retained separately from the final canonical inputs.

These are bounded compatibility checks, not hardware conformance or FPS gates.
Some diagnostic runs overlap; capture allocation and file I/O are intentional.
Their reported FPS/allocations are not acceptance measurements. Engine source and
its accepted performance evidence remain unchanged. Native music/sample activity
is inspected through PCM nonzero markers, not an audible host-output check.

## Outcomes

| Title | Bounded replay | Observed result |
| --- | --- | --- |
| Superfrog (IPF) | 8,000 fields requested; stops at 269 | Explicit unsupported live-WORDSYNC change. No title/gameplay or second-disk coverage. |
| F17 Challenge (IPF) | 8,000 requested; stops at 238 | Same explicit stop; no title/gameplay or second-disk coverage. |
| Overdrive (IPF) | 8,000 requested; stops at 265 | Same explicit stop; no title/gameplay or second-disk coverage. |
| Alien Breed Special Edition '92 (IPF) | 8,000 | Black loader state throughout sampled post-boot frames, no nonzero audio captures; unresolved. |
| Desert Strike (PDX ADF disk 1 + supplied unlabeled disk 2) | 36,000 requested; stops at 22,084 after swap | Mouse click passes the crack intro; fire advances animated titles/story to the disk-2 request. Disk 2 loads data after the swap, then the existing `DF0 seek beyond the standard ADF cylinder range` guard stops the run. Gameplay is not established. |
| Xenon 2 (Band/Cardinals ADF) | 30,000 with input | Clicks pass the crack intro and trainer with defaults off. Joystick fire advances titles/credits to **Insert game disk**. The supplied archive contains only disk 1 and disk 2 is absent from the provided collection; gameplay is unavailable coverage, not an emulation failure. |
| Arte / Sanity (ADF) | 36,000 without input | Extends the earlier 18,000-field run through later particle effects, 3D credits, closing artwork at 32,700 and a return to an earlier star scene by 36,000. No obvious persistent corruption in inspected captures. This is sampled sequence completion, not a full hardware-frame comparison. |
| Desert Dream / Kefrens (ADF) | 36,000 disk A; 24,000 with B in DF1 | Later point-cloud effects lead to corrupted execution and black/solid-color frames near 19,380. Reproduces on the pre-fix engine and with disk B already mounted. Second-disk progression and ending remain unverified. |

Desert Strike's earlier no-swap `desert-strike-final` attempt ends at field
24,455 with `DF0 seek beyond the standard ADF cylinder range` while the visible
screen still requests disk 2. This is a retained explicit stop, not a passing
36,000-field run or proof that the supplied disk 2 fails. The final swap check
uses PDX-labeled disk 1 and the supplied unlabeled disk 2; the exact release
combination is preserved in the manifest. It also hits the same explicit guard,
at field **22,084**, PC `00C1CA8C`, cylinder 79/head 1, after the disk-2 data load
and before gameplay. Whether the seek reflects normal drive behavior, a loader
expectation or the supplied release combination is unresolved. This particular
ADF replay was not repeated against the pre-fix engine. Disk 3 is unavailable.

The no-input ADF exploration budgets were 10,000 fields, followed by 16,000-field
single-click and 18,000-field start-button checks. They are exploratory milestones,
not extra passing games. Final script snapshots are kept next to command logs so
later script refinements do not obscure the earlier inputs.

## Reproduced blockers and reference comparisons

### Live WORDSYNC changes block three IPF titles

Superfrog stops at field **269**, F17 Challenge at **238**, and Overdrive at **265**.
Each run throws the existing explicit unsupported-feature message:
`WORDSYNC enable change during active disk DMA`. These are emulation coverage
stops, not Windows launch failures or successful game boots. The same selected
IPFs stop at exactly the same snapshots on the pre-fix engine. All captured
state, images and chip RAM match: respectively 6, 5 and 6 checkpoints.

| Title | PC at stop | Cycle | DSKLEN | ADKCON | Cylinder/head |
| --- | --- | ---: | --- | --- | --- |
| Superfrog | `0007FA78` | 38085752 | `9613` | `1100` | 2/0 |
| F17 Challenge | `0007F74A` | 33688330 | `9713` | `1100` | 1/0 |
| Overdrive | `00078E44` | 37525072 | `9616` | `1100` | 1/0 |

This gives the existing live-WORDSYNC implementation gap three concrete native
reproducers. It does not justify removing the guard without a hardware-backed
transition model and focused timing tests. Second disks and gameplay are
unavailable coverage in these runs.

### Alien Breed Special Edition '92 remains black

The supplied boot IPF completes an 8,000-field budget but remains black and has
no nonzero audio captures. The final PC is `0007F6D6`, cylinder 0/head 1,
`DSKLEN=8000`, `ADKCON=1500`, and no active DMA transfer. `unsupported=none` does
not make this a passing boot. All 135 state, image and chip-RAM checkpoints are
byte-identical to the pre-fix engine. Protection/loader, receiver or media causes
have not been isolated; no claim is made that disk 2 would resolve this state.

### Desert Dream fails beyond the previous tested portion

The 36,000-field disk-A replay passes the earlier intro, then reaches point-cloud
effects near fields 18,600–19,320. At the next captured checkpoint, **19,380**,
the PC is `26180000`; later captures become black or solid colors with corrupted
execution state. This is a newly exposed late failure, not an extension of the
previous bounded visual pass into a full-demo pass.

The pre-fix 24,000-field replay has **401 byte-identical state, image and chip-RAM
checkpoints**, including the late failure. This rules out the final-row modulo
correction as the differentiating change for this reproduction. A separate
24,000-field replay with disk B pre-mounted in DF1 still reaches the late failure.
It does not establish a successful second-disk transition. The subsystem and
hardware cause remain unassigned; no CPU, blitter or disk correction is inferred
solely from the corrupted PC.

## Reproduction

Select the exact ZIP entry recorded in the manifest. For example:

```powershell
$runner = 'artifacts/native-corpus-2026-09-21-batch2/runner/CopperMod.Amiga.Lightweight.Runner.dll'
dotnet $runner --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/Team17/F17Challenge_1703.zip#/F17Challenge_Disk1.ipf' `
  --frames 8000 --wide-output --boot-probe artifacts/new-corpus-replay/f17
```

Use fresh evidence directories. The three explicit stops are expected failures,
not runs to bypass. Other no-input runs use 8,000 fields for Alien Breed SE and
36,000 for Arte/Desert Dream. The final Desert Strike and Xenon 2 scripts are
[corpus-desert-strike.json](../../CopperScreen.Lightweight.Tests/Workloads/corpus-desert-strike.json)
and [corpus-xenon2.json](../../CopperScreen.Lightweight.Tests/Workloads/corpus-xenon2.json),
with 36,000 and 30,000 fields respectively. Before the Desert Strike replay,
extract the manifest's `desert-strike-disk2` entry unchanged as
`media/corpus-desert-strike-disk2.adf`. Its script ejects disk 1 at field 18,000
and mounts disk 2 at 18,120. Input frames are zero-based; empty entries release controls.
The Xenon 2 trainer defaults remain off; clicks select its default Start Game.
For Desert Dream with disk B already in DF1, add `--drives 2` and
`--adf1 'C:/Data/TestImages/Desert Dream (Kefrens).zip#/Desert Dream (Kefrens) B.adf'`.

## Retained identities and checks

The table records diagnostic workload identities, not correctness goldens.
Completing a budget with `unsupported=none` can still leave a black screen or
corrupted guest execution, as this batch demonstrates. The three live-WORDSYNC
stops and both Desert Strike drive-range stops have no successful workload
summary; their final state/bitmap and exception logs are the evidence.

| Replay | Completed fields | Final cycle | CPU | Hardware | Pixel/audio output |
| --- | ---: | ---: | --- | --- | --- |
| alien-breed-se92-boot | 8000 | 1136684046 | `0xD949DA557E50DA35` | `0x43C42E91AB967D96` | `0x8CBE35645FABC58B` |
| arte-extended-boot | 36000 | 5115540140 | `0xB45EFE6C931B0E7F` | `0x49163B939297A909` | `0xE6FB2760B41DAC15` |
| desert-dream-extended-boot | 36000 | 5163386030 | `0x02DA9F8FDD30E5D4` | `0x900740C83E33939C` | `0xAD99AD2021DBBD14` |
| desert-dream-extended-reference | 24000 | 3421981830 | `0xF03FD37CB9D44E74` | `0x28F5B598D751E88E` | `0x4F77870B6E1E9E52` |
| desert-dream-extended-two-drives | 24000 | 3421981520 | `0xF3992D49828229E6` | `0x54B3B5F7DA09400E` | `0xC5FD121379DD2380` |
| xenon2-final | 30000 | 4262928034 | `0x0F4A684FA98ED95C` | `0x8626F006057EE087` | `0x2A46F4A789AE4D1C` |

ROM and every selected source archive were hash-checked again. The canonical
input scripts are valid, strictly ordered JSON and match their final replay
snapshots. Both frozen engine hashes and the unchanged runner/dependency hashes
were rechecked. Local `reference-comparison.json`, `retained-results.json` and
per-run command/script snapshots preserve the comparisons and output identities.
Documentation links and whitespace checks pass. No engine/host/disk source was
modified, so no new build, unit-suite or performance acceptance claim is made.
The earlier accepted build/tests and performance records remain intact.

Next engineering priority from this batch is the live-WORDSYNC transition:
it has three small native reproducers before title loading. Alien Breed SE and
Desert Dream need separate loader/execution investigations; the exact pre-fix
matches prevent attributing those failures to the recent modulo correction.
