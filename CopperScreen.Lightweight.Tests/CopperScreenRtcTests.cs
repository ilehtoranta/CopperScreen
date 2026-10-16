using System.Buffers.Binary;
using Copper68k;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenRtcTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-rtc-" + Guid.NewGuid().ToString("N"));
    private readonly string _rom;

    public CopperScreenRtcTests()
    {
        Directory.CreateDirectory(_directory);
        _rom = Path.Combine(_directory, "fixture.rom");
        var bytes = new byte[262144];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xFC0100);
        bytes[13] = 34; // Synthetic ROM, not native battery-clock evidence.
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        File.WriteAllBytes(_rom, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClockSelectionSurvivesProfileSaveAndSessionRestart(bool enabled)
    {
        var options = CopperScreenStartupOptions.Parse(
            ["--profile", "lightweight-a500-kickstart13", "--rom", _rom], AppContext.BaseDirectory);
        var initial = CopperScreenSettingsDraft.FromStartupOptions(options);
        var draft = initial.Clone();
        draft.Id = "rtc-test";
        draft.RtcEnabled = enabled;
        Assert.Equal(enabled, draft.NeedsRestartComparedWith(initial));
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var path = CopperScreenProfileStore.Save(draft, _directory);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var profile, out var error), error);
        Assert.Equal(enabled, profile.RtcEnabled);
        var reloaded = CopperScreenSettingsDraft.FromProfile(profile);
        reloaded.KickstartRomPath = _rom;
        using var previous = new CopperScreenLightweightSession(reloaded.ToStartupOptions(_directory));
        Assert.Equal(enabled, previous.Machine.RtcEnabled);
        var cycle = previous.Machine.Cycle;
        previous.Machine.WriteByte(0xDC0028, 8, ref cycle, M68kBusAccessKind.CpuDataWrite);
        previous.TogglePaused();
        using var replacement = new CopperScreenLightweightSession(reloaded.ToStartupOptions(_directory), previous);
        cycle = replacement.Machine.Cycle;
        Assert.Equal(enabled ? 8 : 0xFF,
            replacement.Machine.ReadByte(0xDC0028, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Null(replacement.FaultMessage);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ExpandedLegacyRomProfileRetainsRtcCpuAndMemoryOnLightweight()
    {
        var options = CopperScreenStartupOptions.Parse(
            ["--profile", "expanded-m68040-kickstart-rom", "--rom", _rom], AppContext.BaseDirectory);
        var roundtrip = CopperScreenSettingsDraft.FromStartupOptions(options).ToStartupOptions(_directory);
        using var session = CopperScreenSession.Create(roundtrip);
        var lightweight = Assert.IsType<CopperScreenLightweightSession>(session);
        Assert.Equal(CopperScreenEngine.Lightweight, roundtrip.Engine);
        Assert.Equal(M68kCpuModel.M68040, lightweight.Machine.CpuModel);
        Assert.True(lightweight.Machine.RtcEnabled);
        Assert.Equal(8 * 1024 * 1024, lightweight.Machine.FastRam.Length);
        Assert.Equal(2, lightweight.Machine.FloppyDriveCount);
    }
}
