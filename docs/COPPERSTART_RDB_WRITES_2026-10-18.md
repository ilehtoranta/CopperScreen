# Multiple RDB FFS partition writes — 2026-10-18

**PASS:** six full-ROM cases across eleven boots, with no failures or skips.
The Release production solution builds with zero warnings and errors.
The [qualification receipt](COPPERSTART_RDB_WRITES_2026-10-18.json) records
the cases, binary hashes and retained evidence. No production change was
needed for this continuation.

Both the application runtime and command-line runner create, patch, flush and
read files on two independently mounted FFS partitions. Their live backing
images reproduce the producer's qualified saved image byte for byte, before
host save or disposal. Reset, session replacement, process restart and fresh
protected boots preserve the saved bytes. Reversing the RDB boot priorities
selects the other partition as `SYS:` in both front ends.

## Inputs and partition layout

The firmware is the current `diskboot-2026-10-18-td64.1` pin: 524288 bytes,
SHA-256 `199685F8AA9BEBE7D1879F12E5C7D37A88B85AB7752FCA48AF30C9CEDAC97AD0`.
The machine uses the normal PAL OCS 68000 configuration, 512 KiB Chip and
512 KiB slow RAM, with no floppy medium, Fast RAM, RTC or host DOS shims.

The owned producer fixture `rdb-both-written.hdf` has SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.
The test derives a pristine private image by unlinking `Data/Saved` on both
volumes, freeing and clearing each file's six blocks, and fixing directory
and bitmap checksums. It preserves the original MC68000 RdbProbe HUNK and
startup files. The resulting pristine image has SHA-256
`425D4D57271143F1B438014003E25C07BE0B95A2B9F8611C7F295C9B93F286DE`.
The supplied fixture and ROM are never modified or committed.

| Partition | Start byte | Length | Default boot priority | SYS identity |
| --- | ---: | ---: | ---: | ---: |
| DH0 | 131072 | 1802240 | -128 (data mount) | 1 |
| DH1 | 1933312 | 1802240 | +5 | 2 |

Both use DOS1/FFS, 512-byte blocks, two heads and 32 blocks per track.
The image supplies all partition environments; the front ends supply no
explicit partition geometry. The additional priority cases swap +5 and -128
in PART blocks 2 and 3 and repair their RDB checksums. They require identity 1
instead of identity 2, with both volumes still writable and separately mounted.
The resulting saved image has SHA-256
`10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617`.
Broader signed priority, ties and boot-fallback coverage remains in the
producer's separate native RDB qualification.

## Native execution and observations

On each partition the original guest command creates `Data/Saved`, writes
2049 bytes, seeks to byte 509, overwrites 32 bytes across a sector boundary,
calls `Flush(fh)`, closes, reopens, reads all bytes and checks EOF. It then
uses `DeviceProc` and `DoPkt(ACTION_FLUSH)` for each filesystem. On later boots
the command takes its existing-file readback branch for both files.

RAM snapshots check the boot volume's identity, both branch receipts, both
2049-byte payloads, distinct handler ports and I/O requests, successful flush
replies and completed `CMD_UPDATE` requests with zero error and actual length.
Private ROM offsets are diagnostics for this pinned image, not ABI validation
requirements. Inspection does not perform guest bus reads or write guest RAM.

Independent host decoding checks both FFS files, all payload bytes, six unique
allocated blocks per file, free-space accounting and root, directory, bitmap
and file-header checksums. Every unrelated block within either partition and
every byte outside both partitions must remain unchanged.

| Full-ROM case | Boots | Required observations |
| --- | ---: | --- |
| App writes, save, reset, replacement, fresh boot | 4 | Persisted writable profile/last machine; both files present before host save; host save advances no guest cycles; three unchanged readbacks |
| App reversed priorities | 1 | DH0 becomes SYS; both files and completed guest flushes before host save |
| App protected image | 1 | First mutation returns error 214; backing unchanged before and after host save |
| Runner writes, restart, fresh boot | 3 | Live pre-disposal image snapshot; unchanged writable restart and fresh protected readback |
| Runner reversed priorities | 1 | DH0 becomes SYS; both files present in live pre-disposal snapshot |
| Runner protected image | 1 | Error 214; live snapshot and image after exit remain pristine |

The app tests use the normal runtime, presentation consumer, settings/profile
and last-machine persistence, `SavePersistentStorageAsync`, reset and shared
backing lease for session replacement. Execution is paused before observation.
Reset retains RAM, so both new readback phases and completed flushes are required
before accepting the receipt.

Runner tests launch actual processes with `--hdf-rdb-writable` or `--hdf-rdb`.
Write runs use 3000 fields and readbacks/protected controls use 2000 fields.
`--boot-probe-extended --boot-probe-storage` captures Chip/slow RAM and the
open backing file while the machine is alive, without flushing the source.
The live snapshot must already contain both saved files and match the image
after process exit. This prevents disposal from masking a missing guest flush.

## Reproduction and scope

```powershell
./scripts/test-copperstart-rdb-writes.ps1 `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -OutputDirectory artifacts/rdb-write-proof
```

The script checks both input hashes, builds isolated app and runner outputs,
requires all six native cases to pass without skips, and records the receipts.
An optional `-RunnerPath` selects a built runner. Direct test runs use
`COPPERSCREEN_COPPERSTART_ROM`, `COPPERSCREEN_COPPERSTART_RDB`,
`COPPERSCREEN_LIGHTWEIGHT_RUNNER` and optional `COPPERSCREEN_RDB_WRITE_OUTPUT`.
Missing native inputs mean unavailable coverage.

Evidence is retained under ignored `artifacts/rdb-frontend-writes-20261018/`.
These are source app/runtime and runner full-ROM/media replays: six cases,
six app boots and five runner boots. Host semantic and newly compiled MC68000
cases are zero in this continuation. Installed MSIX interaction, real media,
power-loss durability and performance/timing acceptance are separate coverage.
The [RDB configuration qualification](COPPERSTART_RDB_2026-10-18.md) and
[explicit-partition write qualification](COPPERSTART_HARDDISK_WRITES_2026-10-18.md)
remain separate records.

The subsequent [installed-app qualification](COPPERSTART_RDB_INSTALLED_2026-10-19.md)
adds seven signed-MSIX process phases across eight boots, with both SYS selections,
restart/fresh protected readback and a saved-profile duplication correction.
