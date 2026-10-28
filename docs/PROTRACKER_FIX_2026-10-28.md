# ProTracker startup fix — 2026-10-28

The [startup investigation](PROTRACKER_STARTUP_2026-10-28.md) led to a native
CopperStart Classic ROM screen and input implementation. The editor now starts,
and the test pattern's three inserted notes match Kickstart's pattern bytes.
This is a development ROM compatibility result; the app firmware pin and
historical comparison captures remain unchanged.

## Change boundary

All production changes are in the CopperStart producer:

- `rom/CopperStart.SystemRom/NativeClassicIntuition.cs` installs a permanent
  bounded screen library with real bitmap/View/Copper-list ownership and
  screen lifecycle operations. It accepts ProTracker's 320 × 12 behind-screen.
- `NativeClassicInput.cs` supplies a native input.device handler chain. The
  existing console keyboard owner retains CIA reception/acknowledgement;
  handler callbacks run from the console task. Relative mouse events use the
  same task context. Unsupported commands return IOERR_NOCMD.
- `NativeClassicCia.cs` supplies guest CIA-B timer/interrupt ownership and
  dispatches actual hardware interrupts through Exec. ProTracker's native
  playback path can acquire its CIA timer instead of failing OpenResource.
- `NativeBootConsole.cs`, `Program.cs` and the build export list connect those
  services to the standalone reset-bank boot. A1200/desktop extensions retain
  their existing startup owners.
- The graphics display emitter supports short logical viewport stops with a
  real Copper WAIT/bitplane-disable sequence. The graphics resident moves to
  `$FF9800` to retain space for those instructions.

Moving an already-front screen forward now leaves its display untouched.
An intermediate build exposed why this is necessary: reloading the generated
12-line View overwrote ProTracker's own Copper list after its VBlank server
had installed it. This was a guest screen-ordering issue, not a chipset patch.
The new services use no title-specific branch, host OS shim or modified disk.

The optional disk-loaded V40 desktop Intuition provider is separate. Its old
height restriction was not removed as a substitute for a Classic ROM service.
Full window/IDCMP/desktop APIs and timer.device are outside this bounded fix.

## Protocol and validation

The pair uses PAL OCS 8371/8362, MC68000 interpreter, 512 KiB Chip + 512 KiB slow
RAM, no Fast RAM/RTC/host directories, one protected DD DF0, and the original
PT23F.ADF SHA-256
`38898d61f5b96971be4946761ac3101181d01e40c33512453d39eb76dca60a3a`.
The Kickstart 1.3 reference remains
`ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53`.

The new `utilities-protracker-editor-v2.json` delays keyboard actions until
field 9,000, after both ROMs reach the editor. It performs the same note-entry,
sequencer play and stop actions as v1, then moves the mouse and returns it.
Each final run executes 11,000 fields with snapshots every 100 fields. The old
7,200-field results are retained as historical observations, not relabelled.

## Completed result

| Development ROM property | Result |
| --- | --- |
| File | `artifacts/protracker-fix-2026-10-28/rom-qualified-scenario/CopperStart-DiskBoot.rom` |
| Bytes / checksum | 524,288 / `$FFFFFFFF` |
| SHA-256 | `f5cebe65428c1c47df5d0954df52ae70fae768f83590b96ae967675c93e6d3c0` |
| Compiled root | 492,334 bytes; no managed allocation sites/runtime helpers/external native targets |
| Graphics / placement | 26,207 bytes / `$FF9800` |
| Space before graphics | 5,322 bytes after the root's eight-byte prefix |
| App pin | Unchanged; this path is a development scenario candidate |

Both paired runs complete 11,000 fields with no engine unsupported stop and
unchanged protected media. CopperStart reaches the reviewed editor by field
8,000. In the final guest API trace, Intuition opens at field 7,370, OpenScreen
returns a screen at 7,371, and CIA-B resource/input.device open successfully at
7,801. The two optional timer.device opens still fail; that service is unavailable.

| Observation | Kickstart 1.3 and corrected CopperStart |
| --- | --- |
| Pattern before input, field 8,900 | Empty |
| Pattern after entry, field 9,200 | Complete 1,024-byte first pattern matches; channel-one rows 0/1/2 are `01AC1000`, `017D1000`, `01531000` |
| Sequencer, fields 9,400 and 9,500 | `RunMode = patp`; reviewed pattern cursor advances |
| After stop, field 9,700 | `RunMode = 0` |
| Mouse outward, field 10,100 | ProTracker coordinates `(11, 11)` |
| Mouse return, fields 10,500 and 11,000 | Coordinates `(0, 0)` |
| Pointer | Visible in the corrected ROM; safe sprite initialization enables native sprite DMA |

The pattern, run mode and mouse assertions match across the selected fields.
Reviewed editor/notes/sequencer/pointer images support those assertions. Complete
framebuffer equality is not claimed: free-memory labels and clock phase differ.
The final diagnostic API probe's complete Chip and slow RAM at field 8,000
also match the ordinary production runner byte-for-byte.

The corrected ROM also completes the existing 7,200-field Test Kit audio
scenario. All six selected complete framebuffers (3,000, 3,100, 3,800, 4,000,
4,200, 7,200) match the retained native control byte-for-byte, with the same
frozen engine/CPU binaries and unchanged disk/script. The selected PCM retains
stereo → left-only → silence → restored stereo. No cross-ROM PCM golden is
substituted.

Focused tests execute emitted MC68000 code and synthetic boot disks through
the engine. They cover five heights, screen/input lifecycles and CIA-B ownership
with real timer interrupts.
All eight focused cases pass against the completed ROM. Three standalone
graphics identity cases pass with the intentionally versioned short-screen
bytes; the source-side record retains the previous identities and rationale.
The screen test checks that allocated display resources are fully reclaimed
and that an already-front operation preserves an external LoadView takeover.

The production CopperScreen Release build passes. The full CopperStart solution
and its ordinary native-test project are blocked by existing CopperOS Shell
compile errors (`LoadSegmentNative`, `UnloadSegmentNative`,
`UnloadDosProcessSegment`). A no-dependency build additionally exposes an
unavailable `NativeInputPolling` type in another test. The affected tests were
therefore built in a local isolated harness with the same source files and
repository dependencies. This is explicit focused coverage, not a full-suite
pass. A broader compact-graphics run also finds an unrelated MaxPen expectation
failure at pen zero; the five changed height cases pass independently.
Four extended/monitor-mutation image identity checks also fail against their
existing expected hashes. Those paths and expectations were left untouched;
the three changed standalone identity cases pass. These wider failures are
recorded as unresolved coverage, not a full graphics-suite pass.

To rebuild into a fresh directory:

```powershell
../CopperStart/build/Build-DiskBootRom.ps1 -FileSystemProfile Classic `
  -ArtifactRoot C:/PrivateEvidence/protracker-rom
```

Replay with the runner's `--rom`, `--disk`, `--frames 11000`, bounded machine
arguments above, `--boot-probe-extended --boot-probe-interval 100`, and
`--input-script CopperMod.Amiga.Lightweight.Runner/Workloads/utilities-protracker-editor-v2.json`.

Raw build/replay/test receipts are ignored local evidence under
`artifacts/protracker-fix-2026-10-28/` and adjacent `artifacts/protracker-*` logs.
The source-side contract is recorded in
`CopperStart/Docs/NativeClassicScreenInput20261028.md`.

The [machine-readable receipt](PROTRACKER_FIX_2026-10-28.json) records the final
identities and checked scope. Final raw runs are `completed-actions/`,
`native-final-actions/`, `completed-api/` and `testkit-completed/`. Earlier
directories record intermediate diagnoses and are not the completed candidate.

This does not qualify sample/module audio, disk writes, quit/desktop restoration
or full ProTracker GUI compatibility. Elapsed time/free-memory labels and
timing-dependent cursors are not framebuffer goldens.
No performance acceptance measurement, engine/CPU source change, media commit or
package release is part of this fix.
