# Native CopperOS busy feedback and recovery qualification — 2026-10-20

The development desktop now paints busy feedback and handles refresh/close/input
between DOS calls, and adds read-only recovery inspection with unsaved text copies.
All eight selected facts pass with zero skips on the bounded native A500 profile.
This slice changes no shipping firmware or workspace-admission pin.

The [evidence JSON](COPPERSTART_COPPEROS_RECOVERY_2026-10-20.json) binds 612 artifact hashes
and 23 current source hashes. Its SHA-256 is `829006C3FD1D566589845DAF9A23592BB268599114921361A911C96EAE417D2B`.
See the [implementation guide](../../../CopperOS/docs/NativeDesktopRecovery.md) and
[producer/reproduction guide](../../../CopperStart/Docs/NativeCopperOsRecovery.md).

## Exact development pair

| Input | Identity |
| --- | --- |
| ROM | 524,288 bytes; `07646C8512F129E6ABF5FE38C6D384930CC318C395D7668B383C3D6F49283E0C` |
| GUI provider | 257,312 bytes; `5900735263A90AC53626E6103FD6B232329667AE03D78105BC74D88E2BC085FD`; TextInputEntry 247686 |
| Desktop module | 41,500 bytes; `5368BFDE4DC393B5E1899A7344C78BCD8FE07FBB27B8862CFA583DDC40A8A258` |
| Desktop bootstrap | 652 bytes; `E7321120811F127E7B83F440E7C9C34DABCCB1209A0FF595284BD6F960595A2A` |
| Machine | PAL OCS MC68000; 524,288 Chip + 524,288 Slow; zero Fast; one native DF0 |

The ROM and GUI are unchanged from the
[Save qualification](COPPERSTART_COPPEROS_SAVE_2026-10-19.md).
The 512 KiB ROM layout/checksum and its producer audit remain that record's evidence;
no ROM was rebuilt or enlarged for this application change. The exact local bundle
is `CopperStart/artifacts/copperos-recovery-20261019/bundle-v1` and the final replay
is `CopperStart/artifacts/copperos-recovery-20261019/qualified-v5`.

## Behavior and native coverage

IDCMP fields are copied and the message replied before invoking an action. One
Desktop task/port/Wait loop remains. Bounded checkpoints repaint refresh damage
and busy status, ignore/reply queued edits, and defer close until disk work finishes.
The native probe observed 49 busy messages and 3
busy refreshes across all four operation types. Three busy-footer bitplane checks
pass. The recovery journey also checks physical queued input, a task break during
a scan, a close during Save, an exact saved copy after process restart, and unchanged
archive bytes.

Files V or its recovery footer opens a selected CopperSave drawer; Editor Ctrl-R
or its footer opens the last retained save drawer. The UI shows New/Original and a
bounded optional destination hint, warns that copies can be incomplete, and opens
text as a modified unnamed Recovered document. Save requires a destination choice.
Inspection never deletes, restores or overwrites archives. Native checks cover
valid/invalid/missing hints and candidates, binary refusal, common Save/Discard/Cancel,
failed-save direct entry, and allocation/Lock/Examine/Name rollback. Read-only
inspection leaves the native media clean.

The default eight-fact run also covers freestanding compilation, host/compiled
buffer and glyph checks, missing-GUI startup, the full native Editor journey and
the previous writable-save failure matrix. Save retains source-change detection,
non-truncating replacement, short/failed/partial writes, Close and rename failures,
failed restoration, metadata, read-only/full media, process restart and three
byte-exact reads after MFM export and a fresh ROM boot. All three native journeys
run the same Desktop module. The full result includes 33 completed lifetimes,
with four normal and seven startup-failure lifetimes per journey.

| Native journey | Guest fields | Free after every stop | Lowest observed free at Wait | Lifetimes |
| --- | ---: | ---: | ---: | ---: |
| Editor | 95520 | 456936 | 261928 | 11 |
| Save | 723012 | 450216 | 236144 | 11 |
| Recovery | 130817 | 450216 | 252088 | 11 |

Save's fresh protected boot completes in 4532 additional fields and
checks three exact files. These are functional observations, not latency or
throughput admission. The retained media identity is `A9518DE9BF2226C5A0D4909034D9CB6811F8C84E613E22781968647933BD6170`.

## Bounds and remaining work

Root state stays 256 bytes, the stack 32,768 and cleanup reserve 8,192. A recovery
workspace adds 832 bytes, or 1,664 while replacing an inspection; busy handling
adds no heap workspace. Editor component buffer bounds remain 85,800 for Save and
102,996 for an open with a failed workspace. Including one retained recovery owner
makes these 86,632 and 103,828 bytes. These figures exclude loader/handler, GUI/OS
ownership and fragmentation. Lowest observed free at Wait is not a continuous
whole-machine peak or largest-free-block measurement.

Individual synchronous DOS calls still block the Desktop task. The qualified
ordinal directory iterator rescans previous entries, and the Files cache remains
32 entries. Next prioritize native iterator/cache and save I/O optimization, then
measure operation latency and complete memory/largest-block records. Bounded Files
mutation/paging, Shell/Demo launch-return, 100-cycle/sustained stress, shipping
ROM/OS pair and workspace admission, and the itch.io ZIP journey remain separate.
Recovery inspection does not establish power-loss-safe or atomic replacement.

## Build and retained attempts

The frozen replay build passes with zero warnings/errors. CopperScreen Release
passes with zero warnings/errors. CopperStart Release with isolated outputs passes
with 20 existing dependency-audit warnings and zero errors. Host/compiled, full
ROM/media execution, and throughput/latency results remain separate.

The final default run is eight passes and zero skips. Earlier v1/v4 compile failures
and the explicitly aborted v3 run remain retained and are not successful native
suite evidence. The v2 recovery-only run passes on an older UI prototype and is
not counted toward this final qualification. The evidence binds their logs and
outcomes rather than relabelling them. All final app source matches the frozen
replay byte-for-byte. The old 448-byte GUI fixture is retained; only its Files
footer hint is adapted.

The subsequent [native I/O qualification](COPPERSTART_COPPEROS_IO_2026-10-20.md)
adds private directory cursors and reduces bitmap/copy work in a new development
ROM, retaining Save/recovery coverage within the same limits. This earlier
record's outcomes, inputs and measurements remain unchanged.
