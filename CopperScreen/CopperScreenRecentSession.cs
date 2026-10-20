using System.Diagnostics;
using System.Text.Json;

namespace CopperScreen;

// User-local launch preferences, separate from shipped and user-saved profiles.
internal sealed class CopperScreenRecentSession
{
    private const int MaximumRecentDisks = 8;
    private readonly string _directory;
    private readonly List<string> _recentDisks = [];

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
            if (machine != null) CopperScreenProfileStore.SaveToPath(machine, MachinePath, baseDirectory: baseDirectory);
            CopperScreenUserData.WriteAtomically(HistoryPath, JsonSerializer.Serialize(new History { Volume = Volume, Muted = Muted, RecentDisks = _recentDisks.ToArray() }));
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { LastError = $"Could not save preferences: {ex.Message} Check your user-data folder permissions."; Debug.WriteLine(LastError); }
    }

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
    }
}
