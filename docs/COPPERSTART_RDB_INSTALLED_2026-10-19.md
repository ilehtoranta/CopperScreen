# RDB FFS writes in the installed app — 2026-10-19

**PASS:** seven installed-process phases across eight full-ROM boots. Both
RDB FFS volumes pass writes, guest flushing, reset, process restart and fresh
protected readback, with either partition selected as `SYS:`. Seven host
regression/inspector cases and 64 MSIX tooling controls also pass, without skips.
The Release solution builds with zero warnings and errors. Exact identities,
hashes, observations and retained paths are in the
[qualification receipt](COPPERSTART_RDB_INSTALLED_2026-10-19.json).

## Installed package and discovered correction

This run uses a signed diagnostic MSIX installed by Windows, rather than a
development registration or an unpackaged executable:

| Property | Accepted value |
| --- | --- |
| Package | `CopperScreen.Test.ef78ae9aea114bc5a18b56220a60c510_1.1.45.0_x64__a0cyw3gffeccg` |
| MSIX SHA-256 | `F17FBD0ABB7D3F7FFA3C9743C830D0E1B085D094ED174A919638A90380FD93B0` |
| Windows deployment | `IsDevelopmentMode=false`, status `Ok`, signature `Valid` |
| Execution | Seven distinct, non-elevated processes activated through the package application identity |
| Storage | Package-family `LocalState/CopperScreen/RdbProof/<run>/...` |
| Firmware | `diskboot-2026-10-18-ffs-bitmap.1`, SHA-256 `C77E51BBBAB2D0F5EDFC12B59094707279B2D1BC44E57B585857F1B56763C23B` |

Each process path and each loaded app/engine/disk/CPU assembly must match this
immutable WindowsApps installation. The normal desktop window, native runtime,
presentation consumer and audio output remain active. The bounded machine is
PAL OCS A500, 68000, 512 KiB Chip plus 512 KiB slow RAM, no floppy medium,
RTC, Fast RAM or shared folders. The ROM is hash checked against the current
embedded pin. Neither the ROM nor the original MC68000 probe is changed.

The first installed attempt passed writes and reset, then exposed a production
settings error on process restart. `FromStartupOptions` started with a profile's
hard disks and appended the effective startup hard disks again. Opening a saved
HDF profile therefore doubled its entry in the UI draft, which the normal close
path remembered. The next launch had duplicate unit 0 entries.

The correction replaces the draft's inherited hard-disk list with the effective
startup list. Four host regressions repeatedly open, save and remember RDB and
explicit FFS profiles in protected and writable modes, requiring exactly one
unchanged drive. A new signed identity/version was built after the correction;
all accepted installed phases below use that package. The earlier failure is
retained under `artifacts/rdb-installed-20261019/installed/`, with its original
package alongside it. Its partial success is not included in accepted counts.

## Native writes, selection and readbacks

The owned fixture is the producer's `rdb-both-written.hdf`, SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.
The inspector removes each saved result, releases and clears its six blocks,
and repairs the directory and bitmap checksums in a private copy. The pristine
copy has SHA-256
`425D4D57271143F1B438014003E25C07BE0B95A2B9F8611C7F295C9B93F286DE`.
The original fixture remains unchanged.

DH0 starts at byte 131072 and DH1 at byte 1933312. Each partition contains
3520 blocks of 512 bytes. Both have independent native filesystem handlers.
The image supplies the partition environments; settings persist RDB mode and
no explicit partition metadata. DH0 initially has priority -128 and remains a
data mount; DH1 has priority +5 and supplies SYS identity 2. The second layout
swaps those priorities and repairs both PART checksums, requiring SYS identity 1.

On both volumes the original guest HUNK creates `Data/Saved`, writes 2049 bytes,
patches 32 bytes starting at byte 509, calls `Flush(fh)`, closes, reopens, reads
all bytes and checks EOF. It then calls `DeviceProc` and `DoPkt(ACTION_FLUSH)`
for each filesystem. Subsequent boots must take the existing-file branch on
both volumes and return the same payloads.

| Installed phase | Boots | Required outcome |
| --- | ---: | --- |
| Write, host save and reset | 2 | Both files and successful guest flushes before host save; unchanged reset readback |
| Restart | 1 | New process restores the remembered machine and starts through the normal launcher action; both files read back |
| Fresh protected boot | 1 | New package-private machine opens a protected copy of the pre-host-save export; both files read back |
| Reversed priorities, write | 1 | DH0 supplies SYS; both volumes written and flushed |
| Reversed priorities, restart | 1 | New process restores that machine; SYS remains DH0 and both files read back |
| Reversed priorities, fresh protected boot | 1 | Protected saved copy boots from DH0; both files read back |
| Protected pristine image | 1 | First mutation returns error 214; complete backing remains pristine |

For every successful boot, raw Chip/slow RAM receipts verify SYS identity, both
creation/readback phases, all 2049 bytes per volume, distinct handler ports and
I/O requests, successful flush replies and completed `CMD_UPDATE` requests with
zero error and actual length. These are pinned-ROM private diagnostics, not
guest ABI requirements. No observer writes guest RAM or services guest DOS calls.

After pausing the normal runtime, the observer reads the open backing file
**before host save or disposal** and retains RAM, HDF and window captures.
Independent decoding checks both files, allocation counts, bitmap and metadata
checksums and unchanged unrelated blocks, including all bytes outside the two
partitions. Host save must succeed without advancing guest cycles or changing
the observed image. Reset requires both new readback phases and completed
flushes, rather than accepting retained RAM's old completion marker.

The default layout reproduces the qualified fixture hash exactly. Every
restart and fresh readback preserves that whole image. The reversed layout
similarly preserves SHA-256
`10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617`.
All seven processes exit normally with code zero. Retained profiles and last
machines contain exactly one RDB image with the expected protection mode.

## Reproduction and scope

Create a new diagnostic identity with an existing, trusted signing certificate:

```powershell
$run = [guid]::NewGuid().ToString('N')
./scripts/package-msix.ps1 -IdentityName "CopperScreen.Test.$run" `
  -Publisher '<trusted certificate subject>' -PublisherDisplayName 'Local diagnostics' `
  -PackageVersion '1.1.45.0' -CertificateThumbprint '<owned certificate thumbprint>' `
  -OutputDirectory artifacts/rdb-installed-package -InstalledTestBuild

./scripts/test-msix-rdb-writes.ps1 `
  -PackagePath artifacts/rdb-installed-package/CopperScreen-1.1.45.0-win-x64.msix `
  -RomPath C:\Firmware\CopperStart-DiskBoot.rom `
  -RdbFixturePath C:\Fixtures\rdb-both-written.hdf `
  -OutputDirectory artifacts/rdb-installed-proof
```

The controller rejects existing identities, unsigned/untrusted packages,
development registrations, incorrect inputs, wrong executables, failed phases
and unexpected image hashes. It preserves this run's private profiles, media
and logs outside package storage, then removes only its own registration.
The accepted run's registration is removed; its evidence remains under ignored
`artifacts/rdb-installed-20261019/installed-final/`.

Host coverage consists of four saved-profile regressions, three independent
inspector controls and 64 synthetic MSIX tooling checks. Newly compiled MC68000
coverage is zero in this continuation. Diagnostic observer inclusion and normal
production exclusion were checked separately. The helper and command switch
are compiled only into diagnostic builds.

This is installed native storage compatibility evidence with automated observers
and normal app actions. It does not certify Store submission, a package update,
uninstall/reinstall recovery, power-loss durability, general AmigaOS behavior or
performance/timing acceptance. No package is released and no trust settings
are changed. Earlier [source app/runner checks](COPPERSTART_RDB_WRITES_2026-10-18.md)
remain a separate qualification.

The later [installed-update continuation](COPPERSTART_RDB_UPDATE_2026-10-19.md)
adds a signed in-place update with unchanged package-private images/settings,
both SYS selections, new writes/flushes and fresh protected readbacks across
15 process phases and 17 boots. It is separate from this original single-package
qualification.
