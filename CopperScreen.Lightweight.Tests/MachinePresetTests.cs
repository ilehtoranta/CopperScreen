using CopperScreen;

namespace CopperScreen.Tests;

public sealed class MachinePresetTests
{
    [Fact]
    public void FreshStartupDefaultsToAmiga500()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(CopperScreenStartupOptions.Parse([], AppContext.BaseDirectory));
        Assert.Equal(CopperScreenMachineModel.Amiga500, CopperScreenMachinePresets.Identify(draft));
        Assert.Equal(512, draft.ChipRamKb);
        Assert.Equal(M68kBackendKind.AccurateM68000, draft.CpuBackend);
    }

    [Theory]
    [InlineData(0, 512)]
    [InlineData(1, 1024)]
    [InlineData(2, 2048)]
    public void EveryPresetSurvivesProfileRoundTripAndPassesSessionValidation(int value, int chipRam)
    {
        var model = (CopperScreenMachineModel)value;
        var draft = CopperScreenMachinePresets.Apply(new(), model);
        var restored = CopperScreenSettingsDraft.FromProfile(draft.ToProfile());
        Assert.Equal(model, CopperScreenMachinePresets.Identify(restored));
        Assert.Equal(chipRam, restored.ChipRamKb);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(restored, AppContext.BaseDirectory));
    }

    [Fact]
    public void ChangingModelsPreservesMediaAndHostSettingsWithoutMutatingCommittedMachine()
    {
        var original = new CopperScreenSettingsDraft { KickstartRomPath = "my.rom", FloppyDriveCount = 4, RealFastRamKb = 4096 };
        original.Input = original.Input.WithPortAssignment(2, "none");
        original.DriveDiskPaths[3] = "four.adf";
        original.DriveWriteProtected[3] = false;
        var changed = CopperScreenMachinePresets.Apply(original, CopperScreenMachineModel.Amiga1200);
        Assert.Equal("my.rom", changed.KickstartRomPath);
        Assert.Equal(4, changed.FloppyDriveCount);
        Assert.Equal("four.adf", changed.DriveDiskPaths[3]);
        Assert.False(changed.DriveWriteProtected[3]);
        Assert.Equal(original.Input, changed.Input);
        Assert.True(changed.NeedsRestartComparedWith(original));
        Assert.Equal(4096, original.RealFastRamKb);
        Assert.Equal(512, original.ChipRamKb);
        Assert.Equal(0, changed.RealFastRamKb);
        Assert.Equal(KickstartVersion.Kickstart30, changed.RomVersion);
    }

    [Fact]
    public void SwitchingBackFromA1200RestoresSupportedA500CpuAndRomRequirements()
    {
        var aga = CopperScreenMachinePresets.Apply(new(), CopperScreenMachineModel.Amiga1200);
        var a500 = CopperScreenMachinePresets.Apply(aga, CopperScreenMachineModel.Amiga500);
        Assert.Equal(M68kBackendKind.AccurateM68000, a500.CpuBackend);
        Assert.Equal(KickstartVersion.Kickstart13, a500.RomVersion);
        Assert.Equal(CopperScreenKickstartSource.Kickstart13Rom, a500.KickstartSource);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(a500, AppContext.BaseDirectory));
    }

    [Fact]
    public void OptionalMemoryEligibilityTracksHardwareAndBootMethod()
    {
        var draft = new CopperScreenSettingsDraft { PseudoFastRamKb = 0, RealFastRamKb = 2048, RtcEnabled = true };
        Assert.True(CopperScreenMachinePresets.CanUseSlowRam(draft));
        Assert.True(CopperScreenMachinePresets.CanUseFastRam(draft));
        Assert.False(CopperScreenMachinePresets.CanUseMinimalBoot(draft));
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
        draft.RealFastRamKb = 0;
        Assert.True(CopperScreenMachinePresets.CanUseMinimalBoot(draft));
        draft.KickstartSource = CopperScreenKickstartSource.MinimalDiskBoot;
        Assert.False(CopperScreenMachinePresets.CanUseFastRam(draft));
        var ecs = CopperScreenMachinePresets.Apply(draft, CopperScreenMachineModel.Amiga500Ecs);
        Assert.False(CopperScreenMachinePresets.CanUseSlowRam(ecs));
        Assert.True(CopperScreenMachinePresets.CanUseFastRam(ecs));
        Assert.NotEqual(CopperScreenKickstartSource.MinimalDiskBoot, ecs.KickstartSource);
        var aga = CopperScreenMachinePresets.Apply(ecs, CopperScreenMachineModel.Amiga1200);
        Assert.False(CopperScreenMachinePresets.CanUseSlowRam(aga));
        Assert.True(CopperScreenMachinePresets.CanUseFastRam(aga));
        Assert.False(CopperScreenMachinePresets.CanUseMinimalBoot(aga));
    }
}
