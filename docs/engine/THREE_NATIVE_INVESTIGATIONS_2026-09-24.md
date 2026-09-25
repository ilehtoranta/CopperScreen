# Alien Breed SE '92, Desert Dream and Desert Strike investigation

The three retained failures reproduce on current source `6de958a` with stable
Copper68k 1.4.1. This investigation localizes each failure and exercises isolated
counterfactuals. **Production engine and CPU source are unchanged.**

Subsequent work implements and validates the
[Alien Breed CPU byte-alignment correction](DISK_BYTE_WORDSYNC_2026-09-24.md).
The findings and diagnostic-only scope below describe this original investigation.

A later [WinUAE comparison and drive end-stop correction](DESERT_STRIKE_END_STOP_2026-09-24.md)
supersedes Desert Strike's media-boundary disposition below: the same stale cache
occurs in WinUAE, which continues past the over-seek. Lightweight's artificial
execution stop prevented loader recovery; no guest-cache repair is needed.

The subsequent [Desert Dream live area-pointer correction](DESERT_DREAM_LIVE_POINTERS_2026-09-25.md)
repairs the interrupt-vector corruption described below without changing BLTSIZE
restart policy. This original report retains its investigation-only scope.

| Title | Finding | Observed limit / next work |
| --- | --- | --- |
| Alien Breed SE '92 | WORDSYNC does not realign the CPU byte reader. A diagnostic alignment correction alone passes the black boot and reaches the title and disk-two prompt. | Promote only after focused byte/sync transition checks, affected native retention and a separately measured candidate. |
| Desert Dream | The old Copper list reprograms an active CPU-started blit. The model defers BLTSIZE, loses the programmed destination to the active blit's pointer updates, then clears low RAM and the level-6 vector. | Establish live-register/restart hardware behavior and whether earlier execution timing should avoid this overlap. No guessed restart policy is promoted. |
| Desert Strike | Two guest loaders maintain separate track caches. The briefing loader moves the head; the returning loader uses its stale cache and over-seeks. | Correcting just the stale guest cache diagnostically removes the stop and reaches the disk-three prompt. A coherent media-set replay is needed; disk three is unavailable. |

## Build, media and evidence

Profile: PAL OCS A500, 68000, Kickstart 1.3, 512 KiB chip and 512 KiB slow RAM,
one connected DF0, read-only media, full 908-pixel output. The exact supplied
archives/entries are those in the
[original manifest](NATIVE_CORPUS_2026-09-21_BATCH2_MEDIA.json).
All four source archives, their entries, ROM and extracted Desert Strike disk two
were hash-checked. No source media was modified or additional game media downloaded.

`dotnet build CopperScreen.slnx -c Release` passed with zero warnings/errors.
The fresh production binaries were frozen under
`artifacts/three-native-investigation/runner/`:

- Engine SHA256: `4333ABB681731F8D50A80B7670E9880986212CEE9DEC41B5CF884FAA7D57B435`.
- Copper68k SHA256: `F82CA4A5FD953B0BEE31EE360212070548DB0DAA80874EEEC36DB0EFD1F1AFE1`.

The [machine-readable record](THREE_NATIVE_INVESTIGATIONS_2026-09-24.json)
identifies binaries, media, snapshots and logs. Local evidence remains under
`artifacts/three-native-investigation/`; original September 21 captures remain intact.
The bounded probe uses the existing runner friend boundary and pinned binaries.
It records chip/slow RAM, CPU state, images, instruction rings and selected bus
events. Trace windows use scalar instruction execution with the machine's ordinary
interrupt dispatch and hardware advancement. Separate normal-frame replays retain
the failures, so the diagnosis does not depend on the trace stepping alone.

Diagnostic engine copies add bounded logging outside production source. The
alignment copy additionally changes one behavior described below. These runs
are correctness investigations, sometimes concurrent, with capture allocations;
their wall-clock speed is not a throughput result. No full regression suite or
performance acceptance is claimed for a production correction.

## Alien Breed SE '92: sync polling and CPU byte alignment

`alien-current` repeats the original black state at field 8,000, PC `$07F6D6`,
DSKLEN `$8000`, ADKCON `$1500`, cylinder 0/head 1. The loop at `$07F6D0`
tests WORDEQUAL in the high byte of DSKBYTR. DSKSYNC is `$8911`.

The decoded 100,496-bit track contains `$8911` at bit 46,110, followed by
`$2A91`. This is not an absent sync word or missing disk-two condition. A
receiver-only diagnostic over ten revolutions observes ten recovered `$8911`
matches. The normal CPU polling trace repeatedly misses the short equality
window. For example, reads at cycles 142,341,888 and 142,341,914 bracket the
match; the latter already sees shift `$1222`. This localizes the initial wait
without establishing a physical phase correction.

An explicitly diagnostic eight-CPU-cycle shift of spindle phase lets the polling
loop return. This **does not fix the boot**: the first two CPU-read bytes are
`$44AA`. The decrypted guest comparison at `$07D69E` is `CMP.W #$2A91,(A0)`,
with A0 `$07D9D4`; it rejects `$44AA` and retries. A six-cycle CPU-delay experiment
also fails to establish a boot and is retained separately.

`LightweightDiskSerial.Receive` has its own eight-bit `_byteBits` counter.
`CompareSync` latches the sync interrupt and the DMA path handles its own word
alignment, but the CPU byte counter remains at its old phase. In the isolated
candidate, a newly detected equality with ADKCON.WORDSYNC enabled resets
`_byteBits` to zero. It does not change the decoded media, CPU package, polling
instruction, spindle speed or sync value.

**That alignment change alone, without spindle or CPU-delay edits, reaches:**

- Field 2,000: Alien Breed II advertisement.
- Field 4,000: Alien Breed Special Edition '92 title.
- Field 8,000: visible request to insert disk two and press fire.

Evidence: `alien-align-only`; the independently labeled `alien-align-phase8`
also reaches the prompt but is not needed for the result. `alien-signature`
retains the rejected `$44AA` measurements from the original byte-reader behavior.
The scalar-from-boot control (`alien-align-scalar`) matches all eight retained
state/image/chip-RAM/slow-RAM checkpoints byte-for-byte (32 files).
This establishes a native reproducer and a successful diagnostic remedy for the
black boot, not complete protection, disk-two or gameplay compatibility.

As supporting implementation evidence, WinUAE's
[`wordsync_detected` and DSKBYTR path](https://raw.githubusercontent.com/tonioni/WinUAE/master/disk.cpp)
realign the shared read-bit offset on WORDSYNC. That source was inspected on
2026-09-24; it is an emulator comparison, not a pinned physical timing oracle.
Commodore's [DSKBYTR register description](https://www.manualslib.com/manual/1053840/Commodore-Amiga.html?page=264)
describes transient word equality and read-cleared byte-ready status. Exact
coincident match/read/write behavior still needs discriminating coverage.

## Desert Dream: an overlapping Copper blit clears the interrupt vector

The first decisive corruption precedes the old field-19,380 screenshot. The
following sequence is retained in `dream-blit-origin.log`, with scalar context
in `dream-interrupt-trace/trace.txt`:

| Cycle | Actor / operation |
| ---: | --- |
| 2,748,577,546 | CPU starts a `$4028` clear at `$071000`. |
| 2,748,626,196 | CPU starts another `$4028` clear at `$076000`. |
| 2,748,681,378 | CPU starts a third `$4028` clear at `$07B000`, modulo zero. |
| 2,748,695,510–2,748,695,550 | Old Copper list writes destination `$062800`, control/modulo and BLTSIZE `$4014` while the third clear is active. |
| 2,748,730,468 | Engine starts the deferred `$4014` blit at **`$000000`**. |
| 2,748,730,722 | Blitter DMA clears the high word of vector `$78`. |
| 2,748,737,506 | Blitter DMA clears the old handler at `$0CD0`. |
| 2,748,748,992 | Level-6 interrupt dispatch transfers CPU `$04301E` to `$000000`. |

The CPU's three destination writes are correct. The third clear ends at the
512-KiB chip-address wrap. The old Copper list's late-frame WAIT is followed by
the additional clear at `$0429C4`–`$0429D8`; the replacement list does not contain
that clear. There is no disk DMA active at the corrupting writes.

`LightweightBlitter.OnRegisterWrite` currently queues only BLTSIZE when `_active`
is true. Other writes remain in the register bank while the running blit retains
private live pointers. Its following DMA updates overwrite the newly written
destination. At termination, the deferred start reads the now-wrapped zero
destination and clears low RAM. This explains the cleared vectors, interrupt to
zero and subsequent corrupted PC/stack without attributing them to CPU opcode
semantics or a failed disk-B load.

The trace identifies a concrete overlap and a model limitation. It does **not**
prove that simply restarting immediately, ignoring the second start, or retaining
a snapshot of all registers is correct OCS behavior. Further work must distinguish
active-blit pointer/control writes, BLTSIZE restart and retained bus outputs. Also
check the preceding CPU/blitter/Copper timing against an independent reference:
the original may rely on completing the CPU clears before this Copper sequence.
The [WHDLoad maintainer's record](https://whdload.de/demos/Kefrens_DesertDream.html)
notes timing and blitter-wait repairs, but does not establish this particular
A500 overlap's expected result.

## Desert Strike: separate guest track caches

The unchanged supplied combination is PDX-labeled disk one and unlabeled disk two.
The ordinary replay again stops during field 22,084; single-instruction tracing
captures the exact offending write at cycle 3,137,907,010, PC `$C1CA3E`.
That write requests an inward step with the head already on cylinder 79.

The earlier loader stores its track number at `$C1CAC6`. After reading tracks
0–11, that cache contains `$000B` (cylinder 5/head 1). The briefing code uses a
separate loader at `$01A856`, with its own cache at `$01A970`. It homes and moves
the head during fields 20,633–21,276, ending on cylinder 40/head 0. The first
cache stays `$000B` throughout those moves.

At the return to the slow-RAM loader, it requests track `$0083` (cylinder 65/head 1).
Its arithmetic is correct for its stale cache: `65 - 5 = 60` inward steps.
From the actual cylinder 40 this would reach cylinder 100, so the existing
79-cylinder guard stops it. Increasing drive travel to cylinder 83 would not
resolve that mismatch. The trace does not show a legitimate request for data
on a physical track beyond the standard disk.

`strike-cache-counterfactual` changes only the two guest cache bytes at field
22,000 to the actual track number `$0050`. It then completes 26,000 fields
without the unsupported seek and visibly requests **disk three**. This is a
diagnostic guest-memory edit, not an emulator repair or a passing unchanged-media
replay. Disk three is unavailable, and no gameplay completion is claimed.

The immediate failure is therefore guest loader-state desynchronization. The
mixed release labels and separate loader copies warrant a coherent-set comparison
before assigning the ultimate cause to media or changing drive mechanics. Keep
the original stop classified as unresolved at that boundary, with this narrower
cause recorded; do not add a title-specific cache repair to the engine.

## Reproduction and next steps

Use the manifest's exact ZIP entries and a fresh output directory with the normal
runner's `--rom`, `--disk`, `--wide-output` and `--boot-probe` options. Alien Breed
needs 8,000 fields, Desert Dream 19,400 or more, and Desert Strike the unchanged
[`corpus-desert-strike.json`](../../CopperScreen.Lightweight.Tests/Workloads/corpus-desert-strike.json)
with the existing extracted disk-two path. The unmodified runner fails as recorded.

The local diagnostic probe's positional arguments are title (`alien`, `dream`,
`strike`), output directory, scalar-trace start field and ending field. Optional
`scalar` disables batching from boot; `receiver` advances only hardware after the
native interval; `phase=8`, `delay=6` and `sync-cache` are explicitly diagnostic
state changes. The candidate alignment DLL is isolated in `diagnostic-align/`.
These are local investigation tools, not production APIs or acceptance harnesses.

Prioritize a properly tested WORDSYNC byte-reader correction, then the demonstrated
Desert Dream overlap with hardware-backed register/timing expectations. Keep Desert
Strike's media-set verification separate from drive-range implementation work.
