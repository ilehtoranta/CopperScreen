namespace CopperScreen;

// Desktop setup only. ROM execution and workspace writes remain on Lightweight.
internal static class CopperScreenLaunchWorkflow
{
    public static readonly string[] StartChoices = ["Amiga 500", "Amiga 500+", "Amiga 1200", "Amiga 1200 with CopperOS HDF"];

    public static int StartChoice(CopperScreenSettingsDraft draft)
    {
        var model = CopperScreenMachinePresets.Identify(draft);
        if (draft.KickstartSource == CopperScreenKickstartSource.CopperStart)
            return model == CopperScreenMachineModel.Amiga1200 ? 3 : -1;
        if (draft.KickstartSource is CopperScreenKickstartSource.MinimalDiskBoot or CopperScreenKickstartSource.DiagRom) return -1;
        return (int)model;
    }

    public static CopperScreenSettingsDraft SelectStartChoice(CopperScreenSettingsDraft current, int choice)
    {
        if (choice < 0 || choice >= StartChoices.Length) throw new ArgumentOutOfRangeException(nameof(choice));
        var draft = CopperScreenMachinePresets.Apply(current, (CopperScreenMachineModel)Math.Min(choice, 2));
        draft.Id = choice == 3 ? "my-a1200-copperos" : "my-" + new[] { "a500", "a500-plus", "a1200" }[choice];
        draft.DisplayName = StartChoices[choice];
        draft.SourcePath = null;
        draft.RomVersion = choice switch { 0 => KickstartVersion.Kickstart13, 1 => KickstartVersion.Kickstart20, _ => KickstartVersion.Kickstart30 };
        draft.KickstartSource = choice switch { 0 => CopperScreenKickstartSource.Kickstart13Rom, 3 => CopperScreenKickstartSource.CopperStart, _ => CopperScreenKickstartSource.KickstartRom };
        if (current.RomVersion != draft.RomVersion || current.KickstartSource != draft.KickstartSource ||
            CopperScreenMachinePresets.Identify(current) != CopperScreenMachinePresets.Identify(draft)) draft.KickstartRomPath = null;
        draft.HardDrives.Clear();
        draft.HostDirectories.Clear();
        if (choice == 3 || current.KickstartSource == CopperScreenKickstartSource.CopperStart)
        {
            Array.Clear(draft.DriveDiskPaths);
            Array.Fill(draft.DriveWriteProtected, true);
        }
        if (choice == 3)
        {
            draft.FloppyDriveCount = 1;
            draft.Input = draft.Input.WithPortAssignment(1, CopperScreenControllerProfile.Mouse.Id)
                .WithPortAssignment(2, CopperScreenControllerProfile.None.Id);
        }
        return draft;
    }

    public static string RomGuidance(CopperScreenSettingsDraft draft) => draft.KickstartSource == CopperScreenKickstartSource.CopperStart
        ? draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa
            ? "CopperOS and its boot ROM are included. A personal hard disk is created automatically on first use. No ROM or floppy disk selection is needed."
            : "Saved CopperStart profile: requires its qualified A500 ROM. Use Configure Amiga for this setup."
        : draft.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot
            ? "Saved minimal-boot profile: requires an ADF. Select an Amiga above to boot with a Kickstart ROM and no disk."
        : draft.RomVersion switch
        {
            KickstartVersion.Kickstart20 => "Required: A500+ Kickstart 2.04 ROM (v37.175, 512 KiB). A floppy disk is optional; without one, Amiga starts at the insert-disk screen.",
            KickstartVersion.Kickstart30 => "Required: A1200 Kickstart 3.0 ROM (v39.106, 512 KiB). A floppy disk is optional; without one, Amiga starts at the insert-disk screen.",
            KickstartVersion.Kickstart31 => "Required: A500 Kickstart 3.1 ROM (v40.63, 512 KiB). A floppy disk is optional.",
            _ => "Required: Kickstart 1.3 ROM (v34, 256 KiB). A floppy disk is optional; without one, Amiga starts at the insert-disk screen."
        };

    public static CopperScreenSettingsDraft SelectCopperOsHdf(CopperScreenSettingsDraft current, string path)
    {
        // This simple launcher uses the qualified CopperOS image geometry. Other
        // partition layouts remain configurable through the hard-disk settings.
        using var file = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[4];
        file.ReadExactly(signature);
        if (file.Length != 8 * 1024 * 1024 || !signature.SequenceEqual("DOS\u0001"u8))
            throw new InvalidDataException("Choose the CopperOS 8 MiB FFS HDF. For other hard-disk layouts, use Configure Amiga.");
        var draft = current.Clone();
        draft.HardDrives.Clear();
        draft.HardDrives.Add(CopperScreenHardfileSettings.Ffs(0, path, true, "DH0:2:32:0:255"));
        return draft;
    }
    public const string ContentGuidance = "Choose your Amiga. Native Amiga options need your own Kickstart ROM; floppy disks are optional. CopperOS includes everything needed to start.";
    public const string CopperOsGuidance = "Choose the matching CopperStart ROM first, then create a workspace from the qualified CopperOS system image. Your original stays unchanged. Resume a workspace to keep its files and settings. Uninstalling removes app data; use Export workspace to keep a copy elsewhere.";
    public const string GuestHelp = "CopperOS HDF opens its desktop after around 25 seconds. Choose Files, Editor or About; Tab/arrows select and Enter opens. Older CopperOS floppy previews start at a Shell prompt, where Demo runs the demonstration. Click the display to type; F10 releases captured input. Pause, Restart and Stop are above the display.";

    public static CopperScreenSettingsDraft CopperStart(CopperScreenSettingsDraft current)
    {
        var draft = CopperScreenMachinePresets.Apply(current, CopperScreenMachineModel.Amiga500);
        draft.Id = "my-copperstart";
        draft.DisplayName = "My CopperStart preview";
        draft.SourcePath = null;
        draft.KickstartSource = CopperScreenKickstartSource.CopperStart;
        draft.RomVersion = KickstartVersion.Kickstart13;
        draft.FloppyDriveCount = 1;
        draft.HardDrives.Clear(); draft.HostDirectories.Clear();
        Array.Clear(draft.DriveDiskPaths);
        Array.Fill(draft.DriveWriteProtected, true);
        if (current.KickstartSource != CopperScreenKickstartSource.CopperStart) draft.KickstartRomPath = null;
        return draft;
    }

    public static CopperScreenSettingsDraft Workspace(CopperScreenSettingsDraft current, string userData, string id, string? seed)
    {
        // Validate firmware before creating user data. Never use an alternate ROM or fallback.
        _ = CopperScreenCopperStartRom.Read(current.KickstartRomPath);
        var image = CopperOsWorkspace.Prepare(userData, id, seed);
        CopperOsWorkspace.ValidateOwnership(image);
        using (CopperOsWorkspace.Open(image)) { } // Check readability, integrity and exclusive ownership now.
        var draft = CopperStart(current);
        draft.Id = "copperos-" + id;
        draft.DisplayName = "CopperOS · " + id;
        draft.DriveDiskPaths[0] = image;
        draft.DriveWriteProtected[0] = false;
        // A game profile's WASD bindings must not swallow Shell command text.
        draft.Input = draft.Input.WithPortAssignment(1, CopperScreenControllerProfile.Mouse.Id)
            .WithPortAssignment(2, CopperScreenControllerProfile.None.Id);
        return draft;
    }

    public static string[] Workspaces(string userData)
    {
        var root = Path.Combine(userData, "CopperOS", "Workspaces");
        return Directory.Exists(root) ? Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName).OfType<string>().Where(n => !n.StartsWith('.'))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];
    }

    public static string? DiagnosticRom(string baseDirectory)
    {
#if COPPERSCREEN_INSTALLED_TESTS
        var path = Path.Combine(baseDirectory, "Diagnostics", "GuestProof", "CopperStart-DiskBoot.rom");
        return File.Exists(path) ? path : null;
#else
        return null; // Shipping admission is explicit; never discover arbitrary local proof assets.
#endif
    }
    public static bool IsWorkspacePath(string? path, string userData)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var root = Path.GetFullPath(Path.Combine(userData, "CopperOS", "Workspaces")) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(path).Equals("System.adf", StringComparison.OrdinalIgnoreCase);
    }
    public static string? DiagnosticSeed(string baseDirectory)
    {
#if COPPERSCREEN_INSTALLED_TESTS
        var path = Path.Combine(baseDirectory, "Diagnostics", "GuestProof", "System.adf");
        return File.Exists(path) ? path : null;
#else
        return null;
#endif
    }
}
