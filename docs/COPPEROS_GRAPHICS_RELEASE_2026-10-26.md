# Blitter release-path checkpoint — 2026-10-26

The graphics-release.19 development bundle reduces DisownBlitter register saves
and zero stores. It follows [word-fill.18](COPPEROS_GRAPHICS_WORD_FILL_2026-10-25.md).
The [producer record](../../CopperStart/Docs/NativeGraphicsRelease20261026.md)
links the code, profile, controls and remaining work. This is an ownership-release
optimization, not a package publication.

| Input | SHA-256 |
| --- | --- |
| Combined 1 MiB ROM | `ac97471f40382c7fb3732b9e591a503ec5954195afea0f7977806fec897a98a3` |
| Unchanged DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |

The contract and build hash gate select this ROM. Reset stays exact .9–.18;
GUI/Home/HDF and CPU/chipset execution remain unchanged. Personal disks need no
HDF migration. Standalone/resident images shrink by eight bytes to
24,230/28,137 bytes.

A0 handles the task comparison and then supplies zero for the release stores.
A1 is saved only during cold handoff. Tag, both closed checks, owner binding,
pending heads, atomic Lock/Nest release and serialized dispatch remain in place.
Every integer register remains preserved; uncontended calls add no interrupt
masking.

Completed median guest cycles, drawing including public WaitBlit:

| Call | Before (.18) | After (.19) | Kickstart |
| --- | ---: | ---: | ---: |
| DisownBlitter | 338 | 302 | 64 |
| RectFill 8×8 | 2,882 | 2,846 | 2,270 |
| RectFill 16×4 | 2,812 | 2,774 | 2,270 |
| Text 64 characters | 35,182 | 35,148 | 30,480 |
| BltBitMap 16×8 | 5,132 | 5,096 | 2,416 |

The release gain is 10.7%. All 34 comparisons retain selected original V40.63
state/pixel/result matches; an independent build reproduces every native median.
Final-DMA overlap can hide the release gain for large asynchronous draws.
Substantial performance gaps remain, including OwnBlitter at 356 versus 62 cycles.

All **112 producer graphics tests pass**, zero skips. Six new cases verify
sleeping-task handoff, cached/absolute Exec binding, full register preservation
and wrong-task refusal on both CPUs. Existing instruction-boundary, FIFO,
lifetime, DMA and installed IRQ controls remain in place, along with the prior
2,016-case crossover. The independent build passes three installed comparisons
and 47 image/ownership/release cases, zero skips. Ten pre-staging pixel tests
pass. Source ownership checks 202 retained imports exactly once. CopperScreen
Release builds cleanly; the full producer solution retains its three existing
Shell/DOS API errors.
After staging, all 11 bundled native tests pass, zero skips, including desktop
boot, Editor/input, reset and cold reopen.

Desktop profiler and normal execution control agree at 764 fields /
108,565,932 cycles, including registers, RAM, pixels and PCM. Cold readiness is
15.063983851 guest seconds; Tab redraw is 12 PAL fields / 0.240378415 seconds.
The total is five cycles below .18 with unchanged frame counts; no desktop
frame-count speedup is claimed. Selected pixels retain SHA-256
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
Graphics symbols remain unattributed in this observer.

Local evidence is under `../CopperStart/artifacts/graphics-release`, including
`qualified.rom`, `qualified-emission`, `qualified-full-calls`,
`qualified-reproduction`, crossover and desktop reports, plus producer/consumer
TRXs. The ignored staged bundle is the app input. Complex APIs, contention
throughput and five flat-text overflow exclusions remain outside this comparison.
These are guest cycles, not host acceptance or physical timings.
