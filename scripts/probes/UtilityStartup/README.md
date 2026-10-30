# Utility startup diagnostic

Standalone, input-free PAL A500 probe for observing guest library startup.
Uses the repository Lightweight engine and its pinned Copper68k dependency;
it is not part of the production solution.

```powershell
dotnet run --project scripts/probes/UtilityStartup -c Release -- ROM ADF OUTPUT START_FIELD END_FIELD
```

All five arguments are required. Output must be a new directory. An optional
sixth argument selects `a500-ocs-1m-v1` (default) or `a500-ocs-2m-v1`.
Both use MC68000 interpreter, OCS 8371/8362, 512 KiB Chip + 512 KiB slow RAM,
no host directories, and one protected DF0. The 2 MiB profile adds 1 MiB of
AutoConfig Fast RAM. Its trace records the profile, Fast RAM base and size,
and includes `fastram.bin`. Memory/stack peeks cover the selected Fast bank.
The profiles are separate diagnostic inputs. Before START_FIELD,
execution uses ordinary `ExecuteFrame`. Afterwards the probe invokes existing
private scalar execution helpers by reflection, retaining hardware advancement
and the scalar loop's interrupt sampling. It is coupled to those internal APIs.

A bounded 512-entry host trace records recent instructions and interrupt events.
The probe observes `JSR d16(A6)` Exec OpenLibrary/OldOpenLibrary returns and
Intuition OpenScreen/OpenWindow returns, then stops before negative-vector calls with a
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

At START_FIELD the probe also discovers DOS and Intuition bases from Exec's
public library list, so a late trace can observe libraries opened earlier.
OpenWindow observations retain the caller's 48-byte NewWindow before the call.
AllocMem/AllocVec observations record size, flags and result. DOS Write retains
up to 128 bytes at entry as `writePrefix`, before guest cleanup can free them.
These snapshots use diagnostic peeks and do not advance emulated time.
CloseWindow calls and nonempty GetMsg returns are also observed; the latter
retain 52 generic message bytes as `messagePrefix`, without assuming every
message is an IntuiMessage. Set `COPPERSTART_PROBE_STOP_ILLEGAL=1` to retain
the first observed `$4AFC` instruction before execution. This stopping rule
does not classify a deliberate guest trap as an emulator fault.

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

Optional `--shell-root` points to the native Shell test's `shell/` output,
containing `CopperShell` and `shell-manifest.json`. This creates a minimal OFS
disk, retains the base disk's boot code, and adds `L:CopperShell` so CopperStart
uses the existing guest Shell and its disk-library loader. The receipt pins
the Shell and compiler inputs alongside the GUI inputs. Kickstart 1.3 uses its
normal DOS startup on the same disk. Both guest paths still require replay
verification; the presence of the prerequisites does not establish a reader.

```powershell
python scripts/probes/UtilityStartup/prepare_sysinfo_desktop.py `
  --original media/utilities/originals/sysinfo_4.4.adf `
  --provider-root path/to/native-provider `
  --installer path/to/native-provider/CopperGui `
  --output artifacts/sysinfo-with-native-gui.adf
```

The [ProTracker investigation](../../../docs/PROTRACKER_STARTUP_2026-10-28.md)
records paired API evidence and native RAM equivalence against the normal runner.

`prepare_ppaint_hdf.py` creates a new local 16 MiB hardfile containing the
complete pinned Personal Paint 7.1c archive, its libraries in `Libs:`, and the
same manifest-checked native GUI/Shell prerequisites. Pass `--archive`, `--base`
(the collected SysInfo OFS disk supplies standard boot code), `--provider-root`,
`--shell-root`, and a new `--output` path. Every file is read back byte-for-byte;
the sidecar records each file's hash and origin. Never redistribute this image.

`--filesystem ofs` is the default native 1.3 setup. CopperStart's archived
NativeCopperHdfBoot selects DOS1/FFS hardfiles, so its setup uses
`--filesystem ffs` and the read-only runner arguments
`--hdf-ffs image.hdf DH0:1:32:0:1023`. OFS and FFS twins retain identical logical
files; their disk bytes and boot filesystem are separately identified. An OFS
image not selected by CopperStart is a setup mismatch, not a paint-code fault.

Optional `--math-root` installs `MathFfpNative` and `CopperFfp` from the qualified
producer `NativeMathFfpModuleTests` output, checking `mathffp.json`. This reuses
the existing native FFP kernel and retains its guest-loaded code until reset.
The installer preserves an existing native library. The FFP component test
is not a paint-application pass.

Optional `--mathtrans-root` installs CopperOS's development 68000
`mathtrans.library`. The recipe checks its CPU, name, image length and SHA-256
against `mathtrans.library.json` and records the manifest. Freeze both files
before use if another build is active. This is an explicit test prerequisite,
not a claim of full mathtrans compatibility. Optional `--diskfont` installs an
unchanged user-supplied HUNK library and records its file hash.

Optional `--workbench` accepts a user-supplied Workbench 1.3 ADF or ZIP containing
one ADF. Its files are installed before the controlled startup is written.
Amiga path collisions are resolved case-insensitively. Without complete system
libraries, the image is only a prerequisite diagnostic; an error requester is
not a paint-screen pass. `--direct-launch` uses `SYS:PPaint/PPaint` with the same
installed files, avoiding the Workbench Assign/CD startup sequence. Preserve
the original sequence's result separately: Workbench 1.3's BCPL Assign fails
under the pinned CopperStart ROM before PPaint starts. Use `--hdf-ro`
when replaying the image and record an explicit expanded RAM profile; the
manual requires at least 2 MiB. The original 1 MiB lane is not adequate.

The external instruction probe also accepts `a500-ocs-3m-v1` (512 KiB Chip,
512 KiB slow, 2 MiB Fast) and an optional seventh argument `hdf-ro` or
`hdf-ffs`. The latter uses the explicit `DH0:1:32:0:1023` partition geometry;
both modes open media read-only. Existing five/six-argument ADF invocations
retain their profiles. These are correctness diagnostics, not throughput
acceptance protocols. The [Personal Paint control receipt](../../../docs/UTILITY_PPAINT_CONTROL_2026-10-30.json)
records native editor evidence and both unresolved CopperStart failures.

`COPPERSTART_PROBE_STOP_SET_FUNCTION=1` records the guest registers and stops
at an observed Exec `JSR d16(A6)` SetFunction call. Without that switch the
same call inputs are recorded and execution continues. This observation covers
that instruction form, not arbitrary indirect/tail calls. The diskfont case
shows why caller-side evidence matters: the Shell passed InitResident's success
value 1 as a library base even though diskfont had published a valid library.
