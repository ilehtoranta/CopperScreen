# CopperStart: native CIA resources and Hired Guns continuation

Follow-up: [positioned windows, Boolean gadgets and the original warning buttons](COPPERSTART_HIRED_GUNS_WINDOWS_2026-10-19.md).
The CIA-stage results below retain their original ROM identity.

2026-10-19. Native `ciaa.resource` and `ciab.resource` let the original Hired Guns
SystemTakeover utility pass its resource-open stage. It reaches its warning-window
request and reports **ERROR #0045 : STWARNKILLSYS COULD NOT OPEN WINDOW**, returning
to `1.SYS:>`. Game entry and gameplay remain unqualified.

The [machine-readable evidence](COPPERSTART_HIRED_GUNS_CIA_2026-10-19.json)
records source, ROM, inputs, controls, builds and replays. This continues the
[misc.resource result](COPPERSTART_HIRED_GUNS_MISC_2026-10-19.md).
Implementation is committed in CopperStart at
[`6a58b0d`](https://github.com/ilehtoranta/CopperStart/commit/6a58b0dd71ec3b4eb70ea50aa8ec4446bdb60648).

The emitted MC68000 backend at `$FF6000` installs both resources through guest
Exec. AddICRVector, RemICRVector, AbleICR and SetICR provide ownership, hardware
enables, latched pending status and IRQ dispatch through PORTS/EXTER servers.
Callbacks receive A1/IS_DATA and A5/IS_CODE. Active status remains visible during
callbacks under the V36+ contract; masked events survive hardware ICR reads.
Caller-owned Interrupts with stale Node links are accepted without allocator or
zero-link admission rules. Console and Intuition keyboard polling use SetICR so
they cannot consume other CIA events. No host OS shim, title detection or guest
memory injection is involved; hardware engine code is unchanged.

An intermediate image passed 105 cases but starved console input under continuous
timer IRQs. Short physical INTENA master transactions replace the heavy compiled
Exec Disable/Enable task boundary, preserving the caller's actual interrupt state.
The final regression checks continuous timer IRQs and physical keyboard input.

The fresh main compilation contains 467130 code bytes. Final composition has
1108 CIA bytes, 7178 Intuition bytes and 31622 graphics bytes. The 524288-byte ROM
SHA-256 is `cb40affb0c9b0450b0e74c6a454aa2e568f82574ffd74910d9c9064854c5093a`.
The Release solution build completes with zero warnings/errors using the recorded
dependency root and existing project-reference outputs. The production emitter
produces identical whole-ROM bytes. All 12 qualification input hashes are checked.

The exact-ROM suite passes **106 cases, zero failures and zero skips**. Fresh
main compilation/boot passes its separate case. Three authored CIA probes pass
against native Kickstart 1.3: ownership/contention, masks/pending, software and
hardware IRQs, callback registers and concurrent console input. Fourteen optional
standalone polled-reader cases are excluded, not counted as passes.

A separate Kickstart 3.1 control passes CIAB but fails two CIAA cases at the
post-removal enable-mask assertion; reclaim succeeds. This unresolved native
reference discrepancy is not qualified as a passing control. Assertions remain
intact and no ROM-specific workaround is added. The implementation follows the
documented removal behavior and passing 1.3 control.

Normal and scalar runs cold-start original Hired Guns v1.08.39.25 disk 1 on the
bounded PAL OCS 68000 A500 with 512 KiB Chip/512 KiB slow RAM, one read-only floppy
and RTC disabled. The same physical CLI command is typed from field 2100. Only
disk 1 is mounted; the launch receipt's other-disk inventory is metadata.
Both runs stop cleanly at field 12000 with host exit zero. All **605 capture files
per mode** match: BMP, CPU/hardware/cycle state, Chip RAM, slow RAM and PCM. No
captured unsupported hardware/video path is reported. The final normal image was
visually checked. Mode agreement and clean host exits do not establish game entry.

A separate read-only instruction trace on an intermediate component image
identifies OpenWindow at field 6568: left/top 111/25, size 423×161, WBENCHSCREEN,
IDCMP_GADGETUP and a non-null gadget list. It returns null. Native Intuition
currently supports flat full-screen windows. This requester needs positioned
windows, borders, gadget drawing and input; removing guards alone would report
false success. The trace diagnoses the next boundary separately from final-ROM
qualification.

`workbench.task` also remains unavailable. No full Workbench/native game-launch
control or new Railroad replay is claimed. Earlier evidence retains its own ROM
identity. These are correctness diagnostics, not throughput measurements. ROMs,
media, local build binaries and captures are not committed.
