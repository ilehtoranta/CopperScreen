# CopperStart DOS large-file qualification — 2026-10-19

Accepted: 30 full-ROM cases and 52 reset-ROM boots passed without skips.
The source firmware pin, normal consumer build and embedded resource match
the validated ROM. Host semantics and compiled instruction results are recorded
separately below.

## Change and artifact

The native FFS handler now supports its DOS provider's existing `ACTION_SEEK64`
packet. File positions retain unsigned 32-bit FFS values while seek
displacements retain their signed ABI. The provider's classic fallback
zero-extends successful position words instead of treating bit 31 as a negative
file length. Full-ROM testing also found that the handler rejected writes
through a shared `MODE_OLDFILE` handle; the corrected handler retains real file
and device protection without interpreting sharing mode as write permission.

The long replay also exposed two avoidable traversal costs. Native block I/O
now copies complete aligned internal buffers with 32-bit transfers while
retaining general byte copies and odd guest payload buffers. FFS bitmap cache
hits retain the valid page span instead of reconstructing mount geometry for
each data-block check. The span is clamped at the volume end, preserving padding
and range rejection. Work-buffer addresses are computed once when creating the
private work record. Pointer-chain, checksum and allocation validation remain.

The consumer artifact is `diskboot-2026-10-19-dos-large-files.1`, a development
qualification based on producer commit
`b45c2e4e50860f87e81e21d2a6e5c2a08e2631ad` plus the recorded source snapshot.
The 524,288-byte ROM SHA-256 is
`4054184e5cbcd93511da0c8c86b963955e670c959343dddf31f2c324ea03a963`.
Its native core is 493,878 bytes; the 30,099-byte graphics component starts at
`$FF8A00`. Native static analysis reports no managed allocation sites,
framework features, runtime helpers, external native targets or exception regions.

The producer deliberately retains the previously qualified Classic snapshot
and adds the exact position helper, seek handler, provider fallback and
MODE_OLDFILE correction, the targeted traversal/copy optimizations, and the
shared buffered range-write path.
Concurrent DOS/Exec/SFS work is excluded from that
snapshot. The source hashes and producer inputs are retained in the
[qualification JSON](COPPERSTART_DOS_LARGE_FILES_2026-10-19.json).
TD64 remains enabled; no NSD changes or package release are part of this work.

## Full-ROM evidence

| Group | Passing cases | Reset-ROM boots |
| --- | ---: | ---: |
| Large FFS files on a 4.125 GiB partition | 2 | 4 |
| DD/HD OFS/FFS and RDB regressions | 22 | 27 |
| App/runner write, protection and TD64 regressions | 6 | 21 |
| Total | 30 | 52 |

The large-file and app/runner replays use PAL OCS, a 68000 interpreter,
512 KiB Chip RAM and 512 KiB slow RAM, with no Fast RAM. The RDB regressions
also use 512 KiB Chip plus 512 KiB slow RAM. Floppy regressions retain their
own profiles, including 512 KiB Chip plus 2 MiB Fast RAM for native write/export
checks and HD boot/read cases with 512 KiB Chip RAM only.
The large-file and app/runner cases use the current CopperScreen Lightweight
runtime. The DD/HD/RDB regression group uses the retained frozen replay runtime.
Their distinct binary hashes are recorded; they are not presented as one
identical engine build or memory profile.

The two large-file cases ran in independent processes, initially at Normal
priority. During host contention, those two processes alone were moved to
AboveNormal; their identities and scheduling changes are recorded in the JSON.
Each has its own real TRX result. They use the same frozen fixture and ROM.
The per-boot limit is 2,000,000 guest frames and 180 host minutes. The wall-time
allowance accommodates host load without changing the guest-work limit.
These are correctness checks, not host-throughput acceptance measurements.

The large-file fixture independently creates an ordinary DOS1/FFS volume with
512-byte blocks and fully allocated file-pointer chains. Host sparse storage
holds the zero payload economically; the guest sees a real block device and
receives no host filesystem replies or DOS services.

| Existing file length | Write position | Bytes | DOS calls |
| --- | --- | ---: | --- |
| `0x80000040` | `0x7FFFFFF0` | 32 | `Write` / `Read`, crossing 2 GiB |
| `0xFFFFFFFF` | `0xFFFFFFF0` | 15 | `FWrite` / `FRead`, ending at the FFS size-field limit |

The buffered case explicitly installs a 512-byte full buffer with `SetVBuf`.
Its receipt records that `FWrite` returned all 15 bytes before `Flush`, and
records successful `FRead` on both boots. The native flush sends one range
write rather than a separate write for every byte. Complete native writes
ignore undefined secondary packet errors; short writes retain the pending
suffix and its exact position for retry.

The original guest command checks `Info`, file length, seek positions and
payload. It calls DOS `Flush`, closes the file and sends `ACTION_FLUSH`.
Before host disposal, the test observes completed `CMD_UPDATE` with zero
device error and independently reads the saved backing file. It then starts a
new protected machine from reset and verifies the same bytes through DOS.
Whole-image hashes prove that the protected boot leaves the image unchanged.
Independent checks cover every file-pointer table and checksum, allocation
bits, the valid root and bitmap state, payload, partition guards and a low-file
alias guard.

The six app/runner cases repeat ordinary writes and protection checks, restart
and fresh protected readback, and TD64 partition placements immediately below
and above the 4 GiB device boundary. The candidate was first embedded through a
temporary build resource while the source firmware pin retained its previous
value. After acceptance, the source contract and preset were updated and built
normally without that resource override. The normal app binary differed from
the staged build, so all six frontend cases were repeated against the final
binary. The table and JSON use those final-build results; the earlier staged
passes are retained separately. The emulator runtime binaries stayed identical.

## Discriminating controls and other coverage

With the earlier guest fixture, the previous pinned ROM fails both large-file
cases at length discovery. The intermediate seek-only ROM passes that stage
but fails both writes with `WriteProtected` on the legitimate shared handle.
Those historical controls precede the explicit `SetVBuf` correction and are
retained separately from the current fixture's final accepted results.

Eighteen compiled MC68000 cases cover native packet arithmetic, position limits,
file examination, geometry/TD64 transfers and both shared/update handle modes. Each
large-file write case first verifies that an actual protected file rejects the
write without changing position or data, then clears the protection and checks
the successful write/read. Shared-handle cases also use odd guest buffer addresses.
These are compiled instruction tests, distinct from
the full-ROM cases above. The preceding host semantic audit remains historical
evidence for its recorded source; it is not relabeled as a new ROM replay.

Three additional compiled native tests execute the production Close path on
MC68000, MC68020 and MC68040, with 15 scenarios per CPU (45 total). They check
buffered range packets, partial progress and retry, error preservation,
allocation failure, undefined secondary errors after complete writes, buffer
ownership and cleanup. These remain separate from full-ROM boot evidence.

Sixty-two host stream tests cover buffered and direct range dispatch, fallback,
wide positions and pending-suffix retries. Twenty-four current host filesystem cases cover geometry, bitmap extensions,
allocation, partial final pages, corruption rejection and the cache regression.
That regression fails on the previous implementation: geometry queries rise
from 56 to 119 for a one-byte overwrite when the file grows from one to 64 data
blocks inside the same pointer table and bitmap page. The corrected code passes
the bounded-query check and verifies the complete file payload and allocation
count. The superseded slow ROM replay was cancelled during write validation;
it is retained as an incomplete run and contributes no passing qualification case.

Both production Release solutions pass with zero errors. CopperStart reports
2 existing compiler warnings; CopperScreen reports 0 warnings. All ten
firmware-loader/settings host tests pass. The normal build embeds the source
firmware contract, and its app and runner binaries match the frontend evidence.

The earlier maximum-file attempt on ROM `8053df6d…` omitted `SetVBuf`, so it
exercised unbuffered `FWrite`. That attempt was stopped after observing only
its first byte written; its TRX is aborted and is not qualified. Inspection
also found that the real buffered flush used one native write per byte. Both
the fixture and production path were corrected, producing ROM `4054184e…`
and requiring the fresh replays recorded here. Its separate priority-change
record belongs to that superseded, incomplete attempt and is not reused as
evidence for the accepted processes.

## Compatibility and limits

This extends CopperStart's implementation while keeping the FFS disk format.
It does not make these volumes safe for an unmodified Commodore FFS or old
formatting/repair utilities. The historical
[TD64 patch notes](https://aminet.net/disk/misc/ffstd64.readme) distinguish access
beyond the 4 GiB device boundary from contemporary DOS partition limitations.
Classic DOS callers also retain their own signed-result and seek restrictions.

The tests overwrite short ranges in existing large files. They do not stream
several GiB through DOS, widen the 32-bit FFS file-size field, implement arbitrary
FFS resize, or qualify every representable volume size. Writes still validate
the full existing pointer chain, so short overwrites of very large files remain
expensive on the emulated 68000. The optimizations do not remove that traversal.
Correctness replay
duration is not a throughput measurement. This is source app/runtime and
runner evidence, not a signed installed-package or Store release qualification.

Reproduce the large-file group from CopperStart with
`scripts/test-large-ffs-files.ps1`, supplying the qualified ROM directory and a
new output directory. Ignored artifacts retain the ROM, sparse images, phase
receipts, test results, build logs and source/binary identities under
`artifacts/dos-large-rom-20261019`. ROMs and media are not committed.
