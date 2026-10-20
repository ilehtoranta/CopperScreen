# CopperStart keyboard text and host input release - 2026-10-18

The compact native desktop profile adds keyboard text through the current
keymap.library default and completes CopperScreen's presenter focus/capture-loss
release path. See the [producer contract](../../../CopperStart/Docs/NativeIntuitionText.md)
and [machine-readable evidence](COPPERSTART_INTUITION_TEXT_2026-10-18.json).

The final focused input/text profile passes **6/6**, with zero skips: four native ROM/media journeys and two freestanding checks, retaining **44 checked PNGs** across eight screen lifetimes plus two diagnostic text-console PNGs. Separately, six host checks and both production builds pass. The earlier combined profile passes **17/17** on the provider before the keyboard-transport overflow correction; its broader startup/refresh/shared-port coverage is versioned separately and is not attributed to the final HUNK.

## Selected contract

A native desktop bootstraps through `NativeDesktopProviders.TextInputEntry`
(export `copperstart.intuition.text-input`) and retains its complete provider
HUNK until reset. Earlier raw-input entrypoints remain available. Text startup
installs only keymap.library, pins v36, and unwinds new owners on failed input
startup. Borrowed libraries and selected defaults survive failures.

The worker calls the public MapRawKey vector. One-byte translations become
VANILLAKEY; mixed subscriptions retain other downs as RAWKEY and consume ups.
Raw-only consumers retain downs/ups and a message-owned IAddress history cell.
Custom KeyMap tables support Latin-1, qualifier/caps/repeat bits, strings and
dead/double-dead descriptors. Text retries commit history/layout only after a
successful post. Repeats preserve the original prefix; overflow/focus changes
reset composition. The documented $FA keyboard transport-overflow notification
clears uncertain held/modifier state, preserves Caps Lock and cancels repeat;
raw releases are reconciled by the task. A twelve-key host release burst fails
against the previous provider and passes after this correction. Extreme ring overflow can lose text; recovery restores held
state without inventing replacement characters. Built-in US is bounded; visible
layout selection/loading and complete national layout coverage remain work.

The input state stays 2048 bytes with sixteen 64-byte messages. Published keymap
storage adds 1808 retained bytes until reset. The provider is disk-loaded; the
ROM remains 524288 bytes with SHA-256
`60565368B67150BC17F42D1D9135A7942AF2260D31B9306FC0B1105F026FAEDA`.
The machine remains MC68000 PAL OCS, 524288 Chip + 524288 slow, no Fast RAM.
The selected provider HUNK is 256972 bytes (248632 code), SHA-256
`9B3352B2D8EE2A2A144FAA089C954B926A4E734CBA6EF2B4151AB8E7B4E04ABD`.
Text entry offset is 247346; input entry is 247320; entry is zero.

## Host boundary

CopperScreen queues one emulator-owner command to release tracked keys, mouse
buttons and both joystick ports, including while paused. App deactivation,
presenter focus loss, pointer capture loss, explicit release and failed
capture/recentering use that path. Pointer ownership is cleared before releasing
capture, making synchronous capture-loss reentry safe. Guest key downs require
presenter keyboard focus. Physical host positions stay stable across logical
host layout changes; characters are selected by the guest KeyMap.

Host tests cover the release command, paused state, physical mapping and
presenter scheduling. The native replay supplies the resulting physical
key/button releases through CIA/chipset input. It does not exercise real Windows
focus gestures or pointer confinement; those remain platform integration checks.

## Validation and provenance

Final gate results are in the linked evidence JSON. The selected native profile
reuses the earlier qualified Layers/Utility/ROM/compiler inputs, overlays the
text/input changes, and freezes 139 source/build/host files. Concurrent timer, menus,
BOOPSI and palette changes in the working checkout are outside this native
profile. A full production build is distinct from native profile qualification.

The text journey checks public mapping and physical delivery at both widths,
fresh-owner allocation failures (five), borrowed-owner allocation failures
(four), refused Schedule replacement, idempotence, control/caps/repeat, layout
change, raw strings, dead keys, full-pool retry without duplicates, physical
release, native Wait/replies, and final heap/vector/pin accounting. Its two
console PNGs are diagnostics. Existing startup/refresh/message/input/joined
journeys retain their pixel checks. Correctness replay duration is not throughput
or UI-latency acceptance. This work does not update the shipping firmware pin.
The checkout has a separate hard-disk firmware qualification; this native text
evidence does not qualify that newer ROM/desktop pair.

## Next desktop work

Build the original native Desktop Process with one fixed screen, Home/Files/
Editor views, keyboard navigation and one drain/reply/Wait loop. Add visible
layout selection and actual host capture gestures to integration coverage. Then
join Shell/Demo launch/return, safe file editing, 100-cycle stress and complete
module/stack/queue memory peaks before release packaging.
