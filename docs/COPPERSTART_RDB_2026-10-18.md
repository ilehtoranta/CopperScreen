# CopperStart RDB configuration — 2026-10-18

The app pins `diskboot-2026-10-18-td64.1`, a 524288-byte standalone ROM with SHA-256
`199685F8AA9BEBE7D1879F12E5C7D37A88B85AB7752FCA48AF30C9CEDAC97AD0`.
This is the newer qualified TD64 ROM, retaining the RDB implementation.
The original RDB qualification used ROM `D4143683...7430AD8`; the consumer
readbacks below were repeated against the final shared pin. Its core is 493716 bytes; native graphics is 30099
bytes at `$FF8A00`. The producer retains its frozen Exec/DOS, SDK, compiler,
emulator and CPU inputs. Later concurrent provider changes are outside that
producer qualification. No ROM or disk image is committed or released.

## Settings and persistence

In **Settings > Hard disks**, select an existing image on unit 0 and choose
**RDB** as its image layout. RDB is the default for a newly added CopperStart
disk. Geometry and partition metadata fields are hidden; names, offsets and
boot priorities come from the image. Creation is disabled for RDB images.
Read-only is selected initially and whenever the path changes. Clear it to
allow direct writes to the backing file. Apply the settings and restart.

Profiles and the remembered last machine store `mode: "rdb"`, the image path,
unit and read-only choice. RDB mode saves no synthetic partition metadata.
Changing layout or protection requires a restart. **Partition** mode and its
existing explicit FFS environment settings remain available.

CopperStart's supported app machine remains PAL OCS, a 68000, 512 KiB Chip plus
512 KiB slow RAM and one DD/HD floppy drive. One existing hard-disk image on
unit 0 may contain multiple FFS/DOS1 partitions. The current ROM also supports TD64 extents; see
[the separate TD64 continuation](COPPERSTART_TD64_2026-10-18.md). The highest
signed partition boot priority selects `SYS:`; ties retain discovery order.
Nonbootable/-128 partitions remain data mounts; NoMount partitions are omitted.
Other hard-disk filesystem types are skipped by this ROM. Bootable hard disks retain precedence over DF0;
priorities arbitrate between hard-disk partitions. With data-only partitions,
DOS can boot from a floppy and mount the hard-disk data volumes.

## Command lines

The desktop app accepts:

```powershell
CopperScreen.exe --copperstart --rom C:\Firmware\CopperStart-DiskBoot.rom --hdf-rdb C:\Disks\Amiga.hdf
```

The Lightweight runner accepts:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner.dll --rom C:\Firmware\CopperStart-DiskBoot.rom --hdf-rdb C:\Disks\Amiga.hdf --frames 2000
```

Both new options are read-only by default. Use `--hdf-rdb-writable` to allow
direct writes. Neither requires geometry. A missing, unaligned or invalid RDB
image produces a configuration error without falling back to a synthetic
partition. Inspection reuses CopperDisk's CopperHDF RDB parser and opens the
image read-only. The app's raw/ZIP ROM loader checks the new pin before boot;
older ROMs receive the updated recovery guidance.

The existing `--hdf-ffs`/`--hdf-ffs-writable` geometry syntax remains available.
The generic runner's older `--hdf` option retains its existing writable auto
discovery behavior; the desktop's generic `--hdf` remains read-only by default.

## Consumer validation

All checks pass with no skips:

| Coverage | Cases | Full-ROM boots |
| --- | ---: | ---: |
| Settings/profile/last-machine and firmware checks | 23 | — |
| Headless desktop storage-form workflows | 2 | — |
| Actual runner option/error checks, zero guest frames | 8 | — |
| Native app and runner RDB readback replays | 2 | 4 |

The native app restores saved settings and creates two fresh sessions. The
runner starts two fresh processes, one protected and one writable. Each boots
the pinned ROM without a floppy, selects `DH1:` by priority, opens both FFS
partitions and verifies both 2049-byte patched files and filesystem flush
replies. The backing image remains unchanged. The fixture is the producer's
saved two-partition image, SHA-256
`000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368`.
Runner probes retain the standard guest execution and are diagnostics, not
throughput measurements.

The Release production solution build passes with zero warnings and errors.
Evidence is under ignored `artifacts/rdb-frontend-20261018/`; the companion
JSON records the exact consumer binaries and source snapshots. Installed MSIX
and real-media acceptance are separate from these source app/runner checks.
The producer's 30 full-ROM cases/36 boots, five compiled MC68000 checks and
host font/link checks are separate coverage, recorded in CopperStart's
`Docs/NativeRdbBoot.md` and `artifacts/rdb-20261018/validation.json`.

Reproduce with locally supplied qualified inputs using
`scripts/test-copperstart-rdb.ps1`. Existing explicit-partition write evidence
remains in [the earlier write continuation](COPPERSTART_HARDDISK_WRITES_2026-10-18.md).

The [RDB write continuation](COPPERSTART_RDB_WRITES_2026-10-18.md) adds six
full-ROM cases across eleven boots through the app runtime and runner. Both
partitions are written and flushed before host save/disposal, then read after
reset, replacement, process restart and fresh protected boots. Reversing boot
priorities selects DH0 in both front ends. Two protected mutation controls leave
the backing image unchanged.
