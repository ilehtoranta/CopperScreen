# CopperOS Exec preemption — 2026-10-26

The A1200 CopperOS choice now uses development qualification `diskboot-2026-10-26-a1200-desktop-exec-preemption.36` and seed `copperos-a1200-desktop-2026-10-26-exec-preemption.10`.

Exec switches tasks on native interrupt return, with a four-VBlank quantum and higher-priority wake-ups. The desktop removes its explicit drawing/editor yields and the input Schedule hook. Input blocks on signals at priority 20. Layers protects shared boundary state; worker retirement owns its resources through an Exec MemList. A native idle-resume context-corruption bug is fixed and covered on 68000 and EC020.

| Measurement | .35 | .36 |
| --- | ---: | ---: |
| Cold readiness (s) | 13.822 | 12.159 |
| Drawing return (ms) | 104.373 | 41.568 |
| Key to drawing return (ms) | 138.329 | 85.660 |
| Settled drawing (ms) | 104.429 | 41.568 |
| Settled key to return (ms) | 138.564 | 85.660 |

These are guest-cycle measurements on PAL EC020/Alice/Lisa, 2 MiB Chip RAM and read-only DH0, not host-speed acceptance. Drawing completion is an exact code return; display scanout may finish later. The retained eight-field input sequence still has a 160 ms minimum observation window. Both normal and scalar execution agree exactly on cycles, registers, memory, pixels and audio in cold and field-1000-settled controls. The pixel golden is unchanged.

The bundled ROM/HDF and private system-file hashes are pinned by the [firmware contract](../CopperScreen/Firmware/CopperStartA1200.json), [media contract](../CopperScreen/Firmware/CopperOsA1200.json) and build hash gates. Recognized .9 and earlier system files upgrade with backup while unrelated data and custom system files remain protected. Restart the desktop app to load the rebuilt embedded bundle; the next cold CopperOS launch prepares the recognized workspace update.

The selected assets are retained locally in `../CopperStart/artifacts/exec-preemption/qualified.rom` and `../CopperStart/artifacts/exec-preemption/desktop-v2/`. Source/module/compiler provenance, native context details, counterfactual idle tests and exact asset hashes are in the [producer report](../../CopperStart/Docs/NativeExecPreemption20261026.md). CPU/chipset execution and consumer dependency pins are unchanged; no media or package is published.

Release build passes with zero warnings/errors. Verification has 116 focused Exec passes, 174 graphics/original-control passes, 11 bundled native passes and nine separately versioned A1200 console passes, all without skips. Host/migration has 83 passes plus three unavailable historical replays. Five recognized prior-seed migrations execute and preserve backups/unrelated content. The wider producer solution still has its three pre-existing Shell/DOS API errors, and the wider Layers suite retains nine pre-existing failures; those limits are detailed in the producer report.
