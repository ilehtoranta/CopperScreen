using System.Reflection;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;

// Host-only, bounded snapshots. No CPU bus reads, engine callbacks or hot-path logging.
internal sealed class NativeBootProbe
{
    private readonly string _directory;
    private readonly bool _continueUnsupported;
    private readonly int _interval;
    private readonly bool _extended;
    private bool _sawUnsupported;
    private string? _lastVideoUnsupported;
    private bool _wroteCpuProfile;

    internal NativeBootProbe(string directory, bool continueUnsupported, int interval = 60, bool extended = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval);
        _directory = Path.GetFullPath(directory);
        _continueUnsupported = continueUnsupported;
        _interval = interval;
        _extended = extended;
        Directory.CreateDirectory(_directory);
    }

    internal void ExecuteFrame(LightweightA500Machine machine, bool last)
    {
        if (!_wroteCpuProfile && machine.CpuModel != Copper68k.M68kCpuModel.M68000)
        {
            File.WriteAllText(Path.Combine(_directory, "cpu-profile.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1, cpuModel = machine.CpuModel.ToString(), experimental = true,
                nativeClocksPerMotherboardClock = machine.CpuModel == Copper68k.M68kCpuModel.M68060 ? 8 : machine.CpuModel == Copper68k.M68kCpuModel.M68040 ? 4 : 2,
                motherboardBusBits = 16,
                instructionTiming = machine.CpuModel is Copper68k.M68kCpuModel.M68040 or Copper68k.M68kCpuModel.M68060 ? "approximate-fixed" : "approximate-operand-shape",
                timingPolicy = "ocs-accelerator-v1",
                copper68k = typeof(Copper68k.M68kCpuState).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            }));
            _wroteCpuProfile = true;
        }
        try { machine.ExecuteFrame(); }
        catch
        {
            Capture(machine);
            throw;
        }
        var videoUnsupported = Field<LightweightVideo>(machine, "_video").UnsupportedActiveFeature;
        if (last || machine.CompletedFrames == 1 || machine.CompletedFrames % _interval == 0 ||
            videoUnsupported != _lastVideoUnsupported ||
            (!_sawUnsupported && machine.UnsupportedActiveFeature is not null))
            Capture(machine);
        _lastVideoUnsupported = videoUnsupported;
        _sawUnsupported |= machine.UnsupportedActiveFeature is not null;
        if (_sawUnsupported && !_continueUnsupported)
            throw new InvalidOperationException(machine.UnsupportedActiveFeature);
    }

    private void Capture(LightweightA500Machine m)
    {
        var registers = Field<LightweightRegisters>(m, "_registers");
        var drives = Field<LightweightFloppyDrive[]>(m, "_floppies");
        var drive = drives[0];
        var stem = Path.Combine(_directory, $"frame-{m.CompletedFrames:D6}");
        var state = new
        {
            frame = m.CompletedFrames, cycle = m.Cycle, beam = new[] { m.BeamLine, m.BeamColorClock },
            width = m.FramebufferWidth, height = m.FramebufferHeight,
            pc = $"{m.Cpu.ProgramCounter:X8}", sr = $"{m.Cpu.StatusRegister:X4}", overlay = m.RomOverlayEnabled,
            data = m.Cpu.D.Select(v => $"{v:X8}").ToArray(), address = m.Cpu.A.Select(v => $"{v:X8}").ToArray(),
            dmacon = $"{m.Dmacon:X4}", intena = $"{m.Intena:X4}", intreq = $"{m.Intreq:X4}",
            adkcon = $"{m.Adkcon:X4}", dsklen = $"{registers.Read(0x024):X4}",
            copperPc = $"{m.CopperProgramCounter:X8}", copperWaiting = m.CopperWaiting,
            blitterBusy = m.BlitterBusy, diskActive = m.DiskDmaActive, diskRemaining = m.DiskDmaRemaining,
            diskBit = m.DiskBitPosition, drive.Cylinder, drive.Head, drive.Selected, drive.MotorOn, drive.ReadyCycle,
            drives = drives.Select((d, i) => new
            {
                index = i, d.Mounted, d.Cylinder, d.Head, d.Selected, d.MotorOn,
                d.ReadyCycle, d.DiskChanged, d.BitPosition, d.NextBitCycle
            }).ToArray(),
            ciaAMask = m.CiaAInterruptMask, ciaAPending = m.CiaAPendingInterrupts,
            ciaBMask = m.CiaBInterruptMask, ciaBPending = m.CiaBPendingInterrupts,
            bplcon0 = $"{registers.Read(0x100):X4}", diwstart = $"{registers.Read(0x08E):X4}",
            diwstop = $"{registers.Read(0x090):X4}", ddfstart = $"{registers.Read(0x092):X4}",
            ddfstop = $"{registers.Read(0x094):X4}",
            bitplanes = Enumerable.Range(0, 6).Select(p => $"{registers.GetBitplanePointer(p):X8}").ToArray(),
            modulos = new[] { registers.Read(0x108), registers.Read(0x10A) },
            unsupported = m.UnsupportedActiveFeature,
            videoUnsupported = Field<LightweightVideo>(m, "_video").UnsupportedActiveFeature,
            audioNonzero = m.AudioSamples.Span.IndexOfAnyExcept((short)0) >= 0
        };
        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(stem + ".json", json);
        File.WriteAllBytes(stem + ".chipram", m.ChipRam.ToArray());
        if (!m.FastRam.IsEmpty)
        {
            File.WriteAllBytes(stem + ".fastram", m.FastRam.ToArray());
            File.WriteAllText(stem + ".memory.json", JsonSerializer.Serialize(new
                { fastRamBytes = m.FastRam.Length, fastRamBase = m.FastRamBase }));
        }
        WriteBitmap(stem + ".bmp", m.Framebuffer.Span, m.FramebufferWidth, m.FramebufferHeight);
        if (_extended)
        {
            File.WriteAllBytes(stem + ".slowram", Field<byte[]>(m, "_slowRam"));
            // Signed 16-bit little-endian interleaved stereo, one captured field.
            using var audio = new BinaryWriter(File.Create(stem + ".pcm"));
            foreach (var sample in m.AudioSamples.Span) audio.Write(sample);
        }
        Console.WriteLine(json);
    }

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static void WriteBitmap(string path, ReadOnlySpan<int> pixels, int width, int height)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0x4D42); writer.Write(54 + pixels.Length * 4);
        writer.Write(0); writer.Write(54); writer.Write(40);
        writer.Write(width); writer.Write(-height); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixels.Length * 4); writer.Write(0); writer.Write(0);
        writer.Write(0); writer.Write(0);
        foreach (var pixel in pixels) writer.Write(pixel);
    }
}
