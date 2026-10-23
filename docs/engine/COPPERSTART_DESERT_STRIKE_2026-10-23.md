# CopperStart Desert Strike: Air Superiority flight — 2026-10-23

The rebuilt CopperStart ROM boots the supplied Desert Strike three-ADF set,
loads Air Superiority and reaches player-controlled flight from the frigate
to the coast. Turning, chain-gun fire, Space weapon selection, Hydra firing,
F10 map entry/exit and mouse selection of status/mission panels work. Native
Kickstart 1.3 reaches the same bounded milestones with identical physical
inputs. No new ROM, CPU, disk or engine correction was established.

## Configuration and media

The unchanged 524288-byte CopperStart ROM is produced by
`cf7c9b54aefea723b018bd68e10e829e67684250`, SHA-256
`476f9d1d0afd61316a4e47b21d00e45d65ff54b2730296653629d10857c5c721`.
All five runtime binaries retain the exact identities qualified in the
[Nitro record](COPPERSTART_NITRO_2026-10-23.json), including Copper68k
`1.5.5-nitro.1`, CopperDisk `3.0.1-dev.1` and the frozen functional harness.
Their source qualification is bound to `d9bfc96b4c1f6bc9eb96969ac8a3f9d156a3bf5e`.
The evidence checkout merged newer source at `77a6be9946fafc3673f07c819378cfab0cbfa41e`
while final replays were running. This record retains the selected frozen
runtime and does not qualify that concurrent source merge.

The profile remains PAL OCS 8371/8362, the 68000 interpreter, 512 KiB Chip plus
512 KiB slow RAM, no Fast RAM or RTC, one read-only DF0 and the full 908×313
raster. Native Kickstart 1.3 uses the same machine profile. The archives are
labeled `Desert Strike - Return to the Gulf (1993)(Electronic Arts)(Disk n of 3)`.
Each contains one standard 901120-byte ADF:

| Disk | ADF SHA-256 |
| --- | --- |
| 1 | `6a961cc2aac90fd39ba8d8c1eb3020517aac201a05b00c34448834be52fd7f02` |
| 2 | `7ceb60b1fb3a9683dda55f4184410a31885d7fd79c5f37616bd7c0ad634662c6` |
| 3 | `76912e3cdfc26724ebcf77faff3e1dd2c119eaa93b54135b3df5f0268b9ad2b5` |

Archive labels alone do not establish pristine original-media provenance or
protection qualification. No media conversion, additional crack, trainer,
password entry, saved-game load, guest-memory patch or state restoration is
used. DF0 is physically replaced as requested: disk 1 → 2 → 3 → 2.

## Observations

| Completed field | Reviewed observation under both ROMs |
| ---: | --- |
| 1000, 3000, 5000 | Electronic Arts logo, title and animated Apache introduction. |
| 8000, 10000, 11000 | Animated story scenes and coherent text. |
| 13000–15000 | Disk-two prompt after the mouse click at 12002. Eject at 15002; disk two mounted at 15122. |
| 17000, 18000 | Main menu: Big Dave, joystick controls, sound and movies enabled. |
| 21000 | Air Superiority briefing after fire at 19002. |
| 22000–24000 | Disk-three prompt after fire at 21002. Eject at 24002; disk three mounted at 24122. |
| 26000, 27000 | Apache loading illustration, followed by a disk-two prompt. Eject at 28002; disk two mounted at 28122. |
| 30000–32000 | Mission world, Apache on the frigate and animated sea. This is not yet successful takeoff. |
| 32600 | F10 map: guns 1178, Hydras 38, Hellfires 8, lives 3, load 0, fuel 100, power 600, score 0. |
| 32800, 33000 | Mouse-selected campaign status and radar-site mission information. |
| 36400, 36600 | Reverse flight clears the frigate; visible airborne Apache and shadow over the sea. |
| 37000, 37400 | Right turn and forward flight reach the coast; chain-gun fire during approach. |
| 37600 | Space selects Hydras; the armed message appears during firing. |
| 38000 | F10 returns to mission information: guns 1161, Hydras 37, Hellfires 8, fuel 89, power 600, lives 3, load 0, score 0. |

The [publisher's Amiga manual](https://openretro.org/file/b7466131a063d2c4f2d24ef88570bd234d27437e/Manual.pdf),
pages 4–10, documents the disk prompts, campaign/menu controls, joystick flight,
Space weapon selection and F10/0 map. The map pauses flight and provides status
and mission information. Its first-campaign quickstart is on pages 13–15.

Early forward/fire inputs left the helicopter on the frigate. A separate
read-only instruction trace confirmed that input `$41` reached the guest;
the final workload retains those trials, including an alternate-port trial,
without counting them as successful flight or weapon use. The reverse input
at 36002 produces the first demonstrated departure. A separate WinUAE 6.0.3
native-1.3 diagnostic with the same ADFs also departs with reverse flight and
reaches land. No input workaround or guest-code patch is introduced.

This supplements the [October 19 opening-flight and landing record](COPPERSTART_DESERT_STRIKE_2026-10-19.md)
with the later rebuilt ROM/runtime, coastal flight and demonstrated chain-gun
and Hydra use. That earlier record already documents backward takeoff and the
complete disk sequence. The [original end-stop correction](DESERT_STRIKE_END_STOP_2026-09-24.md)
and its bounded drive-travel model remain unchanged; its unavailable-disk-three
limit describes the media supplied at that earlier date.

## Verification and limits

All three independent final reset replays exit successfully at field 38000,
without a reported unsupported operation. Each retains 1905 files across 381
captured fields. CopperStart normal/scalar captures are byte-identical, and
every final filename, size and SHA-256 matches its corresponding discovery
manifest, including native Kickstart. Captured nonzero PCM is present in 231
fields under CopperStart and 234 under native 1.3; this establishes sample
presence, not listening or audio fidelity.

The complete shared [physical-input workload](../../CopperMod.Amiga.Lightweight.Runner/Workloads/desert-strike-copperstart-campaign1-v1.json)
contains 61 events. Final input and exit-control files are written before
process creation. Each reset replay retains state, both RAM banks, the full
raster and field PCM at field 1 and every 100 fields through 38000.
The [machine-readable record](COPPERSTART_DESERT_STRIKE_2026-10-23.json) retains
commands, binary/ROM/media/input identities, capture manifests, independent
reset comparisons and the separate diagnostic provenance.

Only documentation and the workload change in this task. The qualified runtime,
harness and ROM are unchanged, so builds and emulator suites are not rerun for
this record. The concurrent source merge has separate validation scope.
ROMs, media, captures and build outputs remain local. Historical native
workloads, performance intervals, fingerprints and acceptance dispositions are
unchanged; this is not a throughput measurement.

Enemy destruction, rescue, landing/refuelling, objectives, campaign completion,
later campaigns, Hellfire launch, other co-pilots/control methods and password
reload remain unqualified. Native Kickstart runs on the same engine; the
WinUAE diagnostic has no field-matched input/capture comparison. Neither is a
physical hardware oracle or complete timing qualification.
