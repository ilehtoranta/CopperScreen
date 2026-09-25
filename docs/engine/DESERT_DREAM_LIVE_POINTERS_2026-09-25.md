# Desert Dream: live area-blitter pointers

Follow-up: [hidden-part and combined throughput validation](NATIVE_CORRECTIONS_VALIDATION_2026-09-25.md)
verifies both hidden sections through final greetings. The combined performance
series is valid; hires and native Lemmings pass, while lores exceeds the default
limit. The initial investigation below retains its original validation scope.

Desert Dream's late interrupt-vector corruption is repaired by making area-blitter
pointer writes visible to the running blit. Previously, the register bank accepted
the writes but the blitter kept private pointers copied at BLTSIZE. Subsequent DMA
then overwrote the newly programmed destination. The correction routes CPU and
Copper pointer writes into the active area engine through their existing shared
custom-register path.

This follows the [original overlap investigation](THREE_NATIVE_INVESTIGATIONS_2026-09-24.md#desert-dream-an-overlapping-copper-blit-clears-the-interrupt-vector).
It does not establish a new BLTSIZE restart policy. The earlier trace remains valid:
three CPU clears end at the chip-address wrap, while the old Copper list programs
another clear before the third CPU clear finishes. With frozen live pointers,
deferred BLTSIZE starts at zero, clears vector `$78`, and sends a level-6 interrupt
to zero. The same current pre-correction binary reproduces that failure.

A diagnostic copy of the final engine confirms that the relevant CPU/Copper
timing remains unchanged:

| Cycle | Observed destination behavior |
| ---: | --- |
| 2,748,681,378 | Third CPU clear starts at `$07B000`, as before. |
| 2,748,695,510 | Copper's high-half write changes the active destination from `$07C480` to `$06C480`. |
| 2,748,695,518 | Copper's low-half write changes it to `$062800`. |
| 2,748,730,468 | The deferred `$4014` clear starts at `$066380`, after the remaining DMA increments, instead of zero. |

Neither of the two pointer writes has a pending output in this bounded trace.
The correction therefore does not depend on guessing a same-cycle collision rule
for this transition. Production replays use the engine without diagnostic logging.

## Evidence and boundary

Commodore describes BLTxPT as the DMA source/destination address registers, with
increments and row modulos updating their contents. See the
[1989 Hardware Reference Manual, BLTxPTH/BLTxPTL](https://www.ikod.se/wp-content/uploads/2020/08/Amiga_Hardware_Reference_Manual_1989.pdf).
The manual's safe programming sequence waits for the previous blit before changing
its registers. These descriptions establish the address-register role, but do not
provide a complete cycle-by-cycle oracle for active reprogramming.

Supporting implementation evidence is pinned to WinUAE 6.0.3 source commit
`857f48fcb09399d4d95527e29a2fdeaedb41f827` (tag `6030`):

- [BLTxPT writes](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/custom.cpp#L4037)
  update the pointers used by its running blitter.
- Its [cycle-exact blitter](https://github.com/tonioni/WinUAE/blob/857f48fcb09399d4d95527e29a2fdeaedb41f827/blitter.cpp#L1790)
  uses those pointers and separately handles coincident pointer-write/DMA cases.
  This correction does not claim equivalent coverage of every such collision.
- Its active BLTSIZE handling differs from Lightweight's deferred-start model.
  That policy is not imported as a guessed repair for this demo.

Two isolated trials used the same ROM/media and no guest-memory edits. Live
pointers alone reached the tunnel and circle effects through 26,000 fields.
Adding an immediate restart also progressed, but changed saved memory during the
tunnel (20,000 and 21,000 fields). Both reached the same later state at 26,000.
The restart experiment was not promoted. The production change is further scoped
to area mode; line-mode reprogramming remains unchanged and unverified.

The candidate preserves already accepted bus output addresses and values. It adds
no device state, per-cycle checks, allocation, logging, title recognition or guest
patch. Existing control/modulo/data latching, BLTSIZE deferral, busy/interrupt
timing, CPU package, Copper scheduling and disk behavior are unchanged. Exact
coincident pointer write/accepted output behavior, live control/modulo/data writes,
line-mode reprogramming and restart behavior remain open hardware-model edges.

## WinUAE reference run

Installed WinUAE **6.0.3.0** executable SHA256:
`F3E55700FD811CD543FDFEBF6C3221CFAA3D385828F110C6D3EB534C64503123`.
Profile: PAL OCS A500, compatible cycle-exact 68000, cycle-exact memory/blitter,
no JIT, 512 KiB chip plus 512 KiB slow RAM, native Kickstart 1.3, one read-only
DD drive at 100% speed, waiting-blits disabled. Warp accelerated host presentation.
Live configuration queries and the launch configuration are retained locally.

Disk A ran without input to the disk-two prompt. Replacing it with the exact disk-B
entry continued to the closing Kefrens message, including “TO CONTACT US, FIND THE
SECRET PART...”. No guest memory was patched. This is a supporting reference run,
not a physical-hardware trace, frame-aligned comparison or proof of every effect's
pixel/timing accuracy. The secret part was not entered.

An earlier reference attempt entered WinUAE's interactive debugger, which stopped
servicing its named pipe. That task-owned instance was ended and a fresh run was
used for the reference result. Instruction breakpoints did not produce a usable
trace of the overlap. Thus this work does not independently establish the exact
CPU/Copper/blitter timing preceding the transition.

## Validation and reproduction

The Release solution builds with zero warnings/errors. All **726 engine diagnostic
cases pass**, with zero skips, using the separate diagnostic output directory.
Seven new cases fail on the pre-correction engine and pass afterward:

- Source A, B and C pointer writes feed subsequent area reads.
- Destination high/low writes redirect remaining words in ascending/descending
  mode, preserve untouched old destinations, and retain word-address alignment.

These cases use a DMA pause to isolate functional live-register behavior from
unverified same-cycle races. Existing bus-output, completion/termination, modulo,
line-mode and allocation checks also pass. Host/disk suites were not rerun for
this shared engine-register correction.

Production engine SHA256:
`7EB9C87905BD8CA790DFF21BB918A0B34F7E4B94FE8489F673058BC4D1581FDC`.
The reference engine is the preceding Desert Strike correction
`66CF94D3AF712F7BB12822A44FFCD7A4DD0E2C45B5899A87A9B9C70E7D2AEC5C`.
Both retain the WORDSYNC correction and stable Copper68k 1.4.1.

Normal and scalar production replays each complete **72,000 fields**, load disk
two and reach the closing credits/message routine, with no unsupported feature
or guest-memory patch. All **288** image/state/chip/slow-memory checkpoint files
match between modes. All **76** pre-transition files through field 19,000 match
the frozen pre-correction replay. All **104** files through 26,000 also match the
earlier all-pointer trial, showing that restricting the production change to area
mode retains its demonstrated result. These are checkpoint comparisons, not a
claim of instruction-level equivalence between samples.

| Completed fields | Visually inspected production checkpoint |
| ---: | --- |
| 20,000–22,000 | Line figures, tunnel and circle effects after the former crash |
| 35,000 | Disk-two prompt |
| 42,000 | Disk-two shaded objects |
| 47,000 | Disk-two point-wave effect |
| 53,000–72,000 | Closing message and scrolling credits |

The complete retained lores, hires and native Lemmings workloads also preserve
their CPU, hardware and output fingerprints, with zero measured allocation.
These single diagnostic runs establish identity retention, not throughput
acceptance. No benchmark fingerprint was rebased.

The [machine-readable record](DESERT_DREAM_LIVE_POINTERS_2026-09-25.json) binds
the exact ROM, archive/entry identities, binaries, captures and validation logs.
The PAL OCS native replay uses 512 KiB chip plus 512 KiB slow RAM, Kickstart 1.3,
read-only DF0 and 908-wide output. The
[retained input script](../../CopperScreen.Lightweight.Tests/Workloads/desert-dream-live-pointers.json)
ejects disk A at field 36,000 and inserts B at 36,120. Its local extracted
`media/desert-dream-disk2.adf` must be the exact B entry from
`C:/Data/TestImages/Desert Dream (Kefrens).zip`. No ROM, media or generated capture
is committed.

With those supplied files, the production runner can reproduce the extended run:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll `
  --rom C:/Data/ROM/Kickstart_13.rom `
  --disk 'C:/Data/TestImages/Desert Dream (Kefrens).zip#/Desert Dream (Kefrens) A.adf' `
  --input-script CopperScreen.Lightweight.Tests/Workloads/desert-dream-live-pointers.json `
  --wide-output --frames 72000 --boot-probe <fresh-output-directory>
```

Add `--scalar-cpu` for the scalar control. The local external probe uses the same
production engine and script, saving image/state/chip/slow-memory checkpoints
every 1,000 fields. Raw evidence is under `artifacts/desert-dream-live-blitter/`.
Early diagnostic build failures and input-script ZIP-path parsing failures are
retained as failed attempts, not counted as successful native runs.

At the end of this initial investigation, formal host throughput had not been
measured. The linked follow-up now records a valid combined comparison, with the
lores gate unmet. Replay FPS remains diagnostic only; earlier performance
acceptances and the WORDSYNC INVALID / RERUN record retain their original scope.
This bounded repair is not an OCS conformance sign-off.
