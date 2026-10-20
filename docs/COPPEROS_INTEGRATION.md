# CopperStart/CopperOS integration contract

Current implementation continuations: [original native Shell boot](COPPEROS_BOOT_PROOF_2026-10-16.md)
and [native storage/workspace contract](COPPEROS_STORAGE.md). The inspection
contract and observations below retain their original scope and date; they do
not describe the later writable implementation. Shipping admission remains closed.


Contract **`copperscreen.copperstart-copperos.a500-cli`**, revision
**`1.0.0-draft.1`**, recorded 2026-10-16. This defines the next P1-04 integration
slice; it does not declare an available or qualified CopperOS system.

This is the frozen inspection revision. Later producer implementation and
native execution evidence are recorded separately in the
[original system boot continuation](COPPEROS_BOOT_PROOF_2026-10-16.md).
The absence statements below describe the inspected snapshot, not later builds.

The [machine-readable draft](../packaging/guest/copperos-contract.v1.json) and
[schema](../packaging/guest/copperos-contract.schema.json) deliberately require
`status: blocked`, `shipping: false`, `activationAllowed: false` and
`qualifiedPair: null`. Missing producer assets/versions/ABIs remain null. The app
and MSIX builder do **not** consume this draft or enable a CopperOS option.
Schema validity establishes document consistency, not artifact admission.

## Inspection result

The [dated inventory](COPPEROS_ARTIFACTS_2026-10-16.json) records the inspected
producer commits, source-document hashes, local file sizes and SHA-256 values.
Only the known CopperOS/CopperStart checkouts and relevant output directories
were inspected. Generated files are not assumed to have been built from the
current producer HEAD. Nothing was rebuilt, installed, copied into a runtime
package, or executed as an OS during this inspection.

| Available item | Observed state | Integration consequence |
| --- | --- | --- |
| CopperOS `out/C` | 61 files with HUNK headers; representative Copy, Delete, Dir, MakeDir, Rename, Type and Version files have no `$VER:` tag. | Development command components, not a complete OS or an admitted release. Header recognition does not validate instruction set, dependencies, stack or runtime behavior. |
| Command build manifest | `foundation-in-progress-no-shipping-admission`; zero `qualified_shipping_artifacts`, 15 qualification-only records. | Do not install loose outputs or qualification records through a fallback path. Preserve producer admission requirements. |
| `filesystem/SYS` | Ten `.gitkeep` files; no staged runtime binaries or startup script. | There is no assembled system tree to package. |
| Shell | Managed `CopperOS.Shell.dll`, production semantic/DOS-adapter source, qualification tools and native entry declarations exist. No Shell HUNK or `L/Shell-Seg` was found in the inspected output roots; `tests/Shell.Dos.NativeRoot/bin` is empty. | A managed DLL is not a guest executable. Producer must emit, identify and stage the real 68000 Shell segment. |
| Existing ADFs | 30 ADFs under command qualification outputs, with `CopperSharpNative` root names in their standard root blocks. | Test filesystem/command images, not an identified CopperOS release. Their contents and redistribution rights were not audited. Do not bundle them as the OS. |
| DiskBuilder | Produces nonbootable DOS1 command distribution ADF/raw HDF volumes; current shipping manifest is empty. | System boot-block/startup/Shell composition is additional producer work. Do not weaken command admission to obtain an image. |
| MUI builder | Source/tooling describes a development relocatable library; remaining vectors fail closed and complete MorphOS compatibility is not claimed. No library output was found at the inspected default builder output. | Not a supplied desktop or an initial boot dependency. |
| CopperStart | Qualified app ROM is [diskboot-2026-10-16-dev.1](COPPERSTART_APP.md). Its native DF0 filesystem is read-only; its startup dispatcher/boot CLI is separate from the general CopperOS Shell. | Existing ROM boot success does not prove CopperOS launch or file saves. |

The producer's default `artifacts/native-floppy` ROM still has hash
`a2cacaf2b0ec0eef8212f0eb45fa6f7c0336145264e84e2f9df65239966ab332`, unlike
the selected `ee645f...` app-qualified artifact. Neither the latest source
commit nor a familiar filename selects a compatible ROM automatically.

The producer Shell SG07 record explicitly leaves final runtime staging,
child-completion/resource edges and end-to-end boot qualification unfinished.
Native compile-only evidence and isolated command tests do not close these gaps.

## Ownership and initial experience

The proposed first vertical slice is a **native CopperOS CLI**, one original
demonstration and guest file operations. A graphical desktop, MUI/MorphOS parity,
general Workbench replacement and broader CPU/device profiles are not implied.
This is the starting integration target, not a silent reduction of the first
Store release requirements: P0-02 must still freeze the advertised workflows,
demo, languages, timing budgets and any required desktop experience.

- CopperStart owns the standalone ROM, guest Exec/DOS/trackdisk, processes,
  native filesystem/console and guest Shell registration/return/wait lifetimes.
- CopperOS owns the Shell, internal commands, admitted external commands,
  original demonstration, runtime filesystem composition and release manifest.
- CopperScreen owns artifact validation, profile selection, immutable package
  assets, user workspaces, host input/display/audio, recovery and installed tests.
- Lightweight remains the sole hardware/time owner. All guest services execute
  native instructions through its existing ROM, floppy and input interfaces.
  No Legacy restoration, host DOS callbacks, sibling project references or
  proprietary ROM/OS substitution is permitted.

Producer-side sibling toolchain dependencies observed during inspection are
build concerns for those producers. CopperScreen consumes frozen artifacts;
it must not import their compiler, SDK, host service graph or source projects.

## Versioned pair and artifact admission

`a500-copperos-cli-dev.1` is a **consumer candidate identity**, not a CopperOS
producer release/version and not an activation permission. There is no exact
qualified CopperStart/CopperOS pair today. The current ROM is a baseline only.

Before a later contract revision can admit a pair, its producer handoff must
provide the following concrete data. Null values are blockers, not wildcards.

| Record | Required identity and evidence |
| --- | --- |
| Pair | Unique immutable pair ID, contract/schema revisions, explicit development/release classification and exact compatible component identities. |
| ROM | Producer version/build identity; raw file byte count/SHA-256; exported source snapshot and compiler/SDK hashes; existing or new bounded app qualification. |
| System seed | Bootable standard DD DOS1/FFS ADF, exactly 901,120 bytes; SHA-256; boot-block and filesystem verification; complete file-tree manifest with canonical paths, byte counts, hashes, guest protection bits and dates. |
| Shell | Actual relocatable 68000 `L/Shell-Seg` HUNK, exact entry/exports, version tag, hash, minimum stack, no-FPU requirement, dependency closure and zero managed-runtime/exception/helper gates. An ASM listing or managed DLL is insufficient. |
| Components | Exact admitted command/library/demo versions and hashes, public library/LVO/layout requirements, producer admission reports and commercial distribution/notice records. Reference OS binaries and unadmitted loose `out/C` files are excluded. |
| Native boot binding | Producer-owned startup/registration descriptor binding the Shell to a real DOS Process/CLI, inherited streams/locks, argument ABI, continuation return/wait and teardown. Merely invoking the current `ShellTaskEntry` as an arbitrary HUNK is not sufficient. |
| Guest ABI | A producer-versioned descriptor and SHA-256 for public library dependencies and any private Shell/DOS context, export, frame, continuation and signal layouts crossing component boundaries. Names alone do not establish ABI compatibility. |
| Build/release | Producer source revision plus dirty snapshot where applicable; tool/SDK versions/hashes; dependency locks; rights/notices; verified reproducible output receipt and controlled CI acquisition. |

The observed export names `copperos.shell.entry`, `copperos.shell.child`,
`copperos.shell.execute-begin`, `copperos.shell.execute-poll` and
`copperos.shell.execute-park` describe source interfaces, **not an existing
versioned ABI**. Optional pipeline support also declares
`copperos.shell.queue-handler-task`; its presence does not admit pipes or prove
Queue-Handler lifecycle completion. Private DOS state/layout compatibility must
be frozen by the producers for the selected binary pair. CopperScreen must not
patch their pointers, install missing vectors, or inspect private ROM receipts
as a production compatibility handshake.

Release package paths are relative to a single controlled `GuestAssets/<pair-id>`
root. Future admission rejects absolute/traversing paths, duplicate or
case-colliding names, unsupported schema versions, missing inputs, size/hash
mismatches and incompatible ABI/CPU requirements before replacing a running
session or workspace. Offline startup uses the qualified bundle only; no
runtime download or discovery in developer folders supplies missing files.

The future image includes producer-owned `S/CopperOS-Release.json` with exact
OS version, pair ID and ABI descriptor identity. Its bytes are bound by the
system-tree manifest. This file is a **required new handoff artifact**, not a
file found during inspection. Host-visible version reporting comes from the
verified pair manifest; a guest startup banner/prompt confirms the running
environment separately. Both must agree in qualification.

## Machine and native boot sequence

Target PAL A500: 68000 interpreter, 8371 Agnus/8362 Denise, 512 KiB Chip plus
512 KiB slow RAM at `$C00000`, no Fast RAM/RTG/RTC, one DF0, no HDF or shared
folders. This retains the existing bounded hardware. Shell/stack/filesystem
memory usage and startup/steady-state budgets must be measured before admission;
no minimum stack or claim that the full Shell fits is invented here.

The producer system image supplies a standard boot block that enters the
native DOS startup path. DOS establishes `SYS:` and standard assigns, then
`S:Startup-Sequence` selects/installs the CopperOS Shell through the qualified
native registration owner. `L:Shell-Seg` must receive a valid guest Process/CLI
and its documented bootstrap context. The Shell reaches the agreed interactive
prompt, accepts actual keyboard input, launches/exits the demonstration and
returns to the prompt after a command error. The host remains controllable.

The present ROM instead reads Startup-Sequence through `BootScriptCore` and
then enters its own bounded interactive dispatcher. Its System ROM project
does not reference the CopperOS Shell. Copying `L/Shell-Seg` onto a disk does
not implement registration, child-shell ownership or the ABI handshake.
Extending that native boot boundary belongs to the producers and requires a
newly qualified ROM/pair; the P1-03 ROM allowlist is not relaxed by this draft.

Keyboard/console interaction and host mouse controls/input release are required
for the CLI slice. Any advertised guest mouse-driven application/desktop needs
its own declared asset and input qualification; a CLI prompt does not establish it.

## User storage and updates

The proposed v1 storage target is a **user-owned writable clone of the DF0
system ADF**, retaining one-drive hardware. The package seed stays immutable.
This requires native writable DOS filesystem **and trackdisk** support plus
host persistence integration; both remain qualification blockers. Making the
engine drive writable alone cannot overcome the present ROM's
`ERROR_DISK_WRITE_PROTECTED` behavior. A host folder/Legacy DOS bridge must not
be substituted to make this workflow appear complete.

The current app also rejects write-enabled DF0 for its qualified CopperStart
slice. A future writable pair needs explicit pair-specific host admission and
native qualification; removing that existing restriction globally is not the fix.

The intended paths below are symbolic roots, not currently created directories:

```text
package/GuestAssets/<pair-id>/System.adf
user-data/CopperOS/Workspaces/<workspace-id>/System.adf
user-data/CopperOS/Workspaces/<workspace-id>/workspace.json
```

`user-data` means the [P1-01 per-user directory](PROFILE_STORAGE.md). First run
creates a unique workspace from a verified seed using a temporary file and atomic
promotion. Its versioned sidecar records workspace ID, exact pair/ROM/OS/ABI
identities, seed hash, workspace-format version and migration origin. The
mutable image must not be rejected simply because its hash differs from the
original seed after legitimate writes.

Guest settings and user files live in the writable volume. A future save path
must first complete guest writes/flush and take the snapshot under the existing
single machine owner, then atomically persist it outside the package. A failed
save must retain dirty state, the previous disk file and export/retry controls.
Automatic persistence after normal exit/restart/reset is **not implemented or
qualified by this contract**; P1-04/P1-06 must prove those semantics, low-space
handling and protection of originals. No concurrent host disk write may race
guest DMA or a live guest filesystem operation.

An app/MSIX update never overwrites an existing workspace with a fresh seed.
Reopening verifies its pair identity before mounting: an unknown/incompatible
pair stops with export/recovery guidance, never silent boot with the latest ROM.
The update manifest must explicitly list supported source workspace/pair
versions. Migration retains the original image/sidecar, works on a new clone,
and promotes the new pair only after successful migration and validation.
Reset/repair similarly creates a separate workspace and preserves or exports
the old one. Rollback boot is offered only if its exact admitted runtime pair
is still available; preservation/export is required even when it is not.

## Required evidence and next implementation order

| ID | Owner and concrete next deliverable | Completion criterion |
| --- | --- | --- |
| OS-01 | CopperOS: compose original bootable system seed and versioned release/tree manifest. | Independent boot-block/filesystem/tree/hash verification; no reference OS files; a clean build agent can obtain the exact candidate. |
| OS-02 | CopperOS + CopperStart: emit `Shell-Seg` and implement native boot registration/Process/CLI handoff. | Cold boot on Lightweight reaches the actual CopperOS Shell; a command and child execution return correctly; errors retain host control. |
| OS-03 | Producers: freeze guest ABI descriptor, stack/memory requirements and compatible component identities. | Static checks plus runtime lifetimes/memory budgets pass for the exact paired binaries in the bounded A500. |
| OS-04 | CopperStart + host/storage: native guest writes and user workspace clone/save/reopen. | Create/read/rename/delete a file; save settings; close and restart; recover from full/read-only/damaged volumes without replacing existing work. |
| OS-05 | Product + producers: original demonstration, versions, licences and redistribution notices. | Exact included content/rights inventory; launch and exit the cleared demonstration. No assumed licence inherited from CopperScreen. |
| OS-06 | Producers + release: frozen exports and controlled artifact acquisition. | No sibling checkout, local cache or compiler required to build/install CopperScreen; input/output hashes and provenance retained. |
| OS-07 | Host + release: exact-pair source and installed qualification. | Clean offline first run, input/video/audio, reset/restart, invalid asset/ABI recovery, guest data/settings retention through signed MSIX update, and repair/export checks. |

Start with OS-01/OS-02's **read-only boot proof** using original diagnostic
content, while OS-03 freezes its identities. That proof is a development
milestone only; OS-04 is still required for the product's persistent-file
workflow. OS-05/OS-06 must close before shipping; producer policies cannot be
bypassed by renaming a fixture or marking a manifest qualified.

The future evidence record must bind ROM, OS seed, Shell, demo, input scripts,
workspace sidecar, producer ABI and app/engine/CPU binaries by hash. Record
loading, usable prompt, input-driven demonstration, file operations and
post-update persistence separately. Compiler success, directory inventories,
boot completion or a skipped optional native case cannot substitute for these
observations. Performance uses the existing [measurement rules](engine/PERFORMANCE.md)
and an explicitly frozen workload; no new performance result is asserted here.

## Revision and validation rules

This revision contains no producer version or content hash for a missing OS.
The schema locks that observed absence and the activation/Legacy restrictions.
Completing assets or changing machine/storage/ABI policy requires a new reviewed
contract revision and evidence; do not overwrite `1.0.0-draft.1` with a different
meaning. A later executable pair-manifest schema and admission implementation
must be introduced explicitly before the app/package builder can consume it.
Existing published package versions and historical evidence remain immutable.

The inspection revision changed documentation and contract metadata only. Validate JSON/schema,
links, source observations and the correspondence with the selected ROM contract.
No emulator build/replay or Store installation was needed or performed to claim
this inspection. P1-04 remains open in the [product roadmap](PRODUCTIZATION_ROADMAP.md).

Inspection validation passed: the draft matches its schema; mutations enabling
activation/shipping/Legacy, selecting Legacy as the engine, changing the schema
version or claiming a missing system image were rejected. The selected ROM
contract, all 21 producer source records, 61 loose HUNK files, 30 diagnostic ADFs
and ten staging placeholders were rechecked against recorded sizes/hashes.
Repository documentation links and whitespace checks pass. These checks do not
execute or qualify any guest component.

The schema check uses PowerShell's existing validator, with no new dependency:

```powershell
Test-Json -Json (Get-Content packaging/guest/copperos-contract.v1.json -Raw) `
  -Schema (Get-Content packaging/guest/copperos-contract.schema.json -Raw)
```
