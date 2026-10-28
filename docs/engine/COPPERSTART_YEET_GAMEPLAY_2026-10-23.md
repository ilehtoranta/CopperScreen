# YEET: main integration and first-trial gameplay

YEET 1.0.4 reaches the first Rune Trial board on a fresh CopperStart ROM containing
the integrated main changes. The replay selects the trial, pushes a rune (three
runes to two, moves 0 to 1), and resets the board (three runes, moves 0). The same
scenes and interaction run under Kickstart 1.3. Later rounds, levels and game
completion remain unverified.

The [evidence record](COPPERSTART_YEET_GAMEPLAY_2026-10-23.json) binds the exact ROM,
source, compiler, media, execution binaries, workload, captures and test results.
The [earlier prologue record](COPPERSTART_YEET_2026-10-22.md) remains evidence for
its original candidate; this record qualifies the combined source.

## Integrated source and corrections

[Source PR 17](https://github.com/ilehtoranta/CopperStart/pull/17) includes main
`67590f5` via merge commit `efafc93`, with the conflicts resolved. The qualified
source is `b06ec24669a9623d288e1e9395f13959d51743f3`. It preserves main's CLI default-stack
handling, BCPL support, public library registration, scheduler and graphics
startup work while retaining the packed public-hunk, register-preservation,
keyboard/console and reset-stack fixes.

The integrated source needed these corrections:

- Exec was named in the library list, but its private open/close vectors still
  returned the old stub result. Install those vectors without registering the
  same node twice. The named-Exec guest probe fails before and passes after.
- The graphics VBlank server rewrote COP1LC every field, replacing a direct
  display installed after LoadView(NULL). LoadView now sets COP1LC explicitly;
  VBlank publishes only LOFlist through COP2LC. The discriminating native probe
  installs a green Copper list, waits several VBlanks and writes red from the
  CPU once. The list restores green under Kickstart and the fixed ROM; the
  pre-fix replacement fails. Existing public LOFlist replacement probes pass.
- The boot console's public Screen was marked CUSTOMSCREEN. A standard
  Workbench inquiry therefore opened a second 640x256 two-plane display,
  retaining another 40 KiB bitmap. Mark the existing console WBENCHSCREEN, as
  the native DOS console is. The new guest probe verifies the same FirstScreen,
  shared bitmap and unchanged free Chip RAM across the inquiry.
- Let the boot shell's 6,152-byte CPU-only script workspace use ordinary public
  memory instead of requiring Chip RAM. It uses fitted A501 RAM under the
  existing Exec preference and falls back normally on a Chip-only machine.
  The next trace found a 41,008-byte Chip bitmap request with 50,840 bytes free
  but no chunk larger than 37,160 bytes; removing this unnecessary Chip
  requirement preserves the contiguous DMA storage required by the board.

The Workbench correction follows the first trial-selection failure. The old image had
17,448 Chip bytes free but no chunk larger than 7,584 bytes; YEET's 9,840-byte
MEMF_CHIP allocation returned zero. It then entered its runtime error trap and
jumped to an invalid address. The old normal/scalar pair matched all 1,230 files
while reproducing this failure. Replay completion and CPU parity alone were not
successful gameplay coverage.

The combined image remains 512 KiB using compact font descriptors and equivalent
MOVEQ encodings. Its SHA-256 is `4DDF1698647AA4B4E2C193E8268F5FA17A5AD6F6521AA86D5C38A657368CD997`; it contains
478,988 compiled code bytes and 30,666 native graphics bytes. The compiler gate passes with zero managed allocation
sites and zero framework features. The emulator engine and CPU package are
unchanged. No host OS shim, title-specific rule, ROM hand patch or patched media
is involved.

## Replay and validation

The [gameplay workload](../../CopperScreen.Lightweight.Tests/Workloads/copperstart-yeet-gameplay-v1.json)
uses the supplied `yeet.adf`, SHA-256
`FE52FC8BAC2D0D24620BD425023BFC839160A5B5BBD423A9891508098653D2A8`.
Both ROMs use the same frozen PAL OCS 68000 runner, 512 KiB Chip RAM, 512 KiB
A501 RAM, no Fast RAM and a read-only DF0. The native control is Kickstart 1.3.
The initial neutral prefix allows both ROMs to reach the menu before the first
fire input at field 30,000. Joystick directions move the map cursor; fire enters
the Rune Trial, then fire plus right pushes the rune and button 2 resets.

| Field | Reviewed result |
| --- | --- |
| 29,600 | Menu before the first input. |
| 38,000 | Prologue. |
| 44,000 | World map before trial selection. |
| 45,000 | Rune Trial introduction. |
| 52,000 | Round 1/3, three runes, moves 0. |
| 52,200 | Two runes, moves 1 after the push. |
| 52,600 | Three runes, moves 0 after reset. |

The boot/board-load normal and scalar CopperStart runs each complete 49,000
fields. Their initial board gestures arrived before loading finished; this pair
qualifies board loading and CPU parity. All 1,230 raw files at 246 checkpoints match: BMP, CPU/hardware JSON, PCM, Chip RAM and A501
RAM. Final cycle, CPU, hardware and output fingerprints also match. A separate
53,000-field normal run delays the board gestures until field 52,000. The native
control uses that same delayed workload; both show the rune push and reset.
These reviewed board states match complete native-control images. The JSON
records all complete-image matches, including transition/blank frames, without treating
boot timing as ROM equivalence.

The final firmware/component selection passes 282 cases with zero failures or
skips. The focused Exec/DOS selection passes 70 cases. Retained Miami Chase
frequency and Lotus 2 stack probes pass two cases each against this exact ROM.
The source Release solution builds with four existing NU1601 SDK preview
resolution warnings. Probe execution binaries are recorded separately from the
frozen canonical game runner. The old three baseline failures in the prologue
record are superseded by main's updated implementations/expectations and do not
remain failures in this selected combined-source suite.

These are bounded compatibility replays, not host-throughput acceptance or
complete game coverage. Reproduce with `scripts/probes/CopperStart/replay.py`,
the recorded runner and explicit ROM/media paths, `--slow-ram-kib 512 --frames
53000 --interval 200`, and the workload above. `--firmware native` suppresses
private bootstrap-receipt interpretation after takeover; the explicit ROM path
selects the firmware.
