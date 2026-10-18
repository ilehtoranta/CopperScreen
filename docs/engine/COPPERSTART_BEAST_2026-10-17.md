# CopperStart — Shadow of the Beast, 2026-10-17

Shadow of the Beast (1989), PNA crack, is the next real-media case after the
native graphics qualification. The preceding CopperStart image renders its
intro but corrupts execution when the mouse exits it. The new source fixes a
native Exec Supervisor entry conflict while preserving caller-owned traps.

## Failure and change

The intro installs TRAP #0 at vector `$80`, pointing to `$5337C`. At field 3001,
its trackdisk DoIO requests `$4D600` bytes at `$1BDE0`, from disk offset `$2C00`.
CopperStart selects the correct BeginIO vector, but the private supervisor
wrapper executes TRAP #0 and enters the intro instead of the native device
routine. It returns without loading the next stage: `io_Actual` remains the
previous `$1A00`, and the game jumps into zeroed RAM at `$67F00`.

Exec now enters through `ORI.W #$2000,SR` and the MC68000 privilege-violation
vector. The private handler recognizes only its gate PC, prepares the stacked
return, and enters A5. An already-supervisor caller receives an equivalent
six-byte RTE frame. Other privilege violations chain to the prior handler;
reinstallation preserves the chain. Caller software traps remain available.
The runtime layout includes the larger handler and gate.

The local Kickstart 1.3 control uses the same kind of privileged entry at
`$FC08E6` and a privilege handler at `$FC090E`; its caller-installed TRAP #0
survives the disk call. The implementation follows the documented
[Supervisor callback and RTE ABI](https://developer.amigaos3.net/autodocs/exec.library/Supervisor.html)
and [MC68000 instruction/exception behavior](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf).
No IORequest length, allocator-ownership, list-link, or arbitrary work checks
were added to compensate for this private runtime conflict.

## Bounded qualification

The qualified ROM is 524,288 bytes, with 448,542 compiled C# bytes and the
unchanged 26,252-byte native graphics/bootstrap block at `$FF0000`. SHA-256:
`38A5DDFACF7699CF24EEE25C92478A608BFE618B24724CCC0EA5AA9342D5943E`.

| Coverage | Passed, no skips |
| --- | ---: |
| Focused Exec vectors, task frames, bootstrap and interrupts | 83 |
| Added unrelated-privilege chaining case | 1 |
| Fresh native disk, reader, graphics and interrupt ROM tests | 56 |
| Exact-ROM DOS, script, console, disk and graphics replays | 53 |

The native and regression suites overlap; these totals must not simply be
added. Both Release solutions build. CopperScreen reports no warnings; the
CopperStart solution build reports one existing CopperSharp CS8602 warning.

The native boot regression installs a caller trap, invokes Supervisor, performs
a second trackdisk read, and checks the return, trap count, byte count and
payload. Against the preceding qualified ROM it fails: Supervisor returns
11688 rather than 42, six traps occur rather than two, and the transfer count
remains 1024 rather than 512. The corrected component tests also cover both
CPU modes, explicit caller-trap execution and unrelated privilege chaining.

| Media / execution | Bounded result |
| --- | --- |
| Beast, 32,000 fields in each CPU mode | Intro exit, title and all three disk changes reach the opening game. WELCOME is visible at 24,000, attack animation at 26,000–26,400, and gameplay remains visible at 32,000. All 405 capture files and final CPU/hardware/output/cycle fingerprints match across modes. |
| Native Kickstart 1.3 control, 32,000 fields | The same disk sequence reaches the opening game and responds to fire. This is a comparison control, not CopperStart coverage. |
| North & South, Captain Paranoia variant, 26,000 fields, batch | English and GO clicks reach the opening message and animated strategy map. Combat and a complete game are unverified. |
| Arte by Sanity, 40,000 fields, batch | Effects, credits/greetings, later textured 3D and the artist page are visible. Full-demo completion and secret parts are unverified. |

The original-ROM control established that this crack requires three prompted
DF0 swaps: Disk 2 at field 14120, Disk 1 at 17620, and Disk 2 at 20620, followed
by fire. A two-swap run remains at a prompt and is not a gameplay pass.
The final comparison uses the same three-swap script. Capture files and final
CPU/hardware/output/cycle fingerprints are compared between conservative batch
and scalar execution of the exact rebuilt CopperStart image.

These are bounded opening/gameplay and regression observations. They do not
qualify a complete game, subjective audio quality, every graphics vector, or
host throughput. No emulator timing, pinned CPU package, host OS shim or
published package changes are part of this fix.

## Reproducibility and excluded attempts

The companion JSON records exact producer, ROM, engine, CPU, media, script,
TRX and capture identities. The runner/engine/CPU remain frozen from the prior
comparison. The native ROM fixture has its own recorded engine identity.
Only the Exec managed producer changes within the previously frozen ROM input
set. The inherited producer snapshot includes concurrent source edits as
recorded in the preceding qualification; this is not a clean-commit binary
reproduction claim. The new Exec/test overlay is archived locally, excluding
ROMs and media. Its producer predates comment/formatting-only cleanup.

A faster compiler experiment with Bounded optimization emitted 543,950 bytes,
exceeding the 524,260-byte code capacity. It was rejected before producing a
qualified disk ROM. Its failed build and its separate reader-only tests are
excluded from final pass counts. The final image uses the preceding frozen
compiler and FixedPoint optimization, with the native graphics/bootstrap block
linked by the normal composition path.

The source fix is [CopperStart 9eed3fe](https://github.com/ilehtoranta/CopperStart/commit/9eed3fe584549b36f0c5de175929e890ec620669),
published on `codex/copperstart-beast-20261017`.

Artifacts, ROMs, licensed firmware and game media stay local. The source change
and this evidence are committed separately in their owning repositories.
