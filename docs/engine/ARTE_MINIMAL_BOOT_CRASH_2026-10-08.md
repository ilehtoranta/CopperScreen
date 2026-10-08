# Arte minimal-boot crash investigation — 2026-10-08

## Reproduction

Arte / Sanity reaches its own SOS error handler in minimal disk boot. This is a
real guest address exception, not a scripted demo effect. The same local ADF and
engine pass this point with native Kickstart 1.3. Clearing RAM location zero in
the original boot firmware repairs the reproduced failure; corrected minimal
boot completes the 40,000-field diagnostic budget.

Both profiles use PAL OCS 8371/8362, the Copper68k 1.5.3 68000 interpreter,
512 KiB Chip RAM plus 512 KiB slow RAM, one read-only DF0, no hardfile and no
input. The 908 × 313 native rasters are captured without desktop presentation,
audio-device output or pacing. These are untimed compatibility diagnostics,
not throughput measurements or hardware conformance checks.

The diagnostic executable links the existing minimal-boot assembler, firmware
generator and disk loader. It loads the generated firmware through the ordinary
`LoadKickstart` API and mounts the original disk. The correction changes only
the original boot firmware's RAM initialization. No demo instructions, disk
bytes, CPU package or engine source are patched.

| Run | Observed result |
| --- | --- |
| Minimal boot, ordinary execution | Inspected raster at field 21,000 shows the “No AGA” scene; field 23,000 reproduces SOS error `$50`, exception `3`, saved registers at `$C03162`. Captures continue through 26,000 in the error handler. |
| Minimal boot, first-chance exception capture | Fault with 22,008 completed fields, cycle 3,127,393,270. The core catches its internal `M68kAddressErrorException` and dispatches the guest handler. Captures continue through 24,000; the diagnostic host does not crash. |
| Native Kickstart 1.3 | Completes 40,000 fields without a captured address exception or unsupported-feature stop. Sampled rasters progress past the failed part, closing artwork and into an earlier star scene again. This is bounded sampled progression, not hardware-frame certification. |
| Corrected minimal boot | Completes 40,000 fields without a captured address exception or unsupported-feature stop. Inspected captures show the later 3D greetings at 26,000 and a return to the opening tunnel at 32,000. |

The initial requested 60,000-field minimal run and 55,000-field exception run
were stopped after the fault and its persistent error screen were established.
Neither is reported as completion of its requested budget.

## Cause and correction

The failing instruction is `TST.L (A0)` (`$4A90`) at `$C0CF3E`; `A0` is
`$33FCFFFF`. A 256-instruction history establishes the preceding chain:

1. `MOVEA.L $28(A5),A0` at `$C0CF2A` reads an empty/null list root from
   `$C1508A`, producing `A0=0`.
2. `MOVEA.L (A0),A0` reads RAM address zero. Original minimal boot initialized
   this longword to `$FC040E`, its generic exception-handler address.
3. The traversal continues into the boot ROM and reads `$33FCFFFF`, the first
   four bytes of the handler's `MOVE.W #$FFFF,...` instruction, as another pointer.
4. `TST.L (A0)` attempts an odd-address longword read and faults.

The native replay leaves RAM address zero equal to zero. Arte's traversal then
terminates on its first null-node test. The invalid pointer therefore does not
require list corruption or an unexplained writer: it follows directly from
boot firmware initializing location zero as an exception handler.

The history capture switches to the ordinary instruction/interrupt/hardware
sequence from field 21,000. Its fault field, cycle, opcode and all captured CPU
registers match the ordinary execution trace exactly. The first-chance handler
also preserves all 125 shared normal-versus-traced capture files byte-for-byte.

`MinimalDiskBoot.s` now executes `CLR.L $0` after initializing exception vectors.
Reset still obtains SSP/PC from the ROM overlay, and the Exec base at address
four remains unchanged. Vector slots zero and one contain reset SSP/PC rather
than ordinary handler pointers; see [Motorola's vector assignments](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf).
The choice to leave RAM zero cleared matches the observed native-boot state and
supports this guest null-list convention; it is not a claim that all Amiga
software may safely dereference null pointers.

The synthetic `NullListTraversalTerminatesWithoutFollowingBootFirmware` test
uses the same traversal without proprietary media. It fails before the fix
with a guest exception at `$006816`; all 18 minimal-boot tests pass after it.

The error-screen PC samples remain in SOS input/error handling and the field
counter continues advancing. A motionless guest error screen therefore does not
by itself indicate a stopped hardware clock or blocked host thread.

The correction is general boot initialization, with no title detection or
automatic native-ROM fallback. Earlier startup evidence retains its original
scope. Native and corrected minimal-boot progression do not certify every
chipset/CPU behavior or unlimited playback.

## Separate desktop failure

The user's Visual Studio output records a first-chance
`Copper68k.M68kAddressErrorException` and later an unhandled
`System.ExecutionEngineException` in `Avalonia.Win32.dll`, followed by process
exit. The desktop log for PID 85560 continues reporting the SOS handler beyond
53,000 fields, with no recorded managed fatal stack.

The guest fault reproduces headlessly; the Avalonia/CLR termination does not.
Its cause remains open and cannot be inferred from the guest address exception.

## Evidence and validation

Local, ignored evidence is under `artifacts/arte-crash-2026-10-08/`: diagnostic
source and binaries, generated original firmware, frame JSON/BMP, Chip/slow RAM,
PCM samples, first-chance CPU/RAM captures, logs and a machine-readable summary.
ROM/media and RAM captures remain outside version control.

| Input/component | SHA-256 |
| --- | --- |
| Decoded Arte ADF | `eb7521fc1de886c69706ab0e21898a89199fb12256db40743f3ed190957e896a` |
| Engine binary used by both profiles | `0811f18d3b3da161544690dc0ea7322f208d4e4889f66ede52705fdc2d178e80` |
| Copper68k 1.5.3 binary | `90d187d1fcb641e5434ea9e2dbde5b0ec4b1af08e313990e60762c0367c0acf3` |
| Original full minimal-boot ROM, slow RAM fitted | `60957eafba71a66ac356883863dc1b81eeaf8ba67d911a0e22843500605d990e` |
| Corrected full minimal-boot ROM, slow RAM fitted | `3d872ed64e0485cb755a05bc1aba52ef7e0b6598c96d56887d1b2be7a83bc665` |

The checkout is the existing working tree based on
`aa1dad5dcc0fb7c970e8e3443a160487add846af`, including pre-existing modifications;
this is not a clean-commit regression claim. The ordinary production output
was locked by a separately running runner. Building the complete production
solution with `dotnet build CopperScreen.slnx -c Release` succeeds after the
correction with zero warnings and errors once that unrelated runner has exited.
The isolated production build also passes. The firmware generated from corrected
production source matches the diagnostic correction byte-for-byte. No new
performance acceptance is claimed.

See the [minimal boot contract](MINIMAL_DISK_BOOT.md) and
[issue register](ISSUES.md). The separate desktop Avalonia/CLR failure remains
unresolved.
