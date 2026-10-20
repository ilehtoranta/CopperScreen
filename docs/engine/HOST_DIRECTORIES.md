# Bootable shared folders

Lightweight exposes existing host directories as live AmigaDOS volumes. Configure
**Storage > Shared folders**, or use the same options in the desktop and runner:

```powershell
--host-dir HOST0 Exchange "C:\Amiga\Exchange"
--host-dir-rw DH0 Workbench "C:\Amiga\Workbench"
--host-dir-boot-priority DH0 0
```

Options are repeatable. `--host-dir` is read-only; `--host-dir-rw` permits direct
host writes. Without a priority a folder is an exchange volume. Priorities
−127 through 127 enable boot; higher priorities precede lower ones. Priority
options may appear before or after the mount and may override a desktop profile
mount. Supplying any CLI mounts replaces the profile's folder list.

New settings entries default to read-only and non-booting. Enabling boot starts
at priority 0. Applying mount changes requires restarting the machine; there is
no hot mounting. A folder must already exist. Device names are case-insensitive
and must not duplicate another folder, a floppy or a mounted HDF partition.
Volume labels must be unique among shared folders. The same root cannot be
mounted twice. Minimal disk boot and synthetic runner workloads have no native
DOS and reject shared folders.

An API caller supplies `LightweightA500Configuration.HostDirectories`, an empty
list by default. Each `LightweightHostDirectoryConfiguration` has `DeviceName`,
`VolumeName`, `Path`, `ReadOnly = true`, and `BootPriority = null`. Desktop profile
JSON uses `hostDirectories` entries with those fields; property casing is
accepted independently of the profile's other fields. Paths follow the existing
profile/startup base-directory normalization. Settings cloning and restart
comparison include all five fields.

Users supply their own Workbench files, including `S/Startup-Sequence`, `C`, `L`
and any required libraries. A boot priority does not create an operating system.
The application supplies an original filesystem handler and no licensed OS
files. An installed folder can boot with all floppy and HDF media absent.

A bounded [WHDLoad Hired Guns campaign replay](WHDLOAD_HIRED_GUNS_2026-10-16.md)
checks play, a named save, F10 return to Workbench and fresh-process reload on
the existing handler. Its fixture uses WHDLoad's standard SavePath option to
create the old slave's save subdirectories and verifies deferred writes after
normal exit. The record specifies the supplied inputs and exact machine profile.

## Guest and owner-thread contract

A separate 64 KiB virtual Zorro II board (manufacturer `$07DB`, product `$46`,
diagnostic area `$4000`) is present only with configured folders. Autoconfig
orders Fast RAM, CopperHDF when present, then this board. The storage gateway
registry recognizes both boards independently of an attached HDF.

Original 68000 code in the copied board ROM supplies a native DOS handler
segment and an expansion BootPoint. Native Kickstart starts DOS, creates handler
processes, waits for messages, allocates/frees locks and replies using Exec.
The gateway translates one packet on the machine owner thread. It never
recursively dispatches the CPU or polls the host filesystem per cycle. Transfers
use at most 64 KiB of temporary storage; filesystem latency pauses wall-clock
execution without advancing guest clocks. Guest destinations must be entirely
RAM. Read-only guest strings may also reside in ROM. Existing RAM write paths
invalidate CPU/JIT code generations.

V34 sends the native `CreateProc` launch message before filesystem requests;
the handler acknowledges its segment/startup arguments without overwriting them.
V40 sends `ACTION_STARTUP`. Both register device and volume nodes in native DOS.
Boot nodes are inserted in priority order. Native DOS resolves device, volume and
assign prefixes before dispatch; relative paths use the supplied lock.

| Packets | Operations |
| --- | --- |
| 0 | Native filesystem startup |
| 8, 15, 19, 29, 40 | Locate/free/duplicate/parent locks and Boolean same-object comparison |
| 23, 24 | Examine and stable directory enumeration |
| 1004–1008, 82, 87, 1022 | Update/input/output open, close, seek, read, write and resize |
| 22, 16, 17 | Create directory, delete and rename |
| 21, 28, 34 | Protection, comment and date |
| 25, 26, 27, 1027 | Volume information, flush and filesystem query |
| 1026, 1028, 1030, 1031, 1034 | Open from lock, change mode, duplicate/parent from handle, examine handle |

The packet comparison result is a Boolean; `dos.library` owns the public
`SameLock()` result codes. `ChangeMode` receives a handle BPTR, while other handle
operations use `fh_Arg1`. Native V34/V40 date packets carry a direct DateStamp
pointer, sometimes only word-aligned, despite the BPTR label in some reference
tables; captured native packets and the regression test preserve this distinction.
File-information replies
write only the 224 bytes through `fib_Comment`: some V34 BCPL commands place their
live stack immediately after that reduced structure. Unallocated/reserved bytes
must remain untouched. These details have discriminating packet tests.

Shared and exclusive access conflicts return `ERROR_OBJECT_IN_USE` (202).
Unknown locate-lock modes use shared access as required by the DOS reference
(Workbench 1.3's drawer copier exercises this compatibility rule).
Invalid/stale objects return 211; invalid guest pointers return 115; missing guest
allocation returns 103. Read-only mutations return 214. Unsupported packets
return `ERROR_ACTION_NOT_KNOWN` (209), including formatting, links, notifications,
record locking and ExAll. Read and seek failures use −1; EOF returns zero and
seeking beyond EOF fails. File positions/sizes are bounded by signed classic DOS
limits (less than 2 GiB); 64-bit DOS extensions are not implemented.
Truncation preserves other open handles' positions and returns the actual size.

Reset and successful restart discard open files, locks and enumerations and
register fresh guest structures. File handles flush on close, explicit flush,
reset and disposal. All streams are released even if one shutdown flush fails;
the error remains visible. Preparing a failed replacement does not close or
invalidate handles in the active machine.

Protocol references: [AmigaDOS packets](https://wiki.amigaos.net/wiki/AmigaDOS_Packets),
[expansion boot sequence](https://wiki.amigaos.net/wiki/Expansion_Library), and
[AmigaOS DOS reference manual, chapter 14](https://developer.amigaos3.net/sites/default/files/downloads/2024-10/Amiga_ROM_Kernel_Reference_Manual_DOS.pdf).

## Names, metadata and confinement

Lookups are case-insensitive and read current host entries. Amiga leading/doubled
slashes traverse parents within the selected root. Host absolute paths, dot/dotdot
components, symlinks and junctions are rejected. Internal metadata and temporary
files are hidden. An enumeration snapshots entry paths; later additions appear
on a fresh scan, while removed entries return DOS errors rather than being
silently replaced. Unrepresentable host Unicode names and names longer than
107 Latin-1 characters are omitted; ambiguous case-insensitive names fail.

Ordinary compatible names stay unchanged. Windows-invalid guest names, device
names such as `CON`, trailing dots/spaces and internal-name collisions use
`.copperfs-` followed by the hexadecimal Latin-1 filename bytes. A versioned
`.copperfs.json` in each directory records their original guest names, protection
and comments:

```json
{
  "Version": 1,
  "Entries": [
    { "Name": "CON", "HostName": ".copperfs-434F4E", "Protection": 64, "Comment": "Amiga comment" }
  ]
}
```

Keep sidecars with their directories when moving/copying mapped files from the
host. Rename/delete packets maintain the corresponding entries. Unmapped ordinary
files have protection 0, the usual `rwed`. Explicit Amiga read/write/delete
restrictions and host access restrictions apply; execute handling remains native
DOS's responsibility. Comments are limited to 79 Latin-1 characters. Dates use
host local last-write timestamps and the Amiga 1978 epoch with 50 ticks/second.

Sidecars use a flushed temporary file and atomic replacement. Missing sidecars
mean defaults; malformed, missing-version or unknown-version sidecars return a
clear error and are never overwritten with defaults. Data and metadata are
separate host operations: a later sharing, disk-space or sidecar failure can leave
a completed part of a request on disk. DOS receives a failure; there is no
cross-file transaction or automatic rollback of already-written user data.

The Windows backend leases root/parent directories, opens objects without
following reparse points and validates final handle paths. Truncation, dates,
delete and rename use validated handles. This qualification covers Windows;
the portable fallback has pathname/link checks but has not received equivalent
concurrent-replacement qualification. Mount a selected user folder rather than
treating the handler as an adversarial-host sandbox. Overlapping distinct roots
have separate guest lock domains; do not share writable aliases across volumes.

## Verification

Disposable-folder tests cover packet pointer contracts, reduced V34 buffers,
stale objects, exclusive conflicts, EOF/seek/resize, allocation failures, host
sharing/read-only restrictions, failed replacement, reset cleanup, live scans,
name mapping, malformed metadata, junction rejection and persistent metadata.
Host tests cover CLI/profile defaults, normalization, cloning and restart checks.

Native tests require separately supplied inputs:

```powershell
# Extract licensed inputs into NEW disposable directories; ships no OS bytes.
python scripts/prepare-host-directory-native.py "Workbench13.zip" "wb13" --proof-script --exchange-boot-adf "exchange13.adf"
python scripts/prepare-host-directory-native.py "Workbench31.zip" "wb31" --proof-script --exchange-boot-adf "exchange31.adf"
$env:COPPERSCREEN_KICKSTART13_ROM = "Kickstart_13.rom"
$env:COPPERSCREEN_KICKSTART31_ROM = "kickstart-3.1-a500.rom"
$env:COPPERSCREEN_HOSTFS_WB13 = "wb13"
$env:COPPERSCREEN_HOSTFS_WB31 = "wb31"
$env:COPPERSCREEN_HOSTFS_ADF13 = "exchange13.adf"
$env:COPPERSCREEN_HOSTFS_ADF31 = "exchange31.adf"
dotnet test CopperMod.Amiga.Lightweight.Tests -c Release --artifacts-path artifacts/diagnostic-tests --filter FullyQualifiedName~LightweightNativeHostDirectoryTests
```

Optional `COPPERSCREEN_HOSTFS_CAPTURE` writes bounded endpoint screenshots to the
selected local artifact directory. Missing inputs are unavailable coverage.
The dated verification record below distinguishes native results from host tests.
No performance qualification or broader OS compatibility follows from these tests.

### Verification record — 2026-10-08

Windows host, published Copper68k **1.5.3**, production Release outputs and isolated
diagnostic outputs. The supplied inputs were:

| Input | SHA-256 |
| --- | --- |
| Kickstart 1.3 (`Kickstart_13.rom`) | `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53` |
| A500 Kickstart 3.1 (`kickstart-3.1-a500.rom`) | `8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE` |
| Workbench 1.3 rev 34.20 ZIP, Workbench disk | `5FA3B92E8BF18E5E5DB8CB5E9C58D407A67AF2C892B2E31E2913FE7AF0BAEC1B` |
| Workbench 3.1 rev 40.42 ZIP, Workbench disk | `D93611887ACF91F68F5608A5B7812F03EB16F743F40451B67C93067B04C967ED` |

The extractor prepared disposable folders and exchange-boot media; native tests
copied them again into temporary roots. ROMs, Workbench files, derived media and
screenshots stay in ignored local artifacts and are not distributed.

Native acceptance passed for both target Kickstarts:

- Cold folder boot without floppy/HDF media, 68000, 0 and 2 MiB Fast RAM;
  Shell commands load and execute from the folder. A 70,001-byte file crosses
  host → boot volume → exchange volume and matches independently on the host.
- Create/delete/rename, listing, script protection, comments and an exact guest
  DateStamp update persist. A host edit made during native execution appears in
  the subsequent guest copy. Fresh machines reopen the same backing files.
- Workbench drag-and-drop copies a drawer containing that 70,001-byte file from
  the boot folder into a second shared volume. Endpoint images show both native
  drawers and the copied icon, and host bytes match under 1.3 and 3.1.
- Non-booting exchange folders coexist with native 1.3/3.1 floppy boot and 3.1
  CopperHDF partition boot. The 3.1 folder scenario also passes with 68040
  interpreter and JIT; the JIT test asserts actual compiled instructions.
- Eight separate production runner processes (two per Kickstart/RAM combination,
  1,800 frames each) cold-boot and verify first-write/reopen markers, mapped `CON`
  file contents, persistent comments/protection, and `unsupported=none`.

Full commands/results:

| Check | Result |
| --- | --- |
| `dotnet build CopperScreen.slnx -c Release` | Succeeded, zero warnings/errors |
| `dotnet test CopperMod.Amiga.Lightweight.Tests -c Release --artifacts-path artifacts/diagnostic-tests` with the supplied native inputs | 1,190 passed, zero failed/skipped |
| `dotnet test CopperScreen.Lightweight.Tests -c Release --no-build` | 183 passed, zero failed, six optional replays skipped |
| `dotnet test CopperDisk.Tests -c Release --no-build` | 74 passed, zero failed/skipped |

The six host skips are A1200 desktop boot, desktop Workbench 3.1 floppy/HDF
replays, and three native gameplay/keyboard adapter replays whose separate
fixtures were not configured. Those are unavailable coverage, not successful
replays. All new folder-native cases ran. Existing no-mount diagnostic/golden
checks passed. This is functional Windows validation, not FPS/throughput
qualification or a claim about additional operating systems.

Local records are under `artifacts/hostfs-native/`: `final-build.log`,
`final-engine.log`, `final-host.log`, `final-disk.log`, `final-captures/`, and
`process-final-reopen-results.json` with the per-process logs/probes. The separate
process check used the production runner twice on each fresh proof-script folder
with `--host-dir-rw DH0 Workbench <folder> --host-dir-boot-priority DH0 0
--fast-ram-kib <0|2048> --frames 1800`; its startup also creates `CON` and sets its
comment and script flag. No performance acceptance protocol was changed.
