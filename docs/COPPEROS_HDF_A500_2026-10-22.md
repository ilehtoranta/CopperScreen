# CopperOS HDF boot on the supported A500

CopperOS's original Shell system now boots from an 8 MiB DOS1 FFS HDF with
no floppy inserted. The machine is the existing standalone CopperStart A500:
PAL OCS, 68000, 512 KiB Chip plus 512 KiB slow RAM. The start menu and shipped
workspace seed are unchanged. A1200 and graphical desktop qualification are
outside this change.

## Implementation

The CopperOS bootstrap opens `L:Shell-Seg` through public native DOS vectors.
HDF reads stream through a 4 KiB buffer, avoiding a second whole-file allocation.
The original generated DD DF0 image retains its track/table-cached reader: the
bootstrap compares the opened file's handler port with the public DF0 DOS device
entry before choosing it. Native DOS calls pass the library base explicitly.
The native ROM and emulator hardware required no changes.

`CopperOS/tools/ImageBuilder/build_system_image.py --format hdf` builds the
deterministic partition image, with its root at block 8192 and five allocation
bitmap blocks. It records the explicit mount geometry `DH0:2:32:0:255` in its
receipt. This is a raw FFS partition HDF, without an RDB. ADF remains the default.

The proof scripts also enforce HUNK entry at CODE offset zero. The current
compiler's size optimization is disabled for the small BootShell, Demo and
Storage programs because it can relocate their entry; the large Shell retains
size optimization and passes the same entry check.

## Verification

| Coverage | Result |
| --- | --- |
| Host image serialization | 8 Python tests passed, including deterministic HDF/ADF output, independent filesystem readback, multiple bitmaps, payload beyond floppy capacity and corruption rejection. |
| Host app settings | 16 focused hard-disk settings cases passed. The older optional qualified-HDF fixture replay was excluded from this run. |
| Compiled MC68000 programs | Shell-Seg, BootShell, Demo and Storage passed freestanding 68000/no-FPU compatibility gates and zero-entry checks. |
| Full native ROM/media execution | 3 test cases, 4 complete boots passed: writable HDF, fresh session reopening that HDF, pristine read-only HDF, and ADF compatibility. No skips. |
| Saved HDF readback | An independent reader verified the exact file tree, payload hashes, disjoint block ownership and full allocation bitmap after the native writes. |
| Production solution | `dotnet build CopperScreen.slnx -c Release --artifacts-path artifacts/hdf-a500-build -m:1` passed with zero warnings/errors. |

The full native cases use the standard Lightweight app session and real keyboard
events, observe native console pixels, and check faults/unsupported features.
Each boot exercises Alias, an external Demo command, return to Shell and recovery
after an unknown command. HDF tests additionally exercise native settings/data
creation, rename/delete, relative file and directory operations, and DOS write
protection. Cold reopen disposes the complete writing session before creating a
new machine. Its disk hash remains unchanged after readback. No guest memory or
host filesystem calls are used to implement guest commands.

Final combined native/settings run: **19 passed, 0 failed, 0 skipped** in
`artifacts/hdf-a500-20261022/native-boot-verified.trx`.

## Local artifacts and launch

The final system is under the sibling repository's ignored directory:
`../CopperOS/artifacts/hdf-a500-20261022/system-v4/`.

- `CopperOS.hdf`: pristine 8 MiB system image.
- `CopperOS-Workspace.hdf`: separate writable copy for use.
- `CopperOS-A500.json`: normal app profile pointing to that copy and the qualified ROM.
- `Start-CopperOS.ps1`: launches the current local build with the profile.
- `hdf-image.json`, `native-manifest.json`: build identity and inputs.
- `persisted-filesystem-verification.json`: independent persisted-disk verification.
- `app-proof/20261021T215250006Z-hdf-write/`, `20261021T215308912Z-hdf-reopen/`,
  `20261021T215325663Z-adf/`, `20261021T215408791Z-hdf-readonly/`: final captures,
  native-state observations, binary hashes and result records.

From the CopperScreen checkout:

```powershell
& ../CopperOS/artifacts/hdf-a500-20261022/system-v4/Start-CopperOS.ps1
```

Alternatively use the existing CLI with your copy of the HDF:

```powershell
dotnet artifacts/hdf-a500-build/bin/CopperScreen/release/CopperScreen.dll --copperstart --rom '<qualified CopperStart-DiskBoot.rom>' --hdf-ffs-writable '<CopperOS.hdf>' DH0:2:32:0:255
```

Use `--hdf-ffs` for read-only operation. This uses the separate CopperStart ROM
file, not a Kickstart ROM embedded in the HDF. Rebuild instructions and native
test environment variables are in `CopperOS/tools/ImageBuilder/README.md`.

| Input/output | SHA-256 |
| --- | --- |
| CopperStart ROM (`diskboot-2026-10-19-ffs-td64-frontend.1`) | `d21e5223150a8837739e08acc65af52f483b5789c077a32e071f824bc14b7a86` |
| Pristine HDF | `54c6f78a7480437069b66a5f913c5ccde76fc3e604babeb1dc22674360242175` |
| Written/cold-reopened HDF | `590301973add58f7059ad708f20ac7c65dabe9708e344e1b30884f9a8a722a91` |
| ADF compatibility image | `a6c558dffcbbdeaacce46ce3c3a4154075e090b910584764c3c8c9efe0d9f9cf` |

Build receipts remain nonshipping; dated runtime records contain the observed
qualification. No ROM, media, local artifacts or package releases are committed.
