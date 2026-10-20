# CopperStart Xenon 2 startup — 2026-10-20

Xenon 2 now passes the Cardinals crack intro and Band trainer, displays its
title/credits and reaches **Insert game disk** on the rebuilt CopperStart ROM.
Kickstart 1.3 reaches the same prompt with the same physical input. The supplied
collection contains only disk 1; gameplay and disk-two loading are unavailable
coverage.

The qualified source is `b78ddc09302cd506687327be1c1434891444cc4a`. The 524288-byte ROM has SHA-256
`83dea4b22e4db14358900eeb9f31ffc1eaf4607470b3d7c162919cf60df35826`. Its freshly compiled main is 475596 bytes;
native graphics is 32414 bytes. Independent native recomposition reproduces
the freshly compiled and linked ROM bytes exactly.

## Compatibility corrections

The intro follows Exec's successor twice to obtain graphics.library. Native
Kickstart publishes `exec → expansion → graphics`; CopperStart left Exec
unlinked, producing `$7FFFC` as the graphics base. Exec is now a named/versioned
member of its own library list, and the boot expansion library occupies its
classic position after Exec. Ordinary AddLibrary priority ordering is unchanged.
The obsolete second, minimal intuition.library installation is removed; reset
already installs the full native library.

With those links corrected, the title loaded but the two intros stayed black.
They write a borrowed hardware Copper list to `GfxBase.gb_LOFlist`. A permanent
guest graphics VBlank server now publishes that pointer to COP2LC and the
startup list to COP1LC. Its priority 10 matches the native control. The Chip
startup list waits for scanline 12 before COPJMP2, allowing the interrupt to
publish the pointer. LoadView maintains the same public pointer and hardware
split; blanking replaces the display list before its storage is released.
The server does not require an active View or CopperStart-owned list storage.

Publishing Exec's version exposed an older checksum-restoration defect: the
bootstrap sum began at the Library version instead of Exec SoftVer. It now
sums the native SoftVer-through-MaxExtMem range. The console uses the same
public list publication and restores its HIRES mode each field; teardown
retires its list before freeing it. Allocation failures and private ROM
overlap checks remain. No title recognition, engine timing change, new
caller-legitimacy check or host OS shim is introduced.

## Verification

| Coverage | Passed |
| --- | ---: |
| Focused Exec/bootstrap semantic checks | 22 |
| Emitted display and Intuition instruction checks | 24 |
| Fresh compilation and full-ROM boot cases | 146 |
| **Unique checks; no failures or skips** | **192** |

Four new library-link and caller-list regression scenarios fail on the prior
ROM and pass on the final image, with and without A501 RAM. The separate ten-case
focused run overlaps the final coverage. Twenty-one standalone-reader-root cases
and other unchanged instruction/filesystem cases were not rerun.

An intermediate console test observed a handle store before IsInteractive had
finished. It now waits for an explicit guest read-ready marker. The new BitMap
probe checks common depth/row/plane behavior without treating a version-dependent
flag byte as a cross-version requirement. Component display assertions now check
the startup pointer in COP1LC and the display pointer in COP2LC.

| Original media replay | Fields per mode | Result |
| --- | ---: | --- |
| Xenon 2, supplied Band/Cardinals disk 1 | 30000 | Intro, default trainer, title/credits and game-disk request; all **1505** capture files match normal/scalar. |
| Inside the Machine, DESiRE v1.0.1 regression | 18000 | Motherboard, title, effects, credits and closing DESiRE logo; all **905** capture files match normal/scalar. |

The comparisons include BMP, hardware/CPU state, Chip/slow RAM and PCM snapshots.
All captured unsupported-feature flags are null. Nonzero PCM occurs in 228 Xenon
and 125 demo snapshots per CopperStart mode; listening/fidelity is unassessed.
Fresh native controls reach the same bounded endpoints. Their scene timing,
glyphs and output hashes differ, so cross-ROM image equality is not claimed.

The machine is PAL OCS 68000, 512 KiB Chip plus 512 KiB slow RAM, no Fast RAM,
one read-only DF0 and the unchanged frozen Lightweight runner. These are
compatibility results, not hardware-conformance or throughput measurements.
Interlace, a complete graphics VBlank task owner, gameplay, saving and general
Workbench remain unqualified.

[The evidence record](COPPERSTART_XENON2_2026-10-20.json) preserves exact ROM, producer, media, input,
binary, test and capture identities. The physical workload is
[recorded here](../../CopperMod.Amiga.Lightweight.Runner/Workloads/xenon2-copperstart-startup-v1.json).
Local diagnostics/captures remain under `artifacts/copperstart-xenon2-2026-10-20`.
The initial Inside the Machine preflight used the previous rebuilt ROM and is
retained separately under `artifacts/copperstart-inside-2026-10-20`.
No ROMs, game media, build outputs or credentials are committed.

The Copper pointer behavior is checked against native execution and the
[Commodore Copper hardware reference](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_2.html).
