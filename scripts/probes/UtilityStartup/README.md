# Utility startup diagnostic

Standalone, input-free PAL A500 probe for observing guest library startup.
Uses the repository Lightweight engine and its pinned Copper68k dependency;
it is not part of the production solution.

```powershell
dotnet run --project scripts/probes/UtilityStartup -c Release -- ROM ADF OUTPUT START_FIELD END_FIELD
```

All five arguments are required. Output must be a new directory. The fixed
configuration is MC68000 interpreter, OCS 8371/8362, 512 KiB Chip + 512 KiB slow
RAM, no Fast RAM/host directories, and one protected DF0. Before START_FIELD,
execution uses ordinary `ExecuteFrame`. Afterwards the probe invokes existing
private scalar execution helpers by reflection, retaining hardware advancement
and the scalar loop's interrupt sampling. It is coupled to those internal APIs.

A bounded 512-entry host trace records recent instructions and interrupt events.
The probe observes `JSR d16(A6)` Exec OpenLibrary/OldOpenLibrary returns and
Intuition OpenScreen returns, then stops before negative-vector calls with a
null A6. Low stacks (below `$400`), stacks outside the configured RAM ranges,
and low/unpeekable PCs also stop diagnosis. These are heuristic stopping
conditions, not new emulated hardware faults or general Amiga validity rules.

Outputs are `trace.json` (input hashes, configuration, calls and recent CPU
state), `chipram.bin` and `slowram.bin`. Do not commit media or raw captures.
Exec OpenDevice, DoIO and Wait observations include calls still pending at the
stop, allowing a later device/startup wait to be separated from a library fault.
Execution does not establish application correctness merely by reaching END_FIELD.

`prepare_sysinfo_desktop.py` creates a separately identified controlled SysInfo
disk. It requires `amitools` on the Python import path, the original collected
ADF, and native provider/`CopperGui` outputs from CopperStart's
`NativeClassicDesktopUpgradeTests`. It preserves the utility program and adds
only two guest HUNK files and the prerequisite invocation in Startup-Sequence.
Its JSON receipt records input/output hashes and every changed disk path.
Neither creating the disk nor passing the provider test establishes that
SysInfo reaches its main screen; inspect the native replay separately.

```powershell
python scripts/probes/UtilityStartup/prepare_sysinfo_desktop.py `
  --original media/utilities/originals/sysinfo_4.4.adf `
  --provider-root path/to/native-provider `
  --installer path/to/native-provider/CopperGui `
  --output artifacts/sysinfo-with-native-gui.adf
```

The [ProTracker investigation](../../../docs/PROTRACKER_STARTUP_2026-10-28.md)
records paired API evidence and native RAM equivalence against the normal runner.
