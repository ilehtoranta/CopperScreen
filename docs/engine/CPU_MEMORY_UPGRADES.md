# Model, CPU and Fast RAM configuration

Updated 2026-10-08. The welcome screen and Settings > Setup share the Amiga
model selector. Amiga 500 is the default; Amiga 500 with ECS and experimental
Amiga 1200 are also offered. Selecting a model restores its stock CPU and memory
layout while retaining selected media, storage mounts and input settings.

## Desktop choices

| Motherboard | CPU choices | Fast RAM limit |
| --- | --- | --- |
| A500 OCS or ECS | 68000, 68010, 68EC020 | 8 MiB |
| A500 OCS or ECS | 68020, 68030, 68040, 68040 JIT | 1 GiB |
| A1200 Alice/Lisa | 68EC020 (default) | 8 MiB |
| A1200 Alice/Lisa | 68020, 68030, 68040, 68040 JIT | 1 GiB |

Optional features include slow RAM when compatible with the Chip RAM layout,
Fast RAM, RTC and floppy sounds. Fast RAM sizes are None, 512 KiB, 1, 2, 4,
8, 16, 32, 64, 128, 256, 512 and 1024 MiB, filtered by CPU address width.
Downgrading to a 24-bit CPU clamps an installed larger bank to 8 MiB and updates
the Autoconfig address hint. CPU and RAM changes require a restart. Minimal disk
boot retains its stock 68000 layout and has no Fast RAM.

Banks above 8 MiB require Kickstart 3.x. The supported A500 ROM is native
Kickstart 3.1 (40.63); the A1200 still requires native Kickstart 3.0 (39.106),
PAL Alice/Lisa, 2 MiB Chip RAM and no trapdoor slow RAM. The 060 remains an
engine/runner option, unavailable in the desktop pending compatible OS/FPU
validation. See [CPU support](CPU_SUPPORT.md).

## Engine behavior

The existing 512 KiB–8 MiB Zorro II path and stock CPU factories are preserved.
Larger banks expose a virtual Zorro III memory board with the extended size
table, MEMLIST flag and guest-controlled Autoconfig. The table's standard maximum
is 1 GiB. The board is reachable through both `$FF000000` and the `$E80000`
configuration alias; the latter uses Zorro II nibble encoding. Base assignment
accepts either byte-write order or the Zorro III word write. Large configured
banks occupy 32-bit space from `$10000000`, below `$80000000`, on a 16 MiB
boundary. The desktop hint uses size alignment; the guest determines the address.

Full 68020/030/040/060 addresses reach this bank without 24-bit truncation.
Unmapped high addresses retain floating all-one reads and ignored writes. The
68EC020 continues to mask every transfer to 24 bits. Fast RAM stays CPU-only;
Agnus DMA cannot address it. Reset removes its mapping and retains storage.
JIT code snapshots, generations, invalidation and host gateways cover the high
bank. CopperHDF and host filesystem pointers retain full CPU address width.

The A1200 stock CPU retains `CreateA1200Ec020`. Upgrades use the existing shared
accelerator CPU factory with the fitted Alice/Lisa bus. Aligned Chip/ROM
longwords retain the AGA transfer path; Fast RAM uses the bounded split bus
policy. CPU clocks, caches and posted transfers are approximate, as described in
[architecture](ARCHITECTURE.md). These choices do not certify a physical
accelerator card or its local-memory speed.

The Zorro III encoding follows Commodore's
[configuration-register definitions](https://d0.se/include/libraries/configregs.h)
and [BIGRAM source, appendix D](https://www.devili.iki.fi/mirrors/haynie/zorroiii/bigram/docs/bigram.pdf).

## Verification

`CpuMemoryConfigurationTests` checks every desktop model/CPU/RAM choice, profile
round trips, CPU downgrades and rejected CPU/ROM/memory combinations.
`LightweightUpgradeTests` checks both motherboards, high and unaligned accesses,
bank ends, reset, Autoconfig aliases, JIT execution/invalidation, high RAM disk
requests and guest execution of a registered high RAM host gateway.
`LightweightHostPacketTests` also relocates the shared-folder bootstrap and
library strings into high RAM and reads a file into a high RAM guest buffer.

`LightweightNativeUpgradeTests` executes an original boot disk through unmodified
Kickstart. Guest Exec calls measure total Fast RAM and allocate a buffer; the
guest writes through that returned pointer. Fourteen supplied-ROM cases cover
A500 020/030/040 and 040 JIT, stock A1200 EC020 with 8 MiB, A1200 020/030/040 and
040 JIT, all extended bank sizes, and the 1 GiB maximum on both motherboards.
All fourteen passed on 2026-10-08. The final guest enters supervisor state and
masks interrupts before the steady loop. Native Kickstart 3.x leaves transparent
translation enabled, which deliberately uses interpreter fallback. For the
compiled proof, the takeover guest saves those controls, clears TC/ITT/DTT and
enables CACR. Its counter advances and compiled instructions increase after the
proof marker, with a bounded wait for asynchronous compilation. CPU/hardware clock equality is
checked there after outstanding bus work drains. During native boot, posted
writes can complete after CPU instruction retirement; the test checks both
clocks advance monotonically instead of assuming equality at every field.

Supply `COPPERSCREEN_UPGRADE_A500_ROM` and `COPPERSCREEN_UPGRADE_A1200_ROM` to run
the native cases. The A1200 input can be a ROM or ZIP with one ROM entry. Missing
inputs produce skipped, unavailable coverage. No ROM or copyrighted media is
included. Both are 512 KiB ROMs; the tests enforce SHA-256 identities:

| ROM | SHA-256 |
| --- | --- |
| A500 40.63 | `8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee` |
| A1200 39.106 | `293beea59d7de4caab3e07223d66e844ad5268eb06a04f405b523a0a1543d64c` |

This is bounded boot, allocation and bus coverage; upgraded native
gameplay, high RAM Workbench persistence, accelerator timing and host throughput
acceptance remain unverified. Performance protocols are unchanged.
