using System.Buffers.Binary;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class AgnusChipRamTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-agnus-" + Guid.NewGuid().ToString("N"));

    public AgnusChipRamTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("OcsAgnus", 512, 512, "8371", LightweightAgnusModel.Mos8371)]
    [InlineData("OcsAgnus", 512, 0, "8371", LightweightAgnusModel.Mos8371)]
    [InlineData("Agnus8372A", 512, 512, "8372a", LightweightAgnusModel.Mos8372A)]
    [InlineData("Agnus8372A", 1024, 0, "8372a", LightweightAgnusModel.Mos8372A)]
    [InlineData("Agnus8375Pal2M", 1024, 0, "8375-318069-10", LightweightAgnusModel.Mos8375Pal2M)]
    [InlineData("Agnus8375Pal2M", 2048, 0, "8375-318069-10", LightweightAgnusModel.Mos8375Pal2M)]
    public void ModelAndBanksSurviveSaveLoadSessionAndReset(string model, int chip, int slow, string serialized, LightweightAgnusModel expected)
    {
        var bytes = new byte[524288];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xF80100);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), 40);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), 63);
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        var rom = Path.Combine(_directory, "synthetic.rom");
        File.WriteAllBytes(rom, bytes);
        var draft = new CopperScreenSettingsDraft
        {
            Id = "agnus-test", Chipset = new(Enum.Parse<DmaChipModel>(model), DisplayChipModel.OcsDenise, VideoStandard.Pal),
            ChipRamKb = chip, PseudoFastRamKb = slow, KickstartRomPath = rom,
            KickstartSource = CopperScreenKickstartSource.Kickstart31Rom, RomVersion = KickstartVersion.Kickstart31
        };
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var path = CopperScreenProfileStore.Save(draft, _directory);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(serialized, json.RootElement.GetProperty("Machine").GetProperty("Agnus").GetString());
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var profile, out var error), error);
        var restored = CopperScreenSettingsDraft.FromProfile(profile);
        Assert.Equal(draft.Chipset, restored.Chipset);
        Assert.Equal(chip, restored.ChipRamKb); Assert.Equal(slow, restored.PseudoFastRamKb);
        using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(_directory));
        Assert.Equal(expected, session.Machine.AgnusModel);
        Assert.Equal(chip * 1024, session.Machine.ChipRam.Length);
        session.RenderNextFrame(session.Framebuffer);
        session.Reset();
        Assert.Null(session.FaultMessage);
        Assert.Equal(expected, session.Machine.AgnusModel);
        Assert.Equal(chip * 1024, session.Machine.ChipRam.Length);
    }

    [Theory]
    [InlineData("OcsAgnus", 1024, 0)]
    [InlineData("Agnus8372A", 2048, 0)]
    [InlineData("Agnus8372A", 1024, 512)]
    [InlineData("Agnus8375Pal2M", 2048, 512)]
    [InlineData("EcsAgnus", 1024, 0)]
    public void AvailabilityRejectsImpossibleOrUnspecifiedModels(string model, int chip, int slow)
    {
        var draft = new CopperScreenSettingsDraft
        {
            Chipset = new(Enum.Parse<DmaChipModel>(model), DisplayChipModel.OcsDenise, VideoStandard.Pal),
            ChipRamKb = chip, PseudoFastRamKb = slow
        };
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    [Fact]
    public void SettingsChoicesAndRestartBoundaryFollowTheHardware()
    {
        Assert.Equal(new[] { "512" }, CopperScreenAvailability.ChipRamChoices(DmaChipModel.OcsAgnus));
        Assert.Equal(new[] { "512", "1024" }, CopperScreenAvailability.ChipRamChoices(DmaChipModel.Agnus8372A));
        Assert.Equal(new[] { "1024", "2048" }, CopperScreenAvailability.ChipRamChoices(DmaChipModel.Agnus8375Pal2M));
        Assert.False(CopperScreenAvailability.IsChoiceAvailable("Agnus", "EcsAgnus"));
        var active = new CopperScreenSettingsDraft();
        var changed = active.Clone();
        changed.Chipset = changed.Chipset with { DmaChip = DmaChipModel.Agnus8372A };
        Assert.True(changed.NeedsRestartComparedWith(active));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
