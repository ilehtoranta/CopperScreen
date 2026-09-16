# Optional native replay

The normal focused suite skips two tests unless all three environment variables
are set. These are correctness/allocation checks, not formal throughput samples.

Place your own copy of Lemmings disk 2 ZIP at `media/lemmings-disk2.zip`. The media
directory is ignored by Git. Set paths for a native 256 KiB Kickstart 1.3 ROM and
Lemmings disk 1, then run:

```powershell
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_ROM = 'path/to/Kickstart_13.rom'
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_ADF = 'path/to/Lemmings-disk1.zip'
$env:COPPERSCREEN_LIGHTWEIGHT_NATIVE_SCRIPT = (Resolve-Path 'CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json').Path
dotnet test CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj -c Release
```

The workload uses the previously accepted SR-cracked Lemmings disk set. Different
releases are not interchangeable with its frozen input and fingerprints. Each
native test runs 14,520 fields; one also presses and releases Return during
gameplay. Both require active audio, no reported unsupported feature, and the
accepted pixel/audio fingerprint `C65F87325946E5DA`. The ordinary replay also
requires CPU fingerprint `6AE7090DA8AFB7E3`, at cycle `2063321634`.

The script differs from the library repository's frozen workload only in the
local disk-2 path. It contains input actions, not copyrighted game content.
Interactive sound, controls and residual interlace behavior remain separate
live checks; replay success is not a fresh hardware-correctness claim.
