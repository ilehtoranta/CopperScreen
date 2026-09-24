# Alien Breed II: supplied release requires AGA

2026-09-23. **Investigation resolved as unsupported media/profile mismatch.**
No runtime change is justified by this replay. OCS Alien Breed II compatibility
remains unverified because the corresponding media is unavailable locally.

## Exact media identification

The supplied `Team17/AlienBreedII-TheHorrorContinues_44.zip` contains SPS release
44, the AGA edition. All three entries match the size, CRC32 and SHA1 of
`abred2_a` in the [MAME project's AGA software catalog](https://github.com/mamedev/mame/blob/7717e680bd915969d41c253932c73e9489d06af7/hash/amigaaga_flop.xml#L138).
That catalog identifies this edition as requiring AGA; this conclusion is based
on the bytes, not merely the archive filename.

| Disk | Size | SHA1 |
|---|---:|---|
| `AlienBreed2_Disk1.ipf` | 1139240 | `2BE4018FD32B0F0E53EE8F42EB46FAFEDC177CD4` |
| `AlienBreed2_Disk2.ipf` | 1140380 | `343204FED87783FF300F045D1CEE5E741D44DBB9` |
| `AlienBreed2_Disk3.ipf` | 1140380 | `9C89C6DC3B723C2E71742BC41770CA0EE0616514` |

The distinct [OCS catalog entry, SPS 278](https://github.com/mamedev/mame/blob/7717e680bd915969d41c253932c73e9489d06af7/hash/amigaocs_flop.xml#L1739)
has different disk hashes. A scoped filename search under `C:/Data/TestImages`
found only the AGA release for this title. No new media was downloaded.
The [identification record](ALIEN_BREED_II_MEDIA_2026-09-23.json) records both
editions' identities, archive/entry SHA256 values, catalog revision and probe hashes.

## Observed failure mechanism

The original [corpus](NATIVE_CORPUS_2026-09-23.md) remains unchanged evidence:
black display and PC `$000FF334` from captured field 1140 through 8000, with
405 state/RAM/image files matching between scalar and batched execution.
The Tower Assault candidate produces the same 405 files for this title.

A bounded external instruction probe against the original frozen production
assemblies follows the loader's hunk relocation and BSS clearing. At PC
`$000FF334`, the loader clears `$3A654` longwords starting at `$0000BD1C`, ending
at logical `$000F566C`. On the supported A500's fitted 512 KiB chip RAM, this
range overlaps the physical `$0007F334` alias of the loader at `$000FF334`.
At CPU cycle 159059412, that clear instruction has become zero; its loop is
subsequently stuck at the same captured PC. This is consistent with the AGA
program's larger memory assumptions and the exact media identification.

The prior label of this release as an unresolved OCS engine failure is withdrawn.
The raw failure is preserved, but it cannot establish an OCS CPU, disk receiver
or memory-map defect. Expanding memory or adding a title-specific exception would
not make this AGA release a valid test of the current PAL OCS profile. This
investigation does not certify the decoder or every CPU operation either.

No emulator source, CPU package pin, game bytes or machine profile changed for
this investigation. A future OCS replay should use the identified SPS 278 disks
or another verified OCS edition and retain its own manifest/input evidence.

## Performance follow-up

The only runtime change under evaluation remains the
[Tower Assault OCS identification correction](TOWER_ASSAULT_INVESTIGATION.md).
The owner requested renewed measurement after this investigation. Its original
candidate/reference binaries and comparison v1 remained frozen. After three
invalid attempts, the fourth complete comparison is valid, with all 36 identities
matching, zero measured allocation and valid telemetry. The unchanged six-pair
lores/hires/native Lemmings gate requires a one-sided 95% upper regression bound
≤1% for every workload; measured bounds are **+5.0003%, +1.3831%, +2.1427%**,
respectively. No exception has been granted. Invalid partial series are not reused.

Tower Assault gameplay throughput is measured separately: the old engine cannot
execute the same gameplay, so comparing its Guru-screen FPS would be misleading.
The active-game measurement is not a substitute for the retained regression gate.
Six valid candidate samples average **291.77 FPS**, with matching complete
identities and zero measured allocation. The [performance record](TOWER_ASSAULT_PERFORMANCE_2026-09-23.json)
contains both series and their evidence hashes.

Local probes and captures are in ignored `artifacts/alien-breed2-investigation/`.
No ROM/media, RAM dumps, binaries or copyrighted disassembly are committed.
