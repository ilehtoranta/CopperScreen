# CopperStart hard-disk configuration

The firmware contract now selects the frozen `diskboot-2026-10-18-hdf.1`
development ROM: 524288 bytes, SHA-256
`FC6511086BB86A33A04887FBF2EF1D7F82A91631853E39DB08529AD0C0314AC0`.
This is a consumer qualification ID, not a published producer release.
No ROM or media is bundled.

In **Settings > Storage > Hard disks**, add an existing image on unit 0, select
**Partition**, and choose **Use FFS partition settings**. Enter the device name,
heads, blocks per track, first cylinder and last cylinder from the formatted
image's partition definition. The cylinder endpoints are inclusive. The button
sets DOS1, 512-byte blocks, one sector per block, two reserved blocks,
MaxTransfer `$1FE00` and address mask `$FFFFFFFE`. It does not guess geometry or
format an image. The advanced partition fields remain editable and are retained
when changing the file path or write protection.

CopperStart accepts exactly one existing explicit DOS1 partition on unit 0,
whose exclusive end is strictly below 4 GiB. Its backing file must cover the
configured range. RDB discovery, blank-image creation and multiple partitions
are outside this boot path. Read-only is the default; clearing it allows direct
guest writes to the selected file. Hard-disk boot takes precedence over DF0;
no floppy is required. The qualified app machine remains PAL OCS 68000,
512 KiB Chip + 512 KiB slow RAM, no Fast RAM, RTC or shared folders.

Personal profiles and the last machine retain the layout, unit, file path,
write permission and every explicit DosEnvec field. For example:

```json
"HardDrives": [{
  "Unit": 0, "Path": "C:\\Disks\\system.hdf", "ReadOnly": true,
  "Mode": "partition",
  "Partition": {
    "DeviceName": "DH0", "TableSize": 16,
    "SizeBlockLongs": 128, "SectorsPerBlock": 1,
    "Surfaces": 2, "BlocksPerTrack": 32,
    "LowCylinder": 4, "HighCylinder": 58,
    "ReservedBlocks": 2, "MaxTransfer": "$0001FE00",
    "Mask": "$FFFFFFFE", "DosType": "$444F5301"
  }
}]
```

Both the desktop command line and Lightweight runner accept the same explicit
geometry syntax. These commands mount a 1802240-byte partition starting at byte
131072; the image must already contain its filesystem at that offset:

```powershell
CopperScreen.exe --copperstart --rom C:\Firmware\CopperStart-DiskBoot.rom `
  --hdf-ffs C:\Disks\system.hdf DH0:2:32:4:58

dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- `
  --rom C:\Firmware\CopperStart-DiskBoot.rom `
  --hdf-ffs C:\Disks\system.hdf DH0:2:32:4:58 --frames 3000
```

Use `--hdf-ffs-writable` instead of `--hdf-ffs` to permit writes. The runner
prints the effective byte range and protection. Existing automatic `--hdf`
options retain their previous semantics; they do not configure a CopperStart
FFS mount. Native Kickstart profiles retain their existing RDB support.

## Firmware provenance and coverage

Producer base commit: `b45c2e4e50860f87e81e21d2a6e5c2a08e2631ad`, plus frozen
native sources and producer binaries recorded in the producer's
`artifacts/harddisk-20261018/classic-native/disk-boot-test-manifest.json` and
`artifacts/harddisk-20261018/validation.json`. The Classic filesystem profile
excludes SFS mutation code from this 512 KiB standalone ROM. The core is 493136
bytes, graphics 30774 bytes at `$FF8700`, including packed font data and the
packet-wait early-reply fix. Five DOS source files changed concurrently after
the freeze; this pin qualifies the frozen ROM, not those later source edits.

Producer qualification passed 16 full-ROM cases across 20 reset boots: six
hard-disk cases, including writes, stream/device flushing, two fresh readbacks,
read-only protection and rejected mounts; plus ten DD/HD floppy regressions.
Separately, five compiled MC68000 cases, 31 host filesystem cases and the host
font test passed. The 1.8 MiB partition used the app's slow-RAM configuration;
the producer's 8 MiB multi-bitmap case used 2 MiB Fast RAM, which is not added to
the app's qualified preset. Producer evidence is distinct from app-session,
desktop UI, installed-package and performance coverage.

The focused app tests cover explicit configuration, all-field profile and
last-machine round trips, restart detection, rejection of unsupported mounts
and image bounds. With `COPPERSCREEN_COPPERSTART_ROM` and
`COPPERSCREEN_COPPERSTART_HDF` set, the app test also boots the exact ROM twice
from the producer's original `copperhdf-track.hdf` saved fixture, through
restored settings, verifies all 2049 saved bytes and checks that the protected
image is unchanged. Missing inputs skip this native case explicitly.

Historical CopperOS/installed-package records retain their original firmware
hashes; updating this source contract does not requalify an installed package.

## Consumer results

The [consumer receipt](COPPERSTART_HARDDISK_2026-10-18.json) records exact test
and runtime binary hashes. Release solution build: zero warnings, zero errors.
The focused settings/media/CopperStart suite passed 68 cases, including the
original DF0 lifecycle case and the two-boot native HDF readback case. The
optional Lotus replay was skipped because its media was not supplied. Ten host
geometry cases, one headless desktop form case and ten runner configuration
checks passed separately. The latter include protection defaults, writable
selection, geometry overflow, the exact 4 GiB endpoint, short images, multiple
disks and missing arguments.

The runner also completed a separate 1000-field full-ROM boot with no floppy,
no unsupported feature marker, and the successful native readback receipt. Host
inspection verified all 2049 bytes in the guest's A501 buffer and the original
backing-file hash. This is a compatibility diagnostic, not a performance result.
The initial consumer continuation above replays protected fresh readback.
The subsequent [app/runner write and flush continuation](COPPERSTART_HARDDISK_WRITES_2026-10-18.md)
adds guest writes, live backing-file checks, app save/reset/session replacement,
process restart and fresh protected readback through both front ends.

Reproduce the runner argument checks after building it:

```powershell
./scripts/tests/test-ffs-hardfile-runner.ps1 -RunnerPath <runner.dll>
```

The existing `scripts/test-copperstart.ps1` accepts optional
`-HardDiskFixturePath <copperhdf-track.hdf>` to include the hash-checked two-boot
app readback alongside the firmware lifecycle tests. It explicitly records
unavailable HDF or Lotus inputs rather than treating skips as replay passes.
