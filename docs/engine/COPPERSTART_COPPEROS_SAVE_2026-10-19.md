# Native CopperOS Save, 2026-10-19

The [native Editor](../../../CopperOS/docs/NativeDesktopSave.md) now implements
Save, Save As, overwrite confirmation and Save/Discard/Cancel in the existing
Desktop process and its single IDCMP drain/reply/Wait loop. All storage operations
run through guest DOS on native writable media. The document remains capped at
16 KiB. Failed saves preserve its edited bytes, cursor and dirty state.

Replacement reserves a fresh same-directory recovery drawer, writes and closes
`New`, reads its bytes back, preserves destination comment/protection, and records
`Target`. It verifies the retained source snapshot, moves the original to
`Original`, verifies it again, then promotes `New`. Failed promotion attempts to
restore the original. Failed restoration retains both copies and displays the
recovery drawer. Shared source changes and unexpected destination collisions
refuse replacement. Cleanup and post-save guard failures report a committed save
with a warning. A dedicated recovery browser and power-loss consistency remain
separate work; this multi-operation protocol is not an atomic transaction.

The development ROM is exactly 524,288 bytes, SHA-256
`07646C8512F129E6ABF5FE38C6D384930CC318C395D7668B383C3D6F49283E0C`.
It derives from the Editor ROM by removing the incorrect private access-mode
guard that rejected Write through MODE_OLDFILE handles. The frozen DOS source
baseline has zero resolved-code differences; only NativeAmigaFileSystem.Dispatch
differs after the correction. All other ROM producer inputs and the GUI provider
remain pinned. Code is 466,416 bytes; the unchanged 31,906-byte graphics block
stays at 0xFF8000 with a 25,096-byte gap and checksum FFFFFFFF. The machine stays
PAL OCS MC68000 with 524,288 Chip + 524,288 Slow bytes and zero Fast RAM.
CopperScreen's shipping firmware/media admission is unchanged.

The combined record contains seven successful checks, zero skips: two freestanding builds,
compiled buffer/glyph and host-glyph checks, the prior Editor native journey,
missing-GUI rollback and writable save/recovery. Five unchanged regression facts
are retained from `qualified-v7`; the save-only rerun `qualified-v9` passes two
facts. The original default attempt remains recorded as six passes and one
timeout: its process-exit wait allowed 12,000 fields during a still-running
close-and-save. The process-exit wait changes to the existing 180,000-field
functional hang bound. A second host helper now accepts completed clean process
exit instead of waiting for a deleted window to become idle; its earlier run
`qualified-v8` remains explicitly aborted, after successful restart recovery and
Save As diagnostics. App and other test sources remain byte-identical, and
both native journeys run the exact same Desktop module. No latency threshold is
weakened or admitted by this change. The new ROM separately passes
one native reset/boot check. Coverage includes empty/16 KiB save and reopen,
Save As cancel/confirm, protected files, metadata preservation, external shared
writes, destination races, all five Close boundaries, short/failed/partial
writes, allocation/name limits, both renames and failed restoration, read-only
and exhausted media, and a failed Save from the common close decision.

Both native Editor and Save journeys complete 11 lifetimes, including seven
startup failure cases apiece. Their stable free-after-stop baselines are
462,840 and 456,568 bytes respectively.
They release task, port, screen, input and provider pins. The missing-GUI journey
checks three failures. Recovery is reopened after a process restart. Exported
native MFM media is then booted with a fresh ROM instance and three files are
read back byte-for-byte, including the 16 KiB file. This is guest persistence
evidence; CopperScreen workspace checkpoint/update persistence is still pending.

The reproduced Desktop module is 35,472 bytes, SHA-256
`5B2BD5D1E965E2960341D61F6907B9CBD20F3CE3494CB99A30EE9073506D84AE`; its launcher is 652 bytes.
The unchanged GUI provider is 257,312 bytes, SHA-256
`5900735263A90AC53626E6103FD6B232329667AE03D78105BC74D88E2BC085FD`. Explicit owned-buffer bounds are 85,800 bytes
while saving and 102,996 while opening with a retained failed-save workspace.
These exclude OS/loader/display allocations and fragmentation. The lowest
Desktop Exec.Wait samples are 267,832
bytes for Editor and 242,496 for Save;
these are not continuous peak or largest-free-block measurements.

The Save journey executes 703,868 fields and the fresh-boot read
4,526. The replay keeps its original source hash; a subsequent test-comment correction
clarifies that the exercised shortcut is Ctrl-C, without changing executable code.
Native floppy saves are visibly slow; these counts establish
functional coverage, not response-time or throughput acceptance. Both production
Release builds pass. CopperScreen reports zero warnings; the isolated final
CopperStart build reports 22 warnings (dependency audits and compiler nullability)
and zero errors. The
earlier default-output build failed because unrelated active tests held outputs;
that failed log remains retained separately from the successful isolated build.

The [machine-readable evidence](COPPERSTART_COPPEROS_SAVE_2026-10-19.json) binds
355 artifact hashes and 22 source hashes.
Its SHA-256 is `77471F9B78E3EF29EE30E44284BFE39FE89B831CAC45F3D35CDB8E8357FB7C93`. Final save replay:
`CopperStart/artifacts/copperos-save-20261019/qualified-v9`; retained regression:
`qualified-v7`; exact bundle:
`bundle-v1`; ROM build: `rom-write-v4`. Earlier failed/aborted attempts retain
their original status. The [producer guide](../../../CopperStart/Docs/NativeCopperOsSave.md)
documents exact inputs and reproduction.

Next work is recovery UI, save/directory/startup/paint responsiveness, bounded
Files mutations/paging, largest free blocks and continuous peaks, sustained and
100-cycle stress, child launch/return, and separate shipping ROM/OS and workspace
restart/update admission. The 512 KiB ROM / 1 MiB A500 profile remains mandatory.

## Subsequent recovery and busy qualification

The [2026-10-20 follow-up](COPPERSTART_COPPEROS_RECOVERY_2026-10-20.md) qualifies the changed Desktop module
with cooperative busy handling, read-only recovery inspection and unsaved copies.
The ROM/GUI remain pinned; the Save evidence above retains its original identities
and outcomes.
