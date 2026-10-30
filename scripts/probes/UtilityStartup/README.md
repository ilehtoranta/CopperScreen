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

DOS Open/Read/Write observations include their returned D0; device observations
retain the original IORequest address and its io_Error byte on return. Set
`COPPERSTART_PROBE_STOP_FAILED_DEVICE=1` to stop immediately after an observed
OpenDevice returns a nonzero D0, before the guest's error cleanup. The default
continues tracing. This diagnostic switch does not change the device result.

For a bounded first-write diagnosis, set `COPPERSTART_PROBE_WATCH_ADDRESS` and
`COPPERSTART_PROBE_WATCH_BYTE` to hexadecimal values. At START_FIELD the byte
must still have its expected value. The probe stops and retains its instruction
ring on the first observed change. This is a host diagnostic watch, not an
emulated memory validity rule; it is disabled when the variables are absent.

`prepare_sysinfo_desktop.py` creates a separately identified controlled SysInfo
disk. It requires `amitools` on the Python import path, the original collected
ADF, and native provider/`CopperGui` outputs from CopperStart's
`NativeClassicDesktopUpgradeTests`. It preserves the utility program and adds
only two guest HUNK files and the prerequisite invocation in Startup-Sequence.
Its JSON receipt records input/output hashes and every changed disk path.
Neither creating the disk nor passing the provider test establishes that
SysInfo reaches its main screen; inspect the native replay separately.

`prepare_ppmore_desktop.py` builds a separately identified PPMore reader disk
from the collected SysInfo boot disk and the same guest GUI module. It keeps
PPMore's executable unchanged, installs the Kickstart 1.3 ReqTools 38.385 build
from `ReqTools/libs13/reqtools.library` in the preserved
[Fish 794 archive](https://ftp.fau.de/aminet/misc/fish/fish-0794.lha), and pins
PPShow's bundled PowerPacker library. PPShow's ReqTools library requires 2.0+
and is not used for this test. Original archives and distribution documentation
remain local; generated disks are local test installations.

The reader opens an original, fixed 82-line ASCII fixture. The script writes a
sidecar with every installed file's bytes/hash, archive hashes, provider inputs
and startup sequence. It requires `amitools` and `lhafile`. Pass `--base`,
`--ppmore`, `--ppshow`, `--reqtools`, `--provider-root` and a new `--output` path.
Disk creation alone does not establish a successful PPMore replay.

```powershell
python scripts/probes/UtilityStartup/prepare_sysinfo_desktop.py `
  --original media/utilities/originals/sysinfo_4.4.adf `
  --provider-root path/to/native-provider `
  --installer path/to/native-provider/CopperGui `
  --output artifacts/sysinfo-with-native-gui.adf
```

The [ProTracker investigation](../../../docs/PROTRACKER_STARTUP_2026-10-28.md)
records paired API evidence and native RAM equivalence against the normal runner.
