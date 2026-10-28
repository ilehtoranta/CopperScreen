namespace CopperScreen;

internal enum CopperScreenMachineModel { Amiga500, Amiga500Ecs, Amiga1200 }

// Desktop presets only; the session validator remains the hardware authority.
internal static class CopperScreenMachinePresets
{
    public static string Label(CopperScreenMachineModel model) => model switch
    {
        CopperScreenMachineModel.Amiga500 => "Amiga 500",
        CopperScreenMachineModel.Amiga500Ecs => "Amiga 500+",
        CopperScreenMachineModel.Amiga1200 => "Amiga 1200",
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };

    public static CopperScreenMachineModel Identify(CopperScreenSettingsDraft draft)
        => draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa || draft.Chipset.DmaChip == DmaChipModel.AgaAlice
            ? CopperScreenMachineModel.Amiga1200
            : draft.Chipset.DisplayChip == DisplayChipModel.EcsDenise || draft.Chipset.DmaChip != DmaChipModel.OcsAgnus
                ? CopperScreenMachineModel.Amiga500Ecs : CopperScreenMachineModel.Amiga500;

    public static CopperScreenSettingsDraft Apply(CopperScreenSettingsDraft current, CopperScreenMachineModel model)
    {
        var draft = current.Clone();
        draft.DisplayName = Label(model) + " PAL";
        draft.Engine = CopperScreenEngine.Lightweight;
        draft.Chipset = model switch
        {
            CopperScreenMachineModel.Amiga500 => AmigaChipset.OcsPal,
            CopperScreenMachineModel.Amiga500Ecs => new(DmaChipModel.Agnus8375Pal2M, DisplayChipModel.EcsDenise, VideoStandard.Pal),
            CopperScreenMachineModel.Amiga1200 => new(DmaChipModel.AgaAlice, DisplayChipModel.AgaLisa, VideoStandard.Pal),
            _ => throw new ArgumentOutOfRangeException(nameof(model))
        };
        draft.ChipRamKb = model == CopperScreenMachineModel.Amiga500 ? 512 : model == CopperScreenMachineModel.Amiga500Ecs ? 1024 : 2048;
        draft.PseudoFastRamKb = model == CopperScreenMachineModel.Amiga500 ? 512 : 0;
        draft.PseudoFastBase = "$C00000";
        draft.RealFastRamKb = 0;
        draft.RealFastBase = "$200000";
        draft.RtgVramMb = 0;
        draft.RtcEnabled = model == CopperScreenMachineModel.Amiga500Ecs;
        draft.CpuBackend = model == CopperScreenMachineModel.Amiga1200 ? M68kBackendKind.AccurateM68EC020 : M68kBackendKind.AccurateM68000;
        draft.RomVersion = model == CopperScreenMachineModel.Amiga1200 ? KickstartVersion.Kickstart30
            : current.RomVersion is KickstartVersion.Kickstart20 or KickstartVersion.Kickstart31 ? current.RomVersion : KickstartVersion.Kickstart13;
        draft.KickstartSource = current.KickstartSource == CopperScreenKickstartSource.CopperStart ? CopperScreenKickstartSource.CopperStart
            : model == CopperScreenMachineModel.Amiga1200 ? CopperScreenKickstartSource.KickstartRom
            : model == CopperScreenMachineModel.Amiga500 && current.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot && draft.HardDrives.Count == 0 && draft.HostDirectories.Count == 0
                ? CopperScreenKickstartSource.MinimalDiskBoot
                : draft.RomVersion == KickstartVersion.Kickstart20 ? CopperScreenKickstartSource.KickstartRom
                : draft.RomVersion == KickstartVersion.Kickstart31 ? CopperScreenKickstartSource.Kickstart31Rom : CopperScreenKickstartSource.Kickstart13Rom;
        draft.MarkRequiresRestart();
        return draft;
    }

    public static bool CanUseSlowRam(CopperScreenSettingsDraft draft)
        => draft.ChipRamKb == 512 && draft.Chipset.DmaChip is DmaChipModel.OcsAgnus or DmaChipModel.Agnus8372A;

    public static bool CanUseFastRam(CopperScreenSettingsDraft draft)
        => draft.KickstartSource is not (CopperScreenKickstartSource.MinimalDiskBoot or CopperScreenKickstartSource.CopperStart);

    public static string[] CpuChoices(CopperScreenSettingsDraft draft)
        => draft.KickstartSource == CopperScreenKickstartSource.CopperStart ? Identify(draft) == CopperScreenMachineModel.Amiga1200 ? ["AccurateM68EC020"] : ["AccurateM68000"]
            : Identify(draft) == CopperScreenMachineModel.Amiga1200
            ? ["AccurateM68EC020", "AccurateM68020", "AccurateM68030", "AccurateM68040", "JitM68040"]
            : ["AccurateM68000", "AccurateM68010", "AccurateM68EC020", "AccurateM68020", "AccurateM68030", "AccurateM68040", "JitM68040"];

    public static int MaximumFastRamKb(M68kBackendKind cpu)
        => CopperMod.Amiga.Lightweight.LightweightA500Configuration.MaximumFastRamBytes(CopperScreenLightweightSession.GetCpuModel(cpu)) / 1024;

    public static int[] FastRamChoices(CopperScreenSettingsDraft draft)
        => !CanUseFastRam(draft) ? [0]
            : new[] { 0, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576 }
                .Where(size => size <= MaximumFastRamKb(draft.CpuBackend)).ToArray();

    public static void SetFastRam(CopperScreenSettingsDraft draft, int kib)
    {
        if (!FastRamChoices(draft).Contains(kib)) throw new ArgumentOutOfRangeException(nameof(kib), "Select a supported Fast RAM size for this CPU and boot method.");
        draft.RealFastRamKb = kib;
        draft.RealFastBase = CopperScreenSettingsDraft.FormatAddress(kib == 0 ? 0x200000 : CopperScreenDefaults.GetDefaultFastRamBase(checked(kib * 1024)));
    }

    public static void SetCpu(CopperScreenSettingsDraft draft, M68kBackendKind cpu)
    {
        if (!CpuChoices(draft).Contains(cpu.ToString())) throw new ArgumentOutOfRangeException(nameof(cpu), "Select a supported CPU for this Amiga model.");
        draft.CpuBackend = cpu;
        // A downgrade cannot retain RAM outside the CPU's address space.
        SetFastRam(draft, Math.Min(draft.RealFastRamKb, CanUseFastRam(draft) ? MaximumFastRamKb(cpu) : 0));
    }

    public static bool CanUseMinimalBoot(CopperScreenSettingsDraft draft)
        => draft.Chipset == AmigaChipset.OcsPal && draft.ChipRamKb == 512 && draft.CpuBackend == M68kBackendKind.AccurateM68000
            && draft.RealFastRamKb == 0 && draft.HardDrives.Count == 0 && draft.HostDirectories.Count == 0;
}
