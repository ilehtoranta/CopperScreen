# Fast RAM native diagnostics

These host-side tools inspect runner captures and prepare a disposable native
OFS workload. They contain no ROM or operating-system bytes.

The runner accepts `--fast-ram-kib 0|512|1024|2048|4096|8192`. With nonzero RAM,
boot probes add `.fastram` and `.memory.json` per checkpoint. Existing captures
remain unchanged when Fast RAM is disabled. Use `--boot-probe-extended` to include
slow RAM for guest memory-list inspection.

```powershell
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom "path/to/Kickstart_13.rom" --fast-ram-kib 2048 --frames 1200 --boot-probe artifacts/fast-boot --boot-probe-interval 1200 --boot-probe-extended
python scripts/probes/FastRam/inspect_snapshot.py artifacts/fast-boot/frame-001200
```

The inspector walks Exec's native memory headers and free chunks, checks complete
bank registration with PUBLIC|FAST (without CHIP), and verifies free-byte totals.
It reads snapshots only; it never edits the guest.

For allocation and storage, supply a bootable Workbench 1.3 **OFS partition** image
without an RDB prefix. The output must be new:

```powershell
python scripts/probes/FastRam/prepare_native.py supplied-partition.hdf artifacts/fast-proof.hdf
dotnet run --project CopperMod.Amiga.Lightweight.Runner -c Release -- --rom "path/to/Kickstart_13.rom" --fast-ram-kib 2048 --hdf artifacts/fast-proof.hdf --frames 2400 --boot-probe artifacts/fast-proof-boot --boot-probe-interval 1200 --boot-probe-extended
python scripts/probes/FastRam/inspect_snapshot.py artifacts/fast-proof-boot/frame-002400
python scripts/verify-lightweight-ofs-proof.py artifacts/fast-proof.hdf --name fast-proof.txt --expected "Copper Fast RAM native OFS persistence`n"
```

The disposable startup asks AmigaDOS for 1,200 additional filesystem buffers,
runs `Avail`, and writes/reads a proof file. Confirm Fast RAM free bytes decreased
and inspect the final image. This workload is verified on 68000, 68020 and
68EC020. Add `--cpu 68020` or `--cpu 68ec020` to the runner command for those
experimental profiles. Local CPU `.54` resolves the indexed-JSR boot blocker
and the subsequent instruction gaps. See the [020/EC020 follow-up](../../../docs/engine/COPPERHDF_020_2026-09-27.md)
and [initial record and limits](../../../docs/engine/FAST_RAM_2026-09-27.md).
