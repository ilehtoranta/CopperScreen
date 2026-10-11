# Lightweight A501 RTC — 2026-10-07

Lightweight now includes the optional RTC formerly available in Legacy. Enable
**Settings > Memory > RTC clock**, or pass `--rtc` to the headless runner.
Changing the fitted-clock setting requires a machine restart. Existing default
profiles keep their explicit `rtcEnabled: false`; no default output identity is
changed by fitting a clock automatically.

## Provenance and behavior

The port starts from CopperMod's `Machine/RealTimeClock.cs` and
`AmigaRealTimeClockTests.cs`, inspected at commit
`39b183d63a13caa2429fb80ffcab11e0752d42f7dc2126e3`. The implementation is owned
here; there is no build reference or runtime dependency on that checkout.

The Legacy A501-compatible aperture is `$DC0000–$DC003F`: sixteen nibble registers
at four-byte strides, with Legacy's four byte-lane aliases. Seconds, minutes,
hours, day, month and two-digit year are BCD; weekday is derived from the date.
Controls D/E/F retain their low nibbles, D.BUSY reads clear and F starts in
24-hour mode (`$4`). Byte/word/long transfers preserve Legacy's byte order and
go through the existing Agnus CPU grants. Disabled addresses retain Lightweight's
existing unmapped `$FF` behavior, rather than Legacy's unrelated unmapped-zero
policy.

`RtcEnabled` and optional `RtcNowProvider` are construction-time engine settings.
The default source is host **local** time, sampled on the execution owner only
when the clock is accessed. Guest writes adjust a session-local offset without
changing the host clock. The RTC continues according to host time during pause;
emulated CPU cycles and accelerated frame execution do not advance it.

Hardware reset and CPU RESET restore control defaults while keeping guest time.
An enabled-to-enabled `CreateRestartCandidate` copies clock state independently;
disabling the clock removes it. A fresh machine/application launch starts from
host time. The selected setting persists through ordinary profile saving, but
there is no battery-state file retaining guest offsets across application launches.

## Native correction

The unchanged Legacy date setter truncates 29 February 2028 to 28 February when
Kickstart writes the day/month before the leap year. Both normal and scalar native
checks reproduced the one-day difference. Lightweight now stages calendar digits
while CD.HOLD or CF.STOP is asserted and commits the completed calendar on release.
This also provides a coherent frozen calendar for native reads. Unheld individual
date writes retain Legacy's clamping behavior. The year window remains 1978–2077.

The control-bit definitions and calendar register layout come from the
[OKI MSM6242B manufacturer datasheet, register table and control D/F descriptions](https://w140.com/tekwiki/images/c/ca/Msm6242b.pdf).
This is a bounded digital implementation, not a claim of physical chip timing.

## Verification

`LightweightRealTimeClockTests` covers Legacy BCD values, byte aliases, word/long
access order, guest offsets, host-time progression, the year window, calendar
rollover, 12-hour readback, resets, restart state isolation, disabled mapping,
bus grants, coherent leap-day writes and HOLD release.

`CopperScreenRtcTests` checks setting availability, saved-profile round trips,
session restarts and the existing expanded 68040 ROM profile, which RTC support
now admits without changing its CPU, memory or drive configuration.

`LightweightNativeRtcTests` uses supplied A500 Kickstart 3.1 (40.63), SHA-256
`8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE`.
After 1,500 ROM-only boot fields, an original guest diagnostic opens
`battclock.resource` through native Exec and executes its native ReadBattClock
and WriteBattClock vectors. It checks local time against the 1978 epoch, writes
29 February 2028, verifies advancing host time, resets, boots another 1,500 fields
and reads the retained value. Normal and scalar modes both pass. No native
instruction or OS service is replaced, and no guest media is mounted or modified.
Vectors are from supplied NDK 3.1 `EXEC_LIB.FD` and `BATTCLOCK_LIB.FD`; the API
contract is described in the [AmigaOS BattClock documentation](https://wiki.amigaos.net/wiki/Battclock.resource).

Repeat with `COPPERSCREEN_KICKSTART31_ROM` pointing to the supplied ROM and run
the diagnostic project using its separate artifacts path. Without that input,
the native tests skip explicitly: unavailable coverage, not a successful replay.

## Final working-tree checks

The final Release solution build passes with zero warnings/errors. The complete
diagnostic engine suite passes 1,112 cases, including both supplied native RTC
runs, with zero skips. Host tests pass 173 cases; six other optional native-media
tests are skipped and remain unavailable coverage. The runner's `--rtc` synthetic
smoke completes without an unsupported feature. Documentation links and diff
whitespace checks pass. These results apply to the current working tree, including
its pre-existing chipset changes.

## Remaining limits

BUSY propagation, subsecond divider/reset behavior, 30-second adjustment,
standard-pulse/interrupt output and TEST are not modeled. HOLD/STOP is bounded
calendar freezing/staging, without physical carry qualification. Live 12/24-hour
mode changes during a held write and unheld 12-hour writes are not certified.
Invalid staged dates are clamped on release. The model derives weekday rather
than exposing an independently writable weekday counter.

Other RTC models, A500+/A3000-specific clock hardware, battery-backed RAM and
persistent battery files are separate features. The desktop option fits this
same A501-compatible interface; it does not certify every motherboard's RTC.
Native 1.3 SetClock/Workbench workflows and additional OS/CPU combinations remain
unverified. Build/tests and bounded native correctness do not establish a host
throughput acceptance measurement.
