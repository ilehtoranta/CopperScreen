# Additional AGA media: shared CPU correction

The shared CPU startup blockers for the supplied 4th Dimension demo and Kick
Off 3 are fixed in the unpublished Copper68k 1.5.2 development source. The
correction is pushed in [CopperMod PR #19](https://github.com/ilehtoranta/CopperMod/pull/19),
commits [`7c183e4`](https://github.com/ilehtoranta/CopperMod/commit/7c183e44d37eff27bbdbfe7c5b2078901b69cd5a)
and [`8cd295e`](https://github.com/ilehtoranta/CopperMod/commit/8cd295e44ebcc0d8ffb1b1e15b172f31e3659d4f).
The unchanged application still pins public 1.5.1; it does not contain these
fixes. Release and a pin update remain separate work requiring authorization.

The [eight original native startup failures](AGA_ADDITIONAL_MEDIA_2026-10-03.json)
remain unchanged. This follow-up retains its own package, commands, input,
ROM/media identities, intermediate stops, test records and capture hashes in
the [machine-readable continuation record](AGA_ADDITIONAL_MEDIA_FIXES_2026-10-03.json).
No ROM, media, private package, RAM, audio or raster artifact is committed.

## CPU forms and discriminating regressions

The initial failures were ordinary 68k operand forms, not guest chipset
rejections. Implementing them let native boot expose further missing forms;
each reached family was reproduced in focused tests before its correction.

| Native stop | Reached instruction | Correction |
| --- | --- | --- |
| 4th Dimension, 400 completed fields, Kickstart `$F9759C` | `$B220`: `CMP.B -(A0),D1` | CMP predecrement B/W/L |
| Kick Off 3, 172 fields, `$510E6` | `$20F8`: `MOVE.L ($0068).W,(A0)+` | MOVE absolute-word to postincrement B/W/L |
| 4th Dimension, 2,368 fields, `$1D0F90` | `$36B9`: `MOVE.W (xxx).L,(A3)` | MOVE absolute-long to indirect B/W; existing long route retained |
| Kick Off 3 after the crack-intro exit, `$51116` | `$21D8`: `MOVE.L (A0)+,(xxx).W` | MOVE postincrement to absolute-word B/W/L |
| 4th Dimension, 2,649 fields, `$1D7D48` | `$B353`: `EOR.W D1,(A3)` | EOR data to indirect B/W/L |
| 4th Dimension, 2,744 fields, `$1D7DEE` | `$065A`: `ADDI.W #1,(A2)+` | ADDI postincrement B/W/L |
| Kick Off 3 after disk 2, 12,580 fields, `$19F2E2` | `$8210`: `OR.B (A0),D1` | OR indirect to data B/W; existing long route retained |
| Kick Off 3, 12,581 fields, `$19F284` | `$5239`: `ADDQ.B #1,(xxx).L` | ADDQ byte absolute-long; existing word/long routes retained |
| Kick Off 3 Practice selection, 9,103 fields, `$19F224` | `$E702`: `ASL.B #3,D2` | ASL byte immediate; existing word/long/register-count routes retained |

The 59 focused cases check selected widths, untouched register/memory bits,
sign-extended absolute-word and full absolute-long addresses, source/destination
aliasing, overflow/carry/borrow, preserved X, A7 byte strides and active stack
banks. ASL cases cover encoded count eight, the last shifted bit in X/C
and overflow from any intermediate sign change. Shared 020/030 controls and the 040 fallback remain covered. The held
040 instruction-fetch-buffer regression remains intact; its decoder expectation
now reflects the shared EOR classification while execution still uses fallback.

EOR, ADDI and byte ADDQ memory plans retain explicit read-modify-write barriers.
Timing costs follow the existing approximate advanced operand-shape policy.
These changes do not certify physical instruction durations or bus locking.
Architectural expectations follow [Motorola's programmer reference](https://www.nxp.com/docs/en/reference-manual/M68000PM.pdf).

## Native replays

Both titles use native A1200 Kickstart 3.0 revision 39.106, PAL, 68EC020,
Alice 8374/Lisa 4203, 2 MiB Chip RAM and zero Slow/Fast RAM. The supplied
ADF-in-ZIP images are unchanged. Unsupported execution is never continued.

4th Dimension completes 12,000 fields in both normal and scalar modes with all
151 capture files identical. Captures include moving demo effects, rendered
text and nonzero audio. This is bounded demo progression; the complete demo
and continuous visual fidelity are not certified.

Kick Off 3 passes its crack intro with an ordinary left-mouse click, reaches
the game title and disk-2 prompt, and loads disk 2 into DF0. A short removal
interval was missed by the guest's prompt loop; a longer removal followed by
insertion worked. The retained shorter replay inserts the requested disk at
the first observed prompt. Its main menu exposes joystick direction/fire
control. Mouse movement alone did not move the menu pointer.

Both joystick replays complete 15,000 fields with all 367 capture files identical.
Direction input moves the pointer and fire opens the Practice options. This
verifies menu response. Additional 18,000-, 20,000- and 22,000-field navigation
attempts complete without unsupported execution, but the Practice arrow returns
to the main menu; they do not establish pitch gameplay. Some completed
Kick Off 3 captures are blank while the CPU remains in its menu loop, so
continuous visual stability and playable football are not established by the
startup correction. The record preserves those fields rather than replacing
them with a chosen successful frame.

## Package and consumer validation

The final private package is `1.5.2-aga-dev.10`, built from clean tracked source
at `8cd295e`. Its CPU DLL SHA-256 is
`b7c275ba7a746a9d3e0b03d2a1b461560630c733ea2e25c22b0717512b5e7d92`;
package SHA-256 is
`95eabfc2ba31503a3755a31da8220e75cd54dd4b3de85bd9e99609720e6a658b`.
The CPU C# hashes match the full-suite tested source. Package provenance,
consumer runtime DLLs and input scripts are retained in the JSON record.

| Check | Result |
| --- | --- |
| Complete CPU suite | 4,108 passed; six optional external corpus cases unavailable |
| AHX consumer | 18 passed |
| Private package restore in a fresh isolated consumer cache | Passed |
| Production solution Release build against the private package | Passed, zero warnings/errors |
| Engine diagnostics in separate outputs | 1,076 passed |
| Host integration | 149 passed; six optional native cases unavailable |
| Disk suite | 74 passed |

The public-pin production build also passed before these CPU changes. Native
ROM/media replays above are separate from host and disk tests. Normal/scalar
parity is a determinism check, not an independent hardware oracle. Runner
elapsed times and FPS are diagnostics only; no throughput acceptance is claimed.
