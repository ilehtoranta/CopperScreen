# Copper68k 1.5.3 package upgrade — 2026-10-08

The former Copper68k 1.5.1 pin rejected valid 020 absolute-memory instructions
used by CopperScreen's original boot firmware: MOVE.L between absolute addresses,
TST.L, CLR.W and immediate byte/word/long arithmetic. Rewriting the firmware to
avoid these forms concealed missing CPU support and changed the A500 instruction
stream. The CPU package boundary now addresses that defect.

CopperScreen, Lightweight and the headless runner pin the already published
[Copper68k 1.5.3](https://www.nuget.org/packages/Copper68k/1.5.3). It includes the
required instruction support. The original firmware MOVE/TST/CLR/ADDI/SUBI forms
are restored, including interrupt/Forbid nesting and trackdisk bookkeeping.
The A1200 reset/frame and 2 MiB memory specialization remains intact.

The dependency is restored from NuGet.org. No sibling source reference, private
feed, replacement package under an existing version, or new publication is
involved. All affected production, diagnostic and probe lock files are refreshed;
the production solution also restores successfully with `--locked-mode`.

## Verification

- Release production solution build: zero warnings/errors.
- Ten public-package instruction cases check values, operand widths, flags,
  source/adjacent-memory preservation and complete extension-word consumption.
  Nine fail with 1.5.1; all ten pass with 1.5.3.
- Host suite: 206 passed, six optional native cases skipped, one existing Linux
  destination-lock failure. That same save test fails on unchanged `HEAD`.
- CopperDisk: 74 passed. Lightweight diagnostics: 1,080 passed, using the separate
  `artifacts/diagnostic-tests` output directory.
- Both restored firmware streams match vasm 2.0f byte for byte; sizes/hashes are
  recorded in [the minimal boot contract](MINIMAL_DISK_BOOT.md).

The host run includes all 43 synthetic boot fixtures for A500/A1200: disk DMA,
second-stage library calls, allocation, Supervisor/exception frames, reset/disk
swap, fault reporting, interrupt nesting, settings persistence and AGA output.
The package tests exercise the A1200 68EC020 profile directly, independently of
the firmware. They retain a regression check even if the firmware changes later.

Native ROM/media replays were unavailable and were not repeated. Existing dated
gameplay evidence describes its original package inputs, and this upgrade is
not a new gameplay, throughput or physical accelerator timing qualification.
