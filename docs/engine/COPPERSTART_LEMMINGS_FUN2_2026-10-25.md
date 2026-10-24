# CopperStart Lemmings: Fun 2 completion — 2026-10-25

The current rebuilt CopperStart ROM completes Fun 2, “Only floaters can survive
this”, with 100% rescued. Native Kickstart 1.3 reaches the same result using the
same physical inputs. Live Floater selection and assignment, umbrella descent,
rescue counting, the success/access-code screen and progression to Fun 3's
“Tailor-made for blockers” briefing work. No additional ROM, CPU or engine
compatibility correction was established; no public admission checks change.

This extends the [earlier Fun 1 completion](COPPERSTART_LEMMINGS_2026-10-23.md).
Every run reaches Fun 2 through normal progression and repeats Fun 1's 100%
rescue result. No password, saved state, trainer selection, media patch, guest
memory/register injection or host OS shim is used.

## ROM, runtime and media

The exact 524288-byte ROM has SHA-256
`fefe13e86ad00e3609ad4e385b5e3752e2407e1b5a9b67affa904d2160d9e8e7`.
It retains native emitter producer `0057fb9c38d82d38122ddf69a4aa960573beedd7`
and the compiled guest root from `cf7c9b54aefea723b018bd68e10e829e67684250`.
The [Full Contact record](COPPERSTART_FULL_CONTACT_2026-10-24.json) retains the
recomposition evidence. This continuation reuses that rebuilt image and checks
its checksum and equality to the retained independent composition. No producer
or runtime source is rebuilt here.

The runtime remains the exact merged CopperScreen
`29bc8635fe33ae9e267846ade511e9b368f7158d` binaries and pinned, unpublished
Copper68k `1.5.5-nitro.1`. The evidence parent is
`c9dc6ae4a23759ae4304993d1482b6bf2f57dd57`. The unchanged 25-field functional
harness is retained from the [Lotus 2 continuation](COPPERSTART_LOTUS2_GAMEPLAY_2026-10-24.json),
which records its build and earlier capture-cadence prefix check. That harness
is not rebuilt here. Its runtime/CPU/Disk/Float binaries are unchanged.

The profile remains PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip plus
512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313
raster. The supplied Skid Row two-ADF release is unchanged from the earlier
Lemmings replay. The physical Disk 1 to Disk 2 replacement remains in the input.
The companion record retains both archive and 901120-byte ADF identities.
Pristine original media and protection behavior remain unqualified.

## Reviewed progression

The [preserved publisher manual](https://www.dosdays.co.uk/media/games/lemmings/Lemmings%20-%20Manual.pdf)
documents mouse skill selection/assignment, Floater behavior and P pause in its
Amiga sections. A [supporting level description](https://lemmings.fandom.com/wiki/Only_floaters_can_survive_this)
identifies the last pillar's fatal drop and the Floater route. The observed
briefing and results establish the actual count and rescue requirement.

| Fields | Reviewed observation under both ROMs |
| ---: | --- |
| 14000–14500 | Fun 1 completed with 100% rescued using the retained route. |
| 16600 | Fun 2 briefing: ten lemmings, 10% required, release rate 50, five minutes. |
| 16800 | Level running; first lemming released, OUT 1 and timer 4:57. |
| 16825–16900 | P pause: four sampled full BMPs are identical within each ROM. |
| 16950 | After resume, the Floater panel is selected with ten uses available. |
| 17200 | Four released; Floater stock has fallen to seven through live assignments. |
| 17400–18000 | Umbrellas open during descent from the last pillar; all ten Floater uses are assigned. IN reaches 30% at 18000. |
| 18400 | IN 80%, two lemmings still outside. |
| 19000–20000 | All accounted for: 10% needed, 100% rescued, success message and level-three access code. |
| 21600 | Ordinary continue click advances to Fun 3's briefing. Fun 3 gameplay is not exercised. |

The external driver places the physical mouse near the hatch and submits 130
click pulses, each four fields down and four up, while the ten walkers pass.
The resulting shared frozen workload is replayed independently; no driver
reads or writes guest memory. Repeated clicks on already assigned walkers do
not consume additional uses in this observed game route. The cursor/view
position is set through ordinary relative mouse motion. Selection and
assignments occur after resume, rather than being claimed while paused.

## Verification and limits

Three independent runs start from reset and replay the complete shared input
through 21600 fields: CopperStart normal, CopperStart scalar and native
Kickstart 1.3. Each retains 865 sampled fields and 4325 full-raster BMP, state,
Chip RAM, slow RAM and field PCM files. Every CopperStart capture file matches
between normal and scalar execution. Normal and native reset replays also
match every filename, size and SHA-256 of their respective explorations. All
three exit zero with null captured CPU/video unsupported indicators. Gameplay
and result-screen review establish completion; those indicators alone do not.

Eight of nine selected milestone BMPs are also identical between ROMs. The
field-16800 gameplay-start images differ; both show the
first released lemming and subsequent reviewed milestones agree. No equal
cross-ROM CPU or hardware trajectory is asserted. The four pause samples are
16825, 16850, 16875 and 16900; later world motion, Floater consumption and rescue
counting establish resumed play.

The [companion JSON](COPPERSTART_LEMMINGS_FUN2_2026-10-25.json) records exact
ROM/media/binary/harness/input identities, commands, logs, capture manifests,
pause checks and image comparisons. The committed
[shared workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/lemmings-copperstart-fun2-v1.json)
contains the exact
UTF-8/LF bytes used by all three final runs. Producer/runtime source is
unchanged; retained build, engine, disk and native-ROM suite results are not
claimed as freshly rerun. Identity, input ordering, checksum, full capture and
documentation-link checks cover this evidence-only change.

Fun 3 gameplay, later levels/ratings, the other skills, two-player mode and
persistent save/password reload remain unqualified. Nonzero sampled PCM is
sample presence, not audio-fidelity evidence. Native Kickstart on this engine
is a supporting compatibility control, not a physical-hardware timing oracle.
Historical performance workloads and fingerprints are unchanged; this is not
a throughput measurement. ROMs, media, raw captures and temporary tools stay
local under `artifacts/copperstart-lemmings-fun2-2026-10-25/`. No package is
published.
