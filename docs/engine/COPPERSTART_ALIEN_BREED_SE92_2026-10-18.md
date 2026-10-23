# Alien Breed Special Edition 92: opening-level compatibility

2026-10-18. The supplied original two-disk IPF release boots, loads its matching
second disk, selects Start Game and reaches input-driven level-one movement and
firing with a freshly compiled CopperStart ROM containing the Overdrive fix.
Native Kickstart 1.3 reaches the same milestones on the same PAL OCS 68000 A500
with 512 KiB Chip RAM, 512 KiB A501 RAM, no Fast RAM and one drive. No new
firmware or engine compatibility failure was demonstrated; no source correction
or removal of guest checks is required for this bounded case.
The [evidence record](COPPERSTART_ALIEN_BREED_SE92_2026-10-18.json) binds the exact
media, frozen execution binaries, build inputs, commands and reviewed captures.

The [earlier native investigation](DISK_BYTE_WORDSYNC_2026-09-24.md) established
disk loading and menu/credits only. Its script does not select Start Game.
The new [opening-level input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-alien-breed-se92-level1-v2.json)
uses the same disk swap at fields 8,000/8,100 and fire at 8,200. Three down pulses
at 9,200/9,400/9,600 select Start Game; fire at 10,000 starts it. Fire at 14,000
is retained in both runs after the briefing/loading transition. Movement and
firing begin at 16,000, continue through right/up/left/down/right inputs and stop
at 18,800.
The [official control guide](https://retrogames.biz/games/amiga/alien-breed/)
describes the menu choice and movement/fire controls.

The [idle control](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-alien-breed-se92-idle-v2.json)
has identical loading, menu and shared early fire inputs, then omits movement
and firing from field 16,000 onward. For each ROM, all 400 capture files before
the changed input match its positive run. Each control first differs visually
at field 16,200 and has 30 different later screenshots.
The positive run scrolls from the shuttle bay into an adjoining room while
ammunition decreases; the idle control stays in the starting bay. These controls
establish a player response rather than relying on the attract sequence or a
successful runner exit.

Both 22,000-field native/replacement normal-versus-scalar pairs match all 555
captured files at 111 checkpoints, including image, CPU/hardware state,
Chip/slow RAM and sampled PCM.
Their CPU, hardware, output and cycle fingerprints also match within each pair.
Cross-ROM CPU/RAM identity is not a gate. Across the two normal runs,
94/111 full-viewport decoded RGB images match; 4 raw BMP files match. The 32-bit BI_RGB files
also store an [unused fourth BGRX byte](https://learn.microsoft.com/en-us/previous-versions/dd183376%28v=vs.85%29),
so raw-file differences can be invisible.
The record retains both counts and every differing RGB field; neither CPU-mode
pair receives that relaxation. Field 12,600 captures different opening-display
initialization states; the field-16,400 difference is confined to a 2-by-8-pixel
ammunition-bar segment. No display crop, ROM-byte patch or media edit is used.

The native root was freshly compiled at `2026-10-18T10:53:41.1337392+00:00` using the five
retained root sources and all eleven frozen, corrected Overdrive component DLLs.
Their hashes match the [preceding qualification](COPPERSTART_OVERDRIVE_2026-10-18.json).
The source qualification commit remains
[`84525a2`](https://github.com/ilehtoranta/CopperStart/commit/84525a27cb625d03b59be803d2988c6b5bfc5a16).
The compiler/SDK and graphics composer are also frozen inputs. This preserves
the Overdrive Chip priority -10 / expansion priority 0 correction, the Alien
Breed Supervisor and `$FF8000` graphics changes, and the F17/Lemmings fixes.
The new 512 KiB image is byte-identical to the preceding corrected ROM, SHA-256
`5CA306B3A7ECE26A366ADC90E487FC384276A5463C22C3CE6B98AA4A118764D0`. It contains 459,374 compiled code bytes
and 26,098 graphics bytes. Its root has zero managed allocation
sites and zero framework features. Fresh compilation, rather than copying the
preceding image, produced this result.
The qualified build manifest reconstructs current identities from those verified
inputs and actual output files. The raw compiler manifest is retained separately:
its inherited `inputs` and `graphicsEntries` tables describe older builds and
are excluded from current input/address evidence.

Seven exact-image guest cases pass with no skips: fitted/absent A501 memory
queries and reservations, caller-owned trap-zero execution across Supervisor
and native I/O, fitted/absent allocation preference, and fitted/absent native
graphics opening/caller storage/raster allocation. The existing external-ROM
fixtures load this image only after checking its SHA-256; they are separate
coverage from the original-media replay. The canonical consumer Release solution
build passes with zero warnings; all 21 replay-tool control tests pass. No engine
or CPU implementation changed. The execution runner and pinned Copper68k
`1.5.4-nitro-dev.2` binaries retain their preceding hashes.
The replay recorder uses its native receipt mode for both ROMs to avoid reading
private bootstrap diagnostic fields after guest takeover. Each actual `--rom`
path and hash identifies the executed firmware.

Local ROMs, media, captures and logs remain under
`artifacts/copperstart-alien-breed-se92-2026-10-18/` and are not committed.
The JSON retains their path/hash identities, including the exact original
disk-two alias. This is opening-level compatibility, not completed levels,
two-player play, saves, later-game coverage, audio fidelity, hardware timing or
throughput acceptance. It qualifies the bounded frozen-component native ROM;
the complete moving-main/MorphOS ROM and standard-app admission remain outside
this record. The unchanged ROM hash also preserves the applicability of the
preceding Overdrive regression evidence without claiming those games were
replayed again here.
