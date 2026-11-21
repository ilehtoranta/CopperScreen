# Local A1200 desktop and boot artwork correction

This follows the [local A1200 build and input/startup record](COPPERSTART_LOCAL_A1200_2026-11-17.md).
The Home footer now reads `A1200 | 1 MiB ROM | 2 MiB Chip RAM`; its explicit
native text length is 34. The text fits before the Console button.
`CopperOS/src/System/Desktop/HomeView.cs` owns this fixed A1200 profile label.

`CopperStart/src/CopperStart.Devices/A500BootScreenCore.cs` retains the floppy's
write-protect window and omits the opposite HD density hole. The shared boot
artwork consequently depicts DD media. Drive capabilities and density detection
are unchanged. The existing artwork test now checks the absence of that hole.

Only guest drawing/text changes are intended. The compact GUI is reused unchanged
from `desktop-startup-cluster-20261117` (323,640 bytes, SHA-256
`5400dc3efff6221500750dd812dfbf67c8f415c6dddd261353c9ddbc1011a83e`).
The reset-ROM rebuild uses the previous frozen OS managed inputs with the rebuilt
Devices assembly; the filesystem extension is retained unchanged. No emulator
input path, hardware timing, bootstrap ABI or host OS shim is changed.

## Evidence

Producer inputs and scripts are under CopperStart's
`artifacts/a1200-artwork-20261118`; consumer inputs are under the corresponding
CopperScreen artifact directory. The previous ROM/HDF pair is retained as the
reference. The compiler is frozen with SHA-256
`e0eb3212def8e5293b1837fb5dec1ef70fb145d2fdbf7cc0704982c744cea50b`.

- Ordinary CopperScreen Release solution: zero warnings/errors.
- Complete CopperStart Release solution with isolated outputs and
  `TargetFrameworks=net10.0`: 11 existing warnings, zero errors.
- The focused VSTest command discovered **zero** cases. Its successful process
  exit is unavailable coverage, not a passing test result; the TRX is retained.
- All seven existing managed artwork cases were then invoked directly, with
  their real assertions and the exact updated Devices assembly. All pass.
  `managed-artwork-checks.json` records the method/case list and execution mode.
  The resulting raster preview visibly shows the DD silhouette with one window.
  These are managed drawing checks, distinct from native ROM/media replays.

Native artifact identities and replay results are recorded after the rebuild.
Neither these checks nor short native captures are host-throughput acceptance
or physical-A1200 timing measurements. This is a local preview, not publication.

## Rebuild dependency correction

The first reset-bank candidate (`54cf00fc...`) is rejected. Its native empty-drive
run corrupts execution before the desktop; the desktop replay was terminated
after that failure was established. It was never copied over the working ROM.
Managed IL comparison found an unintended second change in
`NativeTrackDiskDevice.WriteVector`, which embeds the Exec ROM gate offset.
The Devices project had been rebuilt against current Exec while the reset OS
inputs were frozen to the earlier ABI.

`FrozenDevices/FrozenDevices.csproj` instead compiles the same maintained Devices
sources against the exact frozen Exec/SDK references. All 549 method bodies
were compared: **only `A500BootScreenCore.Initialize` differs** from the working
Devices assembly. The trackdisk wrapper and the other 548 bodies match again.
The seven managed drawing checks also pass with this frozen-dependency assembly.
The corrected native build uses this bounded replacement; failed inputs/results
remain separately retained.

The previous ROM's common artwork was also executed natively using a bounded
diagnostic that invalidates bootstrap reply **data**, intentionally exercising
the ROM failure-artwork branch. Its density window has palette index 1 and its
write-protect window has index 3. This diagnostic does not modify instruction
bytes or production hardware behavior and is separate from normal desktop boot.

## Corrected native candidate

The selected reset rebuild is under CopperStart's
`artifacts/a1200-artwork-frozen-20261118`. Its filesystem-extension bank, native
graphics code, preceding kernels, initializer and graphics layout match the
working reference byte for byte (`unchanged-banks.json`).

| Payload | Bytes | SHA-256 |
| --- | ---: | --- |
| Combined A1200 ROM | 1,048,576 | `4b2d907b0dd9ba5fcb50fa6d7227c28251f5f9a1e10c006a6d79d7d765db9615` |
| Home | 38,056 | `6f7edfe259b386a6a73eb26d8dab86d58350c26a17623d98aebc9729d2f3a79c` |
| HDF | 8,388,608 | `401f265c7290a2dddc08d5974206ebc3aa04d0e03d196b27dad29190cd43ce83` |

Home is bound to the unchanged GUI hash and native text entry 307,834.
The independently verified HDF tree, payloads, boot checksum, block ownership
and allocation bitmap pass; the receipt is `candidate-v2/image-verification.json`.

The corrected ROM passes the native common-artwork diagnostic. The density
window's palette index becomes 0 while write protection remains 3. Comparing
both full planar bitmaps yields exactly four changed bytes (offsets 6,184,
6,224, 6,264 and 6,304), each changing `F3` to `03`: precisely the 4×4 HD hole
is removed, preserving the outline bits. Every other bitmap byte is identical.
`native-artwork-comparison.json` records this result. Earlier blank empty-drive
captures and failed diagnostic attempts are not reported as image validation.

Normal supported-bootstrap HDF boot reaches readiness at **799 fields /
16.005478 guest seconds**, matching the preceding compact image. The standalone
public-execution control is retained in `final-control/profile.json`.

The unchanged combined `BundledA1200BootsNativeDesktop` native test **passes**:
one case, zero failures, zero skips (`test-results/artwork-frozen-startup-input.trx`).
Startup is **804 fields** / 16.105637 guest seconds (limit 850); Tab redraw is
**8 fields** (limit 17). The same replay verifies selection, Editor, lowercase
and Shift text, Backspace, reset, relative-mouse activation and fresh reopen.
The actual desktop capture visibly shows the complete corrected footer, fitting
comfortably before Console.

The corrected ROM is installed at `C:\Data\CopperStart\CopperStart-A1200.rom`;
the preceding ROM is retained as `CopperStart-A1200-before-artwork-20261118.rom`.
The seed is `C:\Data\CopperStart\CopperOS-A1200-artwork-20261118.hdf`.
`artifacts/a1200-artwork-20261118/Launch.ps1` uses a new private writable
`workspace-artwork/CopperOS.hdf`. Previous launch paths forward to this preview,
and the saved earlier launcher uses the retained earlier ROM. Earlier user
workspaces are preserved. No shipping firmware pins or package versions change.
The launched updated A1200 preview visibly reaches Home with the complete new
footer; its ROM/HDF copies were hash-checked after installation.
