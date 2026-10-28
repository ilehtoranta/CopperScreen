using System.Diagnostics;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// User-local launch preferences, separate from shipped and user-saved profiles.
internal sealed class CopperScreenRecentSession
{
    private const int MaximumRecentDisks = 8;
    private readonly string _directory;
    private readonly List<string> _recentDisks = [];
    private readonly Dictionary<string, NativeRom> _kickstartRoms = [];
    private readonly Dictionary<string, ModelDisks> _modelDisks = [];

    public CopperScreenRecentSession(string directory)
    {
        _directory = CopperScreenUserData.GetDirectory(directory);
        try
        {
            if (!File.Exists(HistoryPath)) return;
            var history = JsonSerializer.Deserialize<History>(File.ReadAllText(HistoryPath));
            if (history?.Version != 1) return;
            Volume = double.IsFinite(history.Volume) ? Math.Clamp(history.Volume, 0, 100) : 100;
            Muted = history.Muted;
            foreach (var path in (history.RecentDisks ?? []).Reverse()) RememberDisk(path);
            foreach (var entry in history.KickstartRoms ?? [])
                if (Enum.TryParse<CopperScreenMachineModel>(entry.Key, out var model) && Enum.IsDefined(model) &&
                    entry.Value is { } rom && IsNative(rom.Source) && Enum.IsDefined(rom.Version) && !string.IsNullOrWhiteSpace(rom.Path))
                    _kickstartRoms[model.ToString()] = rom;
            foreach (var entry in history.ModelDisks ?? [])
                if (Enum.TryParse<CopperScreenMachineModel>(entry.Key, out var model) && Enum.IsDefined(model) &&
                    entry.Value is { Paths.Length: 4, WriteProtected.Length: 4, Types.Length: 4, DriveCount: >= 1 and <= 4 } disks)
                    _modelDisks[model.ToString()] = disks;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { LastError = $"Could not read saved preferences: {ex.Message}"; Debug.WriteLine(LastError); }
    }

    public static string DefaultDirectory => CopperScreenUserData.DefaultDirectory;
    public string DataDirectory => _directory;
    public string? LastError { get; private set; }
    private string MachinePath => Path.Combine(_directory, "last-machine.json");
    private string HistoryPath => Path.Combine(_directory, "recent-session.json");
    public double Volume { get; private set; } = 100;
    public bool Muted { get; private set; }
    public IReadOnlyList<string> RecentDisks => _recentDisks;

    public CopperScreenSettingsDraft? LoadMachine(string baseDirectory)
    {
        if (!File.Exists(MachinePath)) return null;
        if (!CopperScreenProfile.TryLoad(MachinePath, baseDirectory, out var profile, out var error, _directory))
        { LastError = error; return null; }
        var draft = CopperScreenSettingsDraft.FromProfile(profile);
        // Older preferences only remembered the last machine. Seed a missing
        // per-model default without overwriting a newer explicit ROM choice.
        if (!_kickstartRoms.ContainsKey(CopperScreenMachinePresets.Identify(draft).ToString()))
            RememberKickstart(draft, baseDirectory);
        if (!_modelDisks.ContainsKey(CopperScreenMachinePresets.Identify(draft).ToString()))
            RememberDisks(draft, baseDirectory);
        // Retain broken paths so the launcher can explain and repair them. Clearing
        // a missing CopperOS disk would silently turn a saved workspace into ROM-only boot.
        return draft;
    }

    public void RememberDisk(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _recentDisks.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        _recentDisks.Insert(0, path);
        if (_recentDisks.Count > MaximumRecentDisks) _recentDisks.RemoveRange(MaximumRecentDisks, _recentDisks.Count - MaximumRecentDisks);
    }

    public void Save(CopperScreenSettingsDraft? machine, double volume, bool muted, string? baseDirectory = null)
    {
        Volume = double.IsFinite(volume) ? Math.Clamp(volume, 0, 100) : 100;
        Muted = muted;
        try
        {
            Directory.CreateDirectory(_directory);
            if (machine != null) TryRememberKickstart(machine, baseDirectory ?? AppContext.BaseDirectory);
            if (machine != null) TryRememberDisks(machine, baseDirectory ?? AppContext.BaseDirectory);
            if (machine != null) RememberConfiguration(machine, baseDirectory ?? AppContext.BaseDirectory);
            if (machine != null) CopperScreenProfileStore.SaveToPath(machine, MachinePath, baseDirectory: baseDirectory);
            CopperScreenUserData.WriteAtomically(HistoryPath, JsonSerializer.Serialize(new History { Volume = Volume, Muted = Muted, RecentDisks = _recentDisks.ToArray(), KickstartRoms = _kickstartRoms, ModelDisks = _modelDisks }));
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { LastError = $"Could not save preferences: {ex.Message} Check your user-data folder permissions."; Debug.WriteLine(LastError); }
    }

    public void RememberKickstart(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        if (TryRememberKickstart(draft, baseDirectory)) Save(null, Volume, Muted);
    }

    private string ConfigurationPath(int choice) => Path.Combine(_directory, "MachineDefaults", $"model-{choice}.json");

    public void RememberConfiguration(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        var choice = CopperScreenLaunchWorkflow.StartChoice(draft);
        if (choice < 0) return;
        CopperScreenProfileStore.SaveToPath(draft, ConfigurationPath(choice), baseDirectory: baseDirectory);
    }

    public CopperScreenSettingsDraft RestoreConfiguration(CopperScreenSettingsDraft fallback, string baseDirectory)
    {
        var choice = CopperScreenLaunchWorkflow.StartChoice(fallback);
        if (choice < 0 || !File.Exists(ConfigurationPath(choice))) return fallback;
        if (!CopperScreenProfile.TryLoad(ConfigurationPath(choice), baseDirectory, out var profile, out var error, _directory))
            throw new InvalidDataException(error ?? "Could not restore the saved machine configuration.");
        var restored = CopperScreenSettingsDraft.FromProfile(profile);
        if (CopperScreenLaunchWorkflow.StartChoice(restored) != choice)
            throw new InvalidDataException("The saved machine configuration belongs to a different Amiga model.");
        return restored;
    }

    public void RememberDisks(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        if (TryRememberDisks(draft, baseDirectory)) Save(null, Volume, Muted);
    }

    private bool TryRememberDisks(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        if (!IsNative(draft.KickstartSource)) return false;
        try
        {
            var options = draft.ToStartupOptions(baseDirectory);
            _modelDisks[CopperScreenMachinePresets.Identify(draft).ToString()] = new(
                options.DriveDiskPaths.ToArray(), draft.DriveWriteProtected.ToArray(), draft.FloppyDriveTypes.ToArray(),
                draft.FloppyDriveCount, options.HardDrives.ToArray());
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        { return false; }
    }

    public CopperScreenSettingsDraft RestoreDisks(CopperScreenSettingsDraft draft, bool clearIfMissing = true)
    {
        if (!IsNative(draft.KickstartSource)) return draft;
        if (!clearIfMissing && !_modelDisks.ContainsKey(CopperScreenMachinePresets.Identify(draft).ToString())) return draft;
        Array.Clear(draft.DriveDiskPaths);
        Array.Fill(draft.DriveWriteProtected, true);
        Array.Clear(draft.FloppyDriveTypes);
        draft.FloppyDriveCount = 1;
        draft.HardDrives.Clear();
        if (_modelDisks.TryGetValue(CopperScreenMachinePresets.Identify(draft).ToString(), out var disks))
        {
            disks.Paths.CopyTo(draft.DriveDiskPaths, 0);
            disks.WriteProtected.CopyTo(draft.DriveWriteProtected, 0);
            disks.Types.CopyTo(draft.FloppyDriveTypes, 0);
            draft.FloppyDriveCount = disks.DriveCount;
            draft.HardDrives.AddRange(disks.HardDrives ?? []);
        }
        return draft;
    }

    private sealed record ModelDisks(string?[] Paths, bool?[] WriteProtected, LightweightFloppyDriveType[] Types,
        int DriveCount, CopperScreenHardfileSettings[] HardDrives);

    private bool TryRememberKickstart(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        if (!IsNative(draft.KickstartSource) || string.IsNullOrWhiteSpace(draft.KickstartRomPath)) return false;
        try
        {
            var path = draft.ToStartupOptions(baseDirectory).KickstartRomPath;
            if (string.IsNullOrWhiteSpace(path)) return false;
            var model = CopperScreenMachinePresets.Identify(draft);
            var rom = new NativeRom(path, draft.KickstartSource, draft.RomVersion);
            if (_kickstartRoms.GetValueOrDefault(model.ToString()) == rom) return false;
            _ = CopperScreenKickstartRomArchive.ReadNativeRom(path, rom.Source, rom.Version, model == CopperScreenMachineModel.Amiga1200);
            _kickstartRoms[model.ToString()] = rom;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        { return false; } // Invalid selections never replace a working default.
    }

    public CopperScreenSettingsDraft RestoreKickstart(CopperScreenSettingsDraft draft)
    {
        if (IsNative(draft.KickstartSource) && _kickstartRoms.TryGetValue(CopperScreenMachinePresets.Identify(draft).ToString(), out var rom))
        {
            draft.KickstartRomPath = rom.Path;
            draft.KickstartSource = rom.Source;
            draft.RomVersion = rom.Version;
        }
        return draft;
    }

    private static bool IsNative(CopperScreenKickstartSource source) => source is
        CopperScreenKickstartSource.Kickstart13Rom or CopperScreenKickstartSource.Kickstart31Rom or CopperScreenKickstartSource.KickstartRom;

    private sealed record NativeRom(string Path, CopperScreenKickstartSource Source, KickstartVersion Version);

    // Restore changes the selected machine, not volume/mute or recent floppy
    // history. Persist this single atomic record before committing the UI draft.
    // Let failure reach the restore transaction so it can discard its new copies.
    internal void RememberRestoredMachine(CopperScreenSettingsDraft machine, string baseDirectory)
    {
        CopperScreenProfileStore.SaveToPath(machine, MachinePath, baseDirectory: baseDirectory);
        LastError = null;
    }

    private sealed class History
    {
        public int Version { get; set; } = 1;
        public double Volume { get; set; } = 100;
        public bool Muted { get; set; }
        public string[]? RecentDisks { get; set; }
        public Dictionary<string, NativeRom>? KickstartRoms { get; set; }
        public Dictionary<string, ModelDisks>? ModelDisks { get; set; }
    }
}
