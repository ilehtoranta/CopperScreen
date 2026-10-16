"""Copy the current engine/runner into ignored artifacts for bounded raster tracing.

No production files or emulated ordering are changed. Run prepare_replay.py first.
"""
import argparse
import shutil
from pathlib import Path


HELPER = r'''namespace CopperMod.Amiga.Lightweight;
public sealed partial class LightweightA500Machine
{
    private bool _kickoffCopperWrite;
    private int _kickoffFetches, _kickoffNonzero;
    private readonly int[] _kickoffCounts = new int[8];
    private readonly StreamWriter? _kickoffTrace = Environment.GetEnvironmentVariable("KICKOFF_TRACE") is {} path ? new StreamWriter(path) : null;
    internal void KickoffTraceFetch(int plane, ulong value)
    { _kickoffFetches++; if(value != 0) _kickoffNonzero++; _kickoffCounts[plane]++; }
    private void KickoffTraceWrite(ushort offset, ushort value, long cycle)
    {
        if(_kickoffTrace == null || _completedFrames < 8800 || _completedFrames >= 15000) return;
        if(offset is >= 0x080 and <= 0x09A || offset is >= 0x0E0 and <= 0x10C || offset is 0x1DC or 0x1E4 or 0x1FC || offset is >= 0x180 and <= 0x1BE)
            _kickoffTrace.WriteLine($"W,{_completedFrames+1},{cycle},{BeamLine},{BeamColorClock},{(_kickoffCopperWrite ? "COP" : "CPU")},{_cpu.State.ProgramCounter:X8},{offset:X3},{value:X4},{_copper.ProgramCounter:X8}");
    }
    private void KickoffTraceField(long cycle)
    {
        if(_kickoffTrace != null && _completedFrames >= 6000)
            _kickoffTrace.WriteLine($"F,{_completedFrames+1},{cycle},{_cpu.State.ProgramCounter:X8},{_registers.Dmacon:X4},{_registers.Read(0x100):X4},{_kickoffFetches},{_kickoffNonzero},{string.Join('/',_kickoffCounts)},{_registers.GetBitplanePointer(0):X8},{_copper.ProgramCounter:X8}");
        _kickoffFetches = _kickoffNonzero = 0; Array.Clear(_kickoffCounts);
    }
    private void KickoffTraceRaster()
    {
        if(_kickoffTrace == null || _completedFrames < 8800) return;
        var pixels = Framebuffer.Span; int lit = 0;
        for(var i=0;i<pixels.Length;i+=16) if((pixels[i] & 0xFFFFFF) != 0) lit++;
        _kickoffTrace.WriteLine($"R,{_completedFrames+1},{lit}");
        _kickoffTrace.Flush();
    }
}
'''


def replace_once(path, old, new):
    text = path.read_text(encoding="utf-8-sig")
    if text.count(old) != 1:
        raise ValueError(f"Expected exactly one instrumentation site in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[3]
    output = args.output.resolve()
    if not output.is_relative_to(repo / "artifacts"):
        parser.error("Diagnostic copies must live inside this repository's ignored artifacts directory.")
    if output.exists():
        parser.error("Use a new output directory; existing diagnostic evidence is never overwritten.")
    for source, name in [("CopperMod.Amiga.Lightweight", "engine"), ("CopperMod.Amiga.Lightweight.Runner", "runner")]:
        destination = output / name
        destination.mkdir(parents=True)
        for file in (repo / source).iterdir():
            if file.is_file():
                shutil.copy2(file, destination / file.name)
    replace_once(output / "engine/CopperMod.Amiga.Lightweight.csproj", r"..\CopperDisk\CopperDisk.csproj", str(repo / "CopperDisk/CopperDisk.csproj"))
    replace_once(output / "runner/CopperMod.Amiga.Lightweight.Runner.csproj", r"..\CopperMod.Amiga.Lightweight\CopperMod.Amiga.Lightweight.csproj", r"..\engine\CopperMod.Amiga.Lightweight.csproj")
    machine = output / "engine/LightweightA500Machine.cs"
    replace_once(machine, "private void WriteCustomRegister(ushort offset, ushort value, long cycle)\n    {", "private void WriteCustomRegister(ushort offset, ushort value, long cycle)\n    {\n        KickoffTraceWrite((ushort)(offset & 0x1FE), value, cycle);")
    replace_once(machine, "internal void OnFrameCompleted(long cycle)\n    {", "internal void OnFrameCompleted(long cycle)\n    {\n        KickoffTraceField(cycle);")
    replace_once(machine, "_unsynchronizedField = false;\n        _completedFrames++;", "KickoffTraceRaster();\n        _unsynchronizedField = false;\n        _completedFrames++;")
    replace_once(machine, "=> WriteCustomRegister(offset, value, cycle);", "{ _kickoffCopperWrite = true; WriteCustomRegister(offset, value, cycle); _kickoffCopperWrite = false; }")
    replace_once(output / "engine/LightweightBitplanes.cs", "var value = (ushort)(wide >> (bits - 16));", "machine.KickoffTraceFetch(plane, wide);\n        var value = (ushort)(wide >> (bits - 16));")
    (output / "engine/KickoffDiagnostics.cs").write_text(HELPER, encoding="utf-8")


if __name__ == "__main__":
    main()
