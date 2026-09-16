> Archived working notes from CopperMod on 2026-09-16, not a claim of support
> in the current CopperScreen build. The original bytes are in source-snapshot.zip.

# CopperStart feature matrix

This is the working scope matrix for CopperStart: the host-side replacement
layer that starts from a legal, real Kickstart ROM and selectively replaces
implemented library/device entry points.

## Status vocabulary

| Status | Meaning |
| --- | --- |
| **Implemented** | Host implementation is installed in the appropriate mode and has focused tests. |
| **Partial** | The useful/common path works, but documented behavior or lifecycle coverage is incomplete. |
| **Native** | Deliberately left to the original ROM/device. No host replacement is installed. |
| **Planned** | Intended CopperStart work with no implementation yet. |

## Foundation and execution model

| Area | Status | Current coverage | Important missing work | Priority |
| --- | --- | --- | --- | --- |
| ROM-first boot | **Implemented** | Real KS 3.1 boots normally; CopperStart is an explicit compatibility profile. Missing/invalid ROM configuration is reported rather than bundled. | Broader ROM/version validation and more real-ROM boot fixtures. | P1 |
| Six-byte host gateway | **Implemented** | `FF 00` + big-endian 32-bit opaque token; unknown token follows Line-F exception handling; invalidation spans all six bytes. | Continue backend/regression coverage as CPU work changes. | P1 |
| ROM overlays | **Implemented** | Overlays retain original six bytes and remove cleanly; only selected concrete KS 3.1 LVOs are patched. | Per-library diagnostics/reporting of installed overlay LVOs. | P2 |
| Direct device gateways | **Implemented** | ROM-created device bases are found from `DeviceList`; standard vectors are intercepted without modifying ROM bytes. | Common lifecycle/discovery helper to reduce repeated implementations. | P2 |
| Gateway control result | **Implemented** | Completed, blocked, and deferred-reschedule dispositions are understood by interpreter/timing/JIT paths. | More parity coverage for every enabled 680x0/JIT configuration. | P1 |
| Task dispatch model | **Partial** | Host calls are atomic. Blocking Exec calls return to the outer instruction boundary; no recursive CPU dispatch. | Audit every host implementation for accidental recursive dispatch and complete task lifecycle behavior. | P0 |
| Guest callbacks | **Implemented** | Guest code is entered through continuations rather than direct host invocation. Used by RawDoFmt, input handlers, console graphics/keymap paths, and device hooks. | Shared continuation ownership/diagnostics for nested and failed guest calls. | P1 |

## Exec

| Area / LVO family | Status | Current coverage | Important missing work | Priority |
| --- | --- | --- | --- | --- |
| ROM Exec takeover | **Implemented** | Waits for a valid ROM-created `AbsExecBase`; uses ROM lists, tasks, and memory headers; reset removes overlays. | Real-ROM negative/failure fixtures for all malformed boot states. | P1 |
| Memory | **Implemented** | `AllocMem`, `AllocAbs`, `FreeMem`, `AvailMem`, `AllocMemAndStore`; operates on active Exec memory structures. | Full flags/fragmentation/error semantic audit against KS 3.1. | P1 |
| Pools | **Implemented** | Pool create/allocate/free/delete services exist. | Full flag/alignment and failure-path conformance. | P2 |
| Lists and names | **Implemented** | `NewList`, insert/add/remove/head/tail/enqueue, `FindName`; `FindTask` and `FindResident` use guest list/resident traversal. | Exhaustive malformed-list and priority ordering conformance. | P2 |
| Signals and Wait | **Implemented** | `AllocSignal`, `FreeSignal`, `SetSignal`, `Signal`, `Wait`; blocked ROM task state and wake/recheck path use outer-boundary dispatch. | Real multi-task torture tests and exception/signal edge cases. | P0 |
| Ports/messages | **Implemented** | Add/remove/find ports, put/get/reply message, `WaitPort`; reply-port signals feed normal Wait behavior. | Message ownership/error edge cases and high-contention tests. | P1 |
| MorphOS Exec registry extensions | **Partial** | 68k bridge slots match MorphOS: `FindExecNode`, `AddExecNodeA`, `AddResident`, `AvailPool`, and `PutMsgHead`. `AddExecNodeA` provides unique priority-ordered port/semaphore insertion, including documented allocation of those two node classes. | Remaining MorphOS extension APIs and lifecycle helpers such as DeleteMsgPort/FreeVec ownership audits. | P3 |
| Semaphores | **Implemented** | Init, exclusive/shared obtain/release, attempt variants; contention uses the Wait transition. | Priority inheritance and full KS queue-order behavior, if required by compatibility tests. | P1 |
| Task lifecycle | **Partial** | Core task structures, task lists, scheduling transitions, Forbid/Permit, Disable/Enable, and trap fields are handled. | Complete CreateTask/CreateProc/DeleteTask semantics, stack/CLI/process setup, and task-expunge/exit behavior. | P0 |
| Exceptions/traps | **Partial** | Task trap storage, allocation/free, dispatcher/recovery routing. | Complete exception vector/system alert behavior and broader guest trap callback coverage. | P1 |
| Device I/O lifecycle | **Partial** | `DoIO`, `SendIO`, `CheckIO`, `WaitIO`, `AbortIO` state paths exist; concrete devices own their BeginIO behavior. | Async/error lifecycle stress coverage across all devices and native fallback audit. | P0 |
| Libraries/devices/resources | **Partial** | Find/open/close/add/remove library, device, and resource operations; ROM lists remain authoritative. | Expunge, delayed expunge, version checks, and full library/device lifecycle semantics. | P1 |
| Residents/AUTOINIT | **Partial** | `InitStruct`, `MakeFunctions`, `MakeLibrary`; AUTOINIT library/device route started. | Complete `InitResident` overlay only after all supported resident types and cleanup paths are proven. Native ROM remains authoritative today where omitted. | P1 |
| Formatting | **Partial** | `RawDoFmt`, 16-bit default integer behavior, null putch shim, host/guest putch continuations. | Complete format-specifier and locale/edge-case conformance. | P2 |
| Cache/MMU/system management | **Native** | Original KS 3.1 vectors run unchanged. | Cache controls, supervisor/system calls, vectors, and 68020+ specific Exec facilities. | P2 |

## Host-replaced devices

| Device | Status | Current coverage | Important missing work | Priority |
| --- | --- | --- | --- | --- |
| `trackdisk.device` | **Implemented** | Logical read/write, format, raw read/write, TD64 read/write/seek/format, geometry, change interrupts, eject/remove policy, image dirty/persistence path. | Exact raw MFM/controller timing, full media-change corner cases, NSCMD deliberately excluded. | P1 |
| `timer.device` | **Implemented** | CIA-free deterministic system time; MicroHz, VBlank, EClock, absolute waits; abort/reply behavior and deadline scheduling. PAL/NTSC VBlank uses fixed 50/60 Hz cadence. | Full device-specific error/rounding conformance and long-run overflow tests. | P1 |
| `keyboard.device` | **Implemented** | Host raw keys, duplicate suppression, complete `InputEvent` reads, matrix subsets, CMD_CLEAR, reset-handler priority/timeout/done behavior, qualifier snapshots, repeat, and native-input bridge. CIA serial remains fallback before activation. | Real-ROM reset-sequence smoke coverage and rare physical-keyboard-model differences. | P2 |
| `input.device` | **Partial** | Host input pipeline, handler chaining, raw input events, key repeat, and native fallback forwarding are present. | Remaining documented command surface, mouse/tablet semantics, and ordering stress tests. | P1 |
| `gameport.device` | **Partial** | Host-mapped game-port wrapper and core device lifecycle. | Full controller/CD32/light-pen and edge-case event semantics. | P2 |
| `console.device` | **Partial** | Standard vectors; KS 3.1 `CONU_LIBRARY`, `CONU_STANDARD`, `CONU_CHARMAP`, and `CONU_SNIPMAP` units; read/write/clear/update/reset/flush/stop/start; terminal model; Bell through native Intuition `DisplayBeep`; common ANSI/CSI plus Amiga-private geometry/scroll/cursor controls; V39 default-SGR and concealed-text handling; guest graphics continuations; bold/italic/underline/cursor rendering; active-window input routing from the ROM IntuitionBase; raw event reports; native guest keymap conversion; special-key CSI reports; and `CONU_SNIPMAP` drag-selection/copy through guest `clipboard.device` I/O. | Remaining KS 3.1 escape/input semantics, window/selection edge cases, and real CLI/Workbench smoke coverage. | P0 |
| `clipboard.device` | **Implemented** | CopperStart-owned guest device node with standard vectors and full Exec command prefix (`RESET`, `READ`, `WRITE`, `UPDATE`, `CLEAR`, `STOP`, `START`, `FLUSH`), independent raw-IFF units, generation IDs, `CBD_POST`/satisfy messages, deferred `struct Hook` callbacks, FTXT CHRS/UTF8 and bounded indexed-ILBM host bridges, and console guest-I/O copy/paste. | Broader real-ROM application smoke coverage and optional support for additional Amiga image modes; neither changes the documented clipboard-device protocol. | P2 |
| `serial.device` | **Native** | Original ROM/device behavior remains in control. | Host serial bridge, deterministic buffering, modem/status semantics. | P3 |
| `parallel.device` | **Native** | Original ROM/device behavior remains in control. | Host bridge and hardware-mode behavior. | P3 |
| `ramdrive.device` / other DEVS devices | **Native** | Not replaced. | Add only when a concrete compatibility goal needs them. | P3 |

## Libraries and system services

| Library / area | Status | Current coverage | Important missing work | Priority |
| --- | --- | --- | --- | --- |
| `dos.library` | **Partial** | CopperStart DOS compatibility: handles, locks, paths/files, console handles, errors, diagnostics, and selected LVOs. | Real ROM DOS replacement, packet/filesystem semantics, processes/CLI, assign/notify, shell behavior. | P1 |
| `graphics.library` | **Partial** | Isolated CopperStart graphics calls and synthetic-display support. Real guest graphics is intentionally used by console. | Full graphics primitives, layers, blits, fonts, View/ViewPort lifecycle, and ROM overlay curation. | P1 |
| `intuition.library` | **Partial** | CopperStart synthetic UI/session behavior and selected gateways. | Full windows/screens/gadgets/menu/requester behavior against ROM-created state. | P2 |
| `icon.library` | **Partial** | Existing CopperStart icon gateway behavior. | Full icon parsing, layout, tooltype, and Workbench integration. | P3 |
| `workbench.library` | **Partial** | Existing compatibility/launch behavior. | Full Workbench object lifecycle and desktop behavior. | P3 |
| `expansion.library` | **Partial** | Existing board/autoconfig gateway behavior. | Full expansion config, boot nodes, and device driver lifecycle. | P2 |
| `utility.library` | **Implemented** | Direct host gateways cover TagItems, `CallHookPkt` guest continuations, Amiga epoch dates, 32/64-bit arithmetic, case-insensitive strings, Pack/Unpack, NamedObjects, and unique IDs. | Locale-aware collation/case conversion and exhaustive malformed guest-structure conformance. | P2 |
| `keymap.library` | **Native dependency** | Console input and `RawKeyConvert` invoke its live guest `MapRawKey` entry, so runtime guest keymaps are authoritative. | Host replacement is unnecessary until a specific compatibility/timing requirement appears. | P3 |
| `ciaa.resource` / `ciab.resource` | **Native dependency** | `timer.device` intentionally does not allocate CIA timers or interrupt servers. | Replace only with a clear resource/compatibility need. | P3 |

## Delivery order

1. **P0 — make the current OS path dependable:** complete task/process lifecycle, harden Wait/WaitIO/async device behavior, and finish the console ↔ clipboard-device route.
2. **P0 — validate `clipboard.device`:** real-ROM application coverage for its guest I/O, FTXT/CHRS, and indexed-ILBM bridges; Windows clipboard integration belongs exclusively there.
3. **P1 — finish core Exec semantics:** library/device resident lifecycle, signals/messages/semaphores stress tests, and memory conformance.
4. **P1 — stabilize existing practical devices:** trackdisk media behavior, timer rounding/overflow, keyboard/input ordering, and console CLI smoke tests.
5. **P2/P3 — grow only with a concrete compatibility need:** graphics/Intuition depth, expansion, serial/parallel, and cache/MMU/system-management functions.

## Guardrails

- KS 3.1 ROM remains the authority whenever an LVO or device behavior is not
  explicitly implemented above.
- ROM overlays are limited to concrete, tested LVOs. Unsupported vectors must
  retain original ROM bytes and run natively.
- Host code is atomic. Blocking behavior is represented by an Exec state
  transition and dispatch at the outer instruction boundary, never recursive
  emulator execution.
- Guest callbacks always run through the 68k emulator.
- Windows clipboard access belongs to `clipboard.device`; `console.device`
  communicates with it via normal guest device I/O.
