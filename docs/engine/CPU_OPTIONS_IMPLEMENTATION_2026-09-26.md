# Experimental OCS 020 CPU options — 2026-09-26

This record retains the initial `.50` implementation and its validation.
See the [subsequent cache and decoder correction](LOTUS_III_020_CACHE_2026-09-26.md)
for the later development package and native continuation results.

This implementation follows the [original package audit](CPU_OPTIONS_2026-09-26.md).
The original failed experiments remain historical evidence. The application,
engine configuration and runner now accept 68EC020 and 68020 as **experimental**
OCS accelerators. 68000 remains the default. 68010, 68030, 68040 and JIT are not
enabled by this change.

## Package boundary and release state

The initial implementation pinned development package `Copper68k 1.4.2-ocs020.50`, including the
desktop, engine, runner and package locks. There are no sibling project/source
references. CPU changes live on local CopperMod branch `codex/experimental-ocs020`,
commit `d4d0c8e20490110be1098a6e1a962aa109f99c51`, based on stable 1.4.1 commit
`8fb826eacf8e9dc32948b482c878079448bd6e61`. The owner's worktree is
`artifacts/cpu-options-development`; its branch preserves the source in the
CopperMod repository. Unrelated CopperMod work was left untouched.
A portable source patch is retained under
`artifacts/cpu-options-2026-09-26/source/0001-Add-advanced-CPU-operand-coverage-for-experimental-O.patch`.

The package is **unpublished**. No stable package was overwritten and no remote
push or package publication was performed. Local package artifacts live in
`artifacts/copper68k-feed`. `Directory.Build.props` adds this feed only when the
exact candidate is present. A clean checkout requires that exact package or an
explicitly authorized release before locked restore/CI can succeed. This is a
development candidate, not a merge-ready public dependency.

- NUPKG SHA-256: `73927D210B6F8897EA70597C13FB4A5C2695FF780E8FE0191E761ECFA57528FC`.
- DLL SHA-256: `F49268E9BA79E42C8F1CB54DD76673A3933C8E1BE5E1ECB64407A718B1E75B8D`.
- Informational version: `1.4.2-ocs020.50+d4d0c8e20490110be1098a6e1a962aa109f99c51`.

The package fixes EC020's optional host-code-reader capability and extends
advanced-core operand forms exercised by Kickstart and Lotus startup: MOVE,
MOVEM, arithmetic/logical, indexed comparison, condition-code, bit, jump and
rotate-through-extend operations. Added cases cover flags, width preservation,
signed/scaled and PC-relative addresses, pre/postincrement aliasing, A7 updates
and optional cache peeks. 184 new CPU cases accompany the corrections. The
68000 interpreter is unchanged and newly supported forms preserve the 68040's
existing fallback. Shared 68030 code receives the operand fixes, without claiming
native 68030 host compatibility.

## Host behavior and timing scope

Settings labels identify both new choices as experimental. Profile save/load
retains the choice, and applying it requires replacing the emulated machine.
The active profile label identifies an advanced CPU. The existing PAL OCS,
512 KiB Chip + 512 KiB slow RAM and native Kickstart 1.3 restrictions remain.
This does not provide A1200/AGA, Fast RAM, a selectable accelerator frequency,
MMU/FPU or save-state support.

The runner accepts `--cpu 68000`, `--cpu 68ec020` and `--cpu 68020`. Advanced
summaries and a separate `cpu-profile.json` identify the model, package and
`ocs-accelerator-v1` policy; the default 68000 capture format is unchanged.

```powershell
dotnet build CopperScreen.slnx -c Release
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release --no-build -- --cpu 68020 --rom C:/Data/ROM/Kickstart_13.rom --frames 3000 --boot-probe artifacts/my-020-boot --boot-probe-interval 3000
```

The [architecture](ARCHITECTURE.md) defines the single motherboard clock, 16-bit
bus transfers, EC020 address wrapping, unmapped 68020 upper addresses and untimed
cache peeks. Added instruction timing extends the package's existing operand-shape
policy. It is not a claim of physical cycle accuracy. Motorola's
[MC68020 User's Manual, section 8](https://www.nxp.com/docs/en/data-sheet/MC68020UM.pdf)
describes sequence, alignment, prefetch and bus effects that a bounded execution
policy cannot certify merely by passing its own tests.

## Validation

- Production Release build: zero warnings/errors.
- Copper68k: 1,683 passed; six optional external conformance suites skipped.
- Lightweight diagnostic tests: 750 passed, including 18 CPU-option cases.
- Host/profile tests: 98 passed; three optional native tests skipped.
- CopperDisk: 74 passed.

Engine coverage includes cache enable/reset, unaligned reads/writes, surrounding
byte preservation, 24-bit versus 32-bit aliasing, cross-boundary transfers,
single-clock progress, STOP interrupt masking, nonzero VBR, format-zero interrupt
frames, unsupported model rejection and the unchanged default. Host tests cover
availability plus save/load/restart into the selected CPU.

The exact TRX reports, native logs and captures are retained under
`artifacts/cpu-options-2026-09-26`. Optional test skips are unavailable coverage.
Native probes are correctness diagnostics, not performance acceptance.
The [machine-readable record](CPU_OPTIONS_IMPLEMENTATION_2026-09-26.json) retains
binary/report hashes, package identity and per-model native outcomes.
Locked restore passes for both the production solution and the isolated
diagnostic test project. Runner negative controls reject 68010, 68030, 68040,
unknown and missing CPU arguments.

### Native 020 results

On the final package, both models complete 3,000 ROM-only fields and display the
Kickstart 1.3 insert-disk screen. Both finish at motherboard cycle `426308358`,
with CPU fingerprint `9BEB88CC11409FC1`, hardware `147B7059FA2D0A1D`, output
`E356E4F944FBA179`, and no unsupported feature. The supplied ROM SHA-256 is
`EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`.

The 68020 Lotus III probe reaches its code-entry screen after 5,000 fields.
The existing 68000 input/disk-swap script completes 18,000 fields on candidate
`.45` without an unsupported instruction, but remains at the disk-two prompt.
This is **not a gameplay pass**. Its final fingerprints are CPU
`46218413F90EF1F5`, hardware `6ABD648BEAC28248`, output `98928BB8BCB02865`.
The final `.50` has the same CPU execution code; intervening changes are package
metadata, documentation and timing-plan test catalog entries. Complete game
compatibility, including this disk-two transition, remains open.
The 68EC020 repeats the 18,000-field scripted run on final `.50`, reaching the
same prompt with identical final CPU/hardware/output fingerprints and screenshot
bytes. Both advanced options therefore retain this known continuation limit.

The [follow-up investigation](LOTUS_III_020_INVESTIGATION_2026-09-26.md) localizes
the stall to correctly received fire edges being cleared before consumption.
Retimed/repeated inputs still stall; WinUAE's 020 passes the prompt with the same
media. A justified CPU/bus timing correction remains open.

### Default 68000 regression

The six-title normal/scalar suite **passes** against the retained
`native-regression-baseline-v1.json`: 326,240 fields with no baseline or parity
differences. Lotus III (18,000 fields per mode), Desert Dream (72,000), Tower
Assault (20,000), Alien Breed SE '92 (16,000), Super Cars II (19,000) and Lemmings
(18,120) all retain their captured CPU/hardware/video/audio results. The
machine-readable companion records each result and report hashes; the full local
report is `artifacts/cpu-options-2026-09-26/native-68000-final/report.html`.
Advanced captures do not replace 68000 goldens. Matching those retained outputs
is regression evidence, not independent physical hardware or throughput proof.

## Remaining scope

Missing advanced instructions still fail explicitly. Full addressing/exception
conformance, physical bus phases and cache-coherence behavior need independent
hardware evidence. Lotus disk-two continuation now has a traced lost-edge cause,
but changing chipset timing to force a new CPU through that prompt is not
justified by the evidence. Newer ROMs, memory maps, 68010 wrapper fixes and
68030/68040/JIT integration remain separate work. No throughput claim is made.
