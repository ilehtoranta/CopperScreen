# North & South display investigation — 2026-09-20

## Correction after the real-machine report

**A Copper restart defect is now reproduced and corrected in the candidate.**
When a frame began with Copper DMA disabled, the engine eagerly selected the
old `COP1LC`. Enabling DMA later executed that old list despite the game's
intervening pointer update. A pending frame restart must consume the current
list pointer when it obtains its bus cycle. This was an emulator bug, not a
reason to dismiss the CP release. The user's real-A500 report prompted this
correction to the earlier assessment below.

The candidate retains a pending restart control state across disabled DMA.
Its accepted dummy read uses the existing address/output phases and arbitration;
the output phase loads current `COP1LC`, followed by ordinary instruction fetch.
An ordinary mid-field pause does not restart the list. No title checks, media
patches, allocation, polling callback or separate timeline were introduced.
The already-running frame-start path is unchanged; this is not a full rewrite
or certification of every COPJMP/conflict/sub-CCK boundary.

Independent evidence is provided by Dirk Hoffmann's
[lc probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Agnus/Copper/lc),
including their published A500 **ECS** photographs (not mislabeled as OCS photos).
The tests change list pointers with Copper DMA disabled, both at vertical blank
and later in the visible field. `lc3` changes the pointer twice before activation;
`lc4` activates between changes and distinguishes a later ordinary pause.
The OCS-profile candidate and reference each ran their ADFs for 900 fields:

| Probe | Reference `1db2d0d` | Candidate | Published A500 photograph |
| --- | --- | --- | --- |
| lc1 | Old red list above grey | Green above grey | Green above grey |
| lc2 | Black then old red list | Black then green | Black then green |
| lc3 | Black then old red list | Black then blue | Black then blue |
| lc4 | Black then old red list | Black then green | Black then green |

This comparison verifies list selection, not photographic pixel/beam alignment.
Supporting emulator implementations also preserve late list selection:
[vAmiga's inactive-frame pointer writes](https://github.com/dirkwhoffmann/vAmiga/blob/585a88c6da11f79524760a29952c4cdd584703c0/Core/Components/Agnus/Copper/CopperRegs.cpp).
They are supporting evidence, separate from the hardware photographs and the
user's reported A500/Kickstart 1.3 result.

Four new synthetic cases fail before the fix and pass afterward: Copper-only
or master DMA disabled across one or two frames, followed by pointer changes.
A fifth test retains the old list when an already-executed Copper is merely
paused. All 27 Copper tests pass. Production Release builds with zero warnings
and errors; 639 engine tests, 74 CopperDisk tests and 92 ordinary host tests pass.
All three optional native host tests were then enabled with the supplied media
and also pass (808 passing cases in total across these runs; no native skip
counted as successful coverage).
An initial full diagnostic run had a 3,808-byte Paula serial allocation assertion
failure; the unchanged complete rerun passes. This initial failure is retained
in `engine-tests.log`, not hidden or counted as a pass.

Untouched CP native replay now renders the clean menu **and correct borders**
without the earlier one-register intervention. Candidate engine SHA-256:
`4DEF0F612624A6556F4BCF49304BDF6D9C7F1B74F9CB9A1CC53C9CDC6AEC6968`.
Its 8,500-field replay ends at cycle `1207735192`, CPU `A68AAFDE52E5F290`, hardware
`20525A3C576EF2AA`, output `F6E7F9CC92BD2853`, with `unsupported=none`.
Evidence: `artifacts/north-south-investigation/cp-candidate/` and
`lc-reference/`, `lc-candidate/`, `hardware-lc/` under the same root.

The frozen native Lemmings preflight retains the accepted complete workload:
cycle `2063189682`, CPU `BCF441F9BABF1113`, hardware `206DA179786FCB2A`,
output `F7328C7C24582FCB`, 14,520 completed fields and zero measured allocations.
Formal performance acceptance is separate and pending below. Gameplay of North
& South remains unverified; the repaired menu alone does not certify it.

### Performance candidate

The user authorized committing and pushing this fix on 2026-09-20 with the
invalid benchmark disclosed. This does not establish compliance with the 1%
performance limit or change the invalid measurement's disposition.

[Homogeneous retention v5](../../scripts/run-lightweight-homogeneous-retention-v5.ps1)
uses accepted `1db2d0d` as the reference, changing only the reference pin and
protocol identity from v4. The six balanced pairs, workloads, timing intervals,
complete-workload identities, Normal priority, topology/telemetry rejection rules
and one-sided 95% upper frame-time bound of 1% are unchanged. The previous
lores exception does not transfer. The frozen benchmark runner and dependencies
are retained from the previous protocol on both sides; only the engine DLL differs.
Native preflight matches the existing identity before timing. Formal results
are **INVALID / RERUN**, recorded under `retention-v5/`: after lores and part
of hires, the unchanged host policy rejected package interference above 25%
for 10.298 seconds. The final telemetry sample reports 40.376% package load.
No complete accepted series or performance waiver exists for this candidate.
The partial lores calculation (+1.171% mean, +2.712% upper bound) remains a
diagnostic observation in this invalid series, not an acceptance result.
All raw samples, telemetry and `INVALID.txt` are preserved. A valid full rerun
is still required; the default 1% bound is unchanged.

## Earlier investigation evidence (preserved)

The reproduced CP-release corruption comes from the old crack-intro Copper list
overwriting the game's display setup during handoff. In particular, it restores
`BPL2MOD=2` after the game clears both modulos. Clearing that one register in an
external diagnostic restores the menu; the supplied QTX release renders both
language selection and the main menu correctly on the unchanged engine.

The user subsequently reported correct operation on a real Amiga 500 with
Kickstart 1.3, 512 KiB chip RAM and 512 KiB expansion RAM described as FAST RAM.
The earlier inference that the release's handoff should explain away the failure
is withdrawn. The trace identifies the corruption mechanism in our emulator;
the Copper restart/address-latching behavior remains an active emulation
investigation. The exact expansion address and disk hash of the physical test
have not been independently captured. No title-specific workaround is warranted.

## Reproduction

Use accepted `1db2d0d`, the frozen runner, ROM and PAL OCS profile identified in
[the game corpus](GAME_CORPUS_2026-09-20.md). Both releases are standard ADF entries
inside ZIP archives. The [media manifest](GAME_CORPUS_2026-09-20_MEDIA.json) includes
both archive and uncompressed-image hashes. No source media was modified.

- CP: [original input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-north-south.json), 8,500 fields.
- QTX: the same script and 8,500 fields reach the clean menu.
- QTX extended attempt: [bounded script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-north-south-qtx.json), 13,500 fields. Additional clicks near GO did not establish gameplay; the final capture remains at the menu. This is menu coverage only.

Example with the supplied local QTX image:

```powershell
dotnet artifacts/game-corpus-2026-09-20/runner/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/North & South (1989)(Infogrames)(M5)[cr QTX].zip' `
  --frames 8500 --wide-output `
  --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-north-south.json `
  --boot-probe artifacts/north-south-replay
```

## Register evidence

The CP intro's Copper list contains `0108 0000` at chip address `$060AAC` and
`010A 0002` at `$060AB0`. The first observed nonzero `BPL2MOD` is in field 408.
That is a legitimate setting for the intro's separate playfields; it becomes
wrong for the later game's contiguous 40-byte display rows.

An isolated source copy logged custom-register writes, without changing their
execution order. At field 2,881 the following sequence occurs. CPU PCs in the
trace are the core's current PC at the write, which may follow the opcode.

| CPU cycle | Action | Copper PC after action |
| ---: | --- | --- |
| 409295498 / 409295514 | Game sets `DIWSTRT=$2B81`, `DIWSTOP=$2BCA` | `$060A78` |
| 409295582 / 409295586 | Game clears `BPL1MOD` and `BPL2MOD` | `$060A78` |
| 409296060 / 409296064 | Game installs `COP1LC=$0000AC84` | `$060A78` |
| 409296080 | Game writes `DMACON=$83A0`, enabling Copper DMA | `$060A78` |
| 409296154 / 409296162 | Old intro list restores `DIWSTRT=$2981`, `DIWSTOP=$F1C1` | `$060A9C` / `$060AA0` |
| 409296194 / 409296202 | Old intro list restores modulos 0 and 2 | `$060AB0` / `$060AB4` |

The newly installed list runs in the following field. There are no subsequent
modulo writes through field 8,500, so the stale even-plane stride remains.

Changing a Copper list pointer is distinct from restarting the Copper program
counter; the documented restart mechanisms are a jump strobe and vertical
blanking. The observed stale-list continuation is consistent with that distinction.
See the Commodore [Copper register and startup documentation](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_2.html).

The hardware adds a modulo at the end of each fetched row; `BPL2MOD` serves even
bitplanes. A value of 2 makes those planes advance by 42 bytes while the image
requires 40, producing the observed accumulating misalignment. See the
Commodore [playfield fetch and modulo documentation](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_3.html).
These documented rules support the diagnosis, but are not an independent trace
of this game's exact CPU/Copper timing.

## Controlled intervention and trace checks

An external probe using the frozen engine runs the CP script unchanged except
for one diagnostic register write before field 5,900:
`WriteCustomRegisterFromCopper(0x10A, 0, machine.Cycle)`.
The resulting field-8,500 menu is legible; `DIWSTRT/DIWSTOP` still retain the
intro's values, so its border differs from QTX. This isolates the major corruption
to the even-plane modulo and distinguishes it from damaged graphics data. The
intervention is not a supported replay, media repair or proposed engine behavior.

The source copy used for write tracing reproduces the original CP final capture
exactly. SHA-256 values match for all three field-8,500 artifacts:

| Artifact | SHA-256 |
| --- | --- |
| BMP | `15D8210AD8713050BF2E0D7A8061DB41F53702515F50E6A19A1B97E1C5AB2AC2` |
| Register JSON | `D6EF4D6C60B173B02FBC8D256CA9A8E2209322E3DC37BA7DD26ACDC2A22FF864` |
| Chip RAM | `66C1D247202E09E17488BFFAD4D393B7AD6299DABC963B43ED82FA6EE38D99E3` |

Intervention BMP SHA-256:
`EBC60695CE9B8084B09F99D8AB0FD69982C5D3E8A9F3558F5814FD819A570110`.
Untouched QTX menu BMP SHA-256:
`8BD3B202C1BDDE9928E08D9DF279B3E0E93FAE366BE45456E367129FA6896862`.

QTX complete-workload identities from the production runner:

| Fields | Final cycle | CPU | Hardware | Pixel/audio output |
| ---: | ---: | --- | --- | --- |
| 8,500 | 1207735118 | `443B33D2CA218C3A` | `8FAD28F0AF5F990F` | `8FD163AB65ACD452` |
| 13,500 | 1918245056 | `9A3D2230825A16C8` | `A044911289800426` | `0BD7E0698114748D` |

Local diagnostic evidence is under `artifacts/north-south-investigation/`:
`cp-write-trace.log`, `cp-handoff-trace.log`, `modulo-trace.log`,
`cp-modulo-zero/`, `qtx-menu/`, `qtx-map/`, and the isolated probe/source-copy
projects. Production source and binaries were not edited. An initial diagnostic
build used the runner assembly identity and failed friend-API access checks;
the source copy then used the engine identity in its isolated output directory.
No such build failure is counted as native coverage.

Repeated menu captures also sample the CPU in a blitter wait. A separate bounded
probe finds an active 20-word × 800-row A-to-D copy with a recent start cycle
and a pending output two cycles ahead; the busy snapshot alone is not evidence
of a hung blitter. Gameplay and audible sound remain unverified.

QTX remains useful as a comparison, but does not resolve the CP discrepancy.
The user's hardware result reopens the Copper handoff investigation. Any engine
correction needs focused regression tests, native replay and the unchanged 1%
performance gate; the earlier diagnostic intervention is not a fix.
