# Chip RAM instruction-cache coherence

This standalone diagnostic generates an independently authored 512 KiB ROM,
without Kickstart or game bytes. It copies code to Chip RAM, enables the 020
instruction cache, calls a function returning `1`, overwrites that function to
return `7`, calls it again, flushes the cache with CACR, then calls once more.

The three results at `$C09000`, `$C09004`, `$C09008` must be `1, 1, 7`.
The CPU stops with IPL 7 after writing the results. This checks cache visibility,
not exact instruction timing or accelerator performance. Expected behavior comes
from Motorola's MC68020 cache rules and Commodore's `CacheClearE` documentation
that enables instruction caching for Chip RAM.

```powershell
dotnet run --project scripts/probes/ChipInstructionCache -c Release -- artifacts/new-chip-cache 68020
dotnet run --project scripts/probes/ChipInstructionCache -c Release -- artifacts/new-ec020-chip-cache 68ec020
```

The output directory must be new. Both normal and scalar engine modes run, with
their values, final PC/cycle and `cacheCoherenceMatches` result saved as JSON.
An incorrect result is recorded as `false` to retain before/after evidence;
failure to reach STOP or differing mode results throws an error. The engine's
pinned development package must be available for restore.

For an independent comparison, boot `chip-cache.rom` in a PAL OCS WinUAE profile
with a 68020, 512 KiB Chip RAM and 512 KiB slow RAM, CPU compatibility and cycle
exactness enabled, and JIT disabled. No disks or input events are needed. Read
the three longwords after STOP. See the
[correction record](../../../docs/engine/LOTUS_III_020_CACHE_2026-09-26.md).
