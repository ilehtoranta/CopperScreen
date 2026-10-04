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
        _directory = directory;
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
        { Debug.WriteLine($"Unable to read recent session: {ex.Message}"); }
    }

    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopperScreen");
    private string MachinePath => Path.Combine(_directory, "last-machine.json");
    private string HistoryPath => Path.Combine(_directory, "recent-session.json");
    public double Volume { get; private set; } = 100;
    public bool Muted { get; private set; }
    public IReadOnlyList<string> RecentDisks => _recentDisks;

    public CopperScreenSettingsDraft? LoadMachine(string baseDirectory)
    {
        if (!File.Exists(MachinePath) || !CopperScreenProfile.TryLoad(MachinePath, baseDirectory, out var profile, out _)) return null;
        var draft = CopperScreenSettingsDraft.FromProfile(profile);
        for (var i = 0; i < draft.DriveDiskPaths.Length; i++)
            if (!CopperScreenDiskImageArchive.DiskPathExists(draft.DriveDiskPaths[i])) draft.DriveDiskPaths[i] = null;
        return draft;
    }

    public void RememberDisk(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _recentDisks.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        _recentDisks.Insert(0, path);
        if (_recentDisks.Count > MaximumRecentDisks) _recentDisks.RemoveRange(MaximumRecentDisks, _recentDisks.Count - MaximumRecentDisks);
    }

    public void Save(CopperScreenSettingsDraft? machine, double volume, bool muted)
    {
        Volume = double.IsFinite(volume) ? Math.Clamp(volume, 0, 100) : 100;
        Muted = muted;
        try
        {
            Directory.CreateDirectory(_directory);
            if (machine != null) CopperScreenProfileStore.SaveToPath(machine, MachinePath);
            var temporaryPath = HistoryPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new History { Volume = Volume, Muted = Muted, RecentDisks = _recentDisks.ToArray() }));
            File.Move(temporaryPath, HistoryPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { Debug.WriteLine($"Unable to save recent session: {ex.Message}"); }
    }

    private sealed class History
    {
        public int Version { get; set; } = 1;
        public double Volume { get; set; } = 100;
        public bool Muted { get; set; }
        public string[]? RecentDisks { get; set; }
    }
}
