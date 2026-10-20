# CopperStart native IDCMP refresh — 2026-10-18

The compact Intuition disk entry now delivers native IDCMP_REFRESHWINDOW
messages after successful system frame repair. It extends the
[refresh/repaint qualification](COPPERSTART_INTUITION_REFRESH_2026-10-18.md).
See the [producer implementation](../../../CopperStart/Docs/NativeIntuitionMessages.md)
and [machine-readable evidence](COPPERSTART_INTUITION_IDCMP_2026-10-18.json).

The retained gate passes **8/8**, zero skips: one exact-input cache control and
seven real ROM/media journeys. The same payload also passed actual freestanding
compilation in the retained preflight; two assembler-fixture failures in that
broader attempt are preserved. An insufficient-field-cap/aborted attempt is
retained separately. Only the final completed journeys establish this result.
Six startup, nine refresh and six message lifetimes check 181 captured frames
against bitmap planes and displayed pixels. Complete screen teardown restores
the pre-screen heap baseline, and final graphics/Layers/Utility pins are zero.
The latest `dotnet build CopperStart.slnx -c Release` passes with zero warnings
or errors. The earlier 344-check Layers suite was not rerun here.

Each subscribed window reserves one reusable message. Repeated exposures
coalesce while that message is queued or delivered. ReplyMsg wakes a native
reply task, which collects the reply and sends another notification only if
pending work remains. Successful EndRefresh, including a partial pass, cancels
that pending notification; an already posted message still needs its reply.
Partial refresh continuation remains the application's responsibility.

The first subscription also creates a native Exec reply task/32 KiB stack and
reply port. It blocks in Exec.Wait when idle. Application UserPorts use their
actual task/signal, so PutMsg wakes applications blocked in Wait or WaitPort.
No host-injected event or VBlank polling substitutes for that route.

Reservation and provider failures roll back the tentative record, reply port,
task/stack and newly created UserPort/signal before changing subscription flags.
A caller-supplied UserPort remains owned by its caller. Sending the reserved
notification needs no message allocation. Failed frame repair suppresses a new
notification and leaves pending damage for later topology/ModifyIDCMP retry;
there is no timer-driven low-memory retry loop. Failed application refresh
preserves damage and requests another notification after reply. NoCareRefresh
retires damage after system frame repair without requesting application work.

Closing a window or disabling refresh removes its queued record. Delivered
records retain their reply storage and keep Intuition from expunging until
replied; collection never dereferences a freed window. The native lifecycle
retires the reply task/stack and port after the last record. A deferred expunge
can then be retried. Shared-port callers still drain and detach their ports
before closing, as required by the public API.

The replay uses a separate native producer task to uncover the window after
observing the application's actual Waiting state. The application wakes,
receives/replies to its message and repaints through BeginRefresh/EndRefresh.
It checks repeated lifetimes, coalescing, partial refresh, real allocation
failures, provider refusals, supplied-port retention, failed frame/refresh
projection, NoCareRefresh and queued/delivered teardown. The only host write
acknowledges a captured frame in the fixture's reserved low page.

The ROM is unchanged at 524,288 bytes, SHA-256
`DE0C6C8BE7395788081EF26E9B50FCC01DB912FD2BFA8A7924A7230F4979133F`:
842 contiguous tail bytes and a separate 27,854-byte gap remain. The disk HUNK
is 238,360 bytes (230,710 code bytes), SHA-256
`4E3284903A2D2EF9E0B763FBDEA13569228A6B2AE48F3F92D02AD6BE88C175CD`.
The seven payload inputs, eleven ROM producer inputs and 56-file source snapshot
are bound by hashes. The source manifest is
`1323971DB29B04F70851F13B678DDFADAEF9D1B899BE701E1D3F9F8CD55D2BAB`.
Concurrent preferences/Workbench sources in the snapshot do not admit their
public behavior through this refresh result.

The machine uses 524,288 Chip bytes, 524,288 slow bytes and zero Fast RAM.
Minimum free bytes across the checked message capture points:

| Width | Chip free | Total free | Largest Chip block | Largest slow block |
| --- | ---: | ---: | ---: | ---: |
| 320 | 208,464 | 423,576 | 175,696 | 199,576 |
| 640 | 187,984 | 403,096 | 155,216 | 199,576 |

These points include the payload, two 32 KiB probe stacks, native reply
task/stack and selected live GUI topology. They do not establish transient
loader peaks or the full desktop/input budget.

Next, bind physical keyboard/mouse, pointer, activation/focus, close-gadget
events, qualifiers, key-up/repeat and input queue pressure to this exact
composition. The separate Workbench input entry retains a polled transport;
it is not installed by IntuitionEntry. Unify event/reply ownership and qualify
an input worker that sleeps on real events. Interrupt-to-task wakeup, system
clock progression and the console's single-owner
keyboard handoff still need qualification; this replay signals in task context.
Shared-port multi-window delivery,
contended topology/refresh wait/resume and disjoint damage remain separate
coverage. Budget complete loader/transient peaks, all stacks and input queues.

This remains producer correctness evidence on the 512 KiB ROM / 1 MiB A500.
The shipping app firmware pin is unchanged; this does not admit a new app pair,
publish a package or establish a throughput result.
