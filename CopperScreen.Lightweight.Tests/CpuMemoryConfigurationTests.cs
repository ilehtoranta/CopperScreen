using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CpuMemoryConfigurationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryAvailableCpuAndMemoryChoicePassesTheSessionValidator(int value)
    {
        var draft = CopperScreenMachinePresets.Apply(new(), (CopperScreenMachineModel)value);
        if (value != 2) { draft.KickstartSource = CopperScreenKickstartSource.Kickstart31Rom; draft.RomVersion = KickstartVersion.Kickstart31; }
        foreach (var cpu in CopperScreenMachinePresets.CpuChoices(draft))
        {
            CopperScreenMachinePresets.SetCpu(draft, Enum.Parse<M68kBackendKind>(cpu));
            foreach (var size in CopperScreenMachinePresets.FastRamChoices(draft))
            {
                CopperScreenMachinePresets.SetFastRam(draft, size);
                var restored = draft.Clone();
                Assert.Equal(size, restored.RealFastRamKb);
                Assert.Null(CopperScreenAvailability.GetUnavailableReason(restored, AppContext.BaseDirectory));
            }
        }
    }

    [Fact]
    public void DowngradingTo24BitCpuClampsRamAndRestoresAutoconfigHint()
    {
        var draft = CopperScreenMachinePresets.Apply(new(), CopperScreenMachineModel.Amiga1200);
        CopperScreenMachinePresets.SetCpu(draft, M68kBackendKind.JitM68040);
        CopperScreenMachinePresets.SetFastRam(draft, 1048576);
        Assert.Equal("$40000000", draft.RealFastBase);
        var committed = draft.Clone();
        CopperScreenMachinePresets.SetCpu(draft, M68kBackendKind.AccurateM68EC020);
        Assert.Equal(8192, draft.RealFastRamKb);
        Assert.Equal("$200000", draft.RealFastBase);
        Assert.True(draft.NeedsRestartComparedWith(committed));
        Assert.Throws<ArgumentOutOfRangeException>(() => CopperScreenMachinePresets.SetFastRam(draft, 16384));
        Assert.Throws<ArgumentOutOfRangeException>(() => CopperScreenMachinePresets.SetCpu(draft, M68kBackendKind.AccurateM68000));
    }

    [Fact]
    public void OversizedAndUnbootableRamCannotBypassValidationViaProfiles()
    {
        var draft = new CopperScreenSettingsDraft { RealFastRamKb = 16384, RealFastBase = "$10000000" };
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
        draft.CpuBackend = M68kBackendKind.AccurateM68020;
        Assert.Contains("Kickstart 3", CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
        draft.RomVersion = KickstartVersion.Kickstart31; draft.KickstartSource = CopperScreenKickstartSource.Kickstart31Rom;
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
        draft.RealFastRamKb = 12288;
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
    }
}
