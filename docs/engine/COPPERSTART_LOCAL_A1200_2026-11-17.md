# Local A1200 CopperStart preview — 2026-11-17

This is a local development preview, not a replacement for the historical
Full-filesystems qualification or a firmware release. Production bundle pins
remain unchanged. Media and build outputs remain ignored local artifacts.

## ROM build

CopperStart's A1200 reset build now uses the existing `on+generated+frame`
compaction policy, also used by its Full extension. The compiled reset payload
fell from 497,158 to 471,468 bytes. Graphics remain at `$FF9600`; the linked
initializer/kernel checks remain active, with 23,506 bytes between compiled
reset payload and the first graphics fragment. Both banks retain their
524,288-byte size and valid checksums. The combined file is extension first.
Build scripts explicitly select their consumed .NET 10 target, allowing the
installed .NET 10 SDK to build the dual-targeted CopperSharp tools.

| Artifact | SHA-256 |
| --- | --- |
| Reset | `54dc71db018eda977fbc64671e89759780b94c9197d129600adc8e810b509a56` |
| Full extension | `c35390d245a74346366d068c210b4a5abac859eeee76092481168e8e9d445f71` |
| Combined A1200 | `3e65ea2d8a0d47b4f427ed720e1bfedd9df3930a8711c7dde173d1a1753b66ff` |

Producer receipts and frozen managed inputs are retained in CopperStart's
`artifacts/a1200-fit-20261117`. The final pair/manifest was composed with the
builder's composition tail after completing the extension separately.

## Bootstrap ABI

Current production CopperStart requires the documented CopperMod bootstrap
ABI 1 before native Exec/DOS startup. Lightweight now recognizes the guest's
explicit `FF00 43534731` stub at CHIP `$780` through its existing CPU gateway
boundary. It checks the supervisor/ROM return context and request, returns D0,
and writes only the 16-byte reply at `$770..$77F`. OS execution remains in ROM.
The handler has no persistent state, per-cycle polling, diagnostics or counters;
each cold/external reset is followed by a freshly prepared guest request.
Unrecognized tokens retain ordinary line-F behavior. No CopperMod source
reference or Copper68k package change was introduced.

The protocol authority is CopperStart's `Docs/A500EmulatorBootstrap.md` and
CopperMod's `CopperMod.Amiga/CopperStartBootstrap.md`, with its existing companion
handler as implementation reference. Eighteen new diagnostic tests cover bounded
reply writes, invalid contexts, interpreter 68000/EC020/040, 040 JIT, fallback,
stack restoration, reply visibility and reset.

## Initial desktop and limits

The currently pinned `exec-preemption.10` seed was unavailable locally. The
user's unchanged `redraw.5` desktop HDF still matched its original bundled
seed identity (`ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc`).
Independent boot checksum, exact tree, file payload, ownership and bitmap checks
pass. A private copy is used; the personal original remains unchanged.

Consumer artifacts live under `artifacts/a1200-preview-20261117`. `Preview.targets`
embeds explicit local contracts and checks both hashes, while ordinary builds
retain the production contracts. `Launch.ps1` reopens the PAL Alice/Lisa EC020,
2 MiB Chip, no slow/Fast RAM preview with its private writable HDF.

The CopperStart Release solution and ordinary/preview CopperScreen Release
solutions build. Three existing ROM layout tests and eighteen bootstrap tests
pass. Native desktop boot reaches Home at 584 fields (11.6986 guest seconds),
within the existing 850-field bound. The full desktop test **fails** its Tab
redraw check with this older seed; interaction/reset/reopen checks after that
assertion are not established. The historical timing bounds are unchanged.
The displayed Home footer still describes its historical A500 build; the host
profile and running CPU are A1200 EC020. No host-throughput acceptance is claimed.

The combined bootstrap, CopperHDF, host-packet and CPU-upgrade diagnostic selection
passes all 86 cases without skips. A rerun of the producer's Full-filesystem
native matrix discovered zero cases after its managed rebuild, including with an
explicit adapter path; that matrix is unavailable coverage, not a passing replay.
Live Home is visible, but an Editor shortcut did not change views either.

## Keyboard/input repair

The initial fallback used the obsolete `redraw.5` GUI with the current
interrupt-driven Exec. A bounded native trace confirmed that hardware input
was arriving: Tab make/break (`42 C2`) advanced the console's raw head from
zero to two, while its tail stayed zero. The old GUI had replaced Exec's
Schedule target with its cooperative input trampoline. The application remained
waiting while the GUI never consumed those bytes.

Rebuilding GUI and Home together from maintained current sources fixes the
integration. The current GUI wakes its input task from VBlank and drains the
FIFO through the native signalled worker. Home is compiled against that GUI's
exact text-entry export. The compiler reports no managed allocation sites,
runtime helpers or unresolved native targets for either guest payload.
No keyboard timing, engine scheduling or host OS shim was changed for this
repair. The original desktop and private workspace remain available separately.

The frozen provider inputs, CLI arguments, export map, generated Home profile
and builder are retained under CopperStart's `artifacts/desktop-input-20261117`.
The rebuilt seed is independently checked for boot checksum, exact filesystem
tree, payloads, block ownership and allocation bitmap before embedding.

| Payload | Bytes | SHA-256 |
| --- | ---: | --- |
| GUI | 395,252 | `d052ab6fcaf20a21e0d64283710af4e7153329f70a77d314389bd3beef2c84f8` |
| Home | 38,048 | `e9bd3c5408ca036666fd5c3f71ec58b37bacbeb6e31fa6dd169145604d2d961a` |
| Launcher | 652 | `dff622a94d098768feee10ba5e9b79df21c320caf9ef490eeace7c46ce9725d1` |
| HDF | 8,388,608 | `128ef80e657a1a71d34f918495535326fdc2e0cb8c4e7980de2b39280b5fd926` |

`BundledA1200DeliversKeyboardAndMouseInput` passes the complete native run:
visible Tab selection, Editor shortcut, lowercase and Shift-modified text,
Backspace, reset, physical relative-mouse activation of Editor and fresh reopen.
The live desktop independently opens Editor and accepts shifted text through
the host keyboard path. Ordinary and preview Release solutions build without
warnings or errors.

The current GUI includes more API families than the historical compact image.
Its cold readiness is 1,004 fields (20.1120 guest seconds); Tab redraw completes
in eight fields. The original 850-field compact startup bound remains unchanged
and **fails** for this larger image. The new input-behaviour run is separate
coverage, not a reclassification of that timing failure or host performance
acceptance. Captures and TRX results are under consumer
`artifacts/a1200-preview-20261117`.

`Launch.ps1` now uses `workspace-input-fixed/CopperOS.hdf` and the verified
1 MiB ROM at `C:\Data\CopperStart\CopperStart-A1200.rom`. Its SHA-256 remains
`3e65ea2d8a0d47b4f427ed720e1bfedd9df3930a8711c7dde173d1a1753b66ff`;
the input repair changes the disk-loaded GUI/Home pair, not the ROM bytes.

## Compact startup investigation

The input-correct image above is the frozen reference in
`artifacts/a1200-startup-20261117/reference`. The bounded, diagnostic-only
`scripts/probes/CopperOsBoot` runner measures guest cycles and attributes them
to the retained native maps. Its normal-control and instrumented runs must
match completed fields, cycles, CPU registers, Chip RAM, pixels and PCM.
These are guest-work measurements, not host-throughput acceptance.

The reference reaches readiness at 982 fields (19.671314 guest seconds).
The existing native acceptance test observes it at 1,004 fields because it
checks readiness in 20-field intervals and then renders four settling fields.
Its largest mapped costs include native memory clearing, TLSF boundary walks
and DOS segment loading/relocation; unused rooted GUI bytes still incur these
loader/allocation costs.

CopperStart's `build/Build-CopperOsGui.ps1` now explicitly selects the 14 named
indirect roots needed by the disk GUI. It retains the signalled input/reply
worker, text/keymap, VBlank and beep entries and reachable installed API
vectors. The broad build's separate boot-service installers were unnecessary
roots for this payload. Home must be rebound to each GUI's exact hash and
text-entry offset before composing and independently checking its HDF.

The roots-only intermediate reduces GUI size from 395,252 to 331,892 bytes.
It reaches readiness at 854 fields (17.107232 guest seconds), with exact
control/profile parity and the same rendered Home pixels as the reference.
Its native input/reset/reopen replay passes, but its native startup test
**fails** at 864 fields. This is retained failed evidence, not acceptance.
The 850-field startup and 17-field redraw limits remain unconditional.

An attempted frame-initialization policy was found to be ROM-only and was
stopped without using its output. The next profile instead selects the
existing HUNK-eligible `ElideUnusedRegisterArguments` compiler pass. No engine
timing, input delivery, polling delay or ROM bytes are changed by these builds.

That intermediate GUI is 329,348 bytes; readiness improves only to 852 fields
(17.067168 guest seconds). Control/profile parity and reference Home pixels
still match. The unchanged acceptance test again **fails** at 864 fields,
before reaching its input assertions. Its receipt is
`test-results/compact-startup-input-v2.trx`; input success is not inferred
from this failed run. The next bounded change enables the existing
HUNK-compatible exact terminal-sequence and identical-method sharing passes.

That sharing experiment is 322,276 bytes, but remains at 852 exact / 864
acceptance-test fields. Its control/profile state and reference Home pixels
match. The HUNK has 4,033 relocations versus 3,976 before sharing: smaller code
alone did not reduce this loader workload. It is rejected, with its failed
`compact-startup-input-v3.trx` retained. The broad reference and roots-only
images have 5,753 and 3,999 relocations respectively.

The following profile removes the unhelpful sharing passes and selects
`ClusterInternalCalls` instead. It targets absolute call fixups by arranging
related methods for the compiler's existing signed-word relative calls.
The compiler's freestanding-entry and relocated-call-range fixtures document
the required entry-prefix and signed-range behavior; no code generator or
loader semantics are changed here.

## Clustered GUI result

The selected GUI is 323,640 bytes with 3,211 relocations: 765 fewer than the
unused-argument intermediate and 2,542 fewer than the broad reference.
Generated code inspection confirms actual absolute-to-relative call changes;
for example `RenderCheckbox`'s call to `NativeIntuitionDrawing.Border` changes
from a six-byte absolute JSR to a four-byte BSR.W without a loader fixup.
Home is rebound to text entry 307,834 and the exact new GUI identity.

Cold readiness is **799 fields / 16.005478 guest seconds**, versus the frozen
reference's 982 / 19.671314. Control and profiler agree exactly on completed
fields, cycles, registers, Chip RAM, pixels and PCM. Home pixels also match the
reference. Frozen inputs, both profiles, explicit MSBuild hash pins and media
verification are retained under `artifacts/a1200-startup-20261117`.

| Payload | Bytes | SHA-256 |
| --- | ---: | --- |
| GUI | 323,640 | `5400dc3efff6221500750dd812dfbf67c8f415c6dddd261353c9ddbc1011a83e` |
| Home | 38,048 | `6ccc220c92ec86816564609876fe4914bbbb1495bd1e37c7a819a6ef9f8ae7ce` |
| Launcher | 652 | `3db6495dc94f6bc69378c68f9f4a0f71c1f6c6668284099acb57a0023f7d06f8` |
| HDF | 8,388,608 | `470364db67c6bdcc535a572de2cac6ce07c34d140293d3f915225256ba159730` |

The original boot checksum, exact tree/payload/protection, block ownership and
allocation bitmap checks pass independently. The producer hash remains
`fc8cceed2450caa01c8d87f1678ad30fc58a5d8fe6634819d3423bd3bbd250e9`.
The ROM remains the verified 1 MiB image in `C:\Data\CopperStart`.

The ordinary CopperScreen Release solution builds with zero warnings/errors.
CopperStart's ordinary output build encountered files locked by concurrent
existing ExecProbe processes. Its complete Release solution succeeds with
isolated outputs (`--artifacts-path artifacts/a1200-startup-isolated-build-20261117`,
`TargetFrameworks=net10.0`), with 27 existing warnings and zero errors. Neither
result is a host-throughput measurement or a firmware/package release.

The unchanged combined `BundledA1200BootsNativeDesktop` test **passes**, with no
skips: startup is **804 fields** (16.105637 guest seconds, limit 850) and Tab
redraw is **8 fields** (limit 17). It then verifies visible selection, Editor,
lowercase and Shift text, Backspace, reset, relative-mouse Editor activation,
persistent-storage flush and fresh reopen. Timing assertions are unconditional;
no separate timing-bypass test remains. The receipt is
`test-results/compact-startup-input-v4.trx`, and captures/timing JSON are in
`desktop-tests/bin/CopperScreen.Lightweight.Tests/release/artifacts/desktop-boot-capture`.

`artifacts/a1200-startup-20261117/Launch.ps1` starts this exact enabled preview
with a new private `workspace-compact/CopperOS.hdf`. The preceding preview's
`Launch.ps1` forwards to it; its old launcher is retained as
`Launch-input-fixed.ps1` and both earlier workspaces are preserved. The verified
seed is also copied to `C:\Data\CopperStart\CopperOS-A1200-compact-20261117.hdf`.
Production firmware pins and the default disabled feature remain unchanged.
The launched preview visibly reaches Home under the A1200 Alice/Lisa EC020
profile. Its historical guest footer still describes the older A500 image;
the host title/configuration and native machine assertions identify this run.
