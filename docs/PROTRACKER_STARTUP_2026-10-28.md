# ProTracker 2.3F startup investigation — 2026-10-28

The rebuilt Classic CopperStart ROM does not provide `intuition.library`.
ProTracker successfully launches its editor process, opens DOS and graphics,
then receives zero from `OpenLibrary("intuition.library", 0)`. It immediately
calls `OpenScreen` through that null base. This explains the observed startup
failure. The later damaged stack and console are downstream symptoms.

This is a guest OS compatibility gap in this ROM/profile. The trace does not
justify a CPU, DMA or chipset timing change. No production implementation was
changed during this investigation.

## Paired runtime evidence

Both runs use the original protected `PT23F.ADF`, PAL OCS 8371/8362, MC68000
interpreter, 512 KiB Chip + 512 KiB slow RAM, no Fast RAM/RTC/host directories,
and no input events. Identities match the [utility comparison](UTILITY_AB_2026-10-28.md):

| Input | SHA-256 |
| --- | --- |
| Classic development ROM, 524,288 bytes | `6d8468bb6bd8b0c5b221be865af619f1e4683f4af8085272a689e8e80bd4c181` |
| Kickstart 1.3, 262,144 bytes | `ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53` |
| PT23F.ADF | `38898d61f5b96971be4946761ac3101181d01e40c33512453d39eb76dca60a3a` |

| Call | CopperStart | Kickstart 1.3 |
| --- | --- | --- |
| `OpenLibrary("intuition.library", 0)` | `$00000000`, field 7,368 | `$00C03D24`, field 1,261 |
| `OpenScreen` | Null-base call intercepted before execution | `$00C074D0`, field 1,264 |

On CopperStart, DOS returns `$00C0B200` and graphics returns `$00002440`.
The captured Exec library list includes DOS, graphics and expansion, but no
Intuition. The current task is `PT2.3F`, confirming that the launcher/CreateProc
handoff has succeeded. The relevant editor instructions are:

```text
00C177DC  jsr     -552(a6)             ; OpenLibrary, intuition.library v0
00C177E0  move.l  d0,$00C2D3C4         ; saves zero IntuitionBase
00C177E6  bsr.w   $00C17D68
00C17D68  movea.l $00C2D3C4,a6         ; A6 = 0
00C17D6E  lea     $00C17E3C(pc),a0     ; NewScreen
00C17D72  jsr     -198(a6)             ; OpenScreen through null base
```

The final call is reached at cycle **1,047,037,928**, completed field **7,368**.
At that point USP/A7 is `$00C167EC` and SSP is `$00077F64`, both in writable
RAM. There is no initial stack-allocation failure in this trace. Executing the
call would set the raw PC to `$FFFFFF3A` (24-bit bus address `$FFFF3A`, in the ROM
tail); that address alone must not be classified as an unmapped-PC CPU defect.

The [official ProTracker source](https://github.com/8bitbubsy/pt23f/blob/main/PT2.3F.s#L817)
agrees with the loaded binary: startup stores the Intuition open result without
a null check and calls `OpenPTScreen`. The null result is the immediate cause
for this input; it does not establish that all later APIs would work.

## Follow-on requirement from source inspection

ProTracker's `PTScreenStruct` requests a **320 × 12, depth-one** screen with
type `$008F` (`CUSTOMSCREEN | SCREENBEHIND`). It renders its editor separately.
See the [screen definition](https://github.com/8bitbubsy/pt23f/blob/main/PT2.3F.s#L1258).

CopperStart has a separate disk-loaded guest Intuition provider in
`native/CopperStart.DesktopProviders/NativeIntuitionLibrary.cs`, installed by
`NativeDesktopProviders.InstallIntuitionLocked`. It is not installed by the
Classic standalone ROM or supplied by the original ProTracker disk. Its current
`OpenScreen` validation (lines 372–374 in the inspected working tree) rejects
heights below 84. Therefore merely loading that provider would still reject
this screen. This second finding is **static source evidence**, not a replay
with that provider loaded.

The appropriate next implementation is a bounded native guest Intuition startup
path and support for the small behind-screen lifecycle demonstrated by
Kickstart. That requires guest ABI/lifecycle work and subsequent replay; a fake
library base, host OS shim or ProTracker-specific display patch would not resolve
the contract. No claim is made yet about later editor, playback or save APIs.

## Reproduction and diagnostic validation

The standalone [UtilityStartup probe](../scripts/probes/UtilityStartup/README.md)
observes library returns and stops before null-base vector calls. It does not
instrument production engine code. From the repository root:

```powershell
dotnet run --project scripts/probes/UtilityStartup -c Release -- `
  artifacts/utility-ab/2026-10-28/copperstart-rebuild/CopperStart-DiskBoot.rom `
  media/utilities/originals/PT23F.ADF artifacts/protracker-startup-2026-10-28/copperstart-api 7300 7500

dotnet run --project scripts/probes/UtilityStartup -c Release -- `
  C:/Data/ROM/Kickstart_13.rom media/utilities/originals/PT23F.ADF `
  artifacts/protracker-startup-2026-10-28/kickstart-api 600 1800
```

Use fresh output directories when repeating. Captures are ignored local evidence
under `artifacts/protracker-startup-2026-10-28/`; each final run contains
`trace.json`, `chipram.bin` and `slowram.bin`. Exploratory traces in that directory
followed the later corruption before the API observation isolated its origin.

At field 1,800, the diagnostic Kickstart run matches the ordinary production
runner's complete Chip and slow RAM byte-for-byte:

| Memory | SHA-256 |
| --- | --- |
| Chip | `40ee3f21720fddb5ed2842616e750e78d0a67527d0c83ae82f63e0fcf4ce39f6` |
| Slow | `404de9d6b28fde827157ce2d8f45253df7bb0996dfa5bf944ada5ebc4352603a` |

The production solution and standalone probe both build in Release with zero
warnings/errors. This is targeted startup evidence, not performance acceptance,
full ProTracker qualification or independent physical hardware validation.
ROMs, disks and copied external source remain uncommitted. The app ROM pin is
unchanged.
