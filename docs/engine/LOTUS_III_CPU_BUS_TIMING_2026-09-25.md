# Lotus III: CPU bus spacing — 2026-09-25

The original disk-two prompt now advances with unchanged media and input. The
production candidate reaches the title sequence and one-player and split-screen
**driving demos**. This is bounded compatibility evidence, not player-controlled
racing or a full game-completion claim.

## Correction and independent expectation

The [initial investigation](LOTUS_III_INVESTIGATION_2026-09-25.md) found that the
prompt discarded a correctly received fire edge after returning from its Copper
interrupt. A small synthetic polling program then exposed a more general issue:
Lightweight supplied data at the end of a one-CCK Agnus grant, and the CPU could
immediately request another transfer. Adjacent CPU strobes could therefore be
only **two CPU clocks** apart.

The [Motorola M68000 user manual](https://www.nxp.com/docs/en/reference-manual/MC68000UM.pdf),
section 5 (bus operation), specifies a four-clock standard bus cycle before
additional waits. The WinUAE 6.0.3 trace also places successive uncontended word
transfers two CCKs apart. Instruction-total cycle floors do not repair transfers
that happened too close together earlier inside the instruction.

Lightweight now implements the existing pinned Copper68k 1.4.1
`IM68000BusCycleTiming` interface:

- Start the CPU bus phase through the existing two-clock address-phase delay.
- Keep an Agnus transfer's data-ready cycle, but make its next bus opportunity
  two clocks later. Existing longword word grants remain separate.
- Keep four clocks per word between non-Agnus bus opportunities.

The change is in the engine's CPU/bus boundary. It adds no game-specific behavior,
guest patch, new scheduler, production tracing or dependency version. The CPU
package and disk/interrupt register behavior are unchanged.

## Discriminating regression and synthetic probe

`LightweightCpuBusSpacingTests` executes MOVE/CLR/branch loops from chip and slow
RAM, observing the actual CPU transfer strobes. Both cases fail on the old engine
at strobes 14 and 16, and pass with the correction. They assert the independent
four-clock minimum, not an instruction-count or title-specific timing constant.

The retained [Copper polling probe](../../scripts/probes/CopperPollingTiming/Program.cs)
generates an independently authored test ROM with no game or Kickstart bytes.
It includes a clear/check loop, a Copper interrupt, and RAM logging of interrupted
PCs and consumed flags. Run it with a new output directory:

```powershell
dotnet run --project scripts/probes/CopperPollingTiming -c Release -- artifacts/new-polling-timing
```

For this particular program the old engine interrupts at `$1104` in all 100
fields; the candidate alternates `$110A` and `$100A`. Candidate scalar and normal
runs have identical chip/slow RAM, cycles and final CPU PC. These distributions
are observations, **not hardware golden values** or a claim that more consumed
edges means better timing. The generated ROM can also run in the same PAL OCS,
68000, 512 KiB chip / 512 KiB slow WinUAE profile, without disks.

The generated ROM SHA-256 is
`B4BBF99E61796E74CA96A880080B944B68D8EE3B3BFA22BA56CE49076426373B`.
An initial diagnostic ROM omitted the interrupt-acknowledge vector bytes and
failed in WinUAE; it was corrected before the reference observations used here.
That failed probe is not evidence of a CPU defect.

## Validation

Candidate engine SHA-256:
`7EE0B872B7E2F4A491FCBBC4D8BDC2BBFD577DF34A1EBB62CF4B0148018EE3E9`.
The [evidence manifest](LOTUS_III_CPU_BUS_TIMING_2026-09-25.json) binds binaries,
scripts, logs, reference traces and native captures.

| Check | Result |
| --- | --- |
| Production Release solution | Pass; zero warnings/errors |
| Engine diagnostic suite | 728 passed; zero skipped |
| Host suite | 92 passed; three optional native tests skipped |
| Disk suite | 74 passed; zero skipped |
| Lotus III original script, 18,000 fields | Prompt passed; title and driving demos observed |
| Lotus normal/scalar parity | All 72 state/image/chip/slow-RAM files identical |
| Alien Breed SE '92, 16,000 fields | Matching second disk loads; menu/credits sequence retained |
| Desert Strike, 26,000 fields | Disk-three prompt retained; no drive-range stop |
| Desert Dream, 72,000 fields | Second disk and closing credits retained; no execution stop |

The unchanged Lotus script leaves the prompt after its third press at field
10,500: field 11,000 shows the Magnetic Fields logo and 12,000 the road background.
The first two scripted presses still miss; the original polling race remains.
Field 15,000 shows a one-player driving demo; field 17,000 shows a
split-screen demo. The black 18,000-field checkpoint is a later transition, not
the original disk-two wait. Raw captures remain under `artifacts/lotus3-timing/`.

The three optional host tests were unavailable coverage, not successful native
replays. The separate Alien Breed, Desert Strike and Desert Dream checks above
use their retained media/input scripts on the new production binary. Their CPU
timing is intentionally different from the frozen pre-correction engine; no old
golden fingerprints have been rewritten.

## Remaining limits

The reference trace still differs in CLR/MOVE prefetch placement and JSR target
fetch / stack-write order. The bus-spacing correction does not resolve those
shared-CPU instruction-order questions. WinUAE is supporting evidence, and the
two long-running polling traces were not a first-divergence comparison from
identical CPU/prefetch/beam states. Those limits do not weaken the independent
minimum-spacing regression, but they prevent a full cycle-accuracy claim.

The follow-up [performance verification](LOTUS_III_PERFORMANCE_2026-09-25.md)
is VALID and **owner accepted on 2026-09-25** with a scoped exception. Lores
and hires remain inconclusive (+1.3998% and +2.4203% upper bounds); native gameplay
passes (-1.1329% upper bound). Both native executions were independently validated
before the separately versioned comparison; no historical fingerprint was replaced.
The earlier combined native-corrections measurements also retain their unmet
lores gate. The exception covers only this Lotus comparison; the default 1% limit
and statistical dispositions are unchanged.
