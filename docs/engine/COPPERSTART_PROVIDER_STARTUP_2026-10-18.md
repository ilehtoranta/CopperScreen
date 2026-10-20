# CopperStart native Utility/Layers startup — 2026-10-18

The producer now boots real disk-loaded Utility V40 and owned Layers V52 on
the existing **512 KiB ROM / 1 MiB PAL OCS A500**. Native Exec/DOS perform
loading, AUTOINIT, provider Open/Close, allocation and library-list operations.
The loader retains the provider segment until reset because permanent Utility
keeps addresses into it. Automatic desktop startup remains separate.

The entry creates Utility only when needed, borrows existing compatible
providers, then installs Layers. Layers retains one graphics and one Utility
pin for its lifetime. Failure closes earlier pins and releases owned storage;
only a newly created, unused Utility owner is rolled back. Normal Layers Close
retains the owner, while deferred expunge completes on the final public Close.

The native replay covers two public allocation failures, real Exec rejection
of a temporarily invalid private AUTOINIT table, missing/incompatible providers,
three NewLayerInfo/Dispose lifetimes, repeated startup and expunge. All twelve
failure/reinstallation rows restore free memory and the library-list tail,
with provider pins balanced. MakeLibrary's internal allocation is not injected
by counting a third public AllocMem call.

Startup exposed a mapping error: this ROM reserves ExecBase before the
allocator's lower bound, so TypeOfMem excludes its valid embedded list sentinel.
Layers now recognizes the bounded live ExecBase structure. The old check failed
two valid cases in a five-case compiled regression; all seven final cases pass.
This fixes native validation and unlinking without granting allocation ownership.

The frozen combined replay passes **340/340**, with no skips: 263 compiled
instruction cases, 60 native ROM/media cases, 15 filesystem semantics, one
composition check and one payload compilation. The separate provider semantic
run passes **27/27**. The producer Release solution build passes with no warnings
or errors. The retained 24 display PNGs use separate caller-built Layer machines;
installed-owner drawing and refresh are not covered by those captures.

The HUNK is **223,116 bytes**, with 215,994 code/data bytes. The ROM hash remains
`DE0C6C8BE7395788081EF26E9B50FCC01DB912FD2BFA8A7924A7230F4979133F`;
842 contiguous tail bytes and the separate 27,854-byte earlier gap remain.
After Layers retirement, the module, Utility, 32 KiB probe stack and DOS remain:
243,920 Chip bytes and 548,416 total bytes are free, with largest blocks of
211,152 Chip and 301,640 slow bytes. These are post-expunge observations, not
live-window or peak-load headroom. Utility's retained allocation is 1,808 bytes.

Retained producer evidence is under
`CopperStart/artifacts/desktop-providers-20261018/qualified-v3`; exact hashes,
all startup rows and source/build provenance are in the
[machine-readable record](COPPERSTART_PROVIDER_STARTUP_2026-10-18.json).
The workflow and loader lifetime are documented in producer
`Docs/NativeDesktopProviders.md`. Earlier failed attempts are retained.
CopperScreen's shipping firmware pin is unchanged; this is producer evidence.

The next work is:

1. Create owned Simple/Smart Layers and draw text/rectangles through the installed
   CMLG owner, joining the startup and drawing paths in one native machine.
2. Run damage-restricted BeginUpdate/EndUpdate: restricted pixels, saved-head
   restoration, complete/incomplete updates, repeated BeginUpdate, foreign clips,
   allocation rollback and abort. The portable refresh core already exists;
   native transaction qualification remains open.
3. Prove Simple uncover redraw, Smart backing restore/scroll, staged native
   callbacks, stale-continuation rejection and contended lock wait/resume.
4. Install real Intuition screen/window and BeginRefresh/EndRefresh ownership,
   refresh events/replies, pointer/input, teardown and display restoration.
   Intuition's current ROM capability entry is still a fingerprint path.
5. Measure the complete GUI closure, stacks, bitmap peaks and allocation-pressure
   failures within the same two RAM banks. SuperBitMap and NeverBackfill remain
   separate qualifications. This work establishes no throughput result.
