# Original CopperOS system boot proof — 2026-10-16

This record freezes the original `dev.3` read-only proof. The current app pin
has advanced for the [native storage continuation](COPPEROS_STORAGE.md); use its
new exact ROM/seed pair and evidence for current reproduction. Historical hashes
and observations here have not been relabeled as the new qualification.


This is the development continuation of P1-04, separate from the frozen
`1.0.0-draft.1` artifact inspection. It adds original producer image composition,
native Shell registration and an optional standard-app integration test.
It does not authorize Store distribution or close all of P1-04.

## Qualified development pair

The [dated evidence](COPPEROS_BOOT_PROOF_2026-10-16.json) and
[versioned descriptor](../packaging/guest/copperos-native-boot.v1.json) bind this
source-session proof to these exact inputs:

| Artifact | Version / size | SHA-256 |
| --- | --- | --- |
| Standalone CopperStart ROM | `diskboot-2026-10-16-dev.3`, 524,288 bytes | `1950255822e4d4571dc9d96d510c792d57d3c801fb11568f20155527fc3de444` |
| Original CopperOS DOS1 system image | `0.1.0-dev.1`, 901,120 bytes | `40c56b2539ecaa3eb82e0cd338910d9ca6db38a5fd0b83c8072831cc0c38daa3` |

Pair ID: `a500-copperos-cli-boot-proof-dev.1.50015b6fdce1`. These are unreleased
development artifacts. The earlier inspection contract is unchanged; this new
descriptor keeps shipping and activation disabled.

The native test **passed**, with no skipped coverage: a fresh user profile and
the standard app session booted the disk, observed successful native Shell
registration and the actual prompt, defined and expanded an Alias, launched the
original external Demo, returned to the prompt, then handled `missingcommand`,
`Why` (`Object not found`) and `Echo recovery`. The read-only disk hash and drive
dirty state remained unchanged. The replay completed at 9,328 emulated fields;
that is a diagnostic endpoint, not a throughput result.

Validation results, recorded separately in the evidence:

- 15 compiled native ROM console/DOS boot replay cases passed on Lightweight.
- 12 CopperStart app cases passed, including the 14,000-field bounded Lotus
  replay. All nine pixel milestones match the previous `dev.2` control and all
  nine scalar comparisons match. Inspected race-grid and gameplay captures
  remain consistent with that control; this is not full-game qualification.
- 230 host tests passed; nine optional asset-dependent tests skipped, which
  means unavailable coverage in that run. The supplied-asset proofs above ran
  separately without skips.
- 200 focused CopperOS Shell/Alias/Echo tests, 82 DOS bridge host cases, seven
  disk filesystem host cases and five image serialization/readback cases passed.
- CopperScreen and CopperStart Release production builds passed with zero
  warnings or errors. All four final native artifact reports passed their
  68000/no-FPU/static runtime gates.

Local generated outputs are preserved under
`artifacts/copperos-p1-04/recovery-build` and `artifacts/copperos-p1-04/rom3-candidate`.
The [final native screen](../artifacts/copperos-p1-04/recovery-build/app-proof/20261016T174911420Z/recovery.png)
and transcript/RAM/CPU captures accompany the hash-bound runtime result. Earlier
failed controls are retained under the same artifact root and do not qualify
this final pair.

## Implementation

Producer changes live in the sibling CopperOS checkout:

- `tools/SystemImage.NativeRoot`: original bootstrap, sequential FFS/trackdisk
  reader, bounded single-CODE HUNK loader and original external demonstration.
- `tools/ImageBuilder`: frozen-input native compilation, deterministic bootable
  DOS1 DD ADF composition, complete file hashes, boot checksum and independent
  filesystem/bitmap readback, plus five serialization/rejection tests. Candidate
  identities include the native manifest hash, distinguishing successive builds.
- `src/System/Shell/ShellTextParser.cs`: packed command lookup now reads bytes.
  The previous longword read at each character offset raised an address error
  on 68000. Four memory-boundary regression cases exercise bounded names without
  padding, including an unaligned start.
- `src/System/Shell/ShellScriptEngine.cs`: command failure thresholds still stop
  scripts, while synchronous and asynchronous command errors let interactive
  readers continue. A completed command lookup with no match becomes an ordinary
  command error without attempting to launch a missing path. Focused regression
  cases cover these paths and retain noninteractive script failure limits.

The disk contains original `C/BootShell`, `C/Demo`, `L/Shell-Seg`,
`S/Startup-Sequence` and `S/CopperOS-Release.json`, with no reference OS files
or loose command-distribution outputs. The existing DiskBuilder shipping
admission rules are unchanged. Its nonbootable-image verifier receives only a
private boot-normalized copy for filesystem verification; the real image's
boot checksum is independently verified and the image remains unchanged.

The guest bootstrap loads and relocates Shell, calls native DOS `AddSegment`
with SYSTEM use count `-1`, and creates a real DOS Process/CLI with inherited
streams and a 65,536-byte task stack. It marks the public CLI interactive before
permitting the child to run. The parent waits for the machine lifetime and
retains borrowed streams and the permanent segment. SYSTEM is not PURE command
admission. Arbitrary teardown, multiple shells and complete resource-lifetime
qualification remain outside this bounded proof.

The native Shell entry wrapper initializes its DOS-owned `C:` command path as
the actual child before entering the production Shell task entry. The ROM's
bounded boot CLI supplies no general command search path. This initialization
uses the producer's native DOS path owner and retains its lock/lifetime rules.
Each artifact's manifest retains its own frozen managed inputs and source
snapshot. The Shell and ROM are separate native compilations, with no assumption
that their entire managed input assemblies are byte-identical.

The bootstrap's original reader uses public Exec/trackdisk I/O and separate
FFS pointer-table and track buffers. The current ROM LoadSeg buffers the entire
input HUNK, which exceeds one RAM bank with symbols. Its filesystem also walks
the whole extension chain for every data block. The bounded streaming loader
avoids both restrictions on the existing 512 KiB Chip + 512 KiB slow A500. It
admits one CODE hunk, self RELOC32, optional bounded symbols and END/EOF only.
Final native output omits symbols. This is not a general replacement LoadSeg
or a writable filesystem implementation.

Producer changes in CopperStart correct native CON raw-key translation for
the bottom row and keypad. The original mapping made normal `C` input become
`V`; the old native test typing helper repeated that wrong mapping. The helper
and semantic tests now use physical Amiga raw-key positions. No host key-map
workaround is part of the app or integration test.

The DOS command-lookup bridge also groups its borrowed search inputs into one
request record. A bounded instruction trace showed the previous mixed
eight-argument native call clearing its candidate result over the live platform
context. The private helper now has four address arguments. This change is
compiled into the Shell segment and does not change DOS state layouts. Exact
component inputs remain recorded separately.

The original disk handler's packet FileInfoBlock name now uses the SDK's
length-prefixed layout. Previously it wrote a C string: native DOS read the
first character as a length and reconstructed `CopperOS:` as `opperOS` with an
invalid tail. Host filesystem regressions read root, file and enumerated names
through the same SDK codec. This is a repair of the bounded producer packet
contract, not a general AmigaDOS API compatibility claim.

CopperScreen adds `CopperOsBootProofTests` and `scripts/test-copperos.ps1`.
The test boots through the standard Lightweight session, types actual raw keys
and reads the real native console bitmap without guest writes or DOS calls.
It requires the registration banner and prompt, Alias expansion to an exact
output line, the original external Demo returning, and a command-error recovery
line. Each run preserves its own PNG, transcript, RAM and CPU-state captures.
Missing assets skip the optional test and provide unavailable coverage.

## Reproduction

Build the producer proof in a fresh directory using the instructions in
[the producer image-builder guide](../../CopperOS/tools/ImageBuilder/README.md).
The standalone ROM uses the producer's `build/Build-DiskBootRom.ps1` entry,
68000/ROM profile and explicit export list; this qualification froze its managed
inputs before invoking the same native build. Its complete command, input
hashes and source snapshot are preserved in `rom3-candidate/compile-request.json`.
Later edits in a producer checkout are not covered by this qualification; the
frozen inputs and recorded artifact hashes define the tested pair.
Retain the frozen inputs, native
manifest, compiler reports and image receipt alongside the disk. Then run:

```powershell
./scripts/test-copperos.ps1 -Rom '<qualified standalone ROM>' -ProofDirectory '<proof directory>' -ResultsDirectory artifacts/copperos-proof-results
```

For this local pair, launch the app with `--copperstart --rom` pointing to
`artifacts/copperos-p1-04/rom3-candidate/CopperStart-DiskBoot.rom`, followed by
`artifacts/copperos-p1-04/recovery-build/System.adf`. The production loader pins
the exact ROM hash. A newly rebuilt pair needs its own qualification; changing
the catalog descriptor alone does not admit it or bypass that check.

Generated native binaries, ROMs, images and captures stay under ignored local
artifacts. CopperScreen consumes explicit artifacts; its production solution
does not acquire producer project references. Lightweight remains the sole
hardware/time owner. No Legacy or host OS shim was introduced.

## Remaining P1-04 work

Writable native guest filesystem support, file/settings persistence across app
restart and MSIX updates, complete memory and stack budgets, broader Shell
lifetime/error coverage, commercial rights/notices, release admission and
installed qualification remain required. The source proof is not a performance
measurement or a complete Store-ready operating system.
