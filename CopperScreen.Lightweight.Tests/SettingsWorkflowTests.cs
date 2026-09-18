using CopperScreen;

namespace CopperScreen.Tests;

public sealed class SettingsWorkflowTests
{
    [Theory]
    [InlineData("Engine", "Lightweight", true)]
    [InlineData("Engine", "Legacy", false)]
    [InlineData("CPU backend", "AccurateM68000", true)]
    [InlineData("CPU backend", "JitM68040", false)]
    [InlineData("Kickstart", "CopperStart", false)]
    [InlineData("Kickstart", "DiagRom", false)]
    [InlineData("Connected", "1", true)]
    [InlineData("Connected", "2", true)]
    [InlineData("Connected", "3", true)]
    [InlineData("Connected", "4", true)]
    [InlineData("Connected", "5", false)]
    public void FutureChoicesHaveExplicitAvailability(string setting, string value, bool available)
        => Assert.Equal(available, CopperScreenAvailability.IsChoiceAvailable(setting, value));

    [Fact]
    public void MouseRemainsAvailableOnlyOnTheSupportedPort()
    {
        Assert.True(CopperScreenAvailability.IsControllerAvailable(1, CopperScreenControllerKind.Mouse));
        Assert.False(CopperScreenAvailability.IsControllerAvailable(2, CopperScreenControllerKind.Mouse));
        Assert.True(CopperScreenAvailability.IsControllerAvailable(2, CopperScreenControllerKind.KeyboardJoystick));
    }

    [Fact]
    public void ProfileAvailabilityDoesNotRequireTheUserToHaveSelectedARom()
    {
        var draft = new CopperScreenSettingsDraft();
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
        draft.RtgVramMb = 16;
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
    }

    [Fact]
    public void ProfileCatalogRetainsUnsupportedMachinesWithAnAvailabilityReason()
    {
        var profiles = CopperScreenProfileStore.ListProfiles(AppContext.BaseDirectory);
        Assert.Contains(profiles, profile => profile.Id == "lightweight-a500-kickstart13" && profile.IsAvailable);
        var future = Assert.Single(profiles, profile => profile.Id == "expanded-m68040-jit-kickstart31-rtg");
        Assert.False(future.IsAvailable);
        Assert.Contains("Not yet available", future.ToString());
        Assert.DoesNotContain(future.Path, future.ToString());
    }

    [Fact]
    public void EditingAndDiscardingADraftDoesNotChangeCommittedSettings()
    {
        var committed = new CopperScreenSettingsDraft { KickstartRomPath = "original.rom" };
        committed.DriveDiskPaths[0] = "original.adf";
        var edit = committed.Clone();
        edit.KickstartRomPath = "replacement.rom";
        edit.DriveDiskPaths[0] = "replacement.adf";
        edit.DriveWriteProtected[0] = false;
        edit.Input = edit.Input.WithPortAssignment(2, "none");
        Assert.Equal("original.rom", committed.KickstartRomPath);
        Assert.Equal("original.adf", committed.DriveDiskPaths[0]);
        Assert.Null(committed.DriveWriteProtected[0]);
        Assert.NotEqual(committed.Input.Port2ProfileId, edit.Input.Port2ProfileId);
        var reopened = committed.Clone();
        Assert.Equal("original.adf", reopened.DriveDiskPaths[0]);
    }

    [Fact]
    public void HostAndMediaChangesCanApplyButRomAndHardwareChangesNeedRestart()
    {
        var committed = new CopperScreenSettingsDraft();
        var edit = committed.Clone();
        edit.DisplayName = "My Amiga";
        edit.DriveDiskPaths[0] = "another.adf";
        edit.Input = edit.Input.WithPortAssignment(2, "none");
        edit.PresentationOptions = new(CopperScreenLacedPresentationMode.StableWeave, CopperScreenPixelAspectMode.CrtCorrect);
        Assert.False(edit.NeedsRestartComparedWith(committed));
        edit.KickstartRomPath = "different.rom";
        Assert.True(edit.NeedsRestartComparedWith(committed));
        edit = committed.Clone();
        edit.ChipRamKb = 1024;
        Assert.True(edit.NeedsRestartComparedWith(committed));
    }

    [Fact]
    public void HostGainPreservesFullVolumeScalesStereoAndMutesQueuedSamples()
    {
        float[] original = [-0.5f, 0.25f, 1f, -1f];
        var samples = original.ToArray();
        CopperScreenAudioGain.Apply(samples, 1);
        Assert.Equal(original, samples);
        CopperScreenAudioGain.Apply(samples, 0.5f);
        Assert.Equal(new[] { -0.25f, 0.125f, 0.5f, -0.5f }, samples);
        CopperScreenAudioGain.Apply(samples, 0);
        Assert.All(samples, sample => Assert.Equal(0, sample));
        Assert.Throws<ArgumentOutOfRangeException>(() => CopperScreenAudioGain.Validate(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => CopperScreenAudioGain.Validate(1.1f));
    }
}
