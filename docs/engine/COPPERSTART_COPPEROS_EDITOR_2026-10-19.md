# Native CopperOS Editor, 2026-10-19

The [native Editor](../../../CopperOS/docs/NativeDesktopEditor.md) now joins Home
and Files in the existing Desktop process, window and IDCMP drain/reply/Wait loop.
It implements case-preserving ASCII/Latin-1 letter rendering, a 16 KiB byte buffer,
cursor/viewport movement, insertion/deletion, tabs, CR/LF/CRLF preservation,
New/Open and dirty-document Discard/Cancel decisions. Files open is transactional:
candidate allocation, directory duplication, read, close and validation complete
before replacing the old document. Files retains its directory, selection and
scrolling on failure, and the old Editor status is preserved.

The desktop development ROM remains exactly 524,288 bytes, SHA-256
`C00836CDB82DCF1D48A9F85E5334FBFF87002DD85E85F6560B9844415E850247`.
Its code is 466,442 bytes, a 512-byte increase over the self-reaping Home/Files
profile. The unchanged 31,906-byte graphics block stays at `0xFF8000`, leaving
25,070 bytes of gap. Its end-around-carry checksum is `FFFFFFFF`.

The GUI HUNK is 257,312 bytes, SHA-256
`5900735263A90AC53626E6103FD6B232329667AE03D78105BC74D88E2BC085FD`,
with TextInputEntry 247,686. The machine remains MC68000 PAL OCS, 524,288 bytes
Chip RAM, 524,288 bytes Slow RAM and zero Fast RAM. Cold reset is required for
this ROM/provider pair. CopperScreen's shipping firmware/media pin is unchanged.

The ROM receives and acknowledges keyboard bytes in VBlank. GUI capture has one
IRQ producer, including the console-to-GUI handoff; cooked editing remains in
task context. Full raw-FIFO recovery clears uncertain held state without retyping
the dropped batch. The provider derives modifiers from transitions in order,
preserving Shift/Ctrl text when later releases arrive during a slow paint.
No IRQ schedules tasks or edits Exec task lists. The
[producer record](../../../CopperStart/Docs/NativeCopperOsEditor.md) documents
the narrow source overlays and reproducible build scripts.

The retained Editor result passes five checks with zero skips, including two
real ROM/media journeys and compiled MC68000 buffer/glyph checks. It covers
queued Shift-A/Ctrl-A, IRQ-only FIFO overflow, expose refresh, short reads,
16 KiB boundary editing, CRLF behavior, scrolling, caret placement and dirty
document decisions. Thirty-three Editor scenes and seven Home scenes pass
independent native client-bitplane comparisons. Six open-failure cases plus two
first-open allocation/font failures preserve prior ownership. All 11 main
lifetimes return to exactly 471,792 free bytes and release the app task, screen,
port, input context and Intuition pin. The cold missing-GUI journey repeats three
failures at one baseline.

The separate input/text regression passes six checks; joined refresh/input passes
three checks and repeats three lifetimes at each display width. Native reset/
disk boot and provider compilation each pass one separate check. Both production
Release solution builds pass with zero warnings and errors. No failed or aborted
attempt is counted as successful coverage.

The reproduced Desktop module is 26,416 bytes, SHA-256
`615EA4DF29877151412A6300457523DE12BE9EA78E1540C93A353DF021CADAAB`;
the command stays 652 bytes. One document allocates 16,936 bytes, including its
header, scratch and oversize probe. Replacement temporarily owns two documents
(33,872 bytes). The shared font uses 2,816 bytes. Files retains its 4,384-byte,
32-entry cache and the existing stack/state/cleanup reserve remain 32,768/256/
8,192 bytes.

The [machine-readable evidence](COPPERSTART_COPPEROS_EDITOR_2026-10-19.json)
binds 282 artifact hashes and 25 source hashes. Its SHA-256 is
`00A62522DF0718A951743CBBBEADE5A86F858B212DFC0BE3E5234B2D781F902D`.
The final replay is `CopperStart/artifacts/copperos-editor-20261019/qualified-v11`
and the reproduced bundle is `bundle-v1`. Code audits match the frozen baselines
before applying the console overlay and the provider's two modifier methods.

The 90,133-field main journey is functional evidence. The lowest free-memory
sample at Desktop Exec.Wait is 293,544 bytes; it excludes unsampled loader/I/O/
drawing transients and largest-block measurements. Reset-lived probe veneers
and a 4,096-byte event trace are included in the stable baseline. Complete
startup/directory/paint latency and peak-memory acceptance remain separate.

Safe Save/Save As is next. Ctrl-S currently keeps edits in memory and states that
saving is pending. Qualify source-change/collision handling, unique temporary/
backup replacement, short writes, failed close/rename, full/read-only media and
interrupted replacement on the actual writable DOS filesystem. Then join Files
reload and byte-exact reopen, including host persistence. Stress, child launch/
return, shipping HDF admission and workspace/update persistence remain release
gates.

## Subsequent Save qualification

The [Save development record](COPPERSTART_COPPEROS_SAVE_2026-10-19.md) supersedes
the next-step paragraph above. The Editor identities and results in this record
remain historical evidence for its original profile.
