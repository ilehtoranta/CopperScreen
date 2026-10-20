namespace CopperScreen;

// Desktop setup only. ROM execution and workspace writes remain on Lightweight.
internal static class CopperScreenLaunchWorkflow
{
    public const string ContentGuidance = "This build includes the emulator and machine presets. Native Kickstart ROMs, Workbench and games are not included; choose files you own. CopperStart and CopperOS are experimental development previews. This release does not include their boot media.";
    public const string CopperOsGuidance = "Choose the matching CopperStart ROM first, then create a workspace from the qualified CopperOS system image. Your original stays unchanged. Resume a workspace to keep its files and settings. Uninstalling removes app data; use Export workspace to keep a copy elsewhere.";
    public const string GuestHelp = "CopperOS preview: wait for the Shell prompt, then type Demo and press Enter to run the included demonstration. Try Echo hello to print a message. Click the display to type; F10 releases captured input. Pause, Restart and Stop are above the display.";

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
