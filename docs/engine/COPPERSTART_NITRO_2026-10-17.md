# CopperStart: Nitro boot compatibility

The later [race-start follow-up](NITRO_RACE_START_2026-10-17.md) repairs the wait
with CPU and interrupt-sampling corrections. This record retains the original
boot-fix ROM, package and results below.

Started 2026-10-16; completed 2026-10-17. **LWA-NATIVE-011.** Nitro exposed
two compatibility defects: CopperDisk rejected its valid extended ADF, and
CopperStart handed control to the boot block with stopped CIA event counters.
Both are corrected. The fresh source-built ROM reaches player registration,
car selection and the city introduction. Racing remains unverified: native
Kickstart and corrected CopperStart subsequently wait on the same VBlank request.

The [machine-readable record](COPPERSTART_NITRO_2026-10-17.json) identifies media,
ROMs, frozen inputs, consumer binaries, replay manifests and test results. Local
artifacts are under `artifacts/copperstart-nitro-2026-10-16/`; ROMs, media and
generated captures are excluded from Git. The committed
[input script](../../CopperScreen.Lightweight.Tests/Workloads/corpus-nitro.json)
is the exact script used for the final replays.

## Extended ADF admission

The supplied Nitro archive contains a modern `UAE-1ADF` image with **166 tracks**,
including **19 unformatted raw tracks**. The previous decoder required exactly
160 tracks and rejected raw bit lengths of zero, before guest execution.
CopperDisk now accepts 1–168 declared tracks within the existing drive's cylinder
0–83 range. It retains declared physical tracks, raw orientation and exact bit
lengths. Omitted tracks are unformatted, padded to at least 160 tracks and a whole
cylinder. Zero-bit raw tracks are unformatted even with reserved backing bytes.
The decoded sector view remains 880 KiB; actual payload and drive limits remain
checked. The [WinUAE format implementation](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/disk.cpp)
provides the independent format reference.

Four synthetic cases fail against the preceding decoder. The correction passes
these and two further partial-cylinder cases, covering extra normal/raw tracks
and empty/padded unformatted entries. No engine cycle ordering changes.
Consumer fixes: `7811e1b`, `d7ca8a4`.

## Running CIA counters at handoff

Nitro reads CIAB's 24-bit TOD counter and checks an elapsed interval against
`$120`. Both counters remained stopped after reset in the original fresh ROM;
the zero delta sent the game into its white-screen error loop at PC `$866`,
SR `$2700`. CopperStart now writes a byte to CIAA TODLO (`$BFE801`) and CIAB
TODLO (`$BFD800`) before disk boot handoff. The
[8520 hardware documentation](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_f.html)
defines low-register writes as starting the counter; CIAA counts vertical sync
and CIAB counts horizontal sync. There is no game-specific path or chipset
timing change.

The producer [source fix and regression](https://github.com/ilehtoranta/CopperStart/pull/4)
target `ci/github-actions-build`, the source branch used here. Commits `8fb0c89`
and `8c8e53f` contain the startup correction and final regression. The original
boot-block fixture calls Exec SuperState, masks interrupts, samples both counters
across three PAL beam wraps and checks field/line advancement. Wrap detection
reads only VPOSR V8, avoiding a torn two-register sample. Both 0 and 512 KiB
slow-RAM cases fail on the original ROM with zero deltas; both pass on native
Kickstart 1.3, the diagnostic correction and the final source-built ROM.

## Fresh ROM provenance

The initial fresh build uses producer base
`50f8583a40d24c34a06d4bed71ab31db19d47b95` plus the recorded source snapshot.
Its 524,288-byte ROM has SHA-256
`F37E8E47521661A125B1C9F129FD0DC5E7584980ABBAF25FC5E49066C1DA78BD`,
456,528 code bytes and 26,098 graphics code bytes.

The corrected **source-built** 524,288-byte ROM has SHA-256
`5C5C0ADFBC4E12A59ECA2DD0236C40EFC633B71AB34190E88418F36BEA4FF4C9`,
456,574 code bytes and the same 26,098 graphics code bytes.
Concurrent producer edits were excluded by freezing the initial managed inputs.
The complete corrected `NativeDiskBoot.cs` was compiled under a qualification
namespace with the same bootstrap call sequence and selected unchanged production
export bridges. The frozen System ROM DLL supplies unchanged DOS, console and
graphics-install bodies; compiler, SDK, Exec, devices, DOS and graphics emitter
remain frozen. This generates a complete new MC68000 ROM and composes graphics.
The manifest hashes the inputs and additional qualification root.

This is a bounded frozen-component qualification, rather than a clean build of
the producer's later working tree. The earlier reset-vector diagnostic ROM is
identified separately and excluded from final qualification. No application
firmware-contract promotion or package publication is included.

## Replay results and open boundary

All runs use PAL OCS A500, 68000 interpreter, 512 KiB Chip plus 512 KiB slow RAM,
no Fast RAM or RTC, and one read-only DF0. Each final run completes 14,000 fields
with captures every 200 fields, including field zero.

| Check | Observed result |
| --- | --- |
| Original fresh CopperStart | White error loop, PC `$866`; all 355 batch/scalar capture files match. |
| Corrected source-built CopperStart | Registration at fields 5,000–6,000, sports-car selection at 8,000, city introduction at 9,000; all 355 batch/scalar image/state/RAM/PCM files match. |
| Native Kickstart 1.3 | Same menu/city progression and later wait. Earlier normal/scalar controls match all 284 binary captures; diagnostic JSON has extra fields and is excluded from that comparison. |
| Lotus 2 before/after | Both reach the first Forest Course race and respond to driving input. All 71 images and 71 PCM captures match exactly across the firmware change. |

At field 14,000 both native Kickstart and corrected CopperStart are at PC
`$184B0`, SR `$2004`: a branch back to the VBlank INTREQ-bit poll at `$184A8`.
The guest interrupt handler also clears that request. Diagnostic CPU state is
neither halted nor stopped. A timing race is a possibility, but its cause has
not been established independently; no speculative timing change is included.
Full Nitro racing, game completion and audio fidelity remain unverified.

An independent WinUAE run is unavailable: automatic approval review rejected
launching it with the reason **"blocked by policy"**. No reference execution is
claimed. Capture completion and ordinary runner rates are not host-throughput
acceptance measurements.

## Validation

The production CopperScreen Release build passes with zero warnings/errors.
CopperDisk reports **80 passed**; separate engine diagnostics report **1,245
passed, seven optional native skips**; host tests report **195 passed, six
optional native skips**. Skips are unavailable coverage, not replay passes.
The isolated producer development-branch solution builds using the existing local
g20 SDK feed, zero errors and one pre-existing compiler nullable warning.

The frozen producer's 55-case native suite reports **54 passed and one failed**
on both original and corrected fresh ROMs. The retained failure expects
`IOERR_NOCMD (-3)` for CMD_WRITE, whereas the frozen development devices now
support writes and return documented `TDERR_WriteProt (28)` on this read-only
disk. Independently invoking the unchanged native fixture with expected error
28 passes on both ROMs, including zero `io_Actual` and idle DMA assertions.
Original failed TRX files remain unchanged; the suite is not reported as fully
green. The committed producer base predates write support, so its separate
NoCommand expectation is not changed by this CIA fix.
