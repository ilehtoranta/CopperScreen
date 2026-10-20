# Native CopperOS Desktop and Home

The original CopperOS Desktop now runs as a separate native DOS Process on the
bounded CopperStart A500. Home uses one custom screen/window and one IDCMP
drain/handle/reply/Wait loop. It supports keyboard/mouse tile selection, About,
damage-clipped refresh and return to the console. Files and Editor are marked
Planned. The [producer implementation and replay notes](../../../CopperStart/Docs/NativeCopperOsHome.md)
and [machine-readable evidence](COPPERSTART_COPPEROS_HOME_2026-10-18.json) record
the exact development profile.

| Item | Qualified result |
| --- | --- |
| Complete ROM | 524,288 bytes, SHA-256 `492EF10926F73EDCF3EC7CAFC331E54A84C418924F765BA93AA4D504B20990CA` |
| Layout | 465,930-byte main code, 25,582-byte gap; original 31,906-byte graphics resident at `$FF8000` |
| A500 memory | 524,288 Chip + 524,288 Slow, no Fast RAM |
| Modules | Desktop command 368 bytes; Home 7,196 bytes; GUI provider 256,972 bytes |
| Native checks | 1 reset/boot + 3 Home + 6 input/text, all passed, zero skips |
| Source semantic checks | 12 task/boundary/ownership tests passed, zero skips |
| Home lifecycle | 4 interactive lifetimes, 7 startup-failure cases, 3 missing-provider attempts |
| Heap after Home and failures | Same 496,504-byte baseline after one-time initialization |
| Lowest sampled heap at event Wait | 342,584 bytes including replay/probe overhead |

The first real Desktop Process exposed native self-removal retaining its
33,040-byte process/stack allocation. Exec now reaps the task's public MemList
after selecting the next context. Qualification recompiles the complete ROM
from frozen inputs with one generic call-token overlay in a copy of Exec; all
other ROM producers and the final text/input GUI HUNK are unchanged. The source
implements the same fix. Earlier failing candidates and the rejected oversized
bounded-optimization build remain separate evidence.

The Desktop bundle binds the required ROM and GUI-provider hashes. This proof
uses original native DF0 media; CopperScreen's shipping HDF ROM/OS pin is not
updated by this work. A source release build or this development replay does
not admit a new shipping pair.

The [Files follow-up](COPPERSTART_COPPEROS_FILES_2026-10-18.md) completes bounded DOS
browsing, locks, navigation, selection/scrolling and rollback. File actions remain
future work.
Editor needs case-preserving glyphs, a capped 16 KiB text buffer, cursor/scrolling,
dirty state and recoverable save/replacement. Join open/save/reopen on writable
media and qualify Shell/Demo launch/return. The compact Home font folds lower
case to upper case, so translated input alone is insufficient for Editor.

The first checked Home is field 8,436 in the instrumented floppy journey.
Loader/drawing latency, largest free blocks, loader high-water memory,
100-cycle stress, sustained use, shipping-image integration and workspace/update
persistence remain release gates. This is functional qualification, not a host
throughput or UI responsiveness acceptance measurement.
