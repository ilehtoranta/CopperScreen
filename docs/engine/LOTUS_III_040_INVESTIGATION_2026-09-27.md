# Lotus III on 040: input phase, not an established disk defect

The disk-two stall in the [initial 030/040 validation](CPU_OPTIONS_030_040_060_2026-09-27.md)
is localized to the game's fire-edge polling race. The original scripted presses
miss the consumption window. Moving only the first press/release from fields
6,500/6,520 to **6,502/6,522** lets the **unchanged 040 binary** pass the prompt,
load disk two, show the title and run a driving demo. Normal and scalar execution
match all 95 checkpoint files through 18,000 fields.

This is a separately recorded input variant, not an emulator timing correction
or a replacement of the original failed replay. No demonstrated CPU instruction
or disk-controller defect was found in this prompt path. Exact 040 hardware
timing and broader compatibility remain unverified.

## Where the original presses disappear

The external observer uses the existing instruction/interrupt boundaries only
around the press windows. Its 60 captures through field 11,000 match the original
040 replay byte for byte. The equivalent 030 trace also matches all 60 captures
of its uninstrumented run. The observer adds no production callbacks or tracing.

The game clears its new-press byte at `$C0881A` in `$69B42`, calls the polling
function, tests the bit at `$741B0`, and branches on the saved Z flag at `$741B6`.
The interrupt handler stores the new fire edge at `$75C92`. If the interrupt
arrives after the bit test, RTE restores the earlier Z flag; the branch misses
the newly set byte, and the next main-loop iteration clears it.

| Model / input field | Interrupted PC | Observed outcome |
| --- | --- | --- |
| 040 / 6,500 | `$741B6` | Bit test already missed; edge cleared on next loop |
| 040 / 8,500 | `$741B6` | Same missed edge |
| 040 / 10,500 | `$741B6` | Same missed edge |
| 030 / 6,500 | `$69B4C` | Next loop clears the edge |
| 030 / 8,500 | `$741AA` | Returns before bit test; consumes the edge at `$741B8` |
| 040 / shifted 6,502 | `$741A6` | Returns before bit test; consumes the edge at `$741B8` |

For the original 040 field-6,500 press, the handler stores `$10` at motherboard
cycle `923641382`. RTE returns to `$741B6` at `923642603`, preserving Z=1. The
main loop clears the edge at `923642664`. This happens before the disk-ID call
at `$69B5A`; the observed miss cannot be explained by a rejected disk-two ID.
The same pattern is directly traced for the next two presses.

In the shifted run, the handler stores `$10` at `923925572`. RTE returns to
`$741A6` at `923926793`. The bit test sees the edge, and `$741B8` is reached at
`923926853`; the prompt timer then becomes `$FFF4`, allowing continuation.
These are within-run causal observations, not an aligned hardware timing trace.

## Cache control and independent reference

The 040 has `CACR=0` throughout the traced windows; the 030 has `CACR=1`.
The [040 CACR correction](CPU_OPTIONS_030_040_060_2026-09-27.md#cpu-and-host-changes)
is retained. Bit zero is not the 040 instruction-cache enable bit, so enabling
the cache by treating it as an 020 would be an architectural error.

WinUAE **6.0.3.0** was run with the exact same ROM and extracted disk hashes,
PAL OCS, 512 KiB Chip + 512 KiB slow RAM, 68040 configured at 28,375,160 Hz,
JIT disabled and CPU compatibility/cycle-exact/memory-cycle-exact options enabled.
Configuration queries record these requested settings; they are not a claim of
cycle-exact 040 hardware emulation. Its prompt also has **CACR=0**. A manually
timed fire press advances into later game code at `$69BB6`, then `$782D0`.
The reference process was closed after inspection.

That independently supports cache-disabled media/040 continuation. It does not
compare the same fixed-field input sequence, initial pipeline state or physical
bus timing. The native phase experiment is the direct evidence that the current
040 can pass this prompt without a CPU or chipset change.

## Separately named replay and validation

The new [040 phase variant](../../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3-040-phase.json)
differs from the original portable Lotus script only in the two frame numbers.
Its hold duration, later presses, disk swap and other inputs are unchanged.
The frozen six-title 68000 manifest, its original Lotus input and its baseline
are unchanged. This variant is not silently substituted into that suite.

| Check | Result |
| --- | --- |
| Original 040 trace, 11,000 fields | Reproduces the three missed edges; all 60 available captures match baseline |
| Original 030 trace, 11,000 fields | Second press consumed; all 60 captures match baseline |
| Shifted 040 trace, 7,000 fields | First press consumed; all 40 captures match the uninstrumented shifted run |
| Shifted 040 normal vs scalar, 18,000 fields each | All 95 state, image, RAM and PCM captures byte-identical |
| Visual field 7,000 | Magnetic Fields logo |
| Visual field 9,000 | Lotus III title |
| Visual field 10,000 | Single-player driving **DEMO** |
| Visual field 15,000 | Car selection |
| Runtime boundaries | No CPU, chipset or video unsupported stop |

The final shifted identity is motherboard cycle `2557701195`, CPU
`A643BF2282585AC8`, hardware `DC05F68AFFAD3918`, output `0A9F5BDD29E6E617`.
It is an input-variant identity, not a new hardware golden or an update to the
original failed replay. Player-controlled racing and full game completion are
not established.

The [manifest](LOTUS_III_040_INVESTIGATION_2026-09-27.json) records both disk
identities, package/runner hashes, input changes, trace hashes, all captured-file
hashes and reference evidence. Local files remain under
`artifacts/lotus3-040-investigation/`. Both runs use the same unpublished `.55`
candidate as the initial CPU integration. No package publication occurred.

To reproduce, supply the matching second disk as `media/lotus3-disk2.adf`, or
make a local copy of the variant and set its field-5,100 `diskPath` to the full
path/ZIP selector for your disk. Use a fresh output directory:

```powershell
dotnet CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll --cpu 68040 --rom "path/to/Kickstart_13.rom" --disk "path/to/lotus3-disk1.adf" --frames 18000 --input-script CopperScreen.Lightweight.Tests/Workloads/corpus-lotus3-040-phase.json --boot-probe artifacts/lotus3-040-new --boot-probe-interval 1000 --boot-probe-extended
```

Add `--scalar-cpu` and choose another output directory for the scalar comparison.
The output requires the current local package candidate until a release is
authorized. No ROM or media is included. These are correctness diagnostics;
their printed FPS is not throughput acceptance. Only evidence, the separate
input fixture and current documentation changed, so emulator suites were not
rerun for this investigation.

The remaining timing question requires an independently specified 040 execution
and bus contract or matching hardware evidence. An arbitrary delay that makes
the original scripted press land in the window would not resolve that question.
