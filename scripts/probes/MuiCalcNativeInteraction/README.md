# Native MUI Calc mouse diagnostic

This standalone probe uses the existing Release runner's engine, Copper68k and
CopperDisk assemblies. Build the runner first; the probe deliberately references
those frozen binaries instead of rebuilding the engine from a moving worktree.
It records the engine/CPU hashes, ROM hash and private image hash in its result.
No ROM or HDF belongs in git.

Build with `dotnet build scripts/probes/MuiCalcNativeInteraction/MuiCalcNativeInteraction.csproj -c Release --artifacts-path artifacts/mui-calc-native-interaction-v4`.
Run the resulting DLL with five arguments: ROM, prepared MUI Calc HDF, a new
output directory, `hdf-ro` or `hdf-ffs`, and the maximum field count.

The `mui-calc-native-mouse-v4` protocol uses the separate
`a500-ocs-5m-diagnostic-v1` configuration: PAL OCS 8371/8362, 68000 interpreter,
production conservative loop batching, 512 KiB Chip + 512 KiB Slow + 4 MiB Fast,
908-pixel raster, read-only media, no host-directory volumes or OS services.
It is a correctness diagnostic and supplies no throughput acceptance result.

The reader observes the development muimaster.library 4.1 private ABI documented
by CopperOS's HeadlessRecords, AreaGeometryRecords and native root/owner codecs.
It checks the library header and geometry cookie, bounds list traversal, and
uses the machine's actual Fast RAM mapping. Update the reader if that ABI changes.
It performs no guest-memory writes, guest-method calls, or synthetic event
injection. The only interaction is `SubmitInput` through the emulator's hardware
input path between ordinary `ExecuteFrame` calls.

After Window.Open returns, the probe finds visible Text buttons inside that
window by their labels and layout rectangles. It aims the mouse using the real
Intuition Window mouse coordinates, presses/releases `5`, `Enter`, `6`, `Enter`,
`+`, and captures each step. It then clicks the native close gadget and waits
for empty object/class registries with zero dispatch/callback depth.

`inputAndCleanup=PASS` covers the observed mouse-state transitions, final
registries and unlinking of the original native window. **Arithmetic still requires visual review** of the
`arithmetic-result.bmp`; the probe never infers a result of 11 from an accepted
click. Review the final CLI image as evidence of normal quit. Missing buttons,
timeouts, unsupported hardware, unavailable state and maximum-field expiry are
failed or incomplete coverage. Partial runs are retained without relabelling.

Version 2 retains the same hardware and interaction sequence, increases each
press/release bound to 60,000 fields, and captures CPU registers for diagnosis.
The v1 CopperStart run expired its 15,000-field press bound inside dispatch;
that retained run remains incomplete. Use a fresh output directory for v2 and
record its explicit overall field bound; do not replace earlier receipts.

Version 3 also captures the empty registries before executing 5,000 additional
guest fields for CloseLibrary and CLI return. Registry activity during that tail
fails the run; library expunge is allowed after the captured cleanup observation.
The final settled CLI image still requires visual review. Earlier v1/v2 receipts
retain their original scope and do not acquire this extra observation coverage.

Version 4 also requires the original native Window to be absent from its
Screen.FirstWindow list after settling. Version 3 could report empty MUI
registries even when a failed native DeleteLayer left the OS window linked;
the retained v49 backfill attempt records that counterexample. Bitmap restoration
is checked separately with `UtilityStartup/inspect_mui_calc_backfill.py` and
visual review. Earlier receipts retain their original protocol and results.
