using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopperDisk.FileSystem;

namespace CopperScreen;

internal sealed record CopperScreenWhdLoadSetup(string Rom, string Workbench, string Loader, string Kickstarts, bool Aga)
{
    private static string SettingsPath(string userData) => Path.Combine(userData, "WHDLoad", "setup.json");

    internal static CopperScreenWhdLoadSetup? Load(string userData)
    {
        var path = SettingsPath(userData);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<CopperScreenWhdLoadSetup>(File.ReadAllText(path)); }
        catch (JsonException) { return null; }
    }

    internal void Save(string userData) => CopperScreenUserData.WriteAtomically(SettingsPath(userData), JsonSerializer.Serialize(this));
}

// Offline guest setup. Native Kickstart and the existing shared-folder handler own execution.
internal sealed class CopperScreenWhdLoadLaunch : IDisposable
{
    private readonly DirectoryInfo _staging = Directory.CreateTempSubdirectory("CopperScreen-WHDLoad-");
    private FileStream? _saveLease;
    private string _gameRoot = null!;
    private string _inputPath = null!;
    internal IReadOnlyList<string> Slaves { get; private set; } = [];

    internal static bool IsGame(string? path) => path != null &&
        (Path.GetExtension(path).Equals(".slave", StringComparison.OrdinalIgnoreCase) || IsLha(path));

    private static bool IsLha(string path) => Path.GetExtension(path).Equals(".lha", StringComparison.OrdinalIgnoreCase);

    internal static async Task<CopperScreenWhdLoadLaunch> OpenAsync(string path, CancellationToken cancellation = default)
    {
        path = Path.GetFullPath(path);
        if (!IsGame(path)) throw new ArgumentException("Choose an installed .slave or a .lha archive containing the game.");
        var launch = new CopperScreenWhdLoadLaunch { _inputPath = path };
        try
        {
            launch._gameRoot = Path.GetDirectoryName(path)!;
            if (IsLha(path))
            {
                launch._gameRoot = Path.Combine(launch._staging.FullName, "Game");
                var files = await CopperScreenLhaArchive.ExtractAsync(path, launch._gameRoot, cancellation).ConfigureAwait(false);
                launch.Slaves = files
                    .Where(file => Path.GetExtension(file).Equals(".slave", StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase).Select(launch.RelativeName).ToArray();
            }
            else launch.Slaves = [launch.RelativeName(path)];
            if (launch.Slaves.Count == 0) throw new InvalidDataException("The LhA archive contains no WHDLoad slave.");
            return launch;
        }
        catch { launch.Dispose(); throw; }
    }

    internal async Task<CopperScreenSettingsDraft> PrepareAsync(CopperScreenSettingsDraft current, CopperScreenWhdLoadSetup setup,
        string selectedSlave, string? dataDrawer, string userData, CancellationToken cancellation = default)
    {
        if (!Slaves.Contains(selectedSlave, StringComparer.Ordinal)) throw new ArgumentException("Choose a slave from the selected game.");
        var slave = Path.Combine(_gameRoot, selectedSlave);
        var game = Path.GetDirectoryName(slave)!;
        var name = Path.GetFileName(slave);
        CopperScreenLhaArchive.ValidateGuestName(name);
        var slaveArg = ShellArgument(name);
        RequireHunk(slave, isSlave: true);
        var dataArg = ResolveDataDrawer(game, dataDrawer);
        var rom = CopperScreenKickstartRomArchive.ReadNativeRom(setup.Rom, KickstartVersion.Kickstart31);
        var version = CopperScreenKickstartRomArchive.IdentifyNativeVersion(rom);
        if (version is not (KickstartVersion.Kickstart20 or KickstartVersion.Kickstart30 or KickstartVersion.Kickstart31) ||
            (setup.Aga && version == KickstartVersion.Kickstart20))
            throw new NotSupportedException("Choose native Kickstart 2.04, 3.0 or 3.1 for Amiga 500, or Kickstart 3.0 or 3.1 for Amiga 1200.");

        var system = Path.Combine(_staging.FullName, "System");
        StageWorkbench(setup.Workbench, system, cancellation);
        await StageLoaderAsync(setup.Loader, system, cancellation).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(setup.Kickstarts)) StageKickstarts(setup.Kickstarts, system, cancellation);

        var saves = Path.Combine(userData, "WHDLoad", "Saves");
        CopperScreenAtomicFile.ValidateDestination(saves);
        Directory.CreateDirectory(saves);
        var identity = OperatingSystem.IsWindows() ? _inputPath.ToUpperInvariant() : _inputPath;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + "\n" + selectedSlave)));
        var saveFolder = Path.Combine(saves, key);
        var lockPath = saveFolder + ".lock";
        CopperScreenAtomicFile.ValidateDestination(saveFolder);
        CopperScreenAtomicFile.ValidateDestination(lockPath);
        _saveLease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Directory.CreateDirectory(saveFolder);

        var dataOption = dataArg == null ? "" : " Data=" + dataArg;
        var setupPatches = FindChild(FindChild(system, "C")!, "SetPatch") != null ? "C:SetPatch QUIET\n" : "";
        var startup = setupPatches + "FailAt 21\nMakeDir RAM:ENV\nMakeDir RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\n" +
            "Stack 16384\nCD GAME0:\n" +
            $"SYS:C/WHDLoad Slave={slaveArg}{dataOption} SavePath=SAVE0: SaveDir=Game Preload NoNetwork NoReq NoWriteCache QuitKey=88\n" +
            "If WARN\nEcho \"WHDLoad returned an error. Check the message above.\"\nElse\nCD SYS:\nLoadWB\nEndCLI >NIL:\nEndIf\n";
        var scripts = FindChild(system, "S") ?? Directory.CreateDirectory(Path.Combine(system, "S")).FullName;
        var startupPath = FindChild(scripts, "Startup-Sequence") ?? Path.Combine(scripts, "Startup-Sequence");
        File.WriteAllText(startupPath, startup, Encoding.Latin1);

        var draft = CopperScreenMachinePresets.Apply(current, setup.Aga ? CopperScreenMachineModel.Amiga1200 : CopperScreenMachineModel.Amiga500);
        draft.Id = "whdload-game";
        draft.DisplayName = Path.GetFileNameWithoutExtension(name);
        draft.Description = string.Empty;
        draft.SourcePath = null;
        draft.KickstartSource = CopperScreenKickstartSource.KickstartRom;
        draft.KickstartRomPath = setup.Rom;
        draft.RomVersion = version;
        draft.ChipRamKb = 2048;
        draft.PseudoFastRamKb = 0;
        if (!setup.Aga)
        {
            draft.Chipset = new(DmaChipModel.Agnus8375Pal2M, DisplayChipModel.OcsDenise, VideoStandard.Pal);
            draft.CpuBackend = M68kBackendKind.AccurateM68010;
        }
        CopperScreenMachinePresets.SetFastRam(draft, 8192);
        draft.FloppyDriveCount = 1;
        Array.Clear(draft.DriveDiskPaths);
        Array.Fill(draft.DriveWriteProtected, true);
        draft.HardDrives.Clear();
        draft.HostDirectories.Clear();
        draft.HostDirectories.Add(new("DH0", "WHDSystem", system, ReadOnly: false, BootPriority: 0));
        draft.HostDirectories.Add(new("GAME0", "WHDGame", game, ReadOnly: true));
        draft.HostDirectories.Add(new("SAVE0", "WHDSaves", saveFolder, ReadOnly: false));
        CopperScreenLightweightSession.Validate(draft.ToStartupOptions(AppContext.BaseDirectory));
        return draft;
    }

    private string RelativeName(string path) => Path.GetRelativePath(_gameRoot, path);

    private static string? ResolveDataDrawer(string game, string? drawer)
    {
        if (string.IsNullOrWhiteSpace(drawer)) return null;
        var path = game;
        var parts = drawer.Replace('\\', '/').Split('/');
        foreach (var part in parts)
        {
            CopperScreenLhaArchive.ValidateGuestName(part);
            path = FindChild(path, part) ?? throw new DirectoryNotFoundException($"Data drawer '{drawer}' was not found beside the slave.");
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
                throw new IOException("Choose an ordinary game data drawer beside the slave.");
        }
        return ShellArgument(string.Join('/', parts));
    }

    private static string ShellArgument(string value)
    {
        // AmigaDOS expands variables and backtick commands even in quoted arguments.
        if (value.Length == 0 || value.Any(c => c < 32 || c > 255 || c is '"' or '*' or '$' or '`' or ':' or ';' or ','))
            throw new InvalidDataException("Choose a slave and data drawer with names that can be passed safely to the Amiga Shell.");
        return '"' + value + '"';
    }

    private static void StageWorkbench(string path, string system, CancellationToken cancellation)
    {
        if (!Path.GetExtension(path).Equals(".adf", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length is not (901120 or 1802240))
            throw new InvalidDataException("Choose the Workbench system disk as a standard ADF.");
        Directory.CreateDirectory(system);
        using var image = DiskImageSession.Open(path);
        if (image.Volumes.Count != 1) throw new InvalidDataException("Choose the Workbench system floppy.");
        var volume = image.Volumes[0];
        volume.Mount(cancellation);
        var entries = volume.ReadDirectory(volume.Root, cancellation);
        DiskTransfer.Extract(volume, entries, system, cancellationToken: cancellation);
        var commands = FindChild(system, "C");
        if (commands == null || new[] { "Assign", "MakeDir", "LoadWB" }.Any(command => FindChild(commands, command) == null))
            throw new InvalidDataException("The ADF does not contain the Workbench startup commands. Choose the Workbench system disk.");
    }

    private async Task StageLoaderAsync(string path, string system, CancellationToken cancellation)
    {
        if (IsLha(path))
        {
            var files = await CopperScreenLhaArchive.ExtractAsync(path, Path.Combine(_staging.FullName, "Loader"), cancellation).ConfigureAwait(false);
            var candidates = files.Where(file => Path.GetFileName(file).Equals("WHDLoad", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1) throw new InvalidDataException("Choose an official WHDLoad user package containing one WHDLoad executable.");
            path = candidates[0];
        }
        RequireHunk(path, isSlave: false);
        var commands = FindChild(system, "C")!;
        File.Copy(path, FindChild(commands, "WHDLoad") ?? Path.Combine(commands, "WHDLoad"), overwrite: true);
        var vfs = FindChild(Path.GetDirectoryName(path)!, "WHDLoad.VFS");
        if (vfs != null && File.Exists(vfs))
            File.Copy(vfs, FindChild(commands, "WHDLoad.VFS") ?? Path.Combine(commands, "WHDLoad.VFS"), overwrite: true);
    }

    private static void StageKickstarts(string path, string system, CancellationToken cancellation)
    {
        var devs = FindChild(system, "Devs") ?? Directory.CreateDirectory(Path.Combine(system, "Devs")).FullName;
        var kickstarts = FindChild(devs, "Kickstarts") ?? Directory.CreateDirectory(Path.Combine(devs, "Kickstarts")).FullName;
        foreach (var file in new DirectoryInfo(path).EnumerateFiles())
        {
            cancellation.ThrowIfCancellationRequested();
            if (!file.Name.StartsWith("kick", StringComparison.OrdinalIgnoreCase)) continue;
            CopperScreenLhaArchive.ValidateGuestName(file.Name);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length > 2 * 1024 * 1024)
                throw new InvalidDataException("Choose ordinary Kickstart image and relocation files in the Kickstarts drawer.");
            File.Copy(file.FullName, FindChild(kickstarts, file.Name) ?? Path.Combine(kickstarts, file.Name), overwrite: true);
        }
    }

    private static string? FindChild(string folder, string name) => Directory.Exists(folder)
        ? Directory.EnumerateFileSystemEntries(folder).SingleOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
        : null;

    private static void RequireHunk(string path, bool isSlave)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> prefix = stackalloc byte[512];
        var bytes = prefix[..input.ReadAtLeast(prefix, prefix.Length, false)];
        if (!bytes.StartsWith(new byte[] { 0, 0, 3, 0xf3 }) || isSlave && bytes.IndexOf("WHDLOADS"u8) < 0)
            throw new InvalidDataException(isSlave ? "The selected file is not a WHDLoad slave executable." : "Choose the Amiga WHDLoad executable or its user package.");
    }

    public void Dispose()
    {
        _saveLease?.Dispose();
        try { _staging.Delete(recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { System.Diagnostics.Trace.WriteLine($"Could not remove the temporary WHDLoad session: {ex.Message}"); }
    }
}
