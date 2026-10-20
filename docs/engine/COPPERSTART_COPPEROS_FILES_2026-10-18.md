# Native CopperOS Files — 2026-10-18

The native [Desktop/Home](COPPERSTART_COPPEROS_HOME_2026-10-18.md) now has a
read-only Files view inside its existing process, window and IDCMP loop.
[Implementation and controls](../../../CopperOS/docs/NativeDesktopFiles.md) and
[producer evidence/reproduction](../../../CopperStart/Docs/NativeCopperOsFiles.md)
describe the bounded directory transaction and remaining Editor work.

Files uses public DOS locks and directory APIs on the original guest filesystem.
It supports path/up/root/reload navigation, eight visible rows, keyboard and mouse
selection, page scrolling, empty/nested directories and expose refresh. A fixed
32-entry cache has an explicit truncation notice. Allocation, Lock, Examine,
NameFromLock, ParentDir, mid-ExNext and long-path failures retain the old directory,
entries, selection and viewport. Header actions work while editing a path, and
queued navigation repeats cannot silently replace state after a failure.

The cache is 4,384 bytes; candidate navigation temporarily owns two caches
(8,768 bytes) and two directory locks. The existing 32 KiB stack, 256-byte root
state and 8 KiB cleanup reserve remain. The command loads the large GUI HUNK
before Desktop to avoid a measured contiguous loader allocation failure.

The ROM is unchanged at 524,288 bytes, SHA-256
`492EF10926F73EDCF3EC7CAFC331E54A84C418924F765BA93AA4D504B20990CA`.
All replay execution uses MC68000 PAL A500 with 512 KiB Chip + 512 KiB Slow RAM
and no Fast RAM. The exact frozen GUI provider is unchanged. The development
bundle has a 13,316-byte Desktop module and a 652-byte command.

`CopperStart/artifacts/copperos-files-20261018/qualified-v12` passes **3/3 checks,
zero skips**: freestanding compilation and two separate real ROM/media journeys.
The main journey covers four interactive lifetimes and seven startup failures;
all 11 return to exactly 489,576 free bytes and release their app task, port,
screen, input context and Intuition pin. The separate cold missing-GUI replay
repeats failure three times at the same free-heap baseline. Twelve Files scenes
and twelve Home scenes pass native bitplane comparisons; 36 PNGs retain those
scenes and console returns. Both production Release builds pass without warnings
or errors.

The [machine-readable evidence](COPPERSTART_COPPEROS_FILES_2026-10-18.json) binds
96 artifact hashes and 13 source hashes to the ROM, producer inputs, frozen
application, probe, media replay, bitplanes and reproduced module bundle. Its
SHA-256 is `A862A36EF93FBA67C6DEF27136E5AC3CEAB3AE2B1466E24D58D0946E711F1AA9`.
The bundle is `CopperStart/artifacts/copperos-files-20261018/bundle-v3`; earlier
failed/aborted runs remain separate and are not counted as successful coverage.

The selected filesystem rescans preceding entries during ExNext. This measured
cost motivated the first 32-entry limit; directory responsiveness still needs
optimization and native qualification before expanding it. The minimum sampled
free bytes at Desktop's public Exec.Wait vector is 331,176, which excludes
unsampled loader/I/O/drawing transients and largest-block measurements. The
87,085-field journey is functional evidence, not latency or host-throughput
acceptance.

Editor still needs a case-preserving font on this exact profile, a capped 16 KiB
text buffer, cursor/viewport/editing, transactional Files-to-Editor open, dirty
state and recoverable temporary/backup save replacement on writable DOS media.
Then join save, Files reload and byte-exact reopen. Volume discovery, paging
beyond the cache and file mutation remain future Files work. Shell/Demo return,
stress, shipping HDF admission and workspace/update persistence retain their
separate release gates. This work does not update CopperScreen's shipping pin.
