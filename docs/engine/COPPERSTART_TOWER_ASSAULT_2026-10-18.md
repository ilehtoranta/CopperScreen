# Tower Assault: opening play on original IPF disks

2026-10-18. The supplied original SPS 279 IPF release of Alien Breed: Tower
Assault boots, loads its matching second disk, opens the options and mission
briefing, and reaches input-driven opening-level movement and firing on a
freshly compiled CopperStart ROM containing the Overdrive fix. Native Kickstart
1.3 reaches the same milestones. No additional firmware or engine fix was needed
for this bounded case. The [evidence record](COPPERSTART_TOWER_ASSAULT_2026-10-18.json)
binds the exact inputs, commands, build, execution binaries and reviewed images.

The machine is a PAL OCS A500 with a 68000, 8371 Agnus, 8362 Denise, 512 KiB Chip
RAM, 512 KiB A501 RAM, no additional Fast RAM and one floppy drive. The supplied
ZIP contains three disks; this slice uses the first two. The
[earlier native investigation](TOWER_ASSAULT_INVESTIGATION.md) records the OCS
DENISEID correction already present in the unchanged execution engine. Its
historical hardware and performance results retain their original scope.

The [positive input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-tower-assault-level1-v1.json)
retains the existing corpus sequence: eject at field 3,200, insert disk two at
3,300, fire at 6,500/8,500, Escape at 12,100, Return at 13,000 and fire at 14,000.
The opening level is visible by 15,000. Upward movement at 16,000 and firing at
17,000 are followed by a longer right/up/left firing sequence at
18,000/18,300/18,600, release at 18,900, and another short fire pulse at 21,000.

The [idle input](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-tower-assault-idle-v1.json)
shares loading, options and mission-start events, then omits movement/fire from
field 16,000 onward. For each ROM, all 400 files before the changed input match
its positive run. Each control first differs visually at field 16,200 and has
30 different later images. Full-view review shows
movement, world scrolling and firing in the positive run while the idle control
stays by the landing site. This distinguishes player response from an attract
sequence or a successful runner exit. The positive run ends with Game Over by
field 22,000 after opening play; it does not complete the level.

Each 22,000-field normal/scalar pair matches all 555 capture files at 111
checkpoints, including images, CPU/device state, Chip/slow RAM and sampled PCM.
CPU, hardware, output and cycle fingerprints match within each ROM's pair.
Cross-ROM CPU/RAM equality is not a gate. Across the normal runs,
109/111 raw BMP files and 109/111 full decoded RGB images match. Only the early
boot fields 200 and 400 differ; every subsequent captured screenshot matches
byte-for-byte. The record retains the differing fields and full-image bounds.
The [unused fourth byte in 32-bit BI_RGB](https://learn.microsoft.com/en-us/previous-versions/dd183376%28v=vs.85%29)
is excluded only by normal RGB decoding; raw file identity remains mandatory
for both CPU-mode pairs. Local labelled QA sheets retain every source pixel at
the original dimensions. No display crop, ROM-byte patch or media edit is used.

The root was freshly compiled at `2026-10-18T12:51:22.9589033+00:00` using five retained root
sources and all eleven frozen, corrected component DLLs from the
[SE92 continuation](COPPERSTART_ALIEN_BREED_SE92_2026-10-18.json). Their hashes are
verified before compilation. The source qualification commit remains
[`84525a2`](https://github.com/ilehtoranta/CopperStart/commit/84525a27cb625d03b59be803d2988c6b5bfc5a16),
preserving the Overdrive expansion-memory preference, Alien Breed Supervisor
and `$FF8000` graphics changes, and the F17/Lemmings corrections. Compiler/SDK
and graphics-composer inputs remain frozen; native compile flags are unchanged.
The driver now creates its manifest directly from the current input identities
and output instead of inheriting historical input/vector tables. This corrects
qualification metadata without changing guest code.

The new 512 KiB ROM is byte-identical to the preceding corrected image, SHA-256
`5CA306B3A7ECE26A366ADC90E487FC384276A5463C22C3CE6B98AA4A118764D0`, with 459,374 code bytes and
26,098 graphics bytes at `$FF8000`. Its root has zero managed
allocation sites and zero framework features. This result comes from a new
native compilation. The unchanged image preserves the applicability of earlier
Overdrive regression evidence; those other games were not replayed again here.

Seven exact-ROM guest cases pass with no skips: fitted/absent A501 reporting and
reservations, a caller-owned trap-zero handler across Supervisor and native I/O,
fitted/absent allocation preference, and fitted/absent graphics opening with
caller-owned bitmap/raster storage. The fixtures hash-check the actual new ROM.
These cases are separate coverage from the original-media replays. The canonical
consumer Release solution builds with zero warnings; all 21 replay-tool controls
pass. No CPU or engine implementation changed. All four frozen execution-binary
hashes and Copper68k `1.5.4-nitro-dev.2` remain unchanged. The recorder's native
receipt mode avoids interpreting private bootstrap fields after takeover; each
actual `--rom` path/hash identifies the executed firmware.

ROMs, media, captures and logs remain local under
`artifacts/copperstart-tower-assault-2026-10-18/`. This verifies opening play;
disk three, level completion, later levels, save/load, two-player mode and audio
fidelity remain unverified. It is not physical hardware timing or throughput
acceptance. The frozen-component native ROM is qualified only for the recorded
bounded profile; the complete moving-main/MorphOS ROM and standard-app admission
remain outside this record.
