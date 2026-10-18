# Apidya on the Lemmings-fixed CopperStart ROM

The supplied SPS 764 `Apidya_Disk1.ipf` reaches options, the opening stage and
the continue screen on the PAL OCS A500 with 512 KiB Chip plus 512 KiB slow RAM.
Native Kickstart 1.3 is the control. No new firmware incompatibility was found
in this bounded pass. The [evidence record](COPPERSTART_APIDYA_2026-10-17.json)
identifies the media, ROM, input, binaries and local replay manifests.

This ROM was freshly compiled from the same native root and frozen components
as the [Lemmings qualification](COPPERSTART_LEMMINGS_2026-10-17.md). It includes
the caller-data audit, CIA startup and committed empty-display Intuition fix at
CopperStart `244e2e969dea613cc078ec9a63d0fef32012333c`. The root sources and all
eleven frozen dependency hashes are checked before compilation. The native
root has no managed allocations or framework features. The compiler, Exec/DOS,
device and native graphics components retain their earlier qualified identities;
this is not a clean rebuild of the moving producer tree.

The fresh 512 KiB image has SHA-256
`05936F0470B0CA1A425A527D073436544BAF185D5A2AF4A06F26D5D815961E6A`,
457,216 compiled code bytes and 26,098 separately linked native graphics bytes.
It reproduces the preceding Lemmings-qualified image byte for byte; the recorded
fresh build timestamp and new root assembly identity distinguish this compilation
from reusing the earlier file.

The input is the existing
[`corpus-apidya.json`](../../CopperScreen.Lightweight.Tests/Workloads/corpus-apidya.json).
Fire enters options at field 2,500 and starts the game at 6,020. Direction/fire
input begins at 8,700. Reviewed full-field captures show the player moving from
the left toward the right edge, scrolling scenery, enemies and the HUD.
The recorded player then loses lives and reaches the continue
screen. First-stage completion is not claimed.

Both ROMs run for 12,000 fields in conservative-batch and scalar CPU modes,
capturing every 60 fields and field one. Each mode pair matches all **1,005**
BMP/state/Chip/slow-RAM/sampled-PCM files, with matching final CPU, hardware,
output and cycle fingerprints. All 51 options-screen images from fields 3,000
through 6,000 also match across ROMs. Overall, 103 of the 201 cross-ROM images
match; the remaining fields include loading, animation and player-death phase
differences. The record identifies the visually reviewed checkpoints. General
cross-ROM pixel or CPU/RAM equality is not required or claimed.
Initial black-screen differences in the exploratory run were transition timing,
not a crash. No media, opcode, PC, host-service or display-cropping patch was
used. The CPU remains the pinned `1.5.4-nitro-dev.2` package.

A separate 24,000-field Lemmings regression on the fresh image completes level
one with 100% rescued and preserves all **605** captures from its preceding
qualified normal-mode replay. This regression does not repeat Lemmings scalar
coverage; its earlier qualification records that pair separately.

The exact commands are retained in each manifest. For example, from the
repository root with locally supplied inputs:

```powershell
python scripts/probes/CopperStart/replay.py `
  --rom C:/Firmware/CopperStart-DiskBoot.rom `
  --disk 'C:/Data/TestImages/Team17/Apidya_764.zip#/Apidya_Disk1.ipf' `
  --output artifacts/apidya-fresh --firmware native `
  --frames 12000 --interval 60 --slow-ram-kib 512 `
  --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-apidya.json
```

Repeat in a fresh output directory with `--scalar-cpu`. `--firmware native`
disables interpretation of private CopperStart receipts after game takeover;
the recorded ROM hash identifies the actual firmware. Guest execution is
unchanged. Runner completion alone is not title qualification.

This is disk-one opening-stage correctness evidence. Disk two, later stages,
two-player play, full-game completion and audio listening/fidelity are unverified.
It does not change standard-app firmware admission, installed MSIX coverage or
host-throughput acceptance. ROMs, media, captures and build output remain local
under `artifacts/copperstart-apidya-2026-10-17/`; the generic fresh build began
under the separate North & South exploration directory before this title was
selected. No producer source change or package publication was needed.
