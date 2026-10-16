using System.Buffers.Binary;
using System.Security.Cryptography;
using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightNativeDiskFormatTests
{
    [NativeDiskFormatTheory]
    [InlineData("adf", true)]
    [InlineData("adz", true)]
    [InlineData("dms", true)]
    [InlineData("extended", true)]
    [InlineData("raw", true)]
    [InlineData("raw-adz", true)]
    [InlineData("scp", true)]
    [InlineData("adz", false)]
    [InlineData("dms", false)]
    [InlineData("extended", false)]
    [InlineData("raw", false)]
    [InlineData("raw-adz", false)]
    [InlineData("scp", false)]
    public void NativeKickstartBootsOriginalDiagnosticFromAdditionalFormats(string format, bool batch)
    {
        var rom = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART13_ROM")!);
        Assert.Equal("EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53", Convert.ToHexString(SHA256.HashData(rom)));
        var sectors = new byte[901120]; "DOS"u8.CopyTo(sectors);
        // Original boot code writes a marker and loops.
        ushort[] code = [0x23FC, 0x4469, 0x736B, 0x0007, 0x0000, 0x60FE];
        for (var i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(sectors.AsSpan(12 + i * 2), code[i]);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var previous = sum; sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(sectors.AsSpan(i)));
            if (sum < previous) sum = unchecked(sum + 1);
        }
        BinaryPrimitives.WriteUInt32BigEndian(sectors.AsSpan(4), ~sum);
        var track = AmigaDosTrackEncoder.EncodeTrack(new AdfDiskMedia(sectors), 0, 0);
        var bytes = format switch
        {
            "adf" => sectors,
            "adz" => DiskFormatFixture.Adz(sectors),
            "dms" => DiskFormatFixture.Dms(sectors),
            "extended" => DiskFormatFixture.Extended(sectors),
            "raw" => DiskFormatFixture.Extended(sectors, track, track.Length * 8),
            "raw-adz" => DiskFormatFixture.Adz(DiskFormatFixture.Extended(sectors, track, track.Length * 8)),
            _ => DiskFormatFixture.ScpEncoded(track)
        };
        var extension = format is "extended" or "raw" ? "adf" : format == "raw-adz" ? "adz" : format;
        using var machine = new LightweightA500Machine(new(), batch);
        machine.LoadKickstart(rom); machine.MountDisk(0, bytes, "diagnostic." + extension); machine.Reset();
        for (var frame = 0; frame < 1000 && BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x70000..]) != 0x4469736B; frame++)
            machine.ExecuteFrame();
        Assert.True(BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x70000..]) == 0x4469736B,
            $"Boot diagnostic was not reached: format={format}, PC=${machine.Cpu.ProgramCounter:X8}, DMA={machine.DiskDmaActive}, cylinder={machine.GetDriveState(0).Cylinder}.");
        Assert.Null(machine.UnsupportedActiveFeature);
        Assert.True(machine.IsDriveWriteProtected(0)); Assert.False(machine.IsDriveDirty(0));
    }
}

public sealed class NativeDiskFormatTheoryAttribute : TheoryAttribute
{
    public NativeDiskFormatTheoryAttribute()
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART13_ROM");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Skip = "Native format boot coverage unavailable: supply COPPERSCREEN_KICKSTART13_ROM (A500 34.5).";
    }
}
