# CopperStart TD64 app and runner validation — 2026-10-19

**Passed: 7 full-ROM cases, 22 guest machine boots, no skips.** The app and
command-line runner were built against the updated embedded firmware contract
and tested with the newly validated TD64-only ROM.

| Artifact | Value |
| --- | --- |
| Qualification ID | `diskboot-2026-10-19-ffs-td64-frontend.1` |
| ROM SHA-256 | `D21E5223150A8837739E08ACC65AF52F483B5789C077A32E071F824BC14B7A86` |
| ROM size | 524,288 bytes |
| HDF fixture SHA-256 | `74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046` |
| Runner SHA-256 | `579171A2C54DE4BBDAD05BC91F1FAFD4AACCA22CBC18F34637B195D2553BD95A` |

The tests covered normal FFS file writes, guest flushing, saved profile and
last-session restoration, reset or runner restart, session replacement, and
readback from a fresh write-protected boot. App and runner cases also wrote to
partitions whose byte offsets cross and exceed 4 GiB, checked that the lower
alias remained unchanged, and read large files from an 8,650,752-block FFS
volume. Both read-only mutation controls passed.

The app runtime observed successful guest `CMD_UPDATE` before host save and
verified the backing image while it was still open. The command-line process
verified the HDF after guest `CMD_UPDATE` and exit, restarted against the same
writable image, and verified readback from a fresh protected process. All seven
tests ran in the normal app/runtime and runner paths. These are source build
checks; signed installed-package coverage remains separate.

The ROM retains TD64 and omits the NSD retry path. The matching producer proof,
including explicit and RDB full-ROM tests and 4.125 GiB large-file writes, is
recorded in
`../CopperStart/Docs/NativeFfsDependencyValidation.md`. This app follow-up did
not change the ROM, create a package, or install the firmware.

Local receipts and build output are under
`artifacts/copperstart-td64-frontend-20261019/`. The machine-readable
`qualification.json` records all 7 outcomes, all 22 boots, the pin and fixture
hashes, front-end boot counts, flush/fresh-readback assertions and the runner
hash. `frontend-writes.trx` contains the individual test results.
